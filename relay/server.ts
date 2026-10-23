import * as tls from 'node:tls';
import * as fs from 'node:fs';
import * as path from 'node:path';
import {createHash, timingSafeEqual, randomBytes} from 'node:crypto';
import {Worker} from 'node:worker_threads';
import {monitorEventLoopDelay} from 'node:perf_hooks';

type SavedMember = {label: string; host: boolean};
type Group = {room: string; owner: string; deleted?: boolean; members?: Record<string, SavedMember>};
export class GroupRegistry {
  private groups: Record<string, Group>;
  constructor(private file: string) {
    if (fs.statSync(file).size > 8000000) throw Error('Group registry too large');
    const state = JSON.parse(fs.readFileSync(file, 'utf8'));
    if (state.version !== 1 || !state.groups || typeof state.groups !== 'object' || Array.isArray(state.groups)) throw Error('Invalid group registry');
    this.groups = Object.create(null);
    for (const [id, group] of Object.entries(state.groups) as [string, Group][]) {
      if (!/^[a-f0-9]{32}$/.test(id) || !group || !/^[a-f0-9]{64}$/.test(group.room) || !/^[a-f0-9]{64}$/.test(group.owner) || group.deleted !== undefined && typeof group.deleted !== 'boolean') throw Error('Invalid group metadata');
      if (group.members !== undefined) {
        if (!group.members || typeof group.members !== 'object' || Array.isArray(group.members) || Object.keys(group.members).length > 64) throw Error('Invalid membership registry');
        for (const [token, member] of Object.entries(group.members)) if (!/^[a-f0-9]{64}$/.test(token) || !member || typeof member.host !== 'boolean' || !/^[A-Za-z0-9+/=]{64,512}$/.test(member.label)) throw Error('Invalid saved member');
      }
      this.groups[id] = group;
    }
    if (Object.keys(this.groups).length > 10000) throw Error('Too many registered groups');
  }
  allows(id: string, room: string): boolean { return this.groups[id]?.room === room && !this.groups[id]?.deleted; }
  owns(id: string, room: string, token: string): boolean {
    if (!this.allows(id, room) || !/^[a-f0-9]{64}$/.test(token)) return false;
    const owner = createHash('sha256').update('MiniMap owner v1:' + id + ':' + token).digest();
    return timingSafeEqual(Buffer.from(this.groups[id].owner, 'hex'), owner);
  }
  roster(id: string, room: string): Record<string, SavedMember> {
    return this.allows(id, room) ? {...this.groups[id].members} : {};
  }
  summary() { const groups = Object.values(this.groups);return {registered: groups.length, active: groups.filter(g => !g.deleted).length, rememberedMembers: groups.reduce((sum,g) => sum + Object.keys(g.members ?? {}).length,0)}; }
  private queue: Promise<unknown> = Promise.resolve();
  private worker?: Worker;
  private failure?: Error;
  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.queue.then(operation);
    this.queue = result.catch(() => {});return result;
  }
  private async write(id: string, group: Group): Promise<void> {
    if (this.failure) throw this.failure;
    if (!this.worker) {
      this.worker = new Worker(`
        const {parentPort,workerData}=require('node:worker_threads'),fs=require('node:fs'),path=require('node:path');
        let groups=JSON.parse(fs.readFileSync(workerData,'utf8')).groups;
        parentPort.on('message',({id,group})=>{
          try {
            const next={...groups,[id]:group},text=JSON.stringify({version:1,groups:next});
            if(Buffer.byteLength(text)>8000000)throw Error('Registry size limit');
            const fd=fs.openSync(workerData+'.tmp','w',0o600);
            try{fs.writeFileSync(fd,text);fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
            fs.renameSync(workerData+'.tmp',workerData);
            if(process.platform!=='win32'){const dir=fs.openSync(path.dirname(workerData),'r');try{fs.fsyncSync(dir);}finally{fs.closeSync(dir);}}
            groups=next;parentPort.postMessage({ok:true});
          }catch{parentPort.postMessage({ok:false});}
        });
      `, {eval: true, workerData: this.file});
      this.worker.on('error', () => { this.failure = Error('Registry worker failed'); });
      this.worker.on('exit', () => { this.failure = Error('Registry worker stopped'); });
    }
    const worker = this.worker;worker.ref();
    try {
      await new Promise<void>((resolve, reject) => {
        const clear = () => { worker.off('message', message);worker.off('error', error);worker.off('exit', exited); };
        const message = (result: {ok: boolean}) => { clear();if (result.ok) resolve();else reject(Error('Registry persistence failed')); };
        const error = () => { clear();reject(Error('Registry worker failed')); };
        const exited = () => { clear();reject(Error('Registry worker stopped')); };
        worker.once('message', message);worker.once('error', error);worker.once('exit', exited);worker.postMessage({id, group});
      });
    } catch (error) { this.failure = error as Error;throw error; }
    finally { worker.unref(); }
  }
  remember(id: string, room: string, token: string, label: string, host: boolean): Promise<boolean> {
    return this.enqueue(async () => {
      if (!this.allows(id, room) || !/^[a-f0-9]{64}$/.test(token) || !/^[A-Za-z0-9+/=]{64,512}$/.test(label)) return false;
      const group = this.groups[id], previous = group.members ?? {};
      if (!previous[token] && Object.keys(previous).length >= 64) return false;
      const next = {...group, members: {...previous, [token]: {label, host}}};
      await this.write(id, next);this.groups[id] = next;return true;
    });
  }
  change(action: string, id: string, room: string, token: string): Promise<boolean> {
    return this.enqueue(async () => {
      if (!/^[a-f0-9]{32}$/.test(id) || !/^[a-f0-9]{64}$/.test(room) || !/^[a-f0-9]{64}$/.test(token)) return false;
      const owner = createHash('sha256').update('MiniMap owner v1:' + id + ':' + token).digest('hex');
      const previous = this.groups[id];
      if (previous && !timingSafeEqual(Buffer.from(previous.owner, 'hex'), Buffer.from(owner, 'hex'))) return false;
      if (action === 'DELETE' && !previous || previous?.deleted && action !== 'DELETE' || action === 'CREATE' && previous && previous.room !== room || action === 'ROTATE' && !previous) return false;
      if (!['CREATE','ROTATE','DELETE'].includes(action) || !previous && Object.keys(this.groups).length >= 10000) return false;
      const next: Group = action === 'DELETE' ? {room: previous!.room, owner: previous!.owner, deleted: true} : {room, owner, ...(previous?.room === room && previous.members ? {members: previous.members} : {})};
      await this.write(id, next);this.groups[id] = next;return true;
    });
  }

}

// The relay never receives encryption keys or plaintext names, positions or saved markers.
// Room identifiers are independent HMAC outputs derived by each client.
export class Budget {
  private remaining = 0;
  private month = '';
  private reserved = 0;
  constructor(private file: string, private limit: number) {
    if (!Number.isSafeInteger(limit) || limit < 1) throw Error('Invalid budget');
    if (!fs.existsSync(file)) throw Error('Budget state missing; explicit operator initialization required');
    {
      const state = JSON.parse(fs.readFileSync(file, 'utf8'));
      if (!/^\d{4}-\d{2}$/.test(state.month) || !Number.isSafeInteger(state.reserved) || state.reserved < 0) throw Error('Invalid budget state');
      this.month = state.month; this.reserved = state.reserved;
    }
  }
  snapshot() { return {month:this.month,reservedBytes:this.reserved,limitBytes:this.limit}; }
  take(bytes: number): boolean {
    if (!Number.isSafeInteger(bytes) || bytes < 0) return false;
    const month = new Date().toISOString().slice(0, 7);
    if (month < this.month) throw Error('Clock moved backwards; budget locked');
    if (month !== this.month) { this.month = month; this.reserved = 0; this.remaining = 0; }
    if (bytes > this.remaining) {
      const reserve = Math.min(Math.max(bytes, 1048576), this.limit - this.reserved);
      if (reserve < bytes) return false;
      this.reserved += reserve;
      // Reserve on disk BEFORE sending. A restart forfeits unused reserved bytes.
      const temp = this.file + '.tmp';
      const fd = fs.openSync(temp, 'w', 0o600);
      try { fs.writeFileSync(fd, JSON.stringify({month: this.month, reserved: this.reserved})); fs.fsyncSync(fd); }
      finally { fs.closeSync(fd); }
      fs.renameSync(temp, this.file);
      if (process.platform !== 'win32') {
        const directory = fs.openSync(path.dirname(this.file), 'r');
        try { fs.fsyncSync(directory); } finally { fs.closeSync(directory); }
      }
      this.remaining += reserve;
    }
    this.remaining -= bytes; return true;
  }
}

export function createRelay(options: tls.TlsOptions, budget: {take(bytes: number): boolean; snapshot?(): unknown}, registry?: GroupRegistry, statusFile?: string) {
  const rooms = new Map<string, Set<tls.TLSSocket>>();
  const members = new Map<tls.TLSSocket, {id: string; label: string; host: boolean; persistent?: boolean; compact?: boolean}>();
  const sockets = new Set<tls.TLSSocket>();
  const perIp = new Map<string, number>();
  const started = Date.now(), counters: Record<string,number> = {admitted:0,rejectedCapacity:0,rejectedPerIp:0,rejectedBudget:0,rejectedRate:0,slowReceivers:0,tlsErrors:0,accountedBytes:0,forwardedBytes:0,frames:0,fatalErrors:0};
  const lag = monitorEventLoopDelay({resolution:20});lag.enable();
  let lastFailure: string | null = null;
  let failed = false;
  const fatal = (reason: string) => { if (failed) return;failed = true;lastFailure = reason;counters.fatalErrors++;server.emit('relayFatal', reason);server.close();for (const socket of sockets) socket.destroy(); };
  const announce = (room: string) => {
    const group = rooms.get(room);if (!group) return;
    const enabled = [...group].every(peer => !peer.destroyed && members.get(peer)?.compact);
    for (const peer of group) if (members.get(peer)?.compact && !peer.destroyed) {
      if (peer.writableLength+5 > 262144 || !spend(266)) { peer.destroy();continue; }
      peer.write(Buffer.from([0,0,0,1,enabled?1:0]));
    }
  };
  let windowStart = Date.now(), output = 0;
  const spend = (size: number) => {
    if (Date.now() - windowStart >= 1000) { windowStart = Date.now(); output = 0; }
    if (output + size > 8388608) { counters.rejectedRate++;return false; }
    output += size;
    try { if (!budget.take(size)) { counters.rejectedBudget++;return false; }counters.accountedBytes += size;return true; }
    catch { fatal('budget_storage_failure');return false; }
  };
  const server = tls.createServer({...options, minVersion: 'TLSv1.2', handshakeTimeout: 5000}, socket => {
    let buffer = Buffer.alloc(0), room: string | undefined, controlSession = false;
    let receivedAt = Date.now(), frames = 0, incoming = 0;
    socket.setNoDelay(true); socket.setTimeout(10000, () => socket.destroy());
    socket.on('error', () => socket.destroy());
    socket.on('close', () => {
      members.delete(socket);
      if (room) { const group = rooms.get(room); group?.delete(socket); if (!group?.size) rooms.delete(room);else announce(room); }
    });
    let processing: Promise<void> = Promise.resolve(), queuedBytes = 0;
    socket.on('data', (chunk: Buffer) => {
      queuedBytes += chunk.length;if (queuedBytes > 262144) { socket.destroy();return; }
      processing = processing.then(async () => {
      queuedBytes -= chunk.length;if (socket.destroyed) return;
      if (Date.now() - receivedAt >= 1000) { receivedAt = Date.now(); frames = 0; incoming = 0; }
      incoming += chunk.length;
      if (incoming > 262144 || buffer.length + chunk.length > 262144) { socket.destroy(); return; }
      buffer = Buffer.concat([buffer, chunk]);
      while (!room) {
        const end = buffer.indexOf(10);
        if (end < 0) { if (buffer.length > 1024) socket.destroy(); return; }
        if (end > 1024) { socket.destroy(); return; }
        const hello = buffer.subarray(0, end).toString('utf8');
        const presence = /^SCUMROSTER3 ([a-f0-9]{32}) ([a-f0-9]{64})$/.exec(hello);
        if (presence) {
          if (++frames > 16 || !registry?.allows(presence[1], presence[2])) { socket.destroy(); return; }
          controlSession = true; buffer = buffer.subarray(end + 1);
          const group = rooms.get(presence[1] + ':' + presence[2]);
          const rows = Object.entries(registry.roster(presence[1], presence[2])).map(([id, member]) => {
            const online = [...group ?? []].some(peer => !peer.destroyed && members.get(peer)?.id === id);
            return `${id} ${member.host ? '1' : '0'} ${online ? '1' : '0'} ${member.label}`;
          });
          const response = `OK\n${rows.join('\n')}${rows.length ? '\n' : ''}END\n`;
          if (!spend(Buffer.byteLength(response) * 2 + 256) || socket.writableLength + Buffer.byteLength(response) > 262144) { socket.destroy();return; }
          socket.write(response);continue;
        }
        const control = /^SCUMMEMBERS2 (LIST|KICK) ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64}) ([a-f0-9]{64})$/.exec(hello);
        if (control) {
          if (++frames > 16) { socket.destroy(); return; }
          controlSession = true;buffer = buffer.subarray(end + 1);
          const [, action, groupId, roomId, owner, target] = control;
          if (!registry?.owns(groupId, roomId, owner)) { socket.destroy(); return; }
          const group = rooms.get(groupId + ':' + roomId) ?? new Set<tls.TLSSocket>();
          if (action === 'LIST') {
            const rows = [...group].filter(peer => !peer.destroyed).map(peer => { const m = members.get(peer)!;return `${m.id} ${m.host ? '1' : '0'} ${m.label}`; });
            const response = `OK\n${rows.join('\n')}${rows.length ? '\n' : ''}END\n`;if (socket.writableLength+Buffer.byteLength(response)>262144 || !spend(Buffer.byteLength(response)*2+256)) { socket.destroy();return; }socket.write(response);
          } else {
            if (!spend(256)) { socket.destroy();return; }
            const peer = [...group].find(peer => members.get(peer)?.id === target && !members.get(peer)?.host && !peer.destroyed);
            if (!peer) socket.write('NO\n');
            else { if (members.get(peer)?.persistent) peer.end(Buffer.alloc(4));else peer.destroy();socket.write('OK\n'); }
          }
          continue;
        }
        if (controlSession) { socket.destroy(); return; }
        const management = /^SCUM(CREATE|ROTATE|DELETE)1 ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64})$/.exec(hello);
        if (management) {
          try {
            if (!registry || !spend(128) || !await registry.change(management[1], management[2], management[3], management[4])) { socket.destroy(); return; }
            if (management[1] === 'ROTATE' || management[1] === 'DELETE') for (const [name, group] of rooms) if (name.startsWith(management[2] + ':') && (management[1] === 'DELETE' || name !== management[2] + ':' + management[3])) for (const peer of group) peer.destroy();
            socket.end('OK\n');
          } catch { fatal('registry_storage_failure'); }
          return;
        }
        const persistent = /^SCUMGROUP1 ([a-f0-9]{32}) ([a-f0-9]{64})$/.exec(hello);
        const compactJoin = hello.startsWith('SCUMGROUP4 '), durableJoin = compactJoin || hello.startsWith('SCUMGROUP3 ');
        const memberJoin = /^SCUMGROUP[234] ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64}) ([A-Za-z0-9+/=]{64,512})(?: ([a-f0-9]{64}))?$/.exec(hello);
        const legacy = /^SCUMRELAY1 ([a-f0-9]{64})$/.exec(hello);
        if (memberJoin && registry?.allows(memberJoin[1], memberJoin[2]) && (memberJoin[5] === undefined || registry.owns(memberJoin[1], memberJoin[2], memberJoin[5]))) {
          room = memberJoin[1] + ':' + memberJoin[2];members.set(socket, {id: memberJoin[3], label: memberJoin[4], host: memberJoin[5] !== undefined, persistent: durableJoin, compact: compactJoin});
        }
        else if (persistent && registry?.allows(persistent[1], persistent[2])) room = persistent[1] + ':' + persistent[2];
        else if (legacy && !registry) room = legacy[1]; // Synthetic/legacy test server only.
        else { socket.destroy(); return; }
        const group = rooms.get(room) ?? new Set<tls.TLSSocket>();
        if (group.size >= 9 || (!rooms.has(room) && rooms.size >= 32)) { socket.destroy(); return; }
        if (!members.has(socket)) members.set(socket, {id: randomBytes(32).toString('hex'),label: '-',host: false});
        if ([...group].some(peer => members.get(peer)?.id === members.get(socket)?.id)) { socket.destroy(); return; }
        if (durableJoin && memberJoin) {
          try { if (!await registry!.remember(memberJoin[1], memberJoin[2], memberJoin[3], memberJoin[4], memberJoin[5] !== undefined)) { socket.destroy();return; } }
          catch { fatal('registry_storage_failure');return; }
        }
        if (failed || socket.destroyed) return;
        const current = rooms.get(room) ?? group;
        if (current.size >= 9 || (!rooms.has(room) && rooms.size >= 32) || [...current].some(peer => members.get(peer)?.id === members.get(socket)?.id)) { socket.destroy();return; }
        current.add(socket); rooms.set(room, current); buffer = buffer.subarray(end + 1);
        if (!spend(128)) { socket.destroy(); return; }
        socket.write('OK\n');announce(room);
      }
      while (buffer.length >= 4) {
        const rawSize = buffer.readUInt32BE(0), compact = !!(rawSize & 0x80000000), size = rawSize & 0x7fffffff;
        if (compact && !members.get(socket)?.compact) { socket.destroy();return; }
        if (size < 64 || size > 131072 || (size - 48) % 16 !== 0) { socket.destroy(); return; }
        if (buffer.length < size + 4) return;
        if (++frames > 3) { socket.destroy(); return; }
        counters.frames++;
        const packet = buffer.subarray(0, size + 4); buffer = buffer.subarray(size + 4);
        const recipients = rooms.get(room) ?? new Set<tls.TLSSocket>();
        // During an old-client join, discard in-flight compact frames until new
        // clients receive the fallback signal and resume complete protocol-3 packets.
        if (compact && [...recipients].some(peer => !members.get(peer)?.compact)) continue;
        for (const peer of recipients) {
          // Echo is also the transport heartbeat for a member alone in a room.
          if (peer.destroyed) continue;
          // Never build unbounded queues for slow receivers. Account for TLS/TCP overhead.
          if (peer.writableLength + packet.length > 262144) { counters.slowReceivers++;peer.destroy();continue; }
          if (!spend(packet.length * 2 + 256)) { peer.destroy();continue; }
          peer.write(packet);counters.forwardedBytes += packet.length;
        }
      }
      }).catch(() => { counters.protocolErrors=(counters.protocolErrors??0)+1;socket.destroy(); });
    });
  });
  // Apply admission/budget checks to raw TCP connections, before TLS work begins.
  server.on('connection', socket => {
    const ip = socket.remoteAddress ?? 'unknown';
    if (sockets.size >= 64) { counters.rejectedCapacity++;socket.destroy();return; }
    if ((perIp.get(ip) ?? 0) >= 12) { counters.rejectedPerIp++;socket.destroy();return; }
    if (!spend(32768)) { socket.destroy();return; }counters.admitted++;
    sockets.add(socket as tls.TLSSocket); perIp.set(ip, (perIp.get(ip) ?? 0) + 1);
    socket.on('error', () => socket.destroy());
    socket.on('close', () => { sockets.delete(socket as tls.TLSSocket); const count = (perIp.get(ip) ?? 1) - 1; if (count) perIp.set(ip, count); else perIp.delete(ip); });
  });
  const status = () => ({schema:1, sampledAt:new Date().toISOString(), uptimeSeconds:Math.floor((Date.now()-started)/1000), healthy:!failed, failure:lastFailure, connections:sockets.size, rooms:rooms.size, members:members.size, registry:registry?.summary(), counters:{...counters}, limits:{connections:64,perIp:12,rooms:32,membersPerRoom:9,rememberedPerGroup:64}, eventLoopDelayMs:{mean:Number.isFinite(lag.mean)?lag.mean/1e6:0,p99:lag.percentile(99)/1e6,max:lag.max/1e6}, budget:budget.snapshot?.()});
  Object.assign(server,{relayStatus:status});
  let writingStatus = false;
  const publishStatus = async () => {
    if (!statusFile || writingStatus) return;writingStatus = true;
    try { await fs.promises.writeFile(statusFile+'.tmp',JSON.stringify(status()),{mode:0o600});await fs.promises.rename(statusFile+'.tmp',statusFile); }
    catch { counters.statusWriteFailures = (counters.statusWriteFailures ?? 0)+1; }
    finally { writingStatus = false;lag.reset(); }
  };
  const statusTimer = statusFile ? setInterval(() => { void publishStatus(); },10000).unref() : undefined;
  server.on('close', () => { if(statusTimer)clearInterval(statusTimer);lag.disable();void publishStatus(); });
  server.on('listening', () => { void publishStatus(); });
  server.on('tlsClientError', () => { counters.tlsErrors++; });
  return server;
}

if (require.main === module) {
  const directory = process.env.STATE_DIRECTORY ?? '/var/lib/minimap-relay';
  const budget = new Budget(path.join(directory, 'budget.json'), Number(process.env.MONTHLY_BYTES ?? 50000000000));
  const registry = new GroupRegistry(path.join(directory, 'groups.json'));
  const server = createRelay({key: fs.readFileSync(process.env.RELAY_KEY ?? '/opt/minimap-relay/server.key'), cert: fs.readFileSync(process.env.RELAY_CERT ?? '/opt/minimap-relay/server.crt')}, budget, registry, path.join(directory,'status.json'));
  server.on('relayFatal', reason => { console.error('MiniMap relay stopped: ' + reason);process.exitCode = 1; });
  server.listen(Number(process.env.PORT ?? 47832), '0.0.0.0', () => console.log('MiniMap encrypted relay listening'));
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { server.close(); process.exit(0); });
}

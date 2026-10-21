import * as tls from 'node:tls';
import * as fs from 'node:fs';
import * as path from 'node:path';
import {createHash, timingSafeEqual, randomBytes} from 'node:crypto';

type Group = {room: string; owner: string; deleted?: boolean};
export class GroupRegistry {
  private groups: Record<string, Group>;
  constructor(private file: string) {
    if (fs.statSync(file).size > 4000000) throw Error('Group registry too large');
    const state = JSON.parse(fs.readFileSync(file, 'utf8'));
    if (state.version !== 1 || !state.groups || typeof state.groups !== 'object' || Array.isArray(state.groups)) throw Error('Invalid group registry');
    this.groups = Object.create(null);
    for (const [id, group] of Object.entries(state.groups) as [string, Group][]) {
      if (!/^[a-f0-9]{32}$/.test(id) || !group || !/^[a-f0-9]{64}$/.test(group.room) || !/^[a-f0-9]{64}$/.test(group.owner) || group.deleted !== undefined && typeof group.deleted !== 'boolean') throw Error('Invalid group metadata');
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
  change(action: string, id: string, room: string, token: string): boolean {
    if (!/^[a-f0-9]{32}$/.test(id) || !/^[a-f0-9]{64}$/.test(room) || !/^[a-f0-9]{64}$/.test(token)) return false;
    const owner = createHash('sha256').update('MiniMap owner v1:' + id + ':' + token).digest('hex');
    const previous = this.groups[id];
    if (previous && !timingSafeEqual(Buffer.from(previous.owner, 'hex'), Buffer.from(owner, 'hex'))) return false;
    if (action === 'DELETE' && !previous) return false;
    if (previous?.deleted && action !== 'DELETE') return false;
    if (action === 'CREATE' && previous && previous.room !== room) return false;
    if (action === 'ROTATE' && !previous) return false;
    if (action !== 'CREATE' && action !== 'ROTATE' && action !== 'DELETE') return false;
    if (!previous && Object.keys(this.groups).length >= 10000) return false;
    // Keep an owner-hash tombstone so a deleted code cannot be re-created.
    const groups = {...this.groups, [id]: action === 'DELETE' ? {...previous!, deleted: true} : {room, owner}};
    const fd = fs.openSync(this.file + '.tmp', 'w', 0o600);
    try { fs.writeFileSync(fd, JSON.stringify({version: 1, groups})); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
    fs.renameSync(this.file + '.tmp', this.file);
    if (process.platform !== 'win32') { const directory = fs.openSync(path.dirname(this.file), 'r'); try { fs.fsyncSync(directory); } finally { fs.closeSync(directory); } }
    this.groups = groups; return true;
  }
}

// The relay never receives encryption keys, names, positions or saved markers.
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

export function createRelay(options: tls.TlsOptions, budget: {take(bytes: number): boolean}, registry?: GroupRegistry) {
  const rooms = new Map<string, Set<tls.TLSSocket>>();
  const members = new Map<tls.TLSSocket, {id: string; label: string; host: boolean}>();
  const sockets = new Set<tls.TLSSocket>();
  const perIp = new Map<string, number>();
  let windowStart = Date.now(), output = 0;
  const spend = (size: number) => {
    if (Date.now() - windowStart >= 1000) { windowStart = Date.now(); output = 0; }
    if (output + size > 8388608) return false;
    output += size;
    try { return budget.take(size); }
    catch { server.close(); for (const socket of sockets) socket.destroy(); return false; }
  };
  const server = tls.createServer({...options, minVersion: 'TLSv1.2', handshakeTimeout: 5000}, socket => {
    let buffer = Buffer.alloc(0), room: string | undefined;
    let receivedAt = Date.now(), frames = 0, incoming = 0;
    socket.setNoDelay(true); socket.setTimeout(10000, () => socket.destroy());
    socket.on('error', () => socket.destroy());
    socket.on('close', () => {
      members.delete(socket);
      if (room) { const group = rooms.get(room); group?.delete(socket); if (!group?.size) rooms.delete(room); }
    });
    socket.on('data', (chunk: Buffer) => {
      if (Date.now() - receivedAt >= 1000) { receivedAt = Date.now(); frames = 0; incoming = 0; }
      incoming += chunk.length;
      if (incoming > 262144 || buffer.length + chunk.length > 262144) { socket.destroy(); return; }
      buffer = Buffer.concat([buffer, chunk]);
      if (!room) {
        const end = buffer.indexOf(10);
        if (end < 0) { if (buffer.length > 1024) socket.destroy(); return; }
        if (end > 1024) { socket.destroy(); return; }
        const hello = buffer.subarray(0, end).toString('utf8');
        const control = /^SCUMMEMBERS2 (LIST|KICK) ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64}) ([a-f0-9]{64})$/.exec(hello);
        if (control) {
          const [, action, groupId, roomId, owner, target] = control;
          if (!registry?.owns(groupId, roomId, owner) || !spend(16384)) { socket.destroy(); return; }
          const group = rooms.get(groupId + ':' + roomId) ?? new Set<tls.TLSSocket>();
          if (action === 'LIST') {
            const rows = [...group].filter(peer => !peer.destroyed).map(peer => { const m = members.get(peer)!;return `${m.id} ${m.host ? '1' : '0'} ${m.label}`; });
            socket.end(`OK\n${rows.join('\n')}${rows.length ? '\n' : ''}END\n`);
          } else {
            const peer = [...group].find(peer => members.get(peer)?.id === target && !members.get(peer)?.host && !peer.destroyed);
            if (!peer) { socket.end('NO\n'); return; }
            peer.destroy();socket.end('OK\n');
          }
          return;
        }
        const management = /^SCUM(CREATE|ROTATE|DELETE)1 ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64})$/.exec(hello);
        if (management) {
          try {
            if (!registry || !spend(128) || !registry.change(management[1], management[2], management[3], management[4])) { socket.destroy(); return; }
            if (management[1] === 'ROTATE' || management[1] === 'DELETE') for (const [name, group] of rooms) if (name.startsWith(management[2] + ':') && (management[1] === 'DELETE' || name !== management[2] + ':' + management[3])) for (const peer of group) peer.destroy();
            socket.end('OK\n');
          } catch { server.close(); for (const peer of sockets) peer.destroy(); socket.destroy(); }
          return;
        }
        const persistent = /^SCUMGROUP1 ([a-f0-9]{32}) ([a-f0-9]{64})$/.exec(hello);
        const memberJoin = /^SCUMGROUP2 ([a-f0-9]{32}) ([a-f0-9]{64}) ([a-f0-9]{64}) ([A-Za-z0-9+/=]{64,512})(?: ([a-f0-9]{64}))?$/.exec(hello);
        const legacy = /^SCUMRELAY1 ([a-f0-9]{64})$/.exec(hello);
        if (memberJoin && registry?.allows(memberJoin[1], memberJoin[2]) && (memberJoin[5] === undefined || registry.owns(memberJoin[1], memberJoin[2], memberJoin[5]))) {
          room = memberJoin[1] + ':' + memberJoin[2];members.set(socket, {id: memberJoin[3], label: memberJoin[4], host: memberJoin[5] !== undefined});
        }
        else if (persistent && registry?.allows(persistent[1], persistent[2])) room = persistent[1] + ':' + persistent[2];
        else if (legacy && !registry) room = legacy[1]; // Synthetic/legacy test server only.
        else { socket.destroy(); return; }
        const group = rooms.get(room) ?? new Set<tls.TLSSocket>();
        if (group.size >= 9 || (!rooms.has(room) && rooms.size >= 32)) { socket.destroy(); return; }
        if (!members.has(socket)) members.set(socket, {id: randomBytes(32).toString('hex'),label: '-',host: false});
        if ([...group].some(peer => members.get(peer)?.id === members.get(socket)?.id)) { socket.destroy(); return; }
        group.add(socket); rooms.set(room, group); buffer = buffer.subarray(end + 1);
        if (!spend(128)) { socket.destroy(); return; }
        socket.write('OK\n');
      }
      while (buffer.length >= 4) {
        const size = buffer.readUInt32BE(0);
        if (size < 64 || size > 131072 || (size - 48) % 16 !== 0) { socket.destroy(); return; }
        if (buffer.length < size + 4) return;
        if (++frames > 3) { socket.destroy(); return; }
        const packet = buffer.subarray(0, size + 4); buffer = buffer.subarray(size + 4);
        for (const peer of rooms.get(room) ?? []) {
          // Echo is also the transport heartbeat for a member alone in a room.
          if (peer.destroyed) continue;
          // Never build unbounded queues for slow receivers. Account for TLS/TCP overhead.
          if (peer.writableLength + packet.length > 262144 || !spend(packet.length * 2 + 256)) { peer.destroy(); continue; }
          peer.write(packet);
        }
      }
    });
  });
  // Apply admission/budget checks to raw TCP connections, before TLS work begins.
  server.on('connection', socket => {
    const ip = socket.remoteAddress ?? 'unknown';
    if (sockets.size >= 64 || (perIp.get(ip) ?? 0) >= 12 || !spend(32768)) { socket.destroy(); return; }
    sockets.add(socket as tls.TLSSocket); perIp.set(ip, (perIp.get(ip) ?? 0) + 1);
    socket.on('error', () => socket.destroy());
    socket.on('close', () => { sockets.delete(socket as tls.TLSSocket); const count = (perIp.get(ip) ?? 1) - 1; if (count) perIp.set(ip, count); else perIp.delete(ip); });
  });
  server.on('tlsClientError', () => {});
  return server;
}

if (require.main === module) {
  const directory = process.env.STATE_DIRECTORY ?? '/var/lib/minimap-relay';
  const budget = new Budget(path.join(directory, 'budget.json'), Number(process.env.MONTHLY_BYTES ?? 50000000000));
  const registry = new GroupRegistry(path.join(directory, 'groups.json'));
  const server = createRelay({key: fs.readFileSync(process.env.RELAY_KEY ?? '/opt/minimap-relay/server.key'), cert: fs.readFileSync(process.env.RELAY_CERT ?? '/opt/minimap-relay/server.crt')}, budget, registry);
  server.listen(Number(process.env.PORT ?? 47832), '0.0.0.0', () => console.log('MiniMap encrypted relay listening'));
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { server.close(); process.exit(0); });
}

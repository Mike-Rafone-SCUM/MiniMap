const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const {Budget,GroupRegistry,createRelay}=require('./dist/server.js');
const tls=require('node:tls');
const {once}=require('node:events');
test('durable budget survives restart, cuts off, and refuses corrupt/missing state',()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-relay-test-'));const file=path.join(dir,'budget.json');
  try {
    assert.throws(()=>new Budget(file,100));
    fs.writeFileSync(file,JSON.stringify({month:new Date().toISOString().slice(0,7),reserved:0}));
    const first=new Budget(file,100);assert.equal(first.take(60),true);assert.equal(first.take(41),false);assert.equal(first.take(40),true);assert.equal(first.take(1),false);
    assert.equal(new Budget(file,100).take(1),false);
    fs.writeFileSync(file,'broken');assert.throws(()=>new Budget(file,100));
    fs.writeFileSync(file,JSON.stringify({month:'2000-01',reserved:100}));assert.equal(new Budget(file,100).take(1),true);
  } finally {fs.rmSync(dir,{recursive:true});}
});
test('TLS relay forwards opaque frames, isolates rooms, rejects malformed frames and cuts off', {skip:!process.env.TEST_KEY,timeout:20000},async()=>{
  let allowed=true;
  const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)}, {take:()=>allowed});
  const clients=[];
  const connect=async(room)=>{
    const socket=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});clients.push(socket);socket.on('error',()=>{});
    await once(socket,'secureConnect');const ack=once(socket,'data');socket.write('SCUMRELAY1 '+room.repeat(64)+'\n');assert.equal((await ack)[0].toString(),'OK\n');return socket;
  };
  try {
    server.listen(0,'127.0.0.1');await once(server,'listening');
    const a=await connect('a'),b=await connect('a'),outsider=await connect('b');
    const packet=Buffer.alloc(84,123);packet.writeUInt32BE(80);
    const fromA=once(a,'data'),fromB=once(b,'data');let leaked=false;outsider.on('data',()=>{leaked=true;});
    a.write(packet.subarray(0,2));a.write(packet.subarray(2));
    assert.deepEqual((await fromA)[0],packet);assert.deepEqual((await fromB)[0],packet);
    await new Promise(resolve=>setTimeout(resolve,150));assert.equal(leaked,false);
    const closed=once(b,'close');b.write(Buffer.alloc(4));await closed;
    allowed=false;const cutoff=once(a,'close');a.write(packet);await cutoff;
    assert.equal(leaked,false);
  } finally {for(const client of clients)client.destroy();await new Promise(resolve=>server.close(resolve));}
});
test('budget persistence failure prevents any send',()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-relay-test-'));const file=path.join(dir,'budget.json');
  try {fs.writeFileSync(file,JSON.stringify({month:new Date().toISOString().slice(0,7),reserved:0}));const b=new Budget(file,100);fs.mkdirSync(file+'.tmp');assert.throws(()=>b.take(1));}
  finally{fs.rmSync(dir,{recursive:true});}
});
test('persistent groups retain ownership, reject member rotation and retire old codes',async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-relay-test-'));const file=path.join(dir,'groups.json');
  try {
    fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));let registry=new GroupRegistry(file);
    const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),next='d'.repeat(64);
    assert.equal(await registry.change('CREATE',id,room,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,room),true);
    assert.equal(await registry.change('CREATE',id,next,'e'.repeat(64)),false);
    assert.equal(await registry.change('ROTATE',id,next,room),false);
    assert.equal(await registry.change('ROTATE',id,next,owner),true);
    assert.equal(await registry.change('ROTATE',id,next,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,room),false);assert.equal(registry.allows(id,next),true);
    assert.equal(await registry.change('DELETE',id,next,'e'.repeat(64)),false);
    assert.equal(registry.allows(id,next),true);
    assert.equal(await registry.change('DELETE',id,next,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,next),false);
    assert.equal(await registry.change('DELETE',id,next,owner),true); // Retry after lost acknowledgement.
    assert.equal(await registry.change('CREATE',id,next,owner),false);
    assert.equal(await registry.change('CREATE',id,room,'e'.repeat(64)),false);
    assert.equal(await registry.change('ROTATE',id,room,owner),false);
    assert.equal(fs.readFileSync(file,'utf8').includes(owner),false);
    fs.writeFileSync(file,'broken');assert.throws(()=>new GroupRegistry(file));
  }finally{fs.rmSync(dir,{recursive:true});}
});
test('registered TLS groups disconnect old-code members on owner rotation', {skip:!process.env.TEST_KEY,timeout:20000},async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-relay-test-'));const file=path.join(dir,'groups.json');
  fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));const registry=new GroupRegistry(file);
  const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:()=>true},registry);
  const clients=[];const connect=async(line,accepted=true)=>{
    const socket=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});clients.push(socket);socket.on('error',()=>{});await once(socket,'secureConnect');
    const reply=accepted?once(socket,'data'):once(socket,'close');socket.write(line+'\n');const result=await reply;if(accepted)assert.equal(result[0].toString(),'OK\n');return socket;
  };
  try {
    server.listen(0,'127.0.0.1');await once(server,'listening');
    const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),next='d'.repeat(64);
    await connect(`SCUMCREATE1 ${id} ${room} ${owner}`);
    const member=await connect(`SCUMGROUP1 ${id} ${room}`);
    await connect(`SCUMROTATE1 ${id} ${next} ${room}`,false);
    const disconnected=once(member,'close');await connect(`SCUMROTATE1 ${id} ${next} ${owner}`);await disconnected;
    await connect(`SCUMGROUP1 ${id} ${room}`,false);
    const current=await connect(`SCUMGROUP1 ${id} ${next}`);
    await connect(`SCUMDELETE1 ${id} ${next} ${room}`,false);
    assert.equal(current.destroyed,false);
    const removed=once(current,'close');await connect(`SCUMDELETE1 ${id} ${next} ${owner}`);await removed;
    await connect(`SCUMGROUP1 ${id} ${next}`,false);
    await connect(`SCUMDELETE1 ${id} ${next} ${owner}`);
    await connect(`SCUMCREATE1 ${id} ${next} ${owner}`,false);
    await connect(`SCUMRELAY1 ${room}`,false);
  }finally{for(const client of clients)client.destroy();await new Promise(resolve=>server.close(resolve));fs.rmSync(dir,{recursive:true});}
});

test('host disconnect is authenticated, scoped and permits rejoining', {skip:!process.env.TEST_KEY,timeout:20000},async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-member-test-')),file=path.join(dir,'groups.json');
  fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));const registry=new GroupRegistry(file);
  const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:()=>true},registry);
  const clients=[];
  const connect=async(line,reply=true)=>{
    const socket=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});clients.push(socket);socket.on('error',()=>{});await once(socket,'secureConnect');
    const result=once(socket,reply?'data':'close');socket.write(line+'\n');const response=await result;if(line.startsWith('SCUMMEMBERS2 '))socket.destroy();return [socket,response];
  };
  try {
    server.listen(0,'127.0.0.1');await once(server,'listening');
    const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),hostId='d'.repeat(64),memberId='e'.repeat(64),label=Buffer.alloc(80,77).toString('base64');
    await connect(`SCUMCREATE1 ${id} ${room} ${owner}`);
    const before=fs.readFileSync(file,'utf8');
    const [host]=await connect(`SCUMGROUP2 ${id} ${room} ${hostId} ${label} ${owner}`);
    const [member]=await connect(`SCUMGROUP2 ${id} ${room} ${memberId} ${label}`);
    const [,list]=await connect(`SCUMMEMBERS2 LIST ${id} ${room} ${owner} ${hostId}`);
    assert.equal(list[0].toString(),`OK\n${hostId} 1 ${label}\n${memberId} 0 ${label}\nEND\n`);
    await connect(`SCUMMEMBERS2 KICK ${id} ${room} ${hostId} ${memberId}`,false);assert.equal(member.destroyed,false);
    const [,self]=await connect(`SCUMMEMBERS2 KICK ${id} ${room} ${owner} ${hostId}`);assert.equal(self[0].toString(),'NO\n');assert.equal(host.destroyed,false);
    const otherId='1'.repeat(32);await connect(`SCUMCREATE1 ${otherId} ${room} ${owner}`);
    const [other]=await connect(`SCUMGROUP2 ${otherId} ${room} ${memberId} ${label}`);
    const closed=once(member,'close');const [,ack]=await connect(`SCUMMEMBERS2 KICK ${id} ${room} ${owner} ${memberId}`);assert.equal(ack[0].toString(),'OK\n');await closed;
    assert.equal(host.destroyed,false);assert.equal(other.destroyed,false);
    const [rejoined,rejoinAck]=await connect(`SCUMGROUP2 ${id} ${room} ${memberId} ${label}`);assert.equal(rejoinAck[0].toString(),'OK\n');assert.equal(rejoined.destroyed,false);
    const [,missing]=await connect(`SCUMMEMBERS2 KICK ${id} ${room} ${owner} ${'0'.repeat(64)}`);assert.equal(missing[0].toString(),'NO\n');
    const state=JSON.parse(fs.readFileSync(file,'utf8'));assert.deepEqual(state.groups[id],JSON.parse(before).groups[id]);
    assert.equal(fs.readFileSync(file,'utf8').includes(label),false);
  }finally{for(const client of clients)client.destroy();await new Promise(resolve=>server.close(resolve));fs.rmSync(dir,{recursive:true});}
});

test('member polling reuses one authenticated connection and rechecks owner authority', {skip:!process.env.TEST_KEY,timeout:20000},async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-member-reuse-')),file=path.join(dir,'groups.json');
  fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));const registry=new GroupRegistry(file);
  const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),hostId='d'.repeat(64),label=Buffer.alloc(80,77).toString('base64');await registry.change('CREATE',id,room,owner);
  let connections=0;const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:size=>size!==32768||++connections<=2},registry),clients=[];
  const open=async()=>{const socket=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});clients.push(socket);socket.on('error',()=>{});await once(socket,'secureConnect');return socket;};
  const request=(socket,line,end)=>new Promise((resolve,reject)=>{let text='';const collect=chunk=>{text+=chunk.toString();if(text.endsWith(end)){socket.off('data',collect);resolve(text);}};socket.on('data',collect);socket.write(line+'\n');});
  try {
    server.listen(0,'127.0.0.1');await once(server,'listening');const host=await open();assert.equal(await request(host,`SCUMGROUP2 ${id} ${room} ${hostId} ${label} ${owner}`,'\n'),'OK\n');const admin=await open();
    for(let i=0;i<24;i++){assert.equal(await request(admin,`SCUMMEMBERS2 LIST ${id} ${room} ${owner} ${hostId}`,'END\n'),`OK\n${hostId} 1 ${label}\nEND\n`);await new Promise(resolve=>setTimeout(resolve,100));}
    assert.equal(connections,2);const closed=once(admin,'close');admin.write(`SCUMMEMBERS2 LIST ${id} ${room} ${hostId} ${hostId}\n`);await closed;assert.equal(host.destroyed,false);
  }finally{for(const client of clients)client.destroy();await new Promise(resolve=>server.close(resolve));fs.rmSync(dir,{recursive:true});}
});


test('durable member roster survives an empty room and relay restart with encrypted labels only', {skip:!process.env.TEST_KEY,timeout:20000},async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-offline-roster-')),file=path.join(dir,'groups.json');
  fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));
  const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),token='d'.repeat(64),label=Buffer.alloc(128,77).toString('base64');
  let registry=new GroupRegistry(file);await registry.change('CREATE',id,room,owner);
  let server;const clients=[];
  const start=async()=>{server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)}, {take:()=>true},registry);server.listen(0,'127.0.0.1');await once(server,'listening');};
  const open=async()=>{const socket=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});socket.on('error',()=>{});clients.push(socket);await once(socket,'secureConnect');return socket;};
  const request=(socket,line,end)=>new Promise((resolve,reject)=>{let text='';const collect=chunk=>{text+=chunk.toString();if(text.endsWith(end)){socket.off('data',collect);resolve(text);}};socket.on('data',collect);socket.once('error',reject);socket.write(line+'\n');});
  const stop=async()=>{for(const client of clients)client.destroy();await new Promise(resolve=>server.close(resolve));};
  try {
    await start();let member=await open();assert.equal(await request(member,`SCUMGROUP3 ${id} ${room} ${token} ${label} ${owner}`,'\n'),'OK\n');
    let control=await open();assert.equal(await request(control,`SCUMROSTER3 ${id} ${room}`,'END\n'),`OK\n${token} 1 1 ${label}\nEND\n`);
    await stop();registry=new GroupRegistry(file);await registry.change('CREATE',id,room,owner); // Lost-ack retry preserves members.
    await start();control=await open();assert.equal(await request(control,`SCUMROSTER3 ${id} ${room}`,'END\n'),`OK\n${token} 1 0 ${label}\nEND\n`);
    member=await open();assert.equal(await request(member,`SCUMGROUP3 ${id} ${room} ${token} ${label} ${owner}`,'\n'),'OK\n');
    assert.equal(Object.keys(new GroupRegistry(file).roster(id,room)).length,1);
    assert.equal(await request(control,`SCUMROSTER3 ${id} ${room}`,'END\n'),`OK\n${token} 1 1 ${label}\nEND\n`);
    const outsider=await open(),closed=once(outsider,'close');outsider.write(`SCUMROSTER3 ${id} ${'e'.repeat(64)}\n`);await closed;
    await registry.change('ROTATE',id,'f'.repeat(64),owner);assert.deepEqual(new GroupRegistry(file).roster(id,'f'.repeat(64)),{});
    assert.equal(fs.readFileSync(file,'utf8').includes(label),false);
  }finally{await stop();fs.rmSync(dir,{recursive:true});}
});


test('registry writes are serialized, durable before success and fail closed',async()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-queued-registry-')),file=path.join(dir,'groups.json');
 fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));const registry=new GroupRegistry(file),room='b'.repeat(64),owner='c'.repeat(64);
 try {
  const promises=Array.from({length:40},(_,i)=>registry.change('CREATE',i.toString(16).padStart(32,'0'),room,owner));
  assert.equal(registry.allows('0'.repeat(32),room),false);
  assert((await Promise.all(promises)).every(Boolean));
  assert.equal(new GroupRegistry(file).summary().active,40);
  const before=fs.readFileSync(file);fs.mkdirSync(file+'.tmp');
  await assert.rejects(registry.change('DELETE','0'.repeat(32),room,owner));
  assert.equal(registry.allows('0'.repeat(32),room),true);assert.deepEqual(fs.readFileSync(file),before);
 }finally{fs.rmSync(dir,{recursive:true});}
});

test('fatal budget storage error emits a failure and closes the service',{skip:!process.env.TEST_KEY,timeout:10000},async()=>{
 const {spawn}=require('node:child_process');
 const script=`const fs=require('fs'),tls=require('tls'),{createRelay}=require(${JSON.stringify(path.join(__dirname,'dist/server.js'))});
 const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take(){throw Error('synthetic failure')}});
 server.on('relayFatal',reason=>{console.log(reason);process.exitCode=1});
 server.listen(0,'127.0.0.1',()=>{const c=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});c.on('error',()=>{});});`;
 const child=spawn(process.execPath,['-e',script],{env:process.env,windowsHide:true});let output='';child.stdout.on('data',data=>output+=data);child.stderr.on('data',data=>output+=data);
 const [code]=await once(child,'exit');assert.equal(code,1);assert(output.includes('budget_storage_failure'));
});

test('private health snapshot contains only aggregate operational data', {skip:!process.env.TEST_KEY,timeout:10000},async()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-health-')),file=path.join(dir,'status.json');
 const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:()=>true},undefined,file);
 try {
  server.listen(0,'127.0.0.1');await once(server,'listening');
  for(let i=0;i<50&&!fs.existsSync(file);i++)await new Promise(r=>setTimeout(r,20));
  const status=JSON.parse(fs.readFileSync(file));assert.equal(status.healthy,true);assert.equal(status.limits.connections,64);assert(status.eventLoopDelayMs);
  const text=JSON.stringify(status);assert(!/owner|token|label|address|position|marker/i.test(text));
 }finally{await new Promise(r=>server.close(r));await new Promise(r=>setTimeout(r,50));fs.rmSync(dir,{recursive:true});}
});


test('connection capacity and shared-IP limits stay bounded under a synthetic TLS load',{skip:!process.env.TEST_KEY,timeout:20000},async()=>{
 const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:()=>true}),clients=[];
 const open=async(localAddress,accepted=true)=>{
  const c=tls.connect({host:'127.0.0.1',port:server.address().port,localAddress,rejectUnauthorized:false});clients.push(c);c.on('error',()=>{});
  await once(c,accepted?'secureConnect':'close').catch(error=>{if(accepted)throw error;});return c;
 };
 try {
  server.listen(0,'127.0.0.1');await once(server,'listening');
  for(let i=0;i<12;i++)await open('127.0.0.1');await open('127.0.0.1',false);
  for(let i=12;i<64;i++)await open('127.0.0.'+(Math.floor(i/12)+1));
  assert.equal(server.relayStatus().connections,64);await open('127.0.0.7',false);
  const status=server.relayStatus();assert.equal(status.connections,64);assert.equal(status.counters.rejectedPerIp,1);assert.equal(status.counters.rejectedCapacity,1);
 }finally{for(const c of clients)c.destroy();await new Promise(r=>server.close(r));}
});


test('concurrent durable joins retain every live member in one room',{skip:!process.env.TEST_KEY,timeout:15000},async()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-parallel-join-')),file=path.join(dir,'groups.json');fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));
 const registry=new GroupRegistry(file),id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),label=Buffer.alloc(80,77).toString('base64');await registry.change('CREATE',id,room,owner);
 const server=createRelay({key:fs.readFileSync(process.env.TEST_KEY),cert:fs.readFileSync(process.env.TEST_CERT)},{take:()=>true},registry),clients=[];
 try {
  server.listen(0,'127.0.0.1');await once(server,'listening');
  await Promise.all(Array.from({length:9},async(_,i)=>{const c=tls.connect({host:'127.0.0.1',port:server.address().port,rejectUnauthorized:false});clients.push(c);c.on('error',()=>{});await once(c,'secureConnect');const ack=once(c,'data');c.write(`SCUMGROUP3 ${id} ${room} ${i.toString(16).padStart(64,'0')} ${label}\n`);assert.equal((await ack)[0].toString(),'OK\n');}));
  assert.equal(server.relayStatus().rooms,1);assert.equal(server.relayStatus().members,9);assert.equal(new GroupRegistry(file).summary().rememberedMembers,9);
  const sample=Buffer.alloc(84,12);sample.writeUInt32BE(80);const received=clients.map(c=>once(c,'data'));clients[0].write(sample);assert((await Promise.all(received)).every(([data])=>data.equals(sample)));
 }finally{for(const c of clients)c.destroy();await new Promise(r=>server.close(r));fs.rmSync(dir,{recursive:true});}
});

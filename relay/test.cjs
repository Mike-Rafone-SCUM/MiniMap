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
test('persistent groups retain ownership, reject member rotation and retire old codes',()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'minimap-relay-test-'));const file=path.join(dir,'groups.json');
  try {
    fs.writeFileSync(file,JSON.stringify({version:1,groups:{}}));let registry=new GroupRegistry(file);
    const id='a'.repeat(32),room='b'.repeat(64),owner='c'.repeat(64),next='d'.repeat(64);
    assert.equal(registry.change('CREATE',id,room,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,room),true);
    assert.equal(registry.change('CREATE',id,next,'e'.repeat(64)),false);
    assert.equal(registry.change('ROTATE',id,next,room),false);
    assert.equal(registry.change('ROTATE',id,next,owner),true);
    assert.equal(registry.change('ROTATE',id,next,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,room),false);assert.equal(registry.allows(id,next),true);
    assert.equal(registry.change('DELETE',id,next,'e'.repeat(64)),false);
    assert.equal(registry.allows(id,next),true);
    assert.equal(registry.change('DELETE',id,next,owner),true);
    registry=new GroupRegistry(file);assert.equal(registry.allows(id,next),false);
    assert.equal(registry.change('DELETE',id,next,owner),true); // Retry after lost acknowledgement.
    assert.equal(registry.change('CREATE',id,next,owner),false);
    assert.equal(registry.change('CREATE',id,room,'e'.repeat(64)),false);
    assert.equal(registry.change('ROTATE',id,room,owner),false);
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

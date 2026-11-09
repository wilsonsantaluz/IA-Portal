import test from 'node:test';
import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {mkdtempSync,rmSync} from 'node:fs';
import os from 'node:os';import path from 'node:path';
import {Store} from '../src/store.js';import {Sessions} from '../src/sessions.js';
const a='1/11111111-1111-1111-1111-111111111111',b='2/22222222-2222-2222-2222-222222222222';
class Client extends EventEmitter {
  sends=0; destroyed=0; info={wid:{_serialized:'synthetic-account'}}; initialize(){return Promise.resolve();}async destroy(){this.destroyed++;}async logout(){}
  async sendMessage(){this.sends++;if(this.ambiguous)throw Error('timeout');return {id:{_serialized:'provider-id'}};}
  async getState(){return this.healthState||'CONNECTED';}
}
test('tenant sessions, QR expiry, durable deduplication, ambiguous sends and restore',async()=>{
 const folder=mkdtempSync(path.join(os.tmpdir(),'portal-bridge-'));const file=path.join(folder,'journal.db');let store=new Store(file);const clients=[];let now=100000;
 const sessions=new Sessions(store,async()=>{const c=new Client();clients.push(c);return c;},async()=> 'data:image/png;base64,synthetic',{now:()=>now});
 try{
  assert.throws(()=>sessions.item('../escape'));await sessions.start(a);await sessions.start(b);assert.equal(clients.length,2);
  clients[0].emit('qr','secret');await new Promise(r=>setImmediate(r));assert.equal((await sessions.status(a)).state,'awaiting_qr');assert.equal((await sessions.status(b)).qr,null);
  clients[0].emit('ready');assert.equal((await sessions.status(a)).qr,null);
  const m={id:{_serialized:'inbound'},from:'5511999999999@c.us',type:'chat',body:'synthetic',timestamp:100};
  clients[0].emit('message',m);clients[0].emit('message',m);clients[0].emit('message',{...m,from:'group@g.us'});
  assert.equal(store.pending(a).length,1);assert.equal(store.pending(b).length,0);
  assert.equal((await sessions.send(a,'one',m.from,'reply')).state,'sent');await sessions.send(a,'one',m.from,'reply');assert.equal(clients[0].sends,1);
  clients[0].ambiguous=true;assert.equal((await sessions.send(a,'two',m.from,'reply')).state,'unknown');await sessions.send(a,'two',m.from,'reply');assert.equal(clients[0].sends,2);
  clients[1].emit('qr','secret');await new Promise(r=>setImmediate(r));now+=61000;assert.equal((await sessions.status(b)).state,'pairing_expired');assert.equal((await sessions.status(b)).qr,null);
  store.ack(a,['inbound']);assert.equal(store.pending(a).length,0);clients[0].emit('message',m);assert.equal(store.pending(a).length,0);
  store.reserve(a,'crashed');await sessions.close();store.close();store=new Store(file);
  assert.equal(store.sent(a,'crashed').state,'unknown');assert.deepEqual(store.restore(),[a]);assert.equal(store.sent(a,'one').state,'sent');
 }finally{await sessions.close();store.close();rmSync(folder,{recursive:true,force:true});}
});
test('one reconnect timer, stop cancels retry, stale events cannot revive session',async()=>{
 const store=new Store(':memory:');const clients=[];let callback;let clears=0;
 const sessions=new Sessions(store,async()=>{const c=new Client();clients.push(c);return c;},async()=>'',{schedule:f=>{callback=f;return 1;},clear:()=>clears++});
 await sessions.start(a);clients[0].emit('disconnected','NETWORK');await new Promise(r=>setImmediate(r));assert.equal((await sessions.status(a)).state,'reconnecting');
 await sessions.stop(a);callback();clients[0].emit('ready');assert.equal((await sessions.status(a)).state,'disconnected');assert.equal(clients.length,1);assert.ok(clears);await sessions.close();store.close();
});
test('same account cannot be claimed by another tenant, logout releases binding',async()=>{
 const store=new Store(':memory:');const clients=[];
 const sessions=new Sessions(store,async()=>{const c=new Client();clients.push(c);return c;},async()=>'');
 await sessions.start(a);clients[0].emit('ready');await sessions.start(b);clients[1].emit('ready');await new Promise(r=>setImmediate(r));
 assert.equal((await sessions.status(b)).state,'account_in_use');assert.equal((await sessions.status(a)).state,'connected');
 await sessions.stop(a,'disconnected',true);await sessions.start(b);clients[2].emit('ready');assert.equal((await sessions.status(b)).state,'connected');
 await sessions.close();store.close();
});
test('heartbeat restarts an apparently connected but offline browser',async()=>{
 const store=new Store(':memory:');let client;let scheduled=false;
 const sessions=new Sessions(store,async()=>{client=new Client();return client;},async()=>'',{schedule:()=>{scheduled=true;return 1;},clear:()=>{}});
 await sessions.start(a);client.emit('ready');client.healthState='TIMEOUT';await sessions.health();
 assert.equal((await sessions.status(a)).state,'reconnecting');assert.ok(scheduled);await sessions.close();store.close();
});

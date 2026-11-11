export const keyPattern = /^[1-9][0-9]*\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
export const contactPattern = /^[0-9]{5,30}@(c\.us|lid)$/;
export class Sessions {
  constructor(store, factory, qrImage, { now=Date.now, schedule=setTimeout, clear=clearTimeout, forget=async()=>{} }={}) {
    Object.assign(this,{store,factory,qrImage,now,schedule,clear,forget}); this.items=new Map();this.closing=false;
  }
  item(k) {
    if (!keyPattern.test(k)) throw Error('Invalid session');
    if (!this.items.has(k)) this.items.set(k,{state:'disconnected',attempts:0,generation:0,desired:false,updatedAt:new Date(this.now()).toISOString()});
    return this.items.get(k);
  }
  state(s,state) {s.state=state;s.updatedAt=new Date(this.now()).toISOString();if(state!=='awaiting_qr'){s.qr=null;s.qrExpiresAt=null;}}
  async status(k) {
    const s=this.item(k);
    if(s.qrExpiresAt&&s.qrExpiresAt<=this.now()){this.state(s,'pairing_expired');s.desired=false;this.store.desired(k,false);++s.generation;await s.client?.destroy().catch(()=>{});s.client=null;}
    return {state:s.state,updatedAt:s.updatedAt,lastConnectedAt:s.lastConnectedAt||null,attempts:s.attempts,
      qr:s.state==='awaiting_qr'?s.qr:null,qrExpiresAt:s.qrExpiresAt?new Date(s.qrExpiresAt).toISOString():null};
  }
  async start(k) {
    const s=this.item(k);
    if(s.client||s.starting)return this.status(k);
    this.clear(s.timer);s.desired=true;this.store.desired(k,true);s.starting=true;
    this.state(s,s.attempts?'reconnecting':'starting');const generation=++s.generation;
    try {
      const client=await this.factory(k);
      if(s.generation!==generation||this.closing){await client.destroy().catch(()=>{});return this.status(k);}
      s.client=client;s.starting=false;
      const current=()=>s.generation===generation&&!this.closing;
      client.on('qr',qr=>{if(!current())return;this.qrImage(qr).then(image=>{if(current()){this.state(s,'awaiting_qr');s.qr=image;s.qrExpiresAt=this.now()+60000;}}).catch(()=>this.fail(k,s,generation));});
      client.on('authenticated',()=>{if(current())this.state(s,'authenticating');});
      client.on('ready',()=>{if(current()){
        const account=client.info?.wid?._serialized;
        if(!account||!this.store.bind(k,account)){void this.stop(k,'account_in_use',true);return;}
        s.attempts=0;s.lastConnectedAt=new Date(this.now()).toISOString();this.state(s,'connected');}});
      client.on('auth_failure',()=>{if(current())void this.stop(k,'pairing_expired');});
      client.on('disconnected',reason=>{if(!current())return;if(reason==='LOGOUT'||String(reason).includes('qrcode retries'))void this.stop(k,'pairing_expired');else void this.fail(k,s,generation);});
      client.on('message',m=>{
        if(!current()||!s.desired||m.fromMe||m.type!=='chat'||!contactPattern.test(m.from)||!m.body?.trim()||m.body.length>4000)return;
        const timestamp=new Date(m.timestamp*1000);if(!Number.isFinite(timestamp.getTime()))return;
        this.store.incoming(k,{externalId:m.id._serialized,contactId:m.from,text:m.body,timestamp:timestamp.toISOString()});
      });
      client.on('message_ack',(m,ack)=>{if(!current())return;const id=m.id?._serialized;if(!id)return;
        this.store.db.prepare('UPDATE outbox SET state=? WHERE k=? AND external=?').run(ack<0?'failed':ack>=3?'read':ack>=2?'delivered':ack>=1?'sent':'sending',k,id);
      });
      // Initialization can wait for QR scan. Never block an HTTP request on it.
      Promise.resolve(client.initialize()).catch(()=>{if(current())void this.fail(k,s,generation);});
    } catch { await this.fail(k,s,generation); }
    return this.status(k);
  }
  async fail(k,s,generation) {
    if(s.generation!==generation||this.closing)return;
    ++s.generation;s.starting=false;const client=s.client;s.client=null;
    await client?.destroy().catch(()=>{});
    if(!s.desired||s.generation!==generation+1)return;
    s.attempts++;this.state(s,'reconnecting');
    s.timer=this.schedule(()=>{if(s.desired&&!this.closing)void this.start(k);},Math.min(300000,5000*2**Math.min(s.attempts-1,6)));
  }
  async stop(k,state='disconnected',logout=false) {
    const s=this.item(k);s.desired=false;this.store.desired(k,false);this.clear(s.timer);++s.generation;s.starting=false;
    const client=s.client;s.client=null;this.state(s,state);
    if(client){if(logout)await client.logout().catch(()=>{});await client.destroy().catch(()=>{});}
    if(logout){await this.forget(k);this.store.unbind(k);}
    return this.status(k);
  }
  async send(k,id,contact,text) {
    const previous=this.store.sent(k,id);if(previous)return previous;
    const s=this.item(k);if(s.state!=='connected'||!s.client)return {state:'not_connected'};
    if(!contactPattern.test(contact)||typeof text!=='string'||!text.trim()||text.length>4000)throw Error('Invalid message');
    this.store.reserve(k,id);
    let timeout;
    try {const m=await Promise.race([s.client.sendMessage(contact,text,{sendSeen:false}),new Promise((_,reject)=>{timeout=setTimeout(()=>reject(Error('timeout')),40000);})]);this.store.result(k,id,'sent',m.id._serialized);}
    catch {this.store.result(k,id,'unknown');} // Never blindly resend after an ambiguous side effect.
    finally{clearTimeout(timeout);}
    return this.store.sent(k,id);
  }
  async health() {
    for(const [k,s] of this.items){
      await this.status(k); // Expire an unattended QR too.
      if(this.closing||!s.client)continue;
      const generation=s.generation;
      if((s.state==='starting'||s.state==='authenticating')&&this.now()-Date.parse(s.updatedAt)>180000){await this.fail(k,s,generation);continue;}
      if(s.state!=='connected')continue;
      let timeout;
      try {
        const state=await Promise.race([s.client.getState(),new Promise((_,reject)=>{timeout=setTimeout(()=>reject(Error('heartbeat timeout')),5000);})]);
        if(state!=='CONNECTED')await this.fail(k,s,generation);
      }catch{await this.fail(k,s,generation);}finally{clearTimeout(timeout);}
    }
  }
  async close() {
    this.closing=true;
    for(const s of this.items.values()){this.clear(s.timer);++s.generation;await s.client?.destroy().catch(()=>{});}
  }
}

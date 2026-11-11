import http from 'node:http';
import { timingSafeEqual } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { rm } from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';
import { Store } from './store.js';
import { Sessions, keyPattern } from './sessions.js';
const require=createRequire(import.meta.url);
const {Client,LocalAuth}=require('whatsapp-web.js');const QRCode=require('qrcode');
const secret=process.env.BRIDGE_SECRET;
if(!secret||secret.length<32)throw Error('BRIDGE_SECRET must contain at least 32 characters');
process.umask(0o077);
const root=process.env.BRIDGE_DATA||'./data';mkdirSync(root,{recursive:true,mode:0o700});
const store=new Store(path.join(root,'journal.db'));
const sessions=new Sessions(store,async k=>new Client({
  authStrategy:new LocalAuth({clientId:k.replace('/','-'),dataPath:path.join(root,'profiles')}),
  puppeteer:{executablePath:process.env.CHROMIUM_PATH||undefined,headless:true,args:process.env.BRIDGE_CONTAINER==='true'?['--no-sandbox','--disable-dev-shm-usage']:[]},
  webVersionCache:{type:'none'},qrMaxRetries:5,authTimeoutMs:60000,takeoverOnConflict:false,
}),qr=>QRCode.toDataURL(qr,{width:300,margin:2}),{
  forget:async k=>{if(!keyPattern.test(k))throw Error('Invalid session');await rm(path.join(root,'profiles','session-'+k.replace('/','-')),{recursive:true,force:true});}
});
const equal=v=>{const a=Buffer.from(v||''),b=Buffer.from('Bearer '+secret);return a.length===b.length&&timingSafeEqual(a,b);};
const server=http.createServer(async(req,res)=>{
  res.setHeader('Cache-Control','no-store');res.setHeader('Content-Type','application/json');
  const reply=(status,value)=>{res.writeHead(status);res.end(JSON.stringify(value));};
  if(!equal(req.headers.authorization))return reply(401,{error:'Unauthorized'});
  const match=/^\/sessions\/([1-9][0-9]*\/[0-9a-f-]+)\/(status|start|stop|logout|inbox|ack|send|delivery)$/.exec(req.url||'');
  if(!match||!keyPattern.test(match[1]))return reply(404,{error:'Not found'});
  const [,k,action]=match;
  try {
    let input={};if(req.method==='POST'){const chunks=[];let size=0;for await(const chunk of req){size+=chunk.length;if(size>32768)return reply(413,{error:'Too large'});chunks.push(chunk);}const body=Buffer.concat(chunks).toString('utf8');input=body?JSON.parse(body):{};}
    if(req.method==='GET'&&action==='status')return reply(200,await sessions.status(k));
    if(req.method==='GET'&&action==='inbox')return reply(200,store.pending(k));
    if(req.method!=='POST')return reply(405,{error:'Method not allowed'});
    if(action==='start')return reply(200,await sessions.start(k));
    if(action==='stop'||action==='logout')return reply(200,await sessions.stop(k,'disconnected',action==='logout'));
    if(action==='ack'&&Array.isArray(input.ids)&&input.ids.length<=50){store.ack(k,input.ids);return reply(200,{ok:true});}
    if((action==='send'||action==='delivery')&&typeof input.id==='string'&&/^[0-9a-f-]{36}$/.test(input.id)){
      if(action==='delivery')return reply(200,store.sent(k,input.id)||{state:'absent'});
      return reply(200,await sessions.send(k,input.id,input.contact,input.text));
    }
    return reply(400,{error:'Invalid request'});
  }catch{return reply(503,{error:'Bridge operation failed; sensitive details omitted'});}
});
server.requestTimeout=30000;server.headersTimeout=10000;
let checking=false;
const heartbeat=setInterval(async()=>{if(checking)return;checking=true;try{await sessions.health();}catch{/* No sensitive exception logging. */}finally{checking=false;}},20000);
server.listen(Number(process.env.PORT||3010),'0.0.0.0',()=>{
  console.log('Bridge listening; private sessions and message bodies are never logged.');
  for(const k of store.restore())void sessions.start(k);
});
for(const signal of ['SIGTERM','SIGINT'])process.on(signal,async()=>{clearInterval(heartbeat);server.close();await sessions.close();store.close();process.exit(0);});

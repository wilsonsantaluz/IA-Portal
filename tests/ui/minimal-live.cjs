// Explicit live installation check: no model calls, HAR, traces or saved tokens.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_LIVE_PORTAL_URL,credentials=process.env.IA_PORTAL_CREDENTIALS_FILE;
assert.ok(base&&credentials,'Set IA_LIVE_PORTAL_URL and IA_PORTAL_CREDENTIALS_FILE explicitly');
assert.ok(base.startsWith('https://')||base.startsWith('http://127.0.0.1:'),'Verified HTTPS or loopback only');
const values=Object.fromEntries(fs.readFileSync(credentials,'utf8').trim().split(/\r?\n/).map(line=>{const split=line.indexOf('=');return[line.slice(0,split),line.slice(split+1)]}));
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});try{
 const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();const errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/login');
 await page.getByRole('textbox',{name:/^Email/}).fill(values.Bootstrap__AdminEmail);await page.getByLabel(/^Senha/).fill(values.Bootstrap__AdminPassword);
 await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.waitForURL(url=>!url.pathname.includes('/login'));await page.goto(base+'/chat');
 await page.getByRole('textbox',{name:'Sua pergunta para a IA',exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'Analisar PDF ou imagem',exact:true}).count(),0);
 assert.equal(await page.getByRole('link',{name:'Data Connectors',exact:true}).count(),0);
 const out=path.resolve(__dirname,'../../artifacts/micro-ui');fs.mkdirSync(out,{recursive:true});
 for(const theme of ['dark','light']){
  if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
  await page.screenshot({path:path.join(out,theme+'.png'),fullPage:true});
 }
 assert.deepEqual(errors,[]);await context.close();console.log('PASS: actual HTTPS browser login/chat, minimal upload/source controls, light/dark. No LLM call or token/credential artifact.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1});

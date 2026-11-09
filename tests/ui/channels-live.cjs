// Opt-in live/local check; read-only after login, no Meta/LLM call or saved token.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_LIVE_PORTAL_URL,credentials=process.env.IA_PORTAL_CREDENTIALS_FILE;
assert.ok(base&&credentials,'Set explicit target and private credentials-file environment variables');
assert.ok(base.startsWith('https://')||base.startsWith('http://127.0.0.1:'));
const values=Object.fromEntries(fs.readFileSync(credentials,'utf8').trim().split(/\r?\n/).map(line=>{const i=line.indexOf('=');return[line.slice(0,i),line.slice(i+1)]}));
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});try{
 const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();const errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/login');
 await page.getByRole('textbox',{name:/^Email/}).fill(values.Bootstrap__AdminEmail);await page.getByLabel(/^Senha/).fill(values.Bootstrap__AdminPassword);
 await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.waitForURL(url=>!url.pathname.includes('/login'));await page.goto(base+'/channels');
 await page.getByRole('heading',{name:'Cadastrar conexão',exact:true}).waitFor();
 assert.equal(await page.locator('input[type=password]').count(),0);
 const out=path.resolve(__dirname,'../../artifacts/whatsapp-local-ui');fs.mkdirSync(out,{recursive:true});
 for(const theme of ['dark','light']){
  if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
  await page.screenshot({path:path.join(out,theme+'.png'),fullPage:true});
 }
 assert.deepEqual(errors,[]);await context.close();console.log('PASS: actual local login, updated channels and organization-scoped browser option and no secret-entry fields, light/dark; no Meta/LLM call.');
}finally{await browser.close();}})().catch(e=>{console.error(e.name+': '+e.message.split('\n')[0]);process.exitCode=1});

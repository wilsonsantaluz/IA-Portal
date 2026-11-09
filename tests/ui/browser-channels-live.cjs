// Explicit opt-in: creates/reuses one local test channel, starts an unpaired browser.
// Never scans a QR, sends a real message, captures the QR or writes credentials.
const assert=require('node:assert/strict'),fs=require('node:fs');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_LIVE_PORTAL_URL,credentials=process.env.IA_PORTAL_CREDENTIALS_FILE;
assert.ok(base?.startsWith('http://127.0.0.1:')&&credentials,'Explicit loopback target and private credentials required');
const values=Object.fromEntries(fs.readFileSync(credentials,'utf8').trim().split(/\r?\n/).map(line=>{const i=line.indexOf('=');return[line.slice(0,i),line.slice(i+1)]}));
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});try{
 const context=await browser.newContext({viewport:{width:1440,height:1000}});const page=await context.newPage();const errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/login');
 await page.getByRole('textbox',{name:/^Email/}).fill(values.Bootstrap__AdminEmail);await page.getByLabel(/^Senha/).fill(values.Bootstrap__AdminPassword);
 await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.waitForURL(url=>!url.pathname.includes('/login'));await page.goto(base+'/channels');
 await page.getByRole('heading',{name:'Cadastrar conexão',exact:true}).waitFor();
 const name='WhatsApp - teste local de pareamento';
 await page.getByLabel('Canal selecionado',{exact:true}).count().then(async count=>{
   if(count){await page.getByLabel('Canal selecionado',{exact:true}).click();const option=page.getByRole('option',{name:new RegExp(name)});
     if(await option.count()){await option.click();return;}
     await page.keyboard.press('Escape');
   }
   await page.getByLabel('Nome da conexão',{exact:true}).fill(name);await page.getByLabel('Provedor do canal',{exact:true}).click();
   await page.getByRole('option',{name:'WhatsApp Web · navegador no servidor',exact:true}).click();
   assert.equal(await page.getByLabel('ID da conta externa',{exact:true}).count(),0);
   await page.getByRole('button',{name:'Cadastrar canal',exact:true}).click();
 });
 await page.getByRole('heading',{name:'WhatsApp no servidor',exact:true}).waitFor();
 assert.equal(await page.getByRole('checkbox',{name:'Respostas automáticas reais (documentos autorizados)',exact:true}).isChecked(),false);
 await page.getByRole('button',{name:'Conectar / reconectar',exact:true}).click();
 await page.getByAltText('QR Code privado para vincular WhatsApp').waitFor({timeout:180000});
 assert.equal(await page.getByRole('status').textContent(),'Aguardando pareamento');
 // Deliberately no screenshots/traces/HAR: QR grants access to an account.
 assert.deepEqual(errors,[]);console.log('PASS: real local login, organization-scoped browser channel, automatic replies off and actual Chromium/WhatsApp Web QR generated. No account paired or real message sent.');
 await context.close();
}finally{await browser.close();}})().catch(e=>{console.error(e.name+': '+e.message.split('\n')[0]);process.exitCode=1});

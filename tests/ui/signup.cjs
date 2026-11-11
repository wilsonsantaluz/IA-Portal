const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const {spawn} = require('node:child_process');
const {chromium} = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../..');
const base = 'http://127.0.0.1:15174';
const server = spawn(process.execPath, [path.join(root, 'front/node_modules/vite/bin/vite.js'), '--host', '127.0.0.1', '--port', '15174', '--strictPort'], {cwd: path.join(root, 'front'), stdio:'ignore'});
const output = path.join(root, 'artifacts/signup-ui'); fs.mkdirSync(output, {recursive:true});
(async () => {
 let browser;
 try {
  for(let i=0;i<80;i++) {
   try { if((await fetch(base)).ok) break; } catch {}
   if(i===79) throw new Error('Vite did not start');
   await new Promise(resolve=>setTimeout(resolve,250));
  }
  browser = await chromium.launch({headless:true, executablePath:process.env.IA_CHROME_PATH});
  const context = await browser.newContext({viewport:{width:1280,height:900}});
  const page = await context.newPage(); const errors=[];let confirmations=0,registrations=0;
  page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/api/auth/signup**', async route=>{
   const request=route.request(),pathname=new URL(request.url()).pathname;
   if(pathname.endsWith('/status'))return route.fulfill({json:{enabled:true}});
   if(pathname.endsWith('/confirm')) {
    assert.equal(request.postDataJSON().token,'A'.repeat(64));confirmations++;
    return route.fulfill({json:{message:'E-mail confirmado. Entre com sua senha para acessar o IA-Portal.'}});
   }
   const body=request.postDataJSON();assert.equal(body.email,'visitor@example.test');registrations++;
   return route.fulfill({status:202,json:{message:'Se o e-mail estiver apto para cadastro, você receberá um link de confirmação.'}});
  });
  await page.goto(base+'/login');await page.getByRole('link',{name:'Criar conta no IA-Portal'}).click();
  await page.getByLabel(/^Nome/).fill('Visitante sintético');
  await page.getByLabel(/^E-mail/).fill('visitor@example.test');
  await page.getByLabel(/^Senha/).fill('Synthetic-password-2026');
  await page.getByRole('button',{name:'Receber confirmação por e-mail'}).click();
  await page.getByRole('alert').filter({hasText:'receberá um link'}).waitFor();assert.equal(registrations,1);
  await page.goto(base+'/signup#confirm='+'A'.repeat(64));
  await page.getByRole('button',{name:'Confirmar cadastro',exact:true}).waitFor();
  assert.equal(confirmations,0);assert.equal(new URL(page.url()).hash,'');
  await page.screenshot({path:path.join(output,'confirm-dark.png'),fullPage:true});
  await page.getByRole('button',{name:'Ativar tema claro'}).click();
  await page.screenshot({path:path.join(output,'confirm-light.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
  await page.screenshot({path:path.join(output,'confirm-mobile.png'),fullPage:true});
  await page.getByRole('button',{name:'Confirmar cadastro',exact:true}).click();
  await page.getByRole('alert').filter({hasText:'E-mail confirmado'}).waitFor();assert.equal(confirmations,1);
  assert.deepEqual(errors,[]);
  console.log('PASS: Chromium signup form, explicit confirmation in StrictMode, fragment removed, dark/light/mobile, no page errors. API fixtures only; screenshots artifacts/signup-ui.');
 } finally { if(browser)await browser.close();server.kill(); }
})().catch(e=>{console.error(e);process.exitCode=1});

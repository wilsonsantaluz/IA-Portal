const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const {spawn} = require('node:child_process');
const {chromium} = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../..'), base = 'http://127.0.0.1:15175';
const server = spawn(process.execPath, [path.join(root,'front/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','15175','--strictPort'], {cwd:path.join(root,'front'),stdio:'ignore'});
const output=path.join(root,'artifacts/audit-login-ui');fs.mkdirSync(output,{recursive:true});
const admin={userId:2,userName:'Operador de teste',email:'operator@example.test',organizationId:2,permissions:['audit.read'],roles:['Admin'],token:'synthetic-token'};
const row={id:5,createdAt:new Date().toISOString(),organizationId:2,actorUserId:2,actorName:'Operador de teste',action:'Entrar no IA-Portal',operation:'Auth.Login',area:'Autenticação',method:'POST',route:'/api/auth/login',targetId:null,outcome:'success',statusCode:200,durationMs:38,requestId:'synthetic-reference',remoteAddress:'127.0.0.1'};
(async()=>{
 let browser;
 try{
  for(let i=0;i<80;i++){try{if((await fetch(base)).ok)break;}catch{}if(i===79)throw Error('Vite did not start');await new Promise(r=>setTimeout(r,250));}
  browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});
  const login=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
  const page=await login.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await login.route('**/api/**',r=>r.request().url().endsWith('/api/auth/signup/status') ? r.fulfill({json:{enabled:true}}) : r.fulfill({status:401,json:{error:'Credenciais inválidas'}}));
  await page.goto(base+'/login');await page.getByRole('link',{name:'Criar conta no IA-Portal'}).waitFor();
  assert.equal(await page.locator('.login-float').first().evaluate(e=>getComputedStyle(e).animationName),'none');
  await page.emulateMedia({reducedMotion:'no-preference'});assert.equal(await page.locator('.login-float').first().evaluate(e=>getComputedStyle(e).animationName),'login-float');
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.screenshot({path:path.join(output,'login-dark.png'),fullPage:true});
  await page.getByRole('button',{name:'Ativar tema claro'}).click();await page.screenshot({path:path.join(output,'login-light.png'),fullPage:true});
  await page.getByLabel(/^Senha/).fill('Synthetic-test-password');await page.getByRole('button',{name:'Mostrar senha',exact:true}).click();assert.equal(await page.getByLabel(/^Senha/).getAttribute('type'),'text');
  await page.getByRole('button',{name:'Ocultar senha',exact:true}).click();assert.equal(await page.getByLabel(/^Senha/).getAttribute('type'),'password');
  await page.getByLabel(/^E-mail/).fill('test@example.test');await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.getByRole('alert').filter({hasText:'Usuário ou senha inválidos'}).waitFor();
  await page.getByLabel(/^E-mail/).clear();await page.getByLabel(/^Senha/).clear();
  await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.screenshot({path:path.join(output,'login-mobile.png'),fullPage:true});await login.close();
  const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
  await context.addInitScript(auth=>localStorage.setItem('auth',JSON.stringify(auth)),admin);
  const audit=await context.newPage();audit.on('pageerror',e=>errors.push(e.message));let lastQuery;
  await context.route('**/api/**',async r=>{
   const url=new URL(r.request().url());let data={};
   if(url.pathname==='/api/auth/me')data=admin;
   else if(url.pathname==='/api/organizations')data=[{id:2,name:'Atlas (teste)',slug:'atlas-demo',policyRole:'Admin'}];
   else if(url.pathname==='/api/audit/actions'){
    lastQuery=url.searchParams;
    data=url.searchParams.get('q')==='sem resultado' ? {items:[],nextBeforeId:null,scope:'organization'} : url.searchParams.has('beforeId') ? {items:[{...row,id:2}],nextBeforeId:null,scope:'organization'} : {items:[row,{...row,id:4,action:'Criar ou editar documento',area:'Base de conhecimento',operation:'KnowledgeBase.Save'}],nextBeforeId:3,scope:'organization'};
   }
   await r.fulfill({json:data});
  });
  await audit.goto(base+'/audit');
  try { await audit.getByRole('table',{name:'Registros de ações'}).getByRole('cell',{name:/Entrar no IA-Portal/}).waitFor({timeout:10000}); }
  catch(e) { await audit.screenshot({path:path.join(output,'audit-debug.png'),fullPage:true});console.error('Page errors:',errors);console.error('Rendered alerts:',await audit.getByRole('alert').allTextContents());throw e; }
  await audit.getByRole('button',{name:'Carregar mais'}).click();await audit.getByText('3 registros carregados',{exact:false}).waitFor();
  await audit.screenshot({path:path.join(output,'audit-dark.png'),fullPage:true});
  await audit.getByRole('button',{name:'Ativar tema claro'}).click();await audit.screenshot({path:path.join(output,'audit-light.png'),fullPage:true});
  await audit.getByLabel('Pesquisar usuário, ação ou recurso').fill('sem resultado');await audit.getByRole('button',{name:'Pesquisar',exact:true}).click();await audit.getByText('Nenhum registro encontrado para os filtros selecionados.').waitFor();assert.equal(lastQuery.get('q'),'sem resultado');
  await audit.setViewportSize({width:390,height:844});
  try { await audit.waitForFunction(()=>document.documentElement.scrollWidth<=innerWidth,{timeout:3000}); }
  catch(e) { await audit.screenshot({path:path.join(output,'audit-mobile-debug.png'),fullPage:true});console.error(await audit.evaluate(()=>[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+1 && getComputedStyle(e).position!=='fixed').slice(0,12).map(e=>[e.tagName,e.className,e.getBoundingClientRect().width])));throw e; }
  await audit.screenshot({path:path.join(output,'audit-mobile.png'),fullPage:true});
  const logout = audit.waitForRequest(r=>new URL(r.url()).pathname==='/api/auth/logout');
  await audit.getByRole('button',{name:'Sair',exact:true}).click();
  const signout=await logout;assert.equal(signout.method(),'POST');assert.equal(signout.headers()['authorization'],'Bearer synthetic-token');
  await context.close();
  const viewer={...admin,permissions:['kb.view'],roles:['Viewer']};
  const restricted=await browser.newContext();await restricted.addInitScript(auth=>localStorage.setItem('auth',JSON.stringify(auth)),viewer);
  let auditRequested=false;await restricted.route('**/api/**',r=>{const url=new URL(r.request().url());if(url.pathname==='/api/audit/actions')auditRequested=true;return r.fulfill({json:url.pathname==='/api/auth/me'?viewer:[]});});
  const denied=await restricted.newPage();await denied.goto(base+'/audit');await denied.getByRole('heading',{name:'Acesso não permitido'}).waitFor();assert.equal(await denied.getByRole('link',{name:'Log de ações',exact:true}).count(),0);assert.equal(auditRequested,false);
  assert.deepEqual(errors,[]);
  console.log('PASS: login dark/light/mobile, reduced-motion and animated modes, password visibility and login errors; audit search/pagination/empty state/themes/mobile; viewer route/nav denied. API fixtures only; artifacts/audit-login-ui.');
 }finally{if(browser)await browser.close();server.kill();}
})().catch(e=>{console.error(e);process.exitCode=1});

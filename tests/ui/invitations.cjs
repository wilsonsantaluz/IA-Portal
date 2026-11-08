const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_UI_BASE_URL||'http://127.0.0.1:15173';
const output=path.resolve(__dirname,'../../artifacts/invitations-ui');fs.mkdirSync(output,{recursive:true});
const capability='A'.repeat(64),invitationId='11111111-1111-1111-1111-111111111111';
const expiresAt=new Date(Date.now()+86400000).toISOString();
const permissions=['chat.access','kb.view','kb.manage','access.manage','organizations.manage','organizations.create'];
const owner={userId:1,userName:'Owner sintético',email:'owner@example.test',roles:['Owner'],permissions,organizationId:1,token:'owner-fixture'};
const recipient={userId:3,userName:'Convidado sintético',email:'recipient@example.test',roles:['Viewer'],permissions:['kb.view'],organizationId:2,token:'recipient-fixture'};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});const errors=[],unexpected=[],urls=[];
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();
  let created=false,revoked=false,transferred=false,transfers=0;
  page.on('pageerror',e=>errors.push(e.message));
  await context.addInitScript(auth=>{if(window===window.top&&!localStorage.getItem('auth'))localStorage.setItem('auth',JSON.stringify(auth));},owner);
  await context.route('**/api/**',async route=>{
   const r=route.request(),endpoint=new URL(r.url()).pathname;urls.push(r.url());let data={},status=200;
   assert.equal(r.headers()['x-organization-id'],'1');
   if(endpoint==='/api/auth/me')data={...owner,roles:[transferred?'Admin':'Owner']};
   else if(endpoint==='/api/organizations')data=[{id:1,name:'Workspace A',slug:'default',policyRole:transferred?'Admin':'Owner'}];
   else if(endpoint==='/api/organizations/current/members')data=[{userId:1,userName:'Owner sintético',policyRole:transferred?'Admin':'Owner',isActive:true,accountActive:true,version:transferred?1:0},{userId:2,userName:'Membro sintético',policyRole:transferred?'Owner':'Member',isActive:true,accountActive:true,version:transferred?1:0}];
   else if(endpoint==='/api/organizations/current/invitations'&&r.method()==='POST'){assert.deepEqual(r.postDataJSON(),{role:'Member'});created=true;data={id:invitationId,token:capability,role:'Member',expiresAt};status=201;}
   else if(endpoint==='/api/organizations/current/invitations')data=created?[{id:invitationId,role:'Member',expiresAt,version:revoked?1:0,status:revoked?'revoked':'pending'}]:[];
   else if(endpoint.endsWith('/revoke')){assert.equal(r.postDataJSON().version,0);revoked=true;status=204;}
   else if(endpoint==='/api/organizations/current/transfer-ownership'){assert.deepEqual(r.postDataJSON(),{targetUserId:2,ownerVersion:0,targetVersion:0});transferred=true;transfers++;status=204;}
   else if(endpoint==='/api/organizations/current/ai-default')data={provider:null,model:null,version:0,inherited:true};
   else if(endpoint==='/api/ai/providers')data=[];
   else if(endpoint==='/api/knowledge-templates')data=[];
   else unexpected.push(endpoint);
   await route.fulfill(status===204?{status}:{status,json:data});
  });
  await page.goto(base+'/organizations');await page.getByRole('button',{name:'Gerar link de convite',exact:true}).click();
  await page.getByLabel('Link de convite',{exact:true}).waitFor();assert.equal(await page.getByLabel('Link de convite',{exact:true}).inputValue(),base+'/invite#invite='+capability);
  assert.ok(!(await page.evaluate(()=>JSON.stringify(localStorage))).includes(capability));
  await page.getByRole('button',{name:'Revogar convite '+invitationId,exact:true}).click();await page.getByText('Convite revogado.',{exact:true}).waitFor();assert.equal(await page.getByLabel('Link de convite',{exact:true}).count(),0);
  await page.getByRole('button',{name:'Transferir propriedade',exact:true}).click();await page.getByRole('combobox',{name:'Novo Owner'}).click();await page.getByRole('option',{name:'Membro sintético',exact:true}).click();
  await page.getByText('O novo Owner terá privilégios de administração da instalação.',{exact:false}).waitFor();
  assert.equal(transfers,0);await page.getByRole('button',{name:'Cancelar',exact:true}).click();assert.equal(transfers,0);
  for(const theme of ['dark','light']){if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();await page.screenshot({path:path.join(output,'owner-'+theme+'.png'),fullPage:true});}
  await page.mouse.move(0,0);await page.getByRole('tooltip').waitFor({state:'hidden'});
  for(const width of [390,320]){await page.setViewportSize({width,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}
  if(process.env.IA_AXE_MODULE){const AxeBuilder=require(process.env.IA_AXE_MODULE).default;const result=await new AxeBuilder({page}).include('main').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();assert.deepEqual(result.violations.map(v=>v.id),[]);}
  await page.getByRole('button',{name:'Transferir propriedade',exact:true}).click();await page.getByRole('combobox',{name:'Novo Owner'}).click();await page.getByRole('option',{name:'Membro sintético',exact:true}).click();
  await page.getByRole('button',{name:'Confirmar transferência',exact:true}).click();await page.getByText('Propriedade transferida. Você agora é Admin nesta organização.',{exact:true}).waitFor();assert.equal(transfers,1);
  assert.equal(await page.getByRole('button',{name:'Gerar link de convite',exact:true}).count(),0);await context.close();

  const guest=await browser.newContext({viewport:{width:390,height:844}});const invite=await guest.newPage();let joined=false,accepts=0,loginAttempts=0;
  invite.on('pageerror',e=>errors.push(e.message));
  await guest.route('**/api/**',async route=>{
   const r=route.request(),endpoint=new URL(r.url()).pathname;urls.push(r.url());let data={},status=200;const org=Number(r.headers()['x-organization-id']);
   if(endpoint==='/api/auth/login'){loginAttempts++;if(loginAttempts===1){status=401;data={error:'Invalid credentials'};}else data=recipient;}
   else if(endpoint==='/api/auth/me'){assert.ok([1,2].includes(org));assert.ok(org===2||joined);data={...recipient,organizationId:org,roles:org===1?['Member']:['Viewer'],permissions:org===1?['chat.access','kb.view','kb.manage']:['kb.view']};}
   else if(endpoint==='/api/organizations')data=joined?[{id:1,name:'Workspace A',slug:'default',policyRole:'Member'},{id:2,name:'Workspace B',slug:'b',policyRole:'Viewer'}]:[{id:2,name:'Workspace B',slug:'b',policyRole:'Viewer'}];
   else if(endpoint==='/api/organization-invitations/preview'){if(r.postDataJSON().token===capability)data={name:'Workspace A',role:'Member',expiresAt,alreadyMember:false};else{status=404;data={error:'Convite inválido ou indisponível.'};}}
   else if(endpoint==='/api/organization-invitations/accept'){assert.equal(r.postDataJSON().token,capability);assert.equal(org,2);joined=true;accepts++;data={organizationId:1,alreadyMember:false};}
   else if(endpoint==='/api/organizations/current/ai-default')data={provider:null,model:null,inherited:true,version:0};
   else if(endpoint==='/api/ai/providers'||endpoint==='/api/knowledge-templates')data=[];
   else unexpected.push(endpoint);
   await route.fulfill({status,json:data});
  });
  await invite.goto(base+'/invite#invite='+capability);await invite.waitForURL(base+'/login');
  await invite.getByRole('textbox',{name:/^Email/}).fill('recipient@example.test');await invite.getByLabel(/^Senha/).fill('WrongSyntheticPassword');await invite.getByRole('button',{name:'Entrar',exact:true}).click();
  await invite.getByText('Usuário ou senha inválidos',{exact:true}).waitFor();await invite.getByLabel(/^Senha/).fill('ValidSyntheticPassword123');await invite.getByRole('button',{name:'Entrar',exact:true}).click();
  await invite.getByRole('button',{name:'Aceitar convite e entrar na organização',exact:true}).waitFor();
  await invite.locator('main').getByText('recipient@example.test',{exact:true}).waitFor();
  assert.equal(invite.url(),base+'/invite');assert.equal(accepts,0);assert.ok(!(await invite.evaluate(()=>JSON.stringify(localStorage))).includes(capability));
  await invite.getByRole('button',{name:'Aceitar convite e entrar na organização',exact:true}).click();await invite.waitForURL(base+'/organizations');await invite.locator('main').getByRole('heading',{name:'Organizações',exact:true}).waitFor();
  assert.equal(accepts,1);assert.equal(await invite.evaluate(()=>JSON.parse(localStorage.auth).organizationId),1);
  await invite.goto(base+'/invite#invite='+'C'.repeat(64));await invite.getByText('Convite inválido ou indisponível.',{exact:true}).waitFor();assert.equal(await invite.getByRole('button',{name:'Aceitar convite e entrar na organização',exact:true}).count(),0);
  assert.ok(urls.every(url=>!url.includes(capability)));assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);
  await guest.close();console.log('PASS: owner create/revoke and one-time display, explicit ownership confirmation/cancel, login retry and invitation return, fragment removal/no localStorage or request-URL capability, consent-only join and validated tenant switch; themes/mobile/optional axe. API fixtures.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});

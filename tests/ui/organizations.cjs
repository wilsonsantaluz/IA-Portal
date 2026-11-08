const assert=require('node:assert/strict');const fs=require('node:fs');const path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_UI_BASE_URL||'http://127.0.0.1:15173';
const output=path.resolve(__dirname,'../../artifacts/community-ui');fs.mkdirSync(output,{recursive:true});
const auth={userId:1,userName:'Owner sintético',email:'owner@example.test',token:'synthetic-only',roles:['Owner'],permissions:['chat.access','kb.view','kb.manage','access.manage','organizations.manage','organizations.create'],organizationId:1};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});
 const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();const errors=[];const unexpected=[];
 const organizations=[{id:1,name:'Organização padrão',slug:'default',policyRole:'Owner'}];let settings={provider:null,model:null,inherited:true,version:0};let switched=false;
 page.on('pageerror',e=>errors.push(e.message));
 await context.addInitScript(auth=>{if(window===window.top&&!localStorage.getItem('auth'))localStorage.setItem('auth',JSON.stringify(auth));},auth);
 await context.route('**/api/**',async route=>{
  const request=route.request(),endpoint=new URL(request.url()).pathname;const org=Number(request.headers()['x-organization-id']);assert.ok([1,2].includes(org));let data={};let status=200;
  if(endpoint==='/api/auth/me'){if(org===2)switched=true;data={...auth,organizationId:org,permissions:org===1?auth.permissions:auth.permissions.filter(p=>p!=='organizations.create')};}
  else if(endpoint==='/api/organizations'&&request.method()==='POST'){const body=request.postDataJSON();assert.equal(org,1);assert.equal(body.slug,'synthetic-workspace');organizations.push({id:2,name:body.name,slug:body.slug,policyRole:'Owner'});data=organizations[1];status=201;}
  else if(endpoint==='/api/organizations')data=organizations;
  else if(endpoint==='/api/organizations/current/members')data=[{userId:1,userName:'Owner sintético',policyRole:'Owner',isActive:true,version:0}];
  else if(endpoint==='/api/organizations/current/ai-default'&&request.method()==='PUT'){assert.equal(org,2);settings={...request.postDataJSON(),version:1,inherited:false};data=settings;}
  else if(endpoint==='/api/organizations/current/ai-default')data=settings;
  else if(endpoint==='/api/ai/providers')data=[{id:'deepseek',models:['fixture'],defaultModel:'fixture',streaming:true,apiBilling:'external'}];
  else if(endpoint==='/api/knowledge-templates')data=[JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../ai-gateway/Application/Templates/technical-support.v1.json'),'utf8'))];
  else if(endpoint==='/api/knowledge-templates/technical-support/apply'){assert.equal(org,2);data={id:'technical-support',version:'1.0.0',imported:2,skipped:0};}
  else if(endpoint==='/api/chatBot/sessions')data={items:[],nextCursor:null};
  else if(endpoint==='/api/data/sources')data=[];
  else {unexpected.push(endpoint);}
  await route.fulfill({status,json:data});
 });
 try{
  await page.goto(base+'/organizations');await page.locator('main').getByRole('heading',{name:'Organizações',exact:true}).waitFor();
  await page.getByLabel(/^Nome da organização/).fill('Workspace sintético');await page.getByLabel(/^Identificador da organização/).fill('synthetic-workspace');
  await page.getByRole('button',{name:'Criar organização',exact:true}).click();await page.getByText('Organização criada. Selecione-a no topo para configurar sua base.',{exact:true}).waitFor();
  await page.getByRole('combobox',{name:'Organização ativa'}).click();await page.getByRole('option',{name:'Workspace sintético',exact:true}).click();
  await page.waitForURL(base+'/chat');assert.equal(switched,true,'Explicit header for selected organization reached backend');
  assert.equal(await page.evaluate(()=>JSON.parse(localStorage.auth).organizationId),2);
  await page.goto(base+'/organizations');await page.getByRole('combobox',{name:'Modelo padrão da organização'}).click();await page.getByRole('option',{name:'deepseek · fixture',exact:true}).click();
  await page.getByRole('button',{name:'Salvar padrão',exact:true}).click();await page.getByText('Modelo padrão atualizado.',{exact:true}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Criar organização',exact:true}).isVisible(),false);
  await page.getByRole('button',{name:'Importar template de suporte',exact:true}).click();
  await page.getByText('Template importado. Confira os documentos na base e teste as perguntas no chat.',{exact:true}).waitFor();
  for(const theme of ['dark','light']){
   if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
   await page.screenshot({path:path.join(output,'organizations-'+theme+'.png'),fullPage:true});
  }
  await page.mouse.move(0,0);await page.getByRole('tooltip').waitFor({state:'hidden'});
  for(const width of [390,320]){await page.setViewportSize({width,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No horizontal page overflow: '+JSON.stringify(await page.evaluate(()=>Array.from(document.querySelectorAll('body *')).filter(e=>e.getBoundingClientRect().right>innerWidth+1).slice(0,8).map(e=>({tag:e.tagName,class:e.className,right:e.getBoundingClientRect().right})))));}
  if(process.env.IA_AXE_MODULE){const AxeBuilder=require(process.env.IA_AXE_MODULE).default;const result=await new AxeBuilder({page}).include('main').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();assert.deepEqual(result.violations.map(v=>v.id),[]);}
  assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);
  console.log('PASS: organization creation form, refreshed header list, real selector explicit tenant header/reload, tenant default save, light/dark, 390/320px and optional axe. API fixtures only.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});

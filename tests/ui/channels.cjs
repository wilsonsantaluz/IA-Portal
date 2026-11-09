const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_UI_BASE_URL||'http://127.0.0.1:15173';
const out=path.resolve(__dirname,'../../artifacts/channels-ui');fs.mkdirSync(out,{recursive:true});
const id='11111111-1111-1111-1111-111111111111',conversationId='22222222-2222-2222-2222-222222222222';
const auth={userId:1,userName:'Owner sintético',email:'owner@example.test',token:'synthetic-only',roles:['Owner'],permissions:['channels.manage','kb.view'],organizationId:1};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();const errors=[],unexpected=[];
  let connection=null,received=false,human=false;
  page.on('pageerror',e=>errors.push(e.message));
  await context.addInitScript(auth=>localStorage.setItem('auth',JSON.stringify(auth)),auth);
  await context.route('**/api/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname;assert.equal(req.headers()['x-organization-id'],'1');let data={},status=200;
   if(p==='/api/auth/me')data=auth;
   else if(p==='/api/organizations')data=[{id:1,name:'Synthetic organization'}];
   else if(p==='/api/kb/all')data=[{id:7,title:'Garantia sintética',keyName:'warranty'}];
   else if(p==='/api/channels'&&req.method()==='POST'){
    assert.deepEqual(req.postDataJSON(),{displayName:'Canal sintético',provider:'simulator',externalAccountId:'demo',externalPhoneNumberId:'one',knowledgeIds:[7]});
    connection={id,displayName:'Canal sintético',provider:'simulator',status:'simulated',isEnabled:true,autoReply:false,agentEnabled:true,agentId:'local-extractive-v1',retentionDays:30,monthlyLimit:100,version:0};data=connection;
   }else if(p==='/api/channels')data=connection?[connection]:[];
   else if(p==='/api/channels/'+id&&req.method()==='PUT'){assert.equal(req.postDataJSON().version,connection.version);connection={...connection,...req.postDataJSON(),version:connection.version+1};status=204;}
   else if(p.endsWith('/simulate')){assert.equal(req.postDataJSON().contactId,'synthetic-contact');received=true;data={accepted:true};}
   else if(p.endsWith('/conversations'))data=received?[{id:conversationId,humanHandoff:human,version:human?1:0,updatedAt:new Date().toISOString()}]:[];
   else if(p.endsWith('/usage'))data={inbound:received?1:0,outbound:received?1:0,monthlyLimit:100};
   else if(p.endsWith('/messages'))data=[{id:'m1',direction:'inbound',text:'garantia',state:'processed',sourcesJson:'[]',createdAt:new Date().toISOString()},
    {id:'m2',direction:'outbound',text:'[Resposta simulada] Garantia sintética autorizada',state:'simulated',sourcesJson:'[{"id":7,"title":"Garantia sintética"}]',createdAt:new Date().toISOString()}];
   else if(p.endsWith('/handoff')){assert.equal(req.postDataJSON().humanHandoff,true);human=true;status=204;}
   else unexpected.push(p);
   await route.fulfill(status===204?{status}:{status,json:data});
  });
  await page.goto(base+'/channels');await page.getByText('Nenhum canal cadastrado.',{exact:true}).waitFor();
  await page.getByLabel('Nome da conexão',{exact:true}).fill('Canal sintético');await page.getByLabel('ID da conta externa',{exact:true}).fill('demo');await page.getByLabel('ID do número externo',{exact:true}).fill('one');
  await page.getByRole('checkbox',{name:'Garantia sintética',exact:true}).check();await page.getByRole('button',{name:'Cadastrar canal',exact:true}).click();
  await page.getByRole('checkbox',{name:'Respostas automáticas simuladas',exact:true}).waitFor();assert.equal(await page.getByRole('checkbox',{name:'Respostas automáticas simuladas',exact:true}).isChecked(),false);
  await page.getByRole('checkbox',{name:'Respostas automáticas simuladas',exact:true}).click();
  await page.waitForFunction(()=>[...document.querySelectorAll('label')].find(e=>e.textContent==='Respostas automáticas simuladas')?.querySelector('input')?.checked);
  await page.getByLabel('Mensagem sintética',{exact:true}).fill('garantia');await page.getByRole('button',{name:'Simular mensagem recebida',exact:true}).click();
  await page.getByRole('button',{name:/Conversa 22222222/}).click();await page.getByText('[Resposta simulada] Garantia sintética autorizada',{exact:true}).waitFor();
  await page.getByRole('checkbox',{name:'Encaminhar para atendimento humano (pausar agente)',exact:true}).click();
  await page.waitForFunction(()=>[...document.querySelectorAll('label')].find(e=>e.textContent==='Encaminhar para atendimento humano (pausar agente)')?.querySelector('input')?.checked);assert.ok(human);
  for(const theme of ['dark','light']){
   if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
   await page.mouse.move(0,0);await page.getByRole('tooltip').waitFor({state:'hidden'});
   await page.screenshot({path:path.join(out,theme+'.png'),fullPage:true});
   for(const width of [390,320]){await page.setViewportSize({width,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}
   if(process.env.IA_AXE_MODULE){const AxeBuilder=require(process.env.IA_AXE_MODULE).default;const result=await new AxeBuilder({page}).include('main').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();assert.deepEqual(result.violations.map(v=>v.id),[]);}
   await page.setViewportSize({width:1440,height:1000});
  }
  assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);await context.close();
  console.log('PASS: channels empty state/create/authorized documents/default-off/opt-in/simulator/history/handoff; existing shell themes/mobile/axe. API fixtures only.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});

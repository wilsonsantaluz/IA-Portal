const assert=require('node:assert/strict');const fs=require('node:fs');const path=require('node:path');
const {chromium}=require(process.env.IA_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.IA_UI_BASE_URL||'http://127.0.0.1:15173';
const output=path.resolve(__dirname,'../../artifacts/atlas-ui');fs.mkdirSync(output,{recursive:true});
const permissions=['chat.access','kb.view','kb.manage','access.manage','data.query','connectors.manage'];
const auth={userId:1,userName:'Administrador sintético',email:'atlas-ui@example.test',token:'synthetic-ui-fixture',roles:['admin'],permissions,organizationId:2};
const session={id:'11111111-1111-1111-1111-111111111111',title:'Demonstração Atlas',titleSource:'manual',version:1,provider:'deepseek',model:'fixture',createdAt:'2026-11-01T00:00:00Z',updatedAt:'2026-11-01T00:00:00Z'};
const chart={title:'Fixture sintética (BRL)',series:['saldo_centavos'],rows:[{label:'31-60',values:{saldo_centavos:12345}}]};
const text='DEMONSTRAÇÃO — fixture visual sintética.\n\n```atlas-chart\n'+JSON.stringify(chart)+'\n```\n\n```mermaid\nflowchart TD\n A[Receber contestação] --> B[Validar contrato e fatura]\n B --> C[Registrar desfecho]\n```\n\n```mermaid\nnot a valid diagram\n```\n\n```mermaid\nflowchart TD\nclick A "javascript:window.PWNED=1"\n```';
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.IA_CHROME_PATH});
 const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
 const page=await context.newPage();const errors=[];const unexpected=[];const checks=[];
 let expectedOrganization=2;
 page.on('pageerror',e=>errors.push(e.message));
 await context.addInitScript(auth=>{if(window===window.top)localStorage.setItem('auth',JSON.stringify(auth));},auth);
 await context.route('**/api/**',async route=>{
   const request=route.request();const url=new URL(request.url());const endpoint=url.pathname;
   assert.equal(request.headers()['x-organization-id'],String(expectedOrganization));
   let data;
   if(endpoint==='/api/auth/me')data={...auth,organizationId:expectedOrganization};
   else if(endpoint==='/api/organizations')data=[{id:1,name:'Organização padrão',slug:'default'},{id:2,name:'Atlas Serviços Digitais (demonstração)',slug:'atlas-demo'}];
   else if(endpoint==='/api/chatBot/sessions')data={items:expectedOrganization===2?[session]:[],nextCursor:null};
   else if(endpoint.endsWith('/messages'))data={items:[{id:1,role:'user',content:'Mostre os indicadores e o processo de contestação.',createdAt:session.createdAt,status:'completed'},{id:2,role:'assistant',content:text,createdAt:session.createdAt,status:'completed',provider:'deepseek',model:'fixture'}],nextCursor:null};
   else if(endpoint==='/api/ai/providers')data=[{id:'deepseek',models:['fixture'],defaultModel:'fixture',streaming:true,apiBilling:'external'}];
   else if(endpoint==='/api/data/sources')data=expectedOrganization===2?[{id:1,name:'Atlas local',category:'demo',status:'connected'}]:[];
   else if(endpoint==='/api/data-connectors')data=[{id:1,name:'Atlas local',profileId:'atlas_local',category:'demo',endpoint:'http://127.0.0.1:18081/',catalog:'memory',schema:'atlas',tables:['invoices','payments'],enabled:true,groupIds:[2],version:1,status:'connected',testedAt:session.createdAt}];
   else if(endpoint==='/api/data-connectors/profiles')data=[{id:'atlas_local',category:'demo',endpoint:'http://127.0.0.1:18081/',catalog:'memory',schema:'atlas',tables:['invoices','payments']}];
   else if(endpoint==='/api/data-connectors/groups')data=[{id:2,name:'admin'}];
   else if(endpoint==='/api/data-connectors/1/metadata')data=[{table_name:'invoices',column_name:'net_cents',data_type:'bigint'}];
   else if(endpoint==='/api/data-connectors/1/test')data={status:'connected',testedAt:session.createdAt};
   else if(endpoint==='/api/chatBot/sessions/'+session.id)data=session;
   else {unexpected.push(endpoint);data={};}
   await route.fulfill({status:200,json:data});
 });
 try {
   await page.goto(base+'/chat');await page.getByRole('button',{name:'Demonstração Atlas',exact:true}).click();
   await page.getByRole('figure',{name:'Fixture sintética (BRL)'}).waitFor();
   await page.locator('.recharts-bar-rectangle path').first().waitFor();
   assert.ok((await page.locator('.recharts-bar-rectangle path').first().boundingBox()).height>0);
   await page.getByTitle('Diagrama Mermaid').waitFor();
   await page.frameLocator('iframe[title="Diagrama Mermaid"]').locator('svg').waitFor();
   assert.ok((await page.frameLocator('iframe[title="Diagrama Mermaid"]').locator('svg').boundingBox()).height>0);
   assert.equal(await page.getByTitle('Diagrama Mermaid').getAttribute('sandbox'),'');
   assert.ok((await page.getByTitle('Diagrama Mermaid').getAttribute('srcdoc')).includes("default-src 'none'"));
   assert.equal(await page.getByText('Não foi possível renderizar o diagrama. Código original:',{exact:true}).count(),2);
   assert.equal(await page.evaluate(()=>window.PWNED),undefined);
   await page.getByRole('combobox',{name:'Fonte de dados'}).click();await page.getByRole('option',{name:/Atlas local/}).click();
   assert.equal(await page.getByLabel('Data de referência').inputValue(),'2026-11-01');
   await page.getByText('Perguntas sugeridas Atlas',{exact:true}).click();
   await page.getByRole('button',{name:'Mostre o aging por faixa de atraso.',exact:true}).click();
   assert.equal(await page.getByRole('textbox',{name:'Sua pergunta para a IA'}).inputValue(),'Mostre o aging por faixa de atraso.');
   checks.push('Mocked API UI: organization header, source picker/reference and suggested question reuse the existing chat.');
   for(const theme of ['dark','light']) {
     if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
     await page.screenshot({path:path.join(output,'chat-'+theme+'.png'),fullPage:true});
   }
   await page.goto(base+'/data-connectors');await page.locator('main').getByRole('heading',{name:'Data Connectors',exact:true}).waitFor();
   await page.getByRole('button',{name:'Descobrir tabelas e colunas',exact:true}).click();await page.getByRole('cell',{name:'net_cents',exact:true}).waitFor();
   await page.getByRole('button',{name:'Editar',exact:true}).click();assert.equal(await page.getByLabel('Endpoint').inputValue(),'http://127.0.0.1:18081/');
   for(const theme of ['dark','light']) {
     if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
     await page.screenshot({path:path.join(output,'connectors-'+theme+'.png'),fullPage:true});
   }
   for(const width of [390,320]) {
     await page.mouse.move(0,0);await page.getByRole('tooltip').waitFor({state:'hidden'});
     await page.setViewportSize({width,height:844});
     assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Page overflow: '+JSON.stringify(await page.evaluate(()=>Array.from(document.querySelectorAll('body *')).filter(e=>e.getBoundingClientRect().right>innerWidth+1).slice(0,12).map(e=>({tag:e.tagName,class:e.className,right:e.getBoundingClientRect().right})))));
     assert.ok(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),'Main overflow');
   }
   checks.push('Light/dark connector screen, discovery/edit fields and 390/320px layout.');
   await page.goto(base+'/chat');await page.getByRole('combobox',{name:'Fonte de dados'}).click();await page.getByRole('option',{name:/Atlas local/}).click();
   for(const width of [390,320]) {
     await page.setViewportSize({width,height:844});
     assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Chat page overflow');
     assert.ok(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),'Chat main overflow');
   }
   if(process.env.IA_AXE_MODULE) {
     const AxeBuilder=require(process.env.IA_AXE_MODULE).default;
     for(const theme of ['dark','light']) {
       if(await page.locator('html').getAttribute('data-theme')!==theme)await page.getByRole('button',{name:theme==='light'?'Ativar tema claro':'Ativar tema escuro'}).click();
       const result=await new AxeBuilder({page}).include('main').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();
       assert.deepEqual(result.violations.map(v=>({id:v.id,targets:v.nodes.map(n=>n.target)})),[]);
     }
     checks.push('axe-core WCAG A/AA checks passed in main for light/dark mobile chat source controls.');
   }
   checks.push('Numeric chart renders; valid Mermaid uses sandbox with restrictive CSP; invalid/callback diagrams show original text and no substitute diagram.');
   expectedOrganization=1;
   await page.evaluate(auth=>{localStorage.setItem('auth',JSON.stringify({...auth,organizationId:1}));window.dispatchEvent(new StorageEvent('storage',{key:'auth'}));},auth);
   await page.getByRole('combobox',{name:'Organização ativa'}).filter({hasText:'Organização padrão'}).waitFor();
   await page.getByRole('button',{name:'Abrir conversas'}).click();
   await page.getByText('Nenhuma conversa encontrada. Crie uma para começar.',{exact:true}).waitFor();
   assert.equal(await page.getByRole('combobox',{name:'Fonte de dados'}).count(),0);
   checks.push('Cross-tab organization storage change refreshes identity and remounts workspace, clearing prior source/history state.');
   assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);
   fs.writeFileSync(path.join(output,'checks.json'),JSON.stringify({checks,errors,unexpected,api:'HTTP route fixtures; real API covered by smoke_local.py --atlas'},null,2));
   console.log(checks.map(c=>'PASS: '+c).join('\n'));
 } finally {await context.close();await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});

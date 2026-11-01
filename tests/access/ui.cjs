const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const AxeBuilder = require(process.env.IA_AXE_MODULE || '@axe-core/playwright').default;
const credentials = JSON.parse(fs.readFileSync(process.env.IA_ACCESS_CREDENTIALS, 'utf8'));
const base = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:19080';
const output = path.resolve(__dirname, '../../docs/design/screenshots/access');
fs.mkdirSync(output, { recursive: true });
(async () => {
 const browser = await chromium.launch({ headless: true, executablePath: process.env.IA_CHROME_PATH });
 const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
 const page = await context.newPage(); const errors = []; const checks = [];
 page.on('pageerror', e => errors.push(e.message));
 async function screenshot(name) { await page.mouse.move(0, 0); await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
 async function axe() {
  const results = await new AxeBuilder({ page }).include('main').include('[role="dialog"]').withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();
  assert.deepEqual(results.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })), []);
 }
 try {
  await page.goto(base + '/login');
  await page.getByRole('textbox', { name: /^Email/ }).fill(credentials.email);
  await page.getByLabel(/^Senha/).fill(credentials.password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await page.waitForURL('**/chat');
  await page.getByRole('navigation').getByRole('link', { name: 'Grupos e permissões', exact: true }).click();
  await page.getByRole('button', { name: 'Novo grupo', exact: true }).click();
  const suffix = randomUUID().slice(0, 8); let groupName = 'Consulta ' + suffix;
  await page.getByRole('textbox', { name: 'Nome do grupo' }).fill(groupName);
  await page.getByRole('checkbox', { name: 'Consultar conteúdos', exact: true }).check();
  await axe(); await screenshot('group-editor-dark');
  await page.getByRole('button', { name: 'Salvar grupo', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Grupo e permissões salvos' }).waitFor();
  await axe(); await screenshot('groups-dark');
  await page.getByRole('navigation').getByRole('link', { name: 'Usuários', exact: true }).click();
  await page.getByRole('button', { name: 'Novo usuário', exact: true }).click();
  const email = `ui-${suffix}@example.test`; const password = randomUUID() + '!Aa9';
  await page.getByRole('textbox', { name: /^Nome/ }).fill('Usuário de demonstração');
  await page.getByRole('textbox', { name: /^E-mail/ }).fill(email);
  await page.getByLabel(/^Senha inicial/).fill(password);
  await page.getByRole('checkbox', { name: groupName, exact: true }).check();
  await axe(); await screenshot('user-editor-dark');
  await page.getByRole('button', { name: 'Salvar usuário', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Usuário salvo' }).waitFor();
  await axe(); await screenshot('users-dark');
  await page.getByRole('button', { name: 'Ativar tema claro' }).click();
  await axe(); await screenshot('users-light');
  await page.getByRole('navigation').getByRole('link', { name: 'Grupos e permissões', exact: true }).click();
  await page.getByRole('button', { name: 'Novo grupo', exact: true }).waitFor();
  await axe(); await screenshot('groups-light');
  await page.getByRole('button', { name: 'Editar grupo ' + groupName, exact: true }).click();
  groupName += ' revisado';
  await page.getByRole('textbox', { name: 'Nome do grupo' }).fill(groupName);
  await axe(); await screenshot('group-editor-light');
  await page.getByRole('button', { name: 'Salvar grupo', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  await page.getByRole('navigation').getByRole('link', { name: 'Usuários', exact: true }).click();
  await page.getByRole('row').filter({ hasText: email }).getByRole('button').click();
  await page.getByRole('textbox', { name: /^Nome/ }).fill('Usuário de demonstração editado');
  await page.getByRole('checkbox', { name: 'Conta ativa', exact: true }).uncheck();
  await axe(); await screenshot('user-editor-light');
  await page.getByRole('button', { name: 'Salvar usuário', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  await page.getByRole('row').filter({ hasText: email }).getByText('Inativo', { exact: true }).waitFor();
  await page.getByRole('row').filter({ hasText: email }).getByRole('button').click();
  await page.getByRole('checkbox', { name: 'Conta ativa', exact: true }).check();
  await page.getByRole('button', { name: 'Salvar usuário', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  await page.getByRole('row').filter({ hasText: email }).getByText('Ativo', { exact: true }).waitFor();
  await page.getByRole('navigation').getByRole('link', { name: 'Grupos e permissões', exact: true }).click();
  await page.getByRole('button', { name: 'Novo grupo', exact: true }).click();
  const disposable = 'Temporário ' + suffix;
  await page.getByRole('textbox', { name: 'Nome do grupo' }).fill(disposable);
  await page.getByRole('button', { name: 'Salvar grupo', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  await page.getByRole('button', { name: 'Excluir grupo ' + disposable, exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Excluir grupo', exact: true }).click();
  await page.getByRole('dialog').waitFor({ state: 'hidden' });
  checks.push('Maintenance through real forms: rename group/user, deactivate/reactivate user, delete unassigned group with confirmation; both light dialogs accessible.');
  checks.push('Real admin login, group creation with knowledge read permission, user creation with group association, light/dark tables and dialogs accessible.');
  for (const width of [390,320]) {
   await page.setViewportSize({width,height:844});
   await page.waitForFunction(()=>!document.getElementById('desktop-navigation'));
   for (const route of ['users','groups']) {
    await page.goto(base + '/' + route);
    await page.getByRole('button',{name:route==='users'?'Novo usuário':'Novo grupo',exact:true}).waitFor();
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    assert.ok(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth));
    await screenshot(route+'-mobile-'+width);
   }
  }
  checks.push('Users/groups at 390px and 320px: only table scrolls horizontally, page and main remain within viewport.');
  const auth = await page.evaluate(()=>JSON.parse(localStorage.getItem('auth')));
  await page.getByRole('button',{name:'Sair',exact:true}).click(); await page.waitForURL('**/login');
  await page.getByRole('textbox',{name:/^Email/}).fill(email); await page.getByLabel(/^Senha/).fill(password);
  await page.getByRole('button',{name:'Entrar',exact:true}).click(); await page.waitForURL('**/kb');
  assert.equal(await page.getByRole('button',{name:'Novo Registro',exact:true}).count(),0);
  await page.getByRole('button',{name:'Alternar menu de navegação'}).click();
  await page.getByRole('navigation').waitFor();
  assert.equal(await page.getByRole('navigation').getByRole('link',{name:'Usuários',exact:true}).count(),0);
  assert.equal(await page.getByRole('navigation').getByRole('link',{name:'Chat IA',exact:true}).count(),0);
  await page.keyboard.press('Escape');
  await page.goto(base+'/users'); await page.getByText('Acesso não permitido',{exact:true}).waitFor();
  await page.reload(); await page.getByText('Acesso não permitido',{exact:true}).waitFor();
  const status=await page.evaluate(async()=>{const {token}=JSON.parse(localStorage.getItem('auth'));return (await fetch('/api/users',{headers:{Authorization:'Bearer '+token}})).status});
  assert.equal(status,403);
  checks.push('Restricted user lands on knowledge, sees read-only controls, has no admin/chat menu, and cannot bypass frontend/API guards with direct URL or reload.');
  // Revoke a currently logged-in account, then focus the window to refresh frontend access.
  const roles = await context.request.get(base+'/api/roles',{headers:{Authorization:'Bearer '+auth.token}});
  const group=(await roles.json()).find(g=>g.name===groupName);
  const revoke=await context.request.put(base+'/api/roles/'+group.id,{headers:{Authorization:'Bearer '+auth.token},data:{name:groupName,permissions:[]}});assert.equal(revoke.status(),200);
  await page.goto(base+'/'); await page.getByText(/Sua conta está ativa, mas ainda não possui acesso/).waitFor();
  checks.push('Revoked group access is reflected by /auth/me after reload without issuing another JWT.');
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(output,'checks.json'),JSON.stringify({checks,errors},null,2));
  console.log(checks.map(c=>'PASS: '+c).join('\n'));
 } finally {await context.close();await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});

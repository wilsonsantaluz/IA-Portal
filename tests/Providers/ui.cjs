const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const AxeBuilder = require(process.env.IA_AXE_MODULE || '@axe-core/playwright').default;
const credentials = JSON.parse(fs.readFileSync(process.env.IA_SESSION_CREDENTIALS, 'utf8'));
assert.ok(credentials.email.endsWith('@example.test'), 'Use a fresh disposable installation');
const base = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:19080';
const output = path.resolve(__dirname, '../../docs/design/screenshots/ai-settings'); fs.mkdirSync(output, { recursive: true });
(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.IA_CHROME_PATH });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, reducedMotion: 'reduce' });
  const page = await context.newPage(); page.setDefaultNavigationTimeout(60000);
  const errors = []; const checks = []; page.on('pageerror', e => errors.push(e.message));
  async function login(p, email, password) { await p.goto(base + '/login'); await p.getByRole('textbox', { name: /^Email/ }).fill(email); await p.getByLabel(/^Senha/).fill(password); await p.getByRole('button', { name: 'Entrar', exact: true }).click(); await p.waitForURL('**/chat', { waitUntil: 'domcontentloaded' }); }
  async function select(label, option) { await page.getByRole('combobox', { name: label }).click(); await page.getByRole('option', { name: option, exact: true }).click(); }
  async function send(question) { await page.getByRole('textbox', { name: 'Sua pergunta para a IA' }).fill(question); const pending = page.waitForResponse(r => r.url().endsWith('/api/ia/ask/stream') && r.request().method() === 'POST'); await page.getByRole('button', { name: 'Enviar pergunta' }).click(); const response = await pending; assert.equal(response.status(), 200); await response.finished(); }
  async function axe() { const r = await new AxeBuilder({ page }).include('main').withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze(); assert.deepEqual(r.violations.map(v => ({ id: v.id, targets: v.nodes.map(n => n.target) })), []); }
  async function shot(name) { await page.locator('main').evaluate(e => { e.scrollTop = 0; }); await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
  try {
    let ready = false; for (let i = 0; i < 120; i++) { try { if ((await context.request.get(base + '/api/usage/summary')).status() === 401) { ready = true; break; } } catch {} await page.waitForTimeout(500); } assert.ok(ready);
    await login(page, credentials.email, credentials.password);
    const auth = await page.evaluate(() => JSON.parse(localStorage.getItem('auth'))); const headers = { Authorization: 'Bearer ' + auth.token };
    const initial = await context.request.get(base + '/api/chatBot/sessions', { headers }); assert.equal((await initial.json()).items.length, 0, 'Requires fresh test database');
    await page.goto(base + '/settings/ai');
    const deepseek = page.getByRole('region', { name: 'Provedor DeepSeek', exact: true });
    await deepseek.getByRole('textbox', { name: 'Modelos permitidos DeepSeek', exact: true }).fill('deepseek-chat, deepseek-fixture, deepseek-fixture-ui');
    const saved = page.waitForResponse(r => r.url().endsWith('/api/admin/ai/providers/deepseek') && r.request().method() === 'PUT');
    await deepseek.getByRole('button', { name: 'Salvar DeepSeek', exact: true }).click(); assert.equal((await saved).status(), 200);
    await page.reload(); await page.waitForFunction(() => document.querySelector('textarea')?.value.includes('deepseek-fixture-ui'));
    await select('Modelo padrão global', 'OpenAI · openai-fixture');
    await page.getByRole('button', { name: 'Salvar padrão global', exact: true }).click(); await page.getByText('Padrão global salvo. Preferências pessoais e conversas existentes foram preservadas.', { exact: true }).waitFor();
    await page.getByText('Carregando configurações…', { exact: true }).waitFor({ state: 'hidden' });
    await select('Modelo padrão pessoal', 'Ollama · local-fixture');
    await page.getByRole('button', { name: 'Salvar preferência', exact: true }).click(); await page.getByText('Preferência salva. Será usada nas novas conversas.', { exact: true }).waitFor();
    const before = await context.request.get(base + '/api/usage/summary', { headers });
    await deepseek.getByRole('button', { name: 'Testar DeepSeek', exact: true }).click(); await page.getByText(/Conectividade confirmada/).waitFor();
    const after = await context.request.get(base + '/api/usage/summary', { headers }); assert.equal((await before.json()).requests, (await after.json()).requests);
    checks.push('Administrative provider models/global default and personal preference persist; metadata connectivity creates no paid inference usage.');
    await axe(); await shot('settings-dark');
    await page.goto(base + '/chat'); await page.getByRole('button', { name: 'Nova conversa', exact: true }).click();
    await page.getByRole('combobox', { name: 'Modelo da conversa' }).waitFor(); assert.ok((await page.getByRole('combobox', { name: 'Modelo da conversa' }).innerText()).includes('local-fixture'));
    await send('Primeiro assunto sintetico'); await page.getByText('Resposta local.', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Titulo local', exact: true }).waitFor();
    await select('Modelo da conversa', 'deepseek · deepseek-fixture-ui'); await page.getByRole('dialog').getByRole('button', { name: 'Confirmar troca', exact: true }).click(); await page.getByRole('dialog').waitFor({ state: 'hidden' });
    await page.waitForFunction(() => [...document.querySelectorAll('[role=combobox]')].some(e => e.textContent.includes('deepseek-fixture-ui')));
    await send('Continue o assunto sintetico'); await page.getByText('Resposta de teste.', { exact: true }).waitFor();
    await page.reload(); await page.getByText('Resposta local.', { exact: true }).waitFor(); await page.getByText('Resposta de teste.', { exact: true }).waitFor();
    const listing = await context.request.get(base + '/api/chatBot/sessions', { headers }); const session = (await listing.json()).items[0];
    for (let i = 0; i < 31; i++) { const r = await context.request.post(base + '/api/chatBot/ask', { headers, data: { sessionId: session.id, requestId: randomUUID(), question: 'Interacao sintetica ' + i } }); assert.equal(r.status(), 200); }
    checks.push('New conversation honors personal model; confirmed provider/model change preserves earlier answers and labels after reload.');
    await page.goto(base + '/usage'); await page.getByRole('heading', { name: 'Suas chamadas', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Carregar mais chamadas', exact: true }).click(); await page.getByRole('button', { name: 'Carregar mais chamadas', exact: true }).waitFor({ state: 'hidden' });
    assert.equal(await page.getByRole('table', { name: 'Chamadas de IA', exact: true }).locator('tbody tr').count(), 34);
    assert.ok((await page.locator('main').innerText()).includes('soma parcial'));
    const failed = await context.request.post(base + '/api/chatBot/ask', { headers, data: { sessionId: session.id, requestId: randomUUID(), question: '__provider_failure__' } }); assert.equal(failed.status(), 502);
    await page.reload(); await page.getByText('Cobertura parcial', { exact: true }).waitFor();
    await axe(); await shot('usage-dark');
    await page.getByRole('tab', { name: 'Todos os usuários', exact: true }).click(); await page.getByRole('heading', { name: 'Por usuário', exact: true }).waitFor();
    assert.equal(await page.getByRole('heading', { name: 'Suas chamadas', exact: true }).count(), 0);
    checks.push('Real usage cards/tables distinguish partial costs, local infrastructure and unknown values; detail pagination loads 34 real persisted calls; admin view exposes aggregates only.');
    const readerPassword = randomUUID() + '!Aa9'; const group = await context.request.post(base + '/api/roles', { headers, data: { name: 'Synthetic chat readers', permissions: ['chat.access'] } }); const role = await group.json();
    const created = await context.request.post(base + '/api/users', { headers, data: { userName: 'Synthetic Reader', userEmail: 'reader@example.test', password: readerPassword, groupIds: [role.id] } }); assert.equal(created.status(), 201);
    const loginResponse = await context.request.post(base + '/api/auth/login', { data: { email: 'reader@example.test', password: readerPassword } }); const reader = await loginResponse.json(); const readerHeaders = { Authorization: 'Bearer ' + reader.token };
    assert.equal((await context.request.get(base + '/api/admin/ai/providers', { headers: readerHeaders })).status(), 403);
    assert.equal((await context.request.get(base + '/api/usage/summary?sessionId=' + session.id, { headers: readerHeaders })).status(), 404);
    const readerContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } }); const readerPage = await readerContext.newPage();
    await login(readerPage, 'reader@example.test', readerPassword); await readerPage.goto(base + '/settings/ai'); await readerPage.getByRole('heading', { name: 'Seu modelo padrão', exact: true }).waitFor(); assert.equal(await readerPage.getByRole('region', { name: /Provedor/ }).count(), 0);
    await readerPage.goto(base + '/usage'); await readerPage.getByText('Nenhuma chamada registrada para este período e filtros.', { exact: true }).waitFor(); assert.equal(await readerPage.getByRole('tab', { name: 'Todos os usuários', exact: true }).count(), 0); await readerContext.close();
    checks.push('Regular user sees only personal preferences and own empty consumption; direct admin API and another owner conversation usage are rejected.');
    for (const theme of ['light', 'dark']) {
      if ((await page.locator('html').getAttribute('data-theme')) !== theme) await page.getByRole('button', { name: theme === 'light' ? 'Ativar tema claro' : 'Ativar tema escuro' }).click();
      for (const route of ['settings/ai', 'usage']) {
        await page.setViewportSize({ width: 1440, height: 1100 }); await page.goto(base + '/' + route); await page.getByRole('combobox', { name: route === 'usage' ? 'Finalidade' : 'Modelo padrão global' }).waitFor(); await axe(); if (theme === 'light') await shot(route === 'usage' ? 'usage-light' : 'settings-light');
        for (const width of [390, 320]) { await page.setViewportSize({ width, height: 844 }); await axe(); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); assert.ok(await page.locator('main').evaluate(e => e.scrollWidth <= e.clientWidth)); if (width === 390) await shot((route === 'usage' ? 'usage' : 'settings') + '-' + theme + '-mobile'); }
      }
    }
    checks.push('Settings/usage accessibility and overflow checks pass in light/dark themes at desktop, 390px and 320px.');
    assert.deepEqual(errors, []); fs.writeFileSync(path.join(output, 'checks.json'), JSON.stringify({ checks, errors }, null, 2)); console.log(checks.map(c => 'PASS: ' + c).join('\n'));
  } catch (error) {
    await page.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {});
    fs.writeFileSync(path.join(output, 'failure-controls.json'), JSON.stringify(await page.evaluate(() => ({ url: location.pathname, text: (document.querySelector('main') || document.body).innerText.slice(0,1800), controls: [...document.querySelectorAll('main [role],main label')].map(n => ({ role: n.getAttribute('role'), label: n.getAttribute('aria-label'), labelledBy: n.getAttribute('aria-labelledby'), text: n.textContent.slice(0,100) })) })), null, 2));
    throw error;
  } finally { await context.close(); await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });

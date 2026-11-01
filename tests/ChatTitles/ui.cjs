const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const credentials = JSON.parse(fs.readFileSync(process.env.IA_SESSION_CREDENTIALS, 'utf8'));
assert.ok(credentials.email.endsWith('@example.test'), 'Use a disposable synthetic account');
const base = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:19080';
(async () => {
  const browser = await chromium.launch({headless: true, executablePath: process.env.IA_CHROME_PATH});
  const page = await browser.newPage({viewport: {width: 1440, height: 1000}, reducedMotion: 'reduce'});
  page.setDefaultNavigationTimeout(60000);
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  try {
    let ready = false;
    for (let i = 0; i < 120; i++) {
      try { if ((await page.request.get(base + '/swagger/v1/swagger.json')).status() === 200) { ready = true; break; } } catch {}
      await page.waitForTimeout(500);
    }
    assert.ok(ready, 'Disposable gateway not ready');
    await page.goto(base + '/login');
    await page.getByRole('textbox', {name: /^Email/}).fill(credentials.email);
    await page.getByLabel(/^Senha/).fill(credentials.password);
    await page.getByRole('button', {name: 'Entrar', exact: true}).click();
    await page.waitForURL('**/chat', {waitUntil: 'domcontentloaded'});
    await page.getByRole('button', {name: 'Nova conversa', exact: true}).click();
    async function send(question) {
      await page.getByRole('textbox', {name: 'Sua pergunta para a IA'}).fill(question);
      const response = page.waitForResponse(r => r.url().endsWith('/api/ia/ask/stream') && r.request().method() === 'POST');
      await page.getByRole('button', {name: 'Enviar pergunta'}).click();
      await (await response).finished();
    }
    await send('Organizar recebiveis sinteticos');
    await page.getByRole('button', {name: 'Organizacao de recebiveis', exact: true}).waitFor();
    await page.reload();
    await page.getByRole('button', {name: 'Organizacao de recebiveis', exact: true}).waitFor();
    await page.getByText('Resposta de teste.', {exact: true}).waitFor();
    const output = path.resolve(__dirname, '../../docs/design/screenshots/titles');
    fs.mkdirSync(output, {recursive: true});
    await page.screenshot({path: path.join(output, 'automatic-dark.png'), fullPage: true});
    await page.getByRole('button', {name: 'Renomear conversa Organizacao de recebiveis', exact: true}).click();
    await page.getByRole('textbox', {name: /^Título/}).fill('Meu planejamento');
    await page.getByRole('button', {name: 'Salvar título', exact: true}).click();
    await page.getByRole('dialog').waitFor({state: 'hidden'});
    await send('Continue o planejamento');
    await page.reload();
    await page.getByRole('button', {name: 'Meu planejamento', exact: true}).waitFor();
    assert.equal(await page.getByText('Resposta de teste.', {exact: true}).count(), 2);
    await page.getByRole('button', {name: 'Nova conversa', exact: true}).click();
    await send('__title_failure__ organizar dados');
    await page.getByRole('button', {name: '__title_failure__ organizar dados', exact: true}).waitFor();
    await page.getByText('Resposta de teste.', {exact: true}).waitFor();
    assert.equal(await page.getByRole('alert').count(), 0, 'Title failure became a chat error');
    assert.deepEqual(errors, []);
    await page.screenshot({path: path.join(output, 'fallback-dark.png'), fullPage: true});
    console.log('PASS: browser shows automatic title after first answer and reload, preserves manual title on follow-up, and title failure keeps a successful answer with fallback.');
  } finally { await browser.close(); }
})().catch(e => {console.error(e); process.exitCode = 1;});

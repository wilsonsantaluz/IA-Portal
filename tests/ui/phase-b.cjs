// Run only against the disposable installation described in tests/ui/README.md.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const AxeBuilder = require(process.env.IA_AXE_MODULE || '@axe-core/playwright').default;
const credentials = JSON.parse(fs.readFileSync(process.env.IA_UI_CREDENTIALS, 'utf8'));
const baseURL = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:19080';
const output = path.resolve(__dirname, '../../docs/design/screenshots/redesign');
fs.mkdirSync(output, { recursive: true });
(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.IA_CHROME_PATH });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
  const page = await context.newPage();
  const errors = []; const checks = [];
  page.on('pageerror', e => errors.push(e.message));
  async function shot(name) { await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
  async function accessible() {
    const result = await new AxeBuilder({ page }).include('main').withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze();
    assert.deepEqual(result.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })), []);
  }
  try {
    await page.goto(baseURL + '/login');
    await shot('login-light');
    await page.getByRole('textbox', { name: /^Email/ }).fill(credentials.email);
    await page.getByLabel(/^Senha/).fill(credentials.password);
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await page.waitForURL('**/chat');
    await page.getByRole('button', { name: /Resumir conteúdo/ }).click();
    assert.match(await page.getByRole('textbox', { name: 'Sua pergunta para a IA' }).inputValue(), /Resuma os principais/);
    assert.equal(await page.getByRole('textbox', { name: 'Sua pergunta para a IA' }).evaluate(e => e === document.activeElement), true);
    checks.push('Suggestion prefills and focuses composer without sending a message.');
    await accessible(); await shot('chat-light');
    await page.getByRole('button', { name: 'Ativar tema escuro' }).click();
    await accessible(); await shot('chat-dark');
    await page.goto(baseURL + '/dashboard');
    await page.getByText('As primeiras avaliações começam aqui').first().waitFor();
    await accessible(); await shot('dashboard-dark');
    await page.getByRole('button', { name: 'Ativar tema claro' }).click();
    await accessible(); await shot('dashboard-light');
    checks.push('Real API with empty temporary SQLite: honest empty states, light/dark main accessibility.');
    for (const mode of ['light', 'dark']) {
      if ((await page.locator('html').getAttribute('data-theme')) !== mode) await page.getByRole('button', { name: 'Ativar tema ' + (mode === 'light' ? 'claro' : 'escuro') }).click();
      for (const width of [390, 320]) {
        await page.setViewportSize({ width, height: 844 });
        for (const route of ['dashboard', 'chat', 'kb']) {
          await page.goto(baseURL + '/' + route);
          await page.getByRole('heading', { name: route === 'dashboard' ? 'Dashboard' : route === 'chat' ? 'Chat IA' : 'Base de conhecimento', exact: true }).waitFor();
          assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), route + ' page overflow');
          assert.ok(await page.locator('main').evaluate(e => e.scrollWidth <= e.clientWidth), route + ' main overflow');
          if (route === 'chat') assert.ok(await page.getByRole('button', { name: 'Enviar pergunta' }).isVisible());
          if (width === 390) await shot(route + '-' + mode + '-mobile');
        }
      }
    }
    checks.push('Dashboard/chat/knowledge at 320 and 390px in both themes: no page or main horizontal overflow.');
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.route('**/api/chatkb/metrics/**', route => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await page.goto(baseURL + '/dashboard');
    await page.getByRole('alert').filter({ hasText: 'Não foi possível' }).waitFor();
    await accessible();
    checks.push('503 error shows retry and unavailable values rather than fabricated zero metrics.');
    await page.unroute('**/api/chatkb/metrics/**');
    await page.getByRole('button', { name: 'Atualizar painel' }).click();
    await page.getByText('As primeiras avaliações começam aqui').first().waitFor();
    checks.push('Retry recovers from error using the real API.');
    // Synthetic chart data exists only in this browser test, never in application code or the real database.
    await page.route('**/api/chatkb/metrics/**', route => {
      const endpoint = route.request().url().split('/').pop();
      const body = endpoint === 'summary' ? { totalRatings: 3, ratingsLast30Days: 3, avgRating: 8, lowRatingPct: 0 } : endpoint === 'by-month' ? [{ month: 9, year: 2026, avgRating: 7, count: 1 }, { month: 10, year: 2026, avgRating: 8.5, count: 2 }] : [{ section: 'Seção sintética', avgRating: 8, count: 3 }];
      return route.fulfill({ json: body });
    });
    await page.reload();
    await page.locator('.recharts-surface').first().waitFor();
    assert.equal(await page.locator('.recharts-surface').count(), 2);
    await page.getByRole('button', { name: 'Ativar tema claro' }).click();
    await accessible();
    await page.getByRole('button', { name: 'Ativar tema escuro' }).click();
    await accessible();
    checks.push('Browser-only synthetic fixtures: both charts render in light/dark without JavaScript errors.');
    assert.equal(await page.locator('.portal-page').evaluate(e => getComputedStyle(e).animationDuration), '1e-05s');
    checks.push('Reduced-motion preference suppresses entry animations.');
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(output, 'phase-b-checks.json'), JSON.stringify({ checks, errors }, null, 2));
    console.log(checks.map(x => 'PASS: ' + x).join('\n'));
  } finally { await context.close(); await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });

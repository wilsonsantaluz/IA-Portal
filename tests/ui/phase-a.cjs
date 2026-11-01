// Requires Playwright and @axe-core/playwright. Use a disposable installation with synthetic credentials.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || 'playwright');
const AxeBuilder = require(process.env.IA_AXE_MODULE || '@axe-core/playwright').default;

const baseURL = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:19080';
if (!process.env.IA_UI_CREDENTIALS) throw new Error('IA_UI_CREDENTIALS must point to a temporary synthetic credentials JSON file.');
const credentials = JSON.parse(fs.readFileSync(process.env.IA_UI_CREDENTIALS, 'utf8'));
const screenshotDirectory = path.resolve(__dirname, '../../docs/design/screenshots');
fs.mkdirSync(screenshotDirectory, { recursive: true });

(async () => {
  const browser = await chromium.launch({
    headless: true,
    ...(process.env.IA_CHROME_PATH ? { executablePath: process.env.IA_CHROME_PATH } : {}),
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 }, reducedMotion: 'reduce' });
  const page = await context.newPage();
  const pageErrors = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  const checks = [];
  async function theme(expected) {
    await page.waitForFunction(value => document.documentElement.dataset.theme === value && document.body.dataset.theme === value, expected);
  }
  async function screenshot(name) {
    await page.screenshot({ path: path.join(screenshotDirectory, name), fullPage: true });
  }
  async function shellAccessibility() {
    const result = await new AxeBuilder({ page }).include('header').include('nav').include('.skip-link')
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze();
    assert.deepEqual(result.violations.map(x => ({ id: x.id, impact: x.impact, nodes: x.nodes.map(n => n.target) })), []);
  }
  try {
    await page.goto(`${baseURL}/chat`);
    await page.waitForURL('**/login');
    await theme('light');
    await page.getByLabel('Email', { exact: true }).fill(credentials.email);
    await page.getByLabel('Senha', { exact: true }).fill('wrong-synthetic-password');
    // The existing 401 interceptor reloads login; Phase A preserves that behavior.
    await Promise.all([
      page.waitForEvent('domcontentloaded'),
      page.waitForResponse(response => response.url().endsWith('/api/auth/login') && response.status() === 401),
      page.getByRole('button', { name: 'Entrar', exact: true }).click(),
    ]);
    await page.getByLabel('Email', { exact: true }).fill(credentials.email);
    await page.getByLabel('Senha', { exact: true }).fill(credentials.password);
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await page.waitForURL('**/chat');
    await page.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    checks.push('Protected navigation and real login: invalid credentials rejected, valid synthetic account accepted.');

    assert.equal(await page.locator('html').evaluate(e => getComputedStyle(e).colorScheme), 'light');
    assert.equal(await page.locator('#desktop-navigation').evaluate(e => Math.round(e.getBoundingClientRect().width)), 248);
    assert.equal(await page.getByRole('navigation').getByRole('link', { name: 'Chat IA', exact: true }).getAttribute('aria-current'), 'page');
    await page.waitForTimeout(300);
    await screenshot('phase-a-light-desktop.png');
    await shellAccessibility();
    checks.push('Default light theme, desktop navigation and WCAG A/AA shell checks.');

    await page.getByRole('button', { name: 'Ativar tema escuro', exact: true }).click();
    await theme('dark');
    await screenshot('phase-a-dark-desktop.png');
    await shellAccessibility();
    await page.reload();
    await page.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    await theme('dark');
    checks.push('Dark theme, accessible shell, and preference persistence after reload.');

    const second = await context.newPage();
    await second.goto(`${baseURL}/chat`);
    await second.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    await second.getByRole('button', { name: 'Ativar tema claro', exact: true }).click();
    await theme('light');
    await second.close();
    checks.push('Theme preference synchronizes across tabs.');

    await page.getByRole('button', { name: 'Alternar menu de navegação' }).click();
    await page.waitForFunction(() => Math.round(document.getElementById('desktop-navigation').getBoundingClientRect().width) === 80);
    assert.equal(await page.locator('#desktop-navigation').evaluate(e => Math.round(e.getBoundingClientRect().width)), 80);
    await page.getByRole('navigation').getByRole('link', { name: 'Dashboard', exact: true }).click();
    await page.waitForURL('**/dashboard');
    await page.getByText('Painel de Qualidade da IA', { exact: true }).waitFor();
    await page.getByRole('navigation').getByRole('link', { name: 'Base de conhecimento', exact: true }).click();
    await page.waitForURL('**/kb');
    await page.getByRole('button', { name: 'Novo Registro', exact: true }).waitFor();
    await page.getByRole('navigation').getByRole('link', { name: 'Chat IA', exact: true }).click();
    await page.waitForURL('**/chat');
    await page.getByRole('button', { name: 'Alternar menu de navegação' }).click();
    checks.push('Collapsed sidebar remains usable; existing dashboard, knowledge and chat routes navigate successfully.');

    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(200);
    assert.equal(await page.locator('#desktop-navigation').count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Alternar menu de navegação' }).getAttribute('aria-expanded'), 'false');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await shellAccessibility();
    await screenshot('phase-a-light-mobile.png');
    await page.getByRole('button', { name: 'Alternar menu de navegação' }).click();
    await page.getByRole('navigation').getByRole('link', { name: 'Dashboard', exact: true }).waitFor();
    await screenshot('phase-a-mobile-navigation.png');
    await page.getByRole('navigation').getByRole('link', { name: 'Chat IA', exact: true }).click();
    await page.waitForTimeout(250);
    assert.equal(await page.getByRole('button', { name: 'Alternar menu de navegação' }).getAttribute('aria-expanded'), 'false');
    await page.getByRole('button', { name: 'Ativar tema escuro', exact: true }).click();
    await theme('dark');
    await screenshot('phase-a-dark-mobile.png');
    checks.push('390px mobile layout: no page overflow, accessible header, functional drawer and both themes.');

    await page.setViewportSize({ width: 320, height: 740 });
    await page.waitForTimeout(100);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    checks.push('320px viewport: no horizontal page overflow.');

    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(`${baseURL}/chat`);
    await page.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    await page.keyboard.press('Tab');
    // Reset focus to the document, then check the first keyboard stop.
    await page.evaluate(() => { document.activeElement?.blur(); });
    await page.keyboard.press('Control+Home');
    await page.goto(`${baseURL}/chat`);
    await page.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    await page.keyboard.press('Tab');
    assert.equal(await page.locator(':focus').textContent(), 'Ir para o conteúdo');
    await page.keyboard.press('Enter');
    assert.equal(await page.locator(':focus').getAttribute('id'), 'main-content');
    checks.push('Keyboard skip link reaches the main content.');
    await page.goto(`${baseURL}/chat`);
    await page.getByRole('heading', { name: 'Chat IA', exact: true }).waitFor();
    for (let step = 0; step < 12; step++) {
      await page.keyboard.press('Tab');
      if (await page.locator(':focus').getAttribute('aria-label') === 'Alternar menu de navegação') break;
    }
    assert.equal(await page.locator(':focus').getAttribute('aria-label'), 'Alternar menu de navegação');
    const outline = await page.locator(':focus').evaluate(e => ({ width: parseFloat(getComputedStyle(e).outlineWidth), style: getComputedStyle(e).outlineStyle }));
    assert.ok(outline.width >= 2 && outline.style === 'solid');
    checks.push('Header controls have a visible keyboard focus outline.');

    await page.getByRole('button', { name: 'Sair', exact: true }).click();
    await page.waitForURL('**/login');
    assert.equal(await page.evaluate(() => localStorage.getItem('auth')), null);
    checks.push('Logout clears authentication and returns to login.');
    assert.deepEqual(pageErrors, []);

    // Verify the HTML boot theme independently, with the app JavaScript deliberately blocked.
    const boot = await context.newPage();
    await boot.route('**/assets/*.js', route => route.abort());
    await boot.goto(`${baseURL}/login`);
    assert.equal(await boot.locator('html').getAttribute('data-theme'), 'dark');
    assert.equal(await boot.locator('html').evaluate(e => getComputedStyle(e).backgroundColor), 'rgb(11, 18, 32)');
    await boot.close();
    checks.push('Saved dark theme applies in the initial HTML before app JavaScript runs.');
    fs.writeFileSync(path.join(screenshotDirectory, 'checks.json'), JSON.stringify({ checks, pageErrors }, null, 2) + '\n');
    console.log(checks.map(check => `PASS: ${check}`).join('\n'));
  } finally { await context.close(); await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

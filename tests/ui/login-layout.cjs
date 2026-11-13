// UI-only checks against Vite/preview, with synthetic auth responses (no backend).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const tools = path.join(os.tmpdir(), 'ia-portal-browser-tools', 'node_modules');
const { chromium } = require(process.env.IA_PLAYWRIGHT_MODULE || path.join(tools, 'playwright'));
const AxeBuilder = require(process.env.IA_AXE_MODULE || path.join(tools, '@axe-core/playwright')).default;
const output = path.resolve('artifacts/login-layout');
const base = process.env.IA_LOGIN_URL || 'http://127.0.0.1:19120';
const sizes = [[1920, 1080], [1920, 800], [1366, 768], [1366, 640], [768, 1024], [390, 844]];

(async () => {
  fs.mkdirSync(output, { recursive: true });
  let browser;
  const report = [];
  try {
    for (const mode of ['dark', 'light']) {
      browser = await chromium.launch({ headless: true,
        executablePath: process.env.IA_CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
      const context = await browser.newContext({ reducedMotion: 'reduce' });
      await context.addInitScript(mode => localStorage.setItem('theme-mode', mode), mode);
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      let attempts = 0;
      await page.route('**/api/auth/signup/status', r => r.fulfill({ json: { enabled: true } }));
      await page.route('**/api/auth/login', async r => {
        assert.deepEqual(r.request().postDataJSON(), { email: 'layout@example.test', password: 'SyntheticPassword123!' });
        attempts++;
        await new Promise(resolve => setTimeout(resolve, 400));
        await r.fulfill({ status: 401, json: { error: 'synthetic validation response' } });
      });
      for (const [width, height] of sizes) {
        await page.setViewportSize({ width, height });
        await page.goto(base + '/login');
        await page.getByRole('link', { name: 'Criar conta no IA-Portal' }).waitFor();
        await page.evaluate(async () => {
          await document.fonts.ready;
          await Promise.all(Array.from(document.images, image => image.decode()));
        });
        // Let Chromium paint the theme after navigation before taking evidence.
        await page.waitForTimeout(150);
        const metrics = await page.evaluate(() => {
          const button = document.querySelector('button[type=submit]').getBoundingClientRect();
          const input = document.querySelector('.MuiOutlinedInput-root').getBoundingClientRect();
          return { width: innerWidth, height: innerHeight,
            scrollWidth: document.documentElement.scrollWidth, scrollHeight: document.documentElement.scrollHeight,
            formWidth: document.querySelector('form').getBoundingClientRect().width,
            inputHeight: input.height, buttonHeight: button.height, buttonBottom: button.bottom };
        });
        assert(metrics.scrollWidth <= width, `horizontal overflow: ${mode} ${width}x${height}`);
        assert(metrics.scrollHeight <= height, `unexpected vertical scroll: ${mode} ${width}x${height}`);
        assert(metrics.formWidth <= 460);
        assert(metrics.inputHeight >= 44 && metrics.inputHeight <= 48);
        assert(metrics.buttonHeight >= 44 && metrics.buttonHeight <= 48);
        assert(metrics.buttonBottom <= height);
        if (width >= 900) {
          const art = await page.locator('.login-illustration').evaluate(el => ({ loaded: el.complete && el.naturalWidth > 0, fit: getComputedStyle(el).objectFit }));
          assert(art.loaded && art.fit === 'contain');
        } else {
          assert.equal(await page.locator('.login-story').isVisible(), false);
        }
        await page.screenshot({ path: path.join(output, `${mode}-${width}x${height}.png`), fullPage: true, animations: 'disabled' });
        const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze();
        assert.deepEqual(axe.violations.map(v => ({ id: v.id, targets: v.nodes.map(n => n.target) })), [], `accessibility: ${mode} ${width}x${height}`);
        // Native details remain keyboard operable and preserve the full warning.
        const summary = page.locator('summary');
        await summary.focus();
        await page.keyboard.press('Enter');
        assert.equal(await page.locator('details').getAttribute('open'), '');
        assert(await page.getByText('tente novamente mais tarde.', { exact: false }).isVisible());
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.keyboard.press('Enter');
        report.push({ mode, ...metrics, axeViolations: 0 });
      }
      await page.getByLabel('E-mail', { exact: false }).fill('layout@example.test');
      await page.locator('input[autocomplete=current-password]').fill('SyntheticPassword123!');
      await page.getByRole('button', { name: 'Mostrar senha' }).click();
      assert.equal(await page.locator('input[autocomplete=current-password]').getAttribute('type'), 'text');
      await page.getByRole('button', { name: 'Ocultar senha' }).click();
      await page.getByRole('button', { name: 'Entrar', exact: true }).click();
      await page.getByRole('button', { name: 'Entrando...' }).waitFor();
      assert(await page.getByRole('button', { name: 'Entrando...' }).isDisabled());
      await page.getByRole('alert').filter({ hasText: 'Usuário ou senha inválidos' }).waitFor();
      assert.equal(attempts, 1);
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
      // Reduced heights must scroll normally, keeping everything reachable.
      await page.setViewportSize({ width: 390, height: 360 });
      assert(await page.evaluate(() => document.documentElement.scrollHeight > innerHeight));
      await page.locator('summary').scrollIntoViewIfNeeded();
      await page.screenshot({ path: path.join(output, `${mode}-short-error.png`), fullPage: true });
      await page.getByRole('button', { name: mode === 'dark' ? 'Ativar tema claro' : 'Ativar tema escuro' }).click();
      assert.equal(await page.evaluate(() => localStorage.getItem('theme-mode')), mode === 'dark' ? 'light' : 'dark');
      await page.route('**/api/auth/signup/status', r => r.fulfill({ json: { enabled: false } }));
      await page.goto(base + '/login');
      await page.getByRole('heading', { name: 'Acesse seu espaço' }).waitFor();
      assert.equal(await page.getByRole('link', { name: 'Criar conta no IA-Portal' }).count(), 0);
      assert.deepEqual(errors, []);
      await context.close();
      await browser.close();
      browser = null;
    }
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    console.log(`PASS: ${report.length} layouts; both themes, axe, overflow, short heights, details, password, loading and error state. Auth mocked.`);
  } finally { if (browser) await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });

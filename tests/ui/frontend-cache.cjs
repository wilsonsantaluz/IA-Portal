const assert = require('node:assert/strict');
const base = process.env.IA_UI_BASE_URL || 'http://127.0.0.1:9080';

(async () => {
  for (const path of ['/', '/index.html', '/chat']) {
    const response = await fetch(base + path);
    assert.equal(response.status, 200);
    assert.match(response.headers.get('cache-control') || '', /\bno-cache\b/);
    const html = await response.text();
    const asset = html.match(/src="(\/assets\/[^" ]+\.js)"/);
    assert.ok(asset, 'The shell must reference a versioned JavaScript bundle');
    const bundle = await fetch(base + asset[1]);
    assert.equal(bundle.status, 200);
    assert.match(bundle.headers.get('cache-control') || '', /\bimmutable\b/);
    assert.match(bundle.headers.get('content-type') || '', /javascript/);
  }
  const missing = await fetch(base + '/assets/absent-regression-bundle.js');
  assert.equal(missing.status, 404);
  console.log('PASS: shell revalidation, immutable versioned bundles and missing bundle 404.');
})().catch(error => { console.error(error); process.exitCode = 1; });

// Dependency-only browser launch: no application, game, credentials or network.
const assert = require('node:assert/strict');
const { chromium, firefox, webkit } = require(`/opt/utf-playwright-${process.argv[2]}/node_modules/playwright`);
(async () => {
  for (const engine of process.argv[2] === 'dotnet' ? [chromium] : [chromium, firefox, webkit]) {
    const browser = await engine.launch({ headless: true });
    try {
      const page = await browser.newPage();
      await page.goto('data:text/html,<title>dependency-probe</title>');
      assert.equal(await page.title(), 'dependency-probe');
    } finally { await browser.close(); }
  }
})().catch(() => { console.error('Dependency browser launch failed.'); process.exitCode = 1; });

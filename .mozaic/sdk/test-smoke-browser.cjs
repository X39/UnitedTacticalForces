// Real child-process fixtures for the dependency-only browser probe.
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { writeFileSync } = require('node:fs');
const { test } = require('node:test');
const { startSmoke } = require('./smoke-browser.cjs');

if (process.argv[2] === '--fixture') {
  const mode = process.argv[3];
  if (process.argv[4]) writeFileSync(process.argv[4], String(process.pid));
  if (mode === 'blocked-event-loop') process.on('SIGTERM', () => {});
  const forever = () => new Promise(() => {});
  const page = {
    goto: async (url) => assert.equal(url, 'data:text/html,<title>dependency-probe</title>'),
    title: async () => mode.includes('wrong-title') ? 'private-canary' : 'dependency-probe',
  };
  const context = {
    newPage: () => {
      if (mode === 'blocked-event-loop') while (true) { /* GNU timeout must stop this event loop. */ }
      return mode.includes('page') ? forever() : Promise.resolve(page);
    },
  };
  const browser = {
    newContext: () => mode === 'hang-context' ? forever() : Promise.resolve(context),
    close: () => mode.includes('close') ? forever() : Promise.resolve(),
  };
  const engine = {
    launch: (options) => {
      assert.deepEqual(options, { headless: true });
      return mode === 'hang-launch' ? forever() : Promise.resolve(browser);
    },
  };
  const limits = mode.startsWith('global') ? { phase: 200, profile: 40 } : { phase: 50, profile: 1000 };
  startSmoke({
    profile: mode === 'success-react' ? 'react' : mode === 'invalid-profile' ? 'private-canary' : 'dotnet',
    loadEngines: () => ({ chromium: engine, firefox: engine, webkit: engine }),
    limits,
  });
} else {
  function run(mode) {
    const result = spawnSync(process.execPath, [__filename, '--fixture', mode], {
      encoding: 'utf8', timeout: 3000, windowsHide: true,
    });
    assert.ifError(result.error);
    assert.equal(result.stderr, '');
    assert.ok(!result.stdout.includes('private-canary'));
    assert.throws(() => process.kill(result.pid, 0), { code: 'ESRCH' });
    return result;
  }

  test('successful React route runs the Chromium-only phases and title check', () => {
    const result = run('success-react');
    assert.equal(result.status, 0);
    const expected = [];
    for (const engine of ['chromium']) {
      for (const phase of ['launch', 'new-context', 'new-page', 'navigation', 'title', 'close']) {
        expected.push(`dependency-smoke ${engine} ${phase} start`, `dependency-smoke ${engine} ${phase} pass`);
      }
    }
    expected.push('dependency-smoke probe profile result-pass');
    assert.equal(result.stdout, `${expected.join('\n')}\n`);
  });

  for (const [mode, phase] of [['hang-launch', 'launch'], ['hang-context', 'new-context'],
    ['hang-page', 'new-page'], ['hang-close', 'close']]) {
    test(`hung ${phase} fails promptly with its fixed phase and no retained Node process`, () => {
      const result = run(mode);
      assert.equal(result.status, 1);
      assert.ok(result.stdout.includes(`dependency-smoke chromium ${phase} fail phase-timeout\n`));
      assert.ok(result.stdout.endsWith(`dependency-smoke chromium ${phase} result-fail phase-timeout\n`));
    });
  }

  test('a hung close cannot mask the original title failure', () => {
    const result = run('wrong-title-close');
    assert.equal(result.status, 1);
    assert.ok(result.stdout.includes('dependency-smoke chromium title fail failed\n'));
    assert.ok(result.stdout.includes('dependency-smoke chromium close fail phase-timeout\n'));
    assert.ok(result.stdout.endsWith('dependency-smoke chromium title result-fail failed\n'));
  });

  test('profile deadline bounds a pending page call', () => {
    const result = run('global-page');
    assert.equal(result.status, 1);
    assert.ok(result.stdout.includes('dependency-smoke probe profile fail profile-timeout\n'));
    assert.ok(result.stdout.endsWith('dependency-smoke chromium new-page result-fail profile-timeout\n'));
  });

  test('profile deadline during close retains the original failure', () => {
    const result = run('global-wrong-title-close');
    assert.equal(result.status, 1);
    assert.ok(result.stdout.includes('dependency-smoke probe profile fail profile-timeout\n'));
    assert.ok(result.stdout.endsWith('dependency-smoke chromium title result-fail failed\n'));
  });

  test('unknown profiles fail without printing supplied input', () => {
    const result = run('invalid-profile');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, 'dependency-smoke probe load result-fail failed\n');
  });
}

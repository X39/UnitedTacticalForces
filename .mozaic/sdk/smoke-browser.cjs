// Dependency-only browser launch: no application, game, credentials or network.
const assert = require('node:assert/strict');
const { writeSync } = require('node:fs');

const LIMITS = Object.freeze({ phase: 30_000, profile: 240_000 });

class PhaseFailure extends Error {
  constructor(engine, phase, reason) {
    super('Dependency browser phase failed.');
    this.detail = { engine, phase, reason };
  }
}

function startSmoke({ profile, loadEngines, limits = LIMITS }) {
  const state = { current: { engine: 'probe', phase: 'load' }, failure: null };
  let finished = false;
  const log = (engine, phase, status, reason = '') => {
    try {
      writeSync(1, `dependency-smoke ${engine} ${phase} ${status}${reason ? ` ${reason}` : ''}\n`);
    } catch {
      process.exit(1);
    }
  };
  const finish = (code) => {
    if (finished) return;
    finished = true;
    clearTimeout(profileTimer);
    if (code) {
      const detail = state.failure || { ...state.current, reason: 'failed' };
      log(detail.engine, detail.phase, 'result-fail', detail.reason);
    } else {
      log('probe', 'profile', 'result-pass');
    }
    // Pending API requests or browser-close promises must not retain this process.
    process.exit(code);
  };
  const profileTimer = setTimeout(() => {
    log('probe', 'profile', 'fail', 'profile-timeout');
    state.failure ||= { ...state.current, reason: 'profile-timeout' };
    finish(1);
  }, limits.profile);

  async function phase(engine, name, operation) {
    state.current = { engine, phase: name };
    log(engine, name, 'start');
    let timer;
    try {
      const result = await Promise.race([
        Promise.resolve().then(operation),
        new Promise((_, reject) => {
          timer = setTimeout(() => reject(new PhaseFailure(engine, name, 'phase-timeout')), limits.phase);
        }),
      ]);
      log(engine, name, 'pass');
      return result;
    } catch (error) {
      const failure = error instanceof PhaseFailure ? error : new PhaseFailure(engine, name, 'failed');
      state.failure ||= failure.detail;
      log(engine, name, 'fail', failure.detail.reason);
      throw failure;
    } finally {
      clearTimeout(timer);
    }
  }

  async function probe() {
    if (!['dotnet', 'react'].includes(profile)) throw new Error('Unsupported profile.');
    const engines = loadEngines();
    for (const name of ['chromium']) {
      let browser;
      let failure;
      try {
        browser = await phase(name, 'launch', () => engines[name].launch({ headless: true }));
        const context = await phase(name, 'new-context', () => browser.newContext());
        const page = await phase(name, 'new-page', () => context.newPage());
        await phase(name, 'navigation', () => page.goto('data:text/html,<title>dependency-probe</title>'));
        await phase(name, 'title', async () => assert.equal(await page.title(), 'dependency-probe'));
      } catch (error) {
        failure = error;
      } finally {
        if (browser) {
          try {
            await phase(name, 'close', () => browser.close());
          } catch (error) {
            failure ||= error;
          }
        }
      }
      if (failure) throw failure;
    }
  }
  probe().then(() => finish(0), () => finish(1));
}

module.exports = { startSmoke };
if (require.main === module) {
  const profile = process.argv[2];
  startSmoke({ profile, loadEngines: () => require(`/opt/utf-playwright-${profile}/node_modules/playwright`) });
}

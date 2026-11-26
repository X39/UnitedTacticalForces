# Phase 69 deferred items

Out-of-scope discoveries logged by plan executors. They are not fixed by the plan that found them.

## Found by 69-05

- `tests/e2e/hosting-fleet.spec.ts:558` ("the Command Center nav and the fleet page read Servers") fails in every full
  `playwright test` run of the existing suite: `toHaveTitle(/Servers/)` receives `"Server fleet"`. The same test passes
  when run alone (`-g`), and the failure reproduces unchanged with the original `playwright.config.ts` of base commit
  0dc5b447, so it is pre-existing and independent of the Phase 69 Playwright projects. It looks load or order dependent.
- `tests/e2e/hosting-a11y.spec.ts:215` ("start dialog is accessible and focuses the dismiss button") failed once in the
  run with the original config and passed in the three runs with the new config: a pre-existing flake.
  status: acknowledged

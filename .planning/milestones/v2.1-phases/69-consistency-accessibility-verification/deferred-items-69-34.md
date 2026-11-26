# Deferred items found by 69-34

## Intermittent axe target-size finding in the workspace audit (gap for the workspace slice)

- Test: `tests/e2e/audit/a11y-audit.spec.ts`, `S-07 (de) @workspace`, project `mobile-390`.
- Finding: `.workspace-details__refresh > .button--ghost[type="button"]: target-size (serious): All touch targets must be 24px large, or leave sufficient space`.
- Reproduction: 2 failures in 8 serial repeats (`CI=true npx playwright test --workers=1 --retries=0 --project=mobile-390 -g "a11y.*S-07 \(de\)" --repeat-each=8`); also failed once on the first attempt in a full `CI=true --workers=1` run and passed on retry. The 12-worker full run (841 passed) did not show it.
- Probable cause (unverified): another element overlaps the "Server aktualisieren" ghost button at the moment of the scan right after the saved-settings arrival (a toast or sticky element), or the button is scanned before its stylesheet chunk applied.
- Owner: the workspace slice (69-24 and 69-26) and `WorkspaceShell.tsx`.

## Pipeline budget

- The `react-browser` job (`CI=true npm run test:e2e -- --workers=1`) needs about 48 to 55 minutes for 841 passing tests (525 were done after 31 minutes), above `timeout_minutes: 30` in `.mozaic/pipeline.yml`. Pipeline files were not changed (D-AUTO-1); the pipeline owner decides between a larger timeout, separate jobs per project or more workers.

## Shared install

- `sources/ReactApp/node_modules/@fluentui/react-teaching-popover` lost its `lib` folder, so `vite build` (and therefore `npm run build:assets` and the Playwright web server) fails until `npm ci` is run in the main checkout.

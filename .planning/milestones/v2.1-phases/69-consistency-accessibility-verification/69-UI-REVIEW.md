# Phase 69 — UI Review

**Audited:** 2026-11-25
**Baseline:** 69-UI-SPEC.md (approved design contract)
**Screenshots:** not captured. No dev server was started. The only listener found was on 5173, which is reserved for the main checkout. Per 69-34-SUMMARY, `vite build` and the Playwright web server are also broken by the damaged shared `node_modules`. Visual pillars were scored from code, CSS and the test inventory.
**Interaction captures:** off (workflow.ui_interaction_capture is false). All interaction findings are code-derived.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | No `Common_Cancel` references remain and `copy.ts` is deleted, but `Hosting_Common_Cancel` and retired-key cleanup were not re-verified, and the toast dismiss is a bare "×" glyph. |
| 2. Visuals | 3/4 | One header rhythm and shared state components exist, but the pixel baselines were never generated, so visual regression is unproven. |
| 3. Color | 2/4 | `--quiet` is fixed, but `hosting.css` (Tier A) still holds 84 raw hex literals and `styles.css` 52. |
| 4. Typography | 3/4 | Shared primitives use tokens only, but legacy `styles.css` still has 9 weight 600/800 declarations. |
| 5. Spacing | 2/4 | Shared CSS is token-only, but the ratchet allows 128 off-grid px in `styles.css`, 32 in `ProfileMenu.css`, 20 in `eventsMember.css` and more. |
| 6. Experience Design | 3/4 | Raw controls are essentially gone and enforced by lint. A real intermittent axe target-size failure is unresolved. The CI budget is blown, and baselines and screen-reader checks are deferred. |

**Overall: 16/24**

---

## Top 3 Priority Fixes

1. **Intermittent axe target-size failure on `.workspace-details__refresh` (mobile-390, S-07 de)** — WARNING. It failed 2 of 8 serial repeats, so a CI run with retries reports the suite as flaky and the FND-06 claim is not stable. Fix: find the overlapping element, or give the ghost refresh button the 44px touch height and a stable layout slot. Then re-run with `--repeat-each=8 --retries=0`.
2. **Visual baselines do not exist** (`tests/e2e/__screenshots__` is absent). VER-03's pixel half is unproven, and `UTF_VISUAL_BASELINES=1` cannot compare anything. Fix: run `Update-VisualBaselines.ps1 -Update`, review the 60 images and commit them. Treat this as a ship gate for VER-03, not an optional follow-up.
3. **Raw color and off-grid literals inside Tier A** — WARNING. `features/hosting/hosting.css` has 84 hex and 18 off-grid px, and `finance.css` has 9 hex. The spec says Tier A is fully converged and tokens-only. Fix: tokenize these two sheets and lower the ratchet. Also tokenize `ProfileMenu.css` (7 hex, 32 off-grid px), which is also Tier A.

Further fixes:

4. **CI budget.** The `react-browser` job needs about 50 minutes against a 30-minute `timeout_minutes`. Split the Playwright projects into separate jobs or raise the timeout.
5. **Shared `node_modules` repair.** `react-teaching-popover` is damaged, `build:assets` fails and `npm run ci` cannot exit 0 locally.
6. **Toast dismiss.** It renders a literal "×" in a raw `<button>` (`AppToasts.tsx:131`). The spec calls for an icon-only `ActionButton` with an icon from the approved glyph set, so swap it for one.
7. **Outline ratchet.** The `outlineRemoval` ratchet is 0 everywhere, which is good. Add an explicit test for the two raw `outline: 1px/2px solid` focus rules in `styles.css` (lines 1372 and 3538) that bypass `--utf-focus-ring`.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- Positive: `grep` finds 0 references to `Common_Cancel` or `Hosting_Common_Cancel` in `src` `.ts` and `.tsx`, so D-11 is met for Tier A to C. `features/hosting/copy.ts` is deleted (D-03, D-14). No hard-coded hosting copy turned up in `features/hosting` or `components/server` text nodes.
- WARNING: `AppToasts.tsx:131-136` uses an "×" glyph as visible content. It is `aria-hidden` and labelled `Common_Close`, so it is accessible, but it is not an approved icon from the Design System table.
- WARNING: `Common_Close` is used as the icon dismiss label, which the spec allows. A repo-wide German-walk result was not re-run here, so it rests on the 69-34 report (841 passed, 0 failed).
- Not verified without a render: the nav label sentence-case changes and the h1 equals label test.

### Pillar 2: Visuals (3/4)
- Positive: only three h1 sources remain (`PublicHero.tsx`, `WorkspaceHeader.tsx`, and the bootstrap `main.tsx`), matching D-02. `StateViews.tsx`, `PageStack.tsx`, `statusTones.ts` and `shared-primitives.css` all exist.
- WARNING: there are no pixel baselines (see priority fix 2). The "visual design judgment" and German hero balance reviews are deferred to humans, so the visual contract is unverified by any screenshot.
- WARNING: `EventSidePicker.tsx:39` is a raw `<button>` with `title` as its only extra label. It is a ratchet allow-list candidate and should become `ActionButton` or `FilterChipGroup`.

### Pillar 3: Color (2/4)
- Positive: `--quiet` is `#8b97a7` (`styles.css:1255`). The contrast test was extended. `shared-primitives.css` and `components/server/*.css` have 0 hex or rgb literals.
- BLOCKER-lite (score driver): the ratchet records hex counts of `hosting.css` 84, `styles.css` 52, `eventsMember.css` 24, `commandCenter.css` 14, `finance.css` 9, `ProfileMenu.css` 7, `publicEvents.css` 5 and `modPacks.css` 2. D-06 only froze these. `hosting.css` is a Tier A file the spec expects to be tokenized, so the contract is not met there.
- 60/30/10 and accent-only-on-declared-elements could not be checked without screenshots.

### Pillar 4: Typography (3/4)
- Positive: `shared-primitives.css` uses only `--utf-text-*` and `--utf-weight-bold`, with no `clamp()` and no uppercase header rule found. The Display, Heading, Label and Body roles match the contract.
- WARNING: `styles.css` still has 9 weight 600 or 800 declarations, which contradicts "weights 400 and 700 only". They sit in legacy rules held by the ratchet, and they are reachable from converged pages if those pages share the classes.

### Pillar 5: Spacing (2/4)
- Positive: the shared primitive CSS has only the 2rem toast dismiss (a documented exception) outside tokens.
- WARNING: the off-grid px ratchet allows `styles.css` 128, `ProfileMenu.css` 32, `eventsMember.css` 20, `hosting.css` 18, `wiki.css` 17, `publicEvents.css` 15, `commandCenter.css` 7 and `modPacks.css` 4. The spec's closed rule is that the ratchet may only decrease. That is met, but the shell (`ProfileMenu`) and hosting are Tier A and are not clean.
- Spacing at the 320 and 390 viewports was not measured here. It rests on the layout-metric assertions in the 69-34 run.

### Pillar 6: Experience Design (3/4)
- Positive: the raw-element grep finds only 2 `<button>` in `src/**/*.tsx` outside primitives and the toast, plus 0 `<input>`, `<select>`, `<textarea>` and `<details>`. The ESLint `no-restricted-syntax` rule (`eslint.config.js:27`) covers the five elements plus the button look-alike classes and is error severity. The audit suite exists: `a11y-audit`, `dialog-geometry`, `german-walk`, `route-walk`, `toast-target-size` and the mobile key-screens specs. 841 tests passed, 0 failed, in the default-worker run.
- WARNING: the intermittent `target-size` failure (priority fix 1) leaves FND-06 only conditionally met.
- WARNING: the `CI=true --workers=1` run took 31 minutes for 525 of 841 tests and exceeded the pipeline budget, so CI would time out.
- WARNING: items deferred to humans (screen reader, real phone at 320 and 390, native `beforeunload`, German hero balance, real-env create to Running) are legitimate under D-20 but remain open. The `fails_when` flaky condition applies in CI mode.
- Registry audit: shadcn is not initialized, so it was skipped.

---

## Files Audited
- `.planning/phases/69-consistency-accessibility-verification/69-UI-SPEC.md` (lines 1-389), `69-34-SUMMARY.md`, `deferred-items.md`
- `sources/ReactApp/eslint.config.js`
- `sources/ReactApp/tests/styles/legacy-ratchet.json`
- `sources/ReactApp/src/components/shared-primitives.css`, `AppToasts.tsx`, `ActionControl.tsx`, `Disclosure.tsx`, `StateViews.tsx`, `PageStack.tsx`, `statusTones.ts`
- `sources/ReactApp/src/styles.css`, `src/features/events/EventSidePicker.tsx`, `src/main.tsx`
- `sources/ReactApp/src/features/hosting` and `src/components/server` (greps)
- `sources/ReactApp/tests/e2e/` (inventory)

## Orchestrator Note (2026-11-25)

- The shared `sources/ReactApp/node_modules` damage cited above (`@fluentui/react-teaching-popover` missing `lib`) was repaired before this audit ran: `npx vite build` and the full gate pass on HEAD.
- Post-merge `npx playwright test tests/e2e/audit tests/e2e/journeys` on chromium, mobile-390 and mobile-320: 621 passed, 0 failed, 0 flaky (S-07 de mobile-390 passed; the intermittent target-size case stays tracked in `deferred-items-69-34.md`).
- This audit was code-only (no screenshots) and covered `69-UI-SPEC.md` lines 1-389; visual baselines remain the deferred human follow-up from 69-31.

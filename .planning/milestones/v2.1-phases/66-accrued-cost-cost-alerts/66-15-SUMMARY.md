---
phase: 66-accrued-cost-cost-alerts
plan: 15
subsystem: ui
tags: [cost-alerts, react, settings-page, status-badge, css-tokens, ui-spec, d-16]
status: complete
gap_closure: true

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-10 and 66-11 settings panel, channel availability hints, 66-12 e2e journey
provides:
  - "Cost alerts page column capped by --utf-alerts-page-max-width (48rem); both form sections are workspace-panel cards on the page and the finance panel"
  - "channelStatus(channel, availability) and five Server_Alerts_Channel_Status_* strings (EN/DE) that put a status badge at the start of every channel hint"
  - "Tidied add row (button left, counter right, always rendered), channel list with dividers, closing language line, save footer"
  - "UI-SPEC Gap Closure Amendments (2026-11-21) with the D-16 subsection, and an env-gated screen capture in the e2e spec"
affects: [66-16, 66-17, 66-18, phase-66-verification, uat]

plan_head_before: da332e4332464e0d3475a60f798cd566787752b4
plan_head_after: 968a9d45a0701c66bca2fe3628cd55c92c118d32

actuals:
  tokens: 11200
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Form sections reuse the shared workspace-panel surface from components/server/sections.css instead of a page-local card"
    - "Channel hint is a two column grid (badge, sentence) so wrapped text hangs beside the badge"
    - "Env-gated Playwright describe for review screenshots (UTF_CAPTURE_SCREENS=1), skipped in normal runs"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/src/features/hosting/cost-alert-settings.css
    - sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx
    - sources/ReactApp/src/features/hosting/alertSettingsModel.ts
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/cost-alert-settings.test.tsx
    - sources/ReactApp/tests/hosting/cost-alerts-page.test.tsx
    - sources/ReactApp/tests/hosting/alert-settings-model.test.ts
    - sources/ReactApp/tests/hosting/finance-pages.test.tsx
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - .planning/phases/66-accrued-cost-cost-alerts/66-UI-SPEC.md

key-decisions:
  - "The status badge sits inside the checkbox hint (followed by a real space) so it is announced as part of the description and the In the app description reads 'Always on Shown in ...'"
  - "Badge tones use the --utf-state-* tokens through a doubled selector (.status-badge--tone.cost-alert-settings__badge) that wins over the legacy raw-color .status-badge rules, the same approach as server-state-host"
  - "Card heading margin rules use .cost-alert-settings__panel.workspace-panel h2/h3 so the result does not depend on CSS bundle order against sections.css"

requirements-completed: [CST-04, CST-05, CST-06]

duration: 15min
completed: 2026-11-21
---

# Phase 66 Plan 15: Cost alert settings design pass Summary

**The cost alerts settings page and the finance panel now render their two sections as raised workspace-panel cards in a 48rem capped column, every channel row opens with a tokenized status badge (Always on, Ready, Not linked, No address, Not available), and the add row, channel list, language line and save footer are tidied into one form, with the UI-SPEC amended and the screens reviewed in EN and DE at 1280 and 390px.**

## Performance

- **Duration:** about 15 minutes (start 2026-11-21T19:43:15Z)
- **Tasks:** 3 (task 1 is the tracer)
- **Commits:** 5 (measured from the persisted ledger, base `da332e43`)
- **Files:** 13 modified, none created; no change under `sources/WebApp` or `sources/Api`

## Accomplishments

- **Tracer (task 1):** new token `--utf-alerts-page-max-width: 48rem` caps `.cost-alerts-page`; both sections carry `workspace-panel cost-alert-settings__panel` with a muted description (`workspace-panel__muted`); the finance h3 gets the h2 size and weight. Tests assert the cards on the component (h2 and h3), the capped page wrapper (two panels inside `.cost-alerts-page`) and the finance host (h3 cards, no `.cost-alerts-page` ancestor). The tracer `<verify>` (hosting and style tests, typecheck, lint) passed before task 2 started (auto mode, `⚡ Tracer verified end-to-end — expanding`).
- **Channel badges (task 2):** `channelStatus` in `alertSettingsModel.ts` (Map lookup with a fixed neutral fallback), five EN/DE keys listed in `phase66RequiredKeys`, `Server_Alerts_Channel_InAppHint` no longer starts with "Always on." The panel wraps the channels in `ul.cost-alert-settings__channels`, starts each hint with a `StatusBadge`, closes the card with `p.cost-alert-settings__language` (leading `Info16Regular`, `aria-hidden`), always renders the counter (also at zero rows) in a flex add row with a divider, and the save row is a footer with a divider. Behavior of Save, Discard, validation, conflict, read-only and the leave guard is untouched (all existing cases green).
- **UI-SPEC and screens (task 3):** `## Gap Closure Amendments (2026-11-21)` above Checker Sign-Off with the subsection "Cost alerts page and finance panel (D-16)", the badge table, the new In the app copy, and the explicit list of overridden rows; `--utf-alerts-page-max-width` added to the Spacing Scale table. The e2e spec gained `test.describe("cost alert screens")`, skipped unless `UTF_CAPTURE_SCREENS=1`.

## Screenshot review (task 3)

Captured with `UTF_CAPTURE_SCREENS=1` into `sources/ReactApp/test-results/cost-alert-screens/` (git-ignored) and opened with the Read tool. Reviewed: `settings-pristine-en-1280`, `settings-pristine-en-390`, `settings-pristine-de-1280`, `settings-pristine-de-390`, `settings-readonly-en-1280`, `settings-readonly-de-390` (the other two read-only captures differ only by the same notice and disabled controls and were generated in the same runs).

Checked against the amendment: both cards and the save footer share one left edge; equal card padding; amount rows evenly spaced; the add row shows the counter on the Add line at 1280 and wraps cleanly below the button at 390 in German; each divider sits between two blocks (no orphan), the language line closes the channels card, the save footer divider spans the card width; badges sit on the first line of each hint in both languages; German hints wrap without overflow; the read-only notice sits above the cards with disabled controls.

Defect found and fixed after the first capture (the only CSS change made after review, commit `f93e7b10`): the hint was a plain inline paragraph, so a wrapped sentence and the "Open account settings" link flowed under the badge. `.cost-alert-settings__channels .form-field__hint` is now a two column grid (`auto minmax(0, 1fr)`, baseline aligned) and `.cost-alert-settings__hint-link` takes column 2, so wrapped text and the link hang beside the badge. Re-captured and re-reviewed in EN and DE at both widths.

## Task Commits

1. **Task 1 (tracer): raised cards in a capped column** - `4d2d8f9a` (feat)
2. **Task 2: channel status badges, add row, language line, save footer** - `83843877` (feat)
3. **Task 3a: hang wrapped channel hints beside the badge (post-review CSS fix)** - `f93e7b10` (feat)
4. **Task 3b: env-gated screen capture** - `935bd4c7` (test)
5. **Task 3c: UI-SPEC amendment** - `968a9d45` (docs)

## Verification

| Command | Result |
|---------|--------|
| `npm test -- tests/hosting tests/locales tests/styles` | 100 files, 1772 tests passed |
| `npm run locales:check` | 3640 EN and 3640 DE keys, 0 failures |
| `npm run typecheck`, `npm run lint` | exit 0 |
| `prettier --end-of-line auto --check` on touched source and test files | clean |
| `npm run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` | 13 passed, 8 capture tests skipped (env-gated); includes axe (pristine, dirty, invalid, read-only, EN and DE at 390) and no horizontal scroll at 320 and 390 in EN and DE |
| `UTF_CAPTURE_SCREENS=1 ... -g "cost alert screens"` | 8 passed, 8 PNGs written |
| `git status --porcelain -- sources/WebApp sources/Api` | empty |

Acceptance criteria all passed: `workspace-panel` in the panel 5 (at least 2), `utf-alerts-page-max-width` in tokens.css 1, `export function channelStatus` 1, `Server_Alerts_Channel_Status_` 5 in en.json, 5 in de.json, 5 in server-keys.test.ts, `StatusBadge` in the panel 3, `D-16` in the UI-SPEC 3, `UTF_CAPTURE_SCREENS` in the spec 3.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Wrapped channel hints flowed under the badge**
- **Found during:** Task 3 screenshot review
- **Issue:** with the badge inline in the hint paragraph, the second line of a hint and the account settings link started at the card edge instead of beside the text.
- **Fix:** two column grid on the channel hint (see above).
- **Files modified:** `sources/ReactApp/src/features/hosting/cost-alert-settings.css`
- **Commit:** `f93e7b10`

**2. [Rule 3 - Blocking] ESLint `no-undef` for `process` in the e2e spec**
- **Found during:** Task 3 lint run (the eslint config gives test files browser globals only)
- **Fix:** `import process from "node:process"` in the spec instead of touching the shared ESLint config, which is outside this plan's file list.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts`
- **Commit:** `935bd4c7`

**3. [Rule 1 - Bug] Test assertions the redesign legitimately changed**
- **Found during:** Task 2
- **Issue:** the zero-rows case asserted the counter was absent, and the In the app description case expected a leading "Always on." Both contradict the amended contract (counter always rendered, badge says "Always on").
- **Fix:** updated both assertions (`0 of 10 amounts set`; description `Always on Shown in the account menu ...`). No behavior assertion was weakened.
- **Commit:** `83843877`

**Total deviations:** 3 auto-fixed (2 bug, 1 blocking). **Impact:** none on scope; all inside the plan's files.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. The badges restate availability values the settings DTO already returns (T-66-45, accepted); no new endpoint, field or API call.

## Issues Encountered

None open.

## Next Phase Readiness

Plans 66-16 to 66-18 are untouched. The UI-SPEC now has a `## Gap Closure Amendments (2026-11-21)` section; later gap plans should add their subsections there rather than start a new section.

## Self-Check: PASSED

- Modified files verified present; the five commits `4d2d8f9a`, `83843877`, `f93e7b10`, `935bd4c7`, `968a9d45` are ancestors of HEAD.
- All task acceptance criteria and the plan-level verification re-run after the last commit (table above).

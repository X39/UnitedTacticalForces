---
phase: 66-accrued-cost-cost-alerts
plan: 12
subsystem: testing
tags: [playwright, axe, e2e, parity-matrix, accessibility, cost-alerts, accrued-cost]
status: complete
uat: pending-end-of-phase

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-04 UAT costing scenario and Mailpit; 66-07 accrued row; 66-09 banner and menu rows; 66-11 settings page, channels and strip row
provides:
  - "fleetStream.ts frame builders accruedCostFrame, costAlertRaisedFrame and costAlertDismissedFrame"
  - "hosting-cost-alerts.spec.ts: 13 real-browser tests for the accrued row, live alert, cost-stop link, dismissal, settings save, 320/390 px layout and axe in en and de"
  - "Parity matrix rows CST-accrued, CST-alert-banner, CST-alert-menu and CST-alert-settings plus Phase 66 sentences on four touched rows, guarded by a Phase 66 rule with a live negative sample"
  - "Phase gate results for the whole phase (API, React, migrations, OpenAPI snapshot)"
affects: [uat, phase-66-verification]

plan_head_before: c9f8c9a92c1db3f32c301c9b52f74914c89a243c
plan_head_after: 1e9590e669df903029be8d70fcb41cebda195d87

actuals:
  tokens: 12000
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Strict API mock set per test: unmocked requests are aborted, recorded and asserted empty, which also proves no cost action request was sent"
    - "Matrix guard rule as a pure function over parsed rows, exercised on the real matrix and on inline negative samples"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
  modified:
    - sources/ReactApp/tests/e2e/support/fleetStream.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - .planning/phases/66-accrued-cost-cost-alerts/deferred-items.md

key-decisions:
  - "The cost-stop button of the banner sits in the StatePanel action slot outside the .cost-alert-banner list, so the spec locates the whole state-panel section for it; the workspace header has an identical button, so a page-wide lookup would be ambiguous"
  - "The Phase 66 matrix rule is a pure function (phase66Offenders) so the live negative sample runs the same code the real-matrix assertion runs"
  - "The profile menu axe scan covers the whole #account-profile-panel and passes, so no narrowing to the notifications section was needed"

requirements-completed: [CST-03, CST-04, CST-05, CST-06]
---

# Phase 66 Plan 12: End-to-end proof, parity rows and phase gate Summary

**A real browser now proves accrued cost, a live pushed alert in the banner and profile menu, the shared cost-stop confirmation from the alert, dismissal and the settings save, with axe and 320/390 px checks in English and German; the parity matrix records the four new capabilities behind a guard; every automated gate of the phase passes except one known, unrelated API test.**

## Performance

- **Started:** 2026-11-21T17:15:00Z (approximate)
- **Completed:** 2026-11-21T17:52:00Z
- **Tasks:** 3 (task 3 is the gate and the UAT hand-off; it changed no file and has no commit)
- **Commits:** 2 (measured from the persisted ledger)

## Accomplishments

- Tracer (task 1): `hosting-cost-alerts.spec.ts` runs 13 tests in Chromium, none skipped. It asserts the REST accrued amount and its `<time datetime>`, a pushed frame that updates the amount with the accrued REST read count staying at 1, and an older frame that never moves the amount backwards. A pushed alert shows the banner title and row, exactly one toast (a redelivered frame adds neither a row nor a toast), the profile menu row, and opens the shared confirmation dialog with "This session so far" and the accrued amount while no action request is sent. Dismissal removes the banner and menu row after the API accepted it and focuses `#workspace-title`. The settings page shows "No alerts set", blocks "abc" after blur, blocks Save with the invalid-count alert, then saves and the PUT body is exactly `{revision: 3, thresholds: [12.5], discordEnabled: false, emailEnabled: true, language: "en"}`.
- Layout: at 320x568 and 390x844 in en and de, the workspace with a three-row banner, the open profile menu with three alert rows, and the settings page (also with an added row) have no horizontal page scroll.
- Accessibility: axe at WCAG 2.1 A and AA, with at least one passed rule asserted per scan, reports zero violations on the settings page (pristine, dirty, invalid), the read-only settings page, the workspace with a banner and the open profile menu with alert rows, in en and de at 390x844.
- Parity (task 2): `CST-accrued`, `CST-alert-banner`, `CST-alert-menu`, `CST-alert-settings` added as `kept`, phase 66, with "Phase 66:" evidence and `tests/` paths. `FLT-profile-menu-notifications`, `FACT-estimated-cost`, `FACT-cost-owner` and `FACT-possible-cost-warning` keep their phases and carry a Phase 66 sentence. `grep -c "^| CST-"` is 4 and `grep -c "Phase 66:"` is 15. The guard has a real-matrix rule and an inline negative sample that reports a missing row, a wrong status, missing evidence and an unmarked touched row.

## Task Commits

1. **Task 1: Tracer, accrued cost and cost alerts end to end** - `44941581` (test)
2. **Task 2: Parity matrix rows and guard** - `1e9590e6` (docs)
3. **Task 3: Phase gate and UAT hand-off** - no commit (no test or matrix defect found)

Tracer gate (auto mode): the spec was re-run on the committed tree and passed again (13 passed). Tracer verified end-to-end, expanded.

## Phase gate results (task 3, verify block)

| Command | Result |
|---------|--------|
| `dotnet test X39.UnitedTacticalForces.slnx` | Parser 93 passed; CloudWorker 226 passed; Ui 2 passed; Api 1148 passed, 1 failed. The one failure is the known `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` (pre-existing, plan 66-06 deferred item). |
| `dotnet ef migrations has-pending-model-changes` | Pass: "No changes have been made to the model since the last migration." |
| `dotnet build sources/Api/Api.csproj` | Pass, 0 errors |
| `dotnet run ... --export-openapi` then `cmp` against `sources/ReactApp/openapi/utf-api.json` | Pass: byte-identical |
| `npm run locales:check` | Pass: 3635 English and 3635 German keys, 0 failures |
| `npm run contracts:check` | Pass: regeneration left the working tree clean |
| `npm run format:check` | Fails on 31 files, the known pre-existing drift (see Deferred Issues); the files changed by this plan are formatted |
| `npm run lint`, `npm run typecheck` | Pass |
| `npm test` (Vitest) | Pass: 161 files, 2088 tests |
| `npm run build:assets` | Pass |
| `npm run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` | Pass: 13 passed, 0 skipped |

Because `format:check` is part of `npm run ci` and the known drift makes it fail, `npm run ci` as one command does not exit 0; every other step of it was run separately and passes.

## Human UAT (end-of-phase, not yet answered)

`workflow.human_verify_mode` is `end-of-phase` and an executor cannot sign in to the running UAT profile, so none of the seven steps has an answer yet. They are recorded as **pending**, not passed. Nothing here was self-approved.

Start (PowerShell, repository root): `./scripts/Start-Uat.ps1 -Fresh` (rebuilds and reseeds; Ctrl+C stops everything). Restart without reseeding: `./scripts/Start-Uat.ps1`. React UI: http://localhost:5173/next/command/servers. Mailpit: http://localhost:8025.

| Step | What the human does | Answer |
|------|---------------------|--------|
| 1 | Open "UAT 9 Costing (accrued cost)": the strip shows "This session so far" of about 1.20 EUR with an "As of" time, and after about a minute amount and time advance without a reload | pending |
| 2 | Click "Set an alert" (or open /next/command/servers/cost-alerts), add the amount 1, choose "Save alert settings": within about 30 seconds a warning toast, the "Cost alert" banner naming the server and 1.00 EUR, and the alert at the top of the account menu | pending |
| 3 | In the banner choose "Shut down and stop billing": the usual cost-stop confirmation opens and shows "This session so far"; close it without confirming | pending |
| 4 | Open Mailpit: exactly one cost alert email for that server and amount, in the language used when saving | pending |
| 5 | Restart with `./scripts/Start-Uat.ps1`: no second email and no second alert for the same amount | pending |
| 6 | Dismiss the alert in the banner: it leaves the banner and the account menu and stays gone after a reload | pending |
| 7 | Optional, skippable (needs a Discord bot token in the git-ignored `sources/Api/appsettings.secret.json` and a linked Discord account): step 2 also delivers one Discord DM | pending, may be skipped without blocking |

## Deviations from Plan

None - plan executed as written. The plan's Task 3 text says to present the human check; because the run is non-interactive and the mode is end-of-phase, the check is handed to the orchestrator above instead of being answered here.

## Deferred Issues

- `format:check` drift in 31 files (the known ~30 plus `CostAlertSettingsPanel.tsx`, which differs on every line and looks like a CRLF working-copy artifact). Logged in `deferred-items.md`; production code is out of scope for this plan.
- `ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` fails as before (plan 66-06 deferred item).

## Known Stubs

None. The spec and builders contain no placeholder data that flows to a UI; mock data is confined to tests.

## Threat Flags

None. This plan adds tests and a documentation table only. T-66-38 is mitigated: every test aborts and records unmocked requests and asserts the list is empty. T-66-39: the UAT steps need no secret except the optional Discord token, which is referenced by file name only.

## Self-Check: PASSED

- `sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts` exists; `sources/ReactApp/tests/e2e/support/fleetStream.ts` contains `costAlertRaisedFrame`.
- Commits `44941581` and `1e9590e6` are ancestors of HEAD.
- `grep -c "^| CST-"` on the matrix is 4; `grep -c "Phase 66:"` is 15; `parity-matrix.test.ts` contains "Phase 66".

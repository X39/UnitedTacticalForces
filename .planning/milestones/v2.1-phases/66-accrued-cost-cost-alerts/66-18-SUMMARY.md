---
phase: 66-accrued-cost-cost-alerts
plan: 18
subsystem: testing
tags: [cost-alerts, e2e, playwright, parity-matrix, validation, gap-closure, d-15, d-16, d-17]
status: complete
gap_closure: true

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plans 66-13 to 66-17 (started-hour billing, dismiss-all by ids, settings cards, billing line, dismissal toasts and banner redesign)
provides:
  - "Real-browser journey proving Dismiss all sends exactly the shown ids, a failed refetch keeps the banner, a redelivered frame brings nothing back and every dismissal toasts"
  - "Parity matrix rows CST-accrued, CST-alert-banner, CST-alert-menu and CST-alert-settings carry a Phase 66 gap closure sentence with test paths"
  - "Validation map rows 66-13-01 to 66-18-03 (all green) and a Manual-Only gap closure row"
  - "Full API and React gate results after the gap closure"
affects: [phase-66-verification, uat]

plan_head_before: ab2665d2f60e252ed25efb91b2a3ec82686bff0b
plan_head_after: ad9c3883984ccc95fbf36e9cac38fcc274b25ab3

actuals:
  tokens: 4000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "The e2e mock records dismiss-all bodies and can switch the alert list to 500, so a failed refetch is provable without a real backend"
    - "A frame sent after a negative-case frame (an accrued frame) proves the earlier frame was already processed before the absence is asserted"

key-files:
  created: []
  modified:
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - .planning/phases/66-accrued-cost-cost-alerts/66-VALIDATION.md

key-decisions:
  - "The failing-refetch step runs on the Activity tab (`?tab=activity`), because the LiveBadge with the Reconnect now control is rendered only there; the banner is workspace-level and shows on every tab"
  - "No change to support/fleetStream.ts was needed; the existing send and closeFromServer helpers cover the journey"

requirements-completed: [CST-03, CST-04, CST-05, CST-06]

coverage:
  - deliverable: "Browser journey for dismiss-all ids, failed refetch, no resurrection and dismissal toasts"
    human_judgment: false
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts#gap closure: alerts leave only through the viewer's dismissal (en)"
        status: pass
  - deliverable: "Parity matrix and validation map gap closure records"
    human_judgment: false
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts"
        status: pass
  - deliverable: "Full API and React gates after the gap closure"
    human_judgment: false
    verification:
      - kind: command
        ref: "dotnet test, migration sync, OpenAPI cmp, locales, contracts, lint, typecheck, vitest, build:assets, Prettier, e2e spec"
        status: pass
  - deliverable: "Started-hour value and step, alert at the started hour, dismissal toast, alerts across an API restart, and the look of banner, menu rows and settings page"
    human_judgment: true
    rationale: "Live billing steps, socket pushes across a real restart and visual quality are judged by the user who reported the gaps in the running UAT profile; answers are not yet collected"

duration: 25min
completed: 2026-11-21
---

# Phase 66 Plan 18: Gap Closure Proof and Close-out Summary

**A real-browser journey now proves the three UAT gaps closed (Dismiss all touches only shown alerts, a failed refetch keeps the banner, a redelivered frame never resurrects a dismissed alert, every dismissal toasts), the parity matrix and validation map record the gap closure, and every automated gate passes; the human UAT answers are still to be collected.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 3 of 3 (tracer, matrix and validation, gate run); the human-check of Task 3 is presented below and not yet answered
- **Commits:** 3 task commits, then the SUMMARY commit

## Accomplishments

- Tracer (D-17): `mockApi` gained a `POST /api/cost-alerts/dismissals` handler (records the body in `observed.dismissedAll`, removes the listed ids, answers 204), an `alertsFail` switch that makes `GET /api/cost-alerts` answer 500 and a `failedAlertReads()` counter. The new test "gap closure: alerts leave only through the viewer's dismissal (en)" opens the workspace with two REST alerts, pushes a third by frame, chooses Dismiss all and asserts the body is `{ gameServerId, costAlertIds }` with exactly the three shown ids, the plural toast "3 cost alerts for ... dismissed." after the banner is gone and no single-dismissal request. It then redelivers the frame for a dismissed alert (followed by an accrued frame as an ordering fence) and asserts no banner and an unchanged toast count. Finally one more alert arrives, the stream is closed from the server, the alert list is switched to 500, Reconnect now is used, the failed read is observed and the banner still shows the alert; dismissing it shows the single-alert toast. `observed.unexpected` is empty throughout.
- Matrix: one "Phase 66 gap closure:" sentence with test paths was appended to the Evidence cell of CST-accrued, CST-alert-banner, CST-alert-menu and CST-alert-settings (status, phase and existing text unchanged); `tests/hosting/parity-matrix.test.ts` passes (90 tests).
- Validation map: 17 rows 66-13-01 to 66-18-03 with plan, wave, requirement, threat ids T-66-40 to T-66-51, secure behavior, test type and command, set to green after the gates passed, plus a Gap closure (2026-11-21) row under Manual-Only Verifications pointing at the Task 3 UAT.

## Task Commits

1. Task 1 (tracer): `4eb04755` test(webapp): prove cost alerts leave only through a dismissal
2. Task 2: `fd3c1e22` docs(planning): record the Phase 66 gap closure evidence
3. Task 3: `ad9c3883` docs(planning): mark the Phase 66 gap closure gates green

## Gate Results (Task 3)

| Gate | Outcome |
|------|---------|
| `dotnet test X39.UnitedTacticalForces.slnx` excluding `ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` | pass (Api.Tests 1192 passed, Ui.Tests 2 passed, 0 failed) |
| `dotnet ef migrations has-pending-model-changes` | pass ("No changes have been made to the model since the last migration.") |
| `dotnet build sources/Api/Api.csproj` | pass, 0 errors |
| OpenAPI export and `cmp` against `sources/ReactApp/openapi/utf-api.json` | pass, byte-identical |
| `locales:check` | pass (3647 EN and 3647 DE keys, 0 failures) |
| `contracts:check` | pass |
| `lint` (`--max-warnings 0`) | pass |
| `typecheck` | pass |
| `npm test` | pass (162 files, 2153 tests) |
| `build:assets` | pass |
| Scoped Prettier over 36 changed `ts`, `tsx` and `css` files since 555144c0 | pass |
| `test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` | 14 passed, 24 skipped (the env-gated screenshot captures that run only with `UTF_CAPTURE_SCREENS=1`, skipped by design) |

## Human UAT (Task 3 human-check) - answers pending

The executor cannot answer the UAT; the steps are presented here exactly as written for the user. Start (PowerShell, repository root): `./scripts/Start-Uat.ps1 -Fresh` (rebuilds and reseeds; Ctrl+C stops everything; `./scripts/Start-Uat.ps1` restarts without reseeding). React UI: http://localhost:5173/next/command/servers. Mailpit: http://localhost:8025.

| Step | Expected | Answer |
|------|----------|--------|
| 1. Started-hour billing (gap 1): open "UAT 9 Costing (accrued cost)"; strip shows "This session so far" 1.20 EUR and "Billed per started hour. The next hour starts at" about five minutes after the reseed; stays open until then, within about 30 s after it the amount changes to 1.80 EUR without a reload and the next-hour time moves one hour later | as described | pending |
| 2. Alert at the started hour: "Set an alert", amount 1.50, "Save alert settings"; the alert arrives when the third hour starts (or within about 30 s if step 1's change already happened): warning toast, "Cost alert" banner and top row in the account menu | as described | pending |
| 3. Dismissal feedback (gap 3): "Dismiss alert" in the banner; toast "Cost alert dismissed: UAT 9 Costing (accrued cost), 1.50 EUR." appears, banner and menu row gone and stay gone after a reload | as described | pending |
| 4. Nothing removes an alert by itself (gap 3): on the Cost alerts page add 1.60 and 1.70 and save; two alerts arrive; with the server page open restart the app (Ctrl+C, then `./scripts/Start-Uat.ps1`); the banner keeps both while the app is down and after it is back; "Dismiss all alerts" toasts that 2 cost alerts were dismissed and both leave banner and menu | as described | pending |
| 5. Design (gap 2): banner with one and with two alerts, account menu alert rows and the Cost alerts page at full width and at 390 px, in English and German | "looks good" or the element that still looks wrong | pending |
| 6. Optional, skippable: Mailpit shows one email per alert in the language used when saving | may be skipped without blocking | pending |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Reconnect control only exists on the Activity tab**
- **Found during:** Task 1 (first two runs: "Reconnect now" not found, also after a 10 s timeout)
- **Issue:** The plan says the `LiveBadge` reconnect control is in the workspace, but `LiveBadge` renders only inside `ActivitySection`, not on the default tab.
- **Fix:** `openWorkspace` takes an optional query string and the journey opens `?tab=activity`; the banner is workspace-level so it still shows. The disconnect step waits up to 10 s because the stale state appears only after 3 s of continuous disconnection.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts`
- **Commit:** `4eb04755`

### Plan-faithful adjustments (not defects)

- `sources/ReactApp/tests/e2e/support/fleetStream.ts` needed no change (plan: "if any").
- The Manual-Only note was written as a table row so it renders with the existing table.
- Validation rows list the essential part of each plan's automated command rather than the full chained shell line.

**Total deviations:** 1 auto-fixed (1 blocking). **Impact:** test-only, no production change.

## Authentication Gates

None.

## Known Stubs

None. No production code changed in this plan.

## Threat Flags

None. T-66-50 is mitigated: the journey asserts `observed.unexpected` is empty after every step and aborts unmocked requests. T-66-51: no secret or address appears in any step.

## Issues Encountered

- The step 6 optional email check and steps 1 to 5 of the human UAT are not answered yet; see the UAT table above.
- Pre-existing, unchanged and excluded by filter: `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` (`deferred-items.md`). `git status` shows `.planning/state.json` modified before this plan started; it was left out of every commit.

## Self-Check: PASSED

PASSED: the modified files exist, the commits `4eb04755`, `fd3c1e22` and `ad9c3883` are ancestors of HEAD, `grep -c costAlertIds` on the spec is 4 (at least 1), `grep -c closeFromServer` is 1 (at least 1), `grep -c "Phase 66 gap closure"` on the matrix is 4 (at least 4) and `grep -c 66-13-01` on the validation map is 1.

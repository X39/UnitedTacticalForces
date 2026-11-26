---
phase: 67-server-metrics
plan: 20
subsystem: testing
tags: [playwright, axe, e2e, parity-matrix, decision-log, validation, metrics]
status: complete

requires:
  - phase: 67-19
    provides: Metric charts with pointer and keyboard reading, responsive and forced-colors CSS, performance and source-scan guards
  - phase: 67-13
    provides: The "UAT 10 Metrics" seed and the emulated worker that feeds live readings
  - phase: 67-14
    provides: Live metrics frames written into the metrics query cache
provides:
  - Browser journey for MET-01 to MET-05 with layout checks (320 and 390 px, English and German) and axe scans of the metrics panel
  - Shared e2e helpers (metricsMocks.ts, costMocks.ts, metricsFrame in fleetStream.ts) that keep the unexpected-request assertion meaningful
  - Parity rows MET-tiles and MET-history with the phase67Offenders guard and a live negative sample (FND-07)
  - Decision log entries for the worker channel and server metrics, including the Counter-Strike 2 restart limitation
  - Phase 67 validation map with real test classes, Wave 0 done and rows green
affects: [phase-67-verification, verify-work]

plan_head_before: d303fc3af2a49cc5c7a036bab366d5d3c10e4bbe
plan_head_after: 03168c9027267e283224e34fe9dca3f55431622c
commits: 6

actuals:
  tokens: 10800
  tasks: 3
  commits: 6

tech-stack:
  added: []
  patterns:
    - "One helper per concern (fulfillMetricsRoute, fulfillCostRoute) returns whether it answered, and each spec calls it before its unexpected-request fallback"
    - "Chart fixtures are built relative to the request time because the chart's right edge is the browser clock"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-metrics.spec.ts
    - sources/ReactApp/tests/e2e/support/metricsMocks.ts
    - sources/ReactApp/tests/e2e/support/costMocks.ts
  modified:
    - sources/ReactApp/tests/e2e/support/fleetStream.ts
    - sources/ReactApp/tests/e2e/hosting-workspace.spec.ts
    - sources/ReactApp/tests/e2e/hosting-a11y.spec.ts
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - sources/ReactApp/tests/e2e/hosting-create.spec.ts
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - docs/features/cloud-based-game-server-hosting/decision-log.md
    - .planning/phases/67-server-metrics/67-VALIDATION.md

key-decisions:
  - "The quiet cost reads (accrued cost without a session, empty alert list) get their own helper costMocks.ts instead of widening the metrics helper"
  - "Live UAT steps are recorded as human-verification items because no container runtime is available to start ./scripts/Start-Uat.ps1 here"

requirements-completed: [MET-01, MET-02, MET-03, MET-04, MET-05]

coverage:
  - id: D1
    description: "Browser journey: four tiles from REST, a pushed frame changes a tile with no request for the current readings, range switch loads 24 hours and 7 days, the CPU summary names the 95 percent spike, tables show a gap row, charts return, Unavailable with reasons and no digits"
    requirement: "MET-01"
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-metrics.spec.ts#tiles follow REST and a pushed frame, ranges and tables show the history (MET-01 to MET-05)"
        status: pass
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-metrics.spec.ts#every tile shows Unavailable with its reason and no number, and the charts say there is nothing"
        status: pass
    human_judgment: false
  - id: D2
    description: "Metrics panel passes axe at WCAG 2.1 AA in chart view, table view, all unavailable and with the paused notice, and has no horizontal page scroll at 320 and 390 px in English and German"
    requirement: "MET-04"
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-metrics.spec.ts#server metrics accessibility and server metrics layout (12 cases)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every existing hosting e2e spec that opens the workspace answers the metrics requests, so its unexpected-request assertion stays meaningful"
    verification:
      - kind: e2e
        ref: "npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-metrics.spec.ts tests/e2e/hosting-workspace.spec.ts tests/e2e/hosting-a11y.spec.ts tests/e2e/hosting-cost-alerts.spec.ts tests/e2e/hosting-change.spec.ts tests/e2e/hosting-files-secrets.spec.ts tests/e2e/hosting-create.spec.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Parity rows MET-tiles and MET-history are kept in phase 67 with test evidence, TAB-overview gains a Phase 67 sentence, guarded by phase67Offenders with a live negative sample (FND-07)"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#keeps the Phase 67 metrics rows kept with Phase 67 test evidence and marks the touched row (FND-07)"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#reports a Phase 67 row that is missing, not kept or without evidence (live negative sample)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Decision log records the worker channel, kept HTTP endpoints, wake semantics, metric tiers, availability rules, player sources and the Counter-Strike 2 restart limitation; the validation strategy lists the real test classes and is green"
    verification: []
    human_judgment: true
    rationale: "Prose records; a reader judges whether they describe what Phase 67 shipped. The grep acceptance criteria pass."
  - id: D6
    description: "Live behavior in the running UAT app: live tiles, charts growing, gaps, tables, reload, restart recovery, unavailable states, stop, design in English and German, and real game player counts"
    verification: []
    human_judgment: true
    rationale: "Needs the running app (no container runtime is available to the executor) and real game servers; see the Human Verification section"

duration: 19min
completed: 2026-11-22
---

# Phase 67 Plan 20: End-to-end proof and close-out Summary

**Playwright journey, layout and axe checks for the server metrics panel, strict metrics and cost mocks for every workspace spec, guarded parity rows, decision log entries and a green validation map; all automated gates pass except seven known pre-existing date-dependent Vitest failures.**

## Performance

- **Duration:** 19 min (2026-11-22T14:40Z to 14:59Z)
- **Tasks:** 3 (Task 3 automated gates complete; its live UAT is recorded as human-verification items)
- **Commits:** 6 production and planning commits plus this summary commit
- **Files:** 14 changed, 926 insertions, 18 deletions

## Accomplishments

- `hosting-metrics.spec.ts` (12 cases): the English journey proves the four tiles from REST, a pushed `metricsFrame` changing the CPU and Players tiles while the count of `GET /metrics` stays 1, the 24 hour range with the "Highest: 95%" CPU summary and the gap note, the table view with one "No readings from ... to ..." row, the return to charts and the 7 day range, and the all-unavailable state (every tile reads the unavailable word with the worker sentence and no digit). Layout cases at 320x568 and 390x844 in English and German assert no horizontal page scroll with all chips and the toggle visible. Axe cases at 390x844 in both languages cover chart view, table view, all-unavailable and the paused notice after `closeFromServer`, each asserting zero violations, at least one passed rule and an empty unexpected list.
- `support/metricsMocks.ts`: reading and history fixtures (the history is built relative to the request time with a 10 minute gap 40 to 30 minutes ago and a 95 percent CPU spike five hours ago), `fulfillMetricsRoute`, and request counters. `metricsFrame` was added to `fleetStream.ts`.
- Six existing specs (workspace, a11y, change, files-secrets, cost-alerts, create) answer the two metrics requests before their unexpected-request fallback.
- `MET-tiles` and `MET-history` rows added to the parity matrix, `TAB-overview` gained a Phase 67 sentence, and `phase67Offenders` guards them with a live negative sample.
- Two decision log bullets: the worker channel, and server metrics including the Counter-Strike 2 RCON password limitation ("until the next Start").
- `67-VALIDATION.md`: real test classes and commands per requirement, Wave 0 done, `nyquist_compliant` and `wave_0_complete` true, rows green.

## Task Commits

1. Task 1: `3ea392a4` test(webapp): cover the server metrics journey end to end; `89ffc2a0` test(webapp): mock metrics requests in workspace specs
2. Task 2: `c0711d8f` test(webapp): guard the Phase 67 parity rows; `419ca923` docs(hosting): record the worker channel and server metrics decisions; `a9393d77` docs(planning): update the Phase 67 validation map
3. Task 3: `03168c90` docs(planning): mark the Phase 67 gates green

## Gate Outcomes (Task 3 verify block)

| Gate | Result |
|------|--------|
| `dotnet test X39.UnitedTacticalForces.slnx` (filtered) | Pass: Api.Tests 1408, CloudWorker.Tests 364, Ui.Tests 2, 0 failures. The excluded `ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` also passes now (1 of 1), so the filter is no longer needed |
| `dotnet ef migrations has-pending-model-changes` | Pass: "No changes have been made to the model since the last migration." |
| `dotnet build sources/Api/Api.csproj` | Pass: 0 errors |
| OpenAPI export `cmp` against `sources/ReactApp/openapi/utf-api.json` | Pass: identical |
| `locales:check` | Pass: 3695 English and 3695 German keys, 0 failures |
| `contracts:check` | Pass |
| `lint`, `typecheck`, `build:assets` | Pass |
| `npm test` (Vitest, full) | 2295 passed, 8 failed. 7 are the known date-dependent failures (accrued-cost 4, cost-alert-banner 1, cost-confirm-dialog 2; `deferred-items.md`). The 8th, `metrics-performance` "renders 2 016 connected points at 320px within 200 ms", failed only under full parallel load and passed 3 of 3 alone |
| Prettier `--check` on the files this phase changed | Pass |
| `test:e2e` hosting-metrics, hosting-workspace, hosting-a11y | Pass: 36 of 36 |
| `test:e2e` the seven specs of Task 1 | Pass: 117 passed, 24 skipped (screenshot captures that need `UTF_CAPTURE_SCREENS=1`), none skipped in the metrics spec |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing e2e specs already failed on two unmocked Phase 66 reads**
- **Found during:** Task 1, baseline run
- **Issue:** Besides the metrics reads, the workspace, a11y, change, files-secrets and create specs reported `GET /api/cost-alerts` and `GET /api/game-servers/{id}/accrued-cost` as unexpected requests (120 failures at baseline), so the plan's Task 1 verify could not pass by adding metrics mocks alone.
- **Fix:** Added `tests/e2e/support/costMocks.ts` with `fulfillCostRoute` (accrued cost without a session, empty alert list) and called it in the five specs next to the metrics helper. The cost-alerts spec keeps its own cost mocks.
- **Files modified:** `sources/ReactApp/tests/e2e/support/costMocks.ts` (new, not in the plan's file list), the five specs
- **Commit:** `3ea392a4`, `89ffc2a0`

**Total deviations:** 1 auto-fixed (1 blocking). **Impact:** test support only; no production code changed.

## Deferred Issues

Logged in `deferred-items.md`: `hosting-fleet.spec.ts` (20 cases) and `hosting-placement.spec.ts` (1 case) fail on the same unmocked `GET /api/cost-alerts` read (not in this plan's spec list); the load sensitivity of the 200 ms metrics performance backstop; the 7 date-dependent Vitest failures.

## Human Verification (Task 3 UAT, not run by the executor)

No container runtime (Podman or Docker) is available to the executor, so `./scripts/Start-Uat.ps1` could not be started. These steps are open for the user. Start (PowerShell, repository root): `./scripts/Start-Uat.ps1 -Fresh` (rebuilds and reseeds; Ctrl+C stops; `./scripts/Start-Uat.ps1` restarts without reseeding). React UI: http://localhost:5173/next/command/servers.

| Step | What to look at | Answer |
|------|-----------------|--------|
| 1 | Open "UAT 10 Metrics": Running since (about 90 minutes before the reseed), Players, CPU, Memory have values; CPU or Players change within about 15 s without reload | open |
| 2 | 1 hour charts grow at the right edge; "24 hours" shows a break of about 10 minutes around 35 minutes before the reseed, "Highest: 95%" for CPU about five hours before, and the gap note; "7 days" shows a break of about 3 hours two days ago; no line drops to zero in a gap | open |
| 3 | "Show data as tables": three tables newest first with a "No readings from ... to ..." row per gap; "Show charts" returns | open |
| 4 | Reload on the 1 hour range: same history | open |
| 5 | Restart recovery: Ctrl+C, then `./scripts/Start-Uat.ps1`; history is still there and tiles show values within about 30 s | open |
| 6 | "UAT 2 Running" (no worker): every tile "Unavailable" with the worker sentence. "UAT 1 Off (no deployment)": the not-running sentence and "No readings in this time range." | open |
| 7 | On "UAT 10 Metrics" choose "Shut down and stop billing"; afterwards the tiles show Unavailable with the not-running sentence and no number | open |
| 8 | Design at full width and at 390 px in English and German; answer "looks good" or name the element | open |
| 9 | Optional: real Arma 3, Counter-Strike 2 or ASKA server shows its human player count | skippable |

Items carried from earlier plans that only a real server or browser can settle (none block the phase):

- Real `ClientWebSocket` upgrade through nginx and Kestrel with the worker header (plans 67-08, 67-14; research A1): run a real worker against a deployed API and confirm metrics reach the tiles.
- Live Counter-Strike 2 `status` text format and A2S_INFO on the CS2 port (plan 67-09): until confirmed, an unrecognized format reads "The reading failed. It will be tried again." and never a guessed count.
- Live Arma 3 addon records excluding the headless client (plan 67-10).
- ASKA A2S_INFO on port 27016 (plan 67-05): until confirmed, ASKA may read "The reading failed. It will be tried again."
- Real-browser chart layout, tooltip placement, 320 px and forced colors (plans 67-18, 67-19): the e2e checks cover scroll and axe, not visual taste (see the React UI dislikes memory: no `<details>`, check select and dialog alignment).

## Threat Flags

None. This plan adds tests, planning records and a decision log entry only.

## Known Stubs

None.

## Issues Encountered

The baseline e2e run showed that Phase 66 never ran the other hosting specs after adding the cost reads (see deviation 1).

## Next Phase Readiness

Phase 67 is ready for verification. The only open work is the user's live UAT above.

## Self-Check: PASSED

- Created files exist: `hosting-metrics.spec.ts`, `metricsMocks.ts`, `costMocks.ts` found.
- Commits `3ea392a4`, `89ffc2a0`, `c0711d8f`, `419ca923`, `a9393d77`, `03168c90` are ancestors of HEAD.
- Acceptance criteria: AxeBuilder 2, fulfillMetricsRoute in hosting-workspace 2, metricsFrame in fleetStream 1, MET-tiles 1, MET-history 1, phase67Offenders 7, "until the next Start" 1, "Phase 67" in the decision log 2.

---
phase: 67-server-metrics
plan: 15
subsystem: ui
tags: [react, hosting, metrics, tiles, i18n, tanstack-query]

requires:
  - phase: 67-12
    provides: regenerated OpenAPI snapshot and src/generated/api.ts with GameServerMetricsDto, GameServerMetricsHistoryDto and the two metrics routes
provides:
  - Server metrics panel in the Overview (data-slot="metrics") with four tiles in the order Running since, Players, CPU, Memory
  - metricsModel (reason map with fixed fallback, tile presentation, GiB and percent formatting, isOlderReading)
  - useServerMetrics (no polling, performance.now() stamp, newer cached reading kept)
  - hostingService.serverMetrics and serverMetricsHistory plus hostingQueryKeys.metrics and metricsHistory
  - Every Phase 67 locale key (48) in English and German, identical-allowed entries and the SVC-serverMetrics parity rows
affects: [67-16, 67-17, 67-18, 67-19, 67-20]

plan_head_before: 892870838024482170cb5d5a75d31a6d1d96ddcc
plan_head_after: c8aeb3425fa95a0bb1be52e16a515e6d8cc8dc55
commits: 3

actuals:
  tokens: 13750
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Pure tile model: a reason code from the control plane wins over a value, an unknown or missing code maps to a fixed fallback key, an unavailable tile never carries a number"
    - "Readings query follows useAccruedLive: staleTime Infinity, no refetchInterval, newer cached reading kept on a reordered response"
    - "Panel clock for the age check is a lazily captured performance.now() (react-hooks/purity forbids calling it in render)"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/metricsModel.ts
    - sources/ReactApp/src/features/hosting/useServerMetrics.ts
    - sources/ReactApp/src/components/server/MetricTile.tsx
    - sources/ReactApp/src/components/server/ServerMetricsPanel.tsx
    - sources/ReactApp/tests/hosting/metrics-panel.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/hosting/workspace-shell.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx

key-decisions:
  - "Range keys are Server_Metrics_Range_Hour1, _Hours24, _Days7 and Server_Metrics_RangeText_Hour1, _Hours24, _Days7 instead of the UI-SPEC names with 1h, 24h, 7d, because tests/locales/server-keys.test.ts requires every Server_ key segment to start with an uppercase letter"
  - "metricTiles formats memory through i18n.t (Server_Metrics_Memory_Value) inside the model so the 'of {{total}}' detail can carry the unit; the locale argument still drives every Intl formatter"
  - "Task 1 already contains the complete panel state machine (loading, first-load error with retry, held tiles on a failed refetch); Task 2 therefore added only the behavior tests"

patterns-established:
  - "Metric tile detail lines are described as {key, options} data (plural key without suffix) and rendered by MetricTile with t()"

requirements-completed: [MET-01, MET-02, MET-03, MET-05]

coverage:
  - id: D1
    description: "The Overview shows a Server metrics panel after the details panel with four tiles in the order Running since, Players, CPU, Memory, rendering the control plane values (players including 0, CPU percent, memory in GiB of total) in English and German"
    requirement: "MET-01"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#shows the metrics panel with four tiles in order after the details panel"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows players including zero, CPU as a percent and memory in GiB of the total"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#formats numbers with German separators and sentences in German"
        status: pass
    human_judgment: false
  - id: D2
    description: "An unavailable reading shows the word Unavailable, the info icon and the mapped reason sentence and never a number; an unknown or missing code shows the generic sentence; partial readings render side by side"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows Unavailable and the plain reason for the code %s and no number"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows values and reasons side by side when only some readings are available"
        status: pass
    human_judgment: false
  - id: D3
    description: "One loading panel on first load, the panel error with Try again on a failed first load, and unchanged tiles when a background refetch fails; the readings are never polled"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows the panel error with Try again when the first load fails and refetches on retry"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#never polls: the readings hook has no refetch interval and the metrics files no raw timer"
        status: pass
    human_judgment: false
  - id: D4
    description: "All 48 Phase 67 strings exist in English and German and the parity matrix lists SVC-serverMetrics and SVC-serverMetricsHistory"
    requirement: "MET-03"
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check; tests/hosting/parity-matrix.test.ts; tests/locales"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual quality of the unstyled tiles (styling arrives in plan 67-16; the panel currently renders in the narrow right column of the Overview row)"
    verification: []
    human_judgment: true
    rationale: "No CSS exists for the tiles yet; layout and appearance are verified after plan 67-16"

duration: 37min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 15: Overview Metric Tiles Summary

**The Overview now shows four localized metric tiles (Running since, Players, CPU, Memory) straight from the metrics REST read, with Unavailable plus a plain reason instead of invented numbers, no polling, and all 48 Phase 67 strings in English and German.**

## Performance

- **Duration:** 37 min
- **Completed:** 2026-11-22T13:07:02Z
- **Tasks:** 2
- **Files modified:** 15 (5 created, 10 modified)

## Accomplishments

- Types, query keys and the `serverMetrics` and `serverMetricsHistory` service methods on the 67-12 generated contract.
- `metricsModel.ts`: reason map with fixed fallback `Server_Metrics_Reason_Unknown`, per-tile first-match rule (server reason, unknown code, value), `Intl` percent and GiB formatting, `isOlderReading`.
- `useServerMetrics`: no `refetchInterval`, `staleTime: Infinity`, `performance.now()` stamp, newer cached reading kept.
- `MetricTile` and `ServerMetricsPanel` (loading panel, first-load error with Try again, held tiles on background failure, always-mounted status region) replace the reserved placeholder in `OverviewSection`.
- All Phase 67 locale keys, three identical-allowed entries and two parity rows.

## Task Commits

1. **Task 1: Tracer - the Overview shows four metric tiles** - `201ffda0` (feat), `34ca91aa` (test)
2. **Task 2: Every tile state** - `c8aeb342` (test; the implementation was already complete from Task 1, so there is no separate feat commit)

**Plan metadata:** see the docs commit that follows this summary.

## Files Created/Modified

See `key-files` above. `metrics-panel.test.tsx` holds 19 cases (including the `it.each` over nine reason codes).

## Decisions Made

See `key-decisions`. Plans 67-16 to 67-19 must use `Server_Metrics_Range_Hour1|Hours24|Days7` and `Server_Metrics_RangeText_Hour1|Hours24|Days7`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Range locale keys violated the key vocabulary test**
- **Found during:** Task 1 (running tests/locales)
- **Issue:** `Server_Metrics_Range_1h`, `_24h`, `_7d` and the matching `RangeText` keys fail `tests/locales/server-keys.test.ts` (PascalCase segments).
- **Fix:** Renamed to `Hour1`, `Hours24`, `Days7` variants in en.json and de.json; values unchanged.
- **Files modified:** sources/ReactApp/src/locales/en.json, de.json
- **Commit:** 201ffda0

**2. [Rule 3 - Blocking] Existing workspace tests failed once the Overview requested metrics**
- **Found during:** Task 1 (full tests/hosting run)
- **Issue:** `workspace-shell.test.tsx` found two `role="alert"` elements because the unmocked metrics request failed; `workspace-page.test.tsx` made an unmocked request.
- **Fix:** Added a `serverMetrics` mock (all readings unavailable) to both render helpers; not in the plan's file list.
- **Files modified:** sources/ReactApp/tests/hosting/workspace-shell.test.tsx, workspace-page.test.tsx
- **Commit:** 34ca91aa

**3. [Rule 3 - Blocking] react-hooks/purity rejected performance.now() in render**
- **Found during:** Task 1 (lint)
- **Fix:** The panel captures the clock once with `useState(() => performance.now())`. The value is unused until plan 67-16 adds the age check.
- **Commit:** 201ffda0

**Total deviations:** 3 auto-fixed (1 bug, 2 blocking). **Impact:** none on scope; plan 67-18 must use the renamed range keys.

## Deferred Issues

Pre-existing and out of scope (logged in `deferred-items.md`): `accrued-cost.test.tsx` (4), `cost-alert-banner.test.tsx` (1) and `cost-confirm-dialog.test.tsx` (2) fail because their fixtures use the fixed date 2026-11-21, so `formatInstant` now adds a short date. Some `workspace-page` and other hosting tests time out at 5 s only under full-suite load and pass alone.

## Known Stubs

None. The panel is not styled yet (plan 67-16) and still sits in the narrow right cell of `.workspace-overview__row` until that plan drops the `2fr 1fr` rule.

## Threat Flags

None. T-67-27 mitigated: reason codes map to fixed keys with a generic fallback, the error panel uses fixed copy, and a test asserts the raw code and `Error.message` are never printed.

## Verification

- `npm test -- tests/hosting tests/locales tests/styles`: all pass except the 7 pre-existing date failures above (isolated runs of the load-flaky files pass).
- `locales:check`: 3695 keys, 0 failures. `typecheck` and `lint` pass. Prettier clean on all changed files.
- No file under sources/WebApp, sources/Api or sources/CloudWorker touched.

## Self-Check: PASSED

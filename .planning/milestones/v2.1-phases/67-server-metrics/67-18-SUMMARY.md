---
phase: 67-server-metrics
plan: 18
subsystem: ui
tags: [react, hosting, metrics, svg-chart, history, tanstack-query, css-tokens]
status: complete

requires:
  - phase: 67-15
    provides: metricsModel, useServerMetrics, ServerMetricsPanel with the status region, Server_Metrics_* locale keys
  - phase: 67-16
    provides: token-only metrics.css and the stale re-aging panel
  - phase: 67-17
    provides: once-per-bucket history invalidation by live frames
provides:
  - Three hand-built SVG charts (CPU load, Memory used, Players) with an average line, a min-max band, gap breaking and isolated-point markers
  - Range switcher (1 hour, 24 hours, 7 days) with previous-range placeholder, aria-busy and one announcement
  - Table view with caption, sticky header, newest-first rows and one spanning row per gap
  - useServerMetricsHistory and useMeasuredWidth hooks
  - Seven chart and metrics layout tokens with a contrast guard for the chart line
affects: [67-19, 67-20]

plan_head_before: 219a230d5086919c30663a495ca6bfad02bfbe8b
plan_head_after: b5f332d249635352eb72dd56b93ba543f6559bdf
commits: 4

actuals:
  tokens: 25000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "buildSegments is the only gap decision: a null or a distance above 2 x bucket ends a run; gapRows applies the same rule for the table"
    - "SVG stroke widths and the marker radius are SVG attributes, colours are classes over --utf-* tokens, so the server CSS style guard (no px literal) stays green"
    - "History query keeps the previous range as placeholder data only for the same server (queryKey index 2 check)"

key-files:
  created:
    - sources/ReactApp/src/components/server/MetricLineChart.tsx
    - sources/ReactApp/src/components/server/MetricTable.tsx
    - sources/ReactApp/src/features/hosting/useMeasuredWidth.ts
    - sources/ReactApp/tests/hosting/metric-line-chart.test.tsx
    - sources/ReactApp/tests/hosting/metric-table.test.tsx
    - sources/ReactApp/tests/helpers/metricsHistoryFixtures.ts
  modified:
    - sources/ReactApp/src/features/hosting/metricsModel.ts
    - sources/ReactApp/src/features/hosting/useServerMetrics.ts
    - sources/ReactApp/src/components/server/ServerMetricsPanel.tsx
    - sources/ReactApp/src/components/server/metrics.css
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/styles/tokens-contrast.test.ts
    - sources/ReactApp/tests/hosting/metrics-model.test.ts
    - sources/ReactApp/tests/hosting/metrics-panel.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/hosting/workspace-shell.test.tsx

key-decisions:
  - "The chart clock (right plot edge) ticks every 15 s regardless of range instead of one bucket of the shown range: a 5 minute clock left after a 7 day view would leave a stale edge and a false gap note when switching back to 1 hour"
  - "The plot SVG uses a fixed 160 px viewBox height equal to --utf-metric-chart-height at the default font size and the measured width; the fallback width without ResizeObserver is 480 px"
  - "Players y domain is the nice ceiling of max(capacity, highest max) rounded up to an even number (minimum 2) so the middle tick is a whole number"
  - "While the first history load runs the views region holds one loading StatePanel (a polite status, as the spec allows for loading panels); the panel's own announcement status region stays the only one once data is there"

requirements-completed: [MET-04, MET-05]

coverage:
  - id: D1
    description: "Below the tiles three SVG charts (CPU load, Memory used, Players) show the returned history with average line, min-max band, highest-value summary and accessible names"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#names the plot group and the image and describes the image by the highest value"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows the three history charts below the tiles in the order CPU load, Memory used, Players"
        status: pass
    human_judgment: false
  - id: D2
    description: "Lines break at nulls and gaps wider than twice the bucket, an isolated point is a marker, nothing is interpolated and no zero is drawn; a gap note explains open gaps"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-model.test.ts#breaks at a null value and never turns it into zero or a bridged point"
        status: pass
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#breaks the line at a missing reading, never draws zero and explains the gap"
        status: pass
    human_judgment: false
  - id: D3
    description: "The range switcher loads the matching history, keeps the previous charts busy at reduced opacity, shows the history error with Try again for the same range and announces the change once"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#loads the chosen range, keeps the old charts busy meanwhile and announces it once"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#shows the history error with Try again for the same range while tiles and toolbar remain"
        status: pass
    human_judgment: false
  - id: D4
    description: "The view toggle swaps charts for real tables (caption, headers, newest first, one row per gap) in focusable scroll regions and keeps focus on the toggle"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metric-table.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/hosting/metrics-panel.test.tsx#swaps charts for tables and back with focus staying on the toggle"
        status: pass
    human_judgment: false
  - id: D5
    description: "Charts use only --utf-* tokens, the chart line passes 3:1 on every surface, there is no chart library and no dependency change"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#keeps the chart line at 3:1 on every surface"
        status: pass
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual quality of the charts (spacing of tick labels, band legibility, table scroll region) in a real browser at desktop and 320 px"
    verification: []
    human_judgment: true
    rationale: "jsdom has no layout; pointer and keyboard reading, responsive and forced-colors styling and the performance guard follow in plan 67-19"
---

# Phase 67 Plan 18: History Charts, Range Switcher and Table View Summary

**The Overview metrics panel now draws CPU load, memory and players history as hand-built accessible SVG charts that break honestly at gaps, with a 1 hour, 24 hours and 7 days switcher and a full-fidelity table view, using only design tokens and no chart library.**

## Performance

- **Duration:** about 30 min
- **Completed:** 2026-11-22
- **Tasks:** 2
- **Files:** 17 (6 created, 11 modified), all under `sources/ReactApp`

## Accomplishments

- `metricsModel.ts`: `buildSegments` (only gap decision), `niceCeiling`, `yDomain`, `xTicks`, `yTicks`, `highestPoint`, `hasOpenGap`, `gapRows`, `metricSeries`, range maps, and `Intl` formatting helpers.
- `MetricLineChart`: focusable plot group, `role="img"` SVG named "{metric}, {range}" and described by the highest-value summary, band behind a 2 px line, step interpolation for players, markers for isolated points, clamped fixed domains, empty plot at full height (with the game-does-not-report variant) and the gap note.
- `MetricTable`: caption, `th scope="col"`, newest first, one spanning row per gap including the stretch to now, sticky header in a labelled focusable scroll region.
- `ServerMetricsPanel`: range `FilterChipGroup`, ghost view toggle with `aria-controls`, a single announcement through the existing status region, `aria-busy` plus the refetch opacity token while the previous range shows, history error with Try again.
- Tokens `--utf-metric-chart-min/-height/-tooltip-width/-refetch-opacity`, `--utf-chart-line/-band/-grid` and a contrast guard for the line on all three surfaces.

## Task Commits

1. Task 1 (tracer): `58195425` feat(webapp): draw server metric history as accessible SVG charts; `e92f5b16` test(webapp): cover chart segments, domains and rendering
2. Task 2: `ddf043b4` feat(webapp): add the metrics range switcher and table view; `b5f332d2` test(webapp): cover metric ranges and tables

The tracer gate was automated-only: `<verify>` (tests, typecheck, lint) passed end to end before expansion. No separate RED commit: tests and implementation were committed as feat then test so every commit builds.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests made an unmocked history request once the panel loaded history**
- **Found during:** Task 1 (full panel and workspace test runs)
- **Issue:** `workspace-shell`, `workspace-page`, `workspace-section-panels` and `metrics-panel` tests failed with duplicate `role="alert"` or an extra status region because `serverMetricsHistory` was unmocked.
- **Fix:** Added a `serverMetricsHistory` mock (empty history) to those render helpers and seeded the history cache in the "one status region" test; scoped one assertion to the players tile because the empty players chart now repeats the game-does-not-report sentence.
- **Files modified:** the four test files above (not in the plan's file list, except `metrics-panel.test.tsx`)
- **Commit:** `e92f5b16`

**2. [Rule 2 - Missing critical] Stale right edge after switching ranges**
- **Found during:** Task 2 design
- **Issue:** The plan has the chart clock tick per bucket of the shown range; after a 7 day view (5 minute tick) switching to 1 hour could leave `now` minutes old and flag a false gap.
- **Fix:** One constant 15 s chart clock (`useNow`, still no timer of its own).
- **Files modified:** `ServerMetricsPanel.tsx`
- **Commit:** `ddf043b4`

**3. [Scope note] New test helper**
- `tests/helpers/metricsHistoryFixtures.ts` was added (shared history fixtures); it is not in the plan's file list.

**Total deviations:** 3 (1 blocking, 1 missing-critical, 1 scope note). **Impact:** none on scope or behavior.

## Deferred Issues

Pre-existing and out of scope (`deferred-items.md`): 7 date-dependent failures in `accrued-cost.test.tsx` (4), `cost-alert-banner.test.tsx` (1) and `cost-confirm-dialog.test.tsx` (2). The crosshair, tooltip, keyboard reading, forced-colors and 320 px styling, the 2,016 point performance guard and the dependency and no-polling source scan belong to plan 67-19.

## Known Stubs

None.

## Threat Flags

None. T-67-38 mitigated: fixed y domains clamp every point into the plot, `buildSegments` is the only gap decision, nothing is interpolated or animated, and the table shows the exact values.

## Verification

- `vitest run tests/hosting tests/styles tests/locales`: 1896 passed, 7 failed (exactly the known pre-existing date failures).
- `npm run typecheck`: exit 0. `npm run lint` (`eslint . --max-warnings 0`): exit 0. Prettier clean on every file this plan changed.
- Acceptance: `utf-chart-line` in tokens.css 1, `utf-metric-refetch-opacity` 1, `MetricLineChart.tsx` and `MetricTable.tsx` present, `FilterChipGroup` in the panel 2, `metric-table.test.tsx` has 8 cases.
- No commit touched a file outside `sources/ReactApp`; `package.json` is unchanged.

## Self-Check: PASSED

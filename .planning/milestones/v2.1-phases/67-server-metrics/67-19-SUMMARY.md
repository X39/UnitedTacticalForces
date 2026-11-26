---
phase: 67-server-metrics
plan: 19
subsystem: ui
tags: [react, hosting, metrics, svg-chart, accessibility, keyboard, forced-colors, performance, source-scan]
status: complete

requires:
  - phase: 67-18
    provides: MetricLineChart with buildSegments, the focusable plot group, useMeasuredWidth, MetricTable, the chart tokens and the metrics history fixtures
provides:
  - Pointer and keyboard chart reading (crosshair, aria-hidden tooltip, Left, Right, Home, End, Escape) that never selects a point inside a gap
  - Touch tooltip that stays until a tap outside the plot
  - 800 px and 520 px toolbar rules, and forced-colors chart rules
  - Held-out performance backstop for the 2 016 point series at 320 px
  - Source scan guarding no polling, no timer, no chart library and an unchanged dependency list
affects: [67-20]

plan_head_before: c6a519aa5bc3dd0e18addf8ffc233dd4688e7d7e
plan_head_after: c01a7c9f4d0697552c9953270c07efff065c35ef
commits: 4

actuals:
  tokens: 30000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Selection is stored as a point time, not an index, so a live history refresh keeps or drops it correctly"
    - "The tooltip flip and clamp is computed from an estimate at render and refined on the element in a layout effect, which avoids setState in an effect"
    - "Key handling lives on the plot group and ignores events whose target is not the group itself, so no key is intercepted elsewhere"

key-files:
  created:
    - sources/ReactApp/tests/hosting/metrics-performance.test.tsx
    - sources/ReactApp/tests/hosting/metrics-source-scan.test.ts
  modified:
    - sources/ReactApp/src/components/server/MetricLineChart.tsx
    - sources/ReactApp/src/components/server/metrics.css
    - sources/ReactApp/tests/hosting/metric-line-chart.test.tsx

key-decisions:
  - "Left from no selection starts at the newest point, Right at the oldest, because a user usually wants the latest reading first"
  - "Blur and Escape hide the tooltip; mouse leave hides it; touch keeps it until a pointerdown outside the plot (document listener, no timer)"
  - "The source scan forbids setTimeout and requestIdleCallback as well as setInterval and refetchInterval in the metrics files; the shared useNow clock stays the only time source"

requirements-completed: [MET-04, MET-05]

coverage:
  - id: D1
    description: "A keyboard user focuses a plot and reads values point by point (Left, Right, Home, End, Escape); a bucket without a reading is never selected; keys are not intercepted outside the focused plot"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#moves with Right and Left, jumps with Home and End and hides with Escape"
        status: pass
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#never selects a bucket without a reading"
        status: pass
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#intercepts no key while the plot itself is not the target"
        status: pass
    human_judgment: false
  - id: D2
    description: "The pointer shows a crosshair and an aria-hidden tooltip at the nearest point in x with time, average and lowest/highest; the tooltip flips near the right edge and stays inside the plot; touch keeps it until a tap outside"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#places the crosshair and an aria-hidden tooltip at the point nearest in x"
        status: pass
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#flips the tooltip before the crosshair near the right edge and keeps it inside the plot"
        status: pass
      - kind: unit
        ref: "tests/hosting/metric-line-chart.test.tsx#keeps the tooltip after a touch ends until a tap outside the plot"
        status: pass
    human_judgment: false
  - id: D3
    description: "A 2 016 point series at 320 px renders within 200 ms with at most one line and one band path per segment"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-performance.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "The metrics files hold no refetch interval, raw timer or chart-library import, and package.json dependencies equal the recorded list"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-source-scan.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Toolbar controls are 44 px at 800 px or less, the toolbar stacks at 520 px or less, and forced colors draw line and markers in CanvasText, gridlines in GrayText and no band"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/hosting/metrics-source-scan.test.ts#metrics stylesheet contract"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual quality in a real browser: tooltip placement and legibility at desktop and 320 px, 44 px targets, forced-colors rendering"
    verification: []
    human_judgment: true
    rationale: "jsdom has no layout or forced-colors emulation; the CSS contract is asserted as text only. Full Playwright mobile baselines and the axe scan arrive in plan 67-20 and Phase 69"
---

# Phase 67 Plan 19: Chart Reading, Responsive Styles and Guards Summary

**Each metric chart can now be read by pointer and keyboard through a snapping crosshair and an aria-hidden tooltip that never selects a point inside a gap, with 800 px, 520 px and forced-color styles, a 200 ms held-out performance test at 2 016 points, and a source scan proving no polling, no timer, no chart library and no new dependency.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-22
- **Tasks:** 2
- **Files:** 5 (2 created, 3 modified), all under `sources/ReactApp`

## Accomplishments

- `MetricLineChart`: selectable points are `segments.flat()` from `buildSegments`, so a missing reading is never selectable. Pointer move or down selects the point nearest in x; mouse leave hides; touch persists until a pointerdown outside the plot. Left, Right, Home, End and Escape are handled only when the plot group itself is the event target; handled keys call `preventDefault`, modified keys and others pass through.
- Crosshair (1 px `--utf-border-control`) and a cursor dot are `aria-hidden`; the tooltip is `aria-hidden`, `pointer-events: none`, on `--utf-surface-overlay`, limited to `--utf-metric-tooltip-width`, with time, "Average ..." and, only when min differs from max, "Lowest ..., highest ...". It flips before the crosshair near the right edge and is clamped inside the plot.
- `metrics.css`: tooltip, crosshair and cursor styles (tokens only), 44 px toolbar controls at 800 px or less, stacked toolbar with a full-width toggle at 520 px or less, and a forced-colors block using CanvasText, GrayText, Canvas and Highlight with no band. Focus rings on the plot and table regions were already in place from 67-18 and are asserted.
- `metrics-performance.test.tsx`: 2 016 points at a stubbed 320 px width render within 200 ms with one line and one band, and 21 segments give 21 lines; players (stepped) stays within budget.
- `metrics-source-scan.test.ts`: eight metrics files scanned for `refetchInterval`, `setInterval`, `setTimeout` and `requestIdleCallback` and for imports of 20+ charting packages (with live inline bad samples), `package.json` dependencies equal to the recorded list, no charting package in any dependency section, and a stylesheet contract for the breakpoints, forced colors and focus ring.

## Task Commits

1. Task 1 (tracer): `b5aaac71` feat(webapp): read metric values by pointer and keyboard; `0aa79637` test(webapp): cover chart reading by keyboard and pointer
2. Task 2: `28c70f33` feat(webapp): make metric charts responsive and forced-color safe; `c01a7c9f` test(webapp): guard metric chart performance and no polling

The tracer gate was automated-only: its `<verify>` (chart and panel tests, typecheck, lint) passed before expansion. No separate RED commit: implementation then tests, so every commit builds.

## Verification

- `npm --prefix sources/ReactApp run -s typecheck`: clean. `npm --prefix sources/ReactApp run -s lint`: clean (0 warnings). Prettier check on every changed file: clean. `build:assets`: passes (only the existing chunk-size notice).
- Targeted run (performance, source scan, chart, panel, styles): 6 files, 128 tests passed.
- Full `tests/hosting` plus `tests/styles`: 1 867 passed, 8 failed. Seven are the known date-dependent failures (accrued-cost, cost-alert-banner, cost-confirm-dialog; see `deferred-items.md`). The eighth, `create-server-page.test.tsx` "keeps Advanced options collapsed", passes alone (51 of 51) and is a load-related timing flake of the parallel run, unrelated to this plan.
- Acceptance greps: `ArrowRight` 1 hit, `aria-hidden="true"` 3 hits, `buildSegments` 3 hits in `MetricLineChart.tsx`; `forced-colors` 1 hit in `metrics.css`; both new test files exist.
- No file outside `sources/ReactApp` was touched; `package.json` is unchanged.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] setState inside a layout effect failed the React lint rule**
- **Found during:** Task 1 lint run
- **Issue:** Measuring the tooltip width into state tripped `react-hooks/set-state-in-effect`.
- **Fix:** Placement is computed by a pure `placeTooltip` at render from an estimated width and refined on the element itself in a layout effect.
- **Files modified:** `MetricLineChart.tsx`
- **Commit:** `b5aaac71`

**2. [Rule 2 - Missing critical] Blur and touch dismissal**
- **Found during:** Task 1 design
- **Issue:** The spec names Escape and pointer end only; a tooltip would stay after keyboard focus leaves, and touch needs an explicit outside-tap rule.
- **Fix:** Blur hides the tooltip; a document `pointerdown` listener (registered only while a touch selection exists) hides it on a tap outside the plot.
- **Files modified:** `MetricLineChart.tsx`
- **Commit:** `b5aaac71`

**3. [Scope note] Stylesheet contract tests**
- CSS assertions live in `metrics-source-scan.test.ts` (a `metrics stylesheet contract` block) because jsdom cannot evaluate media queries; this adds coverage beyond the plan's file list without adding a file.

**Total deviations:** 2 auto-fixed (1 bug, 1 missing critical) plus 1 scope note. **Impact:** none on scope or dependencies.

## Known Stubs

None.

## Threat Flags

None. T-67-30 (large series) is mitigated by one path per segment per layer and the performance test; T-67-SC (dependency) by the source scan.

## Issues Encountered

None beyond the pre-existing failures listed above.

## Next Phase Readiness

Ready for 67-20.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/hosting/metrics-performance.test.tsx, sources/ReactApp/tests/hosting/metrics-source-scan.test.ts
- FOUND commits: b5aaac71, 0aa79637, 28c70f33, c01a7c9f

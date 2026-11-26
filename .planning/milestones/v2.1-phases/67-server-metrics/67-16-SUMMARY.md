---
phase: 67-server-metrics
plan: 16
subsystem: ui
tags: [react, hosting, metrics, staleness, css-tokens, live-updates]
status: complete

requires:
  - phase: 67-15
    provides: ServerMetricsReading with receivedAtMs, metricTiles, MetricTile and ServerMetricsPanel with the status region
provides:
  - isReadingStale and browser re-aging of held readings against staleAfterSeconds (performance.now, useNow(5000))
  - lastReading presentation for worker-offline, server-sent stale and browser stale
  - Paused notice after 3 s of socket disconnect with the last values kept
  - metrics.css (token-only tiles, equal heights, wrapping values, two columns at 520px or less)
  - Full-width Overview row (two-column rule removed from sections.css)
affects: [67-17, 67-18, 67-19, 67-20]

plan_head_before: 113ba79cd978c03718c31db26e43ba1ff8e258b9
plan_head_after: a7a2e7a9d2949e59ff8335e6c49fd558f2a65dcc
commits: 4

actuals:
  tokens: 16000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "First-match tile rule in one decide() helper: server reason, missing value, browser stale, value"
    - "useNow(5000) only ticks; the age is measured with performance.now() in a memo keyed by the tick"

key-files:
  created:
    - sources/ReactApp/src/components/server/metrics.css
    - sources/ReactApp/tests/hosting/metrics-model.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/metricsModel.ts
    - sources/ReactApp/src/components/server/ServerMetricsPanel.tsx
    - sources/ReactApp/src/components/server/sections.css
    - sources/ReactApp/tests/hosting/metrics-panel.test.tsx

key-decisions:
  - "The panel measures age with performance.now() inside a useMemo keyed by the useNow tick (with an explicit exhaustive-deps suppression), because react-hooks/purity forbids calling it directly in render and the tick value itself is wall clock"
  - "MetricTile already rendered the last-reading line from plan 67-15, so it needed no change; the paused line carries the carried key Server_Live_StaleReason_Disconnected"
  - "The browser stale reason reuses the server stale key and also carries lastReading, so a locally aged tile reads exactly like a server-sent stale one"

requirements-completed: [MET-01, MET-02, MET-03, MET-05]

coverage:
  deliverables:
    - name: "Held readings age out in the browser (D-09)"
      human_judgment: false
      verification:
        - kind: test
          ref: "tests/hosting/metrics-panel.test.tsx#turns a held value unavailable with a last reading once it is older than staleAfterSeconds"
          status: pass
        - kind: test
          ref: "tests/hosting/metrics-model.test.ts#is stale only after more than staleAfterSeconds on the performance clock"
          status: pass
    - name: "Paused notice after 3 s keeps values and one status region"
      human_judgment: false
      verification:
        - kind: test
          ref: "tests/hosting/metrics-panel.test.tsx#shows the paused sentence after 3 s, keeps the values and keeps one status region"
          status: pass
    - name: "Token-only tile layout with full-width Overview row (D-07)"
      human_judgment: true
      rationale: "Equal heights and 320 px wrapping are visual; the style guard and a CSS content test pass, real rendering is confirmed by the Phase 69 mobile baselines"
      verification:
        - kind: test
          ref: "tests/styles/server-style-guard.test.ts"
          status: pass
---

# Phase 67 Plan 16: Stale Re-aging and Tile Layout Summary

**Metric tiles age a held reading out in the browser against the API's staleAfterSeconds (performance.now plus useNow, no polling), announce a dropped socket after 3 s without hiding values, and sit in a token-only full-width tile grid.**

## Accomplishments

- `isReadingStale` plus a first-match `decide()` rule in `metricsModel.ts`: a server reason wins, a missing value reads the generic sentence, a held value past `staleAfterSeconds` reads the stale sentence, otherwise the value. worker-offline and stale (server or browser) carry `lastReading` when `asOf` is valid.
- `ServerMetricsPanel` re-ages every 5 s with `useNow(5000)` and shows the paused line (`Warning16Regular` plus `Server_Live_StaleReason_Disconnected`) through `useDelayedDisconnect()`; the single `role="status"` region stays mounted.
- `metrics.css` with tokens only; the 800px two-column rule of `.workspace-overview__row` is gone so the metrics panel spans the row.
- Tests: new `metrics-model.test.ts` (8 tests) and 4 new panel tests (stale re-aging with stubbed `performance.now`, worker-offline last reading, paused notice, CSS layout contract).

## Task Commits

1. Task 1 (tracer): `c91d391e` feat(webapp): age metric tiles out when readings go stale; `b559fa5b` test(webapp): cover metric staleness and the paused notice
2. Task 2: `2e17b810` feat(webapp): lay out server metric tiles with tokens; `a7a2e7a9` test(webapp): cover the metrics panel layout

The tracer gate was automated-only: `<verify>` (tests, typecheck, lint) passed end to end before expansion. No separate RED commit (the plan's tests and implementation were committed as feat then test to keep every commit building).

## Deviations from Plan

None - plan executed exactly as written. `MetricTile.tsx` needed no edit because plan 67-15 already rendered the last-reading line and used the planned class names.

## Verification

- `vitest run tests/styles tests/hosting/metrics-*.test.ts* tests/hosting/workspace-section-panels.test.tsx`: 91 passed.
- Full `tests/hosting`: 1716 passed, 7 failed; the 7 are the known date-dependent failures in accrued-cost, cost-alert-banner and cost-confirm-dialog (deferred-items.md), not touched by this plan.
- `npm run typecheck`, `eslint` and Prettier on the changed files: clean.
- Acceptance greps: isReadingStale 2, useDelayedDisconnect 2, 8 model tests, `2fr 1fr` outside comments 0, metrics.css present, `overflow-wrap: anywhere` present.

## Known Stubs

None.

## Threat Flags

None. T-67-37 mitigated: held readings are re-aged against `staleAfterSeconds` and show Unavailable with the last-reading time, never the number.

## Self-Check: PASSED

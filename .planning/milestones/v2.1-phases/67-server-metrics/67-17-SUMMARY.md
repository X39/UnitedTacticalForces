---
phase: 67-server-metrics
plan: 17
subsystem: ui
tags: [react, hosting, metrics, websocket, tanstack-query, live-updates]
status: complete

requires:
  - phase: 67-15
    provides: ServerMetricsReading, hostingQueryKeys.metrics and metricsHistory, useServerMetrics
  - phase: 67-12
    provides: GameServerMetricsMessage wire contract on the fleet socket
provides:
  - parseMetricsFrame, isNewerMetrics and historyBucketMs in metricsFrames.ts
  - Metrics branch in FleetLiveUpdatesProvider writing frames into the readings cache without a request
  - History ranges invalidated at most once per bucket interval from frames, never by a timer
  - Readings key in invalidateWorkspaceQueries (fleet signal, reconnect) and in Refresh server
affects: [67-18, 67-19, 67-20]

plan_head_before: 40e38d84686143a1056dae764efa58a435248c6f
plan_head_after: 6658d70630e5921167e439b02ff4df3d03916313
commits: 5

actuals:
  tokens: 14000
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Frame branch before the alert branches: parse, setQueryData with a performance.now() stamp, return without touching the fleet or the listeners"
    - "History freshness by cache age: a range is invalidated only when query.state.dataUpdatedAt is at least one bucket old and it is not fetching"
    - "Equal as-of frames replace only when the facts differ, so a redelivery never refreshes the receive stamp"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/metricsFrames.ts
    - sources/ReactApp/tests/hosting/metrics-frames.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/metricsModel.ts
    - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
    - sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx
    - sources/ReactApp/tests/hosting/workspace-shell.test.tsx

key-decisions:
  - "Equal as-of times replace the cached reading only when its facts differ, so a redelivered frame cannot reset receivedAtMs and delay the browser age check (D-09)"
  - "History invalidation skips a range that is fetching or never loaded, so a slow refetch is never cancelled by the next frame"
  - "The history prefix is derived from hostingQueryKeys.metricsHistory(serverId, range).slice(0, -1) instead of a second key literal"

requirements-completed: [MET-01, MET-02, MET-03, MET-04, MET-05]

coverage:
  deliverables:
    - name: "A pushed metrics frame replaces the cached reading by server id with no request; newest asOf wins; malformed frames are dropped (MET-05)"
      human_judgment: false
      verification:
        - kind: test
          ref: "tests/hosting/metrics-frames.test.ts"
          status: pass
        - kind: test
          ref: "tests/hosting/fleet-live-updates.test.tsx#writes a metrics frame into the readings cache by server id with a fresh receive stamp and no request"
          status: pass
        - kind: test
          ref: "tests/hosting/fleet-live-updates.test.tsx#drops a malformed metrics frame and never writes it into the cache"
          status: pass
    - name: "History ranges refetch at most once per bucket interval from frames, never by a timer (MET-04)"
      human_judgment: false
      verification:
        - kind: test
          ref: "tests/hosting/fleet-live-updates.test.tsx#refetches an active range once per bucket and never by a timer"
          status: pass
        - kind: test
          ref: "tests/hosting/fleet-live-updates.test.tsx#invalidates a range only once its data is as old as its bucket interval"
          status: pass
    - name: "Readings refetch on a fleet signal for the server, after a socket reopen and on Refresh server; metrics frames never trigger a fleet invalidation"
      human_judgment: false
      verification:
        - kind: test
          ref: "tests/hosting/workspace-live-updates.test.tsx"
          status: pass
        - kind: test
          ref: "tests/hosting/workspace-shell.test.tsx#invalidates exactly the seven keys with exact filters and surfaces a failed refetch"
          status: pass
        - kind: test
          ref: "tests/hosting/fleet-live-updates.test.tsx#never triggers a fleet invalidation or a workspace listener for a metrics frame"
          status: pass
---

# Phase 67 Plan 17: Live Metric Frames Summary

**Metric readings follow pushed frames on the shared fleet socket with newest-asOf-wins and a strict parser, history ranges refetch at most once per bucket interval from those frames, and the readings recover through fleet signals, reconnects and Refresh server, with no polling.**

## Accomplishments

- `metricsFrames.ts`: strict `parseMetricsFrame` (string ids, finite numbers, parseable instants, integer `staleAfterSeconds` of at least 1, value and reason never both set), `isNewerMetrics` and `historyBucketMs` (15 000, 60 000, 300 000 ms).
- `FleetLiveUpdatesProvider`: a metrics branch before the alert branches writes the reading into `hostingQueryKeys.metrics(serverId)` with `performance.now()` and returns without a fleet invalidation or listener call; it then invalidates each cached history range of that server whose data is at least one bucket old (exact keys, skipping fetching and never loaded ranges).
- `invalidateWorkspaceQueries` and the `refresh` key list of `ServerWorkspacePage` include the readings key (exact), so fleet signals, reconnects and Refresh server refetch the current readings.
- Tests: new `metrics-frames.test.ts` (parser, newest-wins, bucket lengths), provider tests for cache writes, older and null as-of frames, redelivery, malformed frames, no fleet invalidation, and history refetch (age boundary, exact keys, other servers, an active observer refetching once per bucket, fetching skipped); workspace tests for the readings key.

## Task Commits

1. Task 1 (tracer): `ab380ab8` feat(webapp): update metric readings live from the fleet socket; `3868d907` test(webapp): cover metric frame parsing and cache updates
2. Task 2: `a3121adf` feat(webapp): refresh metric history once per bucket from frames; `6658d706` test(webapp): cover metric refetch on signals and reconnects
3. Deviation fix: `c9d22ebf` fix(webapp): satisfy exact optional types for the uptime tile

The tracer gate was automated-only: `<verify>` (tests, typecheck, lint) was re-run end to end before expansion. No separate RED commit: tests and implementation were committed as feat then test to keep every commit building.

## Deviations from Plan

**1. [Rule 3 - Blocking] Typecheck error left by plan 67-16 in metricsModel.ts**
- **Found during:** Task 1 verify (`npm run typecheck`)
- **Issue:** `uptimeValue` returned `dateTime: string | undefined`, which `exactOptionalPropertyTypes` rejects; the plan's verify and the "every commit builds" rule could not pass. The plan's prohibition on editing `metricsModel.ts` only existed because 67-16 was assumed to run in parallel; it had already finished.
- **Fix:** spread `{ dateTime: instant }` only when the instant is set.
- **Files modified:** `sources/ReactApp/src/features/hosting/metricsModel.ts`
- **Commit:** `c9d22ebf`

**2. [Rule 1 - Test follow-up] Refresh server test counted six keys**
- **Found during:** Task 2
- **Issue:** `workspace-shell.test.tsx` asserted exactly six invalidations; the readings key makes it seven. `workspace-live-updates.test.tsx` likewise asserted four per-server invalidations (now five).
- **Fix:** updated both expectations and the expected key list.
- **Files modified:** `sources/ReactApp/tests/hosting/workspace-shell.test.tsx`, `sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx`
- **Commit:** `6658d706`

**Total deviations:** 2 auto-fixed (1 blocking, 1 test follow-up). **Impact:** none on scope; both are required for a green typecheck and test suite.

## Verification

- `vitest run` of metrics-frames, fleet-live-updates, workspace-live-updates, workspace-page, workspace-shell, cost-alert-persistence, metrics-panel: 91 passed.
- Full `tests/hosting`: 1740 passed, 7 failed; the 7 are the known date-dependent failures in accrued-cost, cost-alert-banner and cost-confirm-dialog (deferred-items.md), not touched by this plan.
- `npm run typecheck`, `eslint` (max-warnings 0) and Prettier on the changed files: clean.
- Acceptance greps: parseMetricsFrame in the provider, GameServerMetricsMessage in metricsFrames.ts, `hostingQueryKeys.metrics` in useWorkspaceLiveUpdates.ts and ServerWorkspacePage.tsx, historyBucketMs in the provider all at least 1; parser test file has far more than 6 tests.
- Commits touch only files under `sources/ReactApp`.

## Known Stubs

None.

## Threat Flags

None. T-67-26 mitigated: malformed frames are dropped by the strict parser, newest asOf wins, a frame writes only its own server's key, and history refetch is bounded to once per bucket.

## Self-Check: PASSED

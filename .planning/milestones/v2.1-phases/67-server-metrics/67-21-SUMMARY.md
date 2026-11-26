---
phase: 67-server-metrics
plan: 21
subsystem: webapp
tags: [react, metrics, tanstack-query, cache-merge, gap-closure, cr-01, wr-12]
status: complete
gap_closure: true

requires:
  - phase: 67-17
    provides: Frame merge that ignores redelivered frames and the browser receive stamp
  - phase: 67-18
    provides: Metric tiles with the unavailable reasons and the stale re-aging
  - phase: 67-20
    provides: Browser journey for the metrics panel used as the regression run
provides:
  - One shared merge rule, replacesCachedReading in metricsFrames.ts, for REST readings and live frames
  - A stopped or restarted server shows the control plane's reason on its metric tiles, never the previous run's values
  - An unchanged refetch keeps its receive stamp, so the browser age check is no longer delayed (WR-12)
affects: [phase-67-verification, verify-work]

plan_head_before: 6603d8386f1162bc50f1bf365c9ad395e5c9233f
plan_head_after: 93e75c8b2cb1fa0ee2cb4b90379b4d66beb46641
commits: 5

actuals:
  tokens: 6050
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "One exported merge rule decides between two control-plane readings for both transports; it never computes a value"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/metricsFrames.ts
    - sources/ReactApp/src/features/hosting/useServerMetrics.ts
    - sources/ReactApp/src/features/hosting/metricsModel.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/tests/hosting/metrics-panel.test.tsx
    - sources/ReactApp/tests/hosting/metrics-frames.test.ts
    - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
    - .planning/phases/67-server-metrics/67-REVIEW-DISPOSITION.md

key-decisions:
  - "A reading without an as-of time replaces the cached one whenever the facts differ: the control plane sends it only when the active deployment has no snapshot, so it is the current truth"
  - "A reading older by as-of time is refused only when it still holds a value; an older reading of reason codes only may replace the cache because it can only make tiles unavailable"
  - "Equal facts keep the cached entry and its receive stamp for REST and frames alike"
  - "The rule lives in metricsFrames.ts because metricsModel.ts already imports from it and the reverse import would be a cycle"

requirements-completed: [MET-05, MET-01]
---

# Phase 67 Plan 21: Stopped-server metrics merge fix Summary

One shared cache-merge rule, `replacesCachedReading`, now lets a reading without an as-of time (stopped server, new run before its first sample) replace a held value, so tiles show the control plane's not-running or measuring reason instead of the previous run's numbers (closes CR-01 and WR-12).

## What changed

The old merge treated a reading without an as-of time as older than any cached reading. The control plane sends exactly that shape for a stopped server and for a new deployment before its first sample, so the tiles kept the last CPU, memory, player and running-since values. They only turned unavailable through the generic "too old" sentence, never the "The game is not running" sentence (verification truth 5a).

### Final rule (`replacesCachedReading(next, current)` in `metricsFrames.ts`, first match wins)

1. No cached reading: take `next`.
2. Both as-of times known, `next` strictly earlier, and `next` holds any value (running since, CPU, memory used, player count): refuse. This is a reordered older sample.
3. Otherwise take `next` exactly when `!sameReadings(next, current)`. `next` is passed first, so the browser-only `receivedAtMs` on the cached side is ignored.

Private helpers: `asOfMs` (null for null, undefined or unparseable as-of) and `holdsAnyValue`.

Used by `useServerMetrics` (the readings queryFn returns the cached reading unless `replacesCachedReading(loaded, cached)`) and by `FleetLiveUpdatesProvider` (the metrics `setQueryData` updater). `isOlderReading` (metricsModel.ts) and `isNewerMetrics` (metricsFrames.ts) are deleted, so exactly one merge rule remains.

## Tasks

| Task | Name | Commits |
|------|------|---------|
| 1 (tracer) | REST reading replaces the cached one after a stop or restart | `4fb5d677` test (RED), `431cce9f` fix (GREEN) |
| 2 | Live frames merge by the same rule, old rule removed, disposition updated | `fff84aa6` test (RED), `4cc52330` fix (GREEN), `93e75c8b` docs |

## RED results observed before each fix

- Task 1: 13 tests failed. The 4 new panel tests failed against the old merge: after a stop the tiles kept "37%", after a restart the running-since `time` of the previous run stayed (and the new run's running since never appeared), and the refetch re-stamped `receivedAtMs` to 41000 instead of keeping 1000. The 9 `replacesCachedReading` unit cases failed with `TypeError: replacesCachedReading is not a function`.
- Task 2: 1 test failed against the old frame rule: "lets a frame without an as-of time replace the reading and the first frame of the next run replace that" with `AssertionError: expected 50 to be null` (the not-running frame never replaced the cached reading).

## Verification results (actual)

- `npm --prefix sources/ReactApp test -- tests/hosting/metrics-panel.test.tsx tests/hosting/metrics-frames.test.ts tests/hosting/metrics-model.test.ts tests/hosting/fleet-live-updates.test.tsx tests/hosting/metrics-source-scan.test.ts tests/hosting/metric-table.test.tsx tests/hosting/metric-line-chart.test.tsx`: 7 files, 166 tests, all passed (the five-file Task 1 run: 133 tests passed).
- `npm run typecheck`: exit 0. `npm run lint` (`--max-warnings 0`): exit 0.
- Prettier `--check` on all seven touched source and test files: clean.
- `npm run test:e2e -- tests/e2e/hosting-metrics.spec.ts`: 12 passed (21 s).
- `git status --porcelain -- sources/Api sources/CloudWorker sources/HostingContracts sources/WebApp sources/Database`: no output (D-03: no API, worker, contract, database or Blazor change).
- All acceptance-criteria greps of both tasks returned their expected counts (1, 1, 1, 0, 1, 2, 1, 1 for Task 1; 1, 0, 0, 1, 1, 1, 2, 1 for Task 2).
- The unchanged panel test "keeps a cached reading that is newer than the one a refetch returns" passes; the existing "keeps the receive stamp when the same frame is delivered again" passes.
- The whole vitest suite was not run, per the plan (7 pre-existing date-dependent failures and 1 load-only timeout unrelated to metrics).

## Deviations from Plan

None - plan executed exactly as written.

Minor note: the `metrics-frames.test.ts` unit block uses the `reading()` helper with `asOf` overrides; the "does not parse as missing" case asserts both directions (an unparseable next over a valid cached reading, and a no-as-of reading over an unparseable cached one). Both take the replacement through rule 3 because the facts differ.

## Known Stubs

None.

## Threat Flags

None. Mitigations T-67-39 (rule only chooses between control-plane readings, refuses older readings that hold values) and T-67-40 (frames still pass `parseMetricsFrame` before the rule) are in place; T-67-41, T-67-42 and T-67-SC are accepted as planned.

## Review disposition

CR-01 and WR-12 are marked `fixed` by 67-21 in `67-REVIEW-DISPOSITION.md` (`open: 26`). All other findings stay open and out of scope for this gap run.

## Human re-check (UAT step 7)

1. Start from the repository root in PowerShell: `./scripts/Start-Uat.ps1 -Fresh`. Stop everything with Ctrl+C.
2. Open http://localhost:5173/next/command/servers, then open "UAT 10 Metrics" and wait until the tiles show values.
3. Without reloading, choose "Shut down and stop billing" and confirm.
4. Expected: once the stop completes, every tile reads "Unavailable" with "The game is not running, so there is nothing to measure.", with no number and no running-since time.

## Next phase readiness

Verification truth 5a can be re-verified. STATE.md and ROADMAP.md were left untouched for the orchestrator.

## Self-Check: PASSED

- Modified files exist: all eight listed `key-files.modified` entries found on disk.
- Commits `4fb5d677`, `431cce9f`, `fff84aa6`, `4cc52330`, `93e75c8b` are ancestors of HEAD.

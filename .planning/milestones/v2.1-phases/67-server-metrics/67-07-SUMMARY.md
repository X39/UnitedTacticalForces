---
phase: 67-server-metrics
plan: 07
subsystem: api
tags: [metrics, history, retention, hosted-service, ef-core]

requires:
  - phase: 67-04
    provides: ServerMetricBucket tiers, ServerMetricResolutions, ServerMetricsOptions retentions and IServerMetricsService
provides:
  - EServerMetricsRange with the range helpers For, Span, TryParse and Format on ServerMetricResolutions
  - IServerMetricsService.GetHistoryAsync returning GameServerMetricsHistoryDto with API-computed aggregates
  - IServerMetricsService.PruneAsync and the ServerMetricsPruneWorker hosted service
affects: [67-12, 67-16, server-metrics endpoints]

plan_head_before: 802428901808fed5d452cded8a6a42e592b0c11b
plan_head_after: ab302f48490890f090d87335984a9c8ede91a9a5

actuals:
  tokens: 14000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Gaps stay gaps: only stored buckets become points, null aggregate for a zero metric count"
    - "Stateless prune worker copied from the CostAlertWorker loop, registered only in the not-paused block"

key-files:
  created:
    - sources/Api/Services/ServerMetrics/EServerMetricsRange.cs
    - sources/Api/DTO/GameServerMetricsHistoryDto.cs
    - sources/Api/DTO/ServerMetricPointDto.cs
    - sources/Api/DTO/ServerMetricAggregateDto.cs
    - sources/Api/HostedServices/ServerMetricsPruneWorker.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsPruneTests.cs
  modified:
    - sources/Api/Services/ServerMetrics/ServerMetricResolutions.cs
    - sources/Api/Services/ServerMetrics/IServerMetricsService.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsService.cs
    - sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs
    - tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs

key-decisions:
  - "History filters the window and orders in memory after loading the server's single tier, because SQLite cannot compare or order instant columns; the tier of one server is bounded by its retention"
  - "Pruning deletes with ExecuteDeleteAsync per tier using a strict less-than cut, so a bucket starting exactly at the retention limit is kept"

patterns-established:
  - "Range code parsing is closed and case-sensitive (1h, 24h, 7d); the REST layer in 67-12 only has to call TryParse"

requirements-completed: [MET-04, MET-05]

coverage:
  - id: D1
    description: "The 1h, 24h and 7d ranges read the 15, 60 and 300 second tier with API-computed average, minimum and maximum, and never fill a gap"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#HourRange_ReturnsComputedAggregates_AndLeavesEmptyWindowsAbsent"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#CoarserRanges_ReadTheirOwnTier"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#UnavailableMetric_IsNullNotZero_AndMeasuredZeroStaysZero"
        status: pass
    human_judgment: false
  - id: D2
    description: "A reload reads the same series from the durable tier rows"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#Window_IsRespected_AndAReloadReadsTheSameSeries"
        status: pass
    human_judgment: false
  - id: D3
    description: "History is readable only by viewers; others and unknown servers get not found; only 1h, 24h and 7d parse"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#NonViewerAndUnknownServer_GetNotFound"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsHistoryTests.cs#TryParse_AcceptsExactlyTheThreeCodes"
        status: pass
    human_judgment: false
  - id: D4
    description: "Pruning removes each tier at its own retention, keeps snapshots, is idempotent and restart-safe, and the worker is registered only while the control plane is not paused"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsPruneTests.cs#Prune_CutsEachTierAtItsOwnRetention_AndKeepsTheSnapshot"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsPruneTests.cs#Prune_IsIdempotent_AndRestartSafe"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs#ExplicitSetting_ControlsBackgroundServiceRegistration"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 07: Metric History and Tier Pruning Summary

**Viewers read the 1 hour, 24 hour and 7 day series from the durable 15, 60 and 300 second tiers with API-computed aggregates and honest gaps, and a stateless hosted worker prunes every tier to its retention.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-11-22T10:25Z
- **Tasks:** 2
- **Files modified:** 12 (7 created, 5 modified)

## Accomplishments
- `EServerMetricsRange` plus `For`, `Span`, `TryParse` (exactly `1h`, `24h`, `7d`, case-sensitive) and `Format` on `ServerMetricResolutions`.
- `GetHistoryAsync` authorizes like `GetCurrentAsync`, reads only the tier of the range inside `now - Span .. now`, maps every stored bucket to a point whose per-metric aggregate is null for a zero count, rounds the average to two decimals, and returns the newest non-null `MemoryTotalBytes` and `PlayerCapacity`. Windows the worker never reported have no point.
- `PruneAsync` deletes per tier with `ExecuteDeleteAsync` (retention 2 h, 26 h, 7 d 1 h by default) and never touches snapshots.
- `ServerMetricsPruneWorker` copies the `CostAlertWorker` loop (validate once, scope per pass, sanitized failure log, `PruneInterval` delay) and is registered in the not-paused block of `AddHostingControlPlane`.

## Task Commits

1. **Task 1: Tracer - history reads per range and tier** (tracer verified end-to-end: 51 tests across history, ingest and read-model)
   - `c3dd4c49` feat(api): read server metric history per range and tier
   - `4a5bccdb` test(api): cover server metric history reads
2. **Task 2: Prune history to each tier's retention**
   - `62a30f58` feat(api): prune server metric history by tier retention
   - `ab302f48` test(api): cover metric pruning and registration

**Plan metadata:** committed separately (docs: complete plan).

## Decisions Made
- The history window filter and ordering run in memory (SQLite cannot compare instants); the per-server tier is bounded by retention, so the load is small.
- A bucket starting exactly at its retention limit is kept (strict less-than cut), covered by a boundary assertion.

## Deviations from Plan

None - plan executed exactly as written.

Note on TDD: as in plans 67-02 and 67-04, the new interface members and types did not exist before the tests, so a compile-failing RED commit was not possible without breaking the build. Implementation and tests were written together and committed in the plan's order (feat, then test); no separate RED commit exists.

## Issues Encountered
None. The WorkerProtocol XML-doc warnings visible in the build output belong to plan 67-06 and are out of scope.

## Known Stubs
None.

## Threat Flags
None. T-67-35 is mitigated (view claim plus existence check before any read, not found otherwise, closed range parser) and T-67-20 is mitigated (tier retention pruning by a stateless hosted worker). No file under `sources/Api/Services/WorkerProtocol`, `sources/Api/Program.cs`, `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker` was touched. No endpoint was added, so no OpenAPI snapshot or Kiota regeneration was needed; the REST endpoint is plan 67-12.

## Next Phase Readiness
Ready for plan 67-12 (REST endpoints over `GetCurrentAsync` and `GetHistoryAsync`) and the browser plan 67-16.

## Self-Check: PASSED

- All 7 created files exist on disk; commits `c3dd4c49`, `4a5bccdb`, `62a30f58` and `ab302f48` are ancestors of HEAD (`git rev-list --count` over the plan range is 4).
- Acceptance: `GetHistoryAsync` in the interface 1, `TryParse` in the resolutions 1, history test cases 8, `AddHostedService<ServerMetricsPruneWorker>` 1, `ServerMetricsPruneWorker` in the state test 1, prune test cases 3.
- `dotnet test --filter "FullyQualifiedName~ServerMetric|FullyQualifiedName~HostingControlPlane"`: 147 passed, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.

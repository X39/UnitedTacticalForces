---
phase: 67-server-metrics
plan: 02
subsystem: api
tags: [metrics, ef-core, postgres, rollup, worker-protocol, validation]

requires:
  - phase: 66
    provides: hosting entities (GameServer, GameServerDeployment, LifecycleWorker) and the worker protocol contracts
provides:
  - WorkerMetricsSample contract with value-or-reason validation and the closed reason vocabulary
  - ServerMetricSnapshot (latest accepted primary-host sample per server) and ServerMetricBucket (15/60/300 s rollups)
  - IServerMetricsService.IngestAsync with attribution, skew and stale guards, one transaction, one retry
affects: [67-04, 67-07, server-metrics read model, metrics migration]

plan_head_before: 85bdeaad4b874f954e456b3821e5e873e83a846e
plan_head_after: 00132271f858f3f71434ce4db54ea8a75b91d66a

actuals:
  tokens: 15700
  tasks: 2
  commits: 7

tech-stack:
  added: []
  patterns:
    - "Value-or-reason readings: nullable value plus closed-vocabulary reason, validated by IValidatableObject"
    - "Streaming rollup buckets with count, sum, min, max per metric keyed by server, resolution and window start"

key-files:
  created:
    - sources/HostingContracts/WorkerProtocol/WorkerMetricsSample.cs
    - sources/HostingContracts/WorkerProtocol/WorkerMetricUnavailableReasons.cs
    - sources/Database/Hosting/ServerMetricSnapshot.cs
    - sources/Database/Hosting/ServerMetricBucket.cs
    - sources/Api/Services/ServerMetrics/ServerMetricSampleSource.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsIngestResult.cs
    - sources/Api/Services/ServerMetrics/ServerMetricResolutions.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsOptions.cs
    - sources/Api/Services/ServerMetrics/IServerMetricsService.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsService.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsTestData.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs
  modified:
    - sources/Database/DatabaseContext.cs

key-decisions:
  - "Contract validation, deployment match and skew/stale checks run before the primary-host check, so a malformed or foreign sample from any host is surfaced instead of silently dropped"
  - "WorkerSampledAt is normalized to UTC before storage because Npgsql timestamptz rejects non-zero offsets"
  - "ServerMetricsTestData is internal, not public, because it exposes the internal ServerMetricsService"

patterns-established:
  - "Metrics ingest takes trusted attribution from a ServerMetricSampleSource built from the authenticated worker, never from the payload"

requirements-completed: [MET-02, MET-03, MET-04, MET-05]

coverage:
  - id: D1
    description: "A worker sample is a value or a closed-list reason per reading, never both, and zero is a real value"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#InvalidSample_IsRejected_AndWritesNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#UnavailablePlayers_StoreReason_AndMeasuredZeroStaysZero"
        status: pass
    human_judgment: false
  - id: D2
    description: "The latest primary-host sample is one durable snapshot row per server with every reading and reason"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#SecondSample_ReplacesTheSnapshotRow"
        status: pass
    human_judgment: false
  - id: D3
    description: "History is stored as 15, 60 and 300 second rollup buckets with spike-preserving min and max"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#Spike_SurvivesInTheFiveMinuteTier"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#SamplesInOneMinute_MergeCountSumMinimumAndMaximum"
        status: pass
    human_judgment: false
  - id: D4
    description: "Foreign, future, stale, non-primary and contradictory samples write nothing"
    requirement: "MET-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#ForeignFutureOrStaleSample_IsRejected_AndWritesNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs#NonPrimarySource_ReturnsNotStored_AndWritesNothing"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 02: Server Metrics Ingest Summary

**A primary worker's CPU, memory and player samples are validated as value-or-reason, attributed from the authenticated worker, and stored as a per-server snapshot plus spike-preserving 15/60/300 s PostgreSQL rollup buckets in one transaction.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-11-22T09:27Z
- **Tasks:** 2
- **Files modified:** 13 (12 created, 1 modified)

## Accomplishments
- `WorkerMetricsSample` and `WorkerMetricUnavailableReasons` define the worker-to-API contract: exactly one of value or reason per reading, closed reason sets for machine and player readings, memory used and total together, used not above total, count not above capacity.
- `ServerMetricSnapshot` holds the latest accepted sample per server (unique on the server) with every reading and reason; `ServerMetricBucket` holds count, sum, min and max per metric per window, unique on server, resolution and window start.
- `ServerMetricsService.IngestAsync` rejects contract violations, foreign deployments, samples beyond the skew limit and samples older than the stale limit before any write, skips non-primary hosts, and otherwise upserts the snapshot and merges the three tiers inside one transaction with one retry on a concurrent first insert.
- 28 tests pass under `FullyQualifiedName~ServerMetric`; the solution builds with 0 errors.

## Task Commits

1. **Task 1: Tracer - one primary-worker sample becomes the server's durable snapshot**
   - `517b48aa` feat(hosting): add the worker metrics sample contract
   - `d094c806` feat(database): add the server metric snapshot table
   - `bbf93e2e` feat(api): store the latest server metric sample
   - `db7250c1` test(api): cover server metric snapshot ingestion
2. **Task 2: Spike-preserving 15 s, 60 s and 300 s rollups and the ingest guards**
   - `e088a54f` feat(database): add server metric rollup buckets
   - `40d93857` feat(api): roll server metrics into spike-preserving tiers
   - `00132271` test(api): cover server metric tiers and ingest guards

**Plan metadata:** committed separately (docs: complete plan).

## Files Created/Modified
- `sources/HostingContracts/WorkerProtocol/WorkerMetricsSample.cs` - validated sample contract
- `sources/HostingContracts/WorkerProtocol/WorkerMetricUnavailableReasons.cs` - reason vocabulary and worker-allowed sets
- `sources/Database/Hosting/ServerMetricSnapshot.cs`, `ServerMetricBucket.cs` - entities; `sources/Database/DatabaseContext.cs` gains two DbSets
- `sources/Api/Services/ServerMetrics/*` - source and result records, resolutions with epoch-aligned `Floor`, options with `Validate`, service interface and implementation
- `tests/Api.Tests/GameServerHosting/ServerMetricsTestData.cs`, `ServerMetricIngestTests.cs` - builders and 13 test methods (28 cases)

## Decisions Made
- Contract, deployment and time-window checks run before the primary-host check so a bad sample from any host throws instead of being silently dropped.
- `WorkerSampledAt` is stored as UTC (`ToUniversalTime()`), since Npgsql `timestamptz` refuses non-zero offsets.
- No migration, `Program.cs` binding or `FleetChangeInterceptor` change, as the plan prohibits; the tables exist in tests through `EnsureCreatedAsync` only.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ServerMetricsOptions created in Task 1 instead of Task 2**
- **Found during:** Task 1
- **Issue:** The service constructor in the interfaces block takes `IOptions<ServerMetricsOptions>`, so the options type had to exist for Task 1 to compile.
- **Fix:** Created the complete options type (with `Validate`) and committed it with the Task 1 service commit; Task 2 only tests it.
- **Commit:** `bbf93e2e`

**2. [Rule 3 - Blocking] ServerMetricsTestData is internal**
- **Found during:** Task 2
- **Issue:** The plan lists it as `public static`, but it returns the internal `ServerMetricsService` and would fail with CS0050.
- **Fix:** Declared it `internal static`, matching `HostingTestData`.
- **Commit:** `00132271`

**3. [Rule 1 - Bug] Boundary test used an inconsistent receipt time**
- **Found during:** Task 2 verification
- **Issue:** The stale-boundary case in `SamplesAtTheSkewAndStaleLimits_AreAccepted` ingested at a later receipt than the sample's age assumed, so it was correctly rejected by the service.
- **Fix:** Corrected the test to use the same receipt time; no production change.
- **Commit:** `00132271`

**Total deviations:** 3 auto-fixed (2 blocking, 1 test bug). **Impact:** none on scope or behavior.

Note on TDD: the plan fixes the commit order contracts, entity, service, tests, and the types the tests exercise did not exist beforehand, so tests were written together with the implementation rather than as a separate failing RED commit.

## Issues Encountered
None beyond the deviations above.

## Known Stubs
None.

## Threat Flags
None. T-67-07 and T-67-08 are mitigated as planned (trusted source attribution, deployment mismatch rejection, value-or-reason validation with closed sets, skew and stale limits, receipt-time bucketing).

## Next Phase Readiness
Ready for plan 67-04 (read model, options binding in `Program.cs` and the EF migration for both new tables) and plan 67-07 (history read and pruning).

## Self-Check: PASSED

- All 12 created files exist on disk; commits `517b48aa`, `d094c806`, `bbf93e2e`, `db7250c1`, `e088a54f`, `40d93857` and `00132271` are ancestors of HEAD.
- Acceptance criteria: DbSet count 2, `IsUnique = true` count 1 in the bucket entity, `BeginTransactionAsync` present, 13 `[Fact]`/`[Theory]` methods.
- `dotnet test --filter FullyQualifiedName~ServerMetric`: 28 passed, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- No file under `sources/Api/Services/WorkerProtocol`, `sources/Database/Migrations`, `sources/Api/Program.cs`, `sources/WebApp`, `sources/ReactApp` or `sources/CloudWorker` was touched.

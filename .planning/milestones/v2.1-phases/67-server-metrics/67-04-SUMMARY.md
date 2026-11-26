---
phase: 67-server-metrics
plan: 04
subsystem: api
tags: [metrics, read-model, availability, ef-core, migration, postgres]

requires:
  - phase: 67-02
    provides: ServerMetricSnapshot, ServerMetricBucket, ServerMetricsOptions and IServerMetricsService.IngestAsync
provides:
  - GameServerMetricsDto with value-or-reason readings and StaleAfterSeconds
  - IServerMetricsService.EvaluateCurrentAsync (no authorization, for the publisher) and GetCurrentAsync (viewer only)
  - Hosting:Metrics options binding and defaults
  - Tool-generated AddServerMetrics migration creating the snapshot and bucket tables
affects: [67-07, 67-12, 67-16, server-metrics publisher, metrics endpoints]

plan_head_before: 4d0b69c162be7fbc7f729d4990076c201ef21a13
plan_head_after: 3937f3870f692daa3ef1363128cf8e10abbba3de

actuals:
  tokens: 12400
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "One reason-precedence function (Gate) shared by CPU, memory and players so the readings cannot disagree"
    - "Property-style theory over the whole deployment x worker x sample product with an independent oracle"

key-files:
  created:
    - sources/Api/DTO/GameServerMetricsDto.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsAvailabilityTests.cs
    - sources/Database/Migrations/20261122095232_AddServerMetrics.cs
    - sources/Database/Migrations/20261122095232_AddServerMetrics.Designer.cs
  modified:
    - sources/Api/Services/ServerMetrics/IServerMetricsService.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsService.cs
    - sources/Api/Program.cs
    - sources/Api/appsettings.json
    - tests/Api.Tests/GameServerHosting/ServerMetricsTestData.cs
    - sources/Database/Migrations/DatabaseContextModelSnapshot.cs

key-decisions:
  - "Machine readings (CPU, memory) stay visible while the worker is live even without a game process; only players and running since need the process, matching the plan's Claude's-discretion note"
  - "A snapshot with neither value nor reason is reported as measuring instead of an empty reading, so no reading is ever blank"
  - "A sample exactly at the stale limit (45 s) is still fresh; only older samples are stale"

patterns-established:
  - "Control plane decides availability; the browser only re-ages against StaleAfterSeconds"

requirements-completed: [MET-01, MET-02, MET-03, MET-04, MET-05]

coverage:
  - id: D1
    description: "Running since is the primary worker's observed process start, never the deployment timestamp, and is server-not-running without a process"
    requirement: "MET-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#RunningSince_IsTheObservedProcessStart"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#WorkerWithoutProcess_GivesServerNotRunning"
        status: pass
    human_judgment: false
  - id: D2
    description: "A fresh sample of the active session returns its values; a fresh zero stays zero; a worker reason has no value"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#FreshSample_ReturnsTheSampleValues"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#FreshZeroPlayers_StaysZero"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#WorkerReason_IsReturnedWithoutValue"
        status: pass
    human_judgment: false
  - id: D3
    description: "No reading shows a value unless the server runs, the worker is live and the sample is fresh and from the active session; exactly one reason otherwise"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsAvailabilityTests.cs#Reading_IsAValueOnlyWhenEveryConditionHolds"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsAvailabilityTests.cs#SampleAtTheStaleLimit_IsStillFresh"
        status: pass
    human_judgment: false
  - id: D4
    description: "Current readings are readable only by members who may view the server; others get not found"
    requirement: "MET-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs#GetCurrent_RequiresTheServerViewClaim"
        status: pass
    human_judgment: false
  - id: D5
    description: "The tool-generated AddServerMetrics migration creates exactly the two metrics tables and the model snapshot is in sync"
    requirement: "MET-04"
    verification:
      - kind: other
        ref: "dotnet ef migrations has-pending-model-changes (exit 0); dotnet build X39.UnitedTacticalForces.slnx (0 errors)"
        status: pass
    human_judgment: false

duration: 30min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 04: Current Readings and Metrics Migration Summary

**The control plane decides every current metric reading from the primary worker's observed process start and its fresh, session-matched sample with a single reason precedence, and a tool-generated migration makes the snapshot and rollup tables durable.**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-11-22T09:55Z
- **Tasks:** 3
- **Files modified:** 11 (5 created, 6 modified)

## Accomplishments
- `GameServerMetricsDto` carries running since, CPU, memory and players, each as a value or exactly one reason, plus `AsOf` and `StaleAfterSeconds` (45 by default).
- `EvaluateCurrentAsync` picks the newest unresolved deployment, its newest not-stopped `game-server` ordinal-0 worker, judges liveness against `LifecycleWorkerOptions.StaleThreshold` and counts a snapshot only for the active deployment. Reason precedence lives in one local function: server-not-running, worker-offline, (players and running since) server-not-running for a missing process, measuring, stale, then the worker's own value or reason.
- `GetCurrentAsync` authorizes like `CostAccrualService.GetForServerAsync` (view claim plus existence check, else `HostingNotFoundException`).
- `Hosting:Metrics` options are bound in `Program.cs` and defaulted in `appsettings.json`.
- `ServerMetricsAvailabilityTests` runs 50 combinations (2 deployment x 5 worker x 5 sample states) against an independent oracle and checks that no reading carries both a value and a reason.
- `20261122095232_AddServerMetrics` creates exactly `ServerMetricBuckets` and `ServerMetricSnapshots` (foreign keys and indexes) and drops exactly those; `has-pending-model-changes` exits 0.

## Task Commits

1. **Task 1: Tracer - running since and current readings** (tracer verified end-to-end: 43 tests)
   - `6014a47e` feat(api): decide current server metric readings in the control plane
   - `b3fe857c` test(api): cover running since and current readings
2. **Task 2: Availability product proof**
   - `98d10033` test(api): prove metrics are never shown without a fresh reading
   - no production change was needed, so no `fix(api)` commit
3. **Task 3: Scaffold the AddServerMetrics migration**
   - `3937f387` feat(database): add the server metrics migration

**Plan metadata:** committed separately (docs: complete plan).

## Files Created/Modified
- `sources/Api/DTO/GameServerMetricsDto.cs` - current readings with reason codes and the stale threshold
- `sources/Api/Services/ServerMetrics/IServerMetricsService.cs`, `ServerMetricsService.cs` - `EvaluateCurrentAsync`, `GetCurrentAsync`, new constructor
- `sources/Api/Program.cs`, `sources/Api/appsettings.json` - options binding and defaults
- `tests/Api.Tests/GameServerHosting/ServerMetricsTestData.cs` - new `Create` signature, `AddPrimaryWorkerAsync`, `AddSnapshotAsync`
- `tests/Api.Tests/GameServerHosting/ServerMetricsReadModelTests.cs`, `ServerMetricsAvailabilityTests.cs` - 8 read-model and 51 availability test cases
- `sources/Database/Migrations/20261122095232_AddServerMetrics.cs`, `.Designer.cs`, `DatabaseContextModelSnapshot.cs` - generated by `dotnet ef migrations add` (10.0.9)

## Decisions Made
- CPU and memory stay visible while the worker is live even if no game process runs (the machine exists and may cost); players and running since require the process.
- A stored snapshot reading with neither value nor reason is reported as measuring so no reading is ever blank.
- A sample exactly 45 s old is fresh; staleness starts beyond it.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Extra test helpers and an optional parameter**
- **Found during:** Task 1
- **Issue:** Seeding snapshots for previous-deployment and reason cases through `IngestAsync` is impossible (it rejects foreign deployments), and the missing-worker case needs a stopped worker to satisfy the snapshot foreign key.
- **Fix:** Added `ServerMetricsTestData.AddSnapshotAsync` and an optional trailing `stoppedAt` parameter on `AddPrimaryWorkerAsync`; the plan's signature stays source-compatible.
- **Commit:** `b3fe857c`

**2. [Rule 3 - Blocking] Missing using in the service**
- **Found during:** Task 1
- **Issue:** `EWorkerStatus` needs `X39.UnitedTacticalForces.Hosting.Contracts`.
- **Fix:** Added the using before the first commit.
- **Commit:** `6014a47e`

**Total deviations:** 2 auto-fixed (both blocking, test-support only). **Impact:** none on scope or behavior.

Note on TDD: as in plan 67-02, the new types and constructor the tests exercise did not exist beforehand, so a compile-failing RED commit was not possible without breaking the build; tests and implementation were written together and committed in the plan's order (feat, then test). The first commit message was amended once to add the attribution trailer before any further work.

## Issues Encountered
None. No OpenAPI snapshot or Kiota regeneration was needed: this plan adds no endpoint or exposed DTO route (the DTO is first served by a later plan).

## Known Stubs
None.

## Threat Flags
None. T-67-09 is mitigated (view claim plus existence check before any read, not found otherwise) and T-67-34 is mitigated (single precedence, exhaustive property test, running since only from the observed process start). No file under `sources/Api/Services/WorkerProtocol`, `LifecycleWorkerOptions.cs`, `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker` was touched.

## Next Phase Readiness
Ready for plan 67-07 (history read and pruning) and plan 67-12 (publisher using `EvaluateCurrentAsync`); the browser plan 67-16 re-ages readings against `StaleAfterSeconds`.

## Self-Check: PASSED

- All 5 created files exist on disk; commits `6014a47e`, `b3fe857c`, `98d10033` and `3937f387` are ancestors of HEAD.
- `Configure<ServerMetricsOptions>` count 1; `TimeStampStarted` in non-comment service lines 0; `[Fact]/[Theory]` count in the read-model tests 8; `[Theory]` and `MemberData` present in the availability tests; `ServerMetricBuckets` in the model snapshot; `git ls-files` lists exactly the migration and its designer.
- `dotnet test --filter FullyQualifiedName~ServerMetric`: 87 passed, 0 failed; `dotnet ef migrations has-pending-model-changes` exit 0; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.

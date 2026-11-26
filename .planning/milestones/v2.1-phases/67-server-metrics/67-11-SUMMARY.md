---
phase: 67-server-metrics
plan: 11
subsystem: api
tags: [worker-protocol, websocket-channel, wake-signal, ef-interceptor, cost-stop, dotnet, xunit]

requires:
  - phase: 67-03
    provides: WorkerChannelHandler that sends Wake after an ack when a route asks for it
  - phase: 67-06
    provides: IWorkerChannelRegistry push by worker id and by deployment id
provides:
  - IWorkerChannelSignal and a queued WorkerChannelSignal that send Wake frames from one background drainer
  - WorkerChannelSignalInterceptor that wakes workers after the commit of command and deployment changes
  - IWorkerProtocolService.HasDeliverableCommandAsync sharing the claim query and the cost-stop rule
  - Heartbeat acks followed by a Wake while a command is deliverable (missed-push safety net)
affects: [67-12 metrics route, 67-13, worker channel client]

actuals:
  tokens: 17800
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Post-commit interceptor mirroring FleetChangeInterceptor: collect before save, publish after a save without a transaction, defer to TransactionCommitted, discard on failure, cancel, rollback and TransactionStarted"
    - "Wake is a hint, never a command push; the worker answers it through the unchanged protocol service"
    - "One private candidate query shared by claim and deliverability check so they cannot disagree"

key-files:
  created:
    - sources/Api/Services/WorkerProtocol/IWorkerChannelSignal.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelSignal.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelSignalInterceptor.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelSupersessionTests.cs
  modified:
    - sources/Api/Program.cs
    - sources/Api/Services/WorkerProtocol/IWorkerProtocolService.cs
    - sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs
    - sources/Api/Services/WorkerProtocol/HeartbeatFrameRoute.cs
    - tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs
    - tests/Api.Tests/GameServerHosting/LifecycleWorkersControllerTests.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs

key-decisions:
  - "A save that changes a command status and its deployment status sends the worker two Wake frames (one by worker id, one by deployment id); Wake is idempotent so the duplicate is accepted rather than adding a deployment-to-worker lookup to the interceptor"
  - "Deployment Added does not wake (only Status or IsProvisioningBlocked modifications), per the plan; a new deployment has no connected worker yet"
  - "HasDeliverableCommandAsync loads the worker and candidate untracked without a transaction or row lock; it never writes lease, attempt or liveness"

requirements-completed: [MET-05]

duration: 21min
completed: 2026-11-22
status: complete
commits: 4
plan_head_before: 2dd5dd44cc8fac9373c39a4265611d2d142d0a3b
plan_head_after: bf94156856ddd0119117dcfc88ad10bd024cdace

coverage:
  - id: D1
    description: "Committing a command for a connected worker pushes a Wake to its socket after the commit, never before it and never for a rolled-back or failed save"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#AddedCommand_WakesTheConnectedWorkerAfterTheSave"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#TransactionalSave_WakesOnlyAfterCommit_AndNeverAfterRollback"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#FailedSave_SendsNothing"
        status: pass
    human_judgment: false
  - id: D2
    description: "Command status and deployment status or provisioning-block changes wake the affected workers; stage-only changes stay silent"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#CommandStatusChange_Wakes_StageOnlyChange_StaysSilent"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#DeploymentStatusAndProvisioningBlock_WakeTheDeploymentsWorkers_StageStaysSilent"
        status: pass
    human_judgment: false
  - id: D3
    description: "Signals never block or fail a save: a throwing registry logs one warning with ids only and the drainer keeps delivering"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#ThrowingRegistry_LogsOneWarningPerRequest_AndKeepsDraining"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSignalTests.cs#WakeDeployments_PushesToTheDeployment"
        status: pass
    human_judgment: false
  - id: D4
    description: "Heartbeat acks are followed by a Wake only while a command is deliverable, recovering a missed push within one heartbeat; the deliverability check reuses the claim query and cost-stop rule"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSupersessionTests.cs#Heartbeat_IsFollowedByAWakeOnlyWhileACommandIsDeliverable"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs#HasDeliverableCommand_AppliesTheClaimsCostStopRule"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs#HasDeliverableCommand_QueuedCommandOfRunningJob_IsTrueAndChangesNothing"
        status: pass
    human_judgment: false
  - id: D5
    description: "A cost-stop during a running command wakes the worker and its Running probe is answered Superseded, with no polling"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelSupersessionTests.cs#CostStopDuringARunningCommand_WakesTheWorker_AndTheRunningProbeIsSuperseded"
        status: pass
    human_judgment: false
---

# Phase 67 Plan 11: Push-Driven Worker Wake Summary

**Post-commit Wake hints for connected workers: an EF interceptor on command and deployment changes feeding a queued signal, plus a heartbeat safety net built on a read-only deliverability check that reuses the claim query**

## Performance

- **Duration:** about 21 min
- **Tasks:** 2 (tracer plus safety net)
- **Files:** 13 (5 created, 8 modified)

## Accomplishments
- `WorkerChannelSignalInterceptor` mirrors `FleetChangeInterceptor` (per-context `ConditionalWeakTable`, collect before save, publish after a save without a transaction, defer to `TransactionCommitted`, discard on save failure, cancel, rollback, transaction failure and `TransactionStarted`). It collects added or status-modified `WorkerCommand` rows (their `WorkerFk`) and status- or provisioning-block-modified `GameServerDeployment` rows (their `PrimaryKey`), and never throws into the save path.
- `WorkerChannelSignal` queues requests on an unbounded channel and drains them in one background loop through `TrySendAsync` and `SendToDeploymentAsync`; a failed send logs one warning with ids and the exception type only.
- `HasDeliverableCommandAsync` reuses a single private `QueryClaimCandidates` (filter and ordering extracted from the claim without behavior change) and `CanExecuteCommand`. It is untracked, takes no transaction or lock and writes nothing.
- `HeartbeatFrameRoute` asks it after each accepted heartbeat and returns `WakeAfterAck`, so a missed push recovers within one heartbeat interval without the worker claiming on a timer.
- `Program.cs` registers the signal (singleton plus hosted service) and the interceptor and passes both interceptors to `AddInterceptors`.
- Tests: 7 in `WorkerChannelSignalTests`, 3 in `WorkerChannelSupersessionTests` (including end-to-end cost-stop: Wake, then a Running probe acked Superseded), 6 facts and theory cases in `WorkerProtocolServiceTests`.

## Task Commits

1. **Task 1 (tracer): committing a command wakes the connected worker after the commit**
   - `ec1fde9c` feat(api): wake connected workers when their commands change
   - `e5ee6132` test(api): cover worker wake signals
2. **Task 2: heartbeat safety net and cost-stop supersession**
   - `fcd40b4b` feat(api): wake workers with deliverable commands after heartbeats
   - `bf941568` test(api): prove cost-stop supersession over the worker channel

**Plan metadata:** committed separately as `docs(67-11): complete push-driven worker wake plan`.

## Decisions Made
- Wake stays a hint; supersession is learned through the worker's own Running probe, so `CanExecuteCommand` remains the only rule.
- A combined command and deployment status change sends two Wake frames to the same worker; accepted as harmless since Wake carries no data.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing channel tests assumed a heartbeat never trails a Wake**
- **Found during:** Task 2
- **Issue:** `Heartbeat_PersistsProcessEvidenceAndReturnsEmptyResult` asserted the shared `Empty` instance, and two handler tests (`FrameRate_RefillsWithElapsedTime`, `CredentialRotation_KeepsTheOldSocket...`) expected a close frame right after a heartbeat ack; with a deliverable seeded command the heartbeat now correctly trails a Wake.
- **Fix:** The route test asserts a null result with `WakeAfterAck`; `AssertStillServingAsync` gained an opt-in `drainHeartbeatWake` flag used by the two handler tests.
- **Files modified:** tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs, tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs (neither was in the plan file list; both are direct consequences of the new behavior)
- **Verification:** full `GameServerHosting` filter, 1146 passed
- **Committed in:** bf941568

**2. [Ordering] Interceptor registered in `Program.cs` in the tracer commit**
- The plan lists `Program.cs` under Task 1; it shipped with `ec1fde9c` together with the production types so the commit builds.

---

**Total deviations:** 2 (1 test adaptation, 1 ordering note)
**Impact on plan:** None on scope; no edit to `WorkerChannelHandler.cs`, `WorkerChannelRegistry.cs`, `WorkerChannelTestHost.cs`, `MetricsFrameRoute.cs`, `GameServersController.cs`, ReactApp, WebApp or CloudWorker.

## TDD Notes

Tests were written alongside the implementation in the same task; no separate RED commit was made (a non-compiling RED state is forbidden by the executor brief). Each test pair was committed green.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 1146 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- Acceptance greps: `WorkerChannelSignalInterceptor` in `Program.cs` 2 hits, 7 facts in `WorkerChannelSignalTests`, `HasDeliverableCommandAsync` 1 hit in the interface and 2 in the route, 3 facts in `WorkerChannelSupersessionTests`.

## Known Stubs

None.

## Threat Flags

None. The only new surface is an outbound Wake frame with no payload on an already authenticated socket.

## Self-Check: PASSED

- Created files exist: IWorkerChannelSignal.cs, WorkerChannelSignal.cs, WorkerChannelSignalInterceptor.cs, WorkerChannelSignalTests.cs, WorkerChannelSupersessionTests.cs.
- Commits `ec1fde9c`, `e5ee6132`, `fcd40b4b`, `bf941568` are ancestors of HEAD.

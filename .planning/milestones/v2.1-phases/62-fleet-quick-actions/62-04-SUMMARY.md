---
phase: 62-fleet-quick-actions
plan: 04
subsystem: api
tags: [ef-core-interceptor, transactions, websocket, xunit, sqlite]

requires:
  - phase: 62-fleet-quick-actions
    provides: "62-01 tracer: FleetChangeInterceptor (job-only), IFleetChangeNotifier, HostingTestStore interceptor overload"
provides:
  - FleetChangeInterceptor covering server, deployment and job changes the fleet shows
  - Transaction-aware deferral (publish on commit, discard on rollback, transaction failure, failed or canceled save)
  - Optional IFleetChangeNotifier on LifecycleJobExecutor with one post-commit Notify at the possible-cost bulk update
  - FleetChangeInterceptorTests behavior matrix (8 facts)
affects: [62-05, 62-06, 62-11, 63-workspace]

actuals:
  tokens: 12100
  tasks: 2
  commits: 2
plan_head_before: d8f9efd7d05634f85f7db04477523342e5b8f02a
plan_head_after: 98310ae95cc5e84f904d3395af3d393fa8724bfe

tech-stack:
  added: []
  patterns:
    - "One singleton class implements both SaveChangesInterceptor and IDbTransactionInterceptor; per-context Collected/Deferred sets in a ConditionalWeakTable"
    - "SavedChanges publishes when Database.CurrentTransaction is null, otherwise defers to TransactionCommitted"
    - "Writes that bypass SaveChanges (ExecuteUpdate) signal explicitly after their transaction commits"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
    - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
    - tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs
    - tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs

key-decisions:
  - "IDbTransactionInterceptor implemented directly on FleetChangeInterceptor (default interface members compile); no second class or Program.cs change needed"
  - "TransactionStarted also clears Deferred, so ids from a transaction disposed without commit or rollback cannot leak into the next commit on the same context"
  - "SaveChangesCanceled clears Collected just like SaveChangesFailed"
  - "Kept the ILogger introduced by 62-01 for swallowed collect/publish failures (plan text assumed no logger)"
  - "The tracer test seeds through an interceptor-free context on the same SQLite connection, because seeding a server and deployment is now itself a fleet-relevant change"

patterns-established:
  - "Fleet-relevant change set: GameServer add/Title/IsArchived/IsLaunchDisabled/CurrentCostOwnerFk; GameServerDeployment add/Status/PossibleCost; LifecycleJob add/Status"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "Interceptor signals exactly the fleet-relevant committed changes once per distinct server and stays silent for stage, progress and timestamp churn"
    requirement: FLT-04
    verification:
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#GameServerAddAndFleetVisibleEdits_NotifyServerOncePerSave"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#DeploymentStatusAndPossibleCost_Notify_StageAndTimestamp_StaySilent"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#JobAddAndStatus_Notify_ProgressAndStage_StaySilent"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#OneSave_NotifiesEachDistinctServerOnce"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#SaveWithoutRelevantChange_NotifiesNothing"
        status: pass
    human_judgment: false
  - id: D2
    description: "Saves inside explicit transactions are deferred until commit, discarded on rollback and failed saves, and contexts sharing the interceptor never exchange ids"
    requirement: FLT-04
    verification:
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#ExplicitTransaction_DefersUntilCommit_AndPublishesUnionOnce"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#RollbackAndFailedSave_NeverNotify_AndDoNotLeak"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#TwoContexts_SharingInterceptor_DoNotExchangeIdentifiers"
        status: pass
    human_judgment: false
  - id: D3
    description: "The possible-cost bulk update signals once after its transaction commits, never in the no-allocation path, and the executor stays hand-constructible"
    requirement: FLT-04
    verification:
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#Start_WhenCostBearingResultIsAccounted_SignalsFleetOnceAfterCommit"
        status: pass
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#Start_WhenProviderRejectsWithoutAllocating_DoesNotSignalFleetExplicitly"
        status: pass
      - kind: command
        ref: "dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~GameServerHosting (548 passed)"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 04: Complete Fleet Signal Source Summary

**`FleetChangeInterceptor` now signals every committed change the fleet shows. That covers a server being added, retitled, archived, launch-toggled or given a new cost owner; a deployment's status or possible-cost change; and a job being added or changing status. Inside an explicit transaction the interceptor holds signals until `TransactionCommitted` and drops them on rollback or failure. `LifecycleJobExecutor` adds one explicit post-commit `Notify` for the possible-cost `ExecuteUpdate`, which the interceptor cannot see.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-11-19T08:53:22Z
- **Completed:** 2026-11-19T09:01:41Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Coverage now matches D-14. Changes to stage, progress, timestamps, owner display name and other server fields produce no signal, so a running job cannot flood the stream.
- Signals are tied to transactions. `SavedChanges` publishes immediately when there is no explicit transaction; otherwise the ids move to `Deferred`. `TransactionCommitted` publishes `Deferred`. Rollback, transaction failure and `TransactionStarted` clear it. Failed and canceled saves clear only `Collected`. The implicit EF save transaction commits while `Deferred` is still empty, so each save publishes exactly once.
- Contexts stay isolated. All state lives in the per-context holder under its lock. In the two-store test, one context's deferred id appears only after that context commits, while the other context's id appears immediately.
- The executor's explicit signal fires only when `providerResultRequiresCostAccounting` is true, and directly after `resultTransaction.CommitAsync`. The recorder confirms the context had no open transaction when the call was made.

## Task Commits

1. **Task 1: Interceptor entity coverage and transaction-aware deferral**: `8de27752` (feat(api): signal every committed fleet-relevant change)
2. **Task 2: Explicit post-commit signal at the possible-cost bulk update**: `98310ae9` (feat(api): signal the fleet after the possible-cost bulk update)

TDD evidence:
- Task 1: the new suite was run against the tracer-era interceptor (`git show HEAD:` copy). 6 of 8 tests failed, which is RED. The two that passed were the job-only and no-relevant-change cases, which the tracer already covered. All 8 pass on the new code, which is GREEN.
- Task 2: removing the `Notify` line made `Start_WhenCostBearingResultIsAccounted_SignalsFleetOnceAfterCommit` fail, and the no-allocation test still passed. With the line restored, both pass.

## Files Created/Modified
- `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs`: adds full entity coverage, transaction interception (started, committed, rolled back, failed; sync and async) and canceled-save discard
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs`: adds an optional trailing `IFleetChangeNotifier? fleetChangeNotifier = null`, one post-commit `Notify`, and class remarks
- `tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs`: 8-fact matrix with a nested `RecordingNotifier`
- `tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs`: notifier parameter on both `CreateExecutor` overloads, two scenario tests, and a nested `RecordingFleetChangeNotifier` plus `RecordedFleetSignal`
- `tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs`: seeds through an interceptor-free context

## Decisions Made
See key-decisions in the frontmatter. The compiler accepted `IDbTransactionInterceptor` directly on the existing class, so the plan's fallback (a second class registered in Program.cs) was not needed, and Program.cs is unchanged.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Tracer test received two frames after the coverage expansion**
- **Found during:** Task 1
- **Issue:** `FleetSignalTracerTests.CommittedLifecycleJobInsert_ReachesFleetSubscriberAsPinnedFrame` seeds a server and a deployment through the intercepted context. Those adds are now fleet-relevant, so the coalesced seed frame could reach the socket before the job frame, and the "exactly one frame" assertion failed.
- **Fix:** The test now seeds through a plain `NodaTimeSqliteApiDbContext` on `store.Connection` with no interceptor, then inserts the job through the intercepted context. The test's intent and its pinned frame are unchanged.
- **Files modified:** tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs (not in the plan's file list)
- **Commit:** 8de27752

**2. [Rule 2 - Missing Critical] Stale deferred ids on a transaction disposed without commit or rollback**
- **Found during:** Task 1
- **Issue:** EF raises no interceptor event when a transaction is disposed without an explicit commit or rollback, for example in the `await using` + exception path. Deferred ids would survive and be published by the context's next commit, which is a phantom signal (T-62-10).
- **Fix:** `TransactionStarted`/`TransactionStartedAsync` clear `Deferred`. This is safe because a new transaction can only start once no explicit one is open on the context. These two members go beyond the plan's six transaction members.
- **Commit:** 8de27752

**3. [Rule 2 - Missing Critical] Canceled saves**
- **Found during:** Task 1
- **Issue:** The plan requires failed or canceled saves to clear `Collected`, but the tracer handled only `SaveChangesFailed`.
- **Fix:** Added `SaveChangesCanceled`/`SaveChangesCanceledAsync`, which clear `Collected`.
- **Commit:** 8de27752

**Total deviations:** 3 auto-fixed (1 bug, 2 missing critical). **Impact:** all are inside the plan's surface. There are no public, contract or schema changes.

## Issues Encountered
None. The unrelated working-tree change in `sources/ReactApp/tests/hosting/arma-placement.test.tsx` was left untouched.

## Known Stubs
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- 62-05 can add notifier concurrency and failure tests on top of a complete signal source.
- Known limitation (D-14, unchanged): the signal is process-local.
- Deferred by design: signals for claim or permission changes (research Pitfall 17).

## Self-Check: PASSED
- `tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs` exists.
- Commits `8de27752` and `98310ae9` are ancestors of HEAD; `git rev-list --count d8f9efd7..HEAD` = 2.
- Acceptance re-run: `TransactionCommitted` count 2 (>=1), `GameServerDeployment` count 3 (>=1), `IRuntimeProvider` count 0. In the executor, `IFleetChangeNotifier` count is 2 (>=2) and `fleetChangeNotifier?.Notify` count is 1. Interceptor and tracer filter: 10/10. Executor cloud plus interceptor: 60/60. `GameServerHosting`: 548/548.
- Plan verification: `git diff --name-only d8f9efd7 HEAD -- sources/Database sources/ReactApp` prints nothing.

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

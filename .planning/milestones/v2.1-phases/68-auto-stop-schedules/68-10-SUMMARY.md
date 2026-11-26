---
phase: 68-auto-stop-schedules
plan: 10
subsystem: api
tags: [automation, idle-shutdown, max-runtime, hosted-service, lifecycle, auto-stop]

requires:
  - phase: 68-auto-stop-schedules
    provides: entities and audit kinds (68-01), IdleClock and options (68-02), EnqueueAutomatedActionAsync (68-05), projector (68-06), AutoStop alert writer (68-07)
provides:
  - Pure PendingStopPlanner (warn, cancel, supersede, execute, execute without warning)
  - ServerAutomationEvaluator persistence shell with per-server transaction, conditional claim and ordinary Stop job
  - ServerAutomationWorker hosted loop registered only while the control plane is not paused
affects: [68-11, 68-13, 68-14]

status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 5
plan_head_before: 40fdf0f41c721d924123f7585d11234da9870868
plan_head_after: 69e04bcf5547026f745205bfa5266debb1de5e2a

tech-stack:
  added: []
  patterns:
    - "Pure planner plus persistence shell: the evaluator gathers facts under the server-row lock, the planner decides, the shell applies"
    - "Conditional ExecuteUpdateAsync claim followed by tracked entity changes so the fleet interceptor emits live frames"

key-files:
  created:
    - sources/Api/Services/ServerAutomation/EPendingStopDecision.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlanInput.cs
    - sources/Api/Services/ServerAutomation/PendingStopSnapshot.cs
    - sources/Api/Services/ServerAutomation/PendingStopCandidate.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlan.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlanner.cs
    - sources/Api/Services/ServerAutomation/IServerAutomationEvaluator.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationEvaluator.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationEvaluationResult.cs
    - sources/Api/HostedServices/ServerAutomationWorker.cs
    - tests/Api.Tests/ServerAutomation/PendingStopPlannerTests.cs
    - tests/Api.Tests/ServerAutomation/MaxRuntimeTests.cs
    - tests/Api.Tests/ServerAutomation/IdleShutdownTests.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationWorkerTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationTestData.cs
    - tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs

key-decisions:
  - "An open idle warning whose reading turns Unknown is held (neither executed nor cancelled), as recorded in the plan"
  - "The maximum runtime limit instant is max(start + hours, settings.TimeStampUpdated); a limit past its warning window without a warning row executes without a warning"
  - "A Failed stop job of the deployment counts as a stop already queued, so a failed automatic stop is never retried or escalated by the evaluator; a person retries or skips it"
  - "Servers with a running idle clock are evaluated even when idle shutdown is off, so the clock resets"

patterns-established:
  - "ServerAutomationTestData.CreateEvaluator is the single construction site of the evaluator in tests"

requirements-completed: [AUT-01, AUT-02, AUT-04, AUT-05, AUT-06]

coverage:
  - id: D1
    description: "Maximum runtime warns at the limit and queues one ordinary Stop job one lead later; downtime stop runs without a warning; raised limit withdraws the warning"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/MaxRuntimeTests.cs"
        status: pass
  - id: D2
    description: "Idle shutdown warns after the idle limit, stops one lead later, withdraws on activity and settings change, and never acts on an unknown reading"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/IdleShutdownTests.cs, PendingStopPlannerTests.cs"
        status: pass
  - id: D3
    description: "Hosted worker registered only when not paused, publishes after evaluation, survives exceptions, isolates failing servers"
    requirement: "AUT-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ServerAutomationWorkerTests.cs, HostingControlPlaneStateTests.cs"
        status: pass
---

# Phase 68 Plan 10: Idle and Maximum Runtime Shutdown Summary

**Servers now stop themselves after their idle time or maximum runtime: a pure planner decides, a per-server transactional evaluator records the warning episode and queues an ordinary Stop job exactly once, and a hosted worker runs it while the control plane is not paused.**

## Accomplishments

- `PendingStopPlanner.Plan` is pure: with an open stop it supersedes (deployment not running, stop already queued), cancels (activity, settings changed), holds (unknown idle reading) or executes at `StopsAt`; without one it warns at the limit and executes without a warning when a maximum runtime window elapsed unobserved.
- `ServerAutomationEvaluator` evaluates each server in its own transaction under the server-row lock. A warning is one pending stop, one AutoStop alert with deliveries and one `AutomaticStopWarned` audit entry. Execution claims `Pending -> Executed` with a conditional `ExecuteUpdateAsync`, then calls `EnqueueAutomatedActionAsync` in the same transaction; a `HostingConflictException` rolls both back and the next pass retries. Tracked changes after the claim keep the fleet interceptor's live frames working (coordinator note from 68-09).
- The idle clock state is persisted in `ServerAutomationState` through `IdleClock.Advance`; the settings row and its `Version` are never touched.
- `ServerAutomationWorker` copies the cost alert worker loop, publishes raised and resolved alerts after the committed pass and is registered next to `CostAlertWorker` inside the not-paused block. The evaluator validates `ServerAutomationOptions` in its constructor (note from 68-02).

## Task Commits

1. `ee565397` feat(api): plan automatic shutdown warnings
2. `759ebfcb` feat(api): stop servers after their maximum runtime
3. `19a75436` feat(api): stop idle servers
4. `40258d4a` feat(api): run server automation in the control plane
5. `69e04bcf` test(api): cover automatic shutdown for idle and maximum runtime

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Failed stop job blocks automatic re-queue**
- **Found during:** Task 1
- **Issue:** A Failed Stop job can leave the deployment Running (the plan assumed it leaves it not Running). Without a guard the evaluator would queue a new Stop, or an immediate stop without warning, on every pass, which breaks D-AUTO-2.
- **Fix:** `HasStopJobAsync` counts Failed stop jobs as "stop already queued"; a person retries or skips the failed job. Covered by `Pass_AfterAFailedStop_NeverQueuesATerminateJobOrAnotherStop`.
- **Files modified:** `ServerAutomationEvaluator.cs`, `MaxRuntimeTests.cs`
- **Commit:** `759ebfcb`

**2. [Rule 3 - Blocking] Existing registration test lists the hosted services**
- **Found during:** Task 3
- **Issue:** `HostingControlPlaneStateTests` asserts the exact hosted service set and failed after registering the new worker.
- **Fix:** Added `ServerAutomationWorker` to the expected list.
- **Files modified:** `tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs`
- **Commit:** `40258d4a`

### Other notes

- Evaluator candidate selection additionally includes servers with a running idle clock so a disabled idle setting resets its clock.
- The planner ignores `PendingStopPlanInput.ScheduledStop` until plan 68-11 supplies schedule candidates.
- The second `ExecuteUpdateAsync` for the job id was replaced by a tracked change (`LifecycleJobFk`) so the live frame is emitted.

## Known Stubs

None.

## Threat Flags

None. No new endpoint or trust boundary; the evaluator uses the existing metrics reading and lifecycle enqueue paths.

## Self-Check: PASSED

- Files exist: all created files listed above found.
- Commits `ee565397`, `759ebfcb`, `19a75436`, `40258d4a`, `69e04bcf` are ancestors of HEAD.
- `dotnet test` filter `ServerAutomation|GameServerHosting`: 1384 passed, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.

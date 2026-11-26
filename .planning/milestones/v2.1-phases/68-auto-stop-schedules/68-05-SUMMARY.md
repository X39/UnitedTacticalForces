---
phase: 68-auto-stop-schedules
plan: 05
subsystem: api
tags: [lifecycle, automation, enqueue, cost-slice, affordability]

requires:
  - phase: 68-auto-stop-schedules
    provides: ELifecycleJobTrigger, LifecycleJob.Trigger/TriggerLimit, audit kinds AutomaticStopRequested and ScheduledStartRequested (plan 68-01)
provides:
  - One shared lifecycle enqueue core used by the manual and the system-initiated path
  - IGameServerLifecycleService.EnqueueAutomatedActionAsync accepting Start and Stop only
  - AutomatedLifecycleRequest record
  - IHostingCostSliceService.CanReserveInitialSliceAsync, a read-only check sharing the reservation formula
affects: [68-06, 68-07, 68-11]

status: complete
actuals:
  tokens: 60000
  tasks: 2
  commits: 5
plan_head_before: c00145f8efa1c73a01bede1aba6d1e40781e0bbe
plan_head_after: b088372550d712f4398e4e5f4cf8d970c2b4af13

tech-stack:
  added: []
  patterns:
    - "Enqueue core joins dbContext.Database.CurrentTransaction when present and only commits a transaction it opened itself"
    - "A nullable principal marks a system-initiated request; user-only checks (confirmation title, cost owner consent) run only for a principal"
    - "One private availability calculation (EvaluateInitialSliceAsync) feeds both the executor reservation and the pre-check"

key-files:
  created:
    - sources/Api/Services/GameServerLifecycle/AutomatedLifecycleRequest.cs
    - tests/Api.Tests/GameServerHosting/AutomatedLifecycleEnqueueTests.cs
    - tests/Api.Tests/GameServerHosting/HostingCostSliceAffordabilityTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs
    - sources/Api/Services/GameServerLifecycle/IGameServerLifecycleService.cs
    - sources/Api/Services/GameServerLifecycle/HostingCostSliceService.cs
    - sources/Api/Services/GameServerLifecycle/IHostingCostSliceService.cs
    - tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs
    - tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs

key-decisions:
  - "The core refuses any kind other than Start or Stop when there is no principal, in addition to the public method's own action check (defense in depth for D-AUTO-2)"
  - "CanReserveInitialSliceAsync also returns false for a non-EUR owner and for a provider without a configured rate, because the executor's reservation would refuse both"
  - "AuditNote is trimmed and cut to 256 characters before it reaches the audit writer, which sanitizes the whole details string"

patterns-established:
  - "System-initiated Stop is the same Priority 50 job as a manual Stop, distinguished only by Trigger, TriggerLimit, a null requester and the AutomaticStopRequested audit kind"

requirements-completed: [AUT-03, AUT-05]

coverage:
  - id: D1
    description: "Automated Stop queues a normal Stop job (priority 50, no user, trigger and limit) with one AutomaticStopRequested audit row and no LifecycleJobQueued row"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AutomatedLifecycleEnqueueTests.cs"
        status: pass
  - id: D2
    description: "Terminate, restart, update, collect-logs, custom actions and the Manual trigger are refused with HostingValidationException and write nothing"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AutomatedLifecycleEnqueueTests.cs"
        status: pass
  - id: D3
    description: "The core joins the caller's transaction: rollback removes job, audit and the provisioning block; the caller's commit keeps them"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AutomatedLifecycleEnqueueTests.cs"
        status: pass
  - id: D4
    description: "The manual path is unchanged (Manual trigger, requesting user, LifecycleJobQueued audit); GameServerLifecycleServiceTests pass unchanged"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs and AutomatedLifecycleEnqueueTests.cs"
        status: pass
  - id: D5
    description: "CanReserveInitialSliceAsync agrees with ReserveInitialSliceAsync on both sides of the safety range boundary, counts allowance and other reservations, and writes nothing"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/HostingCostSliceAffordabilityTests.cs"
        status: pass
---

# Phase 68 Plan 05: Automated lifecycle enqueue and start affordability Summary

**The control plane can now queue an ordinary Start or Stop job without a user, inside the caller's transaction, through the same preconditions as a manual request, and can never queue Terminate; a read-only check answers with the executor's own formula whether a scheduled start's cost owner can pay.**

## Accomplishments

- Moved the body of `EnqueueActionAsync` into the private `EnqueueCoreAsync`, which takes an optional actor and principal, a trigger, a trigger limit, an audit note and the evaluation instant. The manual method keeps its authorization and action parsing and calls the core with `Trigger Manual`.
- The core joins `dbContext.Database.CurrentTransaction` when the caller opened one (no nested begin, no commit) and otherwise begins and commits its own, as before.
- Added `AutomatedLifecycleRequest` and `EnqueueAutomatedActionAsync`: Start and Stop only, a non-Manual trigger required, no synthetic principal, no confirmation-title check and no cost-owner consent check (the evaluator proves consent in plan 68-11); the frozen cost owner check stays.
- Queue audit kind: `TerminateRequested` for Terminate, `AutomaticStopRequested` or `ScheduledStartRequested` for automation, otherwise `LifecycleJobQueued`; automation audits carry a null actor and `Trigger=...; Limit=...` plus the optional note.
- Extracted `EvaluateInitialSliceAsync` in `HostingCostSliceService` (rate selection, slice amount, other reservations, safety range comparison) and used it from `ReserveInitialSliceAsync` and the new `CanReserveInitialSliceAsync`. `MinimumSafetyRangeEur` still appears twice in the file.
- `AutomatedLifecycleEnqueueTests` (18 cases including theory rows) and `HostingCostSliceAffordabilityTests` (16 cases including theory rows) were added. The full `Api.Tests` suite passes (1511 tests) and `dotnet build X39.UnitedTacticalForces.slnx` has 0 errors.

## Task Commits

1. Task 1 (tracer): `3581bc6c` refactor shared enqueue core; `d2e094a3` automated start and stop entry point; `2c7f2098` tests
2. Task 2: `feed0148` affordability pre-check; `b0883725` tests

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Two IHostingCostSliceService test doubles needed the new member**
- **Found during:** Task 2
- **Issue:** `RecordingCostSliceService` in `LifecycleJobExecutorCloudTests.cs` and `NullCostSliceService` in `UatWorkerChannelEmulatorTests.cs` implement the interface and would no longer compile.
- **Fix:** Added a trivial `CanReserveInitialSliceAsync` to both doubles.
- **Files modified:** `tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs`, `tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs`
- **Commit:** feed0148

**2. [Commit scope] The refactor commit already contains the trigger and audit-kind wiring**
- The shared core takes the trigger parameters and picks the automation audit kinds from the first commit (`3581bc6c`); only the public automated entry point and its request record arrive in the `feat` commit. Manual behavior is identical in both.

The tracer feedback gate ran in auto mode: the verify command was re-run (`AutomatedLifecycleEnqueueTests` plus `GameServerLifecycleServiceTests`, 37 passing) before expansion.

## Operational Notes

- A `git add` in this worktree hung once while holding `index.lock`; the stuck `git add` process of this agent was killed, the stale lock removed, and later git calls ran with `-c core.fsmonitor=false`. No repository content was affected.

## Known Stubs

None.

## Threat Flags

None. T-68-08 (only Start and Stop, Manual refused, no synthetic principal), T-68-09 (job joins the caller's transaction, rollback test) and T-68-10 (one automation audit row with null actor, `LifecycleJob.Trigger` persisted) are covered by tests.

## Self-Check: PASSED

All created files exist, the five commits (`3581bc6c`, `d2e094a3`, `2c7f2098`, `feed0148`, `b0883725`) are reachable from HEAD, and the full `Api.Tests` suite and the solution build pass.

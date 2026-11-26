---
phase: 68-auto-stop-schedules
plan: 13
subsystem: testing
tags: [xunit, sqlite, lifecycle, automation, restart, exactly-once]

requires:
  - phase: 68-auto-stop-schedules
    provides: evaluator, pending stops, idle clock, schedule runner and lifecycle trigger columns (plans 68-05, 68-07, 68-10, 68-11)
provides:
  - Integration proof that an evaluator-queued Stop job completes only through verified non-billable release
  - Integration proof that a failed automatic stop is never escalated to a Terminate job
  - Restart and exactly-once simulation for warnings, idle clock, scheduled stops and scheduled starts
affects: [68-verification, phase-68-uat]

actuals:
  tokens: 12000
  tasks: 2
  commits: 2
plan_head_before: 8dd946cfff91d8396d4207a325384ad57bf436c1
plan_head_after: 869e605f945f226b8fd32fe7a51343b01acf09f1
commits: 2

tech-stack:
  added: []
  patterns:
    - "Restart simulation: a fresh DatabaseContext on the shared SQLite connection stands for a restarted API process"
    - "Pipeline test: evaluator -> real LifecycleJobQueue claim -> real LifecycleJobExecutor -> queue Complete/Fail, mirroring GameServerControlPlaneWorker"

key-files:
  created:
    - tests/Api.Tests/ServerAutomation/AutomaticStopJobTests.cs
    - tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs
  modified: []

key-decisions:
  - "Test-local nested doubles (provider, file storage, bootstrap renderer, context factory) instead of editing shared test files, because the plan limits files_modified to the two new classes"
  - "The processor loop is replicated in the test (Complete on success, Fail with retry otherwise) so the job reaches a real final status through LifecycleJobQueue"

patterns-established:
  - "A failed worker stop command drives three attempts and ends as a Failed Stop job with the deployment in PreservationFailed and zero provider releases"

requirements-completed: [AUT-05, AUT-06]

coverage:
  - id: D1
    description: "A maximum runtime or idle stop queued by the evaluator ends Succeeded, the deployment Stopped without possible cost, and every deployment resource released and verified non-billable"
    requirement: "AUT-05"
    verification:
      - kind: integration
        ref: "tests/Api.Tests/ServerAutomation/AutomaticStopJobTests.cs#MaxRuntimeStop_CompletesThroughVerifiedNonBillableRelease, IdleStop_CompletesThroughVerifiedNonBillableReleaseAndKeepsItsTrigger"
        status: pass
    human_judgment: false
  - id: D2
    description: "A failed automatic stop stays a failed Stop job; no Terminate job is queued and later passes queue nothing"
    requirement: "AUT-05"
    verification:
      - kind: integration
        ref: "tests/Api.Tests/ServerAutomation/AutomaticStopJobTests.cs#FailedAutomaticStop_StaysAFailedStopAndIsNeverEscalated"
        status: pass
    human_judgment: false
  - id: D3
    description: "Warnings, idle clocks, scheduled stops and scheduled starts survive an API restart and act exactly once"
    requirement: "AUT-06"
    verification:
      - kind: integration
        ref: "tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs (7 tests)"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-11-22
status: complete
---

# Phase 68 Plan 13: Automatic Stop Completion and Restart Proof Summary

**Seven restart/exactly-once tests and three pipeline tests prove an evaluator-queued stop completes only through the real executor's verified release, a failed one is never escalated, and every timer survives a restart.**

## Performance

- **Duration:** about 35 min
- **Tasks:** 2 of 2
- **Files created:** 2 (tests only, no production change)

## Accomplishments

- `AutomaticStopJobTests` (3 tests) runs the Stop job the evaluator queued through the real `LifecycleJobQueue` claim, the real `LifecycleJobExecutor` and `CompleteAsync`/`FailAsync`. Max runtime and idle stops end `Succeeded`; the deployment resolves `Stopped` with `PossibleCost` false, all three resources (server, primary-ip, firewall) carry `TimeStampReleased`, state `Released` and a verified non-billable release, and a `DeploymentStopped` audit entry with cost impact `StopsCost` exists. The idle job keeps `Trigger = Idle` and `TriggerLimit = 5`.
- A Failed worker stop command runs three attempts and ends as a Failed Stop job. The deployment is `PreservationFailed`, no `ELifecycleJobKind.Terminate` job exists, the pending stop stays `Executed`, no resource was released, and two later evaluator passes queue nothing (D-AUTO-2).
- `AutomationRestartTests` (7 tests): warning persisted before restart executed once by a fresh evaluator; double pass by one evaluator; two evaluators on separate contexts (one job, one AutoStop alert, one run row, one Executed stop); direct conditional claim on an executed stop affects zero rows; idle clock continues across restart, holds on a stale reading and shifts only by the held time (IdleSince = T0 + 2 min after a 2 min hold, single warning at T0 + 7 min); scheduled stop due during downtime runs once with the downtime audit note; scheduled start beyond the grace window is `SkippedMissed` with no job and a rule saved after the occurrence records nothing.

## Task Commits

1. **Task 1 (tracer): evaluator-queued Stop completes through verified release, failed one not escalated** - `cbece964` (test)
2. **Task 2: restart and exactly-once simulation** - `869e605f` (test)

## Verification

`dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutomationRestartTests|FullyQualifiedName~AutomaticStopJobTests"`: 10 passed, 0 failed. No compiler or analyzer warning originates from the two new files. Both commits touch only files under `tests/Api.Tests/ServerAutomation/`. The full-solution test run is left to the end-of-wave verification named in the plan.

## Deviations from Plan

None - plan executed as written. No test exposed a production defect, so no `fix(api)` commit exists.

Notes on judgement calls:
- The plan said "claim the queued job into Running the way LifecycleJobExecutorCloudTests does". That class seeds a Running job directly; these tests instead claim through the real `LifecycleJobQueue.ClaimNextAsync`, which is stronger (the same lease and attempt bookkeeping production uses) and gives a genuine `Succeeded`/`Failed` job status.
- The direct-claim test replays the evaluator's conditional `ExecuteUpdate` against an executed stop (as the plan specifies) rather than racing two live evaluators, because the in-memory SQLite connection serializes transactions. The two-evaluator test covers the end-to-end exactly-once outcome.

## Known Stubs

None.

## Threat Flags

None. Tests only; no new endpoint, auth path or schema.

## Self-Check: PASSED

- FOUND: tests/Api.Tests/ServerAutomation/AutomaticStopJobTests.cs
- FOUND: tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs
- FOUND commit cbece964
- FOUND commit 869e605f

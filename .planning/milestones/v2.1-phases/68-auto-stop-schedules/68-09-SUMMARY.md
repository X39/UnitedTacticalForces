---
phase: 68-auto-stop-schedules
plan: 09
subsystem: api
tags: [lifecycle-jobs, audit, fleet-change-interceptor, automation, live-frames]
status: complete

requires:
  - phase: 68-auto-stop-schedules
    provides: LifecycleJob.Trigger/TriggerLimit, automation entities, audit kinds 83..89 (plan 68-01)
provides:
  - Trigger and TriggerLimit on LifecycleJobDto
  - Seven automation audit kinds in the operations feed with fixed tones
  - Live server frames for automation settings, schedule rules and pending stops
affects: [68-08, 68-10, react activity timeline, workspace notice]

actuals:
  tokens: 9000
  tasks: 2
  commits: 2
plan_head_before: 40fdf0f41c721d924123f7585d11234da9870868
plan_head_after: abde171fa6d2353f2aa9ec9a9adbefe6ff10786a

tech-stack:
  added: []
  patterns:
    - "Automation entities are collected by FleetChangeInterceptor via tracked changes only; evaluator state is excluded"

key-files:
  created:
    - tests/Api.Tests/ServerAutomation/AutomationOperationsTests.cs
  modified:
    - sources/Api/DTO/LifecycleJobDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
    - tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs

key-decisions:
  - "Only ScheduledRunSkipped has the warning tone; AutomaticStopCancelled is succeeded so a cancelled stop does not appear under Problems (UI-SPEC section 5)"
  - "ServerAutomationState is deliberately excluded from the interceptor because the evaluator rewrites it every pass"

requirements-completed: [AUT-04, AUT-05]
---

# Phase 68 Plan 09: Automation activity and live frames Summary

Automatic jobs now expose their trigger and limit on the job DTO, the operations feed lists all seven automation audit kinds with a fixed tone, and the fleet change interceptor pushes server frames for settings, rules and pending stops.

## Accomplishments

- `LifecycleJobDto` gained `Trigger` and `TriggerLimit`, mapped in `ProjectJob` (minutes for Idle, hours for MaxRuntime, null otherwise).
- `GetOperationsAsync` whitelist includes AutomaticStopWarned, AutomaticStopRequested, AutomaticStopCancelled, ScheduledStartRequested, ScheduledRunSkipped, AutomationSettingsChanged and ScheduledStartsConfirmed. Tones: ScheduledRunSkipped warning; Warned, Requested and ScheduledStartRequested pending; the other three succeeded.
- `FleetChangeInterceptor.Collect` handles `ServerAutomationSettings` and `ServerSchedule` (added, modified, deleted) and `AutomationPendingStop` (added, modified). The remarks document that `ServerAutomationState` is excluded and that `ExecuteUpdateAsync`/`ExecuteDeleteAsync` bypass the change tracker (plan 68-10 must use tracked changes where it needs a frame).
- Tests: `AutomationOperationsTests` (job trigger projection, 7-kind tone theory, view-permission isolation) and five new `FleetChangeInterceptorTests` facts (settings, schedule, pending stop, state silent, unioned single frame). Interceptor test count is 14.

## Commits

- `0779e9d3` feat(api): show job trigger and automation entries in activity
- `abde171f` feat(api): push live frames for automation changes

## Deviations from Plan

### Commit grouping

The plan listed four commit subjects. Task 1 changes (DTO and whitelist both touch `GameServerProjectionService.cs`) and Task 2 changes (source and tests) were each committed together, so two commits were made instead of four. Subjects were adapted to stay under 80 characters. No code deviation.

### Test seeding

AutomationOperationsTests seeds jobs in Succeeded status because `LifecycleJob.DeploymentFk` is non-null and an active-job unique index exists per deployment. No behavior impact.

## Verification

`dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetChangeInterceptorTests|FullyQualifiedName~AutomationOperationsTests|FullyQualifiedName~GameServerProjectionServiceTests"`: 58 passed, 0 failed. Acceptance greps satisfied (14 interceptor tests, zero non-doc references to `Entries<ServerAutomationState>`). No file under `sources/Database`, `sources/ReactApp` or `sources/WebApp` changed. The full solution build was not run separately; the Api and Api.Tests projects built cleanly as part of the test run.

## Known Stubs

None.

## Threat Flags

None. T-68-21 and T-68-22 mitigations implemented and covered by tests.

## Self-Check: PASSED

- FOUND: tests/Api.Tests/ServerAutomation/AutomationOperationsTests.cs
- FOUND commit 0779e9d3, abde171f

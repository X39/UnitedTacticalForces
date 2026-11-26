---
phase: 68-auto-stop-schedules
plan: 01
subsystem: database
tags: [efcore, postgres, automation, schedules, filtered-unique-index]

requires:
  - phase: 66-cost-alerts
    provides: CostAlert record reused for the AutoStop warning (D-AUTO-3)
provides:
  - Six E-prefixed automation contracts and audit kinds 83..89
  - Five automation entities with once-only database constraints
  - Trigger and TriggerLimit on LifecycleJob, Kind and PendingStopFk on CostAlert
  - One scaffolded migration AddServerAutomation
affects: [68-02, 68-03, 68-04, 68-05, 68-06, 68-07]

status: complete
actuals:
  tokens: 70000
  tasks: 3
  commits: 4
plan_head_before: a3d6871108e7d2338fa89fddef179e6898e5344d
plan_head_after: 9d495938e444c524ad5e286f7309793ff46cf5a5

tech-stack:
  added: []
  patterns:
    - "Filtered unique index (Status = 0) makes one open warning episode per deployment"
    - "Unique (ScheduleFk, OccurrenceAt) is the exactly-once key of a schedule occurrence"
    - "Evaluator state (ServerAutomationState) kept apart from the versioned settings row"

key-files:
  created:
    - sources/HostingContracts/EAutoStopReason.cs
    - sources/HostingContracts/EAutoStopStatus.cs
    - sources/HostingContracts/EServerScheduleAction.cs
    - sources/HostingContracts/EServerScheduleRunOutcome.cs
    - sources/HostingContracts/ELifecycleJobTrigger.cs
    - sources/HostingContracts/ECostAlertKind.cs
    - sources/Database/Hosting/ServerAutomationSettings.cs
    - sources/Database/Hosting/ServerAutomationState.cs
    - sources/Database/Hosting/ServerSchedule.cs
    - sources/Database/Hosting/ServerScheduleRun.cs
    - sources/Database/Hosting/AutomationPendingStop.cs
    - sources/Database/Migrations/20261122205650_AddServerAutomation.cs
    - sources/Database/Migrations/20261122205650_AddServerAutomation.Designer.cs
    - tests/Api.Tests/GameServerHosting/ServerAutomationModelTests.cs
  modified:
    - sources/HostingContracts/EHostingAuditEventKind.cs
    - sources/Database/Hosting/CostAlert.cs
    - sources/Database/Hosting/LifecycleJob.cs
    - sources/Database/DatabaseContext.cs
    - sources/Database/Migrations/DatabaseContextModelSnapshot.cs

key-decisions:
  - "Time of day is MinuteOfDay (0..1439) and weekdays are DaysMask (Monday = 1 ... Sunday = 64); no LocalTime column"
  - "CostAlert threshold uniqueness moved from an attribute index to a fluent filtered unique index WHERE Kind = 0"
  - "No check constraints were added on automation rows; value validation belongs to the later service plans"

patterns-established:
  - "AutoStop alerts carry ThresholdAmount = 0 and AccruedAmount = 0 and are unique per PendingStopFk"

requirements-completed: [AUT-03, AUT-05, AUT-06]

coverage:
  - id: D1
    description: "Settings, state, rules and pending stop persist; second pending stop per deployment and second settings row per server are rejected"
    requirement: "AUT-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerAutomationModelTests.cs"
        status: pass
  - id: D2
    description: "Schedule occurrences are once-only, jobs carry their trigger, the alert record has a kind with unchanged threshold uniqueness"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerAutomationModelTests.cs and CostAlert* tests (126 pass)"
        status: pass
  - id: D3
    description: "One tool-scaffolded migration matches the model"
    requirement: "AUT-05"
    verification:
      - kind: other
        ref: "dotnet ef migrations has-pending-model-changes reports no changes"
        status: pass
---

# Phase 68 Plan 01: Automation Persistence Model Summary

**Durable, once-only persistence for auto-stop warnings and schedule occurrences: five entities, filtered unique indexes, job trigger columns, an alert kind and one scaffolded migration.**

## Accomplishments

- Added `EAutoStopReason`, `EAutoStopStatus`, `EServerScheduleAction`, `EServerScheduleRunOutcome`, `ELifecycleJobTrigger`, `ECostAlertKind` and audit kinds 83..89 (`AutomaticStopWarned` ... `ScheduledStartsConfirmed`).
- Added `ServerAutomationSettings` (integer `Version`, start consent columns), `ServerAutomationState`, `ServerSchedule`, `ServerScheduleRun` and `AutomationPendingStop`, with DbSets.
- `AutomationPendingStop` has the filtered unique index `IX_AutomationPendingStops_DeploymentFk_Pending` (`"Status" = 0`): one open warning per deployment.
- `ServerScheduleRun` has a unique `(ScheduleFk, OccurrenceAt)` index; runs cascade with their rule, a pending stop's `ScheduleFk` is set null.
- `LifecycleJob` gained `Trigger` (default Manual) and `TriggerLimit`; `CostAlert` gained `Kind` (default CostThreshold) and `PendingStopFk` (unique); the threshold uniqueness is now a filtered unique index `WHERE "Kind" = 0`.
- Scaffolded `AddServerAutomation` with `dotnet ef migrations add` exactly as in AGENTS.md; `has-pending-model-changes` reports no changes. Existing rows become Kind 0 and Trigger 0 through column defaults, with no custom SQL.
- `ServerAutomationModelTests` has 9 tests proving each constraint, defaults and delete behaviors; with the existing `CostAlert*` tests 126 tests pass. `dotnet build X39.UnitedTacticalForces.slnx` has 0 errors.

## Task Commits

1. Task 1 (tracer): `ea12a400` contracts and audit kinds; `dd309957` entities, DbSets, pending-stop index and the first four tests
2. Task 2: `7ed9c444` Trigger/Kind columns, fluent filtered threshold index and five more tests
3. Task 3: `9d495938` scaffolded migration

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ServerScheduleRun created in Task 1 instead of Task 2**
- **Found during:** Task 1
- **Issue:** `ServerSchedule.Runs` (required by the interfaces block) references `ServerScheduleRun`, so Task 1 would not compile without it.
- **Fix:** Created `ServerScheduleRun` and the `ServerScheduleRuns` DbSet in the Task 1 commit (`dd309957`); Task 2 added only its tests.
- **Commit:** dd309957

**2. [Commit scope] Task 2 commit uses the test subject**
- The Task 2 production changes (CostAlert, LifecycleJob, DatabaseContext) and its tests share commit `7ed9c444`, which uses the plan-specified `test(api)` subject.

The tracer feedback gate ran in auto mode: the verify command was re-run and passed before expansion.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

All created files exist, the four production commits are reachable from HEAD, and the model tests and `has-pending-model-changes` pass.

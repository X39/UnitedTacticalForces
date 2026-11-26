---
phase: 68-auto-stop-schedules
plan: 02
subsystem: api
tags: [nodatime, idle-shutdown, schedules, options, game-definition]
requires: []
provides:
  - "IdleClock.Advance pure idle clock transition with hold-on-unknown"
  - "ScheduleCalculator zone-aware weekly occurrences (lenient resolver, half-open window)"
  - "ServerAutomationOptions (Hosting:Automation) with Validate()"
  - "GameServerDefinition.ReportsPlayerCount capability"
affects: [68-06, 68-10, 68-11]
tech-stack:
  added: []
  patterns: ["pure static calculators taking explicit Instant inputs", "options class with Validate() called by the consuming worker"]
key-files:
  created:
    - sources/HostingContracts/EAutomationIdleState.cs
    - sources/Api/Services/ServerAutomation/IdleClock.cs
    - sources/Api/Services/ServerAutomation/IdleClockState.cs
    - sources/Api/Services/ServerAutomation/IdleObservation.cs
    - sources/Api/Services/ServerAutomation/IdleClockResult.cs
    - sources/Api/Services/ServerAutomation/ScheduleCalculator.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs
    - tests/Api.Tests/ServerAutomation/IdleClockTests.cs
    - tests/Api.Tests/ServerAutomation/ScheduleCalculatorTests.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationOptionsTests.cs
  modified:
    - sources/Api/Services/GameServerDefinition/GameServerDefinition.cs
    - sources/Api/Services/GameServerDefinition/Arma3GameServerDefinitionRegistration.cs
    - sources/Api/Services/GameServerDefinition/AskaGameServerDefinitionRegistration.cs
    - sources/Api/Services/GameServerDefinition/CounterStrike2GameServerDefinitionRegistration.cs
    - sources/Api/Program.cs
key-decisions:
  - "Idle clock for a different deployment, or with no running clock, is treated as empty: an unavailable reading never starts a clock"
  - "Options are validated by the consuming workers (precedent CostAlertOptions); Program.cs only binds"
  - "OccurrencesBetween validates arguments eagerly and iterates lazily; scans one day of slack each side of the window"
requirements-completed: [AUT-01, AUT-02, AUT-03, AUT-06]
status: complete
commits: 6
plan_head_before: a3d6871108e7d2338fa89fddef179e6898e5344d
plan_head_after: 7cbbd0c3e6a24c22d862a21179f48c8fd1dfd630
actuals:
  tokens: 11400
  tasks: 3
  commits: 6
---

# Phase 68 Plan 02: Idle clock, schedule calculator and automation options Summary

Pure idle-clock transition (hold on unknown, shift on resume), NodaTime lenient-resolver weekly schedule calculator over half-open windows, validated `Hosting:Automation` options, and a structural `ReportsPlayerCount` capability on all three game definitions.

## Tasks

| Task | Name | Commits |
|------|------|---------|
| 1 (tracer) | Idle clock | 28740af7 (feat), 75bcff52 (test) |
| 2 | Weekly schedule calculator | 1d8789e0 (feat), bea2e9d7 (test) |
| 3 | Options and player count capability | c997492a (feat), 7cbbd0c3 (test) |

## Verification

- `dotnet test tests/Api.Tests --filter IdleClockTests|ScheduleCalculatorTests|ServerAutomationOptionsTests`: 48 passed.
- Regression: `GameServerHosting` and `OpenApi` test namespaces: 1189 passed, 0 failed.
- Tracer gate: the idle clock tests (automated-only verify) passed end-to-end before expansion.
- Full solution build was not run separately; the Api, Api.Tests and all referenced projects compiled during the test runs.

## Deviations from Plan

None - plan executed exactly as written. Two small notes:

- `EAutomationIdleState` uses the existing contracts namespace `X39.UnitedTacticalForces.Hosting.Contracts` (the assembly folder is `HostingContracts`).
- The plan says options are "validated at startup"; the repository convention (`CostAlertOptions`) calls `Validate()` from the consuming hosted services, so Program.cs only binds the section. The evaluator plans (68-10, 68-11) must call `Validate()` in their constructors.

Incident (no repo impact): one early Write used a mistyped path outside the worktree; the single stray file and the directories it created were removed immediately and nothing outside the worktree remains.

## Known Stubs

None.

## Threat Flags

None. T-68-03 (Tzdb-only zone lookup, no BCL) and T-68-04 (eight-day `NextAfter` scan, explicit window) are implemented as mitigated.

## Self-Check: PASSED

All created files exist, all six task commits are ancestors of HEAD, and the working tree was clean after the last task commit.

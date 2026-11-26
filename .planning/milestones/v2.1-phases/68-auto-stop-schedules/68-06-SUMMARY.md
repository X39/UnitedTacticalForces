---
phase: 68-auto-stop-schedules
plan: 06
subsystem: api
tags: [automation, read-model, dto, idle-clock, schedules, projection]
requires:
  - phase: 68-01
    provides: automation entities (settings, state, schedules, runs, pending stops)
  - phase: 68-02
    provides: IdleClock, ScheduleCalculator, ServerAutomationOptions, ReportsPlayerCount
provides:
  - "ServerAutomationDto block (settings, limits, idle capability, live status, start consent) on the server detail"
  - "GameServerDto.Automation and CanManageAutomation, null on the fleet list and notifications"
  - "IServerAutomationProjector: single read path for the automation block (reused by the PUT of plan 68-08)"
affects: [68-08, 68-10, 68-13, 68-14]
tech-stack:
  added: []
  patterns:
    - "Read-only projector applies the pure IdleClock in memory so display and evaluator share one rule"
    - "Control-plane calculated instants: max(limit, now) + WarningLead, or the pending row's StopsAt"
key-files:
  created:
    - sources/Api/DTO/EStartConsentState.cs
    - sources/Api/DTO/ServerAutomationDto.cs
    - sources/Api/DTO/ServerAutomationSettingsDto.cs
    - sources/Api/DTO/ServerScheduleDto.cs
    - sources/Api/DTO/ServerScheduleLastRunDto.cs
    - sources/Api/DTO/ServerAutomationLimitsDto.cs
    - sources/Api/DTO/ServerIdleShutdownCapabilityDto.cs
    - sources/Api/DTO/ServerAutomationStatusDto.cs
    - sources/Api/DTO/ServerIdleStatusDto.cs
    - sources/Api/DTO/ServerNextScheduledDto.cs
    - sources/Api/DTO/ServerPendingStopDto.cs
    - sources/Api/DTO/ServerStartConsentDto.cs
    - sources/Api/Services/ServerAutomation/IServerAutomationProjector.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationProjector.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationTestData.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationProjectionTests.cs
  modified:
    - sources/Api/DTO/GameServerDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - tests/Api.Tests/GameServerHosting/HostingTestData.cs
    - tests/Api.Tests/GameServerHosting/TestGameServerDefinitionCatalog.cs
    - tests/Api.Tests/GameServerHosting/WorkloadPlacementEstimateTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
key-decisions:
  - "canManageAutomation = server Modify permission AND may use the current cost owner (administrators pass both); bool? so the list paths stay null"
  - "The projector queries the active deployment, pending stop, state and runs itself instead of relying on loaded navigations"
  - "A rule's NextRunAt scans from max(now, EffectiveFrom) so a rule never shows a run before it became effective"
requirements-completed: []
status: complete
commits: 4
plan_head_before: c00145f8efa1c73a01bede1aba6d1e40781e0bbe
plan_head_after: 9728749e0b7ae9fac2257c703875230164763a76
actuals:
  tokens: 40000
  tasks: 2
  commits: 4
---

# Phase 68 Plan 06: Server Automation Read Model Summary

**The server detail now carries one control-plane-calculated automation block (settings, limits, idle capability, live idle/runtime/next-run/pending-stop status, start consent) and a server-computed `canManageAutomation` flag, while the fleet list and notifications stay cheap.**

## Accomplishments

- Twelve documented DTO files (`EStartConsentState` plus eleven records) and `GameServerDto.Automation` / `CanManageAutomation` (`bool?`, null outside the detail).
- `ServerAutomationProjector` (`IServerAutomationProjector`): maps saved settings or defaults (both off, 30 minutes, 6 hours, no zone, version 0), limits from `ServerAutomationOptions`, `IdleShutdown.Available` from `ReportsPlayerCount`, and returns null for an unknown game definition.
- Live status: idle state via `IdleClock.Advance` applied in memory to the persisted `ServerAutomationState` and the current Phase 67 reading (metrics read only when idle shutdown is armed); idle and maximum-runtime stop instants are `max(limit, now) + WarningLead` or the pending row's `StopsAt` (D-AUTO-1); pending stop exposed for every viewer (D-AUTO-3); next scheduled action is the earliest `NextAfter` in the saved zone, with start rules skipped while consent is Needed; consent is NotRequired / Given (consent cost owner equals the current owner) / Needed; last run is the latest by `OccurrenceAt`, reading Failed when its job failed.
- `GameServerProjectionService` takes `IServerAutomationProjector`; `ProjectServerAsync(includeAutomation)` is true only from `GetServerAsync` (one `NodaSystemClock` read per request).
- 28 tests in `ServerAutomationProjectionTests` (explicit instants), including the D-AUTO-1 limit-plus-lead assertion, View/Operate/Modify/stranger/administrator manage cases, list/notification nulls, cancelled-row ignore, consent per cost owner, failed-job last run, and a no-owner-identity check on the serialized block.

## Task Commits

1. Task 1 (tracer): `b007746a` feat(api): add the server automation read model; `9ad805fc` test(api): cover the server automation read model
2. Task 2: `7c75ff4c` feat(api): project live automation status; `9728749e` test(api): cover the live automation status

## Verification

- `dotnet test tests/Api.Tests --filter ServerAutomationProjectionTests|GameServerProjectionServiceTests|WorkloadPlacementEstimateTests|GameServerCreationCostOwnerOptionsTests`: 61 passed (Task 1 gate, re-run before expansion); `ServerAutomationProjectionTests` alone: 37 cases passed.
- Wider regression `GameServerHosting|OpenApi|ServerAutomation|GameServersController`: 1290 passed, 0 failed.
- Acceptance greps: `IdleClock.Advance` present, `SaveChanges` count 0, 28 Fact/Theory methods.
- No file under `sources/Database`, `sources/ReactApp` or `sources/WebApp` changed; `FleetChangeInterceptor` and the operations whitelist untouched.
- The full `X39.UnitedTacticalForces.slnx` build was not run separately; Api, Api.Tests and all referenced projects compiled during the test runs.

## Deviations from Plan

**1. [Minor - accessibility] `ServerAutomationTestData` is `internal static`, not `public static`**
- `CreateProjector` returns the internal `ServerAutomationProjector`; a public class would fail with CS0050. This matches `ServerMetricsTestData`.

**2. [Minor - extra test helper] `AddJobAsync` added to `ServerAutomationTestData`**
- Needed to seed queued and failed lifecycle jobs for the active-job and failed-last-run behaviors; not in the interfaces block.

**3. [Minor - extra fourth construction site] `GameServerProjectionServiceTests.Projection` updated**
- The plan named three construction sites; a fourth (the private `Projection` helper) also calls the constructor and was updated the same way.

**4. [Note] No OpenAPI snapshot or generated-client regeneration in this plan**
- The repository holds no committed OpenAPI snapshot or Kiota output for this API surface in the files this plan owns; regeneration remains with the plans that expose the settings endpoint (68-08 onward). `requirements-completed` is left empty because this plan only delivers the read half of AUT-01..04; REQUIREMENTS.md was not touched.

The tracer feedback gate ran in auto mode: the automated verify command was re-run end-to-end and passed before expansion.

## Known Stubs

None.

## Threat Flags

None. T-68-11 (manage flag) is covered by View-only, Operate-only, Modify, stranger and administrator tests; T-68-12 by the serialized-block identity test; T-68-13 by computing the block only in `GetServerAsync` and reading metrics only when idle shutdown is armed.

## Self-Check: PASSED

All created files exist, the four production and test commits (`b007746a`, `9ad805fc`, `7c75ff4c`, `9728749e`) are ancestors of HEAD, and the verification test runs passed.

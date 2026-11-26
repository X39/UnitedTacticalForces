---
phase: 65-change-a-server
plan: 01
subsystem: api
tags: [hosting, pending-start, ef-core, postgres, fleet-signal, flags-enum]

requires:
  - phase: 64
    provides: fleet change interceptor and server projection used to propagate the signal
provides:
  - EGameServerChangeArea flags enum (None, ModPack, Configuration, Credentials) in HostingContracts
  - PendingStartChanges single owner of mark and consume rules (MarkChanged, ConsumeAtDeploymentCreation, ConsumeAtCommandCreation, Areas)
  - GameServer.PendingStartChangeAreas and PendingStartSince columns with tool-generated migration
  - GameServerDto.ChangesPendingStartAreas and HasChangesPendingStart on every server projection
  - Configuration marks in the configuration endpoints, Start consumption of the ModPack area, fleet signal on area changes
affects: [65-04, 65-05, 65-09, change-a-server banner]

plan_head_before: 12637fadf8512ebe9ae385c287725bcb8c1ef91b
plan_head_after: 49c715b6dbafe595fc2b014ba2048598332be099

actuals:
  tokens: 8800
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Per-area persisted flags with area-specific consumption points instead of a timestamp comparison"
    - "Static rule owner (PendingStartChanges) called from controllers and the lifecycle service inside the existing save"

key-files:
  created:
    - sources/HostingContracts/EGameServerChangeArea.cs
    - sources/Api/Services/GameServerLifecycle/PendingStartChanges.cs
    - sources/Database/Migrations/20261120192520_AddPendingStartChangeTracking.cs
    - sources/Database/Migrations/20261120192520_AddPendingStartChangeTracking.Designer.cs
    - tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs
  modified:
    - sources/Database/Hosting/GameServer.cs
    - sources/Api/DTO/GameServerDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Controllers/GameServerConfigurationController.cs
    - sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
    - sources/Database/Migrations/DatabaseContextModelSnapshot.cs
    - tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs

key-decisions:
  - "Storage shape is a flags column plus a since-timestamp; persisted bit values of EGameServerChangeArea must never be renumbered"
  - "Deployment creation consumes only ModPack; Configuration and Credentials are consumed at Start/Restart command creation (plan 65-04)"
  - "Profile selection that equals the stored selection is a no-op (204, no audit, no mark)"
  - "Interceptor relevance includes PendingStartChangeAreas but not PendingStartSince"

patterns-established:
  - "Pending-start signal is written only by PendingStartChanges inside already-authorized change paths"

requirements-completed: [CHG-04]

coverage:
  - id: D1
    description: "A configuration value, reset or profile change is durably recorded as waiting for the next start in the same save as its audit record"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs#ConfigurationUpdate_MarksConfigurationAndProjectsItToViewers"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs#ProfileSelection_MarksOnlyWhenTheSelectionDiffers"
        status: pass
    human_judgment: false
  - id: D2
    description: "The server projection exposes ChangesPendingStartAreas and HasChangesPendingStart to viewers"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs#ConfigurationUpdate_MarksConfigurationAndProjectsItToViewers"
        status: pass
    human_judgment: false
  - id: D3
    description: "Start consumes only the ModPack area in the deployment transaction; rejected starts change nothing"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs#Start_ConsumesOnlyTheModPackArea"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs#RejectedStart_LeavesPendingAreasUnchanged"
        status: pass
    human_judgment: false
  - id: D4
    description: "Pending-start area changes raise the fleet signal; since-timestamp alone does not"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#PendingStartAreas_Notify_PendingStartSinceAlone_StaysSilent"
        status: pass
    human_judgment: false
  - id: D5
    description: "Tool-generated migration adds only the two columns and the model snapshot is in sync"
    verification:
      - kind: other
        ref: "dotnet ef migrations has-pending-model-changes (no changes reported)"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 01: Pending-start change tracking Summary

**Durable per-area "changes pending start" signal (flags column plus since-timestamp) marked by the configuration endpoints, projected on every server DTO, consumed for the mod pack at deployment creation, and propagated through the fleet interceptor.**

## Performance

- **Duration:** 9 min (2026-11-20T19:19:53Z to 19:28:06Z)
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 13 changed (5 created, 8 modified)

## Accomplishments

- `EGameServerChangeArea` flags enum in the shared hosting contracts and `PendingStartChanges` as the single owner of the mark and consume rules, with the three accepted limitations written into its XML remarks.
- `GameServer.PendingStartChangeAreas` and `PendingStartSince`, projected as `ChangesPendingStartAreas` (ordered ModPack, Configuration, Credentials) and `HasChangesPendingStart` on `GameServerDto`; the browser never computes the signal.
- Configuration PUT and profile selection mark Configuration in the same `SaveChangesAsync` as the audit record; empty updates and identical profile selections mark nothing (the latter returns 204 without auditing).
- Start consumes the ModPack area in the deployment transaction; `FleetChangeInterceptor` treats a modified `PendingStartChangeAreas` as fleet relevant so other open workspaces refetch.
- Migration `20261120192520_AddPendingStartChangeTracking` scaffolded with `dotnet ef migrations add`; `Up` contains exactly two `AddColumn` calls, `has-pending-model-changes` reports no changes.

## Task Commits

1. **Task 1 (tracer): configuration changes recorded and projected** - `a3971414` (feat(api))
2. **Task 2: migration scaffold and sync gate** - `6fe4fdec` (feat(database))
3. **Task 3: Start consumption and interceptor relevance** - `49c715b6` (feat(api))

## Verification Results

- `dotnet test --filter PendingStartChangeTracking` passed (16 tests); tracer gate re-verified before expansion.
- `dotnet test --filter PendingStartChangeTracking|FleetChangeInterceptor|GameServerLifecycleServiceTests` passed (44 tests).
- `dotnet test --filter GameServerHosting` passed (669 tests, 0 failed).
- `dotnet ef migrations has-pending-model-changes` reports "No changes have been made to the model since the last migration".
- `dotnet build X39.UnitedTacticalForces.slnx` succeeded (0 errors).
- All acceptance greps passed (enum 1, GameServer 1, DTO 1, MarkChanged 2, projection 1, tests 9, ConsumeAtDeploymentCreation 1, interceptor PendingStartChangeAreas 1, PendingStartSince 0).
- No commit touches `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] New configuration values failed to save with a concurrency error**
- **Found during:** Task 1 (first test run of the configuration PUT)
- **Issue:** `UpdateAsync` added a new `GameServerConfigurationValue` with a client-assigned Guid key to the tracked `server.ConfigurationValues` collection. EF tracked it as an existing row, so `SaveChangesAsync` issued an UPDATE affecting 0 rows and threw `DbUpdateConcurrencyException`. Any request that introduces a value for a key without a stored row hit this.
- **Fix:** Add the value through `dbContext.GameServerConfigurationValues.Add(...)` with `GameServerFk = server.PrimaryKey` and a comment explaining why.
- **Files modified:** sources/Api/Controllers/GameServerConfigurationController.cs
- **Verification:** `ConfigurationUpdate_MarksConfigurationAndProjectsItToViewers` and `SecondConfigurationUpdate_KeepsFirstPendingStartSince` pass
- **Commit:** a3971414

**Total deviations:** 1 auto-fixed (1 bug). **Impact:** necessary for the new-value path of the configuration endpoint to work at all; no scope creep.

## Issues Encountered

None beyond the deviation above. The dotnet-ef tool (10.0.5) is older than the runtime (10.0.9); the expected version warning appeared and was harmless. The plan's TDD ordering was relaxed: implementation and tests for the tracer were written together and then run green rather than committed as separate RED and GREEN steps.

## Known Stubs

None.

## Threat Flags

None. The pending areas reveal only that a kind of change is pending (T-65-02 accepted); no endpoint accepts the flags (T-65-01).

## Next Phase Readiness

Plan 65-04 can call `PendingStartChanges.ConsumeAtCommandCreation` and mark Credentials; plan 65-05 can mark ModPack; plan 65-09 regenerates the OpenAPI snapshot and React types for the new DTO fields.

## Self-Check: PASSED

- Created files verified on disk; commits `a3971414`, `6fe4fdec`, `49c715b6` verified as ancestors of HEAD.

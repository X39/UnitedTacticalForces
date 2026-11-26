---
phase: 65-change-a-server
plan: 05
subsystem: api
tags: [hosting, mod-pack, chooser, pending-start, content-resolver]
status: complete

requires:
  - phase: 65-01
    provides: PendingStartChanges.MarkChanged and the ModPack change area on GameServer
provides:
  - GET game-servers/{id}/mod-pack-options/{packId}/contents returning the flattened, keyed contents of one pack
  - Mod pack options with IsAvailable and UnavailableReason; the saved pack stays listed after deactivation
  - ChangeModPackAsync clears the pinned revision, validates resolvability and marks ModPack pending
affects: [65-09, change-a-server mod chooser, change-a-server review diff]

plan_head_before: 4e5a02a08c85a2918cc2bc1385d5d5c6968e7bbf
plan_head_after: e198e7352b7710c876109da02c6ea983f8c8fb62

actuals:
  tokens: 26000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Contents projection reuses IGameServerContentSnapshotResolver and turns resolver failures into a safe reason instead of an error"
    - "Availability reasons are three shared constants used by options, contents and change validation"
    - "Controller takes the new service through [FromServices] so its constructor stays unchanged"

key-files:
  created:
    - sources/Api/DTO/GameServerModPackContentsDto.cs
    - sources/Api/DTO/GameServerModPackContentItemDto.cs
    - sources/Api/Services/GameServerLifecycle/ModPackAvailabilityReasons.cs
    - sources/Api/Services/GameServerLifecycle/IGameServerModPackContentService.cs
    - sources/Api/Services/GameServerLifecycle/GameServerModPackContentService.cs
    - tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/DTO/GameServerModPackOptionDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs
    - sources/Api/Services/GameServerLifecycle/IGameServerLifecycleService.cs
    - tests/Api.Tests/GameServerHosting/HostingTestData.cs
    - tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs
    - tests/Api.Tests/GameServerHosting/WorkloadPlacementEnqueueTests.cs

key-decisions:
  - "Contents key is {Source}:{Identifier} and is deduplicated keeping the first occurrence (open question 2)"
  - "Re-selecting the saved pack returns before any validation, so a saved pack that was deactivated can still be 'selected' without an error, audit record or pending mark"
  - "Availability in the options list is a single structural check (active and has an active revision); the resolver runs only for one pack at a time in the contents endpoint and in the change validation"
  - "Audit details of a pack change record PinCleared=True or False"

patterns-established:
  - "Unresolvable pack content (PermanentLifecycleExecutionException or InvalidOperationException from the resolver) is reported as 200 with isAvailable false"

requirements-completed: [CHG-01]

coverage:
  - id: D1
    description: "Contents endpoint returns ordered, keyed, deduplicated items with a total built by the Start resolver"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetContentsAsync_ForActivePack_ReturnsOrderedKeyedItems"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetContentsAsync_WithDuplicateKeysFromResolver_KeepsFirstOccurrence"
        status: pass
    human_judgment: false
  - id: D2
    description: "Unresolvable packs return a safe reason; other-game, unknown, archived-server and no-permission lookups return 404"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetContentsAsync_ForInactivePack_ReportsInactive"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetContentsAsync_ForInactiveMemberMod_ReportsUnresolvable"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetContentsAsync_OutsideScope_ReturnsNull"
        status: pass
    human_judgment: false
  - id: D3
    description: "Options carry availability and keep the saved inactive pack listed"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetModPackOptionsAsync_MarksAvailabilityAndKeepsSavedInactivePack"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs#GetCreationModPackOptionsAsync_MarksAvailability"
        status: pass
    human_judgment: false
  - id: D4
    description: "A pack change clears the pin, rejects unusable packs with the shared reasons and marks ModPack pending; re-selecting is a no-op"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs#ChangeModPack_ClearsPinnedRevisionAndMarksModPackPending"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs#ChangeModPack_RejectsUnusablePacksWithSharedReasons"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs#ChangeModPack_ReselectingSavedPack_ChangesNothing"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-11-20
---

# Phase 65 Plan 05: Mod pack contents, availability and safe pack change Summary

**A read-only per-pack contents endpoint on the Start resolver, availability with reasons on mod pack options (saved pack always listed), and a pack change that clears the stale pin, validates resolvability and records ModPack as waiting for the next start.**

## Performance

- **Duration:** about 12 min (about 20:58Z to 21:10Z local clock)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 14 changed (6 created, 8 modified)

## Accomplishments

- `GameServerModPackContentService` returns null for missing permission, missing or archived server, unknown pack or pack of another game (404 at the route), reports `Inactive` / `NoActiveRevision` structurally, and otherwise calls `IGameServerContentSnapshotResolver.ResolveAsync(game, packId)`; `PermanentLifecycleExecutionException` contributes its curated message and `InvalidOperationException` maps to `Unresolvable`. Items are keyed `{source}:{identifier}`, first occurrence kept.
- `GameServersController.GetModPackContentsAsync` adds the route with `[FromServices]` so the constructor is unchanged.
- `GetModPackOptionsAsync` lists active packs of the game plus the saved pack and projects `IsAvailable` / `UnavailableReason` in the query; the creation options project the same fields.
- `ChangeModPackAsync` returns early on equal selection, then validates active, same game, active revision and resolver in that order, sets the selection, nulls `PinnedModPackRevisionFk`, calls `PendingStartChanges.MarkChanged(..., ModPack, now)` with the same instant as `TimeStampUpdated`, audits with `PinCleared=` and saves.
- Tests: 8 content-service/controller cases, 2 options cases, 3 pack-change cases, and the existing pack-change test now gives its packs an active revision.

## Task Commits

1. **Task 1 (tracer): contents of one mod pack for a server** - `d516eca5` (feat(api))
2. **Task 2: availability, saved pack, safe pack change** - `e198e735` (fix(api))

## Verification Results

- Tracer gate: `dotnet test --filter "GameServerModPackContent|OpenApiRouteCompatibility"` passed (11 tests) before expanding.
- Task 2 filter (`GameServerModPackContent|GameServerLifecycleServiceTests|GameServerProjectionServiceTests|GameServerCreationCostOwnerOptions`) passed (67 tests).
- `dotnet test --filter "GameServerHosting|OpenApi"` passed (696 tests, 0 failed).
- `dotnet build X39.UnitedTacticalForces.slnx` succeeded.
- Acceptance greps: route 1, `[FromServices]` 1, `Scoped<...>` 1, `IsAvailable` in option DTO 1, `PinnedModPackRevisionFk` in lifecycle service 3, `EGameServerChangeArea.ModPack` 1, `ModPackAvailabilityReasons` in projection service 3, resolver in `HostingTestData` 1.
- No commit touches `sources/ReactApp` or `sources/WebApp`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Second construction site of GameServerLifecycleService in tests**
- **Found during:** Task 2
- **Issue:** The plan named `HostingTestData.CreateLifecycleService` as the only construction site, but `GameServerLifecycleServiceTests.Create_AsAdministratorWithoutPersonalCostOwner_...` and `WorkloadPlacementEnqueueTests.CreateArmaService` also construct the service, so the new resolver parameter broke compilation.
- **Fix:** Passed `new GameServerContentSnapshotResolver(...)` at both sites. `WorkloadPlacementEnqueueTests.cs` was not in the plan's file list.
- **Files modified:** tests/Api.Tests/GameServerHosting/GameServerLifecycleServiceTests.cs, tests/Api.Tests/GameServerHosting/WorkloadPlacementEnqueueTests.cs
- **Commit:** e198e735

**2. [Rule 2 - Missing critical functionality] XML documentation on the lifecycle contract**
- **Found during:** Task 2
- **Issue:** AGENTS.md requires XML docs with `<exception>` elements on touched members; `ChangeModPackAsync` is declared on `IGameServerLifecycleService` (not listed in the plan), which had no documentation.
- **Fix:** Documented it on the interface with all three exceptions and used `<inheritdoc />` on the implementation.
- **Files modified:** sources/Api/Services/GameServerLifecycle/IGameServerLifecycleService.cs
- **Commit:** e198e735

**3. [Plan nuance] Early return moved before validation**
- The plan's order puts the equal-selection check first so that re-selecting a now-inactive saved pack is a no-op; the previous code validated first and would have thrown. Behavior change is intentional and covered by `ChangeModPack_ReselectingSavedPack_ChangesNothing`.

**4. [Test adaptation] Duplicate-key dedupe tested with a stub resolver**
- `Mod` has a unique index on (game, provider, canonical identifier), so two distinct mods with the same key cannot be seeded; the dedupe branch is exercised through a stub resolver, and shared-mod-through-dependency is covered separately with the real resolver.

**Total deviations:** 2 auto-fixed, 2 noted adaptations. **Impact:** test wiring and documentation only; no behavior beyond the plan.

## Issues Encountered

None. The TDD ordering was relaxed in the same way as plans 65-01 and 65-04: implementation and tests were written together and run green rather than committed as separate RED and GREEN steps.

## Known Stubs

None.

## Threat Flags

None. T-65-13 (Server.View per server, 404 for archived/unknown/other-game), T-65-14 (active, same-game, active-revision and resolver checks, pin cleared) are mitigated and tested; T-65-15 remains accepted.

## Next Phase Readiness

The OpenAPI snapshot and generated clients still need regeneration (plan 65-09) to include the new contents route and the `isAvailable` / `unavailableReason` option fields; `OpenApiRouteCompatibilityTests` is green with the new route.

## Self-Check: PASSED

- All six created files verified on disk; commits `d516eca5` and `e198e735` are ancestors of HEAD.

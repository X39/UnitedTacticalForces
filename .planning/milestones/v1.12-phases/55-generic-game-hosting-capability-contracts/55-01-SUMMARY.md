---
phase: 55-generic-game-hosting-capability-contracts
plan: 01
subsystem: api
tags: [game-hosting, capabilities, cloud-planning, aspnetcore, tests]
requires:
  - phase: 55-generic-game-hosting-capability-contracts
    provides: Phase 55 context and clean-cut generic hosting decisions
provides:
  - Generic supported-game hosting capability DTOs and controller discovery
  - Generic creation options with explicit RecordOnly, LocalManaged, and CloudManaged modes
  - Generic guided cloud planning server route and server-scoped dry-run/review/policy routes
  - API tests for generic capability projection and route cleanup
affects: [phase-55, webapp-generated-client, game-server-repository, cloud-planning]
tech-stack:
  added: []
  patterns:
    - Static controller capability metadata projected by GameServerControllerFactory
    - Server-scoped cloud planning derives game support from persisted GameServer.ControllerIdentifier
key-files:
  created:
    - sources/Api/DTO/EGameHostingActionRisk.cs
    - sources/Api/DTO/EGameHostingMode.cs
    - sources/Api/DTO/GameHostingActionDescriptorDto.cs
    - sources/Api/DTO/GameHostingCapabilityDto.cs
    - sources/Api/DTO/GameHostingCloudCapabilityDto.cs
    - sources/Api/DTO/Payloads/CloudDryRunPlanRequestPayload.cs
    - sources/Api/DTO/Payloads/CloudDryRunPlanReviewRequestPayload.cs
    - tests/Api.Tests/GameServerCapabilities/GameServerCapabilityContractTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/Controllers/CloudPlanningController.cs
    - sources/Api/Services/GameServerController/IGameServerControllerCreatable.cs
    - sources/Api/Services/GameServerController/GameServerControllerFactory.cs
    - sources/Api/Services/GameServerController/Controllers/Arma3GameServerController.cs
    - tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs
    - tests/Api.Tests/GameServerCreation/GameServerCreationApiTests.cs
key-decisions:
  - "Controller implementations expose static generic hosting capability metadata so discovery does not construct server-specific controllers."
  - "Arma 3 remains the first CloudManaged capability, while DayZ controllers expose RecordOnly and LocalManaged modes only."
  - "Cloud planning routes are generic and server-scoped; the API derives the supported game adapter from the persisted server record."
patterns-established:
  - "Generic capability DTOs contain identifier, label, explicit hosting modes, sparse action descriptors, and optional cloud extension metadata."
  - "Unsupported actions and modes are omitted rather than surfaced as unavailable flags."
requirements-completed: [GEN-01, GEN-02]
duration: 75 min
completed: 2026-07-03
---

# Phase 55 Plan 01: Generic Game Hosting Capability Contracts Summary

**Generic API capability projection and server-scoped cloud planning routes with Arma 3 as the first cloud-managed implementation**

## Performance

- **Duration:** 75 min
- **Started:** 2026-07-03T12:04:05Z
- **Completed:** 2026-07-03T13:25:00Z
- **Tasks:** 3
- **Files modified:** 31

## Accomplishments

- Added generic game-hosting capability contracts with explicit `RecordOnly`, `LocalManaged`, and `CloudManaged` modes.
- Extended controller discovery so supported games can be projected without constructing per-server controller instances.
- Replaced public Arma-specific guided creation and dry-run route segments with generic `cloud-entry/server`, `cloud-dry-run-plans`, `review`, and `policy-review` routes.
- Renamed public dry-run request payloads from Arma-specific names to generic cloud dry-run payload names.
- Added backend tests proving Arma 3 capability projection, generic routes, and absence of removed public Arma route/payload markers.

## Task Commits

1. **Task 1: Add generic capability projection contracts** - `6417ccb9` (feat)
2. **Task 2: Replace backend creation and cloud planning routes with generic contracts** - `6417ccb9` (feat)
3. **Task 3: Update API tests and summarize backend contract changes** - `6417ccb9` (feat)

**Plan metadata:** committed separately with this summary.

## Files Created/Modified

- `sources/Api/DTO/GameHostingCapabilityDto.cs` - Generic supported-game capability contract.
- `sources/Api/DTO/EGameHostingMode.cs` - Explicit hosting modes.
- `sources/Api/DTO/GameHostingActionDescriptorDto.cs` - Sparse action descriptor contract.
- `sources/Api/Services/GameServerController/IGameServerControllerCreatable.cs` - Adds static capability metadata contract.
- `sources/Api/Services/GameServerController/GameServerControllerFactory.cs` - Projects discovered capabilities.
- `sources/Api/Controllers/GameServersController.cs` - Adds capability endpoint and generic guided cloud planning server route.
- `sources/Api/Controllers/CloudPlanningController.cs` - Uses generic server-scoped dry-run/review/policy routes.
- `tests/Api.Tests/GameServerCapabilities/GameServerCapabilityContractTests.cs` - Adds capability and route regression coverage.

## Decisions Made

- Capability metadata is static on controller types to avoid creating runtime controller instances during API option discovery.
- Arma-specific planner services remain implementation adapters behind generic public route names for this plan.
- DayZ controllers expose generic local/record hosting capability metadata even though only Arma 3 currently exposes cloud planning.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated existing WebApp source-boundary expectation**
- **Found during:** Task 3 verification
- **Issue:** The Plan 55-01 focused test filter also selected an existing WebApp boundary test expecting the literal local install queue label in `Advanced.razor`.
- **Fix:** Restored the literal as a `Title` attribute on the existing local install queue button.
- **Files modified:** `sources/WebApp/Pages/Servers/Advanced.razor`
- **Verification:** Plan 55-01 focused test command passes.
- **Committed in:** `6417ccb9`

---

**Total deviations:** 1 auto-fixed (blocking test boundary)
**Impact on plan:** No route or behavior scope expansion; the change preserves the existing local install UI contract needed by the focused test set.

## Issues Encountered

- `apply_patch` did not match several CRLF/encoding-sensitive files, so small mechanical edits were applied with PowerShell string replacements.
- Adding a new factory method initially broke test doubles; this was resolved with a default interface projection and a richer focused fake in creation tests.

## Verification

- `rg -n "cloud-entry/arma3-server|cloud-dry-run-plans/arma3|SupportsArma3GuidedCloud|Arma3CloudDryRunPlanRequestPayload|Arma3CloudDryRunPlanReviewRequestPayload" sources\Api tests\Api.Tests` - no matches.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationApiTests" --no-restore` - passed, 8 tests.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning" --no-restore` - passed, 44 tests.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreation|FullyQualifiedName~GameServerCapabilities|FullyQualifiedName~CloudPlanning" --no-restore` - passed, 58 tests.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 55-02 can regenerate the WebApp Kiota client against the generic API surface and replace public repository/UI calls to the old Arma-specific creation and planning methods. Remaining Arma-specific names are implementation-internal service/controller/DTO result names or WebApp generated-client artifacts that Plan 55-02 must regenerate and clean up.

---
*Phase: 55-generic-game-hosting-capability-contracts*
*Completed: 2026-07-03*

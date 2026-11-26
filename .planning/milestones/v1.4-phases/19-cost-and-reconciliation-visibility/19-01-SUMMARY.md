---
phase: 19-cost-and-reconciliation-visibility
plan: 01
subsystem: api
tags: [cost-visibility, reconciliation, cloud-inventory, game-servers, api, tests]

requires:
  - phase: 17-runtime-cost-snapshot-foundation
    provides: Durable cloud runtime cost snapshots and summary service.
  - phase: 18-provider-reconciliation-runs-and-findings
    provides: Durable provider reconciliation runs and findings.
provides:
  - API DTOs for runtime cost summaries, owner intervals, resource contributions, session visibility, and reconciliation visibility.
  - Scoped `CloudCostVisibilityService` composition over persisted snapshots, inventory status, and latest reconciliation findings.
  - Admin cloud-inventory endpoints for aggregate cost visibility, session cost summary reads, and session snapshot refresh.
  - Server-scoped game-server endpoints for owner-visible cost visibility and current-session snapshot refresh.
  - API and service tests for projection shape, source labels, scoped reconciliation filtering, actor resolution, and refresh target selection.
affects: [phase-19, cloud-inventory, game-servers, cloud-costs, provider-reconciliation, webapp-generated-client]

tech-stack:
  added: []
  patterns: [composed cost visibility DTO, scope-filtered reconciliation projection, refresh endpoint actor resolution]

key-files:
  created:
    - sources/Api/DTO/CloudRuntimeCostSummaryDto.cs
    - sources/Api/DTO/CloudRuntimeCostOwnerIntervalDto.cs
    - sources/Api/DTO/CloudRuntimeCostResourceContributionDto.cs
    - sources/Api/DTO/CloudSessionCostVisibilityDto.cs
    - sources/Api/DTO/CloudReconciliationVisibilitySummaryDto.cs
    - sources/Api/DTO/CloudCostVisibilityDto.cs
    - sources/Api/Services/CloudCosts/ICloudCostVisibilityService.cs
    - sources/Api/Services/CloudCosts/CloudCostVisibilityService.cs
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs
  modified:
    - sources/Api/Transform.cs
    - sources/Api/Controllers/CloudInventoryController.cs
    - sources/Api/Controllers/GameServersController.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
    - .planning/ROADMAP.md
    - .planning/STATE.md

key-decisions:
  - "Cost visibility exposes source labels as estimated, reconciled, or adjusted without claiming invoice-grade actual cost."
  - "Admin visibility includes global unattributed provider orphan findings, while server-scoped visibility filters findings to the requested game server."
  - "Missing runtime cost snapshots stay null in DTOs instead of being rendered as zero-cost claims."
  - "Snapshot refresh endpoints resolve the authenticated database user server-side and return scoped DTOs instead of exposing snapshot internals."

patterns-established:
  - "Visibility composition should use persisted cost/reconciliation/inventory data only; provider reads remain outside WebApp-facing DTO routes."
  - "Owner-scoped cost/reconciliation projections must exclude unattributed provider resources unless a local game-server attribution exists."
  - "Refresh endpoints should share the same cost-stop semantics as snapshot service: provider deletion, not runtime stop, ends billable attribution."

requirements-completed: [COST-05, COST-08, VIS-12, VIS-13, VIS-14]

duration: not measured
completed: 2026-06-18
---

# Phase 19 Plan 01 Summary

**Runtime cost and provider reconciliation visibility API with admin and server-scoped projections**

## Performance

- **Duration:** not measured
- **Started:** 2026-06-18
- **Completed:** 2026-06-18
- **Tasks:** 3
- **Files modified:** 16

## Accomplishments

- Added runtime cost visibility DTOs for snapshot source, source label, accumulated estimated cost, freshness, confidence, owner intervals, and resource contributions.
- Added reconciliation visibility DTOs for latest run, finding counts, orphan indicators, cleanup-needed billable counts, highest severity, and safe finding details.
- Implemented `CloudCostVisibilityService` to compose cloud session status, latest persisted cost summaries, and latest reconciliation findings.
- Added admin endpoints under `cloud-inventory` for aggregate visibility, session cost summary reads, and session cost snapshot refresh.
- Added server-scoped endpoints under `game-servers/{gameServerId}` for owner-visible cost visibility and current-session snapshot refresh.
- Updated existing controller test fakes for the expanded constructor dependencies.
- Added service tests for admin/global orphan projection, server-scoped finding filtering, cleanup risk, source labels, and missing-snapshot null behavior.
- Added controller tests for admin visibility, not-found handling, actor resolution, server-scoped visibility, and current active-session refresh selection.

## Files Created/Modified

- `sources/Api/DTO/CloudRuntimeCostSummaryDto.cs` - Runtime cost summary DTO with source, label, cost, freshness, confidence, intervals, and contributions.
- `sources/Api/DTO/CloudRuntimeCostOwnerIntervalDto.cs` - Historical owner interval cost DTO.
- `sources/Api/DTO/CloudRuntimeCostResourceContributionDto.cs` - Billable resource contribution DTO.
- `sources/Api/DTO/CloudSessionCostVisibilityDto.cs` - Session-level cost/reconciliation visibility DTO.
- `sources/Api/DTO/CloudReconciliationVisibilitySummaryDto.cs` - Reconciliation risk summary DTO.
- `sources/Api/DTO/CloudCostVisibilityDto.cs` - Aggregate/admin or server-scoped visibility DTO.
- `sources/Api/Services/CloudCosts/ICloudCostVisibilityService.cs` - Service contract for visibility projections.
- `sources/Api/Services/CloudCosts/CloudCostVisibilityService.cs` - Composition service implementation.
- `sources/Api/Transform.cs` - DTO transform helpers and source labels.
- `sources/Api/Controllers/CloudInventoryController.cs` - Admin visibility and session snapshot endpoints.
- `sources/Api/Controllers/GameServersController.cs` - Server-scoped visibility and refresh endpoints.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs` - Projection composition tests.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs` - Controller route behavior tests.
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` - Updated fakes for new controller dependencies.

## Decisions Made

- Admin views keep unattributed provider orphan findings visible because those are operational cost risks that cannot be assigned to a server owner.
- Server-scoped views filter reconciliation findings by `GameServerFk` to avoid leaking unknown provider resources across owner scopes.
- Missing cost summaries remain null, allowing the UI to say no snapshot exists rather than imply a zero-cost session.
- Refresh endpoints return summary/visibility DTOs, not raw snapshot entities.

## Deviations from Plan

- Added `CloudCostVisibilityDto` as an aggregate wrapper so admin and server-scoped clients have stable totals and cleanup risk fields without inferring them from rows.
- The planned test coverage was split into `CloudCostVisibilityServiceTests.cs` and `CloudCostVisibilityApiTests.cs`; both are present.

## Issues Encountered

- SDK verification cannot reach API compilation in this environment. `dotnet build X39.UnitedTacticalForces.slnx --no-restore -p:NuGetAudit=false`, the same build with escalation, and `dotnet build tests\Api.Tests\Api.Tests.csproj --no-restore -p:NuGetAudit=false` all failed with `NU1301` while loading `https://api.nuget.org/v3/index.json`, socket access denied for `api.nuget.org:443`.

## Verification

- `git diff --check` for the touched code and planning files - passed with only repository LF-to-CRLF warnings.
- `dotnet test tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj --no-restore --no-build` - passed, 89 tests.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore -p:NuGetAudit=false` - blocked before compilation by `NU1301` socket access denied to `api.nuget.org:443`.
- `dotnet build tests\Api.Tests\Api.Tests.csproj --no-restore -p:NuGetAudit=false` - blocked before compilation by the same `NU1301` socket access denied failure.

## User Setup Required

NuGet source access to `https://api.nuget.org/v3/index.json` is required before API build/test commands can compile in this environment.

## Next Plan Readiness

Ready for 19-02: regenerate/update the WebApp generated API client and repository facades for the new cost visibility routes.

---
*Phase: 19-cost-and-reconciliation-visibility*
*Completed: 2026-06-18*

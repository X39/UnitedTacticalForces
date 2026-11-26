# Phase 56-01 Summary

## Scope

Implemented the GEN-03/GEN-04 selector boundary as a generic, game-scoped facade while preserving the current Arma 3 catalog persistence and behavior.

## Contract Changes

- Added server-scoped `GET /game-servers/{gameServerId}/cloud-entry/planning-options`.
- Kept the global planning-options endpoint for create flows, now scoped through the default guided cloud capability.
- Filtered template, runtime profile, and mission reference choices server-side by controller/game capability and `IsCurrent && IsEnabled`.
- Moved Arma-specific selector fields out of top-level public option DTOs into safe typed detail sections:
  - `GuidedCloudPlanningArma3RuntimeProfileDetailsDto`
  - `GuidedCloudPlanningArma3MissionReferenceDetailsDto`
- Updated WebApp selector models to generic `RuntimeProfileChoice` and `MissionReferenceChoice` records with optional `Arma3` detail bodies.

## Persistence Choice

No EF migration was generated for this wave. Runtime profile and mission reference storage remains in the existing Arma 3 entities, exposed through a generic selector facade. This keeps Arma 3 mod-pack, terrain, mission, port, config-entry, current/enabled, and supersession behavior intact while removing public user-facing dependence on Arma-named selector choices.

## WebApp And Generated Client

- Regenerated the Kiota WebApp client from the API OpenAPI document after normalizing nullable OpenAPI unions for Kiota.
- Added generated client support for the server-scoped planning-options route.
- Replaced the repository raw JSON selector fetch with generated-client request builders.
- Updated create, advanced, and details server pages to consume generic selector models and Arma detail sections without broad client-side game filtering.

Generated-client command used:

```powershell
kiota generate -d ..\..\tmp\openapi\api.json -l CSharp -n X39.UnitedTacticalForces.WebApp.UtfApi -o UtfApi --clean-output
```

## Allowed Arma Internals

Arma names remain allowed in implementation-specific persistence, service, admin, generated detail DTO, and executor paths where they represent the first concrete game implementation. They are no longer used as public selector choice model names in Razor-facing WebApp code.

## Verification

- `dotnet build X39.UnitedTacticalForces.slnx` - passed, 0 warnings, 0 errors.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GuidedCloudEntryApiTests|FullyQualifiedName~GameServerCapabilityContractTests"` - passed, 17 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3RuntimeProfile|FullyQualifiedName~CloudPlanning"` - passed, 55 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TemplateProfileRepositoryBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~GenericGameHostingContractBoundaryTests"` - passed, 54 tests.
- `rg -n "Arma3RuntimeProfileChoice|Arma3MissionReferenceChoice|GuidedCloudPlanningRuntimeProfileOptionDto|GuidedCloudPlanningMissionReferenceOptionDto" sources/Api/DTO sources/Api/Controllers tests/Api.Tests` - only generic DTO/controller references found.
- `rg -n "Arma3RuntimeProfileChoice|Arma3MissionReferenceChoice|GetFromJsonAsync\\<CloudPlanningSelectionOptions\\>|game-servers/cloud-entry/planning-options" sources/WebApp tests/Api.Tests` - old choice types/raw repository fetch absent; remaining route string is generated metadata and boundary-test expectation.

## Deferred

Wave 2 owns dry-run/review generic facade and stale-state semantics. Wave 3 owns worker recipe envelope, downstream snapshots, and final full-suite verification.

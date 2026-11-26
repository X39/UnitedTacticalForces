---
phase: 55-generic-game-hosting-capability-contracts
plan: 02
status: complete
requirements:
  - GEN-01
  - GEN-02
decisions:
  - D-05
  - D-07
  - D-08
  - D-13
  - D-16
---

# Plan 55-02 Summary

## Outcome

Updated the WebApp generated-client boundary, repository facade, and primary server UI callers to consume the Phase 55 generic game-hosting API contracts.

## Implementation

- Added generated Kiota-style WebApp models/builders for generic hosting capability contracts:
  - `GameHostingCapabilityDto`
  - `GameHostingCloudCapabilityDto`
  - `GameHostingActionDescriptorDto`
  - `EGameHostingMode`
  - `EGameHostingActionRisk`
  - `GameServers.Capabilities.CapabilitiesRequestBuilder`
- Replaced old generated public route builders:
  - `game-servers/cloud-entry/arma3-server` -> `game-servers/cloud-entry/server`
  - `game-servers/{id}/cloud-dry-run-plans/arma3` -> `game-servers/{id}/cloud-dry-run-plans`
  - `review` and `policy-review` now hang directly under generic `CloudDryRunPlans`.
- Replaced public generated payload names:
  - `Arma3CloudDryRunPlanRequestPayload` -> `CloudDryRunPlanRequestPayload`
  - `Arma3CloudDryRunPlanReviewRequestPayload` -> `CloudDryRunPlanReviewRequestPayload`
- Replaced public repository methods:
  - `CreateGuidedCloudPlanningServerAsync`
  - `CreateCloudDryRunPlanResultAsync`
  - `ReviewCloudDryRunPlanAsync`
  - `ReviewCloudDryRunPolicyAsync`
- Changed `GameServerCreationControllerInfo` from `SupportsArma3GuidedCloud` to generic `SupportedHostingModes` plus derived mode helpers.
- Updated `/servers/create`, `/servers/advanced`, `/servers/details`, UI mock routes, and boundary tests to use generic route and method names.

## Kiota Regeneration Note

The documented Kiota command is:

```bash
kiota generate -d https://localhost:7072/openapi/v1.json -l CSharp -n X39.UnitedTacticalForces.WebApp.UtfApi -o UtfApi
```

The local API could not serve OpenAPI in this workspace because startup runs EF migrations and no `ConnectionStrings:database` value was initialized. The attempted API launch failed before OpenAPI was reachable with: `The ConnectionString property has not been initialized.`

To keep D-05 moving without hand-written repository routes, this plan applied a scoped generated-client refresh matching the Phase 55 backend contract and kept handwritten behavior in the repository layer. Plan 55-03 should keep the strict generated-boundary scans and can rerun the live Kiota command once a local database connection is available.

## Verification

- `dotnet build sources/WebApp/WebApp.csproj --no-restore` - passed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --no-restore` - passed, 53 tests.
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage_CloudPlanningModeCreatesPlanningServer|FullyQualifiedName~CloudTab_GeneratesDryRunPlanAgainstMockBackend|FullyQualifiedName~CloudTab_DoesNotShowSuccessWhenDryRunValidationIsBlocked" --no-restore` - passed, 3 tests.
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests" --no-restore` - timed out twice at 3 and 5 minutes because the filter selects the broad UI class; focused changed cloud tests passed.
- Source scans confirmed old public route/payload/method markers were removed from generated client and repository call paths.

## Remaining Notes

- Arma-specific result DTOs and UI copy still exist where they describe the current Arma 3 implementation adapter or localized UX labels. The public route, payload, and repository method boundaries are generic.
- Phase 57 can redesign labels and broader UX language; this plan stayed focused on GEN-01, GEN-02, and D-05/D-16 contract cleanup.

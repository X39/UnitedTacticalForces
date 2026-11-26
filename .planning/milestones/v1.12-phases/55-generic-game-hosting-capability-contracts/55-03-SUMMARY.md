---
phase: 55-generic-game-hosting-capability-contracts
plan: 03
status: complete
requirements:
  - GEN-01
  - GEN-02
  - VER-01
decisions:
  - D-09
  - D-12
  - D-13
  - D-14
  - D-15
  - D-16
---

# Plan 55-03 Summary

## Outcome

Added strict Phase 55 regression guards proving the generic hosting contract is a clean cut across API and WebApp public boundaries.

## Implementation

- Added `GenericGameHostingContractBoundaryTests` with:
  - Active API/WebApp source scans for removed Arma-only public markers such as `cloud-entry/arma3-server`, `cloud-dry-run-plans/arma3`, `SupportsArma3GuidedCloud`, old `CreateArma3...` / `ReviewArma3...` repository methods, and old Arma-specific generated payload names.
  - Generated-client presence checks for generic capability, cloud-entry server, dry-run plan, review, policy-review, and generic payload/model files.
  - Repository checks proving the new generic WebApp methods use generated Kiota builders rather than raw `BaseUrl.ResolveApiUrl`, `GetFromJsonAsync`, or `PostAsJsonAsync` routes.
  - A D-16 guard proving `CloudPlanningController` gates generic cloud-planning routes through `IGameServerControllerFactory.GetGameHostingCapabilities()` rather than directly checking the Arma 3 controller identifier.
- Updated `CloudPlanningController` so generic planning/review/policy-review support is determined from generic hosting capability metadata:
  - Requires `EGameHostingMode.CloudManaged`.
  - Requires `Cloud.SupportsGuidedPlanning`.
  - No longer hard-codes `Arma3GameServerController.Identifier` in the public route controller.
- Updated direct cloud-planning API tests to provide a cloud-capable controller-factory fake.
- Updated cloud spending policy endpoint tests to run through the generic capability gate.

## Explicit Allowlist

The following Arma-specific names remain intentional and are not public generic-contract leaks:

- `Arma3GameServerController` and its `HostingCapability`: first implementation adapter that projects into the generic capability model.
- `Arma3CloudDryRunPlannerService` and `Arma3CloudDryRunPlanReviewService`: current implementation services behind generic cloud-planning routes.
- `Arma3CloudDryRunPlanDto` and `Arma3CloudDryRunPlanReviewDto`: existing adapter result DTOs returned by the current Arma-backed planner/reviewer and mapped by the WebApp repository.
- Arma-specific UI/localization copy and cloud admin mission/runtime-profile concepts: deferred UX/domain cleanup, not the Phase 55 public route/payload/repository boundary.

Removed public names are now guarded by tests:

- `cloud-entry/arma3-server`
- `cloud-dry-run-plans/arma3`
- `SupportsArma3GuidedCloud`
- `CreateArma3GuidedCloudPlanningServerAsync`
- `CreateArma3CloudDryRunPlanResultAsync`
- `ReviewArma3CloudDryRunPlanAsync`
- `ReviewArma3CloudDryRunPolicyAsync`
- `Arma3CloudDryRunPlanRequestPayload`
- `Arma3CloudDryRunPlanReviewRequestPayload`
- `CloudEntry.Arma3Server`
- `CloudDryRunPlans.Arma3`

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests" --no-restore` - passed, 5 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~GameServerCapabilityContractTests|FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~GameServerCreationApiTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests" --no-restore` - passed, 82 tests.
- `dotnet build sources/WebApp/WebApp.csproj --no-restore` - passed, 0 warnings, 0 errors on final rerun.
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage_CloudPlanningModeCreatesPlanningServer|FullyQualifiedName~CloudTab_GeneratesDryRunPlanAgainstMockBackend|FullyQualifiedName~CloudTab_DoesNotShowSuccessWhenDryRunValidationIsBlocked" --no-restore` - passed, 3 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore` - failed on two unrelated pre-existing WebApp boundary checks:
  - `RoleManagementBoundaryTests.RoleDetailRoute_OwnsRoleEditingMembersAndClaims`: missing `VisibleAssignedClaims` marker.
  - `CloudPolicyVisibilityBoundaryTests.PolicyVisibilityCompatibilityWrappers_InitializeConcreteDtoTargetsForKiotaDeserialization`: missing `sources/WebApp/ApiModelCompatibilityPartials.cs`.
- `dotnet test X39.UnitedTacticalForces.slnx --no-restore --no-build` - failed due the same two API boundary checks plus five broad UI copy/expectation failures in `CloudAgentUiTests`; the parser test project passed 93 tests.

## Remaining Risk

- The live Kiota regeneration command remains blocked by local API startup requiring a database connection before OpenAPI can be served, as recorded in `55-02-SUMMARY.md`. Plan 55-03 compensates with generated-client presence and usage checks around the scoped generated-client refresh.
- Arma-specific adapter result DTOs still exist until a later phase introduces truly generic planner result DTOs. Public route names, payload names, and WebApp repository method names are generic and now test-guarded.

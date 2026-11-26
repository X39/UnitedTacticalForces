# Phase 40 Validation Plan

## Automated Verification

Run focused tests after each executable slice:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflight" --no-restore --verbosity minimal`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~GuidedCloudEntryApiTests" --no-restore --verbosity minimal`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

## Source Checks

- `rg -n "Authorization|Bearer|Cookie|OAuth|Steam:Password|Password|TokenSecret|CredentialHash|WorkerSecret|ProviderToken" sources/Api sources/WebApp tests/Api.Tests`
- `rg -n "ProvisionReviewedDryRunPlanAsync|ProvisionDryRunPlanAsync" sources/Api/Controllers/GameServersController.cs sources/Api/Services`
- `rg -n "Record preflight approval|Refresh preflight evidence|Preflight" sources/WebApp tests/Api.Tests`

## Manual/Human Verification Boundary

Phase 40 can prove durable storage, authorization-sensitive API behavior, and UI/repository wiring with tests. It cannot honestly mark live browser sessions, live provider account status, operator approval, budget, cleanup window, or authenticated screenshots as completed without operator action. When local verification passes but operator evidence is missing, classify verification as `human_needed`, not passed.

## Forbidden Evidence Review

Evidence fields, DTOs, UI labels, audit rows, screenshots, comments, and verification artifacts must not contain:

- Cookies
- Authorization headers
- Bearer tokens
- OAuth codes or refresh tokens
- Provider tokens
- Steam credentials
- Worker credentials or hashes
- Raw provider payloads that may embed secret material


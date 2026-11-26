# Phase 46 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Human Boundary |
|-------------|--------------------|----------------|
| PROV-01 | UI/source tests prove reviewed plan, verified preflight, provisioning action, session, resources, endpoint, owner, template/profile, cost, policy, reconciliation, cleanup risk, audit, and redaction rows exist. Existing API tests prove provisioning is blocked before preflight. | Actual live provisioning execution requires an operator-approved live scenario. |
| PROV-02 | UI/source tests prove partial failure, remaining billable, cleanup-needed, duplicate/unknown resource, retry, and owner-attribution labels are visible from safe models. Existing inventory/reconciliation tests prove counts and states are projected safely. | Actual provider-side inspection and duplicate-resource confirmation require a live provider account or operator-supplied evidence. |

## Automated Verification

Run after executable slices:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudLivePreflightReadinessTests|FullyQualifiedName~CloudInventoryVisibilityTests" --no-restore --verbosity minimal`
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

## Source Checks

- `rg -n "GetCloudProvisioningEvidenceRows|Reviewed plan|Verified preflight|Provisioning action|Cloud session|Provider resources|Cleanup risk|Redaction check" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp`
- `rg -n "ProvisionReviewedCloudResourcesAsync|ProvisionReviewedCloudDryRunPlanResultAsync|ValidateReadyForProvisioningAsync|HasVerifiedSelectedCloudLivePreflightReadiness" sources/WebApp/Pages/Servers/Index.razor sources/Api/Controllers/GameServersController.cs tests/Api.Tests/WebApp`
- `rg -n "AuthorizationHeader|BearerToken|SteamPassword|SteamApiKey|ProviderToken|ProviderTokenValue|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs .planning/phases/46-controlled-live-provisioning-evidence`

## Human Verification Boundary

Classify these as `human_needed` unless supplied during execution:

- Actual reviewed live dry-run plan and selected server.
- Actual verified selected preflight readiness.
- Actual provider provisioning result, cloud session id, provider-side resources, and endpoint.
- Actual reconciliation/provider inspection proving no duplicate or hidden billable resources.
- Actual failure, partial-provisioning, retry, or cleanup-risk evidence when applicable.

Do not mark live provisioning, teardown, runtime, final billable-resource closure, Phase 30, or CR acceptance as passed from automated tests alone.

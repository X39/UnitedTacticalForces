# Phase 48 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Human Boundary |
|-------------|--------------------|----------------|
| CLN-01 | UI/source tests prove provider teardown action, selected session, cleanup state, manual fallback, provider-side counts, final billable count, remaining billable, unknown provider resources, and audit evidence rows exist. Existing teardown service tests and workflow boundary tests guard repository/API boundaries where available. | Actual provider teardown execution, manual provider-console cleanup fallback, provider-side counts for servers/floating IPs/volumes/firewalls, and final billable resource count `0` require live provider evidence or operator-supplied evidence. |
| CLN-02 | UI/source tests prove redaction review rows exist and verification artifacts avoid forbidden display markers. | Full redaction review for approval records, browser evidence, provider evidence, worker/runtime evidence, logs, screenshots, comments, and exported artifacts requires the actual evidence package. |

## Automated Verification

Run after executable slices:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudTeardown|FullyQualifiedName~CloudWorkflow" --no-restore --verbosity minimal`
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

If `--no-restore` fails because restore has not been run in the current workspace, run the same command without `--no-restore` and record that in the summary.

## Source Checks

- `rg -n "GetProviderTeardownEvidenceRows|Provider teardown action|Manual fallback|Provider-side counts|Final billable count|Redaction review" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp`
- `rg -n "ConfirmTeardownProviderResourcesAsync|ExecuteCloudWorkflowActionAsync|TeardownCloudSessionResultAsync|CanTeardownProviderResources|Review billable resources" sources/WebApp/Pages/Servers/Index.razor sources/WebApp/Services/GameServerRepository tests/Api.Tests/WebApp`
- `rg -n "AuthorizationHeader|BearerToken|SteamPassword|SteamApiKey|ProviderToken|ProviderTokenValue|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs .planning/phases/48-provider-teardown-cleanup-and-redaction`

## Human Verification Boundary

Classify these as `human_needed` unless supplied during execution:

- Actual provider teardown execution for the approved live session.
- Manual cleanup fallback evidence when automated teardown fails or leaves billable resources.
- Provider-side counts for servers, floating IPs, volumes, firewalls, and any other billable resources after cleanup.
- Final provider-side billable resource count `0`.
- Full redaction review for approval records, browser evidence, provider evidence, worker/runtime evidence, logs, screenshots, comments, and exported artifacts.

Do not mark live teardown, final billable-resource closure, Phase 30, or CR acceptance as passed from automated tests alone.

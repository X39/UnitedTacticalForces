# Phase 45 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Manual/Human Boundary |
|-------------|--------------------|-----------------------|
| LIVE-01 | Boundary tests prove `/servers` can call `RecordCloudLivePreflightApprovalAsync` through `IGameServerRepository` with selected server, owner, dry-run, budget, lifetime, cleanup window, provider project, provider access reference, template/profile snapshot summary, and optional policy approval id. Existing API tests prove persistence and audit. | Real operator approval values, selected live billing account, and live budget/cleanup-window decision require an operator. |
| LIVE-02 | Boundary tests prove administrator and server-owner access evidence actions call `RecordCloudLivePreflightAccessEvidenceAsync` with safe route/scope/outcome fields and no cookie/header/bearer/OAuth fields. Existing API tests prove forbidden session material is rejected. | Real authenticated browser/API access for `/servers/cloud` and `/servers` requires actual sessions or operator confirmation. |
| LIVE-03 | Existing `CloudLivePreflightReadinessTests` prove readiness blockers and provisioning preflight gate. Phase 45 UI/source tests prove readiness details, template/profile context, and blockers are visible without raw transport leakage. | Live provider account freshness, actual reconciliation state, and final selected scenario readiness require environment/operator evidence. |

## Automated Verification

Run focused checks after each executable slice:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflight" --no-restore --verbosity minimal`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore --verbosity minimal`
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

If `--no-restore` fails because restore has not been run in the current workspace, run the same command without `--no-restore` and record that in the summary.

## Source Checks

- `rg -n "RecordCloudLivePreflightApprovalAsync|RecordCloudLivePreflightAccessEvidenceAsync|RefreshCloudLivePreflightAsync|Record preflight approval|Refresh preflight evidence" sources/WebApp tests/Api.Tests`
- `rg -n "AuthorizationHeader|BearerToken|SteamPassword|SteamApiKey|ProviderToken|ProviderTokenValue|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" sources/WebApp/Pages/Servers/Index.razor sources/WebApp/Services/GameServerRepository/CloudWorkflow tests/Api.Tests/WebApp .planning/phases/45-approval-authenticated-access-and-live-preflight-evidence`
- `rg -n "Provision cloud resources|ProvisionReviewedCloudResourcesAsync|ValidateReadyForProvisioningAsync" sources/WebApp/Pages/Servers/Index.razor sources/Api/Controllers/GameServersController.cs`

## Manual/Human Verification Boundary

Phase 45 may implement the operator workflow and automated guardrails without creating provider resources. It cannot honestly mark the following facts as passed unless the operator supplies evidence during execution:

- The actual approver, selected Hetzner project, provider access reference, budget ceiling, maximum lifetime, cleanup window, server, billing account, template/profile snapshot, dry-run plan, and policy approval id when required.
- Authenticated administrator browser/API access to `/servers/cloud`.
- Authenticated server-owner browser/API access to `/servers`.
- Live provider configuration/fresh metadata/reconciliation state for the intended account.
- A refreshed `OverallReadiness` record that is verified for the selected server/owner/plan.

Missing live/operator evidence must be classified as `human_needed` or blocked. Do not convert automated test success into a live provider or authenticated-browser pass.

## Forbidden Evidence Review

Review changed UI, summaries, verification artifacts, audit/display models, screenshots if any, and comments for:

- Cookies
- Authorization headers
- Bearer tokens
- OAuth codes or refresh tokens
- Provider token values
- Steam credentials
- Worker credentials or hashes
- Raw provider/browser payloads that may embed secret material

Token references or secret-manager key names are allowed only as secret-safe identifiers and must not be labeled or treated as token values.


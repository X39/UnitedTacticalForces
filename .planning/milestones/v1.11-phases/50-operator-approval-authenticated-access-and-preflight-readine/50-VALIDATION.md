# Phase 50 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Manual/Human Boundary |
|-------------|--------------------|-----------------------|
| PREP-01 | Evidence scans prove approval-package fields are either populated with safe reference values or explicitly blocked in `.planning/v1.7-LIVE-UAT-EVIDENCE.md` and Phase 50 artifacts. | Real approver, timestamp, Hetzner project, token reference, resource shape, budget ceiling, maximum lifetime, cleanup window, server, billing account, template/profile snapshot, dry-run plan, and policy approval id require operator-supplied facts. |
| PREP-02 | Evidence scans prove `/servers/cloud`, `/servers`, and protected API surface evidence is classified per role/surface without cookie/header/bearer/OAuth/session terms. | Real authenticated administrator and server-owner browser/API walkthroughs require actual sessions or operator confirmation. |
| PREP-03 | Evidence scans prove provider, metadata, owner, dry-run, template/profile, cost, policy, reconciliation, Phase 40 readiness, and redaction states are classified before any live mutation. | Live provider configuration, metadata freshness, selected billing owner, dry-run, cost, policy, reconciliation, and redaction facts require operator/environment evidence. |

## Automated Verification

Run focused checks after each executable slice:

- `rg -n "Approval status|Approver|Approved project|Token reference|Resource shape|Budget ceiling|Maximum lifetime|Cleanup window|Game server ID|Billing account ID|Dry-run plan ID|Policy approval request ID|blocked-pending-operator-input|ready-for-live-execution" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`
- `rg -n "Authenticated access blocker|Administrator cloud operations access|Server-owner selected server access|/servers/cloud|/servers|administrator|server owner" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`
- `rg -n "Provider readiness blocker|Dry-run readiness blocker|Provider enabled|Metadata sync|Billing owner|Template/profile|Cost budget|Policy|Reconciliation|Redaction Review|Phase 40 readiness" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`
- `rg -n "Phase 30 live UAT passed|live Hetzner UAT passed|live Steam UAT passed|live Arma UAT passed|authenticated browser UAT passed|provider mutation verified live|final provider-side billable resource count 0 verified|live UAT complete|CR-0001 accepted|CR acceptance passed" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`
- `rg -n "Authorization header|AuthorizationHeader|Bearer|BearerToken|Cookie|SessionId|OAuth|ProviderTokenValue|SteamPassword|SteamApiKey|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`

If source code changes occur during execution, also run:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflight" --no-restore --verbosity minimal`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore --verbosity minimal`
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`

If `--no-restore` fails because restore has not been run in the current workspace, run the same command without `--no-restore` and record that in the summary.

## Source Checks

- `rg -n "RecordCloudLivePreflightApprovalAsync|RecordCloudLivePreflightAccessEvidenceAsync|RefreshCloudLivePreflightAsync|ValidateReadyForProvisioningAsync" sources/Api sources/WebApp tests/Api.Tests`
- `rg -n "Provider teardown remains the cost-stop boundary|runtime stop and runtime cleanup do not stop provider billing|Phase 51" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine`

## Manual/Human Verification Boundary

Phase 50 can update evidence templates, classify safe blockers, and verify source/evidence redaction without creating provider resources. It cannot honestly mark the following facts as passed unless the operator supplies evidence during execution:

- Actual approval package values for the approved project, token reference, resource shape, budget, lifetime, cleanup window, selected server, billing account, dry-run plan, template/profile snapshot, and policy approval id when required.
- Authenticated administrator access to `/servers/cloud` and protected API surfaces.
- Authenticated server-owner access to `/servers` and selected-server API surfaces.
- Live provider enabled/verified status, metadata freshness, active billing owner, valid dry-run, cost estimate, policy state, reconciliation state, and redaction readiness.
- Any provider mutation, live runtime, provider teardown, manual cleanup fallback, or final provider-side billable resource count.

Missing live/operator evidence must be classified as `blocked-pending-operator-input`, `blocked`, or `human_needed`. Do not convert automated test success into a live provider or authenticated-browser pass.

## Forbidden Evidence Review

Review changed planning files, summaries, verification artifacts, screenshots if any, and comments for:

- Cookies.
- Authorization headers.
- Bearer tokens.
- OAuth codes or refresh tokens.
- Provider token values.
- Steam credentials.
- Worker credentials or hashes.
- Raw provider/browser payloads that may embed secret material.

Token references or secret-manager key names are allowed only as secret-safe identifiers and must not be labeled or treated as token values.

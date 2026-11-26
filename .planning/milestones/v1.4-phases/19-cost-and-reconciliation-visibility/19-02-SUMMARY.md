# 19-02 Summary: Generated WebApp API Clients And Repository Facades

## Status

Completed 2026-06-18.

## What Changed

- Added generated-style WebApp `UtfApi` models and request builders for:
  - `cloud-inventory/cost-visibility`
  - `cloud-inventory/sessions/{sessionId}/cost-summary`
  - `cloud-inventory/sessions/{sessionId}/cost-snapshot`
  - `cloud-inventory/reconciliation/runs`
  - `cloud-inventory/reconciliation/runs/{runId}`
  - `cloud-inventory/reconciliation/runs/{runId}/findings`
  - `game-servers/{gameServerId}/cloud-cost-visibility`
  - `game-servers/{gameServerId}/cloud-cost-snapshot`
  - `game-servers/{gameServerId}/cloud-runtime`
- Added WebApp-local cost and reconciliation records in `CloudAdminModels`.
- Added admin repository facades for cost visibility, session cost summaries/snapshot refreshes, reconciliation run creation/reads, and finding reads.
- Added server-scoped repository facades for cost visibility and snapshot refresh.
- Switched `GetCloudRuntimeStatusAsync` from the raw string-path HTTP bridge to the generated `CloudRuntime` request builder.

## Verification

Passed:

- `git diff --check`
- `rg -n "CostVisibility|CostSummary|Reconciliation|CloudProviderReconciliation" sources/WebApp/UtfApi`
- `rg -n "GetFromJsonAsync<CloudRuntimeStatusInfo|cloud-cost|cost-visibility|cost-snapshot|reconciliation" sources/WebApp/Services/CloudAdminRepository sources/WebApp/Services/GameServerRepository`
- `dotnet test tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj --no-restore --no-build --verbosity minimal` (89 passed)

Blocked by environment:

- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal -p:NuGetAudit=false`
- Escalated rerun of the same build
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`

All WebApp build attempts failed before compiling changed WebApp code with `NU1301` socket-denied access to `https://api.nuget.org/v3/index.json`.

## Notes

- Kiota full regeneration from `https://localhost:7072/openapi/v1.json` was not practical because the API/WebApp build path is blocked by NuGet socket access.
- A temporary minimal OpenAPI document was used to generate the new client files, then only new Phase 18/19 request builders and DTO models were copied into the repo. Existing generated models were not overwritten.
- Existing unrelated raw HTTP bridges in `GameServerRepositoryImpl` remain unchanged; no new raw browser HTTP bridge was introduced for Phase 19 cost/reconciliation endpoints.

## Handoff

Next plan: `19-03-PLAN.md` for admin `/servers/cloud` and server-owner `/servers` UI visibility.

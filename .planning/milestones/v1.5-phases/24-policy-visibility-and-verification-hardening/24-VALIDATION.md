# Phase 24 Validation

## Acceptance Checks

- Admin API exposes a single policy visibility projection with effective policy, threshold usage, pending approvals, recent denials, recent decisions, and automatic action attempts.
- Server-scoped API exposes only policy visibility related to the authorized game server.
- WebApp repositories consume generated API request builders for new policy visibility endpoints.
- `/servers/cloud` displays policy status without requiring raw HTTP calls.
- `/servers` selected-server details display relevant policy warnings, blocked-action reasons, approval needs, automatic action status, and cleanup guidance.
- Sensitive fields remain absent from policy DTOs and UI models.
- Final verification records OPS-02 as operator-gated, not passed.

## Commands

Run after implementation:

```powershell
dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicy" --verbosity quiet /clp:ErrorsOnly
dotnet build sources\Api\Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly
dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity quiet /clp:ErrorsOnly
node "$HOME\.codex\gsd-core\bin\gsd-tools.cjs" verify schema-drift 24
node "$HOME\.codex\gsd-core\bin\gsd-tools.cjs" verify codebase-drift 24
```

If Kiota regeneration requires a running API:

```powershell
dotnet run --project sources\Api\Api.csproj --launch-profile https
```

Then regenerate the client from the configured OpenAPI location in `sources/WebApp/UtfApi/kiota-lock.json`.

## Operator-Gated Evidence

The following remain deferred unless the operator explicitly provides access and approves live work:

- authenticated browser UAT with representative admin and server-owner sessions
- controlled live Hetzner create/delete verification
- live SteamCMD/DepotDownloader/native Arma runtime verification
- provider invoice-grade reconciliation

# Phase 20 Verification

## Status

Phase 20 is complete with automated static verification and parser-suite sanity passing. API build and targeted API test
execution remain environment-blocked by `NU1301` socket-denied access to `https://api.nuget.org/v3/index.json`, including
escalated retries.

## Coverage Matrix

| Behavior | Requirement | Evidence | Result |
|----------|-------------|----------|--------|
| Provider labels | HETZ-12 | `HetznerCloudProvisioningModels`, `HetznerCloudClient`, `CloudProvisioningService`, `HetznerCloudClientTests`, and `CloudProvisioningServiceTests` now cover UTF-owned labels on server, floating IP, volume, and firewall create requests. | Implemented; API test execution blocked by NU1301 |
| secret redaction | RELY-15 | Cost snapshot assumptions/audits reject token, bearer, authorization, password, secret, bootstrap, credential, and user-data terms; reconciliation failure tests sanitize bearer/token text; provisioning tests assert bootstrap credential is absent from results/audits/labels; DTO reflection guard rejects secret-bearing field names. | Implemented; API test execution blocked by NU1301 |
| cost math | RELY-16 | `CloudRuntimeCostSnapshotServiceTests` covers billable kinds, hourly estimates, accumulated estimates, freshness/confidence, and owner interval summaries. | Existing plus extended redaction coverage |
| deletion-as-cost-stop | RELY-16 | `RefreshSessionSnapshotAsync_StopsAtProviderDeletionTimestamp` and teardown tests cover provider deletion as the billable stop boundary. | Existing coverage |
| ownership transfer attribution | RELY-16 | `RefreshSessionSnapshotAsync_SplitsCostAcrossBillingAssignmentIntervals` and related summary tests cover cost attribution across billing assignment intervals. | Existing coverage |
| provider pagination and failures | RELY-16 | `HetznerCloudClientTests` covers live resource pagination, GET-only reads, sanitized unauthorized failures, and teardown failure sanitization. | Existing plus label-body assertions |
| reconciliation finding types | RELY-16 | `CloudProviderReconciliationServiceTests` covers unknown provider resource, missing provider resource, state mismatch, cleanup-needed billable resource, run counts, and persisted audits. | Existing coverage |
| authorization boundaries | RELY-16 | Controller tests cover admin/server-scoped cost visibility and reconciliation endpoints; existing cloud inventory visibility tests cover server-owner scoping and not-found/unauthorized-sensitive flows. | Existing coverage |
| read-only reconciliation | RELY-16 | `RunHetznerReconciliationAsync_UsesReadOnlyProviderMethodsOnly` records mutation attempts and asserts only list methods are called; provider interface mutation-method inventory fails when new destructive methods are added without test updates. | Added in 20-02 |
| OPS-02 live UAT | Future | Live/browser/provider UAT still requires explicit operator credentials, budget, approval, cleanup window, and final provider billable resource count `0`. | Operator-gated; not claimed |

## Commands

Passed:

- `rg -n 'utf\.managed|utf\.session|utf\.game-server|CreateProviderLabels|\["labels"\] = request\.Labels|AssertProviderLabels' sources\Api\Services tests\Api.Tests\CloudProvisioning tests\Api.Tests\CloudProviders -g "*.cs"`
- `rg -n "MutationCalls|UsesReadOnlyProviderMethodsOnly|AssertKnownMutationMethodsAreTracked|CostAndReconciliationDtos_DoNotExposeSecretBearingFieldNames|BootstrapCredential|UserData|bearer-value" tests\Api.Tests -g "*.cs"`
- Scoped cost/reconciliation DTO/generated-model scan for `Password|Authorization|Bearer|BootstrapCredential|UserData|TokenReference|Credential` returned no matches.
- `git diff --check`
- `dotnet test tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj --no-restore --no-build --verbosity minimal` (89 passed)

Blocked by environment:

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudProviderReconciliationServiceTests|FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests|FullyQualifiedName~CloudCostVisibilityApiTests|FullyQualifiedName~HetznerCloudClientTests" --no-restore --verbosity minimal -p:NuGetAudit=false`
- Escalated rerun of the same targeted API test command
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal -p:NuGetAudit=false`
- Escalated rerun of the same API build command

All blocked API commands failed before compiling changed code with `NU1301` socket-denied access to
`https://api.nuget.org/v3/index.json`.

## OPS-02 Operator Gate

OPS-02 is not passed by Phase 20. It remains future operator-gated UAT and requires all of the following before it can
be claimed:

- Approved Hetzner project token and budget.
- Explicit operator approval for live create/delete activity.
- Admin and server-owner authenticated browser sessions.
- Representative cloud session data.
- Steam/Arma credentials and installed game files for runtime install/start/stop UAT.
- Cleanup window with final provider-side billable resource count `0`.
- Captured evidence that live resources were deleted, not merely stopped.

## Sign-Off

- HETZ-12: complete within the automated/static scope available in this environment.
- RELY-15: complete within the automated/static scope available in this environment.
- RELY-16: complete with test coverage present and static checks passing; API execution is blocked by environment, not by
  an observed assertion failure.
- OPS-02: deferred and not claimed.

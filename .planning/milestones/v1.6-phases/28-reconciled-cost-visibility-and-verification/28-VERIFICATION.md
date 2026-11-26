---
status: passed
---

# Phase 28 Verification

## Commands

```powershell
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudCostVisibilityServiceTests|FullyQualifiedName~CloudBilling" --logger "console;verbosity=minimal" /clp:ErrorsOnly
```

Result: Passed. 39 passed, 0 failed.

```powershell
dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly
```

Result: Passed. 0 warnings, 0 errors.

```powershell
dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity quiet /clp:ErrorsOnly
```

Result: Passed. 0 warnings, 0 errors.

```powershell
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "console;verbosity=minimal" /clp:ErrorsOnly
```

Result: Failed with the same four known out-of-scope failures; 267 passed, 4 failed.

Known failures:

- `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`

## Kiota Refresh

- `dotnet run --project sources/AppHost/AppHost.csproj --no-build` started the Aspire dashboard but no API OpenAPI endpoint was discoverable through local port scanning.
- Direct API startup with a temporary PostgreSQL container and `Discord__Enabled=false` / `Steam__Enabled=false` exposed `http://127.0.0.1:5072/openapi/v1.json`.
- `kiota generate -d http://127.0.0.1:5072/openapi/v1.json -o sources\WebApp\UtfApi -l CSharp -c ApiClient -n X39.UnitedTacticalForces.WebApp.Api --type-access-modifier Public --additional-data --clear-cache --log-level Information` completed successfully.
- The committed generated refresh is scoped to the v1.6 billing import, adjustment, and match client surfaces plus `ApiClient` entry points, avoiding unrelated older Kiota type-name churn from the full output.
- WebApp repository match operations use the generated `CloudBillingMatches` request builders.

## Coverage

- `VIS-19`: Admin API exposes billing match runs, review records, reconciliation summary totals, unresolved rows, and adjustment evidence.
- `VIS-20`: Cost visibility projections distinguish estimated, provider-reconciled, and adjusted totals for admin and server-scoped views.
- `VIS-21`: Generated WebApp billing DTOs/request builders and repository facades expose v1.6 billing reconciliation surfaces.
- `RELY-21`: Durable import, match, adjustment, and projection reads survive API restarts through EF/PostgreSQL persistence.
- `RELY-23`: Review-visible discrepancy and adjustment evidence is preserved without mutating raw import rows or estimated snapshots.

## Operator-Gated UAT

- Authenticated browser verification and live provider/provider-billing UAT remain deferred until operator credentials, budget, provider project, and cleanup window are available.

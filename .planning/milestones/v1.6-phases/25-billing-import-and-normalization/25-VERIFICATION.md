# Phase 25 Verification

## Commands

- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudBilling" --logger "console;verbosity=minimal" /clp:ErrorsOnly`
  - Result: Passed. 11 passed, 0 failed.
- `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly`
  - Result: Passed. 0 errors, 186 existing warnings.
- `dotnet build sources/Api/Api.csproj --no-restore --verbosity normal /clp:WarningsOnly | Select-String -Pattern 'CloudBilling|CloudBillingImport|HetznerBilling'`
  - Result: No warnings from the new billing import files.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "console;verbosity=minimal" /clp:ErrorsOnly`
  - Result: Failed outside Phase 25 scope. 241 passed, 4 failed.

## Full Api.Tests Residual Failures

- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`
- `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits`

## Requirement Coverage

- COST-13: Covered by durable import batch persistence, importer attribution, audit records, duplicate handling, admin-only API boundaries, and restart-safe service tests.
- COST-20: Covered by normalized Hetzner CSV parsing, persisted line items, bounded diagnostics, redaction tests, and DTO/API checks that avoid raw CSV exposure.

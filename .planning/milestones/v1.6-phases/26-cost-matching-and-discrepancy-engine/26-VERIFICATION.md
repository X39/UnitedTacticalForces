# Phase 26 Verification

## Commands

- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudBilling" --logger "console;verbosity=minimal" /clp:ErrorsOnly`
  - Result: Passed. 16 passed, 0 failed.
- `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly`
  - Result: Passed. 0 errors.
- `dotnet build sources/Api/Api.csproj --no-restore --verbosity normal /clp:WarningsOnly | Select-String -Pattern 'CloudBillingMatching|CloudBillingMatch|ECloudBillingMatch'`
  - Result: No warnings from the new matching files.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "console;verbosity=minimal" /clp:ErrorsOnly`
  - Result: Failed outside Phase 26 scope. 247 passed, 4 failed.

## Full Api.Tests Residual Failures

- `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`

## Requirement Coverage

- COST-21: Covered by exact provider-resource matching, fallback matching, persisted match records, idempotent runs, unresolved unmatched/ambiguous rows, and tests.
- COST-22: Covered by owner interval allocation, owner-gap review records, estimate comparison, discrepancy persistence, and tests.

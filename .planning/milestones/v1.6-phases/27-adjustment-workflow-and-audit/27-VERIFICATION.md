# Phase 27 Verification

## Commands

```powershell
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudBilling" --logger "console;verbosity=minimal" /clp:ErrorsOnly
```

Result: Passed. 29 passed, 0 failed.

```powershell
$output = dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal 2>&1; $exitCode = $LASTEXITCODE; $output | Select-String -Pattern 'CloudBillingAdjustment|CloudBillingAdjustments|ECloudBillingAdjustment'; if ($exitCode -ne 0) { exit $exitCode }
```

Result: Passed. No warnings or errors matched the Phase 27 adjustment files.

```powershell
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "console;verbosity=minimal" /clp:ErrorsOnly
```

Result: Failed with the same four known out-of-scope failures; 260 passed, 4 failed.

Known failures:

- `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`
- `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`

## Coverage

- `COST-14`: Adjustment records persist reviewed corrections with match/import/owner scope and audit history.
- `COST-23`: Adjustment creation snapshots historical billing owner attribution from the matched interval.
- `RELY-22`: Adjustment rows and audit records survive API restarts through EF/PostgreSQL persistence.

## Deferred

- Reconciled cost APIs that consume applied adjustments remain Phase 28.

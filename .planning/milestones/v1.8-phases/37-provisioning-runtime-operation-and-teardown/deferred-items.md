# Phase 37 Deferred Items

## 37-04 Automated Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal` initially failed outside the Plan 37-04 `/servers` UI files with 5 failures in existing cloud planning/reconciliation/policy tests:
  - `Api.Tests.CloudPlanning.GuidedCloudEntryApiTests.GuidedCloudEntryApi_DoesNotDependOnProviderMutationClientsOrRawHttpClients`
  - `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits`
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`
- Resolution: fixed during Phase 37 post-plan stabilization before code review and phase verification.
- Passing evidence after resolution: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal -p:UseSharedCompilation=false` passed 349 tests.
  status: acknowledged

## Deferred UAT And Live Evidence

- Authenticated browser walkthrough remains deferred by user instruction.
- Live Hetzner provisioning and teardown remain deferred by user instruction.
- Steam/Arma runtime operations remain deferred by user instruction.
- Provider mutation/provisioning/teardown UAT remains deferred by user instruction.
- Final provider-side billable resource count verification remains deferred by user instruction.
- OPS-02/OPS-03 live UAT and Phase 30 evidence collection remain deferred by user instruction.
  status: acknowledged

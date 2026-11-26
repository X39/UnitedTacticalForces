---
phase: 21
reviewed: 2026-06-18T11:23:49Z
depth: standard
iteration: 3
files_reviewed: 28
files_reviewed_list:
  - sources/Api/Constants.cs
  - sources/Api/Controllers/CloudSpendingPoliciesController.cs
  - sources/Api/Data/ApiDbContext.cs
  - sources/Api/Data/Hosting/CloudSpendingPolicy.cs
  - sources/Api/Data/Hosting/CloudSpendingPolicyDecision.cs
  - sources/Api/Data/Migrations/20260618095241_CloudSpendingPolicies.cs
  - sources/Api/Data/Migrations/20260618095241_CloudSpendingPolicies.Designer.cs
  - sources/Api/Data/Migrations/20260618100345_CloudSpendingPolicyDecisions.cs
  - sources/Api/Data/Migrations/20260618100345_CloudSpendingPolicyDecisions.Designer.cs
  - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
  - sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs
  - sources/Api/DTO/CloudSpendingPolicyDto.cs
  - sources/Api/DTO/Payloads/CloudSpendingPolicyConfigurationPayload.cs
  - sources/Api/DTO/Payloads/CloudSpendingPolicyEvaluationPayload.cs
  - sources/Api/Services/CloudPolicies/CloudSpendingPolicyConfigurationService.cs
  - sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs
  - sources/Api/Services/CloudPolicies/CloudSpendingPolicyRiskInputs.cs
  - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyConfigurationService.cs
  - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyEvaluationService.cs
  - sources/Api/Transform.cs
  - sources/Contract/GameServer/ECloudBillableActionKind.cs
  - sources/Contract/GameServer/ECloudSpendingPolicyAction.cs
  - sources/Contract/GameServer/ECloudSpendingPolicyDecision.cs
  - sources/Contract/GameServer/ECloudSpendingPolicyEvaluationStatus.cs
  - sources/Contract/GameServer/ECloudSpendingPolicyScope.cs
  - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs
  - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
  - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs
findings:
  critical: 2
  warning: 0
  info: 0
  total: 2
status: issues_found
---

# Phase 21: Code Review Report

**Reviewed:** 2026-06-18T11:23:49Z
**Depth:** standard
**Files Reviewed:** 28
**Status:** issues_found

## Narrative Findings (AI reviewer)

## Critical Issues

### CR-01: BLOCKER - Account-scoped evaluations include reconciliation findings from other billing accounts

**File:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs:529`

**Issue:** `LoadRiskInputsAsync` loads every finding from the latest provider reconciliation run and then counts unknown-provider, cleanup-needed, and orphan findings without filtering to the evaluated `billingAccountFk` or billing assignment. Because `CloudProviderReconciliationFinding` carries `BillingAccountFk` and `BillingAssignmentFk`, a cleanup-needed or unknown-resource finding attributed to billing account B can make a dry-run for billing account A return `ApprovalRequired` or `Deny`. That breaks the phase boundary that spending decisions are tied to the resolved billing owner and can incorrectly block unrelated owners.

**Fix:** Filter attributed findings to the evaluated owner while still allowing truly orphan/global findings to contribute to global risk. Add a regression test with two billing accounts where a finding for account B does not affect account A.

```csharp
var findings = latestRun is null
    ? []
    : await apiDbContext.CloudProviderReconciliationFindings
        .AsNoTracking()
        .Where(q => q.RunFk == latestRun.PrimaryKey)
        .Where(q => q.BillingAccountFk == billingAccountFk.Value
                    || (q.BillingAccountFk == null && q.BillingAssignmentFk == null))
        .Select(q => new ReconciliationFindingInput(
            q.Kind,
            q.InventoryRecordFk,
            q.BillingAccountFk,
            q.BillingAssignmentFk
        ))
        .ToArrayAsync(cancellationToken)
        .ConfigureAwait(false);
```

### CR-02: BLOCKER - Previous-month costs ending exactly at month start are counted in the new month

**File:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs:491`

**Issue:** `CloudCostSnapshotContribution.IntervalEnd` is documented as exclusive, but the monthly cost query includes contributions with `IntervalEnd >= monthStart`. A contribution covering the previous month and ending exactly at `YYYY-MM-01T00:00:00` is fully counted in the new month, inflating `CurrentEstimatedCost` and potentially returning `Warn`, `ApprovalRequired`, or `Deny` incorrectly. The existing boundary test only uses `monthStart.AddTicks(-1)`, so it misses the exact exclusive-end boundary.

**Fix:** Treat the exclusive end correctly and add a regression test with a previous-month contribution whose `IntervalEnd == monthStart`.

```csharp
&& q.contribution.IntervalEnd > monthStart
```

## Summary

The iteration 2 fixes for provider-specific policy lookup, provider-specific cost filtering, optional FK validation, and idempotency retry semantics are present. Migrations and the model snapshot contain the expected new policy/decision tables, indexes, bounded columns, and restricted delete behavior for the new references.

Two blocker correctness issues remain in the dry-run evaluation boundary: reconciliation findings are not scoped to the evaluated billing owner, and exclusive monthly cost intervals are still included at the exact month-start boundary.

## Verification

Attempted `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicy" --verbosity minimal`. The command did not execute the scoped tests because `Api.Tests` still fails to compile in pre-existing unrelated files under `CloudCosts`, `CloudPlanning`, and `CloudReconciliation`.

## Recommendation

Do not pass the phase 21 code review gate until both blocker findings have focused regression tests and fixes.

---

_Reviewed: 2026-06-18T11:23:49Z_
_Reviewer: the agent (gsd-code-reviewer)_
_Depth: standard_

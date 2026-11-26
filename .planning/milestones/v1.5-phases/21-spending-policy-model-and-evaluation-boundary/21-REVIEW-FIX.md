---
phase: 21
fixed_at: 2026-06-18T11:29:21Z
review_path: .planning/phases/21-spending-policy-model-and-evaluation-boundary/21-REVIEW.md
iteration: 3
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 21: Code Review Fix Report

**Fixed at:** 2026-06-18T11:29:21Z
**Source review:** .planning/phases/21-spending-policy-model-and-evaluation-boundary/21-REVIEW.md
**Iteration:** 3

**Summary:**
- Findings in scope: 2
- Fixed: 2
- Skipped: 0

## Fixed Issues

### CR-01: BLOCKER - Account-scoped evaluations include reconciliation findings from other billing accounts

**Files modified:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs`
**Commit:** 93087ae
**Status:** fixed: requires human verification
**Applied fix:** Scoped latest reconciliation findings to the evaluated billing account while preserving truly global orphan findings where both billing owner references are null, and added regression coverage for an account-B cleanup finding plus a global orphan finding during an account-A evaluation.

### CR-02: BLOCKER - Previous-month costs ending exactly at month start are counted in the new month

**Files modified:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs`
**Commit:** 4cb2a21
**Status:** fixed: requires human verification
**Applied fix:** Changed monthly estimated-cost filtering to require `IntervalEnd > monthStart` and updated the boundary regression to use a previous-month contribution ending exactly at the current month start.

## Skipped Issues

None.

## Verification

- `dotnet build sources/Api/Api.csproj --verbosity minimal` passed after CR-01. The API project still emits existing XML documentation warnings.
- `dotnet build sources/Api/Api.csproj --verbosity minimal` passed after CR-02. The API project still emits existing XML documentation warnings.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicy" --verbosity minimal` did not reach the scoped tests because `Api.Tests` still fails to compile in unrelated `CloudCosts`, `CloudPlanning`, and `CloudReconciliation` test files. The reported errors were outside `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs`.
- The `gsd-tools` executable was not on PATH, so commits were created through the installed core script at `$HOME/.codex/gsd-core/bin/gsd-tools.cjs` with the required message format and explicit file list.

---

_Fixed: 2026-06-18T11:29:21Z_
_Fixer: the agent (gsd-code-fixer)_
_Iteration: 3_

---
phase: 21
slug: spending-policy-model-and-evaluation-boundary
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-18
---

# Phase 21 - Validation Strategy

Per-phase validation contract for feedback sampling during execution.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicy` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | ~60-180 seconds for targeted API tests; full-suite timing depends on existing test-project drift |

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicy`.
- **After every plan wave:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj` once existing API test compile drift is fixed or the targeted policy test project path is isolated.
- **Before verification:** Run `dotnet build X39.UnitedTacticalForces.slnx` and targeted policy tests. Do not claim full `dotnet test X39.UnitedTacticalForces.slnx` as green while pre-existing API test compile drift remains.
- **Max feedback latency:** 180 seconds for targeted tests.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 21-01-01 | 01 | 1 | COST-07 | T-21-01 | Invalid thresholds, dates, scope, and action settings are rejected server-side. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyConfigurationTests` | no; Wave 0 | pending |
| 21-02-01 | 02 | 2 | COST-07,COST-19 | T-21-02 | Effective policy selection and threshold decisions are deterministic and provider-mutation-free. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | no; Wave 0 | pending |
| 21-03-01 | 03 | 3 | COST-19,RELY-17 | T-21-03 | Stale metadata, stale reconciliation, cleanup-needed resources, unknown billable resources, sanitized failures, and idempotent retries are covered. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | no; Wave 0 | pending |

## Wave 0 Requirements

- [ ] `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs` - covers COST-07 policy validation and effective-policy selection.
- [ ] `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs` - covers COST-19 threshold/risk handling and RELY-17 idempotent decision persistence.
- [ ] Existing API test compile drift is fixed or isolated before using `dotnet test tests/Api.Tests/Api.Tests.csproj` as a full plan gate. Known drift from research includes stale `CloudInventoryController` constructor calls, missing `Constants` imports, and `ECloudReconciliationRunStatus.Succeeded` references.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full authenticated browser/operator UAT | COST-07,COST-19,RELY-17 | OPS-02 remains deferred until the full change request is implemented and operator credentials/data exist. | Record as operator-gated; do not claim as passed in this phase without explicit operator evidence. |

## Validation Sign-Off

- [ ] All tasks have automated verify commands or Wave 0 dependencies.
- [ ] Sampling continuity: no 3 consecutive tasks without automated verification.
- [ ] Wave 0 covers all missing test references.
- [ ] No watch-mode flags.
- [ ] Feedback latency target is under 180 seconds for targeted policy tests.
- [ ] `nyquist_compliant: true` set in frontmatter only after Wave 0 and automated sampling are complete.

**Approval:** pending

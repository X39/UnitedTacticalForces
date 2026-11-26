---
phase: 21-spending-policy-model-and-evaluation-boundary
plan: "03"
subsystem: api-cloud-policies
tags:
  - spending-policy
  - risk-inputs
  - redaction
  - idempotency
  - validation
dependency_graph:
  requires:
    - 21-01
    - 21-02
  provides:
    - COST-19 risk-aware policy evaluation
    - sanitized evaluation snapshots and failures
    - persisted idempotent risk decision evidence
  affects:
    - sources/Api/Services/CloudPolicies
    - tests/Api.Tests/CloudPolicies
tech_stack:
  added:
    - C# internal risk input record
  patterns:
    - EF Core persisted risk aggregation
    - allowlisted bounded JSON snapshots
    - fail-closed sanitized error persistence
key_files:
  created:
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyRiskInputs.cs
    - .planning/phases/21-spending-policy-model-and-evaluation-boundary/21-03-SUMMARY.md
  modified:
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs
    - .planning/STATE.md
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md
decisions:
  - Unknown billable provider resources deny spending policy evaluation.
  - Cleanup-needed billable resources or stale reconciliation require approval when no deny trigger applies.
  - Stale or unknown cost metadata warns unless a stronger threshold or risk trigger applies.
  - Evaluation evidence stores allowlisted identifiers, counts, freshness, reason codes, and hashed idempotency material only.
  - Evaluation failures persist fail-closed denials with sanitized bounded failure summary and details.
metrics:
  duration: 13m
  completed_date: "2026-06-18T10:31:14Z"
  tasks_completed: 3
  files_changed: 3 production/test files plus closeout docs
requirements:
  completed:
    - COST-19
    - RELY-17
---

# Phase 21 Plan 03: Risk Input Handling, Redaction, and Validation Coverage Summary

Risk-aware policy evaluation now consumes persisted v1.4 cost and reconciliation evidence, stores sanitized bounded decision evidence, and preserves restart-safe idempotent decisions.

## What Changed

Task 1 added failing risk tests, then implemented `CloudSpendingPolicyRiskInputs` and evaluator aggregation over persisted cost snapshots and reconciliation findings. The evaluator now:

- Sums estimated cost from `CloudCostSnapshotContributions` joined to `CloudCostSnapshots`.
- Preserves worst cost freshness where `Unknown` outranks `Stale`, and `Stale` outranks `Fresh`.
- Loads the latest completed reconciliation run for the provider.
- Counts unknown provider resources, cleanup-needed billable resources, and orphan attribution findings.
- Treats missing or old reconciliation as stale according to `ReconciliationStaleAfterMinutes`.
- Applies risk precedence: unknown provider resources deny; cleanup-needed resources or stale reconciliation require approval; stale/unknown cost metadata warns.

Task 2 added failing redaction/idempotency tests, then persisted sanitized bounded evidence for successful and failed evaluations. Evaluation snapshots are allowlisted and avoid raw provider payloads, bearer headers, Steam credentials, worker credentials, provider tokens, and client-supplied free text. Failure handling now catches non-cancellation evaluation exceptions, logs only a hashed idempotency identifier, and stores a fail-closed denied decision with sanitized summary/details.

Task 3 ran final validation gates and recorded the existing compile-drift caveat.

## Commits

| Task | Commit | Message |
|------|--------|---------|
| 1 RED | `0d5971e` | `test(21-03): add failing risk input evaluation tests` |
| 1 GREEN | `6a415ae` | `feat(21-03): aggregate spending policy risk inputs` |
| 2 RED | `13129b0` | `test(21-03): add failing sanitized failure tests` |
| 2 GREEN | `9b7ecc4` | `feat(21-03): persist sanitized evaluation failures` |
| 3 | closeout commit | Summary, state, roadmap, and requirements closeout |

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet build sources/Api/Api.csproj --no-restore -p:NuGetAudit=false` | Passed | API compiled with existing XML documentation warnings only. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyConfigurationTests` | Blocked | Pre-existing test-project compile drift prevented filtered tests from running. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | Blocked | Same pre-existing test-project compile drift prevented filtered tests from running. |
| `dotnet build X39.UnitedTacticalForces.slnx` | Blocked | Same pre-existing `tests/Api.Tests` compile drift prevented solution build completion. |
| Provider-mutation scan over `sources/Api/Services/CloudPolicies` | Passed | No production-service matches for provider clients, HTTP clients, provisioning/teardown services, provider mutation methods, or WebApp generated clients. |
| Production secret-word scan | Passed with benign matches | Only XML documentation text for "cancellation token" matched in production policy service files. |
| Stub scan over changed production/test files | Passed with expected test fixtures | Matches were test idempotency/sensitive-word fixtures and normal nullable setup, not production stubs. |

The pre-existing compile blockers observed in all filtered test and solution-build gates were:

- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs(245,43): CS0103 Constants not found`
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(118,12): CS7036 missing cloudProviderReconciliationService constructor argument for CloudInventoryController`
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(170,17): CS0103 Constants not found`
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs(278,76): CS0117 ECloudReconciliationRunStatus has no Succeeded`
- `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs(279,12): CS7036 missing cloudProvisioningService constructor argument for CloudInventoryController`

Full `dotnet test X39.UnitedTacticalForces.slnx` is not claimed green while this pre-existing API test compile drift remains.

## Deviations from Plan

### Auto-fixed Issues

None - implementation followed the planned scope.

### Validation Caveat

The plan expected targeted policy tests to run, but the `tests/Api.Tests` project currently fails compilation in unrelated Phase 17-19 test files before filters can execute. This was already noted by prior phase summaries and STATE.md. The implementation was still checked with the API project build and source gates.

## Threat Mitigations

| Threat | Mitigation |
|--------|------------|
| Ignored stale cost metadata | Stored freshness is now part of `CloudSpendingPolicyRiskInputs` and can trigger `Warn`. |
| Ignored stale reconciliation | Missing or stale completed reconciliation runs now require approval. |
| Cleanup-needed billable resources | Latest-run findings now require approval. |
| Unknown billable provider resources | Latest-run findings now deny evaluation. |
| Secret leakage in persisted evidence | Snapshots/details are allowlisted or sanitized and bounded before persistence. |
| Provider mutation during evaluation | Source scan confirms no provider clients, provisioning, teardown, HTTP, or generated WebApp client dependencies in policy services. |
| Idempotency drift | Retry tests assert the original persisted decision and evidence remain unchanged. |

## Known Stubs

None. Test fixture strings intentionally include sensitive words and raw-payload phrases to prove persisted evaluation evidence is sanitized.

## Deferred Issues

The unrelated `tests/Api.Tests` compile drift listed in the Verification section remains deferred. It blocks filtered policy tests and the solution build from completing, but it is outside this plan's changed files.

## TDD Gate Compliance

RED and GREEN commits were created for both TDD tasks. RED runs were blocked by the same unrelated compile drift, but Task 1 additionally surfaced the intended missing `CloudSpendingPolicyRiskInputs` type before implementation. GREEN verification used API compilation plus targeted source gates because the test project remained blocked by unrelated files.

## Self-Check: PASSED

- Created file exists: `sources/Api/Services/CloudPolicies/CloudSpendingPolicyRiskInputs.cs`
- Summary file exists: `.planning/phases/21-spending-policy-model-and-evaluation-boundary/21-03-SUMMARY.md`
- Task commits found: `0d5971e`, `6a415ae`, `13129b0`, `9b7ecc4`
- No unexpected tracked deletions were introduced.
- Unrelated local changes in `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/` were not staged or modified by this plan.

---
phase: 21-spending-policy-model-and-evaluation-boundary
plan: "02"
subsystem: api/database
tags:
  - ef-core
  - spending-policy
  - policy-evaluation
  - hosting
  - testing
dependencies:
  requires:
    - 21-01
  provides:
    - spending-policy-decision-contracts
    - spending-policy-decision-entity
    - spending-policy-decision-migration
    - provider-mutation-free-policy-evaluator
    - policy-evaluation-dto
    - policy-evaluation-tests
  affects:
    - phase-21
    - phase-22
    - phase-23
    - phase-24
tech-stack:
  added: []
  patterns:
    - provider-mutation-free evaluator
    - persisted idempotency-key decisions
    - explicit no-policy allow decisions
    - restricted audit relationships
key-files:
  created:
    - sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs
    - sources/Api/DTO/Payloads/CloudSpendingPolicyEvaluationPayload.cs
    - sources/Api/Data/Hosting/CloudSpendingPolicyDecision.cs
    - sources/Api/Data/Migrations/20260618100345_CloudSpendingPolicyDecisions.cs
    - sources/Api/Data/Migrations/20260618100345_CloudSpendingPolicyDecisions.Designer.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyEvaluationService.cs
    - sources/Contract/GameServer/ECloudBillableActionKind.cs
    - sources/Contract/GameServer/ECloudSpendingPolicyDecision.cs
    - sources/Contract/GameServer/ECloudSpendingPolicyEvaluationStatus.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
    - sources/Api/Transform.cs
decisions:
  - Idempotent retry returns the originally persisted spending-policy decision unchanged.
  - Missing effective policy persists an explicit Allow/NoEffectivePolicy decision without seeding thresholds.
  - Evaluation depends only on ApiDbContext, policy configuration, and ILogger, keeping it outside provider mutation boundaries.
  - Current cost uses persisted snapshot contributions when present and records a no-matching-contributions snapshot otherwise.
requirements-completed:
  - COST-07
  - RELY-17
metrics:
  duration: 13m
  completed: 2026-06-18T10:13:18Z
---

# Phase 21 Plan 02: Spending Policy Evaluation Boundary Summary

Provider-mutation-free spending policy evaluation now persists durable, idempotent decisions with audit evidence and threshold outcomes.

## Completed Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Decision persistence schema and initial tests | 802604f, b2128bd | Contract enums, `CloudSpendingPolicyDecision`, `ApiDbContext`, EF migration, evaluation test fixture |
| 2 | Evaluation service, DTOs, and behavior coverage | 3094150, 249e601 | Evaluation payload/DTO, evaluator interface/service, transform mapping, expanded behavior tests |

## What Changed

- Added billable action, policy decision, and evaluation status contract enums.
- Added durable `CloudSpendingPolicyDecision` persistence with bounded JSON evidence, unique idempotency key, and restricted relationships to policy, billing account, session, game server, owner, and reconciliation run records.
- Added `ICloudSpendingPolicyEvaluationService` and `CloudSpendingPolicyEvaluationService` for dry policy evaluation without provider, HTTP, provisioning, teardown, or generated WebApp client dependencies.
- Added evaluation input/decision DTOs and transform mapping.
- Added TDD coverage for persistence shape, restart durability, no-policy behavior, missing owner denial, threshold decisions, idempotent retry behavior, evidence preservation, and provider-mutation-boundary reflection.

## Verification

| Command | Result | Notes |
| ------- | ------ | ----- |
| `dotnet build sources/Api/Api.csproj` | Passed | Build completed with 0 errors. Latest run reported existing XML documentation warnings only. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | Blocked | Test project compile is blocked by unrelated pre-existing API test drift listed below. No plan-owned compile errors remained after fixes. |
| `dotnet build X39.UnitedTacticalForces.slnx` | Blocked | Same unrelated API test-project compile drift blocks full solution build. |
| Targeted acceptance greps | Passed | Verified enum values, DbSet/index/migration shape, evaluator registration and mutation-free dependencies, DTO/transform fields, and threshold/no-policy/idempotency test coverage. |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed duplicate JSON evidence property names**
- **Found during:** Task 2
- **Issue:** Evaluation evidence used duplicate anonymous object property names, which prevented compilation.
- **Fix:** Split the evidence fields into `CostSource` and `RiskSource`.
- **Files modified:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs`
- **Commit:** 249e601

**2. [Rule 1 - Bug] Fixed missing test reflection import**
- **Found during:** Task 2 verification
- **Issue:** The provider-mutation-boundary reflection test used `ConstructorInfo` without importing `System.Reflection`.
- **Fix:** Added the missing using directive so the targeted tests compile past plan-owned code.
- **Files modified:** `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs`
- **Commit:** 249e601

## Deferred Issues

Targeted tests and full solution build are blocked by unrelated pre-existing `tests/Api.Tests` compile drift:

- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(118,12)`: missing `cloudProviderReconciliationService` constructor argument.
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(170,17)`: missing `Constants`.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs(245,43)`: missing `Constants`.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs(278,76)`: `ECloudReconciliationRunStatus.Succeeded` does not exist.
- `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs(279,12)`: missing `cloudProvisioningService` constructor argument.

## Auth Gates

None.

## Known Stubs

None. Empty string defaults in DTO/entity constructors are transport and persistence defaults, not UI-rendered placeholder stubs.

## Threat Flags

None. This plan introduced persistence and dry evaluation only; it did not add network endpoints, provider clients, provisioning, teardown, secret persistence, or generated WebApp client dependencies.

## TDD Gate Compliance

- RED commits: 802604f, 3094150
- GREEN commits: b2128bd, 249e601
- Refactor commits: none needed

## Self-Check: PASSED

- Summary file exists: `.planning/phases/21-spending-policy-model-and-evaluation-boundary/21-02-SUMMARY.md`
- Source commits found: 802604f, b2128bd, 3094150, 249e601
- Key files exist under `sources/Api`, `sources/Contract`, and `tests/Api.Tests`
- No plan-owned source files were left unstaged at summary time

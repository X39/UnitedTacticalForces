---
phase: 22-approval-gates-and-exception-workflow
plan: 01
subsystem: api/cloud-policy
tags:
  - spending-policy
  - cloud-lifecycle
  - gate
  - tests
dependencies:
  requires:
    - Phase 21 spending policy evaluation service
    - Cloud dry-run plans and sessions
    - Durable lifecycle job service
  provides:
    - Reusable cloud spending policy gate service
    - Visible gate result DTO and stable status enum
    - Provisioning, teardown, and cloud lifecycle gate integration
    - Focused gate mapping and dependency-boundary tests
  affects:
    - phase-22
    - phase-23
    - phase-24
    - cloud-policy
    - cloud-inventory
    - game-server-lifecycle
tech-stack:
  added: []
  patterns:
    - Scoped API service delegating to Phase 21 persisted evaluation before provider or lifecycle mutation
    - Conflict response with visible gate result for warn, approval-required, and deny decisions
    - Local-only lifecycle behavior preserved when no active cloud session exists
key-files:
  created:
    - sources/Contract/GameServer/ECloudSpendingPolicyGateStatus.cs
    - sources/Api/DTO/CloudSpendingPolicyGateResultDto.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateResult.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyGateService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateTests.cs
  modified:
    - sources/Api/Controllers/CloudInventoryController.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/Transform.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs
    - tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs
    - tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs
decisions:
  - Gate evaluations create fresh idempotency keys per action attempt because warning acknowledgement and approval replay semantics are delivered by later Phase 22 plans.
  - Runtime start/restart/install/update/delete only gate when the game server has an active non-deleted cloud session; local-only lifecycle actions keep existing behavior.
  - Phase 22-01 returns a visible `409 Conflict` gate result for warn, approval-required, and deny decisions and does not call mutation/enqueue services in those branches.
  - Resize is exposed only as a service method for future endpoints; no production resize endpoint was added.
metrics:
  duration: 45min
  completed: 2026-06-18T11:53:27Z
requirements-completed:
  - COST-15
  - COST-16
  - RELY-18
---

# Phase 22 Plan 01: Spending Policy Gate Integration Summary

Billable cloud actions now flow through a reusable spending-policy gate before provisioning, provider teardown, or cloud-backed lifecycle jobs execute.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add gate result contract and focused tests | 33492fe | `ECloudSpendingPolicyGateStatus`, `CloudSpendingPolicyGateResultDto`, `CloudSpendingPolicyGateTests` |
| 2 | Implement gate service and DTO mapping | 33492fe | `CloudSpendingPolicyGateService`, service interface/result, `Transform` |
| 3 | Integrate gate before mutation/enqueue paths | 33492fe | `CloudInventoryController`, `GameServersController`, touched API test helpers |

## What Changed

- Added a stable gate status enum for allowed, warning acknowledgement required, approval required, and denied outcomes.
- Added an internal gate result plus API DTO mapping that includes the persisted Phase 21 decision.
- Added `CloudSpendingPolicyGateService` to resolve dry-run plans, cloud sessions, active cloud-backed lifecycle actions, and future resize inputs before calling the persisted evaluator.
- Added gate checks before dry-run provisioning, cloud session teardown, and cloud-backed lifecycle enqueueing.
- Added focused tests for provisioning, runtime lifecycle mappings, teardown, resize, status classification, local-only bypass preservation, and no provider mutation dependencies.
- Updated touched API test helpers for the new controller constructor dependency and fixed two small stale test-compilation drifts that blocked any filtered API test run.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyGate"` | Passed | 16 tests passed. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudInventoryVisibilityTests"` | Passed | 6 existing visibility tests passed after wiring the allowing gate fake. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | API project built with 0 warnings and 0 errors. |

## Deviations from Plan

### Auto-fixed Issues

**1. Existing test constructor drift blocked filtered API tests**
- **Found during:** Verification
- **Issue:** Existing API tests had stale `CloudInventoryController` and `GameServersController` constructor calls after the new gate dependency was introduced.
- **Fix:** Updated touched test helpers to pass the gate dependency and added an allowing fake for existing cleanup/teardown tests.
- **Files modified:** `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`, `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs`, `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs`, `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs`
- **Commit:** 33492fe

**2. Stale test-project symbols blocked any filtered API test execution**
- **Found during:** Verification
- **Issue:** Two test files missed the internal API namespace for `Constants`, and one reconciliation status reference used the removed `Succeeded` member.
- **Fix:** Added the API namespace imports and changed the stale status to `Completed`.
- **Files modified:** `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs`, `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs`, `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs`
- **Commit:** 33492fe

## Auth Gates

No new authorization policy was added in this plan. Existing provisioning, teardown, and lifecycle endpoint authorization remains in place; approval authorization is planned in 22-02 and 22-03.

## Known Stubs

- Warning acknowledgement and approved exception replay are intentionally not implemented in this plan. Non-allow decisions return the visible gate result and do not proceed.
- Resize is present as a gate service method only; no resize endpoint exists yet.

## Threat Flags

None. The gate service depends only on `ApiDbContext` and the Phase 21 evaluator; focused tests assert it has no provider mutation client, lifecycle enqueue, HTTP client, or generated WebApp client dependency.

## Self-Check: PASSED

- Verified the expected gate contract, DTO, service, and controller integration files exist.
- Verified plan-owned tests pass.
- Verified the API project builds cleanly.
- Verified unrelated `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/` worktree noise was not staged.

---
phase: 21-spending-policy-model-and-evaluation-boundary
plan: "04"
subsystem: api-cloud-policies
tags:
  - spending-policy
  - api-boundary
  - authorization
  - dry-evaluation
dependency_graph:
  requires:
    - 21-01
    - 21-02
    - 21-03
  provides:
    - authorized spending policy API boundary
    - dry policy evaluation endpoint
    - controller-level authorization and DTO safety tests
  affects:
    - sources/Api/Constants.cs
    - sources/Api/Controllers/CloudSpendingPoliciesController.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
tech_stack:
  added: []
  patterns:
    - ASP.NET Core authorized controller boundary
    - ApiDbContext read projection plus policy-service write/evaluation delegation
    - provider-mutation-free dry evaluation endpoint
key_files:
  created:
    - sources/Api/Controllers/CloudSpendingPoliciesController.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
    - .planning/phases/21-spending-policy-model-and-evaluation-boundary/21-04-SUMMARY.md
  modified:
    - sources/Api/Constants.cs
    - .planning/STATE.md
    - .planning/ROADMAP.md
decisions:
  - The policy API boundary uses class-level authorization plus per-method Claims.Administrative.Server policies.
  - Policy definition writes and dry evaluations resolve the authenticated database user server-side before delegating to policy services.
  - Dry evaluation depends only on policy services and ApiDbContext; provider provisioning, teardown, lifecycle jobs, generated clients, and automatic actions remain out of scope.
metrics:
  duration: 42m
  completed_date: "2026-06-18T10:43:00Z"
  tasks_completed: 2
  files_changed: 5
requirements:
  completed:
    - COST-07
    - RELY-17
---

# Phase 21 Plan 04: Authorized Policy API Boundary Summary

Administrators now have an authorized server-side API boundary to configure spending policies, resolve effective policy, and run dry spending-policy evaluations without provider mutation or lifecycle side effects.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 RED | Add policy controller boundary tests | `13af195` | `CloudSpendingPolicyControllerTests.cs` |
| 1 GREEN | Add authorized policy definition API boundary | `90ae51f` | `Constants.cs`, `CloudSpendingPoliciesController.cs`, `CloudSpendingPolicyControllerTests.cs` |
| 2 | Strengthen dry evaluation route coverage | `237f272` | `CloudSpendingPolicyControllerTests.cs` |

## What Changed

- Added `Constants.Routes.CloudSpendingPolicies = "cloud-spending-policies"`.
- Added `CloudSpendingPoliciesController` with class-level `[ApiController]`, route binding, class-level `[Authorize]`, and per-method `[Authorize(Claims.Administrative.Server)]`.
- Added policy definition endpoints for listing, reading, effective lookup, and create/update through `ICloudSpendingPolicyConfigurationService`.
- Added dry evaluation endpoint `POST dry-evaluations` through `ICloudSpendingPolicyEvaluationService`.
- Added actor resolution through `User.GetDatabaseUserAsync` for policy writes and dry evaluations, returning `Unauthorized` when no database user resolves.
- Added controller tests for authorization attributes, route shape, actor forwarding, DTO projection, validation exception mapping, read not-found behavior, provider/action dependency absence, and decision DTO field-name safety.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet build sources/Api/Api.csproj` | Passed | Latest run completed with 0 errors and existing XML documentation warnings. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyControllerTests` | Blocked | The test project compiled plan-owned API/test files, then stopped on unrelated existing API test drift listed below. |
| `dotnet build X39.UnitedTacticalForces.slnx` | Blocked | Same unrelated `tests/Api.Tests` compile drift blocked the solution build. |
| Route/endpoint/authorization `rg` acceptance checks | Passed | Verified route constant, controller methods, admin policy use, actor resolution, DTO transforms, test coverage terms, and dry-evaluation endpoint symbols. |
| Provider/action dependency scan over `CloudSpendingPoliciesController.cs` | Passed | No matches for provisioning, teardown, lifecycle job, Hetzner client, HttpClient, generated WebApp client, or provider mutation method names. |

Unrelated compile drift blocking filtered tests and solution build:

- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(118,12)`: missing `cloudProviderReconciliationService` constructor argument for `CloudInventoryController`.
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(170,17)`: missing `Constants` symbol.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs(245,43)`: missing `Constants` symbol.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs(278,76)`: `ECloudReconciliationRunStatus.Succeeded` does not exist.
- `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs(279,12)`: missing `cloudProvisioningService` constructor argument for `CloudInventoryController`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed controller validation catch ordering**
- **Found during:** Task 1 GREEN verification
- **Issue:** `ArgumentOutOfRangeException` was caught after its base `ArgumentException`, causing `CS0160`.
- **Fix:** Reordered validation exception catches in `SavePolicyAsync` and `EvaluateDryRunAsync`.
- **Files modified:** `sources/Api/Controllers/CloudSpendingPoliciesController.cs`
- **Commit:** `90ae51f`

**2. [Rule 1 - Bug] Fixed controller test namespace import**
- **Found during:** Task 1 GREEN verification
- **Issue:** New controller tests referenced internal API `Constants` without importing `X39.UnitedTacticalForces.Api`, causing plan-owned `CS0103` errors.
- **Fix:** Added the API namespace import.
- **Files modified:** `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs`
- **Commit:** `90ae51f`

### Execution Shape

The initial RED controller test commit covered both definition endpoints and dry-evaluation behavior. The GREEN implementation therefore introduced the controller and dry endpoint together in `90ae51f`; Task 2 received a separate test refinement commit, `237f272`, that asserts the dry-evaluation route explicitly.

## Auth Gates

None.

## Known Stubs

None.

## Threat Flags

None. This plan adds the planned administrator API boundary only. It does not add provider clients, provisioning, teardown, lifecycle job enqueueing, approval workflow records, generated WebApp clients, or automatic-action execution.

## TDD Gate Compliance

- RED commit: `13af195`
- GREEN commit: `90ae51f`
- Additional Task 2 coverage commit: `237f272`

## Self-Check: PASSED

- Summary file exists: `.planning/phases/21-spending-policy-model-and-evaluation-boundary/21-04-SUMMARY.md`
- Created source files exist: `sources/Api/Controllers/CloudSpendingPoliciesController.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs`
- Task commits found: `13af195`, `90ae51f`, `237f272`
- No unexpected tracked deletions were introduced.
- No plan-owned source files remain unstaged.
- Unrelated local changes in `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/` were not staged or modified.

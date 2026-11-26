---
phase: 21-spending-policy-model-and-evaluation-boundary
plan: 01
subsystem: api/database
tags:
  - ef-core
  - spending-policy
  - hosting
  - validation
dependencies:
  requires:
    - v1.4 running cost snapshot foundation
    - v1.4 provider reconciliation runs and findings
  provides:
    - Cloud spending policy scope/action contracts
    - Durable CloudSpendingPolicy entity and EF migration
    - Policy configuration payload, DTO, service, and effective lookup
    - Targeted policy validation and restart-safe persistence tests
  affects:
    - phase-21
    - phase-22
    - phase-23
    - phase-24
    - cloud-policy
tech-stack:
  added: []
  patterns:
    - EF Core Hosting bounded-context entity with restrict-delete audit references
    - Scoped API service using only ApiDbContext for configuration persistence
    - Billing-account scoped policy wins wholesale, then active global fallback
key-files:
  created:
    - sources/Contract/GameServer/ECloudSpendingPolicyScope.cs
    - sources/Contract/GameServer/ECloudSpendingPolicyAction.cs
    - sources/Api/Data/Hosting/CloudSpendingPolicy.cs
    - sources/Api/DTO/CloudSpendingPolicyDto.cs
    - sources/Api/DTO/Payloads/CloudSpendingPolicyConfigurationPayload.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyConfigurationService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyConfigurationService.cs
    - sources/Api/Data/Migrations/20260618095241_CloudSpendingPolicies.cs
    - sources/Api/Data/Migrations/20260618095241_CloudSpendingPolicies.Designer.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
    - sources/Api/Transform.cs
decisions:
  - No production default spending thresholds were seeded; tests create explicit fixture policies.
  - Effective lookup returns the newest active billing-account policy wholesale before falling back to the newest active global policy.
  - The configuration service has no provider mutation client dependency and only persists validated policy metadata.
  - Billing-account, creator, and updater relationships use restrict delete so policy audit history is not cascaded away.
metrics:
  duration: 11min
  completed: 2026-06-18T09:56:28Z
requirements-completed:
  - COST-07
  - RELY-17
---

# Phase 21 Plan 01: Spending Policy Configuration Summary

Durable spending policy configuration is now represented by contract enums, an EF-backed Hosting entity/table, a server-side configuration service, DTO/payload mapping, and targeted tests for validation, effective selection, and restart-safe persistence.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add spending policy contracts, entity, and configuration tests | 813e5ea, f919b86 | `ECloudSpendingPolicyScope`, `ECloudSpendingPolicyAction`, `CloudSpendingPolicy`, `CloudSpendingPolicyConfigurationTests` |
| 2 | Implement policy configuration service, payload, DTO, and effective selection | b3db937, a626039 | `CloudSpendingPolicyConfigurationService`, service interface, DTO/payload, `ApiDbContext`, `Transform` |
| 3 | Generate EF migration and verify restart-safe policy persistence | c717ffe | `CloudSpendingPolicies` migration, model snapshot, restart-safe lookup test |

## What Changed

- Added stable policy scope/action contract enums with explicit integer values for global and billing-account scoped spending policies.
- Added `CloudSpendingPolicy` with limits, warning/approval thresholds, automatic-action settings, stale reconciliation window, effective dates, audit actor FKs, audit reason, and timestamps.
- Added `CloudSpendingPolicyConfigurationService` with create/update validation and effective policy lookup.
- Added `CloudSpendingPolicyConfigurationPayload`, `CloudSpendingPolicyDto`, and `Transform.ToDto(this CloudSpendingPolicy self)`.
- Added EF Core model configuration and generated the `CloudSpendingPolicies` migration with lookup indexes and restrict-delete relationships.
- Added targeted API tests covering contract shape, secret-safe fields, validation, effective fallback, update stamping, and lookup after recreating the DbContext/service.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet build sources/Api/Api.csproj` | Passed | API and referenced projects built with 0 warnings and 0 errors. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyConfigurationTests` | Blocked by unrelated compile drift | The test project builds policy source but cannot execute filtered tests because unrelated cloud test files currently fail compilation. |
| `dotnet build X39.UnitedTacticalForces.slnx` | Blocked by unrelated compile drift | Same unrelated `tests/Api.Tests` compile failures prevent full solution green. |
| Acceptance `rg` checks | Passed | Verified enum values, service registration, DbSet, DTO/payload/transform mapping, generated migration fields/indexes/restrict FKs, restart-safe test, and no secret-bearing policy contract/entity fields. |

Unrelated compile drift observed in `tests/Api.Tests`:

- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(118,12)`: missing `cloudProviderReconciliationService` constructor argument.
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs(170,17)`: missing `Constants` symbol.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs(245,43)`: missing `Constants` symbol.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityServiceTests.cs(278,76)`: `ECloudReconciliationRunStatus.Succeeded` does not exist.
- `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs(279,12)`: missing `cloudProvisioningService` constructor argument.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] Added persistence-bound input validation beyond threshold ordering**
- **Found during:** Task 2
- **Issue:** The plan required scope/date/threshold/action validation, but a persisted billing-account scoped policy also needs an existing billing account and bounded text fields to avoid invalid foreign keys or unbounded persisted metadata.
- **Fix:** `SavePolicyAsync` verifies billing-account existence before save, normalizes provider and audit reason text, and rejects over-length provider, display, and audit values before `SaveChangesAsync`.
- **Files modified:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyConfigurationService.cs`
- **Commit:** a626039

## Auth Gates

None.

## Known Stubs

None. The policy service is internal by design; the administrator-facing API boundary is explicitly deferred to Plan 21-04.

## Deferred Issues

- The unrelated `tests/Api.Tests` compile drift listed under Verification prevents claiming a green filtered test run or full solution build. This was not broadened per the plan constraint.

## Threat Flags

None. The new durable policy table/service are the planned trust boundary for this plan; no new network endpoint, secret surface, file access pattern, or provider mutation path was added.

## Self-Check: PASSED

- Verified expected created files exist.
- Verified task commits exist: `813e5ea`, `f919b86`, `b3db937`, `a626039`, `c717ffe`.
- Verified no plan-owned source files remain unstaged after task commits.

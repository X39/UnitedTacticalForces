---
phase: 22-approval-gates-and-exception-workflow
plan: 02
subsystem: api/cloud-policy
tags:
  - spending-policy
  - approvals
  - ef-core
  - authorization
dependencies:
  requires:
    - Phase 21 persisted spending policy decisions
    - Phase 22-01 spending policy gate service
  provides:
    - Durable spending policy approval request entity and migration
    - Approval service for create/reuse, approve, deny, validate, expire, and warning acknowledgement
    - Approval request DTO and approval decision payload
    - Admin approval list/read/approve/deny API boundary
    - Gate progression for acknowledged warnings and approved scoped exceptions
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
    - EF Core Hosting entity with restrict-delete audit references
    - Scoped provider-mutation-free approval service
    - Existing spending policy controller extended with admin-only approval routes
key-files:
  created:
    - sources/Contract/GameServer/ECloudSpendingPolicyApprovalStatus.cs
    - sources/Api/Data/Hosting/CloudSpendingPolicyApprovalRequest.cs
    - sources/Api/Data/Migrations/20260618120037_CloudSpendingPolicyApprovals.cs
    - sources/Api/Data/Migrations/20260618120037_CloudSpendingPolicyApprovals.Designer.cs
    - sources/Api/DTO/CloudSpendingPolicyApprovalRequestDto.cs
    - sources/Api/DTO/Payloads/CloudSpendingPolicyApprovalDecisionPayload.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyApprovalService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyGateService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateResult.cs
    - sources/Api/Controllers/CloudInventoryController.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/Controllers/CloudSpendingPoliciesController.cs
    - sources/Api/Transform.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
decisions:
  - Approval requests are matched by provider, action, policy, owner, game server, session, billing assignment, dry-run plan, and resource scope.
  - Approved exceptions must be unexpired at action execution time; pending requests are expired deterministically during reads or validation.
  - Warning acknowledgement uses a matching prior warning decision id and scope.
  - Approval APIs live on `CloudSpendingPoliciesController` under `approvals/*` and use existing admin server authorization.
metrics:
  duration: 58min
  completed: 2026-06-18T12:09:01Z
requirements-completed:
  - COST-15
  - COST-17
  - RELY-18
---

# Phase 22 Plan 02: Approval Workflow Summary

Approval-required gate outcomes now create or reuse durable approval requests, and approved scoped exceptions or acknowledged warnings can allow a later gate retry to proceed.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add approval persistence entity and migration | 70e1d4e | approval status enum, `CloudSpendingPolicyApprovalRequest`, EF migration/model snapshot |
| 2 | Implement approval service and gate progression | 70e1d4e | approval service/interface, gate service/result/interface, gate DTO |
| 3 | Add admin approval API boundary | 70e1d4e | `CloudSpendingPoliciesController`, approval DTO/payload, controller tests |

## What Changed

- Added durable approval requests with status, decision/policy references, scoped game/session/billing/dry-run/resource references, requester/resolver audit fields, projected cost, expiration, and sanitized reasons.
- Added EF configuration and migration with restrict-delete relationships to preserve approval history.
- Added an approval service for pending request reuse, approval, denial, expiry, approved-scope validation, and warning acknowledgement.
- Extended gate evaluation to allow acknowledged warnings, allow approved unexpired matching exceptions, and return pending approval request details for approval-required outcomes.
- Added optional `acknowledgedDecisionId` and `approvalRequestId` query parameters to cloud provisioning, teardown, and lifecycle action endpoints.
- Added admin-only approval list/read/approve/deny endpoints.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyControllerTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests"` | Passed | 45 tests passed. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | API project built with 0 warnings and 0 errors. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicy"` | Partial failure | 80 passed, 3 older Phase 21 evaluation fixture tests failed due stale unique-index/freshness assumptions. |

Broader policy-filter failures observed:

- `CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart`: fixture creates duplicate `CloudCostSnapshots(SessionFk, Source)`.
- `CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans`: fixture creates duplicate active `GameServerBillingAssignments(GameServerFk)`.
- `CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts`: expectation assumes allow, but current evaluator warns when selected-provider cost metadata is unknown.

## Deviations from Plan

### Auto-fixed Issues

**1. Approval validation needed dry-run/resource matching**
- **Found during:** Task 2 tests
- **Issue:** Validating only the decision scope would let an approval for one dry-run/resource scope satisfy another action with the same broader session/owner scope.
- **Fix:** `ValidateApprovedAsync` now accepts and checks current dry-run and resource identifiers.
- **Files modified:** `ICloudSpendingPolicyApprovalService`, `CloudSpendingPolicyApprovalService`, `CloudSpendingPolicyGateService`
- **Commit:** 70e1d4e

**2. Controller actor lookup could throw without an HTTP user**
- **Found during:** Task 3 tests
- **Issue:** Direct controller tests without a `HttpContext.User` triggered a `NullReferenceException` before returning `Unauthorized`.
- **Fix:** `CloudSpendingPoliciesController.GetActorAsync` now returns null when no HTTP user exists and then resolves via the captured principal.
- **Files modified:** `sources/Api/Controllers/CloudSpendingPoliciesController.cs`
- **Commit:** 70e1d4e

## Auth Gates

Approval list, read, approve, and deny endpoints require `Claims.Administrative.Server`. Existing action endpoint authorization remains unchanged; the gate runs after actor resolution.

## Known Stubs

None for the backend approval workflow in this plan. Frontend/client generation and policy visibility are still Phase 24 work.

## Threat Flags

None. Approval service dependency tests assert no provider mutation client, HTTP client, lifecycle enqueue service, or generated WebApp client dependency. Reasons are sanitized through the existing lifecycle failure sanitizer before persistence.

## Self-Check: PASSED

- Verified migration generated through `dotnet ef`.
- Verified focused approval/gate/controller tests pass.
- Verified the API project builds cleanly.
- Verified unrelated `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/` worktree noise was not staged.

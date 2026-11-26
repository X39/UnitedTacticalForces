---
phase: 23-automated-shutdown-and-policy-action-execution
plan: 02
subsystem: api/cloud-policy
tags:
  - spending-policy
  - lifecycle
  - teardown
  - api
dependencies:
  requires:
    - Phase 23-01 automatic action attempt model
    - Durable lifecycle job service
    - Cloud provider teardown service
  provides:
    - Stop runtime automatic action execution
    - Cleanup runtime automatic action execution
    - Explicit provider teardown automatic action execution
    - Admin processing API boundary
  affects:
    - phase-23
    - phase-24
    - cloud-policy
    - game-server-lifecycle
    - cloud-inventory
tech-stack:
  added: []
  patterns:
    - Lifecycle mutation through `IGameServerLifecycleJobService`
    - Provider teardown mutation through `ICloudTeardownService`
    - Admin controller endpoint returning DTOs
key-files:
  created: []
  modified:
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyAutomaticActionService.cs
    - sources/Api/Controllers/CloudSpendingPoliciesController.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyAutomaticActionTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
decisions:
  - `StopRuntime` enqueues lifecycle `Stop`.
  - `CleanupRuntime` enqueues lifecycle `Delete` and does not call provider teardown.
  - `DeleteProviderResources` is the only automatic action that calls provider teardown, and it requires session plus billing scope.
metrics:
  duration: 25min
  completed: 2026-06-18T12:34:08Z
requirements-completed:
  - COST-18
  - RELY-19
---

# Phase 23 Plan 02: Automatic Action Execution Summary

Automatic policy actions now execute through the correct durable boundary for runtime lifecycle work and provider teardown.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Execute stop and cleanup actions | f33131e | `CloudSpendingPolicyAutomaticActionService`, lifecycle fake tests |
| 2 | Execute provider teardown action | f33131e | `CloudSpendingPolicyAutomaticActionService`, teardown fake tests |
| 3 | Add controller/API boundary and tests | f33131e | `CloudSpendingPoliciesController`, controller test wiring |

## What Changed

- Added `GET /cloud-spending-policies/automatic-actions` to list durable automatic action attempts.
- Added `POST /cloud-spending-policies/automatic-actions/process` for admin-triggered processing.
- Mapped `StopRuntime` to lifecycle `Stop`.
- Mapped `CleanupRuntime` to lifecycle `Delete` without provider teardown.
- Mapped `DeleteProviderResources` exclusively to provider teardown and required decision snapshot session/billing scope.
- Persisted lifecycle job references and terminal attempt status after enqueueing.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyAutomaticActionTests"` | Passed | Stop, cleanup, provider teardown, scope, and controller processing coverage included. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyAutomaticActionTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~CloudSpendingPolicyControllerTests"` | Passed | 60 tests passed. |

## Safety Boundary

Runtime cleanup and provider teardown are separate:

- `CleanupRuntime` only enqueues a lifecycle `Delete` job.
- `DeleteProviderResources` is the only path that invokes `ICloudTeardownService`.
- Tests assert stop/cleanup do not call provider teardown and provider teardown does not enqueue lifecycle jobs.

## Deviations from Plan

None requiring scope changes. The processing endpoint is explicit and admin-only; no background hosted scheduler was added in this phase.

## Self-Check: PASSED

- Lifecycle actions preserve billing owner snapshots on the action attempt and lifecycle job.
- Provider teardown is explicit and scoped.
- Direct API boundary returns DTOs and uses the authenticated actor.

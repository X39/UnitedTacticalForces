---
phase: 23-automated-shutdown-and-policy-action-execution
plan: 01
subsystem: api/cloud-policy
tags:
  - spending-policy
  - automatic-actions
  - persistence
  - idempotency
dependencies:
  requires:
    - Phase 21 persisted policy decisions
    - Phase 22 approval and gate foundations
  provides:
    - Durable automatic action attempt model
    - Idempotent attempt creation
    - Notify-only durable no-op behavior
  affects:
    - phase-23
    - phase-24
    - cloud-policy
tech-stack:
  added: []
  patterns:
    - Hosting entity with restrict-delete relationships
    - Scoped service with explicit scheduler boundary
    - SQLite restart-safe persistence tests
key-files:
  created:
    - sources/Contract/GameServer/ECloudSpendingPolicyAutomaticActionStatus.cs
    - sources/Api/Data/Hosting/CloudSpendingPolicyAutomaticActionAttempt.cs
    - sources/Api/DTO/CloudSpendingPolicyAutomaticActionAttemptDto.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyAutomaticActionService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyAutomaticActionService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyAutomaticActionTests.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Transform.cs
decisions:
  - One automatic action attempt is keyed by policy decision primary key because each persisted decision carries the action and scope snapshot.
  - `None` decisions produce no attempt; `NotifyOnly` records a durable `NoAction` attempt and never calls lifecycle or provider mutation services.
  - Attempt records snapshot decision owner/session/action scope and expose only sanitized failure fields.
metrics:
  duration: 35min
  completed: 2026-06-18T12:34:08Z
requirements-completed:
  - COST-18
  - RELY-19
---

# Phase 23 Plan 01: Automatic Action Model Summary

Phase 23 now has a durable automatic policy action attempt model and idempotent scheduler service boundary.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add contracts, entity, DTO, and migration | f33131e | `ECloudSpendingPolicyAutomaticActionStatus`, attempt entity/DTO, EF migration |
| 2 | Implement idempotent attempt creation | f33131e | `CloudSpendingPolicyAutomaticActionService` |
| 3 | Add focused tests | f33131e | `CloudSpendingPolicyAutomaticActionTests` |

## What Changed

- Added stable automatic action attempt statuses for pending, no-op, enqueued, succeeded, failed, and retry-pending states.
- Added a persisted `CloudSpendingPolicyAutomaticActionAttempt` entity with decision/action/scope ownership, lifecycle job linkage, retry counters, and sanitized failure fields.
- Added service methods to list attempts, process one decision, and process all due automatic actions.
- Added restart-safe idempotency tests proving repeated notify-only processing returns the same durable attempt without mutation calls.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyAutomaticActionTests"` | Passed | 11 tests passed. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | API project built with 0 warnings and 0 errors. |

## Deviations from Plan

The model and service were implemented together with the execution behavior from plans 23-02 and 23-03 because the mutation boundary and retry state share the same durable attempt workflow.

## Self-Check: PASSED

- Durable attempt records are persisted.
- `None` and `NotifyOnly` do not enqueue lifecycle jobs or call provider teardown.
- DTOs avoid secret-bearing field names.

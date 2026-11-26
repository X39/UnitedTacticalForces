---
phase: 22-approval-gates-and-exception-workflow
plan: 03
subsystem: api/cloud-policy
tags:
  - spending-policy
  - approvals
  - hardening
  - verification
dependencies:
  requires:
    - Phase 22-01 spending policy gate service
    - Phase 22-02 durable approval workflow
  provides:
    - Expired approved exception hardening
    - Direct endpoint bypass tests
    - Phase 22 verification evidence
  affects:
    - phase-22
    - phase-23
    - phase-24
    - cloud-policy
tech-stack:
  added: []
  patterns:
    - Direct controller tests with blocking gate fakes
    - Expiry enforcement during action-time approval validation
key-files:
  created:
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs
    - .planning/phases/22-approval-gates-and-exception-workflow/22-VERIFICATION.md
  modified:
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs
decisions:
  - Expired approved requests are durably marked `Expired` during validation, matching pending request expiry behavior.
  - Direct endpoint bypass tests cover provisioning, teardown, and lifecycle enqueue paths with non-allow gate responses.
metrics:
  duration: 16min
  completed: 2026-06-18T12:15:18Z
requirements-completed:
  - COST-15
  - COST-16
  - COST-17
  - RELY-18
---

# Phase 22 Plan 03: Hardening Summary

Phase 22 hardening now proves direct action endpoints stop before mutation/enqueue services when a gate returns a non-allow result, and expired approved exceptions are no longer usable.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add expiry and audit hardening | 211cf3e | `CloudSpendingPolicyApprovalService`, approval tests |
| 2 | Add authorization-sensitive/direct bypass tests | 211cf3e | `CloudSpendingPolicyGateEndpointTests` |
| 3 | Run phase gates and close documentation | pending docs commit | `22-03-SUMMARY.md`, `22-VERIFICATION.md` |

## What Changed

- Approval validation now expires both pending and approved requests whose expiration timestamp has passed.
- Added a test proving expired approved requests are durably marked `Expired` and cannot be used.
- Added direct controller tests proving blocked gate outcomes prevent provisioning, teardown, and lifecycle enqueue calls.
- Ran schema drift and codebase drift checks for Phase 22.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~CloudSpendingPolicyControllerTests"` | Passed | 49 tests passed. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | API project built with 0 warnings and 0 errors. |
| `gsd-tools verify schema-drift 22` | Passed | `drift_detected:false`, non-blocking. |
| `gsd-tools verify codebase-drift 22` | Warning | Non-blocking warning for older root files and unknown `.planning/config.json` key. |

## Deviations from Plan

None requiring implementation changes beyond the planned hardening. The broader `FullyQualifiedName~CloudSpendingPolicy` test filter still contains three older Phase 21 evaluation fixture failures documented in `22-02-SUMMARY.md`.

## Auth Gates

Direct endpoint tests cover the policy-gate bypass concern at the controller level. Attribute-level approval endpoint authorization is covered in `CloudSpendingPolicyControllerTests`; deeper claim behavior remains tied to ASP.NET authorization middleware and existing policy setup.

## Known Stubs

Frontend/client generation and end-user policy visibility are deferred to Phase 24.

## Threat Flags

None. The new endpoint tests use fakes only and do not introduce provider, credential, Steam, worker, or frontend client dependencies.

## Self-Check: PASSED

- Verified focused tests pass.
- Verified API build passes.
- Verified schema drift is clean.
- Verified unrelated worktree noise was not staged.

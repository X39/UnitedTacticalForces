---
phase: 23-automated-shutdown-and-policy-action-execution
plan: 03
subsystem: api/cloud-policy
tags:
  - spending-policy
  - retry
  - hardening
  - verification
dependencies:
  requires:
    - Phase 23-01 automatic action attempt model
    - Phase 23-02 automatic action execution
  provides:
    - Retry-visible failure persistence
    - Sanitized failure fields
    - Phase 23 verification evidence
  affects:
    - phase-23
    - phase-24
    - cloud-policy
tech-stack:
  added: []
  patterns:
    - `LifecycleFailureReasonSanitizer` for persisted automatic action failures
    - Retry-pending state with bounded next-attempt timestamp
key-files:
  created:
    - .planning/phases/23-automated-shutdown-and-policy-action-execution/23-VERIFICATION.md
  modified:
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyAutomaticActionService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyAutomaticActionTests.cs
decisions:
  - Failed automatic actions are persisted as `RetryPending` until `MaxAttemptCount` is exhausted, then `Failed`.
  - Sanitized summary/details are persisted for operator visibility without exposing likely credential-bearing messages.
  - Verification uses fake provider/lifecycle services only; live provider teardown remains operator-gated.
metrics:
  duration: 20min
  completed: 2026-06-18T12:34:08Z
requirements-completed:
  - COST-18
  - RELY-19
---

# Phase 23 Plan 03: Hardening Summary

Automatic action execution now records sanitized, retry-visible failures and has focused safety tests around provider teardown boundaries.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Failure and retry visibility | f33131e | `CloudSpendingPolicyAutomaticActionService` |
| 2 | Safety tests | f33131e | `CloudSpendingPolicyAutomaticActionTests` |
| 3 | Verification and closure | pending docs commit | `23-03-SUMMARY.md`, `23-VERIFICATION.md`, `23-REVIEW.md` |

## What Changed

- Mutation failures are caught and persisted on the attempt with `FailureCategory`, sanitized summary/details, attempt count, and next retry timestamp.
- Attempts transition to `RetryPending` until their max attempt count is exhausted.
- Added tests proving provider teardown scope is required before the teardown service is called.
- Added tests proving likely secret-bearing failure text is redacted before persistence.
- Fixed a code-review finding in `301f4d4` so concurrent attempt creation races return the durable attempt instead of surfacing a unique insert failure.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyAutomaticActionTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~CloudSpendingPolicyControllerTests"` | Passed | 60 tests passed. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | API project built with 0 warnings and 0 errors. |
| `gsd-tools verify schema-drift 23` | Passed | `drift_detected:false`, non-blocking. |
| `gsd-tools verify codebase-drift 23` | Warning | Non-blocking warning for older root files and unknown `.planning/config.json` key. |
| Phase 23 code review | Passed | One idempotency race found and fixed in `301f4d4`; final report is clean. |

## Deviations from Plan

The first parallel verification run hit a compiler output file lock because `dotnet test` and `dotnet build` were invoked concurrently. The test command was rerun by itself and passed.

## Residual Risks

- A hosted/background scheduler was not added; Phase 23 exposes explicit admin processing and keeps execution service-driven.
- Live provider teardown remains covered by fake-only tests in this phase.
- The broader `FullyQualifiedName~CloudSpendingPolicy` filter still includes older Phase 21 fixture failures documented in Phase 22.

## Self-Check: PASSED

- Failure fields are sanitized.
- Runtime cleanup does not imply provider deletion.
- Provider teardown requires explicit `DeleteProviderResources`.
- Phase verification evidence is recorded.

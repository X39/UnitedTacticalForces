---
phase: 37-provisioning-runtime-operation-and-teardown
plan: 02
subsystem: api
tags:
  - aspnet-core
  - lifecycle-jobs
  - cloud-worker
  - spending-policy
  - xunit
requires:
  - 37-01 server-scoped provisioning and provider teardown
provides:
  - Durable runtime lifecycle operation status semantics
  - Runtime cleanup separated from provider teardown policy semantics
  - Focused automated coverage for retry, cleanup, sanitized failure, and worker-claim behavior
affects:
  - OPS-02
  - OPS-04
tech_stack:
  added: []
  patterns:
    - ASP.NET Core controller projection
    - EF-backed lifecycle job queue
    - xUnit focused regression tests
key_files:
  created:
    - .planning/phases/37-provisioning-runtime-operation-and-teardown/37-02-SUMMARY.md
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
    - tests/Api.Tests/GameServerLifecycle/LifecycleJobQueueServiceTests.cs
    - tests/Api.Tests/CloudWorker/Arma3LifecycleJobExecutorTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateTests.cs
decisions:
  - ELifecycleJobKind.Delete remains worker-side runtime cleanup in lifecycle policy evaluation; provider teardown is only evaluated by the explicit teardown path.
  - Cloud runtime status returns display-safe runtime action labels because downstream WebApp/repository surfaces consume this list directly.
metrics:
  duration: "~18 min"
  completed: 2026-06-20T15:34:08Z
  tasks: 2
  files_changed: 7
requirements:
  - OPS-02
  - OPS-04
---

# Phase 37 Plan 02: Durable Runtime Lifecycle Jobs And Cleanup Semantics Summary

Durable runtime lifecycle status now exposes safe install/update/start/stop/restart/log/cleanup actions, retry state, and cleanup state without treating runtime cleanup as provider teardown.

## Accomplishments

- Added failing coverage for the OPS-02 runtime lifecycle action matrix, worker claim scoping, retry-pending state, sanitized failure projection, update execution, restart execution, and cleanup billing semantics.
- Updated cloud runtime status projection so available actions use user-facing labels: `Refresh cloud status`, `Retry cloud action`, `Install runtime`, `Update runtime`, `Start runtime`, `Stop runtime`, `Restart runtime`, `Collect logs`, and `Cleanup runtime`.
- Kept conflicting active jobs from exposing additional runtime actions until the active job clears.
- Removed lifecycle `Delete` from provider-teardown spending-policy mapping so runtime cleanup does not imply provider resource deletion or cost stop.

## Task Commits

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add runtime lifecycle job and status tests | 566381e | `CloudInventoryVisibilityTests.cs`, `LifecycleJobQueueServiceTests.cs`, `Arma3LifecycleJobExecutorTests.cs`, `CloudSpendingPolicyGateTests.cs` |
| 2 | Normalize runtime action routing, policy semantics, and status projection | 8f8cecf | `GameServersController.cs`, `CloudSpendingPolicyGateService.cs` |

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~LifecycleJobQueueServiceTests|FullyQualifiedName~Arma3LifecycleJobExecutorTests" --verbosity minimal` | Passed | 22 tests passed |
| `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyGateTests" --verbosity minimal` | Passed | 19 tests passed |
| `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` | Passed | 0 warnings, 0 errors |

## Deferred UAT

- Manual authenticated browser UAT was not run by user instruction.
- Live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, final billable-resource verification, and Phase 30 evidence collection were not run by user instruction.
- This plan does not claim deferred live OPS-02/OPS-03 or Phase 30 UAT as passed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical coverage] Added direct spending-policy regression coverage**
- **Found during:** Task 1
- **Issue:** The plan named `CloudSpendingPolicyGateService.cs` as a behavior target, but the listed test files did not directly lock the cleanup-versus-provider-teardown policy mapping.
- **Fix:** Extended `CloudSpendingPolicyGateTests` to assert lifecycle `Delete` is not mapped to `ProviderTeardown`; provider teardown remains covered through the explicit teardown evaluator.
- **Files modified:** `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateTests.cs`
- **Commit:** 566381e

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Summary file exists at `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-02-SUMMARY.md`.
- Modified source and test files exist.
- Task commits `566381e` and `8f8cecf` exist in git history.
- Stub marker scan found only existing null/default assignments and test fixture values, not plan-blocking stubs.

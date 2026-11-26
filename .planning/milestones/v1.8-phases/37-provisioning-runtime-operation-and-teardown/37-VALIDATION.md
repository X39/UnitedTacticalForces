---
phase: 37
slug: provisioning-runtime-operation-and-teardown
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-20
---

# Phase 37 - Validation Strategy

Per-phase validation contract for feedback sampling during execution.

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 and EF Core SQLite 9.0.4 |
| Config file | `tests/Api.Tests/Api.Tests.csproj` |
| Quick run command | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |
| Estimated runtime | Use focused subsystem filters after each task; use full solution test/build gates at wave and phase boundaries. |

## Sampling Rate

- After every task commit: run the focused command for the touched subsystem.
- After every plan wave: run `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal`.
- Before phase verification: run focused Phase 37 tests and `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`.
- Feedback latency target: no more than one implementation task without an automated check.

## Requirement Verification Map

| Requirement | Behavior | Test Type | Automated Command | Status |
|-------------|----------|-----------|-------------------|--------|
| OPS-01 | Provision a reviewed valid cloud dry-run plan and project session, resource, bootstrap, and worker status without secrets. | API/service plus repository/UI boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Pending Phase 37 additions |
| OPS-02 | Queue and display install, update, start, stop, restart, collect logs, and runtime cleanup as durable jobs. | API/service plus worker dispatch plus source-boundary UI | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkerDispatchServiceTests|FullyQualifiedName~Arma3LifecycleJobExecutorTests" --verbosity minimal` | Pending Phase 37 additions |
| OPS-03 | Treat provider teardown as the explicit cost-stop action, separate from runtime stop, runtime cleanup, and local deletion. | API/service plus UI source-boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Pending Phase 37 additions |
| OPS-04 | Display retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states after provisioning or teardown. | API/service plus mapper plus UI source-boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Pending Phase 37 additions |

## Per-Task Verification Map

Planner-assigned task IDs must map back to the requirement rows above. The planner should add or reference task-specific verification in each `*-PLAN.md` until this table can be finalized during execution.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| planner-assigned | API provisioning/status plan | Wave 0/1 | OPS-01 | T-37-01 / T-37-03 | Owner-safe provisioning/status without secret projection | service/API/boundary | focused provisioning/inventory/repository tests | partial | pending |
| planner-assigned | Runtime operations plan | Wave 0/1 | OPS-02 | T-37-03 / T-37-06 | Durable jobs and sanitized progress/failures | service/API/boundary | focused lifecycle/worker/runtime tests | partial | pending |
| planner-assigned | Provider teardown plan | Wave 0/1 | OPS-03 | T-37-04 / T-37-05 | Cost-stop action cannot be confused with runtime cleanup | service/API/UI boundary | focused teardown/inventory/UI copy tests | partial | pending |
| planner-assigned | State visibility plan | Wave 0/1 | OPS-04 | T-37-03 / T-37-05 | Partial failure and billable-resource states are visible without secrets | service/mapper/UI boundary | focused teardown/inventory/repository/UI tests | partial | pending |

## Wave 0 Requirements

- [ ] Add or extend `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` for any owner-safe provisioning/teardown status route changes.
- [ ] Add `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` for Phase 37 UI copy, no raw generated DTO binding, teardown confirmation, deferred-UAT copy, and forbidden terms.
- [ ] Extend `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` for any new owner-safe generated route and the `Teardown provider resources` label.
- [ ] Add mapper/source checks for partial teardown failure so remaining billable resources do not render as full success.

## Threat-Oriented Checks

| Threat Ref | Threat | Automated Mitigation Target |
|------------|--------|-----------------------------|
| T-37-01 | Owner provisions a stale, superseded, or wrong-owner plan. | Keep dry-run review blockers and `CloudProvisioningService` validation before token resolution. |
| T-37-02 | Warning or approval replay across resource scope. | Preserve Phase 36 continuation identity, exact decision scope, and expiration matching. |
| T-37-03 | Secret leakage through provider, worker, or runtime failures. | Use sanitizers, redaction guard checks, and safe mapper projections. |
| T-37-04 | Runtime cleanup mistaken for provider cost stop. | Require explicit UI copy and separate provider teardown confirmation. |
| T-37-05 | Partial provider deletion hidden as success. | Display remaining billable resources, cleanup-needed resources, and retry/review next steps. |
| T-37-06 | Cloud worker claims another server's job. | Preserve `LifecycleJobQueueService.ApplyWorkerClaimScope` and worker/session/game-server scoping tests. |

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Status |
|----------|-------------|------------|--------|
| Authenticated browser walkthrough of `/servers` provisioning, runtime action, and teardown panels. | OPS-01, OPS-02, OPS-03, OPS-04 | Requires a live authenticated browser session. | Deferred by user instruction. |
| Live Hetzner provider provisioning and teardown. | OPS-01, OPS-03, OPS-04 | Mutates external billable provider resources. | Deferred by user instruction. |
| Steam/Arma install, update, start, restart, stop, and log collection against a real game server. | OPS-02 | Requires external Steam/Arma tooling and runtime environment. | Deferred by user instruction. |
| Phase 30 UAT closure evidence. | OPS-01, OPS-02, OPS-03, OPS-04 | Separate deferred UAT milestone. | Deferred by user instruction. |

## Validation Sign-Off

- [ ] All planner tasks have automated verification or explicit Wave 0 dependencies.
- [ ] Sampling continuity has no three consecutive implementation tasks without automated verification.
- [ ] Wave 0 covers all missing references.
- [ ] No watch-mode flags are used as verification commands.
- [ ] Deferred UAT/live-provider checks remain recorded as deferred, not passed.
- [ ] `nyquist_compliant: true` is set only after task-level verification coverage is complete.

Approval: pending

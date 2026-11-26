---
phase: 36
slug: policy-gate-and-approval-ux
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-20
---

# Phase 36 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 and EF Core Sqlite 9.0.4 |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |
| **Estimated runtime** | ~45-180 seconds for focused API/WebApp boundary tests; solution test runtime varies by environment |

---

## Sampling Rate

- **After every task commit:** Run the quick command, narrowed further to the touched test class when possible.
- **After every plan wave:** Run `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` plus the quick command.
- **Before `$gsd-verify-work`:** Run `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` and focused cloud-policy/API/WebApp boundary tests.
- **Max feedback latency:** 180 seconds for focused feedback; do not substitute manual/live UAT for automated checks.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 36-01-01 | 01 | 1 | POLICY-01 | T36-01 | UI renders allow, warning, approval-required, denied, expired, mismatch, and redaction-blocked states without exposing raw generated DTOs or policy JSON. | source-boundary / repository | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal` | Missing - Wave 0 adds | pending |
| 36-02-01 | 02 | 1 | POLICY-02 | T36-02 | Warning acknowledgement and approved continuation pass only displayed decision/request identifiers and keep mismatched or expired scopes blocked. | API/service/repository | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Partial existing | pending |
| 36-03-01 | 03 | 2 | POLICY-03 | T36-03 | Approver approve/deny actions resolve durable approval requests with audit context and remain authorized server-side. | controller/service/repository | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyControllerTests" --verbosity minimal` | Partial existing | pending |
| 36-04-01 | 04 | 2 | POLICY-01, POLICY-02, POLICY-03 | T36-04 | `/servers` workflow buttons stay disabled until the matching policy gate state allows the action; no provider mutation is represented as successful before gate allow. | WebApp build / source-boundary | `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` | Existing build | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs` - source-boundary tests for required policy gate labels, component marker, disabled continuation behavior, no raw generated DTO or raw HTTP binding, and deferred-UAT copy.
- [ ] Repository facade tests for warning acknowledgement and approval continuation using safe models and allowlisted identifiers.
- [ ] Controller/service tests for approver approve/deny commands when missing generated client coverage is added or wrapped.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Manual authenticated browser UAT, live Hetzner provisioning, Steam validation, Arma runtime validation, provider teardown, and Phase 30 evidence | POLICY-01, POLICY-02, POLICY-03 | Deferred by explicit user instruction | Do not run during Phase 36; record as deferred in summaries and verification rather than claiming pass. |

---

## Validation Sign-Off

- [x] All task categories have an automated verify command or Wave 0 dependency.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 identifies missing source-boundary and repository tests.
- [x] No watch-mode flags.
- [x] Feedback latency target is under 180 seconds for focused commands.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-06-20 for automated Phase 36 planning; manual/live UAT remains deferred.

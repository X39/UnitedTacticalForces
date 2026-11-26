---
phase: 59
slug: lifecycle-log-and-status-feedback-repair
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-05
---

# Phase 59 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / Microsoft.NET.Test.Sdk plus existing UI test host |
| **Config file** | `X39.UnitedTacticalForces.slnx` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle|FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudInventoryVisibilityTests"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | repo-dependent; run targeted filters during tasks and full suite before phase verification |

---

## Sampling Rate

- **After every task commit:** Run the plan-specific targeted API/UI test command.
- **After every plan wave:** Run the quick API command plus the relevant UI filter when UI/mock backend files changed.
- **Before `$gsd-verify-work`:** `dotnet build X39.UnitedTacticalForces.slnx` and targeted API/UI tests must pass, or any
  failure must be classified as unrelated/pre-existing with exact output.
- **Max feedback latency:** use targeted filters first; broad solution tests are final evidence.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 59-01-01 | 01 | 1 | OPS-01, OPS-03 | T-59-01 | Status reasons are durable, owner-safe, and secret-redacted | unit/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle"` | yes | pending |
| 59-01-02 | 01 | 1 | OPS-01, OPS-03 | T-59-02 | Lifecycle DTO/repository labels replace raw enum/n/a display | source/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | yes | pending |
| 59-02-01 | 02 | 2 | OPS-02 | T-59-03 | Stop includes final log capture without user-facing collect action | unit/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle"` | yes | pending |
| 59-02-02 | 02 | 2 | OPS-02, OPS-03 | T-59-04 | Logs tab follows existing jobs and preserves redacted persisted logs | UI/source | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests"` | yes | pending |
| 59-03-01 | 03 | 3 | OPS-04 | T-59-05 | Provider teardown/cost risk outranks normal actions; safe failed actions remain available | source/UI | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | yes | pending |
| 59-03-02 | 03 | 3 | OPS-01, OPS-02, OPS-03, OPS-04 | T-59-06 | Mock UI states cover stuck/recovered/log/failed/cleanup/teardown labels without live secrets | UI | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"` | yes | pending |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements:

- `tests/Api.Tests/Api.Tests.csproj`
- `tests/Ui.Tests/Ui.Tests.csproj`
- `X39.UnitedTacticalForces.slnx`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Visual polish of status/log/job timeline at responsive widths | OPS-01, OPS-02, OPS-03 | Source/UI tests cannot fully judge dense table readability and no-overlap behavior | Use mock states in Phase 60 or local UI walkthrough; verify no visible `n/a`, no overlapping text, readable job reasons, and clear log freshness. |

---

## Validation Sign-Off

- [x] All tasks have automated verify or existing Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target uses targeted tests before full suite.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-07-05

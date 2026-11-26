---
phase: 58
slug: unified-server-workspace
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-05
---

# Phase 58 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / Microsoft.NET.Test.Sdk plus existing UI test host |
| **Config file** | `X39.UnitedTacticalForces.slnx` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WebApp|FullyQualifiedName~CloudTeardown|FullyQualifiedName~CloudCostVisibility|FullyQualifiedName~CloudPolicies"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | repo-dependent; run targeted tests during tasks and full suite before phase verification |

---

## Sampling Rate

- **After every task commit:** Run the plan-specific targeted API/UI test command.
- **After every plan wave:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj` and `dotnet test tests/Ui.Tests/Ui.Tests.csproj`.
- **Before `$gsd-verify-work`:** Full suite should be green or failures must be explicitly classified as legacy/out-of-scope.
- **Max feedback latency:** keep targeted task checks under the shortest practical command for the touched files.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 58-01-* | 01 | 1 | WORK-01 | T-58-01 | Workspace projection exposes only redacted server-scoped data | source/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WebApp|FullyQualifiedName~GameServer"` | yes | pending |
| 58-02-* | 02 | 2 | WORK-01, WORK-02 | T-58-02 | Overview actions and Logs tab do not expose secret/raw/generated payloads | UI/source | `dotnet test tests/Ui.Tests/Ui.Tests.csproj` | yes | pending |
| 58-03-* | 03 | 3 | WORK-03, WORK-04 | T-58-03 | Provider teardown is the only cost-stop action and admin diagnostics are permission-gated | API/UI | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudTeardown|FullyQualifiedName~CloudCostVisibility|FullyQualifiedName~CloudPolicies|FullyQualifiedName~CloudInventory"` | yes | pending |
| 58-04-* | 04 | 4 | WORK-01, WORK-02, WORK-03, WORK-04 | T-58-04 | Regression tests cover all workspace states and authorization boundaries | full | `dotnet test X39.UnitedTacticalForces.slnx` | yes | pending |

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
| Visual polish of selected-server workspace across responsive widths | WORK-01, WORK-02 | Automated source/UI tests cannot fully judge visual hierarchy | Use browser/mock states in Phase 60 or local UI walkthrough; verify no overlap, no user-facing collect logs action, clear cost-stop teardown label, and readable owner/admin states. |

---

## Validation Sign-Off

- [x] All tasks have automated verify or existing Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target uses targeted tests before full suite.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-07-05

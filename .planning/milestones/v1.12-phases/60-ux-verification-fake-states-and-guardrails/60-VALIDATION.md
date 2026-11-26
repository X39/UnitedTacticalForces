---
phase: 60
slug: ux-verification-fake-states-and-guardrails
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-05
---

# Phase 60 - Validation Strategy

> Per-phase validation contract for mock-backed UX verification and redaction guardrails.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / Microsoft.NET.Test.Sdk plus Playwright UI tests |
| **Config file** | `X39.UnitedTacticalForces.slnx` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | repo-dependent; run targeted filters during tasks and full build/test before final handoff |

---

## Sampling Rate

- **After every task commit:** Run the plan-specific targeted API or UI command.
- **After every plan wave:** Run the quick API command plus `CloudAgentUiTests` when UI mock backend or browser tests
  changed.
- **Before `$gsd-verify-work`:** `dotnet build X39.UnitedTacticalForces.slnx`, targeted API/UI filters, and final
  redaction scans must pass, or any failure must be classified with exact output.
- **Max feedback latency:** targeted filters first; broad solution test is final evidence if runtime permits.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 60-01-01 | 01 | 1 | VER-01, VER-02 | T-60-01 | Fake state catalog has explicit labels/actions and generic Arma-compatible boundaries | source/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~ServerWorkspaceBoundaryTests"` | yes | pending |
| 60-01-02 | 01 | 1 | VER-02, VER-04 | T-60-02 | Mock backend uses fake-safe state data and no secret-like sentinels | UI/source | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"` | yes | pending |
| 60-02-01 | 02 | 2 | VER-02, VER-03 | T-60-03 | Coherent browser journey proves creation-to-workspace meaning without live mutation | UI | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"` | yes | pending |
| 60-02-02 | 02 | 2 | VER-03 | T-60-04 | Desktop/mobile walkthrough evidence has stable DOM assertions and screenshot artifacts | UI | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"` | yes | pending |
| 60-03-01 | 03 | 3 | VER-01, VER-04 | T-60-05 | Repository/generated-client boundaries and redaction scans fail on unsafe raw exposure | source/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~UserFacingErrorBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | yes | pending |
| 60-03-02 | 03 | 3 | VER-02, VER-03, VER-04 | T-60-06 | Final evidence names mock/fake scope and keeps live UAT deferred | docs/build/test | `dotnet build X39.UnitedTacticalForces.slnx` | yes | pending |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements:

- `tests/Api.Tests/Api.Tests.csproj`
- `tests/Ui.Tests/Ui.Tests.csproj`
- `tests/Ui.Tests/WebAppHostFixture.cs`
- `tests/Ui.Tests/GameServerUiMockBackend.cs`
- `X39.UnitedTacticalForces.slnx`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Hetzner provisioning, Steam/Arma runtime, authenticated operator walkthrough, provider teardown, and final billable count evidence | VER-03, VER-04 | Deferred by operator direction until whole-CR readiness and cost-controlled live UAT resumption | Do not perform in Phase 60. Final evidence must state these remain deferred. |

---

## Validation Sign-Off

- [x] All tasks have automated verify or existing Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target uses targeted tests before full suite.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-07-05

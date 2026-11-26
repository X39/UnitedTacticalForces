---
phase: 41
slug: cloud-template-domain-and-audit-foundation
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-21
---

# Phase 41 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 with EF Core SQLite |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplate" --no-restore --verbosity normal` |
| **Full suite command** | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal` |
| **Estimated runtime** | ~60 seconds for focused tests; solution build varies by restore state |

---

## Sampling Rate

- **After every task commit:** Run the focused `CloudServerTemplate` tests or the narrow test class named by the task.
- **After every plan wave:** Run `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal`.
- **Before `$gsd-verify-work`:** Focused template tests and solution build must be green.
- **Max feedback latency:** 120 seconds for focused tests when packages are restored.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 41-01-01 | 01 | 1 | TPL-01 | T-41-01 | Version snapshots preserve history without provider calls. | build | `dotnet build sources/Api/Api.csproj --no-restore --verbosity normal` | yes | pending |
| 41-01-02 | 01 | 1 | TPL-01 | T-41-02 | Migration and model indexes enforce one current version per template. | build | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal` | yes | pending |
| 41-02-01 | 02 | 2 | TPL-01, TPL-04 | T-41-03 | Service validates bounds, creates new versions, disables by version, and appends sanitized audit. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateServiceTests" --no-restore --verbosity normal` | yes | pending |
| 41-02-02 | 02 | 2 | TPL-01, TPL-04 | T-41-04 | Admin controller endpoints require server admin authorization and return safe DTOs. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateControllerTests" --no-restore --verbosity normal` | yes | pending |
| 41-03-01 | 03 | 3 | TPL-01, TPL-04 | T-41-05 | Invalid bounds and forbidden vocabulary are rejected or sanitized. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateRedactionTests" --no-restore --verbosity normal` | yes | pending |
| 41-03-02 | 03 | 3 | TPL-01, TPL-04 | T-41-06 | Full template suite covers versioning, auth metadata, audit, and redaction. | unit/build | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplate" --no-restore --verbosity normal` | yes | pending |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

---

## Manual-Only Verifications

All Phase 41 behaviors have automated verification. Live provider, browser, Steam, and provisioning evidence are outside this phase.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s for focused tests
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-21

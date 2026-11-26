---
phase: 35
slug: dry-run-planning-and-review
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-20
---

# Phase 35 - Validation Strategy

> Per-phase validation contract for automated feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x with Microsoft.NET.Test.Sdk and EF Core SQLite-style API tests |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReview|FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal` |
| **Full suite command** | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |
| **Estimated runtime** | Focused tests/build should remain suitable for per-wave feedback; broad solution tests may include unrelated legacy failures and must be recorded separately if run. |

---

## Sampling Rate

- **After every task commit:** Run the focused automated command named in that task's `<verify><automated>` block.
- **After every wave:** Run the combined focused Phase 35 test command when any API, repository, or UI source changed.
- **Before phase verification:** Run the combined focused Phase 35 tests and `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`.
- **Manual/live UAT boundary:** Browser, authenticated-session, live Hetzner, Steam, Arma runtime, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by operator instruction and must not be claimed as passed.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------------|-----------|-------------------|-------------|--------|
| 35-01-T1 | 35-01 | 1 | PLAN-01, PLAN-03 | Providerless dry-run generation persists non-secret review state and supersession evidence. | xUnit API/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewStateTests" --verbosity minimal` | planned | pending |
| 35-01-T2 | 35-01 | 1 | PLAN-01, PLAN-03 | Fingerprint/safe-summary persistence and supersession remain providerless. | xUnit API/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewStateTests|FullyQualifiedName~Arma3CloudPlannerTests" --verbosity minimal` | planned | pending |
| 35-02-T1 | 35-02 | 2 | PLAN-02, PLAN-03 | Review DTO contracts expose typed safe review fields only. | xUnit API/source | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewApiTests" --verbosity minimal` | planned | pending |
| 35-02-T2 | 35-02 | 2 | PLAN-02, PLAN-03 | Review endpoint classifies unusable plans without creating provider resources. | xUnit API/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewApiTests|FullyQualifiedName~Arma3CloudPlanReviewStateTests" --verbosity minimal` | planned | pending |
| 35-03-T1 | 35-03 | 2 | PLAN-03 | Superseded and input-mismatched plans exit before provider client calls. | xUnit API/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests" --verbosity minimal` | existing plus planned | pending |
| 35-03-T2 | 35-03 | 2 | PLAN-03 | Provisioning validation rejects stale, invalid, mismatched, superseded, and owner-mismatched plans before token/provider access. | xUnit API/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~Arma3CloudPlanReviewStateTests" --verbosity minimal` | existing plus planned | pending |
| 35-04-T1 | 35-04 | 3 | PLAN-01, PLAN-02, PLAN-03 | Generated Kiota client surface is regenerated, not hand-authored, and compiles. | build | `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal` | generated | pending |
| 35-04-T2 | 35-04 | 3 | PLAN-02 | Generated review artifacts do not introduce forbidden raw/secret owner-facing fields. | source scan | Exact PowerShell scan command in `35-04-PLAN.md`; exits `0` on no hits and non-zero on forbidden hits. | generated | pending |
| 35-05-T1 | 35-05 | 4 | PLAN-02, PLAN-03 | Repository boundary tests reject raw/generated/secret display surfaces. | xUnit source-boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal` | planned | pending |
| 35-05-T2 | 35-05 | 4 | PLAN-01, PLAN-02, PLAN-03 | Repository facade maps generated DTOs to hand-written safe review models before Razor. | xUnit source-boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal` | planned | pending |
| 35-06-T1 | 35-06 | 5 | PLAN-02, PLAN-03 | `/servers` UI boundary tests reject raw DTOs, forbidden fields, and live-UAT claims. | xUnit source-boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal` | planned | pending |
| 35-06-T2 | 35-06 | 5 | PLAN-01, PLAN-02, PLAN-03 | `/servers` renders the non-mutating dry-run review workflow and keeps provider mutation unavailable. | xUnit source-boundary/build | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal` | planned | pending |
| 35-06-T3 | 35-06 | 5 | PLAN-01, PLAN-02, PLAN-03 | Final automated evidence covers generation, review, blocking, redaction, and build without live UAT claims. | focused tests/build | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReview|FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests" --verbosity minimal`; `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` | planned | pending |

---

## Wave 0 Requirements

- [ ] Create focused API/service tests for review-state persistence, owner-safe review projection, and supersession before or alongside implementation.
- [ ] Extend provisioning tests so every invalid/stale/mismatched/superseded/owner-mismatched state exits before provider token resolution or provider client calls.
- [ ] Extend WebApp source-boundary tests before rendering the `/servers` review panel.
- [ ] Use existing xUnit, EF Core SQLite, source-scan, and build infrastructure; no new package or live-provider dependency is required.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Authenticated browser walkthrough of `/servers` dry-run review | PLAN-01 through PLAN-03 | User explicitly deferred all UAT for this autonomous run. | Deferred evidence: record as not run and cite the user instruction. |
| Live Hetzner, Steam, Arma, provider provisioning, teardown, and Phase 30 evidence | PLAN-01 through PLAN-03 | Live provider/game/authenticated UAT is out of scope and explicitly deferred. | Deferred evidence: do not run; verify no provider mutation is introduced by Phase 35. |

---

## Validation Sign-Off

- [x] Every implementation task has an automated verification command.
- [x] Sampling continuity: no 3 consecutive implementation tasks proceed without automated feedback.
- [x] Wave 0 covers planned test additions before or alongside implementation.
- [x] No watch-mode flags.
- [x] Manual/live UAT deferral is explicit and not counted as passed.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-06-20 for planning.

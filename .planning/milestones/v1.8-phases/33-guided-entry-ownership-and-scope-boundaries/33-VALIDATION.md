---
phase: 33
slug: guided-entry-ownership-and-scope-boundaries
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-19
---

# Phase 33 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x with Microsoft.NET.Test.Sdk |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~GameServerLifecycle"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | ~180 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~GameServerLifecycle"` when API behavior changes; run `dotnet build X39.UnitedTacticalForces.slnx` for WebApp-only changes.
- **After every plan wave:** Run `dotnet test X39.UnitedTacticalForces.slnx`.
- **Before `$gsd-verify-work`:** Full solution test command must be green, or failures must be recorded as unrelated/pre-existing with evidence.
- **Max feedback latency:** 180 seconds for quick API checks; 300 seconds for full solution build/test.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 33-TBD-01 | TBD | TBD | CREATE-01 | T33-01 | `/servers` exposes the owner/operator guided entry only to authorized users. | build/source/API test | `dotnet build X39.UnitedTacticalForces.slnx` plus focused tests if endpoint logic changes | yes | pending |
| 33-TBD-02 | TBD | TBD | CREATE-02 | T33-02 | Existing server eligibility and new Arma 3 planning create path are enforced server-side, not by client-only filtering. | API test | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~GameServerLifecycle"` | partial | pending |
| 33-TBD-03 | TBD | TBD | CREATE-03 | T33-03 | Active billing owner and assignment are required before dry-run/provision continuation. | API test | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3Cloud|FullyQualifiedName~BillingOwnership"` | partial | pending |
| 33-TBD-04 | TBD | TBD | CREATE-04 | T33-04 | Cloud planning inputs validate size, location, storage, ports, SSH/admin CIDR, modpack, and runtime basics before continuation. | API/build test | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning"` | partial | pending |

*Status: pending until planner assigns concrete plan/task IDs.*

---

## Wave 0 Requirements

- [ ] Add focused API tests for any new owner-scoped billing-owner, eligibility, options, or readiness endpoints before or alongside implementation.
- [ ] Add WebApp build coverage for `/servers` guided entry changes through `dotnet build X39.UnitedTacticalForces.slnx`.
- [ ] Existing infrastructure covers xUnit, EF Core SQLite-style API tests, and solution build/test execution.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Authenticated browser walkthrough of `/servers` guided entry | CREATE-01 through CREATE-04 | User explicitly deferred all UAT for this autonomous run. | Deferred evidence: record as not run and cite the user instruction. |
| Live Hetzner, Steam, Arma, and Phase 30 checks | CREATE-01 through CREATE-04 | Live provider/game/authenticated UAT is out of scope for Phase 33 and explicitly deferred. | Deferred evidence: do not run; verify no provider mutation is introduced. |

---

## Validation Sign-Off

- [x] All tasks have planned automated verify commands or Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive implementation tasks should proceed without automated build/test feedback.
- [x] Wave 0 covers missing test references for new API authorization/readiness surfaces.
- [x] No watch-mode flags.
- [x] Feedback latency target documented.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-06-19 for planning; executor must update task IDs when concrete plans exist.

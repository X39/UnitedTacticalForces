---
phase: 17
slug: runtime-cost-snapshot-foundation
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-17
---

# Phase 17 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | ~120 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests`
- **After every plan wave:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj`
- **Before `$gsd-verify-work`:** `dotnet build X39.UnitedTacticalForces.slnx` and `dotnet test X39.UnitedTacticalForces.slnx` must be green or documented with environment-specific failures
- **Max feedback latency:** 120 seconds for the quick targeted command

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 17-01-01 | 01 | 1 | COST-09 | T-17-01 | Cost summaries are estimated from durable snapshot contributions only | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | missing W0 | pending |
| 17-01-02 | 01 | 1 | COST-10 | T-17-02 | Historical cost remains attributed to the billing owner active for each interval | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | missing W0 | pending |
| 17-01-03 | 01 | 1 | COST-11 | T-17-03 | Provider deletion timestamp, not runtime or VM power state, stops billable cost | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | missing W0 | pending |
| 17-01-04 | 01 | 1 | COST-12 | T-17-04 | Snapshot metadata exposes assumptions and confidence without secrets | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | missing W0 | pending |
| 17-01-05 | 01 | 1 | RELY-14 | T-17-05 | Repeated refresh after restart is idempotent and does not duplicate totals | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | missing W0 | pending |

*Status: pending, green, red, flaky.*

---

## Wave 0 Requirements

- [ ] `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - covers COST-09, COST-10, COST-11, COST-12, and RELY-14.
- [ ] EF migration under `sources/Api/Data/Migrations` for runtime cost snapshots and resource contributions.
- [ ] Optional test helper extraction for repeated SQLite `DateTimeOffsetToBinaryConverter` setup if duplicated setup becomes noisy.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| PostgreSQL migration apply against Aspire database | COST-09 | Local PostgreSQL availability is environment-dependent | Run Aspire or configured PostgreSQL, then run the API migration path and confirm the new cost snapshot tables exist. |

---

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency < 120s for targeted cost tests
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

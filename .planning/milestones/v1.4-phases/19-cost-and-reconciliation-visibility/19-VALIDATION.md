---
phase: 19
slug: cost-and-reconciliation-visibility
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-18
---

# Phase 19 - Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 |
| API test project | `tests/Api.Tests/Api.Tests.csproj` |
| API build command | `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal -p:NuGetAudit=false` |
| WebApp build command | `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal -p:NuGetAudit=false` |
| Phase API quick command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudCostVisibility --no-restore --verbosity minimal -p:NuGetAudit=false` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal -p:NuGetAudit=false` |
| Estimated runtime | 120-240 seconds, excluding NuGet/network restore blockers |

## Current Environment Caveat

The current session has a known NuGet/network blocker: API/test builds attempt to reach
`https://api.nuget.org/v3/index.json` and stop with `NU1301` because socket access to
`api.nuget.org:443` is denied, including escalated retries. Verification must distinguish this restore
blocker from code compilation or test assertion failures.

## Sampling Rate

- After 19-01: run Phase API quick command and API build.
- After 19-02: run WebApp build, generated-client static checks, and raw-HTTP bridge static checks.
- After 19-03: run WebApp build plus static UI label checks, then API quick command if restore is available.
- Before phase verification: run full suite if NuGet access is available; otherwise record the `NU1301`
  blocker with exact command output summary.

## Per-Task Verification Map

| Task ID | Plan | Requirement | Secure Behavior | Test Type | Automated Command | Status |
|---------|------|-------------|-----------------|-----------|-------------------|--------|
| 19-01-01 | 01 | COST-05/COST-08 | Cost DTOs expose source labels, freshness, confidence, and contributions without secrets | API unit | Phase API quick command | pending |
| 19-01-02 | 01 | VIS-12/VIS-13 | Admin sees all relevant findings; server owner sees only scoped findings | controller/service unit | Phase API quick command | pending |
| 19-01-03 | 01 | VIS-14 | Provider teardown and billable cleanup risk are distinct in DTOs | API unit/static | Phase API quick command plus `rg` checks | pending |
| 19-02-01 | 02 | VIS-15 | Generated Kiota builders/models exist for new endpoints | WebApp build/static | WebApp build plus `rg "CostVisibility|Reconciliation"` in `UtfApi` | pending |
| 19-02-02 | 02 | VIS-15 | Repository facades call generated builders for new surfaces | static/WebApp build | `rg -n "GetFromJsonAsync|PostAsJsonAsync|ResolveApiUrl" sources/WebApp/Services` | pending |
| 19-03-01 | 03 | VIS-12 | Admin cloud operations shows cost, reconciliation, orphan, and cleanup risk | WebApp build/static | WebApp build plus UI label `rg` checks | pending |
| 19-03-02 | 03 | VIS-13/VIS-14 | Owner server detail shows scoped cost/risk and cost-stop boundary copy | WebApp build/static | WebApp build plus UI label `rg` checks | pending |

## Wave 0 Requirements

- [ ] API tests cover cost visibility DTO composition and source labels.
- [ ] API tests cover server-scoped reconciliation filtering.
- [ ] WebApp generated client includes reconciliation and runtime cost visibility builders/models.
- [ ] WebApp repositories expose cost/reconciliation methods without adding raw browser HTTP bridges.
- [ ] UI copy includes estimated/reconciled/adjusted source labels and avoids invoice-grade actual-cost wording.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Authenticated admin `/servers/cloud` with representative cloud data | VIS-12 | Requires authenticated admin session and seeded/live cloud records | Open `/servers/cloud`, verify cost/reconciliation/risk cards and tabs render accurately. |
| Authenticated server-owner `/servers` detail with scoped cloud data | VIS-13/VIS-14 | Requires authenticated owner claims and representative server data | Open `/servers`, select a cloud-backed server, verify only scoped findings appear and cost-stop copy is visible. |
| Live provider reconciliation followed by UI refresh | VIS-12 | Requires operator-approved Hetzner token and cleanup window | Trigger reconciliation, refresh UI, verify counts/finding badges match persisted run and no deletion occurs. |

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target is below 240 seconds for targeted tests when restore is available
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending


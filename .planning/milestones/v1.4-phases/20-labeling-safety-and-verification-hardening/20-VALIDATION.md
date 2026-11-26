---
phase: 20
slug: labeling-safety-and-verification-hardening
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-18
---

# Phase 20 - Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 |
| API test project | `tests/Api.Tests/Api.Tests.csproj` |
| API build command | `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal -p:NuGetAudit=false` |
| WebApp build command | `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal -p:NuGetAudit=false` |
| Phase API quick command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudProviderReconciliationServiceTests|FullyQualifiedName~CloudCost" --no-restore --verbosity minimal -p:NuGetAudit=false` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal -p:NuGetAudit=false` |

## Current Environment Caveat

The current session has a known NuGet/network blocker: API/test builds attempt to reach
`https://api.nuget.org/v3/index.json` and stop with `NU1301` because socket access to
`api.nuget.org:443` is denied, including escalated retries. Verification must distinguish this restore
blocker from code compilation or test assertion failures.

## Per-Plan Verification Map

| Task ID | Plan | Requirement | Secure Behavior | Test Type | Automated Command | Status |
|---------|------|-------------|-----------------|-----------|-------------------|--------|
| 20-01-01 | 01 | HETZ-12 | Create payloads include safe labels for all billable/provider resources | API unit/fake HTTP | `CloudProvisioningServiceTests`, `HetznerCloudClientTests` | static passed; API test blocked by NU1301 |
| 20-01-02 | 01 | RELY-15 | Labels omit tokens, credentials, user-data, and free-form secret material | API unit/static | secret-term scans and provisioning tests | static passed; API test blocked by NU1301 |
| 20-02-01 | 02 | RELY-15 | Cost/reconciliation outputs and audits do not expose secret-bearing text | API unit/static | static passed; API test blocked by NU1301 |
| 20-02-02 | 02 | RELY-16 | Reconciliation remains read-only and avoids delete/detach calls | reflection/unit | static passed; API test blocked by NU1301 |
| 20-03-01 | 03 | RELY-16 | Coverage matrix ties existing and new tests to each requested behavior | artifact review | `20-VERIFICATION.md` | complete |
| 20-03-02 | 03 | OPS-02 boundary | Live provider/browser UAT remains operator-gated | artifact review | `20-VERIFICATION.md` | complete |

## Wave 0 Requirements

- [x] Provider create requests carry UTF labels.
- [x] Provider request-body tests prove labels are posted for server, IP, volume, and firewall creates.
- [x] Redaction tests cover provider tokens, bearer headers, bootstrap credentials, and user-data text.
- [x] Read-only reconciliation guards cover delete and detach methods.
- [x] Final verification artifact documents existing coverage and remaining operator-gated UAT.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Hetzner create/delete UAT with labels visible provider-side | HETZ-12/OPS-02 | Requires approved Hetzner token, budget, and cleanup window | Provision a session, inspect provider labels, delete all resources, and record final billable resource count `0`. |
| Authenticated admin and owner browser UAT | RELY-16/OPS-02 | Requires real authenticated admin/owner sessions and representative cloud data | Open `/servers/cloud` and `/servers`, verify scoped cost/reconciliation data and no secret material. |

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target is below 240 seconds for targeted tests when restore is available
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

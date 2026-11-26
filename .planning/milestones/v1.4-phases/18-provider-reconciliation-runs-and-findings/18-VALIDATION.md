---
phase: 18
slug: provider-reconciliation-runs-and-findings
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-18
---

# Phase 18 - Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 |
| API test project | `tests/Api.Tests/Api.Tests.csproj` |
| Provider quick command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore --verbosity minimal` |
| Reconciliation quick command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudProviderReconciliation --no-restore --verbosity minimal` |
| API build command | `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |
| Estimated runtime | 120-180 seconds, excluding NuGet/network restore blockers |

## Sampling Rate

- After 18-01: run the provider quick command and API build.
- After 18-02: run API build and targeted reconciliation schema/static checks.
- After 18-03: run the reconciliation quick command, provider quick command, API build, and full solution test command.
- Before phase verification: document any NuGet/network `NU1301` restore blockers separately from compile/test failures.

## Per-Task Verification Map

| Task ID | Plan | Requirement | Secure Behavior | Test Type | Automated Command | Status |
|---------|------|-------------|-----------------|-----------|-------------------|--------|
| 18-01-01 | 01 | HETZ-09 | Provider list boundary reads paged resources without mutation calls | provider unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore --verbosity minimal` | pending |
| 18-02-01 | 02 | HETZ-10 | Runs persist status, actor, counts, and sanitized failure fields | build/schema | `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` | pending |
| 18-02-02 | 02 | HETZ-11 | Findings persist known attribution and safe orphan references | build/schema | `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` | pending |
| 18-03-01 | 03 | HETZ-08 | Admin trigger records completed or failed reconciliation runs | service/controller unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudProviderReconciliation --no-restore --verbosity minimal` | pending |
| 18-03-02 | 03 | HETZ-11 | Findings cover unknown provider, missing provider, state mismatch, and cleanup-needed billable resources | service unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudProviderReconciliation --no-restore --verbosity minimal` | pending |
| 18-03-03 | 03 | HETZ-11/RELY-15 | Reconciliation never calls provider delete/detach/create APIs and persists no secret material | service/provider unit + static | provider and reconciliation quick commands plus `rg` checks | pending |

## Wave 0 Requirements

- [ ] `tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` covers live list pagination, mapping, and GET-only behavior.
- [ ] `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationServiceTests.cs` covers run persistence, finding categories, attribution, failure sanitization, and no mutation.
- [ ] `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs` or equivalent reflection coverage checks admin-only reconciliation endpoints.
- [ ] EF migration exists for reconciliation runs and findings.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Hetzner reconciliation against a real project | HETZ-08/HETZ-09 | Requires operator-provided token, budget, and project safety window | Configure a safe Hetzner token, trigger a reconciliation run, confirm counts/findings, and verify no provider resources are mutated. |
| PostgreSQL migration apply against Aspire database | HETZ-10/HETZ-11 | Local PostgreSQL availability is environment-dependent | Run Aspire or configured PostgreSQL, start the API migration path, and confirm reconciliation tables exist. |

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target is below 180 seconds for targeted tests
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending


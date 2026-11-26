---
phase: 56
slug: generic-runtime-template-and-plan-snapshots
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-04
---

# Phase 56 - Validation Strategy

> Per-phase validation contract for generic selectors, dry-run/review models, snapshots, worker recipes, and stale-plan safety.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit via `Microsoft.NET.Test.Sdk` |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj`, `tests/Ui.Tests/Ui.Tests.csproj`, `X39.UnitedTacticalForces.slnx` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudWorker|FullyQualifiedName~WebApp"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | ~90-240 seconds, depending on build cache |

---

## Sampling Rate

- **After every task commit:** Run the plan-local focused `dotnet test` filter plus any source scan listed in that task.
- **After every plan wave:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudWorker|FullyQualifiedName~CloudProvisioning|FullyQualifiedName~CloudPolicies|FullyQualifiedName~WebApp"`.
- **Before `$gsd-verify-work`:** Run `dotnet test X39.UnitedTacticalForces.slnx`.
- **Max feedback latency:** 240 seconds for focused checks; broad suite may exceed this and should be recorded exactly.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 56-01-01 | 01 | 1 | GEN-03 | T-56-01 | Selectors derive game scope server-side and do not expose secrets. | unit/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GuidedCloudEntryApiTests|FullyQualifiedName~GameServerCapabilityContractTests"` | yes | pending |
| 56-01-02 | 01 | 1 | GEN-03, GEN-04 | T-56-02 | Generic runtime/mission/profile persistence preserves Arma data through generic fields. | unit/build | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3RuntimeProfile|FullyQualifiedName~CloudPlanning"` | yes | pending |
| 56-01-03 | 01 | 1 | GEN-03 | T-56-03 | WebApp selector models are generic and Razor-safe. | boundary/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TemplateProfileRepositoryBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~GenericGameHostingContractBoundaryTests"` | yes | pending |
| 56-02-01 | 02 | 2 | GEN-03, GEN-04 | T-56-04 | Dry-run and review responses are generic while provider validation remains enforced. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3CloudPlannerTests|FullyQualifiedName~Arma3CloudPlanReviewApiTests|FullyQualifiedName~Arma3CloudPlanReviewStateTests"` | yes | pending |
| 56-02-02 | 02 | 2 | GEN-03 | T-56-05 | Provider and runtime fingerprints cannot under-block or over-block stale state. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudProvisioning"` | yes | pending |
| 56-02-03 | 02 | 2 | GEN-03 | T-56-06 | WebApp review/policy models remain redacted, generic, and generated-client backed. | boundary/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests"` | yes | pending |
| 56-03-01 | 03 | 3 | GEN-03, GEN-04 | T-56-07 | Worker claim payloads use a generic recipe envelope and allowlisted Arma body. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkerDispatch|FullyQualifiedName~CloudWorkerApiClient|FullyQualifiedName~CloudWorkerRuntime"` | yes | pending |
| 56-03-02 | 03 | 3 | GEN-04 | T-56-08 | Audit, policy, provisioning, and reconciliation retain cost/owner/provider gates after snapshot changes. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudProvisioning|FullyQualifiedName~CloudPolicies|FullyQualifiedName~CloudReconciliation"` | yes | pending |
| 56-03-03 | 03 | 3 | GEN-03, GEN-04 | T-56-09 | Public Arma names are removed from user-facing selector/dry-run/review/worker contracts except narrow internals. | source scan/full | `dotnet test X39.UnitedTacticalForces.slnx` | yes | pending |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements:

- `tests/Api.Tests/Api.Tests.csproj` already references API and CloudWorker projects.
- `tests/Ui.Tests/Ui.Tests.csproj` already covers browser-level mock UI tests where needed.
- Existing test folders cover cloud planning, worker dispatch/runtime, provisioning, policies, runtime profiles, WebApp boundaries, and UI mock backend behavior.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Hetzner provisioning, SteamCMD, and native Arma runtime | GEN-04 | Live UAT remains deferred by project state and cost-control direction. | Do not run during Phase 56. Record as deferred UAT only if relevant. |

---

## Validation Sign-Off

- [x] All tasks have automated verification or existing Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verification.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target documented.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** pending execution

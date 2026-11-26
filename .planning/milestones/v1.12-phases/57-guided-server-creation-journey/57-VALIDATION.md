---
phase: 57
slug: guided-server-creation-journey
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-05
---

# Phase 57 - Validation Strategy

> Per-phase validation contract for the guided server creation journey, workspace handoff, and no-intermediate-save guarantee.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit via `Microsoft.NET.Test.Sdk`; Playwright through `tests/Ui.Tests` |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj`, `tests/Ui.Tests/Ui.Tests.csproj`, `X39.UnitedTacticalForces.slnx` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~GuidedCloudEntryApiTests|FullyQualifiedName~GenericGameHostingContractBoundaryTests"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` |
| **Estimated runtime** | ~90-240 seconds for focused API/WebApp tests; UI tests may take longer |

---

## Sampling Rate

- **After every task commit:** Run the plan-local focused command listed in the task and any source scan in that task.
- **After every plan wave:** Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~GuidedCloudEntryApiTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests|FullyQualifiedName~GenericGameHostingContractBoundaryTests"`.
- **Before `$gsd-verify-work`:** Run `dotnet test X39.UnitedTacticalForces.slnx` and the focused `tests/Ui.Tests` creation/workspace filters.
- **Max feedback latency:** 240 seconds for focused checks; record exact duration and failures for UI/browser runs.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 57-01-01 | 01 | 1 | CREATE-01 | T-57-01 | Wizard state transitions do not call save APIs. | boundary/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests"` | yes | complete |
| 57-01-02 | 01 | 1 | CREATE-01, CREATE-02 | T-57-02 | Hosting choices are game/capability-aware and task-worded. | boundary/source scan | `rg -n "ServerCreationMode|Server record|Local server|Cloud server" sources/WebApp/Pages/Servers/Create.razor tests/Api.Tests/WebApp` | yes | complete |
| 57-01-03 | 01 | 1 | CREATE-04 | T-57-03 | Validation targets exact steps/fields without saving setup state. | boundary/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests"` | yes | complete |
| 57-02-01 | 02 | 2 | CREATE-01, CREATE-02 | T-57-04 | Create page renders the approved wizard labels and task copy. | UI/boundary | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage"` | yes | complete |
| 57-02-02 | 02 | 2 | CREATE-04 | T-57-05 | Missing choices and disabled options are visible with fix targets. | UI/boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests"` | yes | complete |
| 57-02-03 | 02 | 2 | CREATE-02 | T-57-06 | Normal creation copy does not lead with dry-run/lifecycle/policy internals. | source scan | `rg -n "Record only|Local install|Cloud planning|Create cloud planning server|dry-run" sources/WebApp/Pages/Servers/Create.razor sources/WebApp/Properties/Language.resx` | yes | complete |
| 57-03-01 | 03 | 3 | CREATE-03 | T-57-07 | Cloud creation opens workspace and offers a non-persistent continuation link. | UI | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage_Cloud"` | yes | complete |
| 57-03-02 | 03 | 3 | CREATE-03, CREATE-04 | T-57-08 | Advanced setup consumes query defaults without creating dry-run/review records. | API/UI | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GuidedCloudEntryApiTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests"` | yes | complete |
| 57-03-03 | 03 | 3 | CREATE-04 | T-57-09 | Policy/stale states show `Review cloud setup` next actions. | boundary/source scan | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PolicyGateApprovalUxBoundaryTests|FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests"` | yes | complete |
| 57-04-01 | 04 | 4 | CREATE-01, CREATE-02 | T-57-10 | Tests fail if mode-first labels or intermediate save behavior return. | boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerCreationBoundaryTests"` | yes | complete |
| 57-04-02 | 04 | 4 | CREATE-03, CREATE-04 | T-57-11 | UI tests cover workspace handoff, continuation, blockers, stale/policy copy, and success paths. | UI | `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage|FullyQualifiedName~AdvancedOperationsPage|FullyQualifiedName~ServerDetail"` | yes | complete |
| 57-04-03 | 04 | 4 | CREATE-01-04 | T-57-12 | Broad build/test remains green or documents existing unrelated failures. | build/full | `dotnet build X39.UnitedTacticalForces.slnx` | yes | complete |

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements:

- `tests/Api.Tests/Api.Tests.csproj` already includes API and WebApp boundary test buckets.
- `tests/Ui.Tests/Ui.Tests.csproj` already includes Playwright browser tests and a mock backend.
- `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` already guards no-provider-side-effect cloud creation/readiness behavior.
- `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs` already reads creation page and repository source.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Hetzner provisioning, SteamCMD install, and native Arma runtime launch | CREATE-03, CREATE-04 | Live provider/runtime UAT remains deferred by milestone constraints. | Do not run during Phase 57. Use mock backend and existing no-provider-mutation API tests. |

---

## Validation Sign-Off

- [x] All tasks have automated verification or existing Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verification.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target documented.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** complete - Phase 57 automated validation passed on 2026-07-05.

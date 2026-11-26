# Phase 60: UX Verification, Fake States, And Guardrails - Pattern Map

**Generated:** 2026-07-05
**Status:** Ready for planning

## Pattern Mapping Complete

## Closest Existing Analogs

| Target Concern | Closest Existing File | Pattern To Reuse |
|----------------|-----------------------|------------------|
| Fake API routing | `tests/Ui.Tests/GameServerUiMockBackend.cs` | Route-level Playwright backend, `MockServerState`, request counting, fake server/job/log/cloud state builders |
| Browser journey tests | `tests/Ui.Tests/CloudAgentUiTests.cs` | `StartSessionAsync`, `OpenAsync`, `AssertVisibleTextAsync`, `AssertHiddenTextAsync`, route/action assertions |
| UI host setup | `tests/Ui.Tests/WebAppHostFixture.cs` | WebApp host startup and optional `UTF_UI_TEST_BASE_URL` reuse |
| Creation boundary source tests | `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs` | Source assertions for no intermediate persistence and no Arma-only public markers |
| Generic hosting boundary tests | `tests/Api.Tests/WebApp/GenericGameHostingContractBoundaryTests.cs` | Generic capability/repository guardrails and no generated-client boundary drift |
| Workspace boundary tests | `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` | Source assertions for action labels, target sections, diagnostics, no `Collect logs`, no `n/a` |
| Workflow/redaction tests | `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | Repository mapping and redaction sentinel checks |
| Redaction UX tests | `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` | Unsafe marker and forbidden raw-field scans |

## Concrete Code Excerpts To Preserve

| File | Existing Marker | Reuse Guidance |
|------|-----------------|----------------|
| `GameServerUiMockBackend.cs` | `MockServerState`, `LifecycleJob`, `CloudRuntime`, `CloudStatus`, `CloudCostVisibility`, `CloudPolicyVisibility` builders | Add explicit state catalog helpers instead of duplicating fake API routing |
| `CloudAgentUiTests.cs` | `AssertVisibleTextAsync`, `AssertHiddenTextAsync`, `OpenAsync`, create-flow tests | Build the coherent walkthrough and state matrix from existing helper style |
| `ServerWorkspaceBoundaryTests.cs` | `AssertActionTargets`, `ExtractSourceBetween`, exact `DoesNotContain` markers | Expand exact source guardrails rather than replacing with broad grep |
| `CloudWorkflowRepositoryBoundaryTests.cs` | Redaction guard and action-ranking regression tests | Add exact fake/live evidence and unsafe sentinel expectations |
| `Details.razor` | Workspace tabs, primary action shell, diagnostics allowlist | Browser tests should assert existing user-facing surfaces, not a new test-only page |

## File Roles For Planning

### UI Mock And Browser Tests

- `tests/Ui.Tests/GameServerUiMockBackend.cs`
- `tests/Ui.Tests/CloudAgentUiTests.cs`
- `tests/Ui.Tests/WebAppHostFixture.cs`

Add fake state catalog, fixture expectations, coherent browser walkthrough, focused state matrix checks, responsive
checks, and screenshot/evidence artifact helpers here.

### Source/API Boundary Tests

- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs`
- `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs`
- `tests/Api.Tests/WebApp/GenericGameHostingContractBoundaryTests.cs`
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`
- `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs`
- `tests/Api.Tests/WebApp/UserFacingErrorBoundaryTests.cs`
- `tests/Api.Tests/GameServerCapabilities/GameServerCapabilityContractTests.cs`
- `tests/Api.Tests/GameServerLifecycle/LifecycleWorkerRegistryServiceTests.cs`

Protect repository boundaries, generic game contracts, no raw transport in Razor, no live-evidence claims, and exact
unsafe sentinel redaction.

### Product Surfaces Under Test

- `sources/WebApp/Pages/Servers/Create.razor`
- `sources/WebApp/Pages/Servers/Create.razor.css`
- `sources/WebApp/Pages/Servers/Details.razor`
- `sources/WebApp/Pages/Servers/Details.razor.css`
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`
- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs`
- `sources/WebApp/Services/GameServerRepository/LifecycleJobInfo.cs`
- `sources/WebApp/Services/GameServerRepository/CloudRuntimeStatusInfo.cs`
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs`

Prefer tests and fixture corrections over product UI changes unless a Phase 60 test exposes a real boundary or label
regression from Phases 55-59.

### Evidence And Planning Artifacts

- `.planning/phases/60-ux-verification-fake-states-and-guardrails/60-VERIFICATION.md`
- `.planning/phases/60-ux-verification-fake-states-and-guardrails/60-01-SUMMARY.md`
- `.planning/phases/60-ux-verification-fake-states-and-guardrails/60-02-SUMMARY.md`
- `.planning/phases/60-ux-verification-fake-states-and-guardrails/60-03-SUMMARY.md`

Final evidence must classify mock/browser proof separately from deferred live UAT.

## Planning Landmines

- Do not run live Hetzner, Steam, Arma, provider teardown, bearer/cookie, or authenticated operator evidence.
- Do not add a test-only UI page for normal users; verify the real creation/workspace path.
- Do not preserve old fake payload shapes if they conflict with Phase 59 contracts.
- Do not use real-looking provider ids, resource ids, tokens, cookies, usernames, or project names in mock evidence.
- Do not make screenshots the only pass/fail signal.
- Do not broadly grep for safe infrastructure words such as `authorization`; use exact unsafe sentinel values and raw
  field names.

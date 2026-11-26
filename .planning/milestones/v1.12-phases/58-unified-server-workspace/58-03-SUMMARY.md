---
phase: 58-unified-server-workspace
plan: 03
subsystem: ui
tags: [blazor, server-workspace, provider-teardown, cost-stop, diagnostics]

requires:
  - phase: 58-unified-server-workspace
    provides: "58-01 workspace projection and 58-02 selected-server overview/logs shell."
provides:
  - "Provider teardown wording and result states that explicitly identify the provider-cost stop boundary."
  - "Selected-server Cloud tab provider summary with admin-only collapsed diagnostics."
  - "Danger tab routing that keeps provider-resource review inside the selected-server workspace."
  - "Focused boundary and mock-backed UI tests for teardown, visibility, and diagnostics behavior."
affects: [phase-58, phase-59, server-details, cloud-teardown, provider-cost]

tech-stack:
  added: []
  patterns:
    - "Runtime cleanup remains a server cleanup action and does not claim to stop provider cost."
    - "Provider teardown is the selected-server cost-stop action and remains backed by existing API authorization."
    - "Owners see server-scoped provider status; admins see collapsed selected-server diagnostics."

key-files:
  created:
    - sources/WebApp/ApiModelCompatibilityPartials.cs
    - .planning/phases/58-unified-server-workspace/58-03-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Details.razor
    - sources/WebApp/Pages/Servers/Details.razor.css
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs
    - tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs

key-decisions:
  - "Used the existing selected-server repository projection instead of adding new API DTOs for this plan."
  - "Kept provider teardown confirmation in the Cloud tab and linked runtime cleanup back to the Danger tab."
  - "Kept broad cloud command-center navigation out of the selected-server provider-resource review path."

requirements-completed: [WORK-03, WORK-04]

duration: "focused UI and boundary pass"
completed: 2026-07-05T04:31:16+02:00
---

# Phase 58 Plan 03 Summary

**Provider teardown is now the explicit selected-server cost-stop action, distinct from runtime cleanup.**

## Performance

- **Duration:** focused UI and boundary pass
- **Started:** 2026-07-05T04:24:00+02:00
- **Completed:** 2026-07-05T04:31:16+02:00
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments

- Updated workspace action copy and teardown result mapping so successful provider teardown reports `Provider cost stopped` and remaining billable resources still require follow-up.
- Added a selected-server provider summary to the Cloud tab with session, billable-resource, cleanup-needed, and cost-stop state.
- Added a confirmation-gated `Teardown provider resources` action in the Cloud tab using `TeardownProviderResourcesResultAsync`.
- Added direct copy that runtime cleanup does not stop provider cost, and routed runtime cleanup to the selected server's Danger tab.
- Added collapsed admin-only diagnostics using `Me.HasClaimOrAdmin(Claims.Administrative.Server)`.
- Restored the expected Kiota compatibility partials for cloud policy visibility wrappers so the related visibility boundary is green.

## Task Commits

1. **Tasks 1-3: Cost-stop copy, selected-server provider summary, diagnostics, and tests** - `379c8aa8` (feat)
2. **Plan metadata:** pending in the docs commit that includes this summary.

## Files Created/Modified

- `sources/WebApp/Pages/Servers/Details.razor` - Adds provider-resource summary, cost-stop teardown action, admin diagnostics, and selected-server routing.
- `sources/WebApp/Pages/Servers/Details.razor.css` - Styles provider summary, cost-stop action block, and diagnostics panel.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Maps successful provider teardown to provider-cost-stopped state.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Tightens provider teardown action summaries.
- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs` - Updates provider teardown summary defaults.
- `sources/WebApp/ApiModelCompatibilityPartials.cs` - Adds cloud policy visibility compatibility wrapper constructors.
- `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` - Guards explicit cost-stop wording.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - Guards selected-server diagnostics and provider-resource routing.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - Adds provider teardown mock responses for server detail tests.

## Decisions Made

- Did not add a new API route because existing server-scoped cloud status, cost, policy, session, and teardown calls already supplied the selected-server workspace data needed for Plan 03.
- Kept admin diagnostics collapsed and selected-server scoped to avoid turning the workspace into a broad provider inventory.
- Kept API/backend authorization as the enforcement boundary; UI gating only controls selected-server presentation.

## Deviations from Plan

- Added `ApiModelCompatibilityPartials.cs` after the planned visibility test group exposed a missing compatibility shim for cloud policy visibility deserialization. This was adjacent to WORK-04 because the selected-server provider summary depends on the policy visibility projection staying usable.

## Issues Encountered

- `dotnet build sources\WebApp\WebApp.csproj --no-restore` passed, but the repo still reports existing unrelated WebApp warnings, mostly duplicate `using` directives and older nullable/deprecation warnings in files outside this plan.

## Verification

- `dotnet build sources\WebApp\WebApp.csproj --no-restore` - passed with existing unrelated warnings.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests" --no-restore` - passed, 13 tests.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudCostVisibilityApiTests|FullyQualifiedName~CloudPolicyVisibilityBoundaryTests" --no-restore` - passed, 22 tests.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudTeardown" --no-restore` - passed, 13 tests.
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~ServerDetail|FullyQualifiedName~Logs" --no-restore` - passed, 11 tests.
- `rg -n "stops provider cost|does not stop provider cost|Teardown provider resources" sources/WebApp/Services/GameServerRepository sources/WebApp/Pages/Servers/Details.razor sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - passed.
- `rg -n 'ResolveSelfUrl\("command/cloud"\)|command/cloud|OpenCloudOperations' sources/WebApp/Pages/Servers/Details.razor` - passed with no matches.
- `rg -n "Collect logs|CollectLogs|collect logs" sources/WebApp/Pages/Servers/Details.razor` - passed with no matches.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 04 can finish the workspace handoff by tightening navigation/accessibility, final visual polish, and any remaining selected-server workflow acceptance checks.

---
*Phase: 58-unified-server-workspace*
*Completed: 2026-07-05*

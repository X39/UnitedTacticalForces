# Phase 58: Unified Server Workspace - Pattern Map

**Generated:** 2026-07-05
**Status:** Ready for planning

## Closest Existing Analogs

| Target Concern | Closest Existing File | Pattern To Reuse |
|----------------|-----------------------|------------------|
| Selected-server workspace shell | `sources/WebApp/Pages/Servers/Details.razor` | Route sections, command-page header, tabs, server summary strip, repository-backed async loading |
| Cloud workflow action summaries | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | Map API/generated DTOs into safe action states, status labels, required next steps, cost, policy, teardown, and audit summaries |
| Server repository facade | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | Hand-written Razor-facing models and methods; generated Kiota access remains in implementation |
| Server-scoped API controller | `sources/Api/Controllers/GameServersController.cs` | Authorization-scoped server routes for lifecycle, cloud status, runtime, cost/policy, ownership, audit, logs, configuration, files, and provider teardown |
| UI command styling | `sources/WebApp/wwwroot/css/app.css` and `sources/WebApp/Pages/Servers/*.razor.css` | Dark command-center surfaces, `Utf*` components, status badges, tabs, panels, and existing spacing/radius/color tokens |
| UI mock-backend verification | `tests/Ui.Tests/GameServerUiMockBackend.cs` and `tests/Ui.Tests/CloudAgentUiTests.cs` | Browser-level or host-level fake backend scenarios for cloud/server workspace states |
| WebApp source boundary tests | `tests/Api.Tests/WebApp/*BoundaryTests.cs` | Source scans for repository boundaries, forbidden raw markers, route/string regressions, and user-facing label checks |

## File Roles For Planning

### WebApp Workspace

- `sources/WebApp/Pages/Servers/Details.razor`
  - Likely primary edit target.
  - Split or refactor carefully; preserve route behavior and repository calls.
  - Add dedicated Logs tab and remove user-facing collect logs action.
  - Rework Overview into state plus primary next action.

- `sources/WebApp/Pages/Servers/*.razor.css`
  - Likely styling target if server-specific classes are split or added.
  - Use existing `--utf-*` variables.

### Repository Boundary

- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/*.cs`
  - Add safe aggregate models only if the workspace needs them.
  - Keep generated DTOs and request builders out of Razor.
  - Preserve generated-client boundaries and no raw browser HTTP bridge.

### API Boundary

- `sources/Api/Controllers/GameServersController.cs`
- existing cloud cost/policy/teardown services under `sources/Api/Services`
  - Prefer server-side projections for owner-safe provider summaries and admin diagnostics.
  - Keep broad provider inventory admin-gated.

### Tests

- `tests/Api.Tests/WebApp/*BoundaryTests.cs`
  - Add scans for no user-facing `Collect logs`, generic workspace labels, and no generated DTO binding in Razor.
- `tests/Api.Tests/CloudTeardown/*`
  - Extend cost-stop and remaining-billable behavior checks if API projections change.
- `tests/Ui.Tests/*`
  - Add mock workspace states for overview/action/log/admin diagnostics behavior.

## Planning Landmines

- Do not delete backend collect-log lifecycle capability just because the user-facing action is removed.
- Do not make Overview a second full Cloud/Runtime/Ownership page; it should prioritize one next action and compact status.
- Do not expose broad provider inventory to server owners.
- Do not imply runtime cleanup stops provider cost.
- Do not hand-edit generated Kiota files.
- Do not rely on live provider mutation or live Steam/Arma UAT.

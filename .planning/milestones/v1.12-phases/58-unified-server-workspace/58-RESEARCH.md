# Phase 58: Unified Server Workspace - Research

**Phase:** 58 - Unified Server Workspace
**Status:** Ready for planning
**Generated:** 2026-07-05

## Research Complete

Phase 58 is a selected-server workspace consolidation over existing ASP.NET Core API routes, Blazor WebAssembly pages, Fluent UI components, and WebApp repository models. The phase should be planned as a UI plus projection repair, not as a new hosting subsystem.

## Scope Interpretation

The phase must satisfy WORK-01 through WORK-04:

- WORK-01: one server workspace summarizes identity, game, hosting mode, owner, runtime, latest job, worker/provider state, cost/policy, and cleanup risk.
- WORK-02: relevant lifecycle, cloud, log, owner, configuration, file, and cleanup actions are discoverable from the workspace.
- WORK-03: runtime cleanup and provider teardown are distinct, with provider teardown as cost stop.
- WORK-04: admin fleet/provider diagnostics remain available, but normal server-owner flow does not depend on admin-only pages.

Phase 59 owns deeper lifecycle/log/status feedback repair. Phase 58 may remove or reposition user-facing actions that belong to the workspace, but should avoid broad lifecycle engine rewrites unless they are required to make the workspace truthful.

## Existing Implementation Findings

### Selected Server Workspace

- `sources/WebApp/Pages/Servers/Details.razor` is the main workspace and is currently very large.
- It already has route sections for overview, runtime, cloud, configuration, files, jobs, ownership, audit, and danger.
- It currently mixes status summary, cloud setup, runtime controls, logs, cloud runtime, cost/policy, ownership, danger, runtime cleanup, and admin-adjacent navigation in one component.
- Overview currently shows only a limited summary and latest jobs; it does not yet behave as the session-oriented control surface locked in CONTEXT.md.

### Logs

- Logs are currently nested under the Runtime section.
- User-facing collect/download/clear actions exist today.
- CONTEXT.md changes this product contract: users should not need a collect action to read logs. Log collection should be API/internal plumbing; user-facing log reading and download belong on a dedicated Logs tab.
- API and repository methods for `CollectLogsAsync` can remain if they are still needed by internal flows, later Phase 59 feedback repair, or backend log accessibility.

### Cloud, Cost, And Cleanup

- `Details.razor` already loads cloud runtime status, cost visibility, policy visibility, cloud planning selection options, and recent audit records.
- `GameServersController` exposes server-scoped cloud runtime, cost visibility, policy visibility, provider teardown, and cleanup-runtime endpoints.
- `CloudWorkflowResultMapper` already contains teardown result mapping, remaining billable count, cleanup failed state, and required next-step text. Reuse this rather than creating duplicate status interpretation in Razor.
- Runtime cleanup currently lives in the danger section with a link to provider resource review. The phase should make provider teardown the primary cost-stop action when billable resources remain.

### Owner And Admin Boundary

- Server-owner projections should remain server-scoped and redacted.
- Normal owners should see summaries and safe next actions, not fleet-wide provider inventory.
- Admins should get a collapsed diagnostics panel inside the selected-server workspace for selected-server provider/fleet details.
- Existing `/servers/cloud` or command cloud pages can remain, but they should not be necessary for normal owner operation.

## Planning Recommendations

### Plan Shape

Use the roadmap's four-plan split:

1. Information architecture and projection needs.
2. Workspace summary and action surface implementation.
3. Cleanup/teardown/cost-stop UX and authorization boundaries.
4. Workspace component, repository, and authorization tests.

### Implementation Approach

- Start by extracting or organizing `Details.razor` sections enough to make Overview, Logs, Cloud, Jobs, Ownership, Files, Audit, and admin diagnostics understandable.
- Introduce a small workspace view model or local helpers if it reduces repeated status ranking and next-action logic.
- Keep generated Kiota DTOs behind `IGameServerRepository` models.
- Prefer additive repository facade models for aggregated workspace summaries over direct Razor binding to raw generated DTOs.
- Do not hand-edit `sources/WebApp/UtfApi/**`.

### Verification Focus

- Boundary/source tests should assert no user-facing `Collect logs` action remains in server workspace UI.
- UI tests should cover Overview state/action behavior for local record, local managed, cloud setup needed, active billable cloud, teardown ready, teardown succeeded, log available, and admin diagnostics states.
- API/repository tests should cover server-owner versus admin provider visibility and no secret-bearing values in workspace projections.
- Source scans should catch Arma/dry-run wording leaking into normal workspace labels where generic wording is required.

## Validation Architecture

Automated validation should combine:

- API tests in `tests/Api.Tests/Api.Tests.csproj` for authorization-scoped projections, teardown/cost-stop labels, and repository/source boundary scans.
- UI tests in `tests/Ui.Tests/Ui.Tests.csproj` for mock backend server workspace states.
- Full solution build with `dotnet build X39.UnitedTacticalForces.slnx`.
- Targeted source assertions against `sources/WebApp/Pages/Servers/Details.razor`, related CSS, repository facades, and API controller routes.

Recommended commands:

- Quick API boundary run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WebApp|FullyQualifiedName~CloudTeardown|FullyQualifiedName~CloudCostVisibility|FullyQualifiedName~CloudPolicies"`
- Quick UI run: `dotnet test tests/Ui.Tests/Ui.Tests.csproj`
- Full verification: `dotnet test X39.UnitedTacticalForces.slnx`
- Build gate: `dotnet build X39.UnitedTacticalForces.slnx`

## Risks And Constraints

- `Details.razor` is large; broad rewrites can create regression risk. Plans should isolate layout changes and keep behavior repository-backed.
- Log collection removal is a product decision for user-facing UI, not necessarily deletion of backend lifecycle job capability.
- Provider teardown is destructive and cost-relevant; plans must include confirmation, permission, policy, and state handling.
- Live Hetzner, Steam, Arma, authenticated browser, and provider mutation UAT remain deferred.
- Existing security constraints apply: provider tokens, Steam credentials, worker credentials, bearer headers, cookies, and raw secret-bearing payloads must never reach frontend models or logs.

## Research Complete

Planner can proceed using this research, `58-CONTEXT.md`, and `58-UI-SPEC.md`.

---
phase: 58-unified-server-workspace
plan: 04
subsystem: test
tags: [blazor, server-workspace, logs, provider-teardown, authorization, ui-tests]

requires:
  - phase: 58-unified-server-workspace
    provides: "58-01 projection, 58-02 workspace shell/logs tab, and 58-03 provider teardown/cost-stop UI."
provides:
  - "Final API/source boundary coverage for the unified selected-server workspace."
  - "Mock-backed UI state matrix for local, cloud setup, provider teardown, cost-stopped, failed-job, and empty-log states."
  - "Verification evidence for Logs tab readability, no user-facing collect logs action, cost-stop semantics, and admin diagnostics gating."
affects: [phase-58, server-details, server-workspace, cloud-teardown, ui-tests]

tech-stack:
  added: []
  patterns:
    - "Use mock-backed Playwright states for selected-server workspace acceptance instead of live Hetzner, Steam, Arma, or authenticated browser state."
    - "Keep generated API compatibility behind repository and test fixture boundaries."
    - "Use source boundary tests for non-negotiable UX semantics such as no collect logs and cost-stop wording."

key-files:
  created:
    - .planning/phases/58-unified-server-workspace/58-04-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Details.razor
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - tests/Ui.Tests/CloudAgentUiTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs

requirements-completed: [WORK-01, WORK-02, WORK-03, WORK-04]

duration: "focused test and verification pass"
completed: 2026-07-05T05:12:06+02:00
---

# Phase 58 Plan 04 Summary

**Phase 58 is covered by focused source, API, and UI tests.**

## Accomplishments

- Added a UI state matrix for local record, local managed running, cloud setup needed, provider teardown needed, provider cost stopped, and failed lifecycle states.
- Added UI coverage for provider teardown changing the selected server to a cost-stopped state.
- Added UI coverage that server-owner views do not show admin diagnostics, while admin diagnostics is present but collapsed by default for administrators.
- Added Logs tab coverage proving logs are readable in the UI, the download option is present on the Logs tab, and `Collect logs` is absent from the selected-server UI.
- Adjusted the cloud-planning handoff so records created with setup defaults show `Continue cloud setup` as the primary overview action.
- Extended the mock backend to provide server-scoped cost, policy, session, runtime, log, owner, and job states without live provider dependencies.
- Fixed mock enum payloads for dry-run plan and reconciliation pages uncovered by the broad UI filter.

## Requirement Coverage

- **WORK-01:** Covered by `ServerDetailWorkspaceOverview_CoversCoreStateMatrix` and `ServerWorkspaceBoundaryTests`, including identity, game/hosting mode, owner-backed cost/policy, runtime, latest job, cleanup, and teardown signals.
- **WORK-02:** Covered by selected-server navigation tests, the Logs tab test, lifecycle start/stop/delete coverage, cloud continuation coverage, and source assertions that the workspace owns the relevant server-scoped actions.
- **WORK-03:** Covered by provider teardown UI tests, source assertions for cost-stop wording, and the expanded API filter covering teardown/cost visibility.
- **WORK-04:** Covered by owner/admin UI tests and source boundary tests for collapsed admin diagnostics and server-scoped provider summaries.

## Decision Coverage

- **D-01, D-03:** Overview state/action matrix proves core status and primary actions are immediately visible.
- **D-02, D-04:** Test states use one session-oriented model across local records, local managed servers, cloud setup, failed jobs, and long-lived cost states without a separate lifespan UI.
- **D-05:** Source tests guard one primary action plus secondary actions, and UI tests assert state-selected primary actions.
- **D-06:** Detailed owner, files, jobs, audit, danger, and diagnostics workflows remain tab-scoped or collapsed.
- **D-07:** Cloud setup defaults now render `Continue cloud setup`; cloud setup-needed state renders `Review cloud setup`.
- **D-08:** Logs have a dedicated tab; `Collect logs` is absent from `Details.razor`; logs are readable before any download step.
- **D-09, D-10, D-11:** Provider teardown is promoted as the cost-stop action when billable provider resources remain, and runtime cleanup copy does not claim cost stop.
- **D-12:** Provider teardown success is verified as `Cost stopped` with a provider-cost-stopped result state.
- **D-13, D-16:** Server-owner UI sees only server-scoped provider summary/action data and no hidden admin inventory prompt.
- **D-14, D-15:** Admin diagnostics are selected-server scoped, admin-only, and collapsed until opened.

## Task Commit

1. **Final workspace state coverage:** `d9cbec42 test(58-04): cover unified server workspace states`
2. **Plan metadata:** pending in the docs commit that includes this summary.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~CloudInventoryVisibilityTests" --no-restore` - passed, 23 tests.
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~ServerDetailWorkspaceOverview|FullyQualifiedName~CloudTab_TeardownProviderResources|FullyQualifiedName~CloudTab_ShowsCollapsed|FullyQualifiedName~LogsTab_RendersReadable" --no-restore` - passed, 4 tests.
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CreatePage_CloudModeCreatesPlanningServer|FullyQualifiedName~CloudPlansPage_ExplainsDryRunsAndProvisionsValidPlan|FullyQualifiedName~CloudReconciliationPages_RenderGermanLocalization|FullyQualifiedName~ServerHostCycle_CreatesConfiguresRunsStopsAndDeletesServer|FullyQualifiedName~CloudReconciliationPage_RendersExplanationRunsStartsRunAndOpensDetail" --no-restore` - passed, 5 tests after fixture fixes.
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~ServerDetail|FullyQualifiedName~Workspace|FullyQualifiedName~Logs|FullyQualifiedName~Cloud" --no-restore` - passed, 60 tests.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore` - passed with 0 warnings and 0 errors.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudTeardown|FullyQualifiedName~CloudCostVisibility|FullyQualifiedName~CloudPolicies" --no-restore` - passed, 172 tests.
- `rg -n "Collect logs|CollectLogs|collect logs" sources\WebApp\Pages\Servers\Details.razor` - passed with no matches.
- `rg -n "Continue cloud setup|No logs returned|Teardown provider resources|Cost stopped|Admin diagnostics" sources\WebApp\Pages\Servers\Details.razor tests\Ui.Tests` - passed with expected matches.
- `dotnet test X39.UnitedTacticalForces.slnx --no-restore` - failed outside Phase 58 scope: parser tests passed 93, UI tests passed 60, Api.Tests passed 786 and failed 1 existing role boundary assertion in `Api.Tests.WebApp.RoleManagementBoundaryTests.RoleDetailRoute_OwnsRoleEditingMembersAndClaims` because `VisibleAssignedClaims` was not found in the role detail route source.

## Issues Encountered

- The broad server/detail/cloud/logs UI filter initially exposed stale mock assumptions after separating cloud sessions from local runtime state. The fixture now updates `canStart`/`canStop`, keeps cloud-planning records unprovisioned, and uses numeric generated enum values for dry-run and reconciliation mocks.
- The in-app Browser was not used for the Phase 58 state matrix because these scenarios require mock-backed Playwright routes that are supplied by `tests/Ui.Tests`.

## Deferred UAT

- Live Hetzner, Steam, Arma 3 process, authenticated browser, and real worker-provider UAT remain deferred. Phase 58 closes with repository, API/source, and mock-backed UI coverage only.

## User Setup Required

None.

## Next Phase Readiness

Phase 59 can build on the unified workspace with operational feedback improvements for job states, progress, status wording, and blocked-operation next steps.

---
*Phase: 58-unified-server-workspace*
*Completed: 2026-07-05*

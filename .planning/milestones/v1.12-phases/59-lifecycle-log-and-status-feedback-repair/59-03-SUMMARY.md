---
phase: 59-lifecycle-log-and-status-feedback-repair
plan: 03
subsystem: api-ui-testing
tags: [lifecycle-jobs, operational-status, diagnostics, blazor, ui-tests]

requires:
  - phase: 59-lifecycle-log-and-status-feedback-repair
    plan: 01
    provides: durable lifecycle job reasons and selected-server operational labels
  - phase: 59-lifecycle-log-and-status-feedback-repair
    plan: 02
    provides: log freshness projection and passive Logs tab follow behavior
provides:
  - Cost/safety-ranked selected-server primary and secondary actions.
  - Safe failed-operation next steps that keep normal runtime actions available when the state is usable.
  - Always-visible selected-server diagnostics with an owner-safe allowlist.
  - API/source/UI regression coverage for stuck, recovered, failed, cancelled, cleanup, log, policy, unknown, deferred, and diagnostics states.
affects: [selected-server-workspace, cloud-workflow-results, cloud-agent-ui-tests, phase-59-validation]

tech-stack:
  added: []
  patterns:
    - Rank next actions by cost exposure and repair target specificity before normal runtime actions.
    - Keep diagnostics visible but restrict content to selected-server owner-safe fields.
    - Use source boundary tests to pin broad regression matrix coverage across API, mapper, Razor, and UI mock fixtures.

key-files:
  created:
    - .planning/phases/59-lifecycle-log-and-status-feedback-repair/59-03-SUMMARY.md
  modified:
    - sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
    - sources/WebApp/Pages/Servers/Details.razor
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs
    - tests/Ui.Tests/CloudAgentUiTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs

key-decisions:
  - "Provider teardown stays above policy, job, log, and runtime actions whenever active billable resources or cleanup exposure exist."
  - "Failed operations that leave a normal action safe use that normal action, such as Start server, instead of a separate retry CTA."
  - "Diagnostics are not admin-gated for now, but the Razor surface only shows selected-server session state, worker status, public endpoint, policy, latest job, log freshness, and audit count."
  - "Configuration and file repair targets use explicit workspace sections instead of diagnostics fallback."

patterns-established:
  - "ServerWorkspaceActionKind covers OpenConfiguration and target sections are asserted by source tests."
  - "CloudWorkflowResultMapper selects normal runtime next steps before special retry when a failed state is usable."
  - "CloudAgent UI fixtures model queued, retry-pending, failed, cancelled, final-log-refresh, unknown-worker, and deferred-setup states."

requirements-completed: [OPS-01, OPS-02, OPS-03, OPS-04]

duration: 34m
completed: 2026-07-05
---

# Phase 59 Plan 03 Summary

**Cost-safe next-action routing, visible redacted diagnostics, and regression coverage for lifecycle feedback states**

## Performance

- **Duration:** 34 min
- **Started:** 2026-07-05T15:29:31+02:00
- **Completed:** 2026-07-05T16:03:03+02:00
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments

- Ranked workspace actions by provider cost exposure, unsafe failed/retry lifecycle state, policy blockers, logs/jobs follow-up, and then ordinary runtime actions.
- Updated workflow result mapping so a failed but usable runtime state keeps the normal next action available, such as `Start server`, while special retry remains for unsafe intermediate states.
- Removed the admin-only diagnostics gate and replaced the panel content with an owner-safe selected-server allowlist.
- Added exact target routing for ownership, configuration, files, logs, jobs, and cloud repair surfaces.
- Expanded API/source/UI regression coverage for queued, running, retry-pending, failed, cancelled, stop-log-refresh, failed-log-collection, teardown, cleanup-risk, policy, unknown worker, deferred setup, unsupported action, and diagnostics-redaction cases.

## Task Commits

1. **Task 1: Encode primary action priority and safe failed-operation behavior** - `b845a287` (feat)
2. **Task 2: Route blockers to exact workspace surfaces and keep diagnostics visible** - `59b8507d` (feat)
3. **Task 3: Add regression matrix and final Phase 59 summary** - `0b9ac1de` (test)

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs` - added `OpenConfiguration` for exact configuration routing.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - ranked primary/secondary actions and ownership/configuration/file targets.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - preferred normal runtime next steps for safe failed states.
- `sources/WebApp/Pages/Servers/Details.razor` - visible diagnostics allowlist plus runtime/worker reason rows.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - target, redaction, and Phase 59 regression matrix source checks.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - action-ranking and failure-mapper source checks.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - queued, cancelled, unknown worker, and deferred setup mock states.
- `tests/Ui.Tests/CloudAgentUiTests.cs` - diagnostics visibility and lifecycle/log/status UI matrix checks.
- `.planning/phases/59-lifecycle-log-and-status-feedback-repair/59-03-SUMMARY.md` - final plan summary.

## Requirements And Decisions

- **OPS-01:** Covered by lifecycle queue/running/retry/failed/cancelled source and UI matrix assertions.
- **OPS-02:** Covered by log freshness, failed log collection, and stop-final-log follow tests.
- **OPS-03:** Covered by runtime, worker, provider, job, log, unknown, deferred, and failed status label assertions.
- **OPS-04:** Covered by cost-first teardown routing, policy review, ownership, configuration/files, logs/jobs, safe failed start, and unsupported action mapping.
- **D-01 through D-18:** Preserved from plans 59-01 and 59-02; this plan's matrix test pins their status/log coverage.
- **D-19 through D-23:** Implemented directly by action priority, safe failed-operation next steps, exact target sections, visible redacted diagnostics, and final regression matrix.

## Deviations from Plan

None. The plan scope was executed as written.

## Issues Encountered

- The first full CloudAgent UI run timed out at the command limit and left test/dev-server dotnet processes. Those repo-local processes were cleaned up and the suite was rerun with a longer timeout.
- The unknown/deferred UI test initially proved that the Cloud tab showed status labels but not runtime/worker reason text. The owner-safe reason rows were added to the Cloud runtime panel and verified.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~ServerWorkspaceBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudInventoryVisibilityTests" --logger "console;verbosity=minimal"`
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests" --logger "console;verbosity=minimal"`
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CloudTab_ShowsUnknownWorkerAndDeferredSetupStates" --logger "console;verbosity=minimal"`
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests" --logger "console;verbosity=minimal" --no-build`
- `dotnet build X39.UnitedTacticalForces.slnx`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle|FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudInventoryVisibilityTests" --logger "console;verbosity=minimal" --no-build`
- `rg -n "Collect logs|ContractJobKind\.CollectLogs|CollectLogsAsync|logs/collect" sources\WebApp\Pages\Servers\Details.razor; if ($LASTEXITCODE -eq 0) { exit 1 } else { exit 0 }`
- `rg -n "CredentialHash|ProviderToken|BearerToken|AuthorizationHeader|RawJson|RawPayload|CanViewProviderDiagnostics|Admin diagnostics" sources\WebApp\Pages\Servers\Details.razor; if ($LASTEXITCODE -eq 0) { exit 1 } else { exit 0 }`

## Deferred Live UAT

Live Hetzner provisioning/teardown, Steam credential use, real Arma process control, and manual operator UAT remain deferred. They were not required for Phase 59 completion; this phase is verified with source, API, and mock-backed UI coverage.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 59 is ready for closure. The selected-server workspace now has durable status evidence, passive log freshness, cost-safe next actions, visible redacted diagnostics, and regression coverage across the locked lifecycle feedback states.

---
*Phase: 59-lifecycle-log-and-status-feedback-repair*
*Completed: 2026-07-05*

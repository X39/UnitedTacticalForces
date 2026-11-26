---
phase: 59-lifecycle-log-and-status-feedback-repair
plan: 02
subsystem: api-ui
tags: [lifecycle-jobs, logs, operational-status, blazor, ui-tests]

requires:
  - phase: 59-lifecycle-log-and-status-feedback-repair
    plan: 01
    provides: durable lifecycle job reasons and selected-server operational labels
provides:
  - Stop and CollectLogs lifecycle execution paths that persist sanitized final log records.
  - API and WebApp repository log freshness projection for selected-server runtime status.
  - Logs tab freshness strip and bounded follow loop for existing log-refresh jobs.
affects: [game-server-lifecycle, cloud-runtime-status, selected-server-logs, webapp-mock-backend]

tech-stack:
  added: []
  patterns:
    - Keep log collection as durable lifecycle work, not a surprise browser action.
    - Project log freshness truth from API state before rendering it in Razor.
    - Follow only an already-existing Stop or CollectLogs job and stop after terminal state.

key-files:
  created:
    - .planning/phases/59-lifecycle-log-and-status-feedback-repair/59-02-SUMMARY.md
  modified:
    - sources/Api/Services/GameServerController/IGameServerController.cs
    - sources/Api/Services/GameServerController/Controllers/GameServerControllerBase.cs
    - sources/Api/Services/GameServerController/Controllers/SteamGameServerControllerBase.cs
    - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
    - sources/Api/DTO/CloudRuntimeStatusDto.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/WebApp/Services/GameServerRepository/CloudRuntimeStatusInfo.cs
    - sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Pages/Servers/Details.razor
    - sources/WebApp/Pages/Servers/Details.razor.css
    - tests/Api.Tests/GameServerLifecycle/LifecycleJobExecutorTests.cs
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - tests/Ui.Tests/CloudAgentUiTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs

key-decisions:
  - "Stop performs final log capture inside the same lifecycle job rather than enqueueing a second browser-visible action."
  - "The Logs tab may follow an existing log-refresh job, but it does not create one and does not expose a Collect logs control."
  - "Log freshness labels are stable API/repository projection values such as Logs current, Refreshing after stop, Waiting for worker, Collection unavailable, and Collection failed."

patterns-established:
  - "Controller strategies expose CollectLogsAsync so lifecycle execution can capture logs through the existing game-controller abstraction."
  - "CloudRuntimeStatus carries LogFreshnessKind/Label/Reason, TimeStampLogsRefreshed, LogRefreshJob, and ShouldRefreshLogsOnCompletion."
  - "The WebApp mock backend models log freshness states explicitly for current, running, failed, no-log, and stop-refresh scenarios."

requirements-completed: [OPS-02, OPS-03]

duration: 2h10m
completed: 2026-07-05
---

# Phase 59 Plan 02 Summary

**Visible log freshness, stop-final-log capture, and Logs tab follow behavior without a manual collect action**

## Performance

- **Duration:** 2h10m
- **Started:** 2026-07-05T15:00:09+02:00
- **Completed:** 2026-07-05T15:29:31+02:00
- **Tasks:** 3
- **Files modified:** 18

## Accomplishments

- Added `CollectLogsAsync(User?)` to the game-controller abstraction, with safe base behavior and Steam/local process-log flushing.
- Updated lifecycle execution so `CollectLogs` performs real capture and `Stop` captures final logs before marking the same job succeeded.
- Added API and WebApp projection fields for log freshness, active or failed log-refresh jobs, refreshed timestamps, and follow eligibility.
- Updated the Logs tab to show a freshness strip above persisted logs and to follow existing refresh jobs until terminal state, then reload logs once.
- Expanded source and browser UI coverage for logs current, no logs, running collection, failed collection, and stop-final-log refresh states.

## Task Commits

1. **Task 1: Add final log capture to stop and collect-log execution** - `a797494f` (feat)
2. **Task 2: Project log freshness and active log job state** - `6508eb3c` (feat)
3. **Task 3: Update Logs tab to follow existing jobs and refresh persisted logs** - `fc2ffb16` (feat)

## Files Created/Modified

- `sources/Api/Services/GameServerController/IGameServerController.cs` - controller-level log capture contract.
- `sources/Api/Services/GameServerController/Controllers/GameServerControllerBase.cs` - sanitized fallback log capture record.
- `sources/Api/Services/GameServerController/Controllers/SteamGameServerControllerBase.cs` - final async process log flushing for Steam-backed controllers.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs` - Stop and CollectLogs execution behavior.
- `sources/Api/DTO/CloudRuntimeStatusDto.cs` and `sources/Api/Controllers/GameServersController.cs` - log freshness and log-refresh job projection.
- `sources/WebApp/Services/GameServerRepository/*.cs` - hand-written repository models and mappings for log freshness.
- `sources/WebApp/Pages/Servers/Details.razor` and `.razor.css` - Logs tab freshness strip and bounded follow loop.
- `tests/Api.Tests/GameServerLifecycle/LifecycleJobExecutorTests.cs` and `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - backend/source boundary coverage.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` and `tests/Ui.Tests/CloudAgentUiTests.cs` - browser mock states and assertions.

## Decisions Made

- Kept the user-facing Logs tab passive: it reloads logs/status and follows existing jobs, but it never starts log collection.
- Treated Stop final-log capture as part of the Stop lifecycle operation, matching the plan's one-operation requirement.
- Used API-projected freshness labels instead of deriving log state in Razor from raw jobs/logs.

## Deviations from Plan

None. The plan scope was executed as written.

## Issues Encountered

- An initial parallel verification run hit a Roslyn build-server file lock. Running `dotnet build-server shutdown` and rerunning checks sequentially resolved it.
- WebApp and API builds still report pre-existing duplicate-using, nullable, and XML documentation warnings outside this plan. No new build errors were introduced.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle" --logger "console;verbosity=minimal"`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~ServerWorkspaceBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests" --logger "console;verbosity=minimal"`
- `rg -n "Collect logs|ContractJobKind\.CollectLogs|CollectLogsAsync|logs/collect" sources\WebApp\Pages\Servers\Details.razor; if ($LASTEXITCODE -eq 0) { exit 1 } else { exit 0 }`

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 59-02 is ready for Plan 59-03. The selected-server workspace now has durable log capture, log freshness projection, and passive Logs tab follow behavior for the remaining next-step and regression-matrix work.

---
*Phase: 59-lifecycle-log-and-status-feedback-repair*
*Completed: 2026-07-05*

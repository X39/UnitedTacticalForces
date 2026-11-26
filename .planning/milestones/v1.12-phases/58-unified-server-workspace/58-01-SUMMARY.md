---
phase: 58-unified-server-workspace
plan: 01
subsystem: ui
tags: [blazor, repository-facade, server-workspace, lifecycle, cost-stop]

requires:
  - phase: 57-guided-server-creation-journey
    provides: "Guided creation opens the selected server workspace for continuation actions."
provides:
  - "Hand-written selected-server workspace overview model for Razor-facing UI work."
  - "Repository method that composes server, owner, runtime, job, cost, policy, log, audit, cleanup, and teardown state."
  - "Boundary tests for generated DTO isolation, generic action labels, provider cost-stop priority, and no user-facing collect-logs action."
affects: [phase-58, phase-59, phase-60, server-workspace, game-server-repository]

tech-stack:
  added: []
  patterns:
    - "Generated Kiota DTOs stay behind IGameServerRepository and are projected into hand-written WebApp models."
    - "Workspace next actions use stable enum identifiers plus display labels."

key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - .planning/phases/58-unified-server-workspace/58-01-SUMMARY.md
  modified:
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs

key-decisions:
  - "Every server is treated as session-timed in the workspace projection; no short-lived versus forever split was introduced."
  - "Provider teardown is the primary cost-stop action when billable provider resources or cleanup risk remain."
  - "Runtime cleanup is explicitly modeled as not stopping provider cost."
  - "Collect logs is not exposed as a user workspace action; logs remain accessible through the Logs tab/action."

patterns-established:
  - "Workspace overview data is composed by IGameServerRepository before Razor consumes it."
  - "Primary and secondary workspace actions are derived from server-scoped status, policy, cost, cleanup, and log state."
  - "Source boundary tests protect user-facing wording and repository boundaries when generated clients change."

requirements-completed: [WORK-01, WORK-02]

duration: "resumed close-out"
completed: 2026-07-05
---

# Phase 58 Plan 01 Summary

**Repository-backed server workspace projection with session status, owner/cost/cleanup summaries, ranked next actions, and protected no-collect-logs boundary**

## Performance

- **Duration:** resumed close-out
- **Started:** 2026-07-05T03:48:00+02:00
- **Completed:** 2026-07-05T04:07:00+02:00
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments

- Added `ServerWorkspaceOverviewInfo` and related hand-written models for selected-server status plus actions.
- Exposed `IGameServerRepository.GetServerWorkspaceOverviewAsync` and implemented it by composing existing safe repository/API calls.
- Ranked provider teardown ahead of other actions when it is the cost-stop action, while keeping runtime cleanup separate.
- Added boundary tests that fail if generated DTOs leak into the model or a user-facing collect-logs action returns.

## Task Commits

1. **Tasks 1-2: Workspace model and repository projection** - `ebb92864` (feat)
2. **Task 3: Workspace boundary tests** - `a51fea46` (test)
3. **Plan metadata:** pending in the docs commit that includes this summary.

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs` - Hand-written workspace overview, summary, and action models.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - Adds the selected-server workspace overview repository method.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Composes overview data and ranks primary/secondary workspace actions.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - Protects model boundaries, generic labels, cost-stop wording, and no user-facing collect-logs action.
- `.planning/phases/58-unified-server-workspace/58-01-SUMMARY.md` - Records Plan 01 execution.

## Decisions Made

- Kept the projection repository-side so Plan 02 can bind Razor to a stable safe model rather than generated Kiota DTOs.
- Used generic labels such as `Review cloud setup` and `Teardown provider resources` so the workspace is not Arma 3 specific.
- Kept `OpenLogs` as the workspace action and excluded `CollectLogs`, matching the decision that log collection is API-internal plumbing.

## Deviations from Plan

None - plan scope was executed as written.

## Issues Encountered

- A previous combined parallel build/test run briefly hit a `Claims.dll` file lock. Verification was rerun sequentially and passed.

## Verification

- `dotnet build sources\WebApp\WebApp.csproj --no-restore` - passed with pre-existing warnings.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore` - passed.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore` - passed, 47 tests.
- `rg -n "ServerWorkspaceOverviewInfo|ServerWorkspaceActionInfo|ServerWorkspaceActionKind|GetServerWorkspaceOverviewAsync" sources\WebApp\Services\GameServerRepository` - passed.
- `rg -n -g "ServerWorkspace*.cs" "CollectLogs|Collect logs" sources\WebApp\Services\GameServerRepository` - passed with no matches.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 02 can now update the server details workspace UI against `ServerWorkspaceOverviewInfo` instead of composing status directly in Razor. Plan 03 can use the same projection to distinguish runtime cleanup from provider teardown and keep provider teardown as the only cost-stop wording.

---
*Phase: 58-unified-server-workspace*
*Completed: 2026-07-05*

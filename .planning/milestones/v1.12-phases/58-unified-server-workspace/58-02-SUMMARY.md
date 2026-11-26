---
phase: 58-unified-server-workspace
plan: 02
subsystem: ui
tags: [blazor, server-workspace, logs-tab, playwright, fluent-ui]

requires:
  - phase: 58-unified-server-workspace
    provides: "58-01 repository-backed selected-server workspace overview projection."
provides:
  - "Overview tab that renders core server status and one primary next action from the repository workspace model."
  - "Dedicated Logs tab with read/download/clear behavior and no user-facing collect-logs action."
  - "Focused source and mock-backed Playwright checks for Overview and Logs behavior."
affects: [phase-58, phase-59, phase-60, server-details, logs]

tech-stack:
  added: []
  patterns:
    - "Server Details Overview consumes ServerWorkspaceOverviewInfo rather than composing generated DTO state directly."
    - "Log collection remains backend/internal; the user-facing workspace links to persisted logs and download only from the Logs tab."

key-files:
  created:
    - sources/WebApp/Pages/Servers/Details.razor.css
    - .planning/phases/58-unified-server-workspace/58-02-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Details.razor
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs
    - tests/Ui.Tests/CloudAgentUiTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs

key-decisions:
  - "Runtime keeps lifecycle controls; logs moved to a dedicated Logs tab."
  - "Overview renders exactly one primary next-action region plus compact secondary action links."
  - "User-facing collect-log controls were removed from Details.razor; API/mock support remains for internals and compatibility."

patterns-established:
  - "Workspace action enum values map to links or existing lifecycle actions at the page boundary."
  - "Overview status cards use scoped Details.razor.css with existing --utf-* design tokens."
  - "UI tests validate server detail tabs against the mock backend, including the new Logs tab."

requirements-completed: [WORK-01, WORK-02]

duration: "focused UI pass"
completed: 2026-07-05
---

# Phase 58 Plan 02 Summary

**Selected-server Overview now shows session status plus next actions, while persisted logs live on a dedicated Logs tab**

## Performance

- **Duration:** focused UI pass
- **Started:** 2026-07-05T04:08:00+02:00
- **Completed:** 2026-07-05T04:24:00+02:00
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- Wired `Details.razor` to load `GetServerWorkspaceOverviewAsync` and render identity, server age, owner, runtime, latest job, worker/provider, cost/policy, cleanup, and teardown summaries on Overview.
- Added a primary next-action region and compact secondary action row driven by `ServerWorkspaceActionInfo`.
- Moved log viewing, empty/permission states, download, and clear controls into a dedicated `logs` route/tab.
- Removed the user-facing collect-logs button and page-side collect-logs lifecycle branch.
- Added scoped CSS for overview cards/action rows and extended source/UI tests.

## Task Commits

1. **Tasks 1-3: Overview, Logs tab, styling, and tests** - `2b3cd34d` (feat)
2. **Plan metadata:** pending in the docs commit that includes this summary.

## Files Created/Modified

- `sources/WebApp/Pages/Servers/Details.razor` - Uses the workspace overview model, renders Overview actions, adds the Logs tab, and removes user-facing collect logs.
- `sources/WebApp/Pages/Servers/Details.razor.css` - Adds responsive scoped styles for Overview status/action cards.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - Protects the Details overview/logs contract and no collect-logs action.
- `tests/Ui.Tests/CloudAgentUiTests.cs` - Updates server detail UI checks for Overview, Runtime, and Logs.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - Adds `logs/count` mock responses for the workspace projection.

## Decisions Made

- Kept the persisted log clear action in the Logs tab because it already has permission checks and is separate from log collection.
- Used the repository action model as the single source for Overview next-action labels and target sections.
- Kept deep owner, file, job, audit, and danger flows on their existing tabs instead of duplicating them on Overview.

## Deviations from Plan

None - plan scope was executed as written.

## Issues Encountered

- `Details.razor.css` did not exist, so Plan 02 created it instead of modifying an existing scoped stylesheet.
- The in-app Browser runtime connected on retry, but the target server-detail states require the UI test fixture's Playwright route mocks. Rendered verification therefore used the repo's mock-backed Playwright tests rather than a standalone Browser URL.

## Verification

- `rg -n "GetServerWorkspaceOverviewAsync|PrimaryAction|SecondaryActions|server age|Cost|Cleanup" sources\WebApp\Pages\Servers\Details.razor` - passed.
- `rg -n "LogsSection|ServerHref\(\"logs\"\)|DownloadLogs" sources\WebApp\Pages\Servers\Details.razor` - Logs/download markers passed; the shell regex did not print the nav marker, which is covered by source tests.
- `rg -n "Collect logs|CollectLogsAsync|CollectGameServerLogsAsync" sources\WebApp\Pages\Servers\Details.razor` - passed with no matches.
- `dotnet build sources\WebApp\WebApp.csproj --no-restore` - passed with pre-existing warnings.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests" --no-restore` - passed, 6 tests.
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~ServerDetail|FullyQualifiedName~Logs" --no-restore` - passed, 11 tests.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 03 can build on the Overview action surface and Cloud/Danger tabs to sharpen provider teardown versus runtime cleanup, cost-stopped state, and selected-server admin diagnostics.

---
*Phase: 58-unified-server-workspace*
*Completed: 2026-07-05*

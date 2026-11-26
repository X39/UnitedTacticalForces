---
phase: 60-ux-verification-fake-states-and-guardrails
plan: 01
subsystem: ui-tests
tags: [fake-states, playwright, source-boundaries, redaction]

requires:
  - phase: 59-lifecycle-log-and-status-feedback-repair
    provides: selected-server operational status labels, reasons, and next-step fields
provides:
  - Explicit Phase 60 fake state catalog with labels, reasons, primary actions, forbidden actions, target sections, and evidence text.
  - Mock backend routes for unsupported, unavailable-until-setup-completes, and stale-worker states.
  - Browser/source assertions that protect fake-state coverage, generic boundaries, no user-facing collect-logs action, and redacted workspace surfaces.
affects: [ui-tests, mock-backend, selected-server-workspace, source-boundary-tests]

tech-stack:
  added: []
  patterns:
    - Keep fake UX expectations in one typed UI-test-local catalog.
    - Drive browser status matrices from catalog entries instead of scattered inline theory data.
    - Source-scan the catalog and selected workspace surfaces for coverage and unsafe fields.

key-files:
  created:
    - .planning/phases/60-ux-verification-fake-states-and-guardrails/60-01-SUMMARY.md
  modified:
    - tests/Ui.Tests/CloudAgentUiTests.cs
    - tests/Ui.Tests/GameServerUiMockBackend.cs
    - tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs

key-decisions:
  - "Catalog evidence is explicitly mock/fake browser evidence and does not claim live provider, Steam, or Arma runtime proof."
  - "Unsupported, unavailable-until-setup-completes, and stale-worker taxonomy gaps were added as mock backend server states so their routes are browser-openable."
  - "Boundary tests were extended by source-scanning the UI catalog rather than introducing a second compiled catalog model."

patterns-established:
  - "Phase60ServerStateScenario rows carry ServerId, Route, SelectedGame, HostingMode, BillingOwner, StateLabel, Reason, PrimaryAction, TargetSection, ForbiddenActions, and EvidenceText."
  - "Cloud and jobs browser theories consume catalog rows through MemberData."
  - "ServerWorkspaceBoundaryTests now fail if required Phase 60 fake-state keys or unsafe workspace boundaries regress."

requirements-completed: [VER-01, VER-02, VER-04]

duration: 2h20m
completed: 2026-07-05
---

# Phase 60 Plan 01 Summary

**Fake state catalog and mock-backed browser/source test harness**

## Performance

- **Duration:** 2h20m
- **Completed:** 2026-07-05T16:38:36+02:00
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- Added a typed `Phase60ServerStateScenario` catalog covering local record, local managed, cloud planned, cloud provisioned, running, failed job, stuck/recovered job, log collection, cleanup needed, provider teardown ready, unknown, unavailable, unsupported, waiting-for-worker, stale, failed, deferred, and unavailable-until-setup-completes states.
- Refactored overview, jobs, and cloud browser tests to consume catalog rows while preserving Phase 59 lifecycle and log feedback regression coverage.
- Added mock backend states for unsupported hosting, setup-unavailable, and stale-worker routes so the operational taxonomy is browser-openable through current selected-server workspace paths.
- Expanded `ServerWorkspaceBoundaryTests` with Phase 60 catalog coverage and redaction/boundary assertions for no `Collect logs`, no raw HTTP markers in Razor, no `AdditionalData`, and no unsafe secret-bearing display fields.

## Task Commits

1. **Tasks 1-3: Fake state catalog, backend routing, and boundary tests** - `422a103d` (test)

## Files Created/Modified

- `tests/Ui.Tests/CloudAgentUiTests.cs` - added the catalog, integrity test, and catalog-driven browser matrix data.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - added unsupported, setup-unavailable, and stale-worker fake server states, ownership, and stale retry job data.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - added Phase 60 catalog/source-boundary regression checks.
- `.planning/phases/60-ux-verification-fake-states-and-guardrails/60-01-SUMMARY.md` - this execution summary.

## Decisions Made

- Kept the catalog private to UI tests and guarded through source-boundary scans, avoiding production model churn.
- Used fake-safe markers such as `Mock backend` and `fake provider state`; no live provider mutation or live credential evidence is represented.
- Left existing generated DTO usage outside the selected-server workspace model boundary untouched, while adding exact negative assertions for unsafe Razor-facing markers.

## Deviations from Plan

- Task commits were consolidated into one implementation commit because the catalog, mock backend exposure, and source-boundary checks were tightly coupled and verified together.

## Issues Encountered

- The first UI test run exceeded the initial two-minute timeout; rerunning with a longer timeout completed the filtered browser suite.
- Two catalog rows initially used fake-safe evidence strings that were not rendered by the browser page. They were corrected to visible UI evidence while retaining fake-safe catalog coverage elsewhere.

## Verification

- `rg -n "Phase60ServerStateScenario|local record|local managed|cloud planned|cloud provisioned|provider teardown ready|unavailable until setup completes|Mock backend|fake provider state" tests/Ui.Tests`
- `dotnet test tests\Ui.Tests\Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests" --no-restore`
- `rg -n "StateLabel|PrimaryAction|TargetSection|ForbiddenActions|EvidenceText|LogFreshnessLabel|RuntimeStatusLabel|WorkerStatusLabel" tests\Ui.Tests\GameServerUiMockBackend.cs tests\Ui.Tests\CloudAgentUiTests.cs`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore`
- `git diff --check`

## User Setup Required

None.

## Next Phase Readiness

Plan 60-01 is ready for Plan 60-02. Browser verification can now reference an explicit fake-state catalog and the mock backend exposes every required taxonomy state through selected-server routes.

---
*Phase: 60-ux-verification-fake-states-and-guardrails*
*Completed: 2026-07-05*

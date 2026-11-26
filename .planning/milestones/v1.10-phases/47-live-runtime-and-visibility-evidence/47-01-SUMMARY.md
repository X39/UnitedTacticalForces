---
phase: 47-live-runtime-and-visibility-evidence
plan: 01
subsystem: webapp
tags:
  - cloud-runtime
  - worker-bootstrap
  - runtime-evidence
  - boundary-tests
requires:
  - phase: 46-controlled-live-provisioning-evidence
    provides: Provisioning evidence checklist and human-needed live boundary.
provides:
  - Selected-server runtime evidence checklist.
  - Boundary test coverage for runtime evidence labels and cleanup/cost-stop separation.
  - Reused stable evidence-grid styling for runtime rows.
affects:
  - sources/WebApp/Pages/Servers/Index.razor
  - sources/WebApp/Pages/Servers/Index.razor.css
  - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs
tech-stack:
  added: []
  patterns:
    - Evidence rows derive from repository-safe runtime/status/job/log projections.
    - Missing live worker/runtime facts are classified as human-needed or blocked.
key-files:
  created:
    - .planning/phases/47-live-runtime-and-visibility-evidence/47-01-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Index.razor
    - sources/WebApp/Pages/Servers/Index.razor.css
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs
key-decisions:
  - "Phase 47 runtime evidence starts as a selected-server checklist layered onto the existing Cloud runtime panel."
  - "Worker bootstrap, registration, heartbeat, capabilities, current job, progress, recent log, failure, blocker, cleanup separation, and redaction labels are stable evidence rows."
  - "Runtime cleanup remains separate from provider teardown; provider teardown remains the cost-stop boundary."
patterns-established:
  - "Runtime evidence rows reuse the Phase 46 evidence grid pattern for stable source-testable labels."
requirements_completed: []
duration: 12min
completed: 2026-06-22
---

# Phase 47 Plan 01 Summary

**Worker bootstrap and runtime evidence checklist.**

## Performance

- **Duration:** 12 min
- **Completed:** 2026-06-22T05:45:00+02:00
- **Tasks:** 4
- **Files modified:** 3 source/test files plus this planning artifact

## Accomplishments

- Added a `Runtime evidence` subsection to the selected-server `Cloud runtime` panel.
- Added `GetCloudRuntimeEvidenceRows` and helpers for worker bootstrap, worker registration, heartbeat, capabilities, current job, progress, recent log, last failure, runtime blocker, cleanup separation, and redaction state.
- Reused the Phase 46 stable evidence grid styling for runtime rows with responsive single-column behavior.
- Added `LiveRuntimeEvidence_SelectedServerUiUsesSafeEvidenceRows` to guard evidence labels, helper names, repository-backed runtime action paths, cleanup/cost-stop copy, and forbidden display marker avoidance.

## Task Commits

Pending commit for this plan.

## Files Created/Modified

- `sources/WebApp/Pages/Servers/Index.razor` - Runtime evidence rows and safe helper methods.
- `sources/WebApp/Pages/Servers/Index.razor.css` - Runtime evidence grid styling.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - Phase 47 runtime evidence boundary test.
- `.planning/phases/47-live-runtime-and-visibility-evidence/47-01-SUMMARY.md` - This summary.

## Decisions Made

- Runtime evidence uses existing `CloudRuntimeStatusInfo`, cloud session status, lifecycle job, and selected log projections; no raw browser HTTP or generated client code was added.
- Missing live provisioning or worker/runtime evidence is shown as `Human needed` or blocked instead of success.
- The first runtime slice focuses on worker/status evidence. Detailed install/configure/start/stop/log classification remains in Plan 47-02.

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- The initial boundary test used method-body extraction for `CanRunCloudRuntimeAction`; that helper does not capture expression-bodied switch members well enough, so the test now uses a source-range extraction pattern already used elsewhere in the test file.
- Filtered API tests emitted existing API/test warnings; the WebApp build passed with zero warnings.

## Automated Checks

- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` - passed with 0 warnings and 0 errors.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudWorkerBootstrap" --no-restore --verbosity minimal` - passed, 33 tests after test extraction adjustment.
- Source scans from `47-VALIDATION.md` for runtime evidence labels and runtime action repository paths - reviewed.

## User Setup Required

An operator must still supply or record an actual provisioned live cloud session, worker bootstrap consumption, worker registration, heartbeat freshness, capabilities, Steam/Arma install/start/stop/log evidence, and authenticated browser evidence before any live runtime pass can be claimed.

## Next Phase Readiness

Plan 47-02 can build on the runtime evidence rows by classifying install/configure, start, stop, and log evidence from lifecycle jobs and selected logs.

## Self-Check

All automated checks required for Plan 47-01 passed after the boundary test extraction fix. The implementation does not claim live runtime, provider teardown, final billable-resource closure, Phase 30, or CR acceptance pass status.

---
*Phase: 47-live-runtime-and-visibility-evidence*
*Completed: 2026-06-22*

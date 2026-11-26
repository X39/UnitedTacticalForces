---
phase: 37-provisioning-runtime-operation-and-teardown
plan: 04
subsystem: ui
tags: [blazor, fluent-ui, game-servers, cloud-runtime, teardown, testing]

requires:
  - phase: 37-01
    provides: Cloud provisioning service foundation and operation states.
  - phase: 37-02
    provides: Worker/runtime lifecycle execution and durable job state.
  - phase: 37-03
    provides: Repository-safe cloud operation facade for WebApp consumption.
provides:
  - Repository-backed `/servers` operation UI for provisioning, runtime actions, retries, cleanup risk, and provider teardown.
  - Source-boundary tests for Phase 37 UI copy, redaction, repository usage, and cost-stop separation.
  - Deferred evidence record for live/manual UAT excluded by user instruction.
  - Post-plan stabilization evidence for full API test-suite failures found during Task 3.
affects: [servers-ui, provisioning-runtime, provider-teardown, phase-37-verification]

tech-stack:
  added: []
  patterns:
    - Blazor/Fluent UI operation panels using existing `IGameServerRepository` facade models.
    - Source-boundary tests for UI copy, forbidden markers, and semantic action separation.

key-files:
  created:
    - tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs
    - .planning/phases/37-provisioning-runtime-operation-and-teardown/deferred-items.md
  modified:
    - sources/WebApp/Pages/Servers/Index.razor
    - sources/WebApp/Pages/Servers/Index.razor.css

key-decisions:
  - "Kept `/servers` repository-backed by calling `IGameServerRepository` facade methods and safe CloudWorkflow models instead of generated DTOs or raw HTTP."
  - "Separated `Cleanup runtime` from `Teardown provider resources`; only provider teardown is presented as the cost-stop action."
  - "Recorded manual authenticated browser, live Hetzner, Steam/Arma, provider mutation, final billable-resource verification, OPS-02/OPS-03 live UAT, and Phase 30 evidence as deferred."

patterns-established:
  - "Provider teardown panels use distinct destructive styling and confirmation copy naming provider billing impact."
  - "Runtime operation controls stay separate from provider billing controls and use repository facade actions."
  - "Boundary tests scan the marked Razor region for required UI copy and forbidden raw/secret/generated markers."

requirements-completed: [OPS-01, OPS-02, OPS-03, OPS-04]

duration: 15min
completed: 2026-06-20
---

# Phase 37 Plan 04: `/servers` Operation UI and Automated Verification Summary

**Repository-backed `/servers` operations UI with explicit runtime controls, retry/cleanup visibility, and provider teardown as the distinct cost-stop action.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-06-20T15:52:44Z
- **Completed:** 2026-06-20T16:06:56Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- Added Phase 37 source-boundary tests covering `/servers` required copy, repository-only operation access, redaction behavior, deferred-UAT wording, forbidden markers, and runtime/provider cost-stop separation.
- Extended `sources/WebApp/Pages/Servers/Index.razor` with a marked `ServersProvisioningRuntimeTeardown` region for provisioning, runtime actions, retry, cleanup-needed/orphan-risk/partial-failure states, remaining billable resource visibility, and sanitized failures.
- Added distinct provider teardown UI with `Teardown provider resources` confirmation copy, separate from `Cleanup runtime` and runtime stop.
- Added responsive styles for provider teardown, resource rows, failure details, and mobile operation action stacking.

## Task Commits

1. **Task 1: Add Phase 37 /servers operation source-boundary tests** - `c6de04d` (`test`)
2. **Task 2: Implement `/servers` provisioning, runtime, retry, and provider teardown UI** - `d934d3f` (`feat`)
3. **Task 3: Run final automated verification and record deferred manual evidence** - no code commit; verification-only task with deferred item recorded in plan metadata.

## Files Created/Modified

- `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` - Phase 37 UI source-boundary tests.
- `sources/WebApp/Pages/Servers/Index.razor` - `/servers` provisioning/runtime/retry/provider teardown operation UI.
- `sources/WebApp/Pages/Servers/Index.razor.css` - Operation layout, provider teardown, resource row, failure detail, and mobile styles.
- `.planning/phases/37-provisioning-runtime-operation-and-teardown/deferred-items.md` - Resolved full-suite failures found during Task 3 plus deferred manual/live evidence.

## Decisions Made

- Kept `/servers` as the owner/operator surface rather than adding a new route.
- Kept all Phase 37 UI operation calls behind `IGameServerRepository`; no raw browser HTTP bridge or generated DTO binding was introduced.
- Presented `Teardown provider resources` as the explicit destructive cost-stop action and kept `Cleanup runtime` as a separate runtime cleanup action.
- Did not run or claim manual/live UAT evidence that the user explicitly deferred.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed WebApp build break from an unavailable status property**
- **Found during:** Task 2 (`/servers` UI implementation)
- **Issue:** The initial UI used a non-existent remaining-billable-resource property on the cloud session status model.
- **Fix:** Computed remaining billable state from the safe cost visibility counts already exposed through the repository facade.
- **Files modified:** `sources/WebApp/Pages/Servers/Index.razor`
- **Verification:** Boundary tests and WebApp build passed.
- **Committed in:** `d934d3f`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was required for correctness and stayed within the planned repository-safe UI surface.

## Issues Encountered

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal` initially failed with 5 deterministic failures outside the Plan 37-04 files. These were fixed during Phase 37 post-plan stabilization before code review and phase verification; see `deferred-items.md`.
- The first solution build run was executed concurrently with a WebApp build and hit a transient `obj` race. The sequential rerun passed.

## Automated Verification

- PASS: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests" --verbosity minimal` failed RED before implementation with missing UI markers, then passed after implementation.
- PASS: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` (23 tests).
- PASS: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests" --verbosity minimal` (48 tests).
- PASS: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkerDispatchServiceTests|FullyQualifiedName~Arma3LifecycleJobExecutorTests|FullyQualifiedName~LifecycleJobQueueServiceTests" --verbosity minimal` (17 tests).
- PASS: `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` with existing generated nullable-context warnings in `sources/WebApp/UtfApi/**`.
- PASS: `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` on sequential rerun.
- PASS: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal -p:UseSharedCompilation=false` (349 tests) after post-plan stabilization.

## Deferred UAT and Live Evidence

Deferred by user instruction, not claimed as passed:

- Authenticated browser walkthrough.
- Live Hetzner provisioning and teardown.
- Steam/Arma runtime operations.
- Provider mutation/provisioning/teardown UAT.
- Final provider-side billable resource count verification.
- OPS-02/OPS-03 live UAT.
- Phase 30 evidence collection.

## Known Stubs

None. Stub scan findings were existing empty collection/null state initialization, port defaults, and required redaction copy; none block the Plan 37-04 goal.

## Threat Flags

None. The new UI surface is covered by the plan threat model: repository-only operation calls, redacted result handling, and explicit runtime/provider billing copy separation.

## User Setup Required

None for automated verification. Live provider/runtime/manual UAT remains deferred.

## Next Phase Readiness

The `/servers` owner/operator UI is ready for automated follow-on work. Manual/live evidence remains open by explicit instruction and should be handled only when the user authorizes browser UAT, provider mutation, Steam/Arma runtime checks, and final provider billable-resource verification.

## Self-Check: PASSED

- Found summary file: `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-04-SUMMARY.md`
- Found deferred item file: `.planning/phases/37-provisioning-runtime-operation-and-teardown/deferred-items.md`
- Found boundary test file: `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs`
- Found task commits: `c6de04d`, `d934d3f`

---
*Phase: 37-provisioning-runtime-operation-and-teardown*
*Completed: 2026-06-20*

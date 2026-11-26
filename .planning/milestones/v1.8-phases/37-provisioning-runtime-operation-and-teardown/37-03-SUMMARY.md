---
phase: 37-provisioning-runtime-operation-and-teardown
plan: 03
subsystem: webapp-repository
tags: [blazor, kiota, cloud-provisioning, runtime-operations, redaction]
requires:
  - phase: 37-provisioning-runtime-operation-and-teardown
    provides: server-scoped provision and provider teardown API routes from Plan 37-01
  - phase: 37-provisioning-runtime-operation-and-teardown
    provides: runtime lifecycle status, cleanup, and labels from Plan 37-02
provides:
  - Repository-local Kiota RequestInformation sender for server-scoped provision, provider teardown, and runtime cleanup operations
  - Selected-server repository facade methods for Phase 37 operation workflows
  - Redaction-guarded operation result mapping for retry, cleanup, partial failure, remaining billable resources, and worker/current-job state
affects: [phase-37, servers-workflow, cloud-provider-actions, runtime-operations, redaction]
tech-stack:
  added: []
  patterns:
    - Repository-local Kiota RequestInformation helpers bridge generated-route gaps without editing generated UtfApi files.
    - Remaining billable provider resources map to follow-up failure state instead of full teardown success.
key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudProvisioningRuntimeCommandSender.cs
    - .planning/phases/37-provisioning-runtime-operation-and-teardown/37-03-SUMMARY.md
  modified:
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs
key-decisions:
  - "Server-scoped Phase 37 WebApp operations use repository-local Kiota RequestInformation helpers because generated builders are absent and generated UtfApi files remain untouched."
  - "Provider teardown is labeled Teardown provider resources in repository results; runtime cleanup is labeled Cleanup runtime."
  - "Remaining billable resources after provider teardown map to a non-success follow-up state requiring Review billable resources."
patterns-established:
  - "Generated-route gaps can be bridged in repository-local senders using IRequestAdapter and RequestInformation while preserving CloudWorkflowKiotaSender continuation query handling."
  - "Cloud workflow result mappers must preserve redaction guard behavior while adding safe UI state fields."
requirements-completed: [OPS-01, OPS-02, OPS-03, OPS-04]
duration: 10min
completed: 2026-06-20
---

# Phase 37 Plan 03: Repository-Safe Operation Facade and Redaction Mapping Summary

**Repository-local cloud operation facade with safe Kiota senders and redacted operation result mapping**

## Performance

- **Duration:** 10 min
- **Started:** 2026-06-20T15:38:35Z
- **Completed:** 2026-06-20T15:47:44Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Added failing repository boundary coverage for Phase 37 selected-server operations, route ownership, labels, and teardown follow-up semantics.
- Added `CloudProvisioningRuntimeCommandSender` to issue server-scoped POST operations through Kiota `IRequestAdapter` without touching generated `UtfApi` files.
- Added repository facade methods for provision-from-reviewed-dry-run, provider teardown, runtime status refresh, runtime lifecycle action, and runtime cleanup.
- Extended cloud workflow result mapping with safe redaction-preserved fields for cleanup, retry, partial failure, remaining billable resources, worker state, job progress, and available actions.
- Normalized labels to the Phase 37 wording, including `Teardown provider resources`, `Review billable resources`, and runtime action labels.

## Task Commits

1. **Task 1: TDD RED repository boundary tests** - `c146095` (test)
2. **Task 2: GREEN repository facade and mapper implementation** - `68953b4` (feat)
3. **Task 3: Verification-only boundary and build checks** - no code commit; verification passed with no file changes.

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudProvisioningRuntimeCommandSender.cs` - Repository-local Kiota `RequestInformation` factory for generated-route gaps.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - Adds selected-server Phase 37 operation facade methods.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Wires facade methods through `CloudWorkflowKiotaSender` and normalizes operation labels.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` - Adds safe operation result fields needed by the operation UI.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Maps runtime status, teardown, retry, cleanup, and partial-failure follow-up state without exposing secrets.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - Adds static boundary tests for repository-only operation behavior and route safety.
- `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-03-SUMMARY.md` - Execution record for this plan.

## Decisions Made

- Used repository-local Kiota `RequestInformation` helpers for missing generated routes instead of editing generated `sources/WebApp/UtfApi/**` files.
- Kept operation sending behind `CloudWorkflowKiotaSender` so continuation query handling, parser usage, and redaction mapping stay centralized.
- Treated teardown responses with remaining billable resources as failure/follow-up state so the UI cannot present incomplete provider cleanup as success.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test Bug] Corrected bodyless POST route assertion**
- **Found during:** Task 2 (GREEN implementation)
- **Issue:** The RED boundary test initially required `SetContentFromParsable` for server-scoped operation POST routes, but the planned routes are bodyless and carry identity in the selected server route plus optional continuation query parameters.
- **Fix:** Removed the body assertion while keeping route, method, and sender boundary assertions.
- **Files modified:** `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`
- **Verification:** `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal`
- **Committed in:** `68953b4`

**2. [Rule 1 - Mapper Bug] Preserved success state for empty runtime failure text**
- **Found during:** Task 2 (GREEN implementation)
- **Issue:** Runtime status mapping sanitized empty `LastFailureReason` into a non-null string, which could incorrectly classify status as failure.
- **Fix:** Checked null/whitespace before passing failure text through the redaction guard.
- **Files modified:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`
- **Verification:** Focused boundary tests passed.
- **Committed in:** `68953b4`

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** Both fixes preserved the intended repository-safe operation behavior and did not expand scope.

## Issues Encountered

- The first parallel Task 3 verification output exceeded the tool output limit, so the focused test and WebApp build were rerun serially with captured tail output.
- `git status` continues to report pre-existing unrelated dirty items: `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/`. They were not modified or staged for this plan.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` - passed, 18 tests.
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` - passed, 0 warnings, 0 errors.
- `git diff -- sources/WebApp/UtfApi` - no generated client edits.

## Deferred UAT

Deferred by user instruction:

- Manual authenticated browser UAT.
- Live Hetzner/provider mutation, provisioning, and teardown checks.
- Steam and Arma runtime checks.
- Phase 30 evidence collection.
- End-to-end billable-resource review and provider cleanup confirmation.

These checks were not run and are not claimed as passed.

## Known Stubs

None.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: generated-route-gap | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudProvisioningRuntimeCommandSender.cs` | Adds explicit server-scoped operation routes in repository code because generated Kiota builders are absent. Mitigated by internal-only helper, `IRequestAdapter`, no raw browser HTTP, no generated-client edits, and no secret-bearing request body. |

## User Setup Required

None - no external service configuration required for the repository boundary and build verification performed in this plan.

## Next Phase Readiness

- Plan 37-04 can wire selected-server UI controls to the new repository facade methods.
- Runtime and provider teardown UI should continue to treat remaining billable resources as a blocking follow-up state.
- Live provider/provisioning/teardown UAT remains deferred and must be handled separately before claiming end-to-end cloud operation success.

## Self-Check: PASSED

- Found summary file: `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-03-SUMMARY.md`
- Found task commit: `c146095`
- Found task commit: `68953b4`

---
*Phase: 37-provisioning-runtime-operation-and-teardown*
*Completed: 2026-06-20*

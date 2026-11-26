---
phase: 34-generated-client-and-repository-gate-foundation
plan: 01
subsystem: webapp-repository
tags: [blazor, webapp, repository, kiota, redaction, cloud-workflow]
requires:
  - phase: 33-guided-entry-ownership-and-scope-boundaries
    provides: guided `/servers` cloud-entry repository and UI boundary
provides:
  - Typed cloud workflow result taxonomy for owner-facing repository projections
  - Repository-local policy gate transport preserving continuation identity
  - Fail-closed redaction guard for forbidden field names and display terms
  - Mapper entry points for policy, lifecycle, provisioning, teardown, cost, and audit outcomes
affects: [phase-34, phase-35, phase-36, phase-37, phase-38, cost-03]
tech-stack:
  added: []
  patterns:
    - Repository-local projection records instead of raw generated DTOs
    - Centralized redaction guard before Razor-visible result mapping
key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
  modified: []
key-decisions:
  - "Policy continuation identity is preserved as safe ids and labels while raw evidence and generated extension data stay outside repository result records."
  - "The missing generated policy-gate model is handled through a repository-local transport shape instead of editing generated Kiota files."
patterns-established:
  - "Cloud workflow mapper methods return `CloudWorkflowActionResult` and never carry generated DTO instances forward."
  - "Display strings pass through `CloudWorkflowRedactionGuard` before entering repository result projections."
requirements-completed: [COST-03]
duration: 35min
completed: 2026-06-19
---

# Phase 34 Plan 01: Repository Result Taxonomy Summary

**Typed, allowlisted cloud workflow repository results with policy continuation identity and a fail-closed redaction guard**

## Performance

- **Duration:** 35 min
- **Started:** 2026-06-19T14:18:00Z
- **Completed:** 2026-06-19T14:53:34Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Added `CloudWorkflowModels` with the required Loading, Success, PolicyAllow, PolicyWarning, ApprovalRequired, Denied, ValidationBlocked, Failure, and Empty states.
- Added policy gate result and continuation fields for decision id, approval request id, action kind, scope key, server/session/plan/billing ids, and expiration.
- Added a repository-local policy gate transport because the generated WebApp client does not currently include a gate result model.
- Added mapper entry points for policy gates, lifecycle jobs, provisioning, teardown, cost visibility, and audit records.
- Added a redaction guard for forbidden field names and display terms before repository results reach Razor.

## Task Commits

1. **Task 1: Define cloud workflow result taxonomy and allowlisted records** - `8e80c60` (feat)
2. **Task 2: Add policy gate transport and mapper entry points** - `180a224` (feat)

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` - Typed result taxonomy and allowlisted projection records.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs` - Forbidden field/display-term guard used by mappers.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs` - Repository-local shape for policy-gated conflict payloads.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Mapper entry points from transport/generated/client models into repository result projections.

## Decisions Made

- Used a `CloudWorkflowModels` static container to match the existing `CloudAdminModels` pattern.
- Kept the policy gate transport independent from generated Kiota files because no generated gate result model exists in `sources/WebApp/UtfApi/Models`.
- Treated display redaction as a mapper boundary so later Razor components receive already-vetted projection records.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed Task 2 mapper compile issues**
- **Found during:** Task 2
- **Issue:** Initial mapper code used nullable-only helper overloads and incorrect `ECloudBillableActionKind` member names.
- **Fix:** Collapsed the mapper display sanitizer to one helper and mapped the actual contract enum members.
- **Files modified:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`
- **Verification:** `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- **Committed in:** `180a224`

**Total deviations:** 1 auto-fixed blocking implementation issue.
**Impact on plan:** No scope expansion; the fix was required for the planned mapper to compile.

## TDD Gate Compliance

Plan tasks were marked `tdd="true"`, but the user-provided write scope allowed only the four implementation files and the summary. No test files were created because that would have violated the plan write scope. Verification used the plan's automated build and redaction scan.

## Issues Encountered

- `gsd-tools` was not available on PATH in this shell, so SDK-driven state updates and final docs commit routing could not be used.
- `dotnet build` passes with existing generated Kiota nullable-context warnings under `sources/WebApp/UtfApi/**`; those files are generated and outside this plan's write scope.

## Verification

- PASS: `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- PASS: Redaction scan found forbidden terms only in `CloudWorkflowRedactionGuard.cs`.
- DEFERRED: Manual/browser/authenticated UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by operator instruction.

## Known Stubs

None.

## Threat Flags

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 34 follow-up plans can map real repository action callers to `CloudWorkflowActionResult` without binding Razor to generated DTOs or raw policy evidence. COST-03 has a concrete repository result contract for subsequent UI work.

## Self-Check: PASSED

- Found summary file and all four CloudWorkflow implementation files.
- Found commits `8e80c60` and `180a224` in git history.
- Stub scan found no placeholder/TODO/FIXME text and only intentional empty collection defaults/static mapper labels.

---
*Phase: 34-generated-client-and-repository-gate-foundation*
*Completed: 2026-06-19*

---
phase: 33-guided-entry-ownership-and-scope-boundaries
plan: 04
subsystem: webapp
tags: [blazor, fluent-ui, guided-entry, game-servers, validation]
requires:
  - phase: 33-03b
    provides: IGameServerRepository guided cloud entry facade methods
provides:
  - /servers guided Arma 3 cloud entry panel
  - Repository-backed guided planning server creation, owner assignment, and readiness validation controls
  - Responsive guided step rail, field validation, readiness blockers, and readiness summary
affects: [phase-33, phase-34, phase-35, servers-ui, guided-entry]
tech-stack:
  added: []
  patterns:
    - Razor page guided workflow state backed by IGameServerRepository facade methods
    - Client-side validation before authoritative server readiness validation
key-files:
  created:
    - .planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-04-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Index.razor
    - sources/WebApp/Pages/Servers/Index.razor.css
key-decisions:
  - "The owner workflow stays on /servers; /servers/cloud remains the existing admin fleet console."
  - "Continue to dry run validates readiness only and does not create dry-run plans or mutate provider resources."
  - "Razor consumes guided-entry behavior through IGameServerRepository and does not import generated request builders or raw HTTP calls."
patterns-established:
  - "Use a single guided panel below the server grid for owner-facing Arma 3 cloud entry rather than adding a new route."
  - "Render local validation, server readiness blockers, and ready summary separately so client-side completeness cannot claim server readiness."
requirements-completed: [CREATE-01, CREATE-02, CREATE-03, CREATE-04]
duration: 43min
completed: 2026-06-19
---

# Phase 33 Plan 04: `/servers` Guided Cloud Entry UI And Validation Summary

**Repository-backed `/servers` guided Arma 3 cloud entry with owner gating, visible validation, and readiness-only dry-run handoff**

## Performance

- **Duration:** 43 min
- **Started:** 2026-06-19T11:56:00Z
- **Completed:** 2026-06-19T12:38:54Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Added a `Create cloud server` toolbar path and `Plan cloud hosting` row action to `/servers`.
- Added a guided setup panel for eligible Arma 3 server selection, planning-server creation, active billing owner assignment, modpack/runtime inputs, cloud sizing/location/storage/port/SSH fields, readiness validation, and a compact ready summary.
- Kept the UI inside the repository boundary by using `IGameServerRepository` guided-entry methods and `IModPackRepository`, with no raw browser HTTP, generated request-builder imports, dry-run creation call, provider mutation, payment, invoice, checkout, or `/servers/cloud` normal creation path.
- Added responsive CSS for the guided panel, step rail, form grid, validation list, and readiness summary.

## Task Commits

1. **Task 1-2: Add guided entry state, commands, panel, validation, readiness summary, and styles** - `e8b3828` (feat)

## Files Created/Modified

- `sources/WebApp/Pages/Servers/Index.razor` - Adds the guided cloud entry toolbar/row actions, repository-backed state loading, planning-server creation, owner assignment, client validation, server readiness call, and readiness summary.
- `sources/WebApp/Pages/Servers/Index.razor.css` - Adds responsive guided cloud panel, step list, form, validation, action, and summary styles.
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-04-SUMMARY.md` - Captures plan completion evidence.

## Decisions Made

- Kept the normal owner/operator workflow in the existing `/servers` page instead of creating a separate route.
- Required an active billing owner before guided planning-server creation or owner save can proceed.
- Kept `Continue to dry run` as a readiness validation action only; Phase 33 does not create dry-run plans or provider resources.
- Used server readiness blockers as authoritative readiness evidence and rendered local validation separately.

## Verification

- `git diff --check -- sources/WebApp/Pages/Servers/Index.razor sources/WebApp/Pages/Servers/Index.razor.css` passed; only line-ending warnings were emitted.
- Filtered boundary scan over `sources/WebApp/Pages/Servers/Index.razor` found no `payment`, `invoice`, `checkout`, `tax`, provider token, Steam credential, worker credential, authorization header, bearer token, `/servers/cloud`, dry-run creation call, raw `cloud-entry` route, `RequestBuilder`, or direct `HttpClient` usage.
- Source assertion scan found `Create cloud server`, `Plan cloud hosting`, `Continue to dry run`, `GetGuidedCloudEntryOptionsAsync`, `CreateArma3GuidedCloudPlanningServerAsync`, `AssignGuidedCloudEntryBillingOwnerAsync`, `ValidateGuidedCloudEntryReadinessAsync`, `.guided-cloud-panel`, and `.guided-summary`.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` passed with existing generated nullable warnings in `sources/WebApp/UtfApi/Models/CloudSpendingPolicy*`.
- `dotnet test X39.UnitedTacticalForces.slnx --no-build --verbosity minimal` was attempted. Parser tests passed (`89/89`). The broader command failed for unrelated existing API cloud-policy/reconciliation tests and an AppHost SDK resolution attempt against NuGet despite `--no-build`.

## Deferred Evidence

- Manual browser `/servers` UAT is deferred by user instruction.
- Authenticated UAT is deferred by user instruction.
- Live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred and were not run or claimed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Active owner gating for guided planning-server creation**

- **Found during:** Task 1 source review
- **Issue:** The create action originally required a selected owner but did not require that owner to be active.
- **Fix:** Tightened `CanCreateGuidedPlanningServer` so inactive owners cannot enable the create action.
- **Files modified:** `sources/WebApp/Pages/Servers/Index.razor`
- **Verification:** Solution build and boundary scans passed after the change.
- **Committed in:** `e8b3828`

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** The fix strengthened the planned owner-gating behavior without adding scope.

## Issues Encountered

- Repeated subagent recovery attempts stalled after the system interruption, so Plan 04 was completed inline in the main context.
- `dotnet test X39.UnitedTacticalForces.slnx --no-build --verbosity minimal` failed outside this UI change:
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ExcludesEstimatedCostsEndingAtCurrentMonthStart` failed on a SQLite unique constraint for `CloudCostSnapshots.SessionFk, CloudCostSnapshots.Source`.
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_IgnoresOtherAccountReconciliationFindingsButRetainsGlobalOrphans` failed on a SQLite unique constraint for `GameServerBillingAssignments.GameServerFk`.
  - `Api.Tests.CloudPolicies.CloudSpendingPolicyEvaluationTests.EvaluateAsync_ProviderSpecificEvaluationIgnoresOtherProviderCosts` expected `Allow` but got `Warn`.
  - `Api.Tests.CloudReconciliation.CloudProviderReconciliationServiceTests.RunHetznerReconciliationAsync_PersistsCountsFindingsAndAudits` failed on JSON serialization cycle detection.
  - The solution-level test invocation also attempted to resolve `Aspire.AppHost.Sdk/9.5.2` from NuGet, which is unavailable with restricted network access.

## Known Stubs

None.

## Threat Flags

None introduced by this plan. The UI renders allowlisted DTO fields only and does not expose provider tokens, Steam credentials, worker credentials, authorization headers, bearer tokens, or raw secret-bearing payloads.

## User Setup Required

None - no external service configuration required for this UI plan.

## Next Phase Readiness

Phase 33 is complete. Phase 34 can build generated-client and repository gate foundations on top of the owner-facing `/servers` entry path, preserving the repository boundary and deferred live UAT constraints.

## Self-Check: PASSED

- Found `sources/WebApp/Pages/Servers/Index.razor`.
- Found `sources/WebApp/Pages/Servers/Index.razor.css`.
- Found `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-04-SUMMARY.md`.
- Found implementation commit `e8b3828`.

---
*Phase: 33-guided-entry-ownership-and-scope-boundaries*
*Completed: 2026-06-19*

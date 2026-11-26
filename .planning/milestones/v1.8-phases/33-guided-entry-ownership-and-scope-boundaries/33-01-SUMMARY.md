---
phase: 33-guided-entry-ownership-and-scope-boundaries
plan: 01
subsystem: api
tags: [aspnet-core, dto, cloud-planning, billing-ownership, redaction]

requires:
  - phase: 32-blocker-repair-and-milestone-verification
    provides: deferred live-UAT boundary and v1.8 blocker context
provides:
  - Guided cloud entry response DTO contracts for owner/operator options and readiness results.
  - Guided cloud entry request payload contracts for planning-server creation, owner selection, and readiness validation.
  - Secret-safe API transport shapes for CREATE-01 through CREATE-04.
affects: [phase-33, phase-34, phase-35, guided-cloud-entry, generated-client]

tech-stack:
  added: []
  patterns:
    - File-scoped ASP.NET Core API DTO and payload records with XML documentation and init-only properties.
    - Allowlisted browser-visible contract fields for guided Arma 3 cloud entry.

key-files:
  created:
    - sources/Api/DTO/GuidedCloudEntryBillingOwnerDto.cs
    - sources/Api/DTO/GuidedCloudEntryOptionsDto.cs
    - sources/Api/DTO/GuidedCloudEntryReadinessDto.cs
    - sources/Api/DTO/GuidedCloudEntryServerOptionDto.cs
    - sources/Api/DTO/Payloads/GuidedCloudEntryBillingOwnerSelectionPayload.cs
    - sources/Api/DTO/Payloads/GuidedCloudEntryReadinessPayload.cs
    - sources/Api/DTO/Payloads/GuidedCloudPlanningServerCreationPayload.cs
  modified: []

key-decisions:
  - "Kept Plan 33-01 contract-only: no controller behavior, provider mutation, payment workflow, or generated-client edits were added."
  - "Matched readiness payload field names to the existing Arma 3 dry-run request where fields overlap so later dry-run transformation can be mechanical."

patterns-established:
  - "Guided cloud entry contracts expose owner, assignment, provider option names, ports, SSH/CIDR, modpack, runtime basics, readiness blockers, and deferred assumptions without secret-bearing fields."
  - "Readiness payloads can carry incomplete optional planning inputs so later server-side validation can return blockers before any dry-run or provider action."

requirements-completed: [CREATE-01, CREATE-02, CREATE-03, CREATE-04]

duration: 4 min
completed: 2026-06-19
---

# Phase 33 Plan 01: API Guided-Entry DTO And Payload Contracts Summary

**Secret-safe ASP.NET Core DTO and payload contracts for guided Arma 3 cloud entry, billing-owner selection, and readiness validation**

## Performance

- **Duration:** 4 min
- **Started:** 2026-06-19T09:26:56Z
- **Completed:** 2026-06-19T09:30:25Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- Added response DTOs for guided cloud entry options, eligible server choices, selectable billing owners, and readiness results.
- Added request payloads for side-effect-light planning-server creation, guided owner selection, and non-mutating readiness validation.
- Preserved the Phase 33 boundary: no provider resource mutation, no payment or invoicing concepts, no generated client edits, and no secret-bearing transport fields.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add guided entry response DTOs** - `b65dd34` (feat)
2. **Task 2: Add guided entry request payloads** - `ec79600` (feat)

**Plan metadata:** pending docs commit

## Files Created/Modified

- `sources/Api/DTO/GuidedCloudEntryBillingOwnerDto.cs` - Owner-scoped billing account projection for guided entry.
- `sources/Api/DTO/GuidedCloudEntryOptionsDto.cs` - Options response containing server choices, owner choices, provider option names, recommendations, metadata freshness, and assumptions.
- `sources/Api/DTO/GuidedCloudEntryReadinessDto.cs` - Readiness result echoing selected owner, assignment, provider inputs, storage, ports, SSH/CIDR, modpack, runtime basics, blockers, and assumptions.
- `sources/Api/DTO/GuidedCloudEntryServerOptionDto.cs` - Existing server option projection with eligibility state and blockers.
- `sources/Api/DTO/Payloads/GuidedCloudEntryBillingOwnerSelectionPayload.cs` - Owner-selection request shape.
- `sources/Api/DTO/Payloads/GuidedCloudEntryReadinessPayload.cs` - Authoritative readiness-validation request shape covering CREATE-03 and CREATE-04 inputs.
- `sources/Api/DTO/Payloads/GuidedCloudPlanningServerCreationPayload.cs` - Planning-server creation request shape with owner, modpack, and runtime basics.

## Decisions Made

- Kept this plan limited to transport contracts. Endpoint behavior, server-side authorization, generated Kiota updates, and UI wiring remain in later Phase 33 plans.
- Used nullable provider option and port fields in readiness payloads where validation needs to report incomplete inputs, while keeping selected billing account and assignment identifiers aligned with existing dry-run request naming.
- Treated default empty strings and collections as DTO initialization, not placeholder UI data.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` passed with the repository's existing XML documentation warning set (`CS1573`, `CS1574`, `CS1591`) outside the new guided-entry files.
- Manual, authenticated browser, live provider, Steam, Arma, and Phase 30 UAT checks were not run per user instruction and Phase 33 context.

## Verification

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` - PASS, with existing XML documentation warnings.
- `rg -n "Token|Password|Credential|Authorization|Bearer|Secret|Raw|Json" sources/Api/DTO -g "GuidedCloudEntry*.cs"` - PASS, no matches.
- `rg -n "payment|invoice|checkout|tax|provider token|Steam:Password|Authorization|Bearer" sources/Api/DTO -g "GuidedCloudEntry*.cs" -g "Payloads/GuidedCloud*.cs"` - PASS, no matches.
- `rg -n "TODO|FIXME|placeholder|coming soon|not available|= \\[\\]|= \\{\\}|= null|= \"\"" sources/Api/DTO -g "GuidedCloudEntry*.cs" -g "Payloads/GuidedCloud*.cs"` - PASS, no stub matches.

## Known Stubs

None.

## Threat Flags

None - the new browser-visible API contract surface is the planned surface covered by T-33-01 and T-33-02.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for the next Phase 33 plans to add server-side guided-entry behavior, generated client/repository support, and `/servers` UI wiring using these DTO and payload contracts. Deferred UAT remains explicitly open and must not be claimed by Phase 33.

## Self-Check: PASSED

- Summary file exists.
- All seven created DTO and payload files exist.
- Task commits `b65dd34` and `ec79600` exist in git history.

---
*Phase: 33-guided-entry-ownership-and-scope-boundaries*
*Completed: 2026-06-19*

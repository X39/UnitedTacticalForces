---
phase: 38-cost-responsibility-history-and-audit-visibility
plan: 01
subsystem: api-webapp
tags: [aspnet-core, blazor, authorization, cloud-costs, xunit]

requires: []
provides:
  - Server-scoped read-only cost responsibility history endpoint access
  - Server-scoped read-only sanitized hosting audit endpoint access
  - Selected-server `/servers` cost responsibility history and audit context
affects: [phase-38, servers-workflow, cloud-cost-visibility, hosting-audit]

tech-stack:
  added: []
  patterns:
    - Read-only server cost responsibility context uses the same server inspection policy as cost visibility.
    - Mutating owner assignment remains administrative while owner/audit history is inspectable by server-scoped readers.
    - `/servers` uses existing repository facade calls and dense detail-section/data-table patterns.

key-files:
  created:
    - .planning/phases/38-cost-responsibility-history-and-audit-visibility/38-01-SUMMARY.md
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/WebApp/Pages/Servers/Index.razor
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs

key-decisions:
  - "Widened only read-only owner history and audit endpoints from admin-only to server resource inspection scope."
  - "Kept owner assignment mutation on the administrative policy."
  - "Used cost responsibility/internal attribution language and did not add payment, checkout, invoice, or tax workflows."

patterns-established:
  - "History and audit visibility is selected-server context, not fleet-only admin context."
  - "Sanitized audit rows render a cost-owner label from billing account/assignment identifiers without raw payload fields."

requirements-completed: [COST-01, COST-02]

duration: not recorded
completed: 2026-06-21
---

# Phase 38 Plan 01: Cost Responsibility History And Audit Visibility Summary

**Server-scoped users can inspect selected-server cost responsibility history and sanitized audit context without introducing payment or invoice workflow concepts.**

## Accomplishments

- Changed `GetBillingOwnerHistoryAsync` and `GetHostingAuditRecordsAsync` to use `Authorize($"{Claims.ResourceBased.Server.All}:*")`, matching server-scoped cost visibility.
- Left `AssignBillingOwnerAsync` on `Claims.Administrative.Server` so cost-owner mutation did not broaden.
- Updated `/servers` selected-server details to always load owner history and audit records for an inspectable selected server.
- Renamed the UI surface to `Cost responsibility history` and `Cost responsibility audit context`, with a muted internal attribution note.
- Added status/reason/cost-owner formatting helpers and empty states for history/audit tables.
- Added API reflection/direct controller tests and WebApp boundary tests for policy shape, persisted rows, repository calls, and forbidden commercial wording.

## Files Created/Modified

- `sources/Api/Controllers/GameServersController.cs` - Widened read-only owner history/audit endpoint policies to server inspection scope.
- `sources/WebApp/Pages/Servers/Index.razor` - Made selected-server responsibility history/audit visible outside `CanManageServers`, renamed labels, added formatters and empty states.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs` - Added policy-shape and persisted history/audit endpoint tests.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - Added source-boundary test for selected-server cost responsibility copy and repository calls.
- `.planning/phases/38-cost-responsibility-history-and-audit-visibility/38-UI-SPEC.md` - Added UI contract.
- `.planning/phases/38-cost-responsibility-history-and-audit-visibility/38-RESEARCH.md` - Added implementation research.
- `.planning/phases/38-cost-responsibility-history-and-audit-visibility/38-01-PLAN.md` - Added execution plan.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudCostVisibilityApiTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` - PASS, 30 tests.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore` - PASS, 0 errors. Existing generated WebApp nullable warnings remained.

## Deviations from Plan

### Auto-fixed Issues

**1. Build-only Razor helper variable collision**
- **Found during:** Solution build.
- **Issue:** Pattern variables in `FormatAuditCostOwner` reused names across an `if`/`else if` chain, causing CS0136 in Razor compilation.
- **Fix:** Renamed the pattern variables to unique names for transfer/new-only/previous-only cases.
- **Files modified:** `sources/WebApp/Pages/Servers/Index.razor`
- **Verification:** Solution build passed after the fix.

## Issues Encountered

- First solution build failed on the helper variable collision above; targeted tests had already passed because they source-scan WebApp code but do not compile the Razor project.
- `gsd-tools query init.phase-op 38` reported an existing `.planning/config.json` warning for unknown key `quick_branch_template`; this was pre-existing config state and was not changed.

## Threat Flags

None found in the Phase 38 implementation scope. The broadening is limited to read-only endpoints already constrained to a single game-server resource policy, and tests assert the mutating assignment endpoint remains administrative.

## Deferred UAT

Authenticated browser walkthrough, live Hetzner/Steam/Arma/provider mutation, provisioning, teardown, final provider billable-resource verification, and Phase 30 UAT evidence remain deferred. They were not run and are not claimed as passed.

## Self-Check: PASSED

- Summary file exists at `.planning/phases/38-cost-responsibility-history-and-audit-visibility/38-01-SUMMARY.md`.
- Required plan artifacts exist: UI spec, research, and plan.
- Required automated verification commands passed after the Razor build fix.

---
*Phase: 38-cost-responsibility-history-and-audit-visibility*
*Completed: 2026-06-21*

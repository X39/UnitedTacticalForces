---
phase: 36-policy-gate-and-approval-ux
plan: 04
subsystem: webapp-ui
tags: [blazor, fluent-ui, policy-gate, approvals]

requires:
  - phase: 36-policy-gate-and-approval-ux
    provides: repository-safe policy review, refresh, approve, and deny methods from Plan 03
  - phase: 36-policy-gate-and-approval-ux
    provides: API policy review and approval hardening from Plan 02
provides:
  - `/servers` policy decision panel with allow, warning, approval-required, denied, expired, mismatch, failure, and redaction-blocked states
  - Warning acknowledgement and approved continuation controls with selected-scope identity checks
  - Selected-server approval request list/detail with approve and deny confirmation flow
affects: [servers-page, policy-gate, approval-ux, cloud-workflow]

tech-stack:
  added: []
  patterns: [Fluent UI action panel, repository-only Razor boundary, selected-scope continuation guard]

key-files:
  created:
    - .planning/phases/36-policy-gate-and-approval-ux/36-04-SUMMARY.md
  modified:
    - sources/WebApp/Pages/Servers/Index.razor
    - sources/WebApp/Pages/Servers/Index.razor.css

key-decisions:
  - "The `ServersPolicyGateApproval` marked region uses only `IGameServerRepository` methods for policy review, refresh, approve, and deny operations."
  - "Warning acknowledgement passes only the displayed policy decision id; approved continuation passes only the displayed approval request id."
  - "Approver controls are rendered only for users with the server administrative authority signal, with API authorization remaining authoritative."
  - "Manual/live UAT evidence remains explicitly deferred and is shown as deferred copy in the policy panel."

patterns-established:
  - "Policy-gated billable workflow controls must compare the stored gate identity with the current selected server, session, dry-run plan, billing owner, action, scope, decision, approval request, and expiration before enabling continuation."
  - "Approval request rows on `/servers` stay scoped to the selected server and render durable audit context without raw transport fields."

requirements-completed: [POLICY-01, POLICY-02, POLICY-03]

duration: interrupted/recovered
completed: 2026-06-20
---

# Phase 36 Plan 04: `/servers` Policy Gate And Approval UX Summary

**Repository-backed policy decision and approval workflow controls on the game-server page**

## Performance

- **Duration:** Interrupted executor recovered inline.
- **Completed:** 2026-06-20T13:46:39Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments

- Added the marked `ServersPolicyGateApproval` panel under the dry-run review workflow.
- Enabled `Review policy decision`, `Acknowledge warning`, `Continue approved action`, and `Refresh policy status` through `IGameServerRepository`.
- Added selected-scope guards for gate identity, server/session/dry-run, billing owner, decision id, approval request id, and expiration.
- Added selected-server approval request rows, audit detail, reason and expiration fields, and approve/deny confirmation flows.
- Added responsive CSS for policy gate panels, summaries, approval tables, approval details, and mobile full-width actions.
- Preserved deferred-UAT copy in the policy panel and did not run or claim manual/live provider validation.

## Task Commits

1. **Tasks 1/2/3: Add policy gate panel, approval controls, CSS, and automated verification** - `bb7b514` (feat)

**Plan metadata:** pending final metadata commit.

## Files Created/Modified

- `sources/WebApp/Pages/Servers/Index.razor` - Added `ServersPolicyGateApproval`, repository-backed policy review/refresh/acknowledge/continue handlers, approval list/detail, confirmation-gated approve/deny commands, and scope matching helpers.
- `sources/WebApp/Pages/Servers/Index.razor.css` - Added policy gate and approval panel/table/form/action responsive styles.
- `.planning/phases/36-policy-gate-and-approval-ux/36-04-SUMMARY.md` - Plan execution summary.

## Decisions Made

- Recovered the incomplete subagent checkpoint inline rather than starting another UI executor, because the Razor/CSS write scope was already partially modified.
- Used the existing page-level Fluent confirmation pattern via `IDialogService.ShowConfirmationAsync`.
- Kept the approval list sourced from selected-server policy visibility and the command results sourced from the repository-safe approval command facade.
- Left generated `UtfApi` files untouched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Execution Recovery] Restarted executor checkpointed partial Razor work without commits**
- **Found during:** Plan 04 execution
- **Issue:** The restarted executor added incomplete `Index.razor` markup and handlers but did not finish helpers, CSS, verification, commits, or summary.
- **Fix:** Closed the executor, completed missing helpers/CSS inline, fixed Razor datetime binding, ran the required automated verification, and committed the implementation.
- **Files modified:** Plan-declared files only.
- **Verification:** Focused Phase 36 tests, WebApp build, and solution build passed.
- **Committed in:** `bb7b514`

---

**Total deviations:** 1 recovered execution interruption
**Impact on plan:** No product scope change. Manual/live UAT remains deferred.

## Issues Encountered

- Initial WebApp build failed because the partial executor work referenced missing local policy helper types and used a string-bound `datetime-local` input. The recovery added the missing local helpers/types and switched the expiration input to nullable `DateTime` binding converted to `DateTimeOffset`.
- The WebApp build still reports existing generated nullable-context warnings in `sources/WebApp/UtfApi/Models/*`; these are pre-existing generated-client warnings and were not introduced by Plan 04.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal` - Passed, 29 tests.
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` - Passed with existing generated nullable-context warnings, 0 errors.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` - Passed, 0 warnings, 0 errors.

Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by explicit instruction.

## Known Stubs

None.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: continuation-guard | `sources/WebApp/Pages/Servers/Index.razor` | Warning acknowledgement and approved continuation are UI conveniences only. Mitigated by selected-scope identity checks and server-side gate revalidation through the repository/API. |
| threat_flag: approver-ui | `sources/WebApp/Pages/Servers/Index.razor` | Approve/deny controls are shown only for users with the server administrative authority signal, but API authorization remains the enforcement point. |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 36 implementation is ready for phase-level verification. Manual/live UAT remains deferred.

## Self-Check: PASSED

- Verified all created/modified files listed in this summary exist.
- Verified task commit `bb7b514` exists in git history.
- Verified focused Phase 36 tests and builds passed.
- Verified no generated `sources/WebApp/UtfApi/**` files were hand-edited.

---
*Phase: 36-policy-gate-and-approval-ux*
*Completed: 2026-06-20*

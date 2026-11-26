---
phase: 36-policy-gate-and-approval-ux
plan: 02
subsystem: api
tags: [aspnet-core, policy-gate, approvals, sqlite-tests]

requires:
  - phase: 35-dry-run-planning-and-review
    provides: dry-run plans and review records for Arma 3 cloud planning
  - phase: 36-policy-gate-and-approval-ux
    provides: policy gate repository/UI boundary RED tests from Plan 01
provides:
  - Non-mutating Arma 3 dry-run policy review endpoint
  - Exact warning acknowledgement and approval continuation identity validation
  - Approve and deny authorization/audit regression coverage
affects: [policy-gate, cloud-planning, approval-ux, provisioning-gates]

tech-stack:
  added: []
  patterns: [ASP.NET Core controller gate endpoint, EF Core Sqlite policy tests, server-side continuation identity checks]

key-files:
  created:
    - .planning/phases/36-policy-gate-and-approval-ux/36-02-SUMMARY.md
  modified:
    - sources/Api/Controllers/CloudPlanningController.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs

key-decisions:
  - "Policy review evaluates the persisted dry-run plan through ICloudSpendingPolicyGateService and does not call provider provisioning, teardown, lifecycle enqueue, Steam, or Arma runtime services."
  - "Warning acknowledgement and approved continuation now require the exact persisted decision id, preventing same-scope stale decision replay."
  - "Approve/deny hardening remains on Claims.Administrative.Server for Phase 36, matching the approved plan scope."

patterns-established:
  - "Review endpoints may expose gate state as CloudSpendingPolicyGateResultDto while leaving billable mutation to later explicit continuation endpoints."
  - "Approval continuation is validated by request status, expiration, decision id, dry-run plan id, resource id, and persisted scope."

requirements-completed: [POLICY-02, POLICY-03]

duration: 55min
completed: 2026-06-20
---

# Phase 36 Plan 02: API Policy-Review Endpoint And Approval Hardening Summary

**Server-authoritative Arma 3 dry-run policy review with exact continuation identity checks and durable approve/deny audit coverage**

## Performance

- **Duration:** 55 min
- **Started:** 2026-06-20T10:08:04Z
- **Completed:** 2026-06-20T11:02:59Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- Added `ReviewArma3DryRunPolicyAsync` at `POST /game-servers/{gameServerId}/cloud-dry-run-plans/arma3/policy-review`.
- Hardened warning acknowledgement and approved continuation so stale same-scope decision ids cannot bypass the server gate.
- Added focused API tests for allow, warning, approval-required, denied, stale/mismatched approvals, admin authorization, persisted resolver/reason/timestamp fields, audit JSON, and double-resolution rejection.

## Task Commits

1. **Task 1: Add API tests for non-mutating policy review and exact continuation blocking** - `66af5f3` (test)
2. **Task 2: Implement non-mutating Arma 3 dry-run policy review endpoint** - `3b187c9` (feat)
3. **Task 3: Harden approver approve and deny authorization/audit tests** - `4d802f5` (test)

**Plan metadata:** pending final metadata commit.

## Files Created/Modified

- `sources/Api/Controllers/CloudPlanningController.cs` - Added the non-mutating policy review endpoint and gate service dependency.
- `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs` - Added exact decision-id checks for warning acknowledgements and approved continuation.
- `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs` - Added policy-review endpoint tests for all gate outcomes.
- `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs` - Added stale/mismatched/expired/denied continuation blocking coverage.
- `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs` - Added approve/deny admin authorization and durable audit persistence coverage.
- `.planning/phases/36-policy-gate-and-approval-ux/36-02-SUMMARY.md` - Plan execution summary.

## Decisions Made

- Kept policy review read-only with respect to provider/lifecycle/runtime services; only existing policy gate decision/approval records may be produced by the gate service.
- Required exact persisted decision identity for warning acknowledgement and approval continuation, not just same-scope matching.
- Kept approve/deny authority on `Claims.Administrative.Server` per the Phase 36 plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Hardened approval service despite under-declared file list**
- **Found during:** Task 2
- **Issue:** The plan behavior and threat model required stale, mismatched, expired, denied, and wrong-decision continuation ids to remain blocked, but the frontmatter `files_modified` list did not include `CloudSpendingPolicyApprovalService.cs`.
- **Fix:** Added exact `DecisionFk` and warning decision primary-key checks in `CloudSpendingPolicyApprovalService`.
- **Files modified:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs`
- **Verification:** Focused policy tests and API build passed.
- **Committed in:** `3b187c9`

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** Required for POLICY-02 correctness and T-36-06 mitigation. No new architecture, dependency, provider mutation, or UI scope was added.

## Issues Encountered

- Task 3 hardening tests passed against existing production approve/deny logic, so no production change was needed for POLICY-03.
- Existing build/test warning noise was observed in unrelated files during `dotnet test`; the plan-level `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` completed with 0 warnings and 0 errors.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyControllerTests" --verbosity minimal` - Passed, 39 tests.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - Passed, 0 warnings, 0 errors.

Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by explicit instruction.

## Known Stubs

None.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: endpoint | `sources/Api/Controllers/CloudPlanningController.cs` | New authenticated, resource-authorized policy-review endpoint. Mitigated by `Claims.ResourceBased.Server.Configuration`, route/payload game-server matching, database-user resolution, active-server check, required dry-run id, and non-mutating gate-service-only implementation. |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for Phase 36 Plan 03/04 repository and UI work. API policy review and approval hardening are available behind automated tests, with live/manual UAT still deferred.

## Self-Check: PASSED

- Verified all created/modified files listed in this summary exist.
- Verified task commits `66af5f3`, `3b187c9`, and `4d802f5` exist in git history.

---
*Phase: 36-policy-gate-and-approval-ux*
*Completed: 2026-06-20*

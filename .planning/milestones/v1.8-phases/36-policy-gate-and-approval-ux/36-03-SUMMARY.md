---
phase: 36-policy-gate-and-approval-ux
plan: 03
subsystem: webapp-repository
tags: [blazor, kiota, policy-gate, approvals]

requires:
  - phase: 36-policy-gate-and-approval-ux
    provides: API policy-review endpoint and approval hardening from Plan 02
  - phase: 36-policy-gate-and-approval-ux
    provides: repository and UI boundary tests from Plan 01
provides:
  - Safe Razor-facing policy approval models
  - Repository-local Kiota request sender for policy approve and deny commands
  - WebApp repository facade for policy review, refresh, approve, and deny workflows
affects: [policy-gate, cloud-planning, approval-ux, webapp-repository]

tech-stack:
  added: []
  patterns: [Repository facade, Kiota RequestInformation gap handling, redaction-guarded safe model projection]

key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalCommandPayload.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalCommandSender.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalModels.cs
    - .planning/phases/36-policy-gate-and-approval-ux/36-03-SUMMARY.md
  modified:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs

key-decisions:
  - "Generated approve and deny builders remain absent, so Phase 36 uses a repository-local Kiota RequestInformation helper instead of hand-editing generated UtfApi files."
  - "Razor-facing approval state is projected through allowlisted models and CloudWorkflowRedactionGuard rather than exposing generated DTOs or raw transport data."
  - "Warning acknowledgement and approved continuation are represented as typed repository parameters only; server-side policy validation remains authoritative."

patterns-established:
  - "Generated-client gaps can be bridged with repository-local Kiota RequestInformation helpers when generation is unavailable, keeping raw HTTP out of Razor and service repositories."
  - "Policy approval command results should include safe status, summary, request, rows, and refresh capability without leaking AdditionalData, raw JSON, headers, or exception bodies."

requirements-completed: [POLICY-01, POLICY-02, POLICY-03]

duration: interrupted/recovered
completed: 2026-06-20
---

# Phase 36 Plan 03: WebApp Repository Policy Approval Surface Summary

**Safe policy-review and approval command repository facade for the `/servers` UX**

## Performance

- **Duration:** Interrupted executor recovered inline.
- **Completed:** 2026-06-20T13:31:01Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Added safe policy approval command payload, request, status, and command-result models for Razor consumption.
- Extended `CloudWorkflowResultMapper` to map policy-review and approval transports into redaction-guarded repository models.
- Added a repository-local Kiota `RequestInformation` sender for approve, deny, and Arma 3 policy-review routes without hand-editing `sources/WebApp/UtfApi/**`.
- Exposed `ReviewArma3CloudDryRunPolicyAsync`, `RefreshCloudPolicyStatusAsync`, `ApprovePolicyRequestAsync`, and `DenyPolicyRequestAsync` through `IGameServerRepository`.

## Task Commits

1. **Task 1: Add safe policy approval models and command payload** - `d704390` (feat)
2. **Tasks 2/3: Add generated-client gap handling and repository facade methods** - `99f78a6` (feat)

**Plan metadata:** pending final metadata commit.

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalCommandPayload.cs` - Kiota-serializable approve/deny command payload limited to `reason` and `timeStampExpires`.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalModels.cs` - Safe approval request, command, command-result, and status models.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalCommandSender.cs` - Repository-local Kiota request helper for policy review and approve/deny generated-client gaps.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Safe mapping from policy gate review and approval transports.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - Added policy review, refresh, approve, and deny repository method contracts.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Implemented the Phase 36 policy gate repository facade.
- `.planning/phases/36-policy-gate-and-approval-ux/36-03-SUMMARY.md` - Plan execution summary.

## Decisions Made

- Used repository-local Kiota `RequestInformation` helpers because generated approve/deny route builders were unavailable and generated `UtfApi` files must remain untouched.
- Kept all policy and approval transport projection behind repository-safe models so Razor does not bind generated DTOs or raw JSON-bearing objects.
- Returned refresh-capable safe action results for policy visibility and approval commands so the UI can recover after approve/deny without duplicating transport logic.

## Deviations from Plan

### Auto-fixed Issues

**1. [Execution Recovery] Original executor was shut down after hanging**
- **Found during:** Task 2/3 execution
- **Issue:** The Plan 03 executor stopped making progress and produced partial code changes without commits or summary.
- **Fix:** Closed the executor, verified the partial implementation in the main session, committed the already-completed model work, completed/staged the repository facade work, reran verification, and committed the recovery.
- **Files modified:** Plan-declared files only.
- **Verification:** WebApp build and repository boundary tests passed.
- **Committed in:** `d704390`, `99f78a6`

---

**Total deviations:** 1 recovered execution interruption
**Impact on plan:** No product scope change. Tasks 2 and 3 were combined into one recovery commit because the interrupted partial changes were already interdependent.

## Issues Encountered

- The generated client did not provide approve/deny route builders, so the approved repository-local Kiota sender path was used.
- The original Plan 03 subagent was closed after repeated timeouts; no unreviewed subagent work was committed without build/test verification.

## Verification

- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` - Passed, 0 warnings, 0 errors.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` - Passed, 13 tests.

Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by explicit instruction.

## Known Stubs

None.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: generated-client-gap | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPolicyApprovalCommandSender.cs` | Approve/deny routes use repository-local Kiota request construction because generated builders are absent. Mitigated by keeping helper internal to the repository boundary, using Kiota serialization, and avoiding raw browser HTTP or hand-edited generated code. |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for Phase 36 Plan 04. The `/servers` UI can now consume repository-safe policy review, refresh, approve, and deny operations. Manual UAT remains deferred.

## Self-Check: PASSED

- Verified all created/modified files listed in this summary exist.
- Verified task commits `d704390` and `99f78a6` exist in git history.
- Verified no `sources/WebApp/UtfApi/**` files were hand-edited.

---
*Phase: 36-policy-gate-and-approval-ux*
*Completed: 2026-06-20*

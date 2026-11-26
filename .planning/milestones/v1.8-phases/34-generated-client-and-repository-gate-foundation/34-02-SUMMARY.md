---
phase: 34-generated-client-and-repository-gate-foundation
plan: 02
subsystem: webapp-repository
tags: [blazor, webapp, repository, kiota, redaction, lifecycle]
requires:
  - phase: 34-generated-client-and-repository-gate-foundation
    plan: 01
    provides: typed cloud workflow result taxonomy and mapper foundation
provides:
  - Generated-client-backed cloud workflow read paths for lifecycle jobs, billing owners, audit records, and billing accounts
  - ELifecycleJobKind-based lifecycle action facade returning typed cloud workflow results
  - Legacy lifecycle methods delegated to generated builders where supported
  - Audit workflow summaries that omit raw audit details
affects: [phase-34, cost-03]
tech-stack:
  added: []
  patterns:
    - Generated Kiota request builders behind IGameServerRepository
    - Repository-local generated DTO to record mapping
    - Broad cloud workflow lifecycle result category with contract lifecycle kind discriminator
key-files:
  created:
    - .planning/phases/34-generated-client-and-repository-gate-foundation/34-02-SUMMARY.md
  modified:
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
key-decisions:
  - "Lifecycle action selection uses the existing shared ELifecycleJobKind contract enum; CloudWorkflowActionKind remains the broad Lifecycle result category."
  - "UpgradeGameServerAsync remains on the existing legacy raw path because Upgrade is not an ELifecycleJobKind member."
  - "CleanupCloudRuntimeAsync remains an explicitly named legacy path because this plan did not identify a generated cleanup builder to use."
requirements-completed: [COST-03]
duration: 55min
completed: 2026-06-19
---

# Phase 34 Plan 02: Generated Client Repository Cleanup Summary

**Cloud workflow repository reads and supported lifecycle actions now use generated Kiota builders behind `IGameServerRepository`, with typed redacted lifecycle results.**

## Performance

- **Duration:** 55 min
- **Completed:** 2026-06-19
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Replaced raw HTTP reads for lifecycle jobs, active billing owner, billing owner history, audit records, and billing accounts with generated request builders.
- Added generated DTO to repository record mappers for `LifecycleJobDto`, `GameServerBillingAssignmentDto`, `HostingAuditRecordDto`, and `BillingAccountDto`.
- Added `RunCloudLifecycleActionAsync` using `X39.UnitedTacticalForces.Contract.GameServer.ELifecycleJobKind` as the lifecycle action discriminator.
- Routed install, start, stop, restart, update, collect logs, and delete through generated builders.
- Delegated existing legacy lifecycle methods to the generated helper where supported.
- Kept `UpgradeGameServerAsync` and `CleanupCloudRuntimeAsync` on explicit legacy paths because the selected discriminator/builder set does not cover them.
- Kept cloud workflow audit summaries allowlisted by omitting raw audit `Details` from `CloudWorkflowAuditSummary`.

## Task Commits

1. **Task 1: Convert cloud workflow read paths to generated builders** - `0e10a76` (feat)
2. **Task 2: Add lifecycle action result facade methods** - `13f6b25` (feat)

## Files Created/Modified

- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - Added the ELifecycleJobKind-based cloud lifecycle action result facade method.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Replaced cloud workflow reads and supported lifecycle actions with generated builders.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Added generated DTO mappers and unsupported lifecycle validation result mapping.
- `.planning/phases/34-generated-client-and-repository-gate-foundation/34-02-SUMMARY.md` - Execution summary.

## Decisions Made

- Used `ELifecycleJobKind` instead of expanding `CloudWorkflowModels.CloudWorkflowActionKind`, honoring the continuation decision and write scope.
- Returned a typed `ValidationBlocked` result for unsupported lifecycle kinds instead of adding new model enum members.
- Preserved legacy raw helper usage only for paths outside this plan's generated-supported lifecycle scope: billing account creation, billing owner assignment, game server creation, upgrade, and cloud runtime cleanup.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 4 - Decision] Lifecycle action discriminator conflict resolved by user decision**
- **Found during:** Task 2
- **Issue:** The plan asked for `CloudWorkflowActionKind` to identify start/stop/restart/install/update/upgrade/delete/collect-log actions, but the existing enum only had a broad `Lifecycle` value and `CloudWorkflowModels.cs` was outside write scope.
- **Fix:** Continued with the user's selected option 2: use `ELifecycleJobKind` as the method discriminator while keeping result `ActionKind = Lifecycle`.
- **Files modified:** `IGameServerRepository.cs`, `GameServerRepositoryImpl.cs`, `CloudWorkflowResultMapper.cs`
- **Commit:** `13f6b25`

## TDD Gate Compliance

The plan tasks were marked `tdd="true"`, but the user-provided write scope allowed only three implementation files and this summary. No test files were created because that would have violated the scoped instruction. Verification used the plan's build and boundary scans.

## Issues Encountered

- `gsd-tools` was not available on PATH, so SDK-driven state/roadmap/requirements updates were not run.
- The repository had pre-existing unrelated dirty items: `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/`. They were left untouched.
- `dotnet build` passes with existing generated Kiota nullable-context warnings under `sources/WebApp/UtfApi/**`; generated files were not edited.

## Verification

- PASS: `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- PASS: `rg -n "RequestBuilder|UtfApi|HttpClient" sources\WebApp\Pages\Servers` returned no matches.
- PASS: `rg -n "GetFromJsonAsync|PostAsJsonAsync|PostJsonRequiredAsync|BaseUrl.ResolveApiUrl" sources\WebApp\Services\GameServerRepository\GameServerRepositoryImpl.cs` found no matches for lifecycle jobs, billing owner/history, audit records, billing accounts, or generated-supported lifecycle actions. Remaining matches are legacy create/assign/upgrade/cleanup helper paths outside this plan's generated-supported scope.
- DEFERRED: Manual/browser/authenticated UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by operator instruction.

## Known Stubs

None. Stub scan found one intentional "not available" validation message for unsupported lifecycle kinds; it is a real `ValidationBlocked` result path, not a placeholder.

## Threat Flags

None.

## User Setup Required

None.

## Self-Check: PASSED

- Found summary file path.
- Found commits `0e10a76` and `13f6b25` in git history.
- Build and repository-boundary scans passed with only noted generated-file warnings, legacy-scope raw helper matches, and the intentional unsupported-lifecycle validation message.

---
*Phase: 34-generated-client-and-repository-gate-foundation*
*Completed: 2026-06-19*

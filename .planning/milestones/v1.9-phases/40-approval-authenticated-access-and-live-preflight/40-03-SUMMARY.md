---
phase: 40-approval-authenticated-access-and-live-preflight
plan: 03
subsystem: api-webapp
tags: [aspnet-core, blazor, live-preflight, provisioning-gate, redaction]
requires:
  - phase: 40-01
    provides: Durable live preflight records, DTOs, payloads, migration, and audit kinds.
  - phase: 40-02
    provides: Approval/access evidence service and server-scoped preflight routes.
provides:
  - Provider, metadata, billing-owner, dry-run, cost, policy, and reconciliation readiness computation.
  - Server-scoped provisioning guard that validates verified preflight before policy/provider mutation.
  - WebApp repository facade, safe transports, and `/servers` plus `/servers/cloud` preflight visibility.
  - API readiness matrix tests and WebApp repository/UI boundary tests.
affects: [phase-40, phase-41, cloud-live-preflight, cloud-provisioning-gate, webapp-cloud-workflow]
tech-stack:
  added: []
  patterns: [repository-local Kiota RequestInformation sender, safe display projection, provisioning preflight gate, aggregate readiness row]
key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightCommandSender.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightModels.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightTransport.cs
    - tests/Api.Tests/CloudPreflight/CloudLivePreflightReadinessTests.cs
  modified:
    - sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Pages/Servers/Index.razor
    - sources/WebApp/Pages/Servers/Index.razor.css
    - sources/WebApp/Pages/Servers/Cloud.razor
    - sources/WebApp/Pages/Servers/Cloud.razor.css
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs
key-decisions:
  - "Aggregate readiness is recorded as an OverallReadiness preflight row with safe blocker codes."
  - "ProvisionReviewedCloudDryRunPlanAsync validates verified preflight before spending-policy evaluation and before provider provisioning."
  - "WebApp preflight endpoints use repository-local Kiota RequestInformation helpers because generated builders are absent and generated UtfApi files remain untouched."
  - "Display records omit provider token references; approval command transport serializes only a reference field for API submission."
patterns-established:
  - "Phase 40 readiness refresh recomputes current persisted state instead of trusting stale client status."
  - "Server-owner and admin pages show compact preflight evidence rows and blockers without session, credential, token, or raw payload material."
requirements-completed: [LIVE-01, LIVE-02, LIVE-03]
duration: 45min
completed: 2026-06-21
---

# Phase 40 Plan 03 Summary

**Provider/billing/dry-run/cost/policy/reconciliation readiness is computed, enforced before live provisioning, and visible in the WebApp.**

## Performance

- **Duration:** 45 min
- **Started:** 2026-06-21T19:11:00+02:00
- **Completed:** 2026-06-21T19:36:00+02:00
- **Tasks:** 4
- **Files modified:** 13

## Accomplishments

- Extended `CloudLivePreflightService` with readiness refresh and provisioning validation that checks provider configuration, metadata freshness, active billing owner, selected dry-run plan, cost budget, policy approval, reconciliation blockers, and cleanup window.
- Added a `POST /game-servers/{gameServerId}/cloud-live-preflight/refresh` route and preflight validation before the reviewed dry-run plan provisioning path can reach policy or provider mutation code.
- Added WebApp repository methods and safe Kiota transports for preflight read, approval, access-evidence, and readiness refresh operations.
- Added compact live-preflight panels on `/servers` and `/servers/cloud` with approved labels, status badges, blocker lists, and no secret/session material in display models.
- Added readiness tests and WebApp boundary tests covering blocker categories, mutation blocking, repository facade methods, local Kiota sender usage, and UI labels.

## Task Commits

1. **Task 1: Add readiness computation and provisioning gate** - `6605731` (`feat`)
2. **Task 2: Add readiness and provider-mutation blocking tests** - `40c1977` (`test`)
3. **Task 3: Add WebApp repository and UI preflight visibility** - `1ffde8c` (`feat`)
4. **Verification support: Resolve archived Phase 39 evidence path** - `03d3dce` (`test`)

**Plan metadata:** this summary commit

## Files Created/Modified

- `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs` - Readiness computation and verification guard.
- `sources/Api/Controllers/GameServersController.cs` - Refresh endpoint and provisioning preflight gate.
- `tests/Api.Tests/CloudPreflight/CloudLivePreflightReadinessTests.cs` - Ready-state, blocker, and provisioning-block coverage.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightCommandSender.cs` - Repository-local Kiota request construction.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightModels.cs` - Safe WebApp-facing preflight models.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightTransport.cs` - Kiota parse/serialize transports for preflight routes.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` and `GameServerRepositoryImpl.cs` - Preflight facade methods.
- `sources/WebApp/Pages/Servers/Index.razor` and `.css` - Server-owner preflight visibility and refresh action.
- `sources/WebApp/Pages/Servers/Cloud.razor` and `.css` - Admin preflight summary/detail visibility.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - WebApp repository/UI boundary coverage.

## Decisions Made

- Readiness is recomputed from persisted control-plane state when refreshed or validated, so stale client data cannot unlock live provider mutation.
- Readiness blockers are stored as safe reason codes/strings and displayed as operational blockers, not raw provider/API/session payloads.
- Phase 40 does not claim live provider/browser approval evidence. The implementation is ready, but actual operator evidence remains `human_needed`.

## Deviations from Plan

- The WebApp used repository-local Kiota `RequestInformation` helpers instead of generated builders because the generated client did not yet include the new preflight routes. Generated files were intentionally left untouched.
- The `Record preflight approval` UI control is present with the approved label but remains disabled until an operator-facing form is intentionally added; API and repository methods are ready for that follow-up.

## Issues Encountered

- Existing controller tests construct `GameServersController` directly, so preflight validation remains optional in tests that do not inject `ICloudLivePreflightService`; production DI still provides the service.
- Generated Kiota policy models continue to emit existing nullable-context warnings during WebApp and solution builds.
- Final full-suite verification exposed that `VerificationHandoffBoundaryTests` still looked for Phase 39 handoff files in the active phase directory after v1.8 archival; the test now falls back to `.planning/milestones/v1.8-phases`.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflightReadinessTests" --no-restore --verbosity minimal` passed: 12 tests, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflightReadinessTests|FullyQualifiedName~CloudInventoryVisibilityTests" --no-restore --verbosity minimal` passed: 22 tests, 0 failed.
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal` passed with existing generated Kiota nullable warnings.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --no-restore --verbosity minimal` passed: 27 tests, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflight|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --no-restore --verbosity minimal` passed: 46 tests, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` passed with existing generated Kiota nullable warnings.
- `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` passed: parser tests 89 passed; API tests 387 passed.
- Display-surface forbidden marker scan across changed preflight UI/models and Phase 40 artifacts returned no matches.

## User Setup Required

Operator action is still required before live provider mutation:

- Record a fresh approval package with budget, cleanup window, selected server, billing account, assignment, dry-run plan, token reference, and policy approval identifier when needed.
- Verify authenticated administrator and server-owner access evidence.
- Refresh preflight readiness and confirm the latest `OverallReadiness` row is `Verified`.

## Next Phase Readiness

Phase 41 implementation can start from the guarded provisioning path, but actual live provisioning remains blocked until operator-owned Phase 40 preflight evidence is recorded and verified for the selected server/plan/owner.

---
*Phase: 40-approval-authenticated-access-and-live-preflight*
*Completed: 2026-06-21*

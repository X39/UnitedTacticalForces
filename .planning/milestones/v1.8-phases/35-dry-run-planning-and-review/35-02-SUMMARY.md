---
phase: 35-dry-run-planning-and-review
plan: 02
subsystem: api
tags: [cloud-planning, dry-run-review, redaction, ef-core, testing]

requires:
  - phase: 35-dry-run-planning-and-review
    provides: durable dry-run fingerprints, safe summaries, and supersession from Plan 01
provides:
  - Owner-safe Arma 3 dry-run review DTO contracts
  - Authenticated route-scoped dry-run review endpoint
  - Providerless review service with stale, invalid, mismatched, superseded, and owner-mismatched states
  - Focused API/service tests for projection, states, and redaction boundaries
affects: [phase-35, dry-run-review, cloud-provisioning-gates, generated-client-plan-04]

tech-stack:
  added: []
  patterns:
    - Providerless review projection from persisted dry-run plan, session, inventory, cost, and cached metadata rows
    - Owner-facing DTO allowlist that omits raw JSON, AdditionalData, token references, credentials, headers, and worker hashes
    - Server-side recomputation of normalized non-secret input fingerprints for review mismatch detection

key-files:
  created:
    - sources/Api/DTO/Arma3CloudDryRunPlanReviewDto.cs
    - sources/Api/DTO/Arma3CloudDryRunPlanReviewStateDto.cs
    - sources/Api/DTO/Arma3CloudDryRunPlanReviewResourceDto.cs
    - sources/Api/DTO/Arma3CloudDryRunPlanCostBreakdownDto.cs
    - sources/Api/DTO/Payloads/Arma3CloudDryRunPlanReviewRequestPayload.cs
    - sources/Api/Services/CloudPlanning/IArma3CloudDryRunPlanReviewService.cs
    - sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs
    - tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewApiTests.cs
  modified:
    - sources/Api/Controllers/CloudPlanningController.cs

key-decisions:
  - "Dry-run review is a non-mutating API projection over persisted local control-plane records and cached metadata."
  - "Review usability is classified server-side before later provider mutation can consume a plan."
  - "Owner-facing review contracts expose typed allowlisted collections and cost labels instead of raw plan/resource/estimate JSON."

patterns-established:
  - "Review services recompute the current non-secret input fingerprint and compare it to the persisted Plan 01 fingerprint."
  - "Review endpoints use the same resource-scoped authorization and safe HTTP result pattern as dry-run creation."

requirements-completed: [PLAN-02, PLAN-03]

duration: 12min
completed: 2026-06-20
---

# Phase 35 Plan 02: Owner-Safe Dry-Run Review API And Projection Summary

**Providerless Arma 3 dry-run review API with typed safe plan details, cost labels, and explicit unusable-state classification.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-06-20T03:04:11Z
- **Completed:** 2026-06-20T03:15:41Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- Added owner-safe dry-run review DTOs with typed planned resources, assumptions, firewall rules, validation failures, metadata freshness, estimate totals, and cost breakdown labels.
- Added a providerless `Arma3CloudDryRunPlanReviewService` that selects requested/latest plans, recomputes current input fingerprints, compares active owners, checks supersession and metadata freshness, and projects persisted rows without raw JSON exposure.
- Added `POST game-servers/{gameServerId}/cloud-dry-run-plans/arma3/review` behind the existing resource configuration policy.
- Added focused tests for valid review projection, invalid/stale/input-mismatched/superseded/owner-mismatched/no-plan states, route authorization metadata, no provider-client dependency, and forbidden public DTO properties.

## Task Commits

1. **Task 1: Define review contracts and failing projection tests** - `66af8a3` (test)
2. **Task 2: Implement review service and controller endpoint** - `c57337c` (feat)

**Plan metadata:** `bdbd41e` (docs: complete plan)

## Files Created/Modified

- `sources/Api/DTO/Arma3CloudDryRunPlanReviewDto.cs` - Owner-safe review projection.
- `sources/Api/DTO/Arma3CloudDryRunPlanReviewStateDto.cs` - Distinct review usability states.
- `sources/Api/DTO/Arma3CloudDryRunPlanReviewResourceDto.cs` - Safe planned-resource rows without provider resource ids or details JSON.
- `sources/Api/DTO/Arma3CloudDryRunPlanCostBreakdownDto.cs` - Cost estimate breakdown with `Dry-run estimate` label and freshness.
- `sources/Api/DTO/Payloads/Arma3CloudDryRunPlanReviewRequestPayload.cs` - Non-mutating review payload with optional plan id and current selected Arma 3 inputs.
- `sources/Api/Services/CloudPlanning/IArma3CloudDryRunPlanReviewService.cs` - Review service contract.
- `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs` - Providerless review classification and projection implementation.
- `sources/Api/Controllers/CloudPlanningController.cs` - Authenticated review endpoint under the existing cloud dry-run route.
- `tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewApiTests.cs` - RED/GREEN API and service tests for review projection, states, and redaction.

## Decisions Made

- Review projections are built from persisted plan/session/resource/estimate rows plus cached metadata status; no provider client or direct HTTP dependency was added.
- `CanProvision` is true only for `ValidFresh`; every unusable state returns a correction or regeneration next step.
- Raw `ValidationDetailsJson`, `PlannedOperationsJson`, `DetailsJson`, `AdditionalData`, token references, credentials, authorization headers, bearer tokens, worker hashes, and raw payload fields are excluded from public owner-facing review DTO properties.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- RED verification failed as expected before Task 2 because `Arma3CloudDryRunPlanReviewService` did not exist.
- First GREEN compile failed on a missing enum namespace import in the new service; fixed before the Task 2 commit.
- One stale-metadata test initially updated a detached fixture entity after `ChangeTracker.Clear`; fixed to update the tracked cached metadata row before the Task 2 commit.
- Existing API XML documentation warnings and existing test nullable/analyzer warnings still appear during focused test runs outside this plan's changes.

## Verification

- RED expected failure: `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewApiTests" --verbosity minimal` failed before implementation because the review service did not exist.
- PASS: `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlanReviewApiTests|FullyQualifiedName~Arma3CloudPlanReviewStateTests" --verbosity minimal` after implementation: 9 passed.
- PASS: Provider-client/source scan found provider-client markers only in negative test assertions, not in the review service/controller.
- PASS: Stub marker scan over touched files found no TODO/FIXME/placeholder/coming-soon/not-available markers.
- DEFERRED: Manual, authenticated browser, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 UAT remain deferred by instruction.

## Known Stubs

None.

## Threat Flags

None beyond the planned Phase 35 review trust boundaries. The new endpoint is authenticated/resource-scoped, recomputes the input fingerprint server-side, compares active ownership, and projects allowlisted typed fields only.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for Phase 35 Plan 03 to make provider-mutation blockers authoritative for stale, invalid, mismatched, superseded, and owner-mismatched dry-run plans. Deferred UAT remains open and must not be claimed by Phase 35.

## Self-Check: PASSED

- Found summary file: `.planning/phases/35-dry-run-planning-and-review/35-02-SUMMARY.md`
- Found review service: `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs`
- Found review tests: `tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewApiTests.cs`
- Found task commits `66af8a3` and `c57337c` in git history.

---
*Phase: 35-dry-run-planning-and-review*
*Completed: 2026-06-20*

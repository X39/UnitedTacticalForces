---
phase: 37-provisioning-runtime-operation-and-teardown
plan: 01
subsystem: api
tags: [aspnet-core, ef-core, cloud-provisioning, provider-teardown, xunit]

requires: []
provides:
  - Server-scoped `/game-servers/{gameServerId}/cloud-dry-run-plans/{dryRunPlanId}/provision` API route
  - Server-scoped `/game-servers/{gameServerId}/cloud-sessions/{sessionId}/provider-teardown` cost-stop API route
  - Owner-safe cloud status projection for cleanup, orphan, partial-failure, and remaining-billable states
affects: [phase-37, servers-workflow, cloud-provider-actions, cloud-status]

tech-stack:
  added: []
  patterns:
    - GameServersController delegates provider mutation to CloudProvisioningService and CloudTeardownService after route-scope and policy-gate checks.
    - CloudInventoryService exposes allowlisted status booleans/counts instead of raw provider or worker payloads.

key-files:
  created:
    - .planning/phases/37-provisioning-runtime-operation-and-teardown/37-01-SUMMARY.md
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/DTO/CloudSessionStatusDto.cs
    - sources/Api/Services/CloudInventory/CloudInventoryService.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
    - tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs
    - tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs

key-decisions:
  - "Server-scoped provider actions live in GameServersController, while CloudInventoryController remains the admin provider/fleet console."
  - "Provider teardown is exposed as an explicit POST provider-teardown route and delegates to CloudTeardownService as the cost-stop boundary."
  - "Remaining billable and partial-failure status is projected through CloudSessionStatusDto using allowlisted counts and booleans."

patterns-established:
  - "Route-scope validation before policy-gate evaluation for owner/operator provider actions."
  - "Controller provider actions perform authorization, route-scope, and policy checks only; provider calls remain in services."

requirements-completed: [OPS-01, OPS-03, OPS-04]

duration: 9 min
completed: 2026-06-20
---

# Phase 37 Plan 01: Server-Scoped Provisioning And Provider Teardown Summary

**Server-scoped provisioning and provider-teardown API routes with safe cleanup, orphan, partial-failure, and remaining-billable status projection**

## Performance

- **Duration:** 9 min
- **Started:** 2026-06-20T15:10:48Z
- **Completed:** 2026-06-20T15:19:45Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- Added owner/operator `/game-servers/{gameServerId}/cloud-dry-run-plans/{dryRunPlanId}/provision` with route-scope validation, policy-gate continuation ids, and delegation to `ICloudProvisioningService`.
- Added `/game-servers/{gameServerId}/cloud-sessions/{sessionId}/provider-teardown` as the explicit provider cost-stop action, with route-scope validation before policy-gate and teardown service calls.
- Extended `CloudSessionStatusDto` and `CloudInventoryService` with `HasPartialFailure`, `CleanupNeededResourceCount`, and `RemainingBillableResourceCount`.
- Added focused tests for blocked/wrong-route provisioning, provider teardown, safe status projection, and no secret-like status output.

## Task Commits

1. **Task 1: Add owner-safe provisioning and provider-teardown API/status tests** - `4337174` (test)
2. **Task 2: Implement server-scoped provision and provider-teardown endpoints** - `57c0942` (feat)

## Files Created/Modified

- `sources/Api/Controllers/GameServersController.cs` - Added server-scoped provisioning and provider-teardown routes plus route-scope helpers.
- `sources/Api/DTO/CloudSessionStatusDto.cs` - Added allowlisted partial-failure, cleanup-needed count, and remaining-billable count fields.
- `sources/Api/Services/CloudInventory/CloudInventoryService.cs` - Computes cleanup, orphan, partial-failure, retry, and billable-resource status from persisted inventory rows.
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` - Added RED/GREEN coverage for server-scoped provider actions and safe status projection.
- `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs` - Updated controller helper for new provider service dependencies.
- `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` - Updated controller helper with throwing provider service fakes.
- `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs` - Updated controller helper with existing recording provider service fakes.

## Decisions Made

- Server-scoped provider actions were added to `GameServersController` to keep `/servers` owner/operator workflow separate from admin `/servers/cloud`.
- Provider teardown uses `provider-teardown` in the route name and delegates to `CloudTeardownService`; runtime cleanup remains separate.
- Status projection exposes only booleans/counts derived from persisted inventory state, not provider payloads, token references, worker hashes, or credential material.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests" --verbosity minimal` - PASS, 25 tests.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - PASS, 0 warnings, 0 errors.
- Initial parallel rerun of test/build hit a transient DLL file lock; serial reruns passed and are the recorded verification result.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated constructor-only test helpers outside the named plan files**
- **Found during:** Task 2
- **Issue:** Adding `ICloudProvisioningService` and `ICloudTeardownService` to `GameServersController` broke existing API test helpers that manually construct the controller.
- **Fix:** Added throwing or recording provider service fakes to the affected helper constructors without changing their test behavior.
- **Files modified:** `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs`, `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs`
- **Verification:** Focused tests passed; API build passed.
- **Committed in:** `57c0942`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Required for build correctness after the new controller dependencies. No behavior outside Plan 37-01 was changed.

## Issues Encountered

- A parallel post-commit verification run hit a transient `obj` DLL file lock. Serial reruns of both required commands passed.
- `state.advance-plan` could not parse this repository's free-form current-position fields, so the plan position/progress lines were updated manually after the other GSD state handlers completed.

## Known Stubs

None. Stub scan found only intentional DTO empty-list defaults, nullable route/query parameters, and pre-existing guided-entry copy.

## Threat Flags

None. The new provider-action routes were already covered by the plan threat model boundaries for browser/repository to `GameServersController`, policy-gate evaluation, provider service delegation, and safe DTO projection.

## Authentication Gates

None.

## Deferred UAT

Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation/provisioning/teardown UAT, final provider billable-resource verification, and Phase 30 evidence collection remain deferred by user instruction. They were not run and are not claimed as passed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 37-02 can build runtime operation endpoints on top of the same server-scoped controller and safe status projection patterns. Generated WebApp client/repository integration remains for later Phase 37 plans.

## Self-Check: PASSED

- Summary file exists at `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-01-SUMMARY.md`.
- Task commits exist: `4337174`, `57c0942`.
- Required verification commands passed serially.

---
*Phase: 37-provisioning-runtime-operation-and-teardown*
*Completed: 2026-06-20*

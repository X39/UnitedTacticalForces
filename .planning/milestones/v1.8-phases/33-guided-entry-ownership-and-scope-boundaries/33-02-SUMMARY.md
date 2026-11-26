---
phase: 33-guided-entry-ownership-and-scope-boundaries
plan: 02
plan_name: Server-Side Guided Entry Authorization, Ownership, And Readiness Endpoints
subsystem: api
tags: [aspnet-core, authorization, billing-ownership, cloud-planning, testing]
requires:
  - 33-01 guided cloud entry DTO and payload contracts
provides:
  - API-enforced guided entry options for Arma 3 planning servers
  - Side-effect-light Arma 3 planning server creation with active owner assignment
  - Resource-scoped guided billing owner assignment and readiness validation
affects:
  - phase-33
  - phase-34
  - phase-35
  - guided-cloud-entry
  - generated-client
  - servers-ui
tech_stack:
  added: []
  patterns:
    - Reflection-safe controller tests for route metadata and direct invocation
    - Owner-scoped billing account projection using server-side actor checks
key_files:
  created:
    - tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs
key_decisions:
  - Guided entry readiness is a non-mutating server-side validation boundary; it does not create dry-run plans, cloud sessions, inventory records, cost estimates, or lifecycle jobs.
  - Non-admin billing owner options and assignments are limited to active user-owned billing accounts for the actor; admins/server admins receive active accounts only.
  - New guided Arma 3 planning servers grant the creator direct server access when needed without queuing local install lifecycle work.
patterns_established:
  - Guided entry endpoints live under game-servers/cloud-entry and game-servers/{gameServerId}/cloud-entry with explicit authorization policies.
  - Controller tests use SQLite direct invocation plus reflection so route metadata, owner scope, readiness, and no-side-effect behavior stay covered.
requirements_completed: [CREATE-01, CREATE-02, CREATE-03, CREATE-04]
metrics:
  duration: 12 minutes
  tasks_completed: 2
  files_changed: 2
  completed_at: 2026-06-19T09:47:11Z
---

# Phase 33 Plan 02: Server-Side Guided Entry Authorization, Ownership, And Readiness Endpoints Summary

Guided Arma 3 cloud-entry API endpoints now enforce authorization, billing ownership scope, and readiness validation without invoking provider, dry-run, session, inventory, cost, or lifecycle side effects.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add guided cloud entry API tests | ce3a0f7 | `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` |
| 2 | Implement guided cloud entry API endpoints | fae0508 | `sources/Api/Controllers/GameServersController.cs`, `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` |

## Implementation

Added four guided-entry controller methods to `GameServersController`:

- `GET game-servers/cloud-entry/options` returns active, resource-accessible Arma 3 server options plus active billing owner choices scoped to the actor.
- `POST game-servers/cloud-entry/arma3-server` creates a stopped Arma 3 planning server, assigns an active billing owner, and grants creator access when needed.
- `POST game-servers/{gameServerId}/cloud-entry/billing-owner` updates guided-entry billing ownership for authorized users.
- `POST game-servers/{gameServerId}/cloud-entry/readiness` validates guided-entry selections and returns blockers without mutating cloud or lifecycle state.

The implementation reuses existing EF entities and services: `ApiDbContext`, `IResourceAuthorizationService`, `IBillingOwnershipService`, and `IGameServerControllerFactory`. It deliberately avoids provider clients, cloud provisioning services, teardown services, direct `HttpClient` use, lifecycle job queuing, dry-run plan creation, inventory creation, session creation, and cost estimate creation.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GuidedCloudEntry" --no-restore --verbosity minimal` failed during RED as expected with missing controller methods.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~GuidedCloudEntry" --no-restore --verbosity minimal` passed after implementation: 9 passed.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` passed. Remaining warnings are generated WebApp client nullable-context warnings.
- Static side-effect scan over the new guided-entry method ranges passed for forbidden provider, dry-run, teardown, lifecycle enqueue, and direct HTTP patterns.
- Manual, authenticated, UAT, live-provider, Steam, and Arma checks were deferred by user instruction.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Corrected admin owner-scope test expectation**

- **Found during:** Task 2
- **Issue:** The shared test seed helper already created a second active billing account; the admin owner-options test initially omitted it even though the plan requires admins to see all active accounts.
- **Fix:** Updated the expected admin owner-option set to include the seeded active account.
- **Files modified:** `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs`
- **Commit:** fae0508

## Known Stubs

None. Stub-pattern scan found only test literals, intentional null navigation properties, and existing nullable query parameters.

## Threat Flags

None. This plan adds planned authenticated API routes and keeps the new trust boundary server-side, with no secret exposure and no provider mutation path.

## Deferred Verification

UAT/manual/browser/authenticated/live-provider checks are deferred per user instruction. Follow-up verification should exercise the generated client and UI wiring once later Phase 33 plans expose these endpoints in the WebApp.

## Next Phase Readiness

The API now provides the server-side boundary needed for generated client updates and guided-entry UI wiring. Later plans can consume these endpoints without adding provider provisioning, billing invoices, cloud dry-run plans, or lifecycle install jobs.

## Self-Check: PASSED

- Found summary file: `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-02-SUMMARY.md`
- Found key implementation file: `sources/Api/Controllers/GameServersController.cs`
- Found key test file: `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs`
- Found task commit: `ce3a0f7`
- Found task commit: `fae0508`

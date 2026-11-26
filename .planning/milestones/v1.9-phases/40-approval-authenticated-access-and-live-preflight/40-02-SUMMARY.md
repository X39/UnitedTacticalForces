---
phase: 40-approval-authenticated-access-and-live-preflight
plan: 02
subsystem: api
tags: [aspnet-core, authorization, live-preflight, audit, redaction]
requires:
  - phase: 40-01
    provides: Durable live preflight entity, DTO, payload, migration, and audit event kinds.
provides:
  - Scoped preflight service for approval and authenticated-access evidence.
  - Server-scoped API routes for live preflight read, approval write, and access-evidence write.
  - Audit rows for approval and access evidence.
  - Controller/service tests for authorization metadata, persistence, audit, route mismatch, and forbidden material rejection.
affects: [phase-40, phase-41, cloud-live-preflight, server-scoped-api]
tech-stack:
  added: []
  patterns: [server-scoped controller routes, service-level relationship validation, forbidden evidence denylist, audit append after persistence]
key-files:
  created:
    - sources/Api/DTO/Payloads/CloudLivePreflightAccessEvidencePayload.cs
    - sources/Api/Services/CloudPreflight/ICloudLivePreflightService.cs
    - sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs
    - tests/Api.Tests/CloudPreflight/CloudLivePreflightApiTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
key-decisions:
  - "Mismatched selected preflight records return NotFound from route-scoped APIs to avoid disclosing records outside the server scope."
  - "Access evidence copies the latest verified approval package context instead of accepting owner/plan IDs in the access payload."
  - "Existing direct controller tests are kept stable by adding the preflight service as an optional final constructor dependency."
patterns-established:
  - "Live preflight writes validate active billing assignment, current dry-run plan, optional policy approval, and optional reconciliation state server-side."
  - "Authenticated access evidence persists only route, scope, outcome, timestamp, and note."
requirements-completed: [LIVE-01, LIVE-02]
duration: 24min
completed: 2026-06-21
---

# Phase 40 Plan 02 Summary

**Server-scoped live preflight APIs with approval/access evidence validation, audit attribution, and session-material rejection.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-06-21T18:55:00+02:00
- **Completed:** 2026-06-21T19:19:00+02:00
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments

- Added `CloudLivePreflightService` to record approval packages and authenticated-access evidence against the durable 40-01 entity.
- Added server-scoped `GameServersController` routes for reading preflight records, recording approval, and recording access evidence.
- Added API tests proving route authorization metadata, approval persistence, admin and server-owner access evidence, forbidden session material rejection, route mismatch handling, and audit row creation.

## Task Commits

1. **Task 1: Add service and payload for approval/access evidence** - `95e7e4a` (`feat`)
2. **Task 2: Add server-scoped preflight controller endpoints** - `5014950` (`feat`)
3. **Task 3: Add authorization and forbidden-material tests** - `17c3c3a` (`test`)

**Plan metadata:** this summary commit

## Files Created/Modified

- `sources/Api/DTO/Payloads/CloudLivePreflightAccessEvidencePayload.cs` - Access-evidence request model with route, scope, outcome, status, timestamp, and note.
- `sources/Api/Services/CloudPreflight/ICloudLivePreflightService.cs` - Preflight read/write service contract.
- `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs` - Relationship validation, bounded field normalization, forbidden-material rejection, and audit append behavior.
- `sources/Api/Controllers/GameServersController.cs` - Server-scoped live preflight endpoints.
- `tests/Api.Tests/CloudPreflight/CloudLivePreflightApiTests.cs` - Focused controller/service API coverage.

## Decisions Made

- Access evidence is attached to the latest verified approval package for the route-scoped server, avoiding a second set of owner/plan IDs in the access payload.
- Forbidden access evidence is rejected before persistence instead of relying only on audit sanitization.
- Mismatched selected records are treated as not found to preserve server-scope boundaries.

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- Existing direct controller tests instantiate `GameServersController` in many files, so the new service dependency was added as an optional final constructor parameter to avoid unrelated test churn.

## Verification

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` passed with existing XML documentation warnings.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflightApiTests" --no-restore --verbosity minimal` passed: 5 tests, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflight" --no-restore --verbosity minimal` passed: 7 tests, 0 failed.
- Production-source scan found forbidden markers only in the denylist and framework authorization imports.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 40-03 can compute provider, billing, dry-run, cost, policy, reconciliation, and aggregate readiness from the persisted approval/access evidence. Provider mutation remains blocked until aggregate readiness is verified.

---
*Phase: 40-approval-authenticated-access-and-live-preflight*
*Completed: 2026-06-21*

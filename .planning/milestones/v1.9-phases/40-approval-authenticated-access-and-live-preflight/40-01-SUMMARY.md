---
phase: 40-approval-authenticated-access-and-live-preflight
plan: 01
subsystem: database
tags: [ef-core, postgres, sqlite-tests, live-preflight, redaction]
requires:
  - phase: v1.8-phase-39
    provides: Phase 30 handoff and existing dry-run, policy, billing, cost, and reconciliation records.
provides:
  - Durable live preflight evidence entity and EF migration.
  - Secret-safe approval package DTO and creation payload.
  - Approval/preflight audit event kinds for later service workflows.
  - SQLite persistence and DTO redaction tests.
affects: [phase-40, phase-41, live-preflight, cloud-provisioning-gate]
tech-stack:
  added: []
  patterns: [bounded evidence fields, restrictive foreign keys, DTO allowlist mapping, SQLite persistence regression tests]
key-files:
  created:
    - sources/Api/Data/Hosting/CloudLivePreflightRecord.cs
    - sources/Api/DTO/CloudLivePreflightRecordDto.cs
    - sources/Api/DTO/Payloads/CloudLivePreflightApprovalPayload.cs
    - sources/Contract/GameServer/ECloudLivePreflightStatus.cs
    - sources/Contract/GameServer/ECloudLivePreflightEvidenceKind.cs
    - tests/Api.Tests/CloudPreflight/CloudLivePreflightRecordTests.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Transform.cs
    - sources/Contract/GameServer/EHostingAuditEventKind.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
key-decisions:
  - "Persist token references and allowlisted evidence summaries only; never raw provider, session, Steam, or worker secret material."
  - "Reference existing dry-run, billing, policy, and reconciliation records by foreign key instead of copying provider payloads into preflight evidence."
patterns-established:
  - "Live preflight evidence is a durable hosting aggregate with restrictive relationships to existing control-plane records."
  - "Browser-visible preflight data is projected through an allowlisted DTO."
requirements-completed: [LIVE-01, LIVE-03]
duration: 10min
completed: 2026-06-21
---

# Phase 40 Plan 01 Summary

**Durable live preflight approval records with safe DTO projection, EF migration, and persistence/redaction tests.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-06-21T18:44:00+02:00
- **Completed:** 2026-06-21T18:54:09+02:00
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments

- Added `CloudLivePreflightRecord` with selected server, billing account, active assignment, dry-run plan, optional policy approval, reconciliation run, approver, budget, lifetime, cleanup window, status, and bounded evidence fields.
- Added preflight status/evidence enums, hosting audit event kinds, DTO/payload mapping, and an EF Core migration for the new table and indexes.
- Added focused SQLite tests proving approval evidence survives context reload and browser-facing DTOs do not expose forbidden credential/session fields.

## Task Commits

1. **Task 1: Add preflight persistence contracts** - `c0f07ff` (`feat`)
2. **Task 2: Add DTO, payload, transform, and migration** - `bd10acf` (`feat`)
3. **Task 3: Add persistence and redaction tests** - `6b7238d` (`test`)

**Plan metadata:** this summary commit

## Files Created/Modified

- `sources/Api/Data/Hosting/CloudLivePreflightRecord.cs` - Durable evidence aggregate for Phase 40 approval/preflight records.
- `sources/Api/Data/ApiDbContext.cs` - DbSet and restrictive relationship configuration.
- `sources/Api/DTO/CloudLivePreflightRecordDto.cs` - Secret-safe response model for preflight state.
- `sources/Api/DTO/Payloads/CloudLivePreflightApprovalPayload.cs` - Approval package creation payload.
- `sources/Api/Transform.cs` - Entity-to-DTO projection.
- `sources/Contract/GameServer/ECloudLivePreflightStatus.cs` - Pending/verified/blocked readiness states.
- `sources/Contract/GameServer/ECloudLivePreflightEvidenceKind.cs` - Approval/access/readiness evidence categories.
- `sources/Contract/GameServer/EHostingAuditEventKind.cs` - Preflight audit event identifiers.
- `sources/Api/Data/Migrations/20260621164904_AddCloudLivePreflightRecords.cs` - EF migration for the new table.
- `sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs` - EF model snapshot update.
- `tests/Api.Tests/CloudPreflight/CloudLivePreflightRecordTests.cs` - Persistence and redaction coverage.

## Decisions Made

- Token references are persisted as references only; token values and session material remain outside the database and DTOs.
- Existing control-plane records are linked by foreign key so later readiness checks can validate current state without duplicating provider payloads.

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- SQLite round-tripped `DateTimeOffset` values at millisecond precision, so the test seed normalizes timestamps to match provider behavior.

## Verification

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` passed with existing XML documentation warnings.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudLivePreflightRecordTests" --no-restore --verbosity minimal` passed: 2 tests, 0 failed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 40-02 can now add service/controller behavior on top of the durable approval record. Provider mutation remains blocked until Plan 40-03 marks the selected preflight package complete and current.

---
*Phase: 40-approval-authenticated-access-and-live-preflight*
*Completed: 2026-06-21*

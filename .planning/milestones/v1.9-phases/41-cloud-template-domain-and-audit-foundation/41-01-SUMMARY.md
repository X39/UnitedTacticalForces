---
phase: 41-cloud-template-domain-and-audit-foundation
plan: 01
subsystem: database
tags: [ef-core, postgres, hosting, templates, audit]
requires:
  - phase: 40-approval-authenticated-access-and-live-preflight
    provides: cloud preflight and audit vocabulary foundation
provides:
  - durable cloud server template version snapshot entity
  - EF Core schema for current and historical template versions
  - template create/update/disable/selection/provisioning-use audit event kinds
affects: [cloud-template-management, cloud-planning, cloud-provisioning, audit]
tech-stack:
  added: []
  patterns: [ef-core-version-snapshot, filtered-current-index, restrictive-audit-relationships]
key-files:
  created:
    - sources/Api/Data/Hosting/CloudServerTemplate.cs
    - sources/Api/Data/Migrations/20260621222446_AddCloudServerTemplates.cs
    - sources/Api/Data/Migrations/20260621222446_AddCloudServerTemplates.Designer.cs
  modified:
    - sources/Api/Data/ApiDbContext.cs
    - sources/Contract/GameServer/EHostingAuditEventKind.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs
key-decisions:
  - "Template versions use one row per snapshot with TemplateId plus Version and a filtered current-version index."
  - "Port fields use generic query/platform terminology instead of Steam-specific names to keep later DTO redaction checks clean."
patterns-established:
  - "Template history is preserved by appending version rows and linking superseded rows to replacement rows."
  - "Template schema relationships to users and superseded versions use restrictive delete behavior."
requirements-completed: [TPL-01, TPL-04]
duration: 23 min
completed: 2026-06-21
---

# Phase 41 Plan 01: Cloud Template Domain And Audit Foundation Summary

**Versioned EF Core cloud server template schema with restrictive audit relationships and template audit event vocabulary**

## Performance

- **Duration:** 23 min
- **Started:** 2026-06-21T22:02:00Z
- **Completed:** 2026-06-21T22:25:30Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Added `CloudServerTemplate` as a durable version snapshot entity with stable `TemplateId`, monotonic `Version`, current/enabled state, shape defaults, bounds, actors, reason, and timestamps.
- Wired `ApiDbContext.CloudServerTemplates` with restrictive user/supersession relationships, unique version indexing, a filtered current-template index, and an active lookup index.
- Generated `AddCloudServerTemplates` migration and updated the EF model snapshot.
- Added explicit template audit event values 37 through 41.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add template version entity and EF model wiring** - `ccf414f` (feat)
2. **Task 2: Generate and verify the template schema migration** - `7ec31d4` (feat)

**Plan metadata:** pending current commit

## Files Created/Modified

- `sources/Api/Data/Hosting/CloudServerTemplate.cs` - Durable template version snapshot entity.
- `sources/Api/Data/ApiDbContext.cs` - DbSet, indexes, and restrictive relationships.
- `sources/Contract/GameServer/EHostingAuditEventKind.cs` - Template audit event vocabulary.
- `sources/Api/Data/Migrations/20260621222446_AddCloudServerTemplates.cs` - Table, indexes, and FKs migration.
- `sources/Api/Data/Migrations/20260621222446_AddCloudServerTemplates.Designer.cs` - Generated target model.
- `sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs` - Updated EF model snapshot.

## Decisions Made

- Used one row per version snapshot instead of a parent/current pair to avoid circular current-version FK complexity.
- Used a filtered unique index on `TemplateId` where `IsCurrent` is true to enforce a single current version per logical template.
- Used generic `DefaultQueryPort` and `DefaultPlatformPort` field names instead of Steam-specific property names so client-facing DTOs can maintain a simple forbidden-term redaction policy.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Avoided Steam-specific port property names**
- **Found during:** Task 1 (Add template version entity and EF model wiring)
- **Issue:** The plan described a Steam port default, while the Phase 41 redaction requirements later forbid `Steam` in DTO/payload property names.
- **Fix:** Modeled the same port concept as generic server query/platform ports.
- **Files modified:** `sources/Api/Data/Hosting/CloudServerTemplate.cs`, `sources/Api/Data/Migrations/20260621222446_AddCloudServerTemplates.cs`, generated migration model files
- **Verification:** Forbidden-term scan on the entity returned no credential/session marker matches.
- **Committed in:** `ccf414f` and `7ec31d4`

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** The schema still captures all required port defaults and bounds while reducing redaction false positives for later transport tests.

## Issues Encountered

None. EF migration generation succeeded with the installed EF 10 CLI against the EF Core 9 project after a successful build.

## User Setup Required

None - no external service configuration required.

## Verification

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity normal` - passed with existing warnings.
- `dotnet ef migrations add AddCloudServerTemplates --project sources/Api/Api.csproj --startup-project sources/Api/Api.csproj` - passed.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal` - passed with existing warnings.
- Migration scope check confirmed only `CloudServerTemplates` is created and no unrelated tables are altered.

## Next Phase Readiness

The durable template schema and audit event vocabulary are ready for Plan 41-02 service, DTO, and controller implementation.

---
*Phase: 41-cloud-template-domain-and-audit-foundation*
*Completed: 2026-06-21*

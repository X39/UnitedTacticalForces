---
phase: 17-runtime-cost-snapshot-foundation
plan: 01
subsystem: database
tags: [ef-core, postgresql, hosting, cost-snapshots]

requires:
  - phase: 16-runtime-visibility-cleanup-and-verification
    provides: Cloud sessions, resource inventory, teardown timestamps, and worker visibility used by runtime cost records.
provides:
  - Durable `CloudCostSnapshot` aggregate records tied to cloud sessions and game servers.
  - Durable `CloudCostSnapshotContribution` interval records tied to resources, billing accounts, and assignments.
  - Runtime cost source/rate contract enums and hosting audit event kind.
  - EF Core DbSets, restrict-delete relationships, model snapshot updates, and `CloudRuntimeCostSnapshots` migration.
affects: [phase-17, phase-18, phase-19, cloud-costs, provider-reconciliation]

tech-stack:
  added: []
  patterns: [durable snapshot aggregate, interval contribution rows, restrict-delete hosting history, deterministic idempotency indexes]

key-files:
  created:
    - sources/Contract/GameServer/ECloudCostSnapshotSource.cs
    - sources/Contract/GameServer/ECloudCostRateUnit.cs
    - sources/Api/Data/Hosting/CloudCostSnapshot.cs
    - sources/Api/Data/Hosting/CloudCostSnapshotContribution.cs
    - sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.cs
    - sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.Designer.cs
  modified:
    - sources/Contract/GameServer/EHostingAuditEventKind.cs
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs

key-decisions:
  - "Phase 17 plan 01 remains API-internal persistence only; no DTO, endpoint, Kiota, WebApp, or live provider call was added."
  - "Snapshot refresh idempotency is enforced by unique `(SessionFk, Source)` and contribution interval keys."
  - "Historical cost attribution uses restrict-delete FKs so session, resource, owner, and actor history is not cascade-deleted."

patterns-established:
  - "Cost snapshots are aggregate rows; resource and owner splits live in contribution rows."
  - "Every contribution preserves billing account and billing assignment FKs at interval recording time."
  - "Freshness, confidence, currency, and bounded assumptions are stored on both aggregate and contribution rows."

requirements-completed: [COST-10, COST-12, RELY-14]

duration: 4h13m elapsed, including stalled executor recovery
completed: 2026-06-17
---

# Phase 17 Plan 01 Summary

**Durable EF Core runtime cost snapshot schema with billable resource contribution and owner attribution rows**

## Performance

- **Duration:** 4h13m elapsed, including stalled executor recovery
- **Started:** 2026-06-17T21:28:12+02:00
- **Completed:** 2026-06-18T01:40:54+02:00
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- Added runtime cost source and rate-unit contract enums plus `CloudRuntimeCostSnapshotRefreshed = 26`.
- Added `CloudCostSnapshot` and `CloudCostSnapshotContribution` hosting entities with attribution, interval, rate, freshness, confidence, assumptions, and timestamp fields.
- Wired EF Core DbSets, restrict-delete relationships, deterministic unique indexes, migration, designer, and model snapshot metadata.

## Task Commits

1. **Task 1: Add runtime cost snapshot contracts and hosting entities** - `841a125` (feat)
2. **Task 2: Wire EF Core DbSets, relationships, unique keys, and migration** - `968ab88` (feat)

## Files Created/Modified

- `sources/Contract/GameServer/ECloudCostSnapshotSource.cs` - Source enum for estimated, reconciled, and adjusted snapshot rows.
- `sources/Contract/GameServer/ECloudCostRateUnit.cs` - Rate unit enum for hourly and monthly cost rates.
- `sources/Contract/GameServer/EHostingAuditEventKind.cs` - Adds `CloudRuntimeCostSnapshotRefreshed`.
- `sources/Api/Data/Hosting/CloudCostSnapshot.cs` - Session-level runtime cost aggregate.
- `sources/Api/Data/Hosting/CloudCostSnapshotContribution.cs` - Resource and billing-owner interval contribution rows.
- `sources/Api/Data/ApiDbContext.cs` - DbSets, restrict-delete relationships, and idempotency indexes.
- `sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.cs` - PostgreSQL migration for the new tables and indexes.
- `sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.Designer.cs` - Migration target model.
- `sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs` - Current EF model snapshot.

## Decisions Made

- Kept this plan persistence-only so Phase 19 can add authorized API/UI visibility separately.
- Used unique `(SessionFk, Source)` for the current aggregate snapshot per source.
- Used unique `(SnapshotFk, ResourceInventoryRecordFk, BillingAssignmentFk, IntervalStart, IntervalEnd, Source)` for restart-safe contribution insertion.
- Stored no raw provider payloads, provider tokens, Steam credentials, worker credentials, bearer headers, or unbounded text fields.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created EF migration manually**
- **Found during:** Task 2 (migration generation)
- **Issue:** `dotnet build` and `dotnet ef` cannot reach `https://api.nuget.org/v3/index.json`; even the escalated build fails with `NU1301` and socket access denied for `api.nuget.org:443`.
- **Fix:** Added the migration, designer, and model snapshot updates by matching the repository's existing EF Core generated style.
- **Files modified:** `sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.cs`, `sources/Api/Data/Migrations/20260617193600_CloudRuntimeCostSnapshots.Designer.cs`, `sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs`.
- **Verification:** Schema acceptance `rg` checks pass; build remains environment-blocked before compilation.
- **Committed in:** `968ab88`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** No scope creep. The migration was required to complete the planned persistence work after CLI generation was blocked by the local NuGet/source environment.

## Issues Encountered

- Automated build verification is blocked in this environment. Command run: `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal`. It fails before compilation with `NU1301` because the SDK tries to load the NuGet service index and socket access to `api.nuget.org:443` is denied. The same failure occurs when run with escalation.

## Verification

- `rg "enum ECloudCostSnapshotSource|Estimated = 0|Reconciled = 1|Adjusted = 2" sources/Contract/GameServer` - passed.
- `rg "CloudRuntimeCostSnapshotRefreshed = 26" sources/Contract/GameServer/EHostingAuditEventKind.cs` - passed.
- `rg "class CloudCostSnapshot|class CloudCostSnapshotContribution" sources/Api/Data/Hosting` - passed.
- `rg "BillingAccountFk|BillingAssignmentFk|IntervalStart|IntervalEnd|MetadataSourceTimestamp|Freshness|ConfidenceScore|AssumptionsJson|Currency|RateAmount|RateUnit" sources/Api/Data/Hosting/CloudCostSnapshotContribution.cs` - passed.
- `rg -i "token|password|secret|authorization|api key|apikey|connection string" sources/Api/Data/Hosting -g "CloudCostSnapshot*.cs"` - passed with no matches.
- `rg "DbSet<CloudCostSnapshot>|DbSet<CloudCostSnapshotContribution>" sources/Api/Data/ApiDbContext.cs` - passed.
- `rg "CloudCostSnapshots|CloudCostSnapshotContributions" sources/Api/Data/Migrations` - passed.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 17-02 can build the snapshot service and calculator against durable snapshot tables, contribution rows, owner attribution FKs, and idempotency indexes. Re-run the API build once NuGet/source access is available.

## Self-Check: PASSED

Implementation matches plan scope and acceptance text checks. API build verification awaits NuGet source access.

---
*Phase: 17-runtime-cost-snapshot-foundation*
*Completed: 2026-06-17*

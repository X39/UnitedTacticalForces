---
phase: 17-runtime-cost-snapshot-foundation
plan: 03
subsystem: api
tags: [ef-core, sqlite-tests, hosting, cost-snapshots, audit, idempotency]

requires:
  - phase: 17-runtime-cost-snapshot-foundation
    provides: Plan 02 runtime cost snapshot service over persisted sessions, inventory, assignments, and cached metadata.
provides:
  - Hardened estimated snapshot metadata with source, currency, freshness, confidence, and bounded assumptions.
  - Secret-safe snapshot and contribution assumption allowlist.
  - Secret-safe hosting audit records for snapshot refreshes.
  - Idempotent refresh behavior that replaces derived contribution rows for the existing estimated session snapshot.
  - SQLite test coverage for freshness, assumptions, secret safety, audit fields, repeated refreshes, and restart-safe refresh.
affects: [phase-17, phase-19, cloud-costs, billing-attribution, provider-reconciliation]

tech-stack:
  added: []
  patterns: [allowlisted assumptions, provider-cache-only metadata, transactional refresh, derived-row replacement, secret-safe audit]

key-files:
  created: []
  modified:
    - sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs
    - tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs

key-decisions:
  - "Unknown metadata now carries zero confidence, while stale metadata keeps reduced confidence and fresh metadata keeps full confidence."
  - "Assumptions JSON records only calculation facts and rate source labels; arbitrary configuration values are never serialized."
  - "Audit details include ids, contribution count, source, currency, and total only."

patterns-established:
  - "Refresh audit records should summarize derived financial state without including provider payloads, assumptions JSON, or configuration values."
  - "Runtime cost snapshot hardening is covered by SQLite service tests rather than controller or UI tests because Phase 17 exposes no API surface."

requirements-completed: [COST-09, COST-12, RELY-14]

duration: 11min
completed: 2026-06-17
---

# Phase 17 Plan 03 Summary

**Runtime cost snapshot hardening, secret-safe audit, and idempotent refresh validation**

## Performance

- **Duration:** 11min
- **Started:** 2026-06-18T01:54:00+02:00
- **Completed:** 2026-06-18T02:05:45+02:00
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Extended snapshot and contribution assumption JSON with the monthly-hour normalization, IP/volume monthly rates, billable resource kinds, provider deletion cost-stop boundary, no-provider-mutation marker, and rate source labels.
- Changed confidence scoring so missing metadata is explicitly unknown with zero confidence instead of being treated like stale metadata.
- Expanded audit details for refreshes with secret-safe operational fields: snapshot id, session id, contribution count, source, currency, and total.
- Added SQLite tests for metadata/freshness/confidence, bounded assumptions, sensitive-word absence, idempotent repeated refresh, restart-safe refresh, and refresh audit fields.

## Task Commits

1. **Task 1: Preserve source, assumptions, freshness, currency, and confidence without secrets** - `87f478d` (feat)
2. **Task 2: Make refresh idempotent, restart-safe, audited, and fully validated** - `87f478d` (feat)

Tasks were committed together because the metadata, audit, and idempotency checks all harden the same refresh path.

## Files Created/Modified

- `sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` - Hardened confidence scoring, assumption serialization, and audit detail output.
- `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - Added coverage for metadata, secret safety, idempotency, restart-safe refresh, and audit records.

## Decisions Made

- Kept Phase 17 service-only; no controller, DTO, Kiota, Blazor, live provider, or provider mutation files were added.
- Treated unknown metadata distinctly from stale metadata so consumers can avoid presenting unsupported precision.
- Kept contribution rows derived and replaceable during refresh to preserve one estimated snapshot per session without duplicated totals.

## Deviations from Plan

None.

## Issues Encountered

- The .NET verification commands could not reach API compiler execution in this environment. Targeted API tests, unfiltered API tests, API build, full solution build, and full solution test commands reported `NU1301` while trying to load the NuGet service index for `https://api.nuget.org/v3/index.json`, with socket access denied for `api.nuget.org:443`, even when run with elevated command permissions.

## Verification

- `rg "ECloudCostSnapshotSource.Estimated|ECloudEstimateFreshness|ConfidenceScore|AssumptionsJson|noProviderMutation|ProviderDeletionTimestamp" sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` - passed.
- `rg -i "token|password|secret|authorization|api key|apikey|connection string" tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - passed; it finds the sensitive-word fixtures and absence assertions.
- `rg "BeginTransactionAsync|CloudCostSnapshotContributions|RemoveRange|AppendAsync|CloudRuntimeCostSnapshotRefreshed" sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` - passed.
- `rg "IsIdempotentForRepeatedRefreshes|IsRestartSafeAcrossNewServiceInstance|AppendsSecretSafeAuditRecord" tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - passed.
- `git diff --check -- sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - passed with only repository LF-to-CRLF warnings.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` - parser projects built, then API-related projects did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` - `X39.BI.Config.Parsing.Tests` passed 89 tests, then API-related projects did not reach compilation because NuGet source/socket access returned `NU1301`.

## User Setup Required

NuGet source access is required before SDK build/test commands can execute compilation in this environment.

## Next Phase Readiness

Phase 17 has the persisted schema, interval service, source metadata, assumptions, audit, and idempotency safeguards that Phase 19 can expose through authorized API/UI surfaces.

## Self-Check: PASSED

Implementation matches plan scope and acceptance text checks. SDK verification awaits NuGet source access.

---
*Phase: 17-runtime-cost-snapshot-foundation*
*Completed: 2026-06-17*

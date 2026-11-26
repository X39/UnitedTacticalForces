---
phase: 18-provider-reconciliation-runs-and-findings
plan: 02
subsystem: api
tags: [ef-core, migrations, hosting, reconciliation, audit]

requires:
  - phase: 18-provider-reconciliation-runs-and-findings
    provides: Plan 01 read-only Hetzner live resource boundary.
provides:
  - Reconciliation run status, finding kind, and finding severity contract enums.
  - Hosting audit event kinds for reconciliation started, completed, and failed events.
  - Durable reconciliation run and finding entities with bounded fields and nullable attribution.
  - DbContext sets, restrict-delete relationships, explicit finding index names, migration, and model snapshot baseline.
affects: [phase-18, cloud-inventory, provider-reconciliation, hosting-audit, ef-core]

tech-stack:
  added: []
  patterns: [restrict-delete history records, bounded finding details, nullable orphan attribution, manual migration]

key-files:
  created:
    - sources/Contract/GameServer/ECloudReconciliationRunStatus.cs
    - sources/Contract/GameServer/ECloudReconciliationFindingKind.cs
    - sources/Contract/GameServer/ECloudReconciliationFindingSeverity.cs
    - sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs
    - sources/Api/Data/Hosting/CloudProviderReconciliationFinding.cs
    - sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs
    - sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.Designer.cs
  modified:
    - sources/Contract/GameServer/EHostingAuditEventKind.cs
    - sources/Api/Data/ApiDbContext.cs
    - sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs

key-decisions:
  - "All reconciliation history relationships use restrict delete, including run-owned findings."
  - "Unknown provider resources can be recorded without local session, game server, billing account, or billing assignment attribution."
  - "Finding indexes use explicit shortened database names so migration and runtime model names stay aligned."
  - "The migration was written manually because EF tooling cannot run while NuGet source access is blocked."

patterns-established:
  - "Reconciliation findings should preserve known inventory attribution but allow null owner references for orphan provider resources."
  - "Provider failure fields are bounded and sanitized at storage boundaries before service/API exposure."

requirements-completed: [HETZ-10, HETZ-11, RELY-14]

duration: not measured
completed: 2026-06-18
---

# Phase 18 Plan 02 Summary

**Durable reconciliation run and finding schema**

## Accomplishments

- Added contract enums for reconciliation run status, finding kind, and finding severity.
- Extended hosting audit event kinds with reconciliation started/completed/failed values 27-29 without renumbering existing values.
- Added `CloudProviderReconciliationRun` with provider id, actor, status, timestamps, live provider counts, known inventory count, finding counts, orphan count, category counts, and bounded sanitized error fields.
- Added `CloudProviderReconciliationFinding` with run, provider, kind, severity, resource kind/id/name, optional inventory/session/game-server/billing references, expected/actual state fields, summary, bounded details JSON, and timestamp.
- Wired DbSets and restrict-delete relationships in `ApiDbContext`.
- Added manual migration and model snapshot updates for `CloudProviderReconciliationRuns` and `CloudProviderReconciliationFindings`.

## Files Created/Modified

- `sources/Contract/GameServer/ECloudReconciliationRunStatus.cs`
- `sources/Contract/GameServer/ECloudReconciliationFindingKind.cs`
- `sources/Contract/GameServer/ECloudReconciliationFindingSeverity.cs`
- `sources/Contract/GameServer/EHostingAuditEventKind.cs`
- `sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs`
- `sources/Api/Data/Hosting/CloudProviderReconciliationFinding.cs`
- `sources/Api/Data/ApiDbContext.cs`
- `sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs`
- `sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.Designer.cs`
- `sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs`

## Deviations from Plan

- `dotnet ef migrations add CloudProviderReconciliation` could not be used because SDK commands fail before tooling execution on NuGet source access. The migration and snapshot were written manually and checked against existing migration style.

## Issues Encountered

- SDK verification could not reach compilation in this environment. `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` failed with `NU1301` while trying to load the NuGet service index for `https://api.nuget.org/v3/index.json`, with socket access denied for `api.nuget.org:443`, including the final escalated retry.

## Verification

- `rg "enum ECloudReconciliationRunStatus|Running = 0|Completed = 1|UnknownProviderResource|MissingProviderResource|StateMismatch|CleanupNeededBillableResource|CloudReconciliationRun" sources/Contract/GameServer` - passed.
- `rg "class CloudProviderReconciliationRun|class CloudProviderReconciliationFinding|LiveServerCount|LiveIpAddressCount|LiveVolumeCount|LiveFirewallCount|KnownInventoryCount|FindingCount|OrphanFindingCount|InventoryRecordFk|SessionFk|GameServerFk|BillingAccountFk|BillingAssignmentFk|Expected|Actual|DetailsJson" sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs sources/Api/Data/Hosting/CloudProviderReconciliationFinding.cs` - passed.
- `rg -i "token|password|secret|authorization|api key|apikey|bearer|user data|userdata" sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs sources/Api/Data/Hosting/CloudProviderReconciliationFinding.cs sources/Contract/GameServer/ECloudReconciliationRunStatus.cs sources/Contract/GameServer/ECloudReconciliationFindingKind.cs sources/Contract/GameServer/ECloudReconciliationFindingSeverity.cs` - passed with no matches.
- `rg "CloudProviderReconciliationRuns|CloudProviderReconciliationFindings|IX_CloudReconFindings|character varying" sources/Api/Data/ApiDbContext.cs sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs` - passed.
- `rg "20260618003000_CloudProviderReconciliation|partial class CloudProviderReconciliation|CloudProviderReconciliationRuns|CloudProviderReconciliationFindings" sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.Designer.cs sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs` - passed.
- `rg "CloudReconciliationRunStarted = 27|CloudReconciliationRunCompleted = 28|CloudRuntimeCostSnapshotRefreshed = 26" sources/Contract/GameServer/EHostingAuditEventKind.cs` - passed.
- `git diff --check -- sources/Contract/GameServer/ECloudReconciliationRunStatus.cs sources/Contract/GameServer/ECloudReconciliationFindingKind.cs sources/Contract/GameServer/ECloudReconciliationFindingSeverity.cs sources/Contract/GameServer/EHostingAuditEventKind.cs sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs sources/Api/Data/Hosting/CloudProviderReconciliationFinding.cs sources/Api/Data/ApiDbContext.cs sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.Designer.cs sources/Api/Data/Migrations/ApiDbContextModelSnapshot.cs` - passed with only repository LF-to-CRLF warnings.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.

## User Setup Required

NuGet source access is required before SDK build/test commands can execute compilation in this environment.

## Next Plan Readiness

Plan 18-03 can implement the reconciliation service and admin trigger/read endpoints on top of the durable run/finding schema.

## Self-Check: PASSED

Implementation matches plan scope and acceptance text checks. SDK verification awaits NuGet source access.

---
*Phase: 18-provider-reconciliation-runs-and-findings*
*Completed: 2026-06-18*

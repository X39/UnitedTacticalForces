---
phase: 18-provider-reconciliation-runs-and-findings
plan: 03
subsystem: api
tags: [reconciliation, cloud-inventory, hetzner, audit, admin-api, tests]

requires:
  - phase: 18-provider-reconciliation-runs-and-findings
    provides: Plan 02 durable reconciliation run and finding schema.
provides:
  - Scoped reconciliation service with durable run history and finding reads.
  - Read-only comparison of Hetzner live resources against provider-backed inventory.
  - Finding generation for unknown provider resources, missing provider resources, state mismatches, and cleanup-needed billable resources.
  - Admin-only reconciliation trigger and read endpoints under cloud inventory.
  - Run and finding DTO transforms with secret-safe fields.
  - Hosting audit records for started, completed, and unsuccessful reconciliation runs.
affects: [phase-18, cloud-inventory, provider-reconciliation, hosting-audit, admin-api]

tech-stack:
  added: []
  patterns: [read-only reconciliation service, run-scoped finding de-duplication, nullable orphan attribution, admin endpoint projection]

key-files:
  created:
    - sources/Api/Services/CloudReconciliation/ICloudProviderReconciliationService.cs
    - sources/Api/Services/CloudReconciliation/CloudProviderReconciliationService.cs
    - sources/Api/Services/CloudReconciliation/CloudProviderReconciliationRunResult.cs
    - sources/Api/DTO/CloudProviderReconciliationRunDto.cs
    - sources/Api/DTO/CloudProviderReconciliationFindingDto.cs
    - tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationServiceTests.cs
    - tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs
    - .planning/phases/18-provider-reconciliation-runs-and-findings/18-REVIEW.md
  modified:
    - sources/Api/Transform.cs
    - sources/Api/Controllers/CloudInventoryController.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryServiceTests.cs
    - tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs
    - tests/Api.Tests/CloudProviders/HetznerMetadataSyncTests.cs
    - tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs
    - tests/Api.Tests/CloudTeardown/CloudTeardownServiceTests.cs
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md
    - .planning/STATE.md

key-decisions:
  - "Reconciliation returns durable run status instead of converting provider/configuration read problems into transient controller errors."
  - "Unknown provider resources remain unattributed unless local inventory proves an owner."
  - "Equivalent findings are de-duplicated per run by finding kind, resource kind, and provider resource id."
  - "Controller endpoints live on the existing cloud inventory admin surface; WebApp/generated client updates remain Phase 19 scope."

patterns-established:
  - "Reconciliation services should resolve provider credentials server-side, use read-only provider methods, and persist only sanitized summaries/details."
  - "Finding DTOs should project bounded stored fields, not raw provider payloads."
  - "Tests for provider-client fakes must implement new interface members to keep unrelated provider workflows compiling."

requirements-completed: [HETZ-08, HETZ-09, HETZ-10, HETZ-11, RELY-14]

duration: not measured
completed: 2026-06-18
---

# Phase 18 Plan 03 Summary

**Reconciliation service, admin trigger, audit, and tests**

## Accomplishments

- Added `ICloudProviderReconciliationService` and `CloudProviderReconciliationService` with durable run creation, terminal status updates, run listing, single-run lookup, and finding lookup.
- Implemented read-only Hetzner reconciliation over servers, floating IPs, volumes, and firewalls using the Phase 18 provider read boundary.
- Added finding generation for unknown provider resources, missing provider resources, state mismatches, and cleanup-needed billable resources.
- Preserved inventory, session, game-server, billing account, and billing assignment attribution for known resources while keeping unknown provider resources ownerless.
- Added run and finding DTOs plus `Transform` mappings.
- Added admin-only endpoints:
  - `POST /cloud-inventory/reconciliation/runs`
  - `GET /cloud-inventory/reconciliation/runs`
  - `GET /cloud-inventory/reconciliation/runs/{runId}`
  - `GET /cloud-inventory/reconciliation/runs/{runId}/findings`
- Added audit records for reconciliation started/completed/unsuccessful outcomes with sanitized details.
- Added service tests for counts, all finding kinds, attribution, duplicate local row de-duplication, secret redaction, and no provider mutation.
- Added controller tests for trigger/read DTO responses and not-found behavior.
- Updated existing fake Hetzner clients in provisioning, teardown, and metadata tests to implement the new read-only list interface members.
- Completed a standard inline code review and recorded it in `18-REVIEW.md`.

## Files Created/Modified

- `sources/Api/Services/CloudReconciliation/ICloudProviderReconciliationService.cs`
- `sources/Api/Services/CloudReconciliation/CloudProviderReconciliationService.cs`
- `sources/Api/Services/CloudReconciliation/CloudProviderReconciliationRunResult.cs`
- `sources/Api/DTO/CloudProviderReconciliationRunDto.cs`
- `sources/Api/DTO/CloudProviderReconciliationFindingDto.cs`
- `sources/Api/Transform.cs`
- `sources/Api/Controllers/CloudInventoryController.cs`
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationServiceTests.cs`
- `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationApiTests.cs`
- `tests/Api.Tests/CloudInventory/CloudInventoryServiceTests.cs`
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`
- `tests/Api.Tests/CloudProviders/HetznerMetadataSyncTests.cs`
- `tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs`
- `tests/Api.Tests/CloudTeardown/CloudTeardownServiceTests.cs`
- `.planning/phases/18-provider-reconciliation-runs-and-findings/18-REVIEW.md`
- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/STATE.md`

## Deviations from Plan

- The plan listed `CloudProviderReconciliationApiTests.cs` as the controller test artifact; it was added alongside the service tests.
- SDK test execution could not be completed because the environment still blocks NuGet source/socket access before compilation.

## Issues Encountered

- SDK verification could not reach compilation in this environment. `dotnet build` and targeted `dotnet test` both returned `NU1301` while trying to load `https://api.nuget.org/v3/index.json`, with socket access denied for `api.nuget.org:443`. The same result occurred on escalated retries.

## Verification

- `git diff --check` - passed with only repository LF-to-CRLF warnings.
- `rg "interface ICloudProviderReconciliationService|class CloudProviderReconciliationService|CloudProviderReconciliationRunDto|CloudProviderReconciliationFindingDto|reconciliation/runs|CloudReconciliationRunStarted|CloudReconciliationRunCompleted" sources/Api tests/Api.Tests/CloudReconciliation -g "*.cs"` - passed.
- `rg "UnknownProviderResource|MissingProviderResource|StateMismatch|CleanupNeededBillableResource|InventoryRecordFk|BillingAccountFk|BillingAssignmentFk|OrphanFindingCount" sources/Api/Services/CloudReconciliation tests/Api.Tests/CloudReconciliation -g "*.cs"` - passed.
- `rg "DeleteServerAsync|DeleteFloatingIpAsync|DeleteVolumeAsync|DeleteFirewallAsync|DetachVolumeAsync|CreateServerAsync|CreateFloatingIpAsync|CreateVolumeAsync|CreateFirewallAsync" sources/Api/Services/CloudReconciliation tests/Api.Tests/CloudReconciliation -g "*.cs"` - passed with matches only in fake test guard declarations.
- `dotnet build tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal -p:NuGetAudit=false` - did not reach compilation because NuGet source/socket access returned `NU1301`; escalated retry returned the same result.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~CloudProviderReconciliation --no-restore --verbosity minimal -p:NuGetAudit=false` - did not reach compilation because NuGet source/socket access returned `NU1301`; escalated retry returned the same result.

## User Setup Required

NuGet source access is required before SDK build/test commands can execute compilation in this environment.

## Next Plan Readiness

Phase 19 can plan the WebApp/API visibility layer for current cost, accumulated cost, reconciliation status, finding counts, orphan indicators, and cleanup risk.

## Self-Check: PASSED

Implementation matches plan scope and acceptance text checks. SDK verification awaits NuGet source access.

---
*Phase: 18-provider-reconciliation-runs-and-findings*
*Completed: 2026-06-18*

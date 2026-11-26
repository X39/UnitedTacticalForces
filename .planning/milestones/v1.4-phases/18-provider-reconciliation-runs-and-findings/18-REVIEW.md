---
phase: 18-provider-reconciliation-runs-and-findings
phase_number: 18
status: clean
depth: standard
files_reviewed: 13
findings:
  critical: 0
  warning: 0
  info: 0
  total: 0
reviewed_at: 2026-06-18
reviewer: inline-codex
---

# Phase 18 Code Review

## Scope

Reviewed Phase 18 plan 03 source and test changes:

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

## Findings

No open issues found.

## Review Notes

- The reconciliation service only calls the Hetzner live-resource read methods and does not call create, delete, detach, or other mutation methods.
- Run creation is durable before provider reads; completed and unsuccessful terminal states persist sanitized counts or provider-error details.
- Finding generation preserves inventory, session, game server, billing account, and billing assignment references for known resources while leaving unknown provider resources unattributed.
- Equivalent findings are de-duplicated by finding kind, resource kind, and provider resource id. The service tests include duplicate local inventory rows to cover this.
- DTOs and controller transforms expose bounded reconciliation fields and do not pass through raw provider payloads or credential material.
- Admin-only route metadata is covered through the existing cloud inventory controller authorization reflection test.

## Verification Context

Static checks passed:

- `git diff --check`
- Reconciliation service/API grep for service registration, DTOs, routes, and audit event use.
- No-mutation grep on `sources/Api/Services/CloudReconciliation` with mutation matches only in fake test guard declarations.

SDK build/test commands did not reach compilation in this environment because NuGet source/socket access returned `NU1301` for `https://api.nuget.org/v3/index.json`, including escalated retries.

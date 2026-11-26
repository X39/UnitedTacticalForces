---
phase: 9
status: passed
verified: 2026-06-16
requirements:
  - HETZ-05
  - PROV-01
  - PROV-02
  - PROV-03
  - PROV-04
---

# Phase 9 Verification: Hetzner Provisioning Execution

## Result

Passed.

## Requirement Coverage

- `HETZ-05`: `CloudInventoryController.ProvisionDryRunPlanAsync` exposes an administrator-only provision action for valid dry-run plans, and `CloudProvisioningService` creates Hetzner firewalls, servers, floating IPs, and optional volumes from the owner-linked inventory plan.
- `PROV-01`: `IHetznerCloudClient` exposes explicit mutating create/delete/detach methods; provider metadata and dry-run paths remain separate from the provisioning service.
- `PROV-02`: `CloudProvisioningService` persists provider resource identifiers, action/result details, requested shape snapshots, public endpoint fields, billing snapshots, and `FutureProvisioned` inventory state after successful provider creates.
- `PROV-03`: `CloudProvisioningService.ValidateAsync` rejects consumed, stale, owner-mismatched, unsupported, ownerless, invalid, or already-provider-backed plans before issuing provider calls.
- `PROV-04`: Successful provisioning records public endpoint host/port on the cloud session and returns a credential-free provisioning result for API consumers.

## Evidence

- Plans and summaries: `09-01`, `09-02`, and `09-03` completed the provider create boundary, durable provisioning service, and admin provisioning endpoint.
- Source: `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` validates the dry-run plan before provider mutation, creates provider resources, persists provider IDs and endpoint data, and transitions resources to durable states.
- Source: `sources/Api/Controllers/CloudInventoryController.cs` maps `POST cloud-inventory/dry-run-plans/{planId}/provision` behind `Claims.Administrative.Server`.
- Tests: `tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` covers provider POST endpoints, bearer-token handling, returned provider/action IDs, and explicit provider method boundaries.
- Tests: `tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs` covers successful provisioning, provider-call order, endpoint projection, owner snapshots, preflight rejection without provider calls, partial failure cleanup-needed state, bootstrap integration, and redaction.

## Automated Checks

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "CloudProvisioning|CloudInventory|HetznerCloudClient" --no-restore --verbosity minimal` - passed, 27 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --verbosity minimal` - passed, 78 tests.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` - succeeded with known warnings.

The first parallel attempt hit Windows file locks in build outputs. After `dotnet build-server shutdown`, the same checks were rerun serially and passed.

## Residual Risk

- Live Hetzner provisioning against production provider resources was not executed in this automated audit run. The code is covered by fake provider/client tests and should receive controlled operator UAT before production use.

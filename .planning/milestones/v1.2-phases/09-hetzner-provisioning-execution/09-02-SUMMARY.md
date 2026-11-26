---
phase: 9
plan: 09-02
completed: 2026-06-16
requirements-completed:
  - HETZ-05
  - PROV-02
  - PROV-03
  - PROV-04
---

# 09-02 Summary: Durable Provisioning Service

## Completed

- Added `ICloudProvisioningService` and `CloudProvisioningService`.
- Added `CloudProvisioningResultDto` and public endpoint fields on `CloudGameServerSession`.
- Added EF migration `AddCloudProvisioning` for `PublicEndpointHost` and `PublicEndpointPort`.
- Implemented preflight validation for plan status, provider identifier, active owner assignment, provider configuration, metadata snapshot freshness, resource state, and missing provider ids before any Hetzner create call.
- Persisted provider ids, operation details, future-provisioned states, endpoint host/port, and billing snapshots after successful provider creates.
- Added partial provisioning handling that marks the plan incomplete and created resources cleanup-needed while keeping ownership snapshots.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~CloudProvisioningServiceTests --no-restore --verbosity minimal`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal`

## Notes

- The service saves after each successful provider create so Phase 10 cleanup can inspect partial state after an interrupted attempt.
- Preflight rejections for consumed plans, stale metadata, and owner mismatch are covered by tests that assert zero provider create calls.

## Self-check

Passed.

---
phase: 10
plan: 10-01
completed: 2026-06-16
requirements-completed:
  - CLN-01
  - CLN-02
  - AUDT-04
---

# 10-01 Summary: Hetzner Teardown Provider Boundary

## Completed

- Added explicit Hetzner teardown methods for volume detach and provider resource deletion.
- Implemented `POST /v1/volumes/{id}/actions/detach` and `DELETE` calls for servers, floating IPs, volumes, and firewalls.
- Reused the existing bearer-token request handling and sanitized failure mapping for no-body provider mutations.
- Updated provider boundary tests to allow only read/create/delete methods plus explicit `DetachVolumeAsync`.
- Added fake HTTP tests for teardown methods and sanitized teardown failures.
- Updated existing Hetzner client test fakes with throwing teardown stubs so metadata and provisioning tests remain focused.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore --verbosity minimal`

## Notes

- The provider boundary still rejects generic update, power, attach, and arbitrary detach operations.
- Teardown methods return success/failure only; durable deletion and audit semantics are implemented in later Phase 10 plans.

## Self-check

Passed.

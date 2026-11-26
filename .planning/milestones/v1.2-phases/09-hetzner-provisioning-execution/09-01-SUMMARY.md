---
phase: 9
plan: 09-01
completed: 2026-06-16
requirements-completed:
  - HETZ-05
  - PROV-01
  - PROV-02
---

# 09-01 Summary: Hetzner Provider Create Boundary

## Completed

- Added explicit Hetzner create request/result models for firewalls, servers, floating IPs, and volumes.
- Extended `IHetznerCloudClient` with `CreateFirewallAsync`, `CreateServerAsync`, `CreateFloatingIpAsync`, and `CreateVolumeAsync`.
- Implemented POST calls in `HetznerCloudClient` using existing bearer-token handling and sanitized failure mapping.
- Updated provider guard tests so the interface allows only `Verify*`, `Get*`, and explicit `Create*` methods while continuing to reject update/delete/power/attach/detach operations.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore --verbosity minimal`
- Covered endpoint/method assertions through the full API test run.

## Notes

- Dry-run planning and inventory services remain free of provider-client dependencies.
- Create methods return provider ids and action ids only; they do not expose token material.

## Self-check

Passed.

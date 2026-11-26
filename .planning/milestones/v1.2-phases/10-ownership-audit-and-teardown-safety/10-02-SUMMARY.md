---
phase: 10
plan: 10-02
completed: 2026-06-16
requirements-completed:
  - BILL-05
  - BILL-06
  - AUDT-04
  - CLN-01
  - CLN-02
---

# 10-02 Summary: Durable Cloud Teardown Service

## Completed

- Added `Deleted` cloud resource state plus deletion timestamps on cloud sessions and resource inventory records.
- Mapped deletion timestamps through cloud session and resource DTOs, transforms, and EF migration output.
- Adjusted partial provisioning failure handling so only provider-created resources become cleanup-needed.
- Added `ICloudTeardownService` and `CloudTeardownService` for server-side Hetzner token resolution, safe provider delete ordering, durable deletion state, and cleanup-needed failure state.
- Recorded teardown start, success, failure, and resource-deleted audit events without exposing provider credentials.
- Added focused tests for partial provisioning cleanup ownership, successful teardown, failed teardown, owner snapshot preservation, and sanitized audit details.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "CloudProvisioning|CloudTeardown" --no-restore --verbosity minimal`

## Notes

- Deletion is the cost-stop boundary for provider-created resources.
- Failed teardown leaves remaining billable provider resources in `CleanupNeeded` with preserved billing account and assignment snapshots.

## Self-check

Passed.

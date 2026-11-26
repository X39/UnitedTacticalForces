# Phase 9: Hetzner Provisioning Execution - Research

## RESEARCH COMPLETE

## Current State

- `IHetznerCloudClient` is intentionally read-only in v1.1 and exposes only credential verification plus metadata reads.
- `CloudInventoryService` persists dry-run sessions, resources, plans, billing snapshots, state transitions, and sanitized audit records without depending on provider clients.
- `CloudResourceInventoryRecord` already has the durable fields Phase 9 needs for most provider execution data: `ProviderResourceId`, requested shape fields, state, `DetailsJson`, `StateReason`, timestamps, and billing owner snapshots.
- `CloudGameServerSession` has provider/session state fields but lacks explicit public endpoint fields for post-provision visibility.
- v1.1 tests assert that dry-run and inventory services do not introduce provider mutations. Phase 9 should replace those assertions with a narrower rule: dry-run services stay non-mutating, while a dedicated provisioning service and explicit client methods own mutations.

## Implementation Strategy

1. Extend the Hetzner provider boundary with explicit create methods only. Keep read-only metadata methods separate by naming and tests.
2. Add small provider request/result models that expose only provider identifiers, operation/action identifiers, and public network details. Do not expose tokens or request headers.
3. Add `CloudProvisioningService` as the only service that depends on `IHetznerCloudClient` for mutating calls.
4. Validate the entire dry-run plan before the first provider call:
   - plan exists and status is `Valid`;
   - provider is Hetzner;
   - session exists and is not already provisioned or cleanup-needed;
   - required server resource exists;
   - every resource is ready for provisioning and has no provider id;
   - billing account and assignment snapshots exist and the assignment is still active for the server;
   - provider configuration is enabled, verified, and resolves to a secret token;
   - metadata source timestamp still matches the latest successful cache.
5. Persist each provider-created resource id and operation details as soon as the call succeeds. If a later call fails, the already-created resources must remain owner-attributed and marked cleanup-needed.
6. Store public endpoint details on the cloud session and surface them through `CloudGameServerSessionDto`.

## Verification Targets

- Unit tests prove invalid/stale/ownerless/already-consumed plans do not call mutating provider methods.
- Unit tests prove successful provisioning stores provider ids and public endpoint details.
- Existing cloud dry-run tests continue to prove dry-run services do not depend on provider clients.
- Build/test commands: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter Cloud --no-restore --verbosity minimal`.

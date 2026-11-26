# Phase 11 Research: Cloud Worker Bootstrap Credentials

## RESEARCH COMPLETE

## Scope

Phase 11 prepares provisioned Hetzner VMs to connect back as cloud workers. It should create durable cloud worker identity and bootstrap credential state, generate short-lived scoped credential material, and pass that credential only through server-side provisioning bootstrap data. Worker registration, heartbeat capability reporting, and frontend visibility remain Phase 12.

## Existing Implementation Surface

- `LifecycleWorker` and `LifecycleWorkerRegistryService` already persist durable worker registrations and heartbeats, but local workers currently register by direct worker id without any scoped bootstrap credential.
- `CloudProvisioningService` owns real provider mutation calls and already receives the internal `HetznerCloudServerCreateRequest.UserData` field, but currently passes `null`.
- `CloudGameServerSession` and `CloudResourceInventoryRecord` carry provider state, owner snapshots, provider identifiers, public endpoint fields, and deletion timestamps.
- `HostingAuditService` sanitizes audit summaries and details before persistence. `LifecycleFailureReasonSanitizer` redacts messages containing password, secret, token, authorization, api key, or connection string vocabulary.
- Cloud provisioning tests already assert provider token material is not persisted in audit records or DTO-like results.

## Implementation Implications

- Add a server-side persisted bootstrap credential/identity entity scoped to exactly one `CloudGameServerSession`. Store worker id, session/game-server/billing snapshots, issue/expiry/consumption timestamps, and only a hash of the bootstrap credential.
- Generate high-entropy bootstrap credentials with `RandomNumberGenerator`, return raw credential material only through an internal service result used to render provider `user_data`, and never expose the raw credential or hash through public DTOs.
- Provide a consume/validate service method now so Phase 12 can add a registration endpoint without designing credential semantics later. Consuming should reject unknown, expired, disabled, or already consumed credentials.
- Generate Hetzner cloud-init/user-data from the internal bootstrap bundle. The payload should include only API endpoint, worker id, cloud session id, game server id, provider id, and the bootstrap credential. It must not include Hetzner provider tokens, Steam credentials, OAuth secrets, or database connection data.
- Keep bootstrap details out of `CloudResourceInventoryRecord.DetailsJson`, `CloudGameServerSessionDto`, provider status DTOs, audit records, lifecycle job errors, and logs.

## Validation Architecture

- Unit tests should use in-memory SQLite with `EnsureCreatedAsync` to prove new EF entities persist and enforce the expected service behavior.
- Service tests should assert:
  - credential creation creates one identity scoped to one session;
  - raw credential is returned only from the internal service result;
  - persisted state stores a hash, not the raw credential;
  - audit records contain no raw credential and no credential vocabulary;
  - consume succeeds once before expiry and fails for wrong, expired, or already consumed credentials.
- Provisioning tests should assert `HetznerCloudServerCreateRequest.UserData` contains API endpoint, worker id, and bootstrap credential, while persisted session/resource details and audits do not contain the credential.
- Run `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "CloudWorkerBootstrap|CloudProvisioning" --no-restore --verbosity minimal`.

## Sources

- Existing provider user-data boundary: `sources/Api/Services/CloudProviders/Hetzner/Models/HetznerCloudProvisioningModels.cs`
- Existing provisioning orchestration: `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs`
- Existing worker persistence: `sources/Api/Data/Hosting/LifecycleWorker.cs`
- Existing audit sanitization: `sources/Api/Services/GameServerLifecycle/HostingAuditService.cs`

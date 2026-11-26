# Phase 12 Research: Cloud Worker Registration And Visibility

## RESEARCH COMPLETE

## Scope

Phase 12 completes the v1.2 control-plane loop after Phase 11 provisioning user-data. It should let a VM use its bootstrap material to register as a cloud lifecycle worker, continue heartbeating with durable capability metadata, and surface cloud/bootstrap/cleanup state safely to admins and server owners.

## Existing Implementation Surface

- `LifecycleWorker` and `LifecycleWorkerRegistryService` are the existing durable worker model. Local worker behavior should remain the default path.
- `LocalGameServerLifecycleWorker` sends local heartbeats every 10 seconds and depends on `HeartbeatAsync(workerId, displayName, cancellationToken)`.
- `LifecycleWorkersController` currently has an authenticated admin list endpoint at `GET lifecycle-workers/all`.
- Phase 11 `CloudWorkerBootstrapService.ConsumeBootstrapAsync` validates and consumes a short-lived credential, returning the scoped `CloudWorkerBootstrapCredential` row without raw material.
- `CloudGameServerSession` has durable provider/session/resource/billing state, while `CloudResourceInventoryRecord` stores provider resource ids and cleanup/orphan states.
- `CloudInventoryController` has admin-only cloud reads and mutating provisioning/teardown endpoints.
- `GameServersController` already uses resource-scoped authorization for server-owner reads and actions.
- `Cloud.razor` loads sessions, dry-run plans, resources, estimates, and metadata, but currently renders plans/resources/estimates/metadata only.
- `CloudAdminRepository` intentionally uses raw `HttpClient` until OpenAPI/Kiota regeneration is available.

## Implementation Implications

- Extend the existing `LifecycleWorker` entity with a worker kind and nullable cloud metadata instead of adding a second registration table.
- Keep a hashed worker credential on the worker row for post-registration heartbeat authentication. Raw worker credentials should be generated server-side and returned once in a response header, not a DTO body.
- Use request headers such as `Authorization: Bearer <credential>` and `X-UTF-Worker-Id` or a route worker id for worker-facing calls. Avoid request/response DTO properties named `Credential`, `Token`, `Secret`, `Password`, or `Authorization`.
- Registration should:
  - require a known worker id and bootstrap bearer credential;
  - consume the Phase 11 bootstrap row exactly once;
  - create or update the matching `LifecycleWorker` as a cloud worker;
  - link it to cloud session, game server, billing account, and billing assignment;
  - persist static capability fields and heartbeat timestamp;
  - append a sanitized `CloudWorkerRegistered` audit record.
- Heartbeat should:
  - authenticate with the persisted worker credential hash;
  - reject disabled, local, unlinked, deleted-session, or credential-mismatched workers;
  - update heartbeat and capability metadata;
  - avoid per-heartbeat audit noise unless capabilities change materially.
- Visibility should derive retry availability from persisted state. Provisioning and teardown can map to existing endpoints. Bootstrap retry should not be exposed for already-created VMs until a secure delivery path exists.

## Validation Architecture

- Add SQLite service tests for:
  - local worker heartbeat defaults to local kind and remains compatible;
  - cloud registration consumes bootstrap and links session/game-server/billing data;
  - raw bootstrap and worker credentials are absent from DTOs/audits/log-like strings;
  - heartbeat updates persisted timestamps and capabilities;
  - API restart is represented by creating a new context/service over the same database and reading the persisted worker/session state.
- Add API/controller tests for:
  - missing/wrong bootstrap or worker auth returns unauthorized without echoing supplied material;
  - registration response body is safe and the one raw worker credential is header-only;
  - admin can list cloud workers/status;
  - resource-scoped server owner can read safe cloud status for their server but cannot use admin-only cloud controls.
- Add WebApp model/repository tests or focused build coverage for new raw `HttpClient` cloud methods.
- Run targeted `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "CloudWorker|LifecycleWorker|CloudInventory" --no-restore --verbosity minimal` and finish with `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`.

## Sources

- Worker persistence: `sources/Api/Data/Hosting/LifecycleWorker.cs`
- Worker registry: `sources/Api/Services/GameServerLifecycle/LifecycleWorkerRegistryService.cs`
- Worker listing controller: `sources/Api/Controllers/LifecycleWorkersController.cs`
- Bootstrap consume service: `sources/Api/Services/CloudWorkerBootstrap/CloudWorkerBootstrapService.cs`
- Cloud session and inventory state: `sources/Api/Data/Hosting/CloudGameServerSession.cs`, `sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs`
- Cloud admin surface: `sources/WebApp/Pages/Servers/Cloud.razor`, `sources/WebApp/Services/CloudAdminRepository`
- Server-owner surface: `sources/Api/Controllers/GameServersController.cs`, `sources/WebApp/Pages/Servers/Index.razor`

---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
<!-- refreshed: 2026-11-18 -->
# Architecture

**Analysis Date:** 2026-11-18

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────────┐
│                              Clients                                     │
├────────────────────────┬────────────────────────┬────────────────────────┤
│ React SPA (Vite/TS)    │ Blazor WASM WebApp     │ Discord slash commands │
│ `sources/ReactApp`     │ `sources/WebApp`       │ `Api/HostedServices/   │
│ openapi-typescript     │ Kiota client `UtfApi`  │  DiscordBot.cs`        │
└───────────┬────────────┴───────────┬────────────┴───────────┬────────────┘
            │ HTTPS / cookies / WS   │                        │
            ▼                        ▼                        ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                 API control plane  `sources/Api`                         │
│  Controllers ─► Services (Lifecycle, HostingRuntime, WorkerProtocol, …)  │
│  Hosted services: GameServerControlPlaneWorker, ProviderResource-,       │
│  DeploymentHealth-, HostingCost-ReconciliationWorker, DiscordBot         │
└──────┬──────────────────────┬─────────────────────────┬──────────────────┘
       │ EF Core (Npgsql)     │ Hetzner Cloud HTTP API   │ Worker protocol (HTTPS,
       ▼                      ▼                          │ worker pulls/reports)
┌──────────────────┐ ┌──────────────────────────┐        │
│ PostgreSQL       │ │ Hetzner (servers, vols,  │        │
│ `sources/Database`│ │ firewalls, primary IPs)  │        │
└──────────────────┘ └────────────┬─────────────┘        │
                                  │ cloud-init bootstrap  │
                                  ▼                       ▼
                    ┌──────────────────────────────────────────────┐
                    │ CloudWorker (one per active deployment)      │
                    │ `sources/CloudWorker` (container, SteamCMD)  │
                    │ Arma3 / Aska / CounterStrike2 executors      │
                    └──────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Aspire AppHost | Dev composition: Postgres, API, WebApp, ReactApp (Vite), optional dev CloudWorker container | `sources/AppHost/Program.cs` |
| API host | Config, auth (cookie + Steam + Discord), EF, CORS, rate limiting, OpenAPI, control-plane pause middleware | `sources/Api/Program.cs` |
| Controllers | HTTP endpoints for community domain and hosting | `sources/Api/Controllers/*.cs` |
| Lifecycle services | Enqueue/claim/execute durable lifecycle jobs, access, finance, audit, projections | `sources/Api/Services/GameServerLifecycle/` |
| Runtime providers | Provider-neutral `IRuntimeProvider`; Hetzner implementation; reconciliation; persistent storage | `sources/Api/Services/HostingRuntime/` |
| Worker protocol | Worker registration, credentials, command claim/report, bootstrap payload rendering, storage transfer | `sources/Api/Services/WorkerProtocol/` |
| Game definitions | API-safe game metadata, actions, file capabilities, mod source providers | `sources/Api/Services/GameServerDefinition/` |
| Game configuration impls | Shared config schema/rendering used by API and worker | `sources/GameServerImplementations/` |
| Hosting contracts | Hosting enums and API-to-worker wire DTOs | `sources/HostingContracts/`, `sources/HostingContracts/WorkerProtocol/` |
| CloudWorker | Outbound client, credential store, command journal, game install/process supervision | `sources/CloudWorker/` |
| Database | `DatabaseContext`, entities by area, migrations | `sources/Database/` |
| Update stream | In-memory WebSocket fan-out of typed messages | `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`, `sources/WebSocketContracts/` |
| Blazor WebApp | Legacy/parallel UI with Kiota client wrapped by repositories | `sources/WebApp/` |
| React SPA | Primary modern UI (routes, features, generated OpenAPI types) | `sources/ReactApp/src/` |
| Config parser | Bohemia config parsing (Pidgin) | `sources/X39.BI.Config.Parsing/ConfigParser.cs` |
| Service defaults | Aspire health, OTel, resilience, discovery | `sources/ServiceDefaults/Extensions.cs` |

## Pattern Overview

**Overall:** Modular monolith API acting as a durable control plane, plus a separately deployed pull-based worker per deployment.

**Key Characteristics:**
- HTTP requests never execute provider or game side effects directly: `GameServersController` actions return `202 Accepted` with a persisted `LifecycleJob` (`sources/Api/Controllers/GameServersController.cs:138`).
- Jobs are leased by `GameServerControlPlaneWorker` (`sources/Api/HostedServices/GameServerControlPlaneWorker.cs`) via `ILifecycleJobQueue.ClaimNextAsync` with a `LeaseOwner` column (`sources/Api/Services/GameServerLifecycle/LifecycleJobQueue.cs`); terminate jobs have reserved concurrency.
- Workers connect outbound only; the API exposes `lifecycle-workers/{workerId}/…` endpoints (`sources/Api/Controllers/LifecycleWorkersController.cs`).
- Providers persist intent before mutation, label resources, and are reconciled provider-wide (`ProviderResourceReconciler.cs`, `ProviderProvisioningMutationGuard.cs`).
- DI via `X39.Util.DependencyInjection` attributes (`[Scoped<Impl, IFace>]`), plus explicit `AddHostingControlPlane` in `HostingControlPlaneServiceCollectionExtensions.cs`.

## Layers

**Contracts (leaf projects):**
- Purpose: Shared types without server dependencies
- Location: `sources/Claims`, `sources/Common`, `sources/HostingContracts`, `sources/WebSocketContracts`, `sources/GameServerImplementations`
- Depends on: Claims <- Common <- WebSocketContracts; HostingContracts and GameServerImplementations are standalone
- Used by: Api, Database, WebApp, CloudWorker

**Persistence:**
- Purpose: EF Core model and migrations (PostgreSQL + NodaTime)
- Location: `sources/Database` (areas: `Authority`, `Content`, `Core`, `Eventing`, `Hosting`, `Media`, `Meta`, `Verein`, `Wiki`)
- Depends on: Claims, Common, HostingContracts
- Used by: Api only

**API control plane:**
- Purpose: Endpoints, authorization, application services, background reconcilers
- Location: `sources/Api`
- Depends on: Database, all contract projects, ServiceDefaults
- Used by: ReactApp, WebApp, CloudWorker (protocol only), Discord

**External worker:**
- Purpose: Deployment-local game execution
- Location: `sources/CloudWorker`
- Depends on: HostingContracts, GameServerImplementations only (never Api/Database)
- Used by: Hetzner servers bootstrapped through `CloudInitWorkerBootstrapPayloadRenderer.cs`; Aspire dev container

**Frontends:**
- `sources/ReactApp` (TS, Vite, generated `src/generated/api.ts` from `openapi/utf-api.json`)
- `sources/WebApp` (Blazor WASM, Kiota `UtfApi/` behind `Services/*Repository`)

## Data Flow

### Primary Request Path

1. Request hits pipeline in `sources/Api/Program.cs:81-108` (forwarded headers, path base, CORS, rate limiter, auth, `HostingControlPlanePauseMiddleware`).
2. Controller in `sources/Api/Controllers/` authorizes (policies from `sources/Api/Authorization/`) and queries `DatabaseContext` or a service.
3. Result projected into DTOs from `sources/Api/DTO/`; side effects may broadcast via `IUpdateStreamService`.

### Game Server Lifecycle Flow

1. `POST game-servers/{id}/actions/{actionId}` -> `IGameServerLifecycleService.EnqueueActionAsync` persists a `LifecycleJob` (`sources/Database/Hosting/LifecycleJob.cs`) and audit record.
2. `GameServerControlPlaneWorker` leases the job and runs `LifecycleJobExecutor` (`sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs`).
3. Executor calls `IRuntimeProvider.ProvisionAsync/ObserveAsync/ReleaseAsync` (`HetznerRuntimeProvider.cs` -> `HetznerCloudClient.cs`), writing `GameServerDeployment`/`DeploymentResource` rows.
4. Executor issues `WorkerCommand` rows; worker claims via `commands/claim`, reports `received/running/succeeded/failed`.
5. Job attempts recorded in `LifecycleJobAttempt`; failures sanitized with `HostingFailureSanitizer`.

### Worker Flow

1. `sources/CloudWorker/Program.cs` loads stored credential or bootstrap JSON (`/run/secrets/utf-worker-bootstrap.json`).
2. `CloudWorkerRuntime` registers, heartbeats, rotates credentials via `CloudWorkerApiClient`.
3. Commands journaled in `FlatFileWorkerCommandJournal` and dispatched through `WorkerGameExecutorRegistry` to `Arma3WorkerCommandExecutor` / `AskaWorkerCommandExecutor` / `CounterStrike2WorkerCommandExecutor`.
4. Storage snapshots/preservation flow through `CloudWorkerStorageTransfer` <-> `WorkerStorageTransferService`.

### Reconciliation Flows

- `ProviderResourceReconciliationWorker` -> `ProviderResourceReconciler` (discover, adopt, dangling incidents).
- `DeploymentHealthReconciliationWorker` -> `DeploymentHealthReconciler` (worker heartbeat health).
- `HostingCostReconciliationWorker` -> `HostingCostReconciler` / `AutomaticCostStopService` (ledger, cost stop).

**State Management:**
- Durable truth in PostgreSQL; worker-local state in its state directory (credential + journal).
- Update stream subscriptions are process-local in memory.
- `HostingControlPlaneState` singleton toggles pause/emergency state.

## Key Abstractions

**IRuntimeProvider:** `sources/Api/Services/HostingRuntime/IRuntimeProvider.cs` - `GetProfilesAsync`, `ProvisionAsync`, `ObserveAsync`, `ReleaseAsync`, `ReleaseDanglingAsync`, `DiscoverAsync`. Impl: `HetznerRuntimeProvider.cs`.

**IGameServerDefinitionRegistration / Catalog:** `sources/Api/Services/GameServerDefinition/` - metadata per game (`Arma3…`, `Aska…`, `CounterStrike2…Registration.cs`).

**IGameConfigurationImplementation:** `sources/GameServerImplementations/IGameConfigurationImplementation.cs` - shared config definition/rendering.

**IWorkerCommandExecutor:** `sources/CloudWorker/IWorkerCommandExecutor.cs` - per-game executor in worker.

**Lifecycle queue/executor:** `ILifecycleJobQueue.cs`, `ILifecycleJobExecutor.cs`.

**Authorization requirements:** `sources/Api/Authorization/Requirements/*Requirement.cs` via `Abstraction/AppAuthorizationHandler.cs`.

**Client repositories:** `sources/WebApp/Services/*Repository/` (Blazor); `sources/ReactApp/src/api/services.ts` (React).

## Entry Points

- `sources/Api/Program.cs` - API host (`dotnet run`, Aspire, `deploy/host/utf-api.service`, `Dockerfile`).
- `sources/CloudWorker/Program.cs` - worker console app; image `sources/CloudWorker/Dockerfile`, `docker-entrypoint.sh`.
- `sources/AppHost/Program.cs` - Aspire dev orchestration.
- `sources/WebApp/Program.cs` / `App.razor` - Blazor client.
- `sources/ReactApp/src/main.tsx` -> `src/app/App.tsx`, routes in `src/app/routes.ts`.

## Architectural Constraints

- **Threading:** ASP.NET Core async; background loops as `BackgroundService`s creating DI scopes per job.
- **Global state:** `UpdateStreamService` singleton (in-memory sockets); `HostingControlPlaneState` singleton; invariant culture set in `Program.Main`.
- **Circular imports:** None; CloudWorker must not reference Api/Database.
- **Process boundary:** No game process execution in the API; dev worker only allowed when `Environment.IsDevelopment()` (`sources/AppHost/Program.cs`).
- **Generated code:** `sources/WebApp/UtfApi/**`, `sources/ReactApp/src/generated/**`, `sources/Database/Migrations/**`.

## Anti-Patterns

### Executing side effects in controllers
**What happens:** Calling provider or worker logic inside a request.
**Why it's wrong:** Loses durability on restart.
**Do this instead:** Enqueue a `LifecycleJob` via `IGameServerLifecycleService` and handle in `LifecycleJobExecutor`.

### Hetzner specifics leaking upward
**What happens:** Using `Hetzner*` types outside `HostingRuntime`.
**Why it's wrong:** Breaks provider-neutral boundary.
**Do this instead:** Use `Runtime*` request/result types from `IRuntimeProvider`.

### Editing generated clients / migrations by hand
**Do this instead:** Regenerate (Kiota, `npm run contracts:generate`, `dotnet ef migrations add`).

### Returning EF entities from endpoints
**Do this instead:** Project to `sources/Api/DTO/*Dto`.

## Error Handling

**Strategy:** Domain exceptions mapped to HTTP results in controllers.

**Patterns:**
- `HostingNotFoundException`, `HostingForbiddenException`, `HostingConflictException`, `HostingValidationException`, `HostingAffordabilityException` in `Services/GameServerLifecycle/`.
- `WorkerProtocol*Exception` in `Services/WorkerProtocol/`.
- `LifecycleExecutionException` vs `PermanentLifecycleExecutionException` controls retry; `LifecycleSupersededException` for superseded jobs.
- Messages sanitized via `HostingFailureSanitizer` before logs/persistence; worker uses `WorkerSecretRedactor`.

## Cross-Cutting Concerns

**Logging:** `ILogger<T>` structured templates; OTel via ServiceDefaults; `HostingTelemetry.cs`.
**Validation:** Service-level validation exceptions; `RuntimeInboundNetworkPlanValidator`, `WorkerCommandValidation`.
**Authentication:** Cookie + Steam OpenID + Discord OAuth (`Program.AddAuth`); workers use deployment-scoped credentials (`WorkerCredentialService`) with `WorkerProtocolRateLimitPolicy`.

---

*Architecture analysis: 2026-11-18*

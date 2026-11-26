---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# Codebase Structure

**Analysis Date:** 2026-11-18

## Directory Layout

```
UnitedTacticalForces/
├── X39.UnitedTacticalForces.slnx   # Active solution (sources/ + tests/)
├── global.json, Dockerfile, compose.yaml
├── .github/workflows/dotnet.yml    # GitHub CI
├── .mozaic/                         # Mozaic release pipeline, image checks
├── ci/                              # Release/deploy helper scripts (Python/sh)
├── deploy/                          # Host config: nginx.conf, systemd units in host/
├── docs/
│   ├── features/cloud-based-game-server-hosting/  # Authoritative hosting spec
│   ├── operations/                  # e.g. hetzner-live-uat.md
│   ├── change-requests/             # Historical
│   └── schemas/                     # ui-route-manifest.schema.json
├── sources/
│   ├── Api/                         # ASP.NET Core control plane
│   ├── AppHost/                     # Aspire dev orchestration
│   ├── Claims/                      # Claim constants
│   ├── CloudWorker/                 # External per-deployment worker
│   ├── Common/                      # Shared app types
│   ├── Database/                    # EF Core context, entities, migrations
│   ├── GameServerImplementations/   # Shared game config definitions/renderers
│   ├── HostingContracts/            # Hosting enums + WorkerProtocol DTOs
│   ├── ReactApp/                    # React/Vite SPA
│   ├── ServiceDefaults/             # Aspire defaults
│   ├── WebApp/                      # Blazor WASM client
│   ├── WebSocketContracts/          # Update stream messages
│   └── X39.BI.Config.Parsing/       # Config parser library
├── tests/
│   ├── Api.Tests/                   # API + hosting tests (SQLite NodaTime context)
│   ├── CloudWorker.Tests/
│   ├── Ui.Tests/                    # WebApp host UI tests
│   └── X39.BI.Config.Parsing.Tests/
└── source/                          # Legacy, untracked/unreferenced - do not use
```

Untracked leftovers not in the solution: `sources/Api.Database/`, `sources/X39.UnitedTacticalForces.Roles/`, `source/`, `tmp/`, `artifacts/`.

## Directory Purposes

**`sources/Api`:**
- `Controllers/` - one controller per resource (`GameServersController.cs`, `LifecycleWorkersController.cs`, `CostOwnersController.cs`, `VereinController.cs`, ...)
- `Services/GameServerLifecycle/` - jobs, executor, access, finance, audit
- `Services/HostingRuntime/` - `IRuntimeProvider`, `Hetzner*` DTOs/client/provider, storage, placement, reconciliation
- `Services/WorkerProtocol/` - worker auth, commands, bootstrap, storage transfer
- `Services/GameServerDefinition/` - game metadata, mod sources/formats
- `Services/GameServerConfiguration/`, `GameServerSecrets/`, `Media/`, `StorageDemand/`, `Steam/`, `UpdateStreamService/`
- `HostedServices/` - background workers and `DiscordBot.cs`
- `Authorization/Abstraction`, `Authorization/Requirements`, `Authentication/`, `Middleware/`, `ModelBinding/`, `OpenApi/`
- `DTO/`, `DTO/Payloads/`, `DTO/Updates/` - transport shapes
- `Helpers/`, `ExtensionMethods/`, `Parsers/`, `Resources/`, `SeedAssets/`
- Key files: `Program.cs`, `Constants.cs`

**`sources/Database`:** `DatabaseContext.cs`, area folders (`Hosting/` holds `GameServer.cs`, `GameServerDeployment.cs`, `DeploymentResource.cs`, `LifecycleJob.cs`, `LifecycleWorker.cs`, `WorkerCommand.cs`, `CostOwner.cs`, `HostingCostLedgerEntry.cs`, ...), `Migrations/` (generated).

**`sources/CloudWorker`:** flat folder; core runtime (`CloudWorkerRuntime.cs`, `CloudWorkerApiClient.cs`, `CloudWorkerCredentialStore.cs`, `FlatFileWorkerCommandJournal.cs`), per-game files prefixed `Arma3*`, `Aska*`, `CounterStrike2*`, `Dockerfile`, `docker-entrypoint.sh`.

**`sources/HostingContracts`:** `E*.cs` enums at root; `WorkerProtocol/Worker*Request|Response.cs`.

**`sources/ReactApp`:** `src/app` (App, routes, config, i18n), `src/api` (client/services), `src/auth`, `src/components`, `src/features/{admin,events,events-member,hosting,wiki}`, `src/pages`, `src/generated` (generated), `openapi/utf-api.json`, `tests/` (vitest), `playwright.config.ts`.

**`sources/WebApp`:** `Pages/` (route-shaped folders, `_Param` for route params), `Components/`, `Shared/`, `Services/*Repository/`, `UtfApi/` (Kiota, generated).

## Key File Locations

**Entry Points:**
- `sources/Api/Program.cs`: API host
- `sources/CloudWorker/Program.cs`: worker
- `sources/AppHost/Program.cs`: Aspire dev
- `sources/ReactApp/src/main.tsx`: SPA
- `sources/WebApp/Program.cs`: Blazor

**Configuration:**
- `sources/Api/appsettings*.json` (`appsettings.secret.json` exists - never read)
- `sources/AppHost/appsettings*.json` (`Hosting:DevelopmentWorker`)
- `deploy/production.json`, `deploy/webapp.appsettings.json`, `deploy/host/utf-api.service`, `deploy/host/utf-web.service`
- `.editorconfig`, `global.json`

**Core Logic:**
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs`
- `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs`
- `sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs`
- `sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs`

**Testing:**
- `tests/Api.Tests/GameServerHosting/`, `tests/CloudWorker.Tests/`, `sources/ReactApp/tests/`

## Naming Conventions

**Files:**
- One type per file, file named after type: `HetznerServerCreateRequest.cs`
- Enums `E*`: `EDeploymentStatus.cs`; interfaces `I*`: `IRuntimeProvider.cs`
- Partial-class splits by concern: `Arma3WorkerCommandExecutor.Join.cs`
- Hetzner wire DTOs: `Hetzner<Thing>{Dto|Request|Response}.cs`
- Worker protocol: `Worker<Action>{Request|Response}.cs`
- React: `PascalCase.tsx` components, `camelCase.ts` modules, `kebab-case.test.ts` tests

**Directories:**
- PascalCase feature folders in C#; Blazor route params as `_ParamName`; React feature folders kebab-case

## Where to Add New Code

**New hosting lifecycle behaviour:**
- Primary code: `sources/Api/Services/GameServerLifecycle/`
- Entities: `sources/Database/Hosting/` + `dotnet ef migrations add`
- Tests: `tests/Api.Tests/GameServerHosting/`

**New runtime provider:**
- Implementation: `sources/Api/Services/HostingRuntime/<Provider>RuntimeProvider.cs` implementing `IRuntimeProvider`

**New game:**
- API metadata: `sources/Api/Services/GameServerDefinition/<Game>GameServerDefinitionRegistration.cs`
- Shared config: `sources/GameServerImplementations/<Game>ConfigurationImplementation.cs`
- Worker execution: `sources/CloudWorker/<Game>*.cs`, register in `WorkerGameExecutorRegistry` in `Program.cs`
- Tests: `tests/CloudWorker.Tests/<Game>ImplementationTests.cs`

**New worker protocol message:**
- `sources/HostingContracts/WorkerProtocol/`, server handling in `Services/WorkerProtocol/`, endpoint in `LifecycleWorkersController.cs`

**New API endpoint:** `sources/Api/Controllers/`, DTOs in `sources/Api/DTO/`; then refresh `sources/ReactApp/openapi/utf-api.json` and run `npm run contracts:generate`.

**New React UI:** `sources/ReactApp/src/features/<area>/`, shared controls in `src/components/`.

**Utilities:** `sources/Api/Helpers/`, `sources/Api/ExtensionMethods/`; cross-project types in `sources/Common/`.

## Special Directories

**`sources/WebApp/UtfApi/`, `sources/ReactApp/src/generated/`, `sources/Database/Migrations/`:** Generated: Yes; Committed: Yes.
**`sources/ReactApp/node_modules/`, `dist/`, `**/bin`, `**/obj`:** Generated: Yes; Committed: No.
**`sources/Api/.local/hosting/`, `sources/Api/App_Data/`:** Local runtime data (key ring, media); Committed: No.
**`source/`:** Legacy tree; Committed: No; not referenced.

---

*Structure analysis: 2026-11-18*

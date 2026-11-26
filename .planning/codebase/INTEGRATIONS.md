---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# External Integrations

**Analysis Date:** 2026-11-18

## APIs & External Services

**Cloud hosting provider:**
- Hetzner Cloud API (`https://api.hetzner.cloud/v1/`) - servers, volumes, primary IPs, firewalls, server types, pricing
  - Client: hand-written `sources/Api/Services/HostingRuntime/HetznerCloudClient.cs` with DTOs `Hetzner*Dto.cs` / `*Request.cs` / `*Response.cs`
  - Provider: `HetznerRuntimeProvider.cs` implementing `IRuntimeProvider.cs`; reconciliation via `IProviderResourceReconciler.cs`, mutation guard `ProviderProvisioningMutationGuard.cs`
  - Auth: `Hosting:Providers:Hetzner:Token` (must come from a secret source; BaseUrl validated to official host in `HetznerRuntimeOptions.cs`)

**Steam:**
- Steam Web API profile lookup - `SteamWebAPI2` in `sources/Api/Services/SteamRepository.cs`; auth `Steam:ApiKey`
- Steam Workshop metadata (`ISteamRemoteStorage/GetPublishedFileDetails/v1/`) - `sources/Api/Services/StorageDemand/SteamWorkshopMetadataClient.cs`
- SteamCMD - installed in the worker image (`sources/CloudWorker/Dockerfile`, checksum-pinned) for game content acquisition (Arma 3, Aska, Counter-Strike 2 executors in `sources/CloudWorker/`)

**Discord:**
- Discord bot / slash commands - `Discord.Net` in `sources/Api/HostedServices/DiscordBot.cs`; auth `Discord:Bot:BotToken`, `Discord:Bot:ApplicationId`, `Discord:Bot:PublicKey`

**TeamSpeak:**
- Display-only connection info (`TeamSpeak:Host`, `Port`, `Channel`, ...) from `sources/Api/Constants.cs`; no client SDK

## Data Storage

**Databases:**
- PostgreSQL
  - Connection: `ConnectionStrings:database` (Aspire `AddPostgres("pg-database")` in `sources/AppHost/Program.cs`)
  - Client: EF Core + Npgsql (+ NodaTime) via `sources/Database/DatabaseContext.cs`
- SQLite - test-only EF provider in `tests/Api.Tests`

**File Storage:**
- Local filesystem on the API host for persistent server data - `sources/Api/Services/HostingRuntime/FileSystemServerFileStorage.cs`, `PersistentServerStorageOptions.cs`, `PersistentStoragePathResolver.cs`
- Worker-local storage and transfer - `sources/CloudWorker/CloudWorkerStorageTransfer.cs`, credential store `CloudWorkerCredentialStore.cs`
- Hetzner volumes for deployment data

**Caching:**
- None (in-memory WebSocket subscribers only, `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`)

## Authentication & Identity

**Auth Provider:**
- Steam OpenID and Discord OAuth with cookie session (`AddCookie`, `AddSteam`, `AddDiscord` in `sources/Api/Program.cs`); Discord credentials `Discord:OAuth:ClientId` / `ClientSecret`
- JWT bearer package referenced in `sources/Api/Api.csproj`
- Development/testing bypass scheme `development-authorization-bypass` gated by `Testing:AuthorizationBypass:Enabled`
- Cloud workers: deployment-scoped bootstrap/worker credentials, outbound connection via `sources/CloudWorker/CloudWorkerApiClient.cs`; API side under `sources/Api/Services/WorkerProtocol`

## Monitoring & Observability

**Error Tracking:**
- None (no Sentry etc.)

**Logs:**
- `ILogger<T>` structured logging; OpenTelemetry logs/metrics/traces with OTLP exporter (`sources/ServiceDefaults/Extensions.cs`); hosting metrics in `sources/Api/Services/HostingRuntime/HostingTelemetry.cs`; failure redaction in `HostingFailureSanitizer.cs`

## CI/CD & Deployment

**Hosting:**
- Single Linux host (Amsel), rootless Podman, systemd user units `deploy/host/utf-api.service` / `utf-web.service`, nginx `deploy/nginx.conf`
- Images streamed over SSH (no registry on host); `:previous` tags for rollback; `pg_restore` backups in `~utf-deploy/backups/` (`deploy/host/README.md`)

**CI Pipeline:**
- Mozaic (x39.mozaic.space) - `.mozaic/pipeline.yml` (PR: react-tests, dotnet-release, react-browser jobs with TRX results), `.mozaic/release.yml` (trusted release + deploy); SSH key from org secret variable `AMSEL_DEPLOY_SSH_KEY`, public key `deploy/amsel-ci.pub`, pinned host key `deploy/known_hosts`
- GitHub Actions - `.github/workflows/dotnet.yml` (React checks on Node 24, .NET restore/build/test/publish; push/PR to master, weekly cron, releases)

## Environment Configuration

**Required env vars / config keys:**
- `ConnectionStrings:database`
- `General:ApiBaseUrl`, `General:ClientBaseUrl`, `General:BasePath`, optional `Cors:AllowedOrigins`
- `Steam:Enabled`, `Steam:ApiKey`
- `Discord:Enabled`, `Discord:OAuth:*`, `Discord:Bot:*`
- `Hosting:Providers:Hetzner:*` (Token, ServerType, Image, Location, pricing, Volume)

**Secrets location:**
- `sources/Api/appsettings.secret.json` / `appsettings.user.json` (local, not published), .NET user secrets, host `api.env` file, Mozaic organization secret variables, `~/.config/mozaic-deploy/targets/utf/` on the host for DB credentials

## Webhooks & Callbacks

**Incoming:**
- OAuth/OpenID callbacks for Steam and Discord (`sources/Api/Program.cs`)
- Worker protocol HTTP endpoints called outbound by each `CloudWorker` (`sources/Api/Services/WorkerProtocol`)
- WebSocket update stream at route `update-stream` (`sources/Api/Constants.cs`)

**Outgoing:**
- Hetzner Cloud API, Steam Web API, Discord gateway/REST, OTLP exporter endpoint

---

*Integration audit: 2026-11-18*

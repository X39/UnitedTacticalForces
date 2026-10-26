# UTF deployment requirements

This document describes required runtime bindings, not completed deployment.
Use the approved ordinary pipeline command supplied by the shared infrastructure
owner. Target mutation, nginx, databases, credentials and shared capacity remain
that owner's responsibility. The application worker must complete acceptance
against the actual accepted run; prepared YAML is not deployment evidence.

## Release inputs

Accept all four publications from the same checked source/run in applications
registry `01a166b4-bc34-7973-a711-bf840c866313`:

| Component | OCI repository | Output archive |
| --- | --- | --- |
| API | `utf/api` | `images/api.tar` |
| Web | `utf/web` | `images/web.tar` |
| Standard worker | `utf/cloud-worker` | `images/worker.tar` |
| ASKA worker | `utf/cloud-worker-aska` | `images/aska.tar` |

Runtime references use `x39.mozaic.space/applications/<repository>@sha256:<digest>`.
Do not resolve mutable tags. Only API and web run on the shared application target;
workers are outbound agents for individual game deployments.

## Data and startup

Preserve the existing PostgreSQL database and its major version. Compose specifies
PostgreSQL 17; historical mentions of PostgreSQL 18 are not upgrade authorization.
API startup applies EF migrations, seed updates and legacy secret migration before
listening. Before activation the runtime owner must quiesce the writer, take a
coherent database/files/key-ring backup and verify isolated restore. Image rollback
cannot reverse database changes.

API runs UID/GID `1654:1654` and needs durable writable paths:

- Media: `/app/App_Data`.
- Hosting storage: `/var/lib/united-tactical-forces/hosting`.
- Data-protection keys: `/home/app/.aspnet/DataProtection-Keys`.

Supply `ConnectionStrings__database` privately, and explicit absolute
`Hosting__Secrets__KeyRingPath` and `Hosting__PersistentStorage__BasePath`.
Configure the public `General__BasePath`, `General__ApiBaseUrl` and
`General__ClientBaseUrl`, retaining disabled authorization bypass and explicit
Steam/Discord enablement. Optional enabled integrations need their existing
private runtime credentials; none belongs in CI or frontend settings.

## Worker bindings

Bind these API settings to the two exact accepted worker receipts:

- `Hosting__WorkerProvisioning__ContainerImage`: standard worker digest.
- `Hosting__WorkerProvisioning__GameContainerImages__aska`: ASKA digest.
- `Hosting__WorkerProvisioning__RegistryServer`: `x39.mozaic.space`.
- `Hosting__WorkerProvisioning__RegistryUsername` and `RegistryToken`: private
  read-only authority restricted to the two worker repositories.
- `Hosting__WorkerProvisioning__RegistryAuthenticationRequired`: `true`.

Both production image defaults are intentionally empty, and the registry-server
default is `ghcr.io`; runtime configuration must override them. Preserve the
provider token privately. Never substitute API/web pull authority for worker
bootstrap authority. Compose is not the authoritative protected-target manifest.

Each worker keeps private journal state at `/var/lib/united-tactical-forces/worker`
and separate runtime data at `/srv/united-tactical-forces/runtime`. Game UID 10002
and Steam UID 10003 share GID 10001. ASKA requires Wine/Xvfb and persistent saves.
Publishing images does not authorize provisioning a game VM. Source-pinned OCI
assembly validates payload and ownership but does not prove Wine, SteamCMD or
worker process execution; runtime acceptance remains required.

## Application acceptance

Both API and web listen on TCP 8080. Preserve internal DNS `api:8080`; web nginx
forwards `/api/` without stripping its prefix. PostgreSQL TCP 5432 stays private.
Host ingress supplies HTTPS for `unitedtacticalforces.de` and its `www` alias.

Verify the accepted running API/web digests, then private and public behavior:

- `/`: HTTP 200 HTML.
- `/api/events/upcoming`: HTTP 200 JSON array from the anonymous database-backed
  API route, proving startup/migration/database access.
- `/next/`: React HTML and working referenced assets.
- `/appsettings.json`: correct public API origin and no private configuration.

Production does not expose `/health` or `/alive`. Full external worker operation
requires separately authorized registration, heartbeat and command-flow acceptance;
record publication bindings separately from that operational evidence.

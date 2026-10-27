# UTF deployment requirements

This document describes required runtime bindings, not completed deployment.
Use the approved ordinary pipeline command supplied by the shared infrastructure
owner. Target mutation, nginx, databases, credentials and shared capacity remain
that owner's responsibility. The application worker must complete acceptance
against the actual accepted run; prepared YAML is not deployment evidence.

The ordinary `activate-release` job invokes `ci/deploy.sh` using the reviewed
Nuremberg endpoint, `utf-deploy` account, protected `utf_target_invoke` reference
and public host key in `deploy/known_hosts`, with manifest `deploy/production.json`.
Its successful `checked-artifacts`
dependency must supply all three exact receipts. The coordinator owns installation,
private input activation and actual plan binding. Ordinary resource admission
handles scheduling without per-push slot permits.

## Release inputs

Accept all three publications from the same checked source/run in applications
registry `01a166b4-bc34-7973-a711-bf840c866313`:

| Component | OCI repository | Output archive |
| --- | --- | --- |
| API | `utf/api` | `images/api.tar` |
| Web | `utf/web` | `images/web.tar` |
| Shared worker for Arma 3, ASKA, and CS2 | `utf/cloud-worker` | `images/worker.tar` |

Runtime references use `x39.mozaic.space/applications/<repository>@sha256:<digest>`.
Do not resolve mutable tags. Only API and web run on the shared application target;
workers are outbound agents for individual game deployments.

## Data and startup

Preserve the existing PostgreSQL databases and their major version. The retained
operator provisioning receipt records separately authorized fresh
`utf_fresh_20261025` on the PostgreSQL 18 primary, while preserving original `utf`.
The coordinator must verify that authority and the staged private input activation
before startup. Compose's PostgreSQL 17 declaration does not authorize a downgrade,
reset, standby promotion or replacement of either retained database.
API startup applies EF migrations, seed updates and legacy secret migration before
listening. Before activation the runtime owner must quiesce the writer, take a
coherent database/files/key-ring backup and verify isolated restore. Image rollback
cannot reverse database changes.

Startup also starts durable lifecycle processing and provider reconciliation.
Isolated restore checks must fence provider access and remain separate from the
sole writable production control plane; publication does not authorize a game VM.

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

Bind these API settings to the single exact accepted shared worker receipt:

- `Hosting__WorkerProvisioning__ContainerImage`: shared worker digest.
- `Hosting__WorkerProvisioning__RegistryServer`: `x39.mozaic.space`.
- `Hosting__WorkerProvisioning__RegistryUsername` and `RegistryToken`: private
  read-only authority restricted to the shared worker repository.
- `Hosting__WorkerProvisioning__RegistryAuthenticationRequired`: `true`.

The production image default is intentionally empty, and the registry-server
default is `ghcr.io`; runtime configuration must override them. Preserve the
provider token privately. Never substitute API/web pull authority for worker
bootstrap authority. Compose is not the authoritative protected-target manifest.

### Migration from the separate ASKA image

Before releasing this source, the infrastructure owner must update its reviewed
immutable target helper and private input binding through the intended activation
program. Require exactly API, web, and worker receipts from the current generation;
`dependency_image_repositories` must contain only `worker`. Derive only
`Hosting__WorkerProvisioning__ContainerImage` from that accepted dependency.
Remove `Hosting__WorkerProvisioning__GameContainerImages__aska` and its old dependency
from the protected environment schema, effective environment hash and readiness
validation together. Preserve registry credentials, all other secrets, backup,
recovery, source/run/generation and provenance fences. Do not merely skip the
fourth receipt in an old helper or reuse an older worker lacking Wine support.

The public server-side properties `GameServerDefinition.WorkerImageIdentifier`
and `WorkerProvisioningOptions.GameContainerImages` were removed. Use the one
`ContainerImage`; remove the old game-image settings and development `ImageVariant`.
The shared `sources/CloudWorker/Dockerfile` replaces `Dockerfile.Aska` and contains
the former Wine/Xvfb dependencies. Existing deployed game workers are not changed
by this source edit; their replacement requires its own normal lifecycle operation.

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

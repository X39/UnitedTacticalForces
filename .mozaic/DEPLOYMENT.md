# UTF deployment requirements

This document describes required runtime bindings, not completed deployment.
Use the approved ordinary pipeline command supplied by the shared infrastructure
owner. Target mutation, nginx, databases, credentials and shared capacity remain
that owner's responsibility. The application worker must complete acceptance
against the actual accepted run; prepared YAML is not deployment evidence.

The ordinary `activate-release` job uses the pinned Amsel endpoint,
`utf-deploy` account, and the host-owned key in project secret
`managed-utf-ssh`. Its `checked-artifacts` dependency supplies all three
exact receipts to the UTF-specific receiver. It defaults to receive-only
validation. Require retained runtime adoption, coherent backup and scoped
worker digest acceptance before enabling activation.

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

### Hosting execution pause

The API defaults to a paused hosting control plane. Both API image packaging paths
set the public image environment `Hosting__ControlPlane__Paused=true`; the application
also defaults to paused when `Hosting:ControlPlane:Paused` is absent. A malformed
boolean rejects startup. The decision is immutable for the process lifetime.

While paused, the API does not register the lifecycle processor, deployment health,
hosting cost, or provider resource reconciliation services. Retained queued jobs,
future retries, waiting commands, and expired leases are preserved without claiming
or executing them. Hosting mutation requests and every deployment-worker protocol
request receive HTTP 503 with code `hosting_control_plane_paused`; the API never
pretends an operation succeeded. Existing action/provider projections show the
pause in both user interfaces and disable action, retry, and skip controls.

Verify the effective state without credentials or private configuration through
`GET /api/hosting/control-plane`: it returns `{"paused":true}` with `Cache-Control:
no-store`. This endpoint reports the same validated singleton used for processor
registration and request admission. The API startup log records the boolean too.
Database migrations and application catalog seeding still run normally.

Before the first API start, run the accepted API image with the additional argument
`--check-hosting-paused` through the intended native configuration consumer. This
supported diagnostic loads the same application JSON files, optional secret/user
JSON, environment variables, and command-line overrides in their normal order, and
reuses the runtime pause validator. It exits before service registration, database
access, migrations, hosted services, provider access, or Steam work. A successful
check prints only `{"paused":true}` and exits zero. An enabled/invalid pause value or
configuration loading error exits nonzero without printing configuration content.
The diagnostic does not start an HTTP listener.

Use a read-only, network-disabled probe with the same effective configuration as
the intended API start. Feed private environment input through the existing native
consumer's `--env-file` support; never read or print its contents. The image default
needs no override. Match all intended file mounts and argument overrides, if any:
a probe that omits a future override is not proof of that future configuration.
Remove only the probe container created for this check before the operator's
zero-runtime inventory proof. Successful diagnostics do not authorize game-worker
execution or a provider machine; verify the HTTP status again after the API starts.

Only API and web containers start on the shared application target; the accepted
worker image remains a remote deployment dependency. A pause does not terminate
already-running external game processes or withdraw commands already delivered by
another API instance. Before relying on paused startup, the operator must ensure
there are no running deployment workers/other enabled API instances with accepted
commands. Provider cleanup and billing reconciliation are also paused, so existing
billable resources require explicit operator handling.

Future authorized game execution requires an explicit operator override
`Hosting__ControlPlane__Paused=false` (or `Hosting:ControlPlane:Paused=false` in the
supported configuration consumer) and an API restart. There is no UI or HTTP resume
switch. Enabling resumes retained work using the existing authorization, ownership,
lease, and provider guards; audit the backlog before doing so. The current protected
native private-input schema need not change merely to start API/web paused. Any
future native-input override must be added through its reviewed schema/version
transition rather than inserting an unsupported key into the private map.

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
Host ingress supplies HTTPS for `unitedtacticalforces.de`, its `www` alias,
`old.unitedtacticalforces.de`, and the retired canary hostname. Preserve the incoming
host when proxying to web nginx. Main serves the existing root-built React assets;
only `old.unitedtacticalforces.de` selects Blazor. Redirect www and canary to main.
`/next` also redirects to main instead of exposing another optional interface.

Both interfaces load same-origin `/api/` from the public configuration endpoint.
Keep the existing API path base `/api`, secure host-only authentication cookies,
database and data-protection keys. Steam callbacks are `/api/signin-steam` on the
requesting host; enabled Discord requires its corresponding `/api/signin-discord`
callback registered for each interface host. Relative authentication return paths
preserve navigation on old without expanding the API's absolute redirect allowlist.
An existing main-host session remains on main; old requires its own host session.
The Blazor boot-declared empty `appsettings.Production.json` must remain packaged.
The existing UTF rootless network is `10.89.0.0/24`; production
`ReverseProxy:KnownNetworks` trusts that isolated nginx-to-API hop so forwarded
HTTPS produces HTTPS provider callbacks. Match this setting to the actual private
proxy subnet when moving the application; do not trust arbitrary public proxies.

Verify the accepted running API/web digests, then private and public behavior:

- `/`: HTTP 200 HTML.
- `/api/events/upcoming`: HTTP 200 JSON array from the anonymous database-backed
  API route, proving startup/migration/database access.
- Main `/`: React HTML and working referenced assets; `/next/` redirects to main.
- Old `/`: Blazor HTML, boot resources and production configuration load successfully.
- `/appsettings.json`: correct public API origin and no private configuration.

Production does not expose `/health` or `/alive`. Full external worker operation
requires separately authorized registration, heartbeat and command-flow acceptance;
record publication bindings separately from that operational evidence.

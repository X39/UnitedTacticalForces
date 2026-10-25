# UTF release handoff — 25 October 2026

Application preparation is complete for the existing native release contract,
subject to the unexecuted cloud gates below. This is not a CI or deployment receipt.
The preparation commit containing this document follows source
`8435057ddd1cf2d4c3340d1a5709093a82fe941d`; dispatch its committed successor,
including the Compose key-ring correction. Pre-existing `.mozaic/sdk/` changes
are excluded and remain unrelated work. Existing remotes are preserved.

## Source and publication inputs

Broker verification at 20:58 UTC on 25 October returned `ok=true`, HTTP 200
for project, source repository, pipeline registration and OCI repository lists.
The existing project and source are private; the repository is active with
default branch `refs/heads/main`. All four OCI repositories below are private,
active and unquarantined. No duplicate resource was created.

The sole pipeline registration is `01a16867-883d-7033-a542-87081c09c572`,
named `UTF complete tested immutable release`, pointing to `.mozaic/release.yml`.
Listing without a revision returns `revision: null`, empty triggers and zero
jobs by design; it is registration evidence, not compilation evidence.
The documented GET with both
`?revision=8435057ddd1cf2d4c3340d1a5709093a82fe941d&repository_type=git`
was rejected by the broker with `ok=false`, `httpStatus: 0`,
`code: request_rejected`. Central handling must permit this exact read-only
validation query or perform it through the supported private API channel.
No browser MFA or new token is needed for the already verified setup routes.
Derived Git/pull credentials, branch policy, triggers and deployment remain
outside this delegated broker subset; central ownership is unchanged.

Use the existing private X39 project `01a166ad-9024-7b30-a9bc-cd03ac37f2e8`,
repository `01a166ad-93f3-7fa2-a4f0-e57014752726`, trusted `refs/heads/main`,
and full definition `.mozaic/release.yml`. Do not register the static pilot as
the full release or create a replacement repository. Registry ID is
`01a166b4-bc34-7973-a711-bf840c866313`.

| Component | Dockerfile / target | OCI repository | Repository ID |
| --- | --- | --- | --- |
| API | `Dockerfile` / `api` | `utf/api` | `01a166c1-9739-7210-95bf-12f3e61ae8e0` |
| Blazor and React web | `Dockerfile` / `web` | `utf/web` | `01a166b4-bcbc-7403-b21e-46de55ee811f` |
| Standard worker | `sources/CloudWorker/Dockerfile` / `runtime` | `utf/cloud-worker` | `01a166c1-9796-79b3-80ee-041bb0075417` |
| ASKA worker | `sources/CloudWorker/Dockerfile.Aska` / `runtime` | `utf/cloud-worker-aska` | `01a166c1-97fa-7431-83a3-6dabe264c858` |

Native registry coordinates use `x39.mozaic.space/applications/` followed by
the repository above. All contexts are repository root. `release` is a mutable
publication alias; record each accepted `@sha256:` manifest, exact source SHA,
run and plan together. No accepted component manifest digest is available here.
Full hosting acceptance requires all nine jobs and four publications from the
same successful trusted run. Web/API-only activation is a distinct runtime-owner
decision and must omit remote-worker inputs as documented in Mozaic's
`deploy/utf-worker-inputs.md`. Worker publication does not start game servers.

The immutable base aliases and action pin remain exactly in `release.yml`.
Both worker files retain SDK `3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f`
and runtime `ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe`.
The shared owner must confirm admission/preload of every declared base before
dispatch. No new dependency image or local-worker alternative is accepted.

## Runtime boundary and readiness

Only web and API run on the application target, with dedicated PostgreSQL.
Preserve the internal DNS alias `api:8080`: bundled nginx proxies `/api/`
without stripping the prefix. Both application images listen on TCP 8080;
PostgreSQL uses private TCP 5432. Host ingress terminates HTTPS for
`unitedtacticalforces.de` and `www.unitedtacticalforces.de`; documented retained
host routing is loopback `127.0.0.1:18080` to web. Inspect existing runtime
ownership before activation; this document does not authorize changing it.

Check `GET https://unitedtacticalforces.de/` for HTML and
`GET https://unitedtacticalforces.de/api/events/upcoming` for HTTP 200 and a JSON
array, plus their private equivalents. The latter is an anonymous database-backed
API route. Production has no `/health` or `/alive` readiness endpoint. This
workstation's HTTPS probes failed with `HttpRequestException`; no production
health or deployed digest is claimed.

Preserve PostgreSQL data, API media `/app/App_Data`, hosting files
`/var/lib/united-tactical-forces/hosting`, and the absolute key-ring directory
`/home/app/.aspnet/DataProtection-Keys`. Compose now explicitly configures that
key-ring path on its existing keys volume. The protected target must independently
bind its durable paths and verified nonroot API UID; Mozaic's example UID 0 and
example source/repository IDs are placeholders, not usable activation inputs.
Likewise its example worker/ASKA repository paths differ from the actual bindings
above and must be replaced by the runtime owner.

API startup applies EF migrations and seed updates. Quiesce the sole writer,
take a coherent database/files/key-ring backup, and retain a verified isolated
restore before activation. Image rollback cannot undo migrations. PostgreSQL 18
startup/migration acceptance remains a runtime gate; retained Compose specifies
PostgreSQL 17 and is not authorization to upgrade any existing database volume.

Each active game deployment has one external, outbound-connected worker.
Standard and ASKA images are dependencies, never permanent API-host services.
The agent has private durable command state and a separate runtime-data mount;
game UID 10002 and Steam UID 10003 share runtime GID 10001. Selected game metadata
defines game/query ports. ASKA additionally requires Wine/Xvfb and durable saves.
Worker defaults bound the container to 7g memory and 1024 PIDs; game sizing and
disk admission remain deployment-specific. No game VM is authorized by this handoff.

## Runtime input names

Provision values privately through the supported runtime-owner flow, never CI.
Keep existing values; frontend appsettings are public.

- Required database secret: `ConnectionStrings__database`.
- Public origin/configuration: `General__BasePath`, `General__ApiBaseUrl`,
  `General__ClientBaseUrl`; preserve explicit `Steam__Enabled` and
  `Discord__Enabled` switches and disabled authorization bypass.
- Durable paths: `Hosting__Secrets__KeyRingPath`,
  `Hosting__PersistentStorage__BasePath`.
- Full worker group: `Hosting__WorkerProvisioning__ContainerImage`,
  `Hosting__WorkerProvisioning__GameContainerImages__aska`,
  `Hosting__WorkerProvisioning__RegistryServer`,
  `Hosting__WorkerProvisioning__RegistryUsername`,
  `Hosting__WorkerProvisioning__RegistryToken`,
  `Hosting__WorkerProvisioning__RegistryAuthenticationRequired`.
- Hosting provider secret: `Hosting__Providers__Hetzner__Token`.
- Optional enabled integrations: `Steam__ApiKey`, `Steam__Username`,
  `Steam__Password`, `Discord__OAuth__ClientId`,
  `Discord__OAuth__ClientSecret`, `Discord__Bot__ApplicationId`,
  `Discord__Bot__PublicKey`, `Discord__Bot__BotToken`.

Separate the target's API/web pull authority from the remote worker's exact
two-repository pull credential. No registry push or deployment credentials enter
jobs. Retain protected worker bootstrap and journal custody outside game users.

## Evidence and next cloud action

Historical local checks in `RELEASE.md` remain evidence for unchanged inputs;
they are not cloud CI. Focused preparation checks passed: YAML parsing, the
Production key-ring path matching its durable mount, nine release jobs, four
publication bindings, immutable base pins and complete Dockerfile alias maps.
`git diff --check` passed. No unchanged solution/browser suites were rerun.
Previous run
`01a1711a-7635-7932-ae0c-dc9559b27cba` was reported cancelled by the coordinator;
its prior queued state and nine unexecuted jobs are not a successful release.
No blind retry is requested.

The coordinator reports Mozaic `63ff5ad` healthy and one authorized Fedora
CPX42 `172392630`, runner `1225916b-d114-592c-a21c-fc732139a506`, one slot.
Capacity authorization is therefore not the pending dependency. The platform
finisher `01a173ae-6661-7511-9261-4db56e4fcdbc` owns native Dockerfile support on
Fedora: current inspected `apps/mozaic-runnerd/src/control.rs` rejects a native
build unless the lease is trusted and `uses_debian_organization_profile()` holds,
with error `native Dockerfile builds require the attested Debian organization
profile and a trusted lease`. UTF requests `fedora-44-cloud-v1` and
`bounded-disk-v1`; changing labels alone cannot establish native support.

Next cloud action: after the finisher supplies accepted native-action/profile
and base-admission readiness, the coordinator dispatches this committed full
release once through the supported trusted-main path. Retain all nine outcomes,
four same-run immutable publication receipts, and bounded cleanup observations.
Jobs retain 1 CPU, 1536 MiB RAM, 1024 MiB scratch, 256 PIDs, 32 MiB logs and
30-minute timeouts in a serial chain. Browser/native layer size, Wine/multilib
and the independent builder store must fit those accepted bounds; actual
execution remains unverified. No push, cloud trigger, provisioning, production
mutation, secret read or deployment occurred during this preparation.

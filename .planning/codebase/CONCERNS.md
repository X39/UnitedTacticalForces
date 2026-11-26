---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# Codebase Concerns

**Analysis Date:** 2026-11-18

## Tech Debt

**Oversized control-plane classes:**
- Issue: Core hosting logic sits in a few very large files that mix orchestration, provider calls, persistence, and error mapping.
- Files: `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs` (~3260 lines), `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs` (~2310), `sources/Api/Services/HostingRuntime/ProviderResourceReconciler.cs` (~1420), `sources/CloudWorker/Arma3WorkerCommandExecutor.cs` (~1520), `sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs` (~1150), `sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs` (~1130), `sources/CloudWorker/CloudWorkerStorageTransfer.cs` / `sources/Api/Services/WorkerProtocol/WorkerStorageTransferService.cs` (~1100 each), `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs`, `sources/Api/Services/GameServerLifecycle/HostingFinanceService.cs` (~1000 each).
- Impact: Hard to review, high merge-conflict rate, and a change to one job kind can regress another. The matching test files are just as large (`tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs`, `HetznerRuntimeProviderTests.cs` at ~2100 lines each).
- Fix approach: Split by job kind or lifecycle phase (for example, one handler per job type behind a dispatcher). Split provider create, observe, release, and discover into collaborators. Do this only behind the existing tests.

**Fat controllers with inline domain logic:**
- Issue: Controllers project EF entities and run use-case logic inline.
- Files: `sources/Api/Controllers/VereinController.cs` (~1975 lines), `sources/Api/Controllers/UsersController.cs` (~1145), `sources/Api/Controllers/EventsController.cs` (~1020), `sources/Api/Controllers/CostOwnersController.cs` (~920), `sources/Api/Controllers/ConfigurationProfilesController.cs` (~900).
- Impact: Logic cannot be reused or unit-tested apart from HTTP. Authorization checks are spread across actions.
- Fix approach: Move use cases into services under `sources/Api/Services/<Feature>`. Keep the controllers thin.

**Two parallel frontends:**
- Issue: Both the Blazor WebAssembly app (`sources/WebApp`) and a React/Vite app (`sources/ReactApp`, described in `docs/features/react-ui-beta.md`) are active. AppHost starts both (`sources/AppHost/Program.cs` lines 12-14), and CI builds both (`.github/workflows/dotnet.yml`, `.mozaic/pipeline.yml` job `react-tests`).
- Impact: Every feature needs UI work twice, using two generated clients (Kiota `sources/WebApp/UtfApi`, plus the OpenAPI contracts in `sources/ReactApp/openapi`). It also doubles the localization upkeep (`sources/WebApp/Properties/Language.Designer.cs` is ~20k lines) and the CI time.
- Fix approach: Decide which frontend is the target and set a deprecation date for the other. Until then, gate new features on the chosen one.

**Legacy and stray trees in the repository root:**
- Issue: `source/` (old `X39.UnitedTacticalForces.*` projects) is still committed and is not in `X39.UnitedTacticalForces.slnx`. `sources/Api.Database` and `sources/X39.UnitedTacticalForces.Roles` are also outside the solution. Root planning notes (`NUCLEAR_OPTION_IMPLEMENTATION_PLAN.md`, `codex.md`, `todo.md`) are tracked, and user-specific `*.DotSettings.user` files are committed. Local build outputs exist (`sources/Api/bin/Codex`, `sources/Api/bin/CodexBuild`, `tests/Ui.Tests/bin`, `tmp/`).
- Impact: Grep and search results are polluted (for example, Playwright `protocol.d.ts` TODOs under `tests/Ui.Tests/bin`). Contributors may edit dead code.
- Fix approach: Delete or archive `source/` and the unreferenced projects. Move the notes into `docs/` or `.planning/`. Untrack the `*.DotSettings.user` files and add `tmp/` to `.gitignore` if it is not already covered.

**Large debug seeder:**
- Issue: `sources/Api/Data/DebugDemoDataSeeder.cs` (~2700 lines) is hand-maintained demo data compiled into the API assembly.
- Impact: It drifts from the schema with each migration and enlarges the production binary.
- Fix approach: Move it into fixture files or a development-only assembly.

**Two CI systems:**
- Issue: GitHub Actions (`.github/workflows/dotnet.yml`) and Mozaic (`.mozaic/pipeline.yml`, `.mozaic/release.yml`, plus about 10 shell/Python helper scripts) both build and test. Mozaic also does the release and deploy.
- Impact: Divergent gates, so a green result on one system does not imply green on the other.
- Fix approach: Pick one system as the authority. Reduce the other to a mirror or remove it.

## Known Bugs

**Unbounded per-key semaphore dictionaries:**
- Symptoms: Memory grows slowly over the API's lifetime because entries are never removed.
- Files: `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs` (line 39 `_provisioningLocks`, `GetOrAdd` at line 174, no `TryRemove`), `sources/Api/Services/HostingRuntime/ServerFileLockProvider.cs` (`_locks`, line 9).
- Trigger: Each new provisioning intent or server ID adds a `SemaphoreSlim` that stays forever.
- Workaround: Restart the process. Fix with ref-counted removal or a keyed lock that has eviction.

**Process-local locks assume a single API instance:**
- Symptoms: Duplicate provisioning attempts or concurrent file edits if more than one API replica runs.
- Files: the same two lock providers as above.
- Trigger: Horizontal scaling. The durable job lease in `sources/Api/HostedServices/GameServerControlPlaneWorker.cs` protects jobs, but these in-memory gates do not protect anything across processes.
- Workaround: Run exactly one API instance (the current deployment, `deploy/host/utf-api.service`).

## Security Considerations

**Development authorization bypass compiled into production:**
- Risk: `sources/Api/Authentication/DevelopmentAuthorizationBypassAuthenticationHandler.cs` is registered when `Testing:AuthorizationBypass:Enabled` is true (`sources/Api/Program.cs` lines 307-393). If it is misconfigured, every endpoint is open.
- Current mitigation: `ValidateAuthorizationBypassConfiguration` checks the environment. Production explicitly sets `Testing__AuthorizationBypass__Enabled=false` in `deploy/host/utf-api.service`.
- Recommendations: Exclude the handler from Release builds with `#if DEBUG` or move it into a separate assembly. Add a test asserting that startup fails when the flag is set under Production.

**Worker credential hashing:**
- Risk: Worker credentials are stored as unsalted SHA-256 (`sources/Api/Services/WorkerProtocol/WorkerCredentialService.cs` lines 119-143). This is acceptable only if credentials are high-entropy random tokens.
- Current mitigation: Comparison is fixed-length. Rate limiting applies through `WorkerProtocolRateLimitPolicy` (`sources/Api/Program.cs` line 235).
- Recommendations: Document and enforce a minimum credential entropy at generation. Add a test that `CryptographicOperations.FixedTimeEquals` is used.

**Secret material in config and on disk:**
- Risk: Several secret classes reach the API: provider API tokens, Steam account credentials, Discord bot/OAuth secrets, worker bootstrap credentials, and per-server secrets (`sources/Api/Controllers/GameServerSecretsController.cs`). The DataProtection key ring sits on a podman volume (`Hosting__Secrets__KeyRingPath`). Losing that volume makes stored secrets unrecoverable; leaking it decrypts them.
- Current mitigation: `appsettings.secret.json` is excluded from publish. Production secrets come from a host env-file outside the repo (`deploy/host/utf-api.service`). The container runs read-only with `--cap-drop=ALL`.
- Recommendations: Back up and encrypt the key-ring volume. Add a log-redaction test covering every secret class. Rotate provider tokens on a schedule.

**Deploy pipeline trust:**
- Risk: Release deploys over SSH with one organization-wide key. The committed public key and pinned host keys are `deploy/amsel-ci.pub` and `deploy/known_hosts`. The host bootstrap scripts (`deploy/host/Initialize-Host.ps1`, `Add-OrgKeyVariable.ps1`, `Set-DeployKeyVariable.ps1`) are untracked. A single org-wide key widens the blast radius to every target that trusts it.
- Current mitigation: Host keys are pinned, and the Mozaic jobs use network allow-lists (`.mozaic/pipeline.yml`).
- Recommendations: Use per-target deploy keys with a forced command. Commit the reviewed host scripts or remove them. Keep `known_hosts` current when hosts are rotated.

## Performance Bottlenecks

**Process-local WebSocket fan-out:**
- Problem: `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs` keeps all subscribers in memory under a lock and broadcasts serially.
- Cause: It is a singleton with no backplane.
- Improvement path: Send concurrently with per-socket timeouts. Add a backplane (for example, Postgres LISTEN/NOTIFY) before running multiple replicas.

**Polling control plane and reconciliation:**
- Problem: Job claiming (`GameServerControlPlaneWorker.cs`) and provider-wide discovery (`ProviderResourceReconciler.cs`) poll the database and the Hetzner API on a timer.
- Cause: There is no notification-driven wakeup.
- Improvement path: Tune the intervals through options and watch Hetzner rate limits. Consider NOTIFY-based wakeups for newly enqueued jobs.

## Fragile Areas

**Provisioning, attribution, and teardown:**
- Files: `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs`, `HetznerCloudClient.cs`, `ProviderResourceReconciler.cs`, `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs`.
- Why fragile: Correctness depends on persisting intent before mutating the provider, on resource labels, on adopting late-created resources, and on verified non-billable teardown. A missed label or a skipped verification leaks billable servers.
- Safe modification: Keep the order "persist intent, then mutate the provider, then observe". Extend `tests/Api.Tests/GameServerHosting/HetznerRuntimeProviderTests.cs` and `ProviderResourceReconcilerTests.cs` for each change.
- Test coverage: Unit tests use a fake client. No test runs against the real Hetzner API.

**Worker protocol and command redelivery:**
- Files: `sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs`, `sources/CloudWorker/*` (command journal, `Arma3WorkerCommandExecutor.cs`, `CloudWorkerStorageTransfer.cs`), `sources/HostingContracts`.
- Why fragile: Exactly-once effects rely on stable command IDs plus a local journal on both sides. Contract changes must stay compatible with workers that are already deployed.
- Safe modification: Version the contracts in `sources/HostingContracts`. Add redelivery and restart cases to `tests/CloudWorker.Tests` and `tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs`.

**Lease-based job execution:**
- Files: `sources/Api/HostedServices/GameServerControlPlaneWorker.cs`.
- Why fragile: Lease renewal runs as a parallel task. If renewal is lost while work is in progress, a second owner can take the job.
- Safe modification: Keep every side effect idempotent and keyed by job and intent IDs.

**Broad exception catches:**
- Files: about 60 `catch (Exception` sites in `sources/Api/Services` and `sources/CloudWorker`.
- Why fragile: They can hide programming errors as retryable failures.
- Safe modification: Narrow them to the expected types, and always log with structured context.

## Scaling Limits

**Single API instance:**
- Current capacity: One podman container (`deploy/host/utf-api.service`, 2 CPU, 2 GiB, `--pids-limit=512`).
- Limit: In-memory locks, WebSocket subscribers, and local-filesystem persistent storage (`Hosting__PersistentStorage__BasePath` volume) all stop working correctly with a second replica.
- Scaling path: Move to distributed locks (Postgres advisory locks), a WebSocket backplane, and shared or object storage.

## Dependencies at Risk

**Kiota-generated client plus OpenAPI contracts for React:**
- Risk: Both clients must be regenerated after every API change. A stale client silently breaks the UI.
- Impact: Runtime 4xx/5xx errors in one of the two frontends.
- Migration plan: Fail CI on stale generation, which React already does with `npm run contracts:check`. Add an equivalent check for Kiota.

**Bundling a game runtime into the worker image:**
- Risk: Per `todo.md`, Wine and Xvfb for ASKA add about 439 MB to the shared cloud worker image.
- Impact: Slower provisioning and higher egress for every game.
- Migration plan: Per-game, on-demand dependency setup, as described in `todo.md`.

## Missing Critical Features

**Game-selected execution strategies:**
- Problem: The worker supports only direct installation. Container-based game execution and versioned setup plans are missing (`todo.md`).
- Blocks: Lean worker images, and games such as DayZ that ship as community containers.

**Second runtime provider:**
- Problem: Hetzner is the only `IRuntimeProvider` (`sources/Api/Services/HostingRuntime/IRuntimeProvider.cs`).
- Blocks: Proving that the provider-neutral abstraction holds.

## Test Coverage Gaps

**Real-provider and end-to-end teardown:**
- What's not tested: Live Hetzner create, discover, and delete, and the full worker-to-API run outside the Aspire harness.
- Files: `sources/Api/Services/HostingRuntime/HetznerCloudClient.cs`, `sources/CloudWorker`.
- Risk: Billable resource leaks after provider API changes.
- Priority: High

**Controllers and authorization:**
- What's not tested: Many large controllers (`VereinController.cs`, `UsersController.cs`, `CostOwnersController.cs`) have little direct coverage in `tests/Api.Tests`.
- Files: `sources/Api/Controllers/*`.
- Risk: Authorization regressions on cost-owner and secrets endpoints.
- Priority: High

**Deploy scripts:**
- What's not tested: The PowerShell host bootstrap scripts (`deploy/host/*.ps1`) and the systemd units.
- Files: `deploy/host/`.
- Risk: A broken first deploy, or a broken rollback (see the recent rollback fix in commit e8883f40).
- Priority: Medium

---

*Concerns audit: 2026-11-18*

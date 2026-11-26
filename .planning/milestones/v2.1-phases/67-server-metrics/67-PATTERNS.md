# Phase 67: Server Metrics - Pattern Map

**Mapped:** 2026-11-21
**Files analyzed:** 37 new/modified (grouped; see table)
**Analogs found:** 33 / 37 (4 partial or none: worker WebSocket client/handler/registry/A2S)

All analog paths below were checked as git-tracked source under `sources/` and `tests/` (no `.gsd` capability mirrors involved). Paths are relative to `A:/dev/git/unitedTacticalForces`. The closest end-to-end analog for the whole metrics vertical (entity, hosted worker, publisher, REST endpoint, browser frame, query cache, tile) is Phase 66 accrued cost / cost alerts. The worker channel (D-02) has no WebSocket analog on the worker side; reuse `UpdateStreamController` (accept loop) and `LifecycleWorkersController` (auth and exception mapping).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/HostingContracts/WorkerProtocol/WorkerMetricsSample.cs` | contract (record, validated) | request-response | `sources/HostingContracts/WorkerProtocol/WorkerHeartbeatRequest.cs` | exact |
| `sources/HostingContracts/WorkerProtocol/WorkerChannelFrame.cs`, `EWorkerChannelFrameType.cs`, `WorkerChannelAck.cs`, `EWorkerChannelOutcome.cs`, `WorkerMetricUnavailableReasons.cs` | contract | event-driven | `sources/HostingContracts/WorkerProtocol/WorkerCommandStageRequest.cs`, `EWorkerWorkloadState.cs`; reasons: `sources/Api/Services/GameServerLifecycle/AccruedCostUnavailableReasons.cs` | role-match |
| `sources/WebSocketContracts/Hosting/GameServerMetricsMessage.cs` | contract (browser frame) | pub-sub | `sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs` | exact |
| `sources/Database/Hosting/ServerMetricBucket.cs` (+ `DbSet` in `DatabaseContext.cs`, scaffolded migration) | model | CRUD / batch | `sources/Database/Hosting/CostAlert.cs` | exact |
| `sources/Api/Controllers/LifecycleWorkersController.cs` (add `GET channel`) | controller | streaming | same file + `sources/Api/Controllers/UpdateStreamController.cs` | exact |
| `sources/Api/Services/WorkerProtocol/WorkerChannelHandler.cs` | service | streaming / request-response | `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs` (loop, send lock) + `LifecycleWorkersController.TryMapException` | role-match |
| `sources/Api/Services/WorkerProtocol/WorkerChannelRegistry.cs`, `WorkerCommandDispatcher.cs`, `IWorkerCommandSignal.cs` | service | pub-sub | `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs` | role-match |
| `sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs` (accept protocol 1..3, supported games) | service | request-response | itself | exact |
| `sources/Api/Services/ServerMetrics/ServerMetricsService.cs`, `ServerMetricsOptions.cs` | service | CRUD / transform | `sources/Api/Services/GameServerLifecycle/CostAccrualService.cs`, `CostAlertOptions.cs` | role-match |
| `sources/Api/Services/ServerMetrics/ServerMetricsPublisher.cs` | service | pub-sub | `sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs` | exact |
| `sources/Api/HostedServices/ServerMetricsPruneWorker.cs` (+ registration) | service (BackgroundService) | batch | `sources/Api/HostedServices/CostAlertWorker.cs` | exact |
| `sources/Api/Controllers/GameServersController.cs` (add `GET {id}/metrics`) | controller | request-response | same file `GetAccruedCostAsync` (lines 138-158) | exact |
| `sources/Api/DTO/GameServerMetricsDto.cs` and series DTOs | DTO | request-response | `sources/Api/DTO/GameServerAccruedCostDto.cs` | exact |
| `sources/CloudWorker/CloudWorkerChannelClient.cs` | service (outbound client) | streaming | `sources/CloudWorker/CloudWorkerApiClient.cs` + `CloudWorkerRuntime.cs` | partial |
| `sources/CloudWorker/CloudWorkerRuntime.cs` (protocol 3, SupportedGameIdentifiers) | service | event-driven | itself | exact |
| `sources/CloudWorker/WorkerMetricsSampler.cs` | utility | transform | `sources/CloudWorker/WorkerRuntimeStatus.cs` / `Arma3ProcessObservation.cs` | partial |
| `sources/CloudWorker/A2sInfoClient.cs` | utility (UDP) | request-response | `sources/CloudWorker/CounterStrike2RconClient.cs` | role-match |
| `sources/CloudWorker/Arma3JoinMonitor.cs`, `Arma3JoinEvidence.cs`, `Arma3JoinAddonAssets/fn_init.sqf` (human count record) | service | event-driven | themselves | exact |
| `IWorkerCommandExecutor.cs` (+ `ObservePlayersAsync` default method) | interface | request-response | `CompensateSupersededAsync` default method in same file | exact |
| `sources/ReactApp/src/components/server/ServerMetricsPanel.tsx`, `MetricTile.tsx`, `MetricLineChart.tsx`, `MetricTable.tsx`, `metrics.css` | component | request-response + pub-sub | `AccruedCost.tsx`, `CostStrip.tsx`, `FilterChipGroup.tsx` | role-match |
| `sources/ReactApp/src/features/hosting/metricsModel.ts` | utility (pure) | transform | `sources/ReactApp/src/features/hosting/accruedModel.ts` | exact |
| `sources/ReactApp/src/features/hosting/metricsFrames.ts` | utility (parser) | event-driven | `sources/ReactApp/src/features/hosting/costFrames.ts` | exact |
| `sources/ReactApp/src/features/hosting/useServerMetrics.ts` | hook | request-response + pub-sub | `sources/ReactApp/src/features/hosting/useAccruedLive.ts` | exact |
| `FleetLiveUpdatesProvider.tsx`, `services.ts`, `types.ts` (modified) | provider / service | pub-sub | themselves (accrued cost branch) | exact |
| `OverviewSection.tsx`, `sections.css` (modified) | component | composition | themselves | exact |
| `tokens.css`, `locales/en.json`, `de.json`, `identical-allowed.json` | config | config | existing Phase 66 token and key blocks | exact |
| Tests (API, worker, React) | test | - | see Test Patterns | exact / role-match |

## Pattern Assignments

### `ServerMetricBucket.cs` (model, tiered rollup rows)

**Analog:** `sources/Database/Hosting/CostAlert.cs`

**Imports and attributes** (lines 1-24): `System.ComponentModel.DataAnnotations`, `...Schema`, `Microsoft.EntityFrameworkCore`, `X39.UnitedTacticalForces.Database.Meta`; `[Index(..., IsUnique = true)]` on the natural key (it makes the rollup once-only and redelivery idempotent). Use `IPrimaryKey<Guid>`.

```csharp
[Index(nameof(DeploymentFk), nameof(ThresholdAmount), IsUnique = true)]
[Index(nameof(CostOwnerFk), nameof(TimeStampCreated))]
public sealed class CostAlert : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }
    public Guid GameServerFk { get; set; }
```
Every property has a `<summary>`/`<value>` doc. Use `Instant` (NodaTime) for `BucketStart`. Register `public DbSet<ServerMetricBucket> ServerMetricBuckets { get; set; } = null!;` beside `DatabaseContext.cs` line 84-88. Scaffold with the AGENTS.md `dotnet ef migrations add` command; never hand-write. Do not add the entity to `FleetChangeInterceptor` (research anti-pattern).

---

### `ServerMetricsPruneWorker.cs` (hosted service, batch)

**Analog:** `sources/Api/HostedServices/CostAlertWorker.cs` (lines 19-70). Copy: primary-constructor with `IServiceScopeFactory`, `IOptions<...>`, `ILogger<>`; `_options.Validate()` first; loop of `CreateScope` -> work -> `Task.Delay(interval, stoppingToken)`; cancellation `break`; failures logged through `HostingFailureSanitizer.Sanitize(ex.Message)`.

```csharp
catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
catch (Exception ex)
{
    logger.LogError("Cost alert evaluation failed and will retry: {Failure}",
        HostingFailureSanitizer.Sanitize(ex.Message));
}
```
**Registration** (`sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs` lines 25-30): add `services.AddHostedService<ServerMetricsPruneWorker>();` inside the same not-paused block. Options class copies `CostAlertOptions` (with `Validate()`).

---

### `ServerMetricsPublisher.cs` (service, pub-sub to browsers)

**Analog:** `sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs` (lines 26-91 and 171-196)

Copy: `[Scoped<ServerMetricsPublisher, IServerMetricsPublisher>]` attribute (`X39.Util.DependencyInjection.Attributes`); primary constructor with `IUpdateStreamService`, `IHostingAuthorizationService` (use `GetServerViewAudienceAsync` only; no cost owner intersection), `ILogger<>`; fail-closed audience read (catch, one warning with counts, return); per-recipient `SendAsync<T>` that logs ids only and never throws; send to `FleetChangeStreamPaths.ForUser(userId)`.

```csharp
serverAudience = await authorizationService
    .GetServerViewAudienceAsync(ids, cancellationToken).ConfigureAwait(false);
...
await updateStreamService.SendUpdateAsync(FleetChangeStreamPaths.ForUser(userId), message)
    .ConfigureAwait(false);
```
Call the publisher after the ingest transaction commits (same ordering rule as `CostAlertWorker` lines 44-48).

---

### `GameServerMetricsMessage.cs` (browser frame contract)

**Analog:** `sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs` (whole file). Namespace `X39.UnitedTacticalForces.WebSockets.Contracts.Hosting`; flat scalars only (the update stream serializer has no NodaTime or enum converter): `DateTimeOffset`, nullable `double?/long?/int?`, reason codes as `string?`, `required Guid ServerId`. Every property gets `<summary>` and `<value>`. The wire `type` is the full .NET name, which the browser constant must match exactly.

---

### `GameServersController.cs` add `GET {gameServerId}/metrics?range=` (controller, request-response)

**Analog:** same file, lines 138-158.

```csharp
[HttpGet("{gameServerId:guid}/accrued-cost")]
[Authorize(Claims.ResourceBased.Server.View)]
[ProducesResponseType<GameServerAccruedCostDto>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetAccruedCostAsync(
    [FromRoute] Guid gameServerId,
    [FromServices] ICostAccrualService accrualService,
    CancellationToken cancellationToken)
{
    try { var r = await accrualService.GetForServerAsync(gameServerId, User, NodaSystemClock.Instance.GetCurrentInstant(), cancellationToken).ConfigureAwait(false); return Ok(r); }
    catch (Exception ex) when (TryMapException(ex, out var result)) { return result; }
}
```
Use `[FromServices] IServerMetricsService`, `[FromQuery] string range` (validate `1h|24h|7d`, else `BadRequest`), XML-doc with `<param>`/`<returns>`. Regenerate `sources/ReactApp/openapi/utf-api.json` and `src/generated/api.ts` (`npm run contracts:generate`). Do not touch `sources/WebApp/UtfApi`.

---

### `LifecycleWorkersController.cs` add `GET channel` + `WorkerChannelHandler.cs`

**Analog 1 (auth, mapping):** `sources/Api/Controllers/LifecycleWorkersController.cs` lines 14-19 (attributes: `[AllowAnonymous]`, `[EnableRateLimiting(WorkerProtocolRateLimitPolicy.Name)]`, `[ApiExplorerSettings(IgnoreApi = true)]`, route `lifecycle-workers/{workerId:guid}`), lines 202-211 (bearer read, 512-char limit) and 220-231 (exception to outcome mapping).

```csharp
private bool TryReadBearerCredential(out string credential)
{
    credential = string.Empty;
    var authorization = Request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
    credential = authorization[prefix.Length..].Trim();
    return credential.Length is > 0 and <= 512;
}
```
**Analog 2 (accept pattern):** `sources/Api/Controllers/UpdateStreamController.cs` lines 52-58:
```csharp
if (!HttpContext.WebSockets.IsWebSocketRequest) return BadRequest();
using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
await _updateStreamService.RegisterAsync(subscriptionPath, webSocket, HttpContext.RequestAborted);
return new EmptyResult();
```
Keep the controller action a three-line shim and put the socket loop in `WorkerChannelHandler.RunAsync(workerId, credential, WebSocket, ct)` (testable with `WebSocket.CreateFromStream`). Map `WorkerProtocolAuthenticationException` -> close 1008, `WorkerProtocolConflictException` -> `Superseded` ack, `WorkerProtocolValidationException` -> `Rejected`, `WorkerProtocolNotFoundException` -> `Rejected`. The handler calls `IWorkerProtocolService` unchanged, per frame, with the held credential (see `LifecycleWorkersController` lines 72-118 for each method's argument shape). Per-socket single writer: copy the `SemaphoreSlim sendLock` per socket approach from `UpdateStreamService.cs` (lines 29-37, 52, 94-120). Never log frame bodies (commands carry secrets).

---

### `WorkerChannelRegistry` / `WorkerCommandDispatcher` / `IWorkerCommandSignal` (in-process notify)

**Analog:** `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs` and `FleetChangeInterceptor.cs` (process-local signal after commit; documented single-instance limitation). Register with `[Singleton<...>]` like `UpdateStreamService`. The research (Pitfall 4) requires reading `AutomaticCostStopService.cs` and `LifecycleJobExecutor` command-commit sites before wiring `Notify(workerId)`.

---

### `WorkerMetricsSample.cs` and channel contracts (HostingContracts)

**Analog:** `sources/HostingContracts/WorkerProtocol/WorkerHeartbeatRequest.cs` lines 1-50: `sealed record`, `IValidatableObject`, data annotations (`[MaxLength]`, `[Range]`), `yield return new ValidationResult(msg, [nameof(...)])` for cross-field rules (value xor reason; used <= total). Enums use `E` prefix (`EWorkerWorkloadState.cs` is the sibling). Add XML docs to every member (the heartbeat record is missing some; do not copy that gap). Reason-code constants: mirror `AccruedCostUnavailableReasons.cs` (static class of string constants).

---

### `CloudWorkerChannelClient.cs` (worker, outbound streaming)

**Analog (partial):** `sources/CloudWorker/CloudWorkerApiClient.cs` (request construction, `CloudWorkerJson.Options`, `EnsureSuccessAsync`, never log secrets) and `CloudWorkerRuntime.cs` (credential store use lines 20-75, heartbeat build lines 407-427, rotate then continue, report journal and `ReportAsync` returning false on 409). Replace the poll loop with `ClientWebSocket`, `Options.SetRequestHeader("Authorization", "Bearer " + credential)`, `KeepAliveInterval`, and `KeepAliveTimeout` (set explicitly; default is infinite). Single writer `Channel<Frame>` plus a receiver completing `TaskCompletionSource` by frame id. Heartbeat from a timer independent of command execution. Registration change at `CloudWorkerRuntime.cs` lines 38-39:

```csharp
ProtocolVersion = 3,
SupportedGameIdentifiers = ["arma3", "cs2", "aska"],   // or derive from WorkerGameExecutorRegistry
```
Heartbeat payload excerpt to reuse (lines 407-427): `WorkerHeartbeatRequest { DeploymentId, Stage, ProcessId, ProcessStartedAt, ... Workloads }` built from `commandExecutor.ObserveRuntimeAsync`. Keep `register` and `rotate-credential` on HTTP; close and reopen the socket after rotate.

---

### `IWorkerCommandExecutor.ObservePlayersAsync` (interface default method)

**Analog:** `sources/CloudWorker/IWorkerCommandExecutor.cs` lines 17-24 (`CompensateSupersededAsync` default method with `=> Task.CompletedTask` and an XML remark about source compatibility). Copy the shape: default returns an "unavailable, `game-does-not-report`" result so Aska and any future game compile.

---

### `A2sInfoClient.cs` (worker utility, UDP request-response)

**Analog (role-match):** `sources/CloudWorker/CounterStrike2RconClient.cs` (loopback client behind interface `ICounterStrike2RconClient`, bounded timeout, no exceptions leaking secrets). Create `IA2sInfoClient` plus implementation so tests inject a fake UDP responder. Byte layout: see 67-RESEARCH.md "A2S_INFO request and response". Timeout or malformed reply returns null with `query-failed`, never zero. Humans = `max(0, players - bots)` (assumption A3, unit-test it).

---

### Arma 3 human-count record (`Arma3JoinMonitor.cs`, `Arma3JoinEvidence.cs`, `fn_init.sqf`)

**Analog:** the same files. Add a second bounded record kind (`UTFPL|1|<nonce>|<seq>|<humans>`) reusing nonce, tail-read and sequence rules; exclude headless via `getUserInfo` index 7 as `fn_init.sqf` lines 22-26 already do. Extend `tests/CloudWorker.Tests/Arma3JoinMonitorTests.cs` and `Arma3JoinAddonTests.cs`; do not weaken existing readiness cases.

---

### `WorkerMetricsSampler.cs` (worker utility, transform)

**Analog (partial):** `sources/CloudWorker/WorkerRuntimeStatus.cs` for the nullable-value report shape. Pure functions over injected text readers (so tests feed fixture strings): CPU from `/proc/stat` delta (first call returns null + `measuring`), memory from cgroup v2 then `/proc/meminfo`, non-Linux null + `unsupported-platform`. Never 0 for unmeasured.

---

### React: `metricsFrames.ts`

**Analog:** `sources/ReactApp/src/features/hosting/costFrames.ts` lines 1-107. Copy: exported full-.NET-type-name constant, `isText`/`isInstant` helpers, `parse...Frame(frame: UpdateStreamFrame)` returning `{ serverId, ... } | null`, strict check of every field, null-or-number validation, ignore `$id`, drop on any failure.

```ts
export const accruedCostMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerAccruedCostMessage";
export function parseAccruedCostFrame(frame: UpdateStreamFrame): ParsedAccruedCostFrame | null {
  if (frame.type !== accruedCostMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  ...
```
Test analog: `tests/hosting/cost-frames.test.ts`.

### React: `useServerMetrics.ts`

**Analog:** `sources/ReactApp/src/features/hosting/useAccruedLive.ts` lines 23-38: `useQuery` with key from `hostingQueryKeys`, `enabled: serverId !== ""`, `staleTime: Infinity`, `refetchOnWindowFocus: false`, no `refetchInterval`, keep newer cached value. Add keys in `services.ts` beside line 176 (`accruedCost: (serverId) => ["hosting","servers",serverId,"accrued-cost"] as const`) and a service method beside line 242. The range series query key must include server id and range.

### `FleetLiveUpdatesProvider.tsx` (modify)

**Analog:** its own `onMessage` (lines 149-159): add a branch before the alert branches:

```ts
const accrued = parseAccruedCostFrame(frame);
if (accrued !== null) {
  queryClient.setQueryData<GameServerAccruedCost>(
    hostingQueryKeys.accruedCost(accrued.serverId),
    (current) => (isNewerAccrued(accrued.accrued, current) ? accrued.accrued : current),
  );
  return;
}
```
Also the reconnect handling at lines 211-229 (`onOpen`, invalidate on reopen): add one refetch of current readings and the active range there; throttle range refetch from frames to the bucket interval without a timer.

### React: `metricsModel.ts`

**Analog:** `sources/ReactApp/src/features/hosting/accruedModel.ts` (pure, framework-free, `Map` of codes to i18n keys, presentation function from `(data, serverState, loading)`, `isNewer...` helper). Put `buildSegments` (see RESEARCH "Gap-breaking path builder"), nice ticks, highest selection, reason-code `Map` with fixed fallback key `Server_Metrics_Reason_Unknown`. Test analog: `tests/hosting/accrued-model.test.ts`.

### React: `ServerMetricsPanel.tsx`, `MetricTile.tsx`

**Analogs:** `components/server/AccruedCost.tsx` (lines 23-72): `useTranslation`, presentation hook, `useDelayedDisconnect()` plus `Warning16Regular aria-hidden` and `t("Server_Live_StaleReason_Disconnected")`, `<time dateTime>` with `formatInstant` from `@/features/hosting/costAlertFormat`, `import "@/components/server/workspace-shell.css"`. `CostStrip.tsx` is the precedent for a panel replacing a `SlotPlaceholder` and for `data-slot`. Range switcher: `FilterChipGroup` (`@/components/FilterChipGroup`, props `label`, `items`, `value`, `onChange`; no counts). View toggle: `ActionButton variant="ghost"` from `@/components/ActionControl`. Loading and errors: `StatePanel` from `@/components/primitives`. Clock: `useNow` from `@/app/useNow` only.

### React: `OverviewSection.tsx` and `sections.css` (modify)

**Analog:** itself. Replace lines 88-90:

```tsx
<SlotPlaceholder variant="panel" slot="metrics" title={t("Server_Overview_MetricsTitle")}>
  {t("Server_Overview_MetricsUnavailable")}
</SlotPlaceholder>
```
with `<ServerMetricsPanel server={server} />` placed after the details panel; `data-slot="metrics"` moves onto the new `section.workspace-panel`. Remove the `2fr 1fr` rule in `sections.css` (lines 89-91 inside a media query; line 53-58 is the one-column base). `SlotPlaceholder` import stays only if still used by another slot in this file (it is not; drop the import).

### Tokens, locales, CSS guard

New tokens go only in `src/styles/tokens.css` (near `--utf-workspace-fact-min` line 33 and `--utf-output-max-height` line 35): `--utf-metric-chart-min`, `--utf-metric-chart-height`, `--utf-metric-tooltip-width`, `--utf-chart-line`, `--utf-chart-band`, `--utf-chart-grid`. Extend `tests/styles/tokens-contrast.test.ts` for the chart pair. `metrics.css` is scanned by `tests/styles/server-style-guard.test.ts`; tokens only, no literals. Keys are flat `Server_Metrics_*` in `src/locales/en.json` and `de.json` with parity checked by `scripts/check-locales.mjs`; identical-value keys (`Server_Metrics_Tile_Cpu`, `Server_Metrics_Memory_Value`) need entries with reasons in `src/locales/identical-allowed.json`. Regenerate `openapi/utf-api.json` and `src/generated/api.ts`.

## Shared Patterns

### Fail-closed audience publishing
**Source:** `sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs` lines 42-65 and 179-196. **Apply to:** `ServerMetricsPublisher`. Audience read failure drops the whole batch with one warning; per-send failure logs ids only.

### Authorization on REST read
**Source:** `GameServersController.cs` line 139 `[Authorize(Claims.ResourceBased.Server.View)]`. **Apply to:** metrics endpoint. Returns 404 for unknown or non-viewable servers.

### Worker credential and exception mapping
**Source:** `LifecycleWorkersController.cs` lines 202-231. **Apply to:** `WorkerChannelHandler`. Credential never in query string, never logged.

### Controller exception mapping in GameServersController
**Source:** `GameServersController.cs` lines 154-157 (`catch (Exception ex) when (TryMapException(ex, out var result))`). **Apply to:** metrics endpoint.

### Background service loop
**Source:** `CostAlertWorker.cs`. **Apply to:** prune worker (and any dispatcher safety sweep); register only when the control plane is not paused.

### Browser live-data rule
**Source:** `useAccruedLive.ts` + `FleetLiveUpdatesProvider.tsx` lines 149-159. **Apply to:** all metrics React code: REST for first value, frames via `setQueryData`, no `refetchInterval`, no raw `setInterval`, no second socket, values written as received.

### Handwritten source rules (AGENTS.md)
**Apply to:** every C# file. English identifiers/docs; `E` prefix on every enum; XML docs with `<summary>`, `<param>`, `<returns>`, `<value>`, `<exception>` on every type and member; one top-level type per file; file-scoped namespaces; trailing commas; 120-column lines; migrations only via `dotnet ef migrations add`.

## Test Patterns

| New test | Analog | Notes |
|----------|--------|-------|
| `tests/Api.Tests/GameServerHosting/ServerMetricsPublisherTests.cs` | `CostAlertPublisherTests.cs` (lines 1-50: `HostingTestStore.CreateAsync()`, `HostingTestData.SeedServerAsync`, `GrantAsync`, hand-written `RecordingUpdateStream`) | No Moq. Real EF over SQLite in-memory. |
| `ServerMetricIngestTests`, `ServerMetricsPruneTests`, `ServerMetricsReadModelTests`, `ServerMetricsAvailabilityTests` | `AccruedCostEndpointTests.cs`, `CostAlertEvaluatorTests.cs` | Rollup exactness, duplicate idempotent, property-style state product. |
| `WorkerChannelHandlerTests` | no direct analog | Use `WebSocket.CreateFromStream` over a duplex in-memory pair (no TestHost in `Api.Tests`) against `HostingTestStore`. |
| `tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs`, `A2sInfoClientTests.cs`, `WorkerMetricsSamplerTests.cs` | `CloudWorkerRuntimeTests.cs`, `CloudWorkerApiClientTests.cs`, `WorkerHeartbeatRequestTests.cs` | Hand-written fakes; fixture strings for `/proc` files; in-process fake UDP responder. |
| `sources/ReactApp/tests/hosting/metrics-frames.test.ts`, `metrics-model.test.ts`, `metrics-panel.test.tsx`, `metrics-source-scan.test.ts` | `cost-frames.test.ts`, `accrued-model.test.ts`, `accrued-cost.test.tsx`, `cost-alerts-source-scan.test.ts` | Source scan asserts no `refetchInterval`, raw `setInterval`, chart library import, unchanged dependency list. |

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `CloudWorkerChannelClient.cs` | outbound client | streaming | The worker has no WebSocket client; only HTTP `HttpClient`. Use RESEARCH Pattern 2 (single writer, correlated acks). |
| `WorkerChannelHandler.cs` / `WorkerChannelRegistry.cs` | service | request-response over socket | `UpdateStreamService` is a browser fan-out, not request/response with one connection per worker. Use RESEARCH Pattern 1 and Pitfalls 4, 5, 10, 11. |
| `MetricLineChart.tsx`, `MetricTable.tsx` | component | transform | ReactApp has no chart. Use UI-SPEC plot geometry and RESEARCH "Chart decision"; structure the component with `ActivityTimeline.tsx` / `FilterChipGroup.tsx` style conventions for CSS and a11y. |
| `A2sInfoClient.cs` wire parsing | utility | request-response | No UDP query code exists (RCON client only sends `quit`). Use RESEARCH A2S code example. |

## Notes For The Planner

- Wave order from research: transport adapter (protocol 3, HTTP kept for versions 1-2), then ingestion, storage and prune, then samplers and per-game player sources, then React panel. Keep `IWorkerProtocolService` untouched.
- Pre-existing defect on the rewritten path: `CloudWorkerRuntime.cs` line 39 registers only `["arma3","cs2"]`, so ASKA workers are rejected (`WorkerProtocolService.cs` lines 72-74). Fix in the protocol 3 change.
- `GameServerDeployment.TimeStampStarted` is never written; `runningSince` must come from the primary worker's `ProcessStartedAt`.

## Metadata

**Analog search scope:** `sources/Api` (Controllers, HostedServices, Services/GameServerLifecycle, Services/WorkerProtocol, Services/UpdateStreamService), `sources/Database/Hosting`, `sources/HostingContracts/WorkerProtocol`, `sources/WebSocketContracts`, `sources/CloudWorker`, `sources/ReactApp/src/{features/hosting,components/server,components}`, `tests/Api.Tests/GameServerHosting`, `tests/CloudWorker.Tests`, `sources/ReactApp/tests`
**Files scanned:** about 45 (read or grepped)
**Pattern extraction date:** 2026-11-21

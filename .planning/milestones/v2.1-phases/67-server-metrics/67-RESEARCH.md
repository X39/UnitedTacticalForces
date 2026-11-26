# Phase 67: Server Metrics - Research

**Researched:** 2026-11-21
**Domain:** Worker-to-control-plane WebSocket transport (ASP.NET Core 10 / .NET 10 worker), time-series retention in PostgreSQL via EF Core, game player-count sources (Arma 3 / CS2 / ASKA), accessible SVG charts in React 19
**Confidence:** HIGH for in-repo architecture and the transport rewrite surface (all read this session); MEDIUM for A2S/player-count behavior per game (docs and library source, not exercised against live servers); LOW for ASKA player data (no source found)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Worker transport (MET-05)**
- **D-01:** The worker connects to the control plane over a **persistent, worker-authenticated outbound WebSocket**, not
  polling. Metrics are pushed over it. The worker protocol today is entirely HTTP POST (register, heartbeat,
  `commands/claim`, received/running/succeeded/failed reports); there is no worker WebSocket yet.
- **D-02:** **Full channel scope (user decision):** the socket is the worker's full channel, so command delivery,
  command reports and heartbeat/liveness move onto it as well, not only metrics. The user chose this over "metrics only"
  after being offered the narrower options. It is a protocol rewrite and keeps the existing guarantees: deployment-scoped
  credentials, one worker per deployment, durable command journal and redelivery, idempotent reports, API restart
  recovery. — **Reversibility:** costly — every worker command and report path and the worker health logic depend on it.
  Planner: this is the largest part of the phase; consider splitting it into its own plan wave (transport first, metrics
  on top) and decide at plan time whether the HTTP endpoints stay as a fallback or are removed.
- **D-03:** Metrics reach the control plane **only** through this worker channel (MET-05, roadmap criterion 5). Provider
  provisioning, observation and teardown stay independent of the worker and of this socket.

**History storage and retention (MET-04)**
- **D-04:** History lives in a **PostgreSQL table** (durable across API restarts and reloads), added through a scaffolded
  `dotnet ef migrations add` migration. — **Reversibility:** costly — schema plus data migration.
- **D-05:** Retain **7 days, downsampled**. The tiering (raw interval, rollup resolutions, whether to store min/avg/max)
  is Claude's discretion; spikes should survive downsampling, and pruning must be a durable background job.

**Players per game (MET-02)**
- **D-06:** Show a real player count for **all three games where possible**: Arma 3 (existing join monitor), Counter-Strike 2
  (existing RCON client) and Aska if its process exposes a reliable source. Any game or moment where the count cannot be
  read shows "unavailable". Player count is reported by the worker through the game adapters; the API never queries the
  game.

**Overview display (MET-01, MET-03, MET-04)**
- **D-07:** **Tiles plus charts** in the Overview metrics slot: current-value tiles (uptime as "running since", players,
  CPU, memory) with a full chart per metric below and a range switcher (1 hour, 24 hours, 7 days).
- **D-08:** Chart implementation (hand-built accessible SVG vs an exact-pinned library with a supply-chain checkpoint) is
  Claude's discretion. ReactApp has no chart library today; charts must use `--utf-*` tokens, be keyboard/screen-reader
  accessible, and be localized in EN and DE.

**Unavailable and stale rules (MET-05)**
- **D-09:** A metric is **unavailable** when the server is stopped, the worker is offline, the sample is stale, or the game
  does not report it. Tiles show "Unavailable" with a plain reason (stopped / worker offline / game does not report
  players). Charts **break the line at gaps** and never interpolate or draw zero. A sample older than about three report
  intervals counts as stale. Exact interval and threshold are Claude's discretion.

### Claude's Discretion
- Downsampling tiers and sampling interval (D-05).
- Chart implementation (D-08).
- Exact stale threshold and unavailable-reason wording (D-09), following Phase 61 UI-SPEC copy rules.
- Whether the browser receives metric updates live (reuse the fleet/workspace socket) or by query refresh.

### Deferred Ideas (OUT OF SCOPE)
- Disk and network metrics, per-member metric visibility rules, and metric alerting were not discussed; revisit only if requested.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MET-01 | User sees uptime (running since) for a running server | The deployment field `TimeStampStarted` is exposed in `GameServerDeploymentDto` but **nothing in `sources/Api` writes it** (grep: no hosting write site), so it cannot be the source. Use the worker-observed game-process start (`WorkerHeartbeatRequest.ProcessStartedAt` / `LifecycleWorker.ProcessStartedAt`, already persisted every heartbeat) of the primary host as `runningSince`. No new protocol field is required for MET-01 itself. |
| MET-02 | User sees current player count where the game exposes it | New worker-side `ObservePlayersAsync` seam per game executor. Arma 3: extend the existing SQF join addon with an engine-observed human count. CS2: A2S_INFO over loopback UDP (the existing RCON client only sends `quit`, drops response bodies, and the RCON password exists only in command secrets). ASKA: probe A2S on the query port; otherwise an explicit "game does not report players". |
| MET-03 | User sees CPU and memory usage of the server machine | New worker-side sampler: CPU from `/proc/stat` deltas, memory from cgroup v2 / `/proc/meminfo`; non-Linux or unreadable returns a null value plus a reason code, never zero. |
| MET-04 | User sees a short retained history of these metrics as charts | `ServerMetricBucket` table with streaming 15 s / 60 s / 300 s tiers (min/avg/max/count), durable prune worker, REST `GET game-servers/{id}/metrics?range=`, hand-built SVG charts that break at gaps, survive reload because history comes from REST. |
| MET-05 | Metrics arrive through the worker protocol; unavailable shown as unavailable, never fake | Metrics frame on the new worker WebSocket channel only; API evaluates availability (server state, worker liveness, staleness, game capability) and sends reason codes; browser maps codes to EN/DE copy. |
</phase_requirements>

## Project Constraints (from CLAUDE.md / AGENTS.md)

`./CLAUDE.md` does not exist; `AGENTS.md` is authoritative and was provided in the task context. No project skills exist (`.claude/skills`, `.agents/skills` absent per AGENTS.md). Directives the plan must satisfy:

- All identifiers, comments, XML docs and technical docs in English; localized end-user text in resource files (ReactApp: flat `Server_{Area}_{Item}` keys in `en.json` and `de.json`; API: `.resx`).
- Every enum type is named `E<PascalCase>`. Every C# type and member gets meaningful XML documentation (`<summary>` plus `<param>`, `<returns>`, `<value>`, `<exception>`). One top-level type per file named after the type. Generated code (EF migration designers, `src/generated/api.ts`, Kiota) is exempt and never hand-edited.
- Architecture: API never executes or samples a game process; worker protocol contracts live in `sources/HostingContracts`; WebSocket wire messages live only in `sources/WebSocketContracts`; do not touch the legacy Blazor WebApp (the `sources/WebApp` Kiota client) unless an API change would break its compatibility.
- API, DTO, endpoint, database changes the feature needs are in scope without approval; put each change in the layer that owns the data; regenerate the OpenAPI snapshot (`sources/ReactApp/openapi/utf-api.json`) and generated types.
- EF migrations only via `dotnet ef migrations add <Name> --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations`; never hand-create the migration, designer, or snapshot.
- Commit cohesive units; subject format `type(scope): summary` under 80 characters; `!` plus a `BREAKING CHANGE:` body for incompatible public contract changes (the worker protocol change is a breaking contract change if the HTTP endpoints are removed).
- Security: worker credentials, Steam credentials, provider tokens must not reach frontend clients or unredacted logs.
- Cross-cutting or server-wide features need an explicit user request: the worker WebSocket channel is explicitly requested (D-01/D-02), so it is in scope; do not expand it beyond D-02 (for example no new game-server-wide features).
- Testing focus per AGENTS.md: worker authentication and redelivery, persistence, job transitions, authorization-sensitive actions, recovery. Test doubles are hand-written (no Moq); `DatabaseContext` is real EF over SQLite in-memory (`HostingTestStore`).
- Subagent delegation is explicitly allowed by AGENTS.md for independent exploration and validation.

## Summary

Phase 67 is two very different pieces of work stacked on each other. The small, well-understood piece is the metrics feature: a worker-side sampler, a persisted tiered history, a REST read model, a push frame to the browser, and an Overview panel with four tiles and three charts. The large, risky piece is D-02: replacing the worker's HTTP pull protocol (heartbeat, `commands/claim`, four report endpoints) with one persistent outbound WebSocket that also carries command delivery, reports, heartbeats and metrics. The existing protocol has precise semantics (lease-based claim ordering, redelivery of `Received`/`Running` commands first, idempotent `409`-means-superseded reports, per-frame credential verification, `LastHeartbeatAt` driving health reconciliation and fault cost-stop) that the socket must reproduce exactly. The recommended approach keeps `IWorkerProtocolService` and its transactional logic untouched and puts a thin socket adapter in front of it, so the rewrite is a transport swap and not a re-implementation of the guarantees.

Several CONTEXT assumptions do not hold in the code and the planner must plan around them. (1) The "existing join monitor" emits only Headless Client readiness, not a player count; the "existing RCON client" only issues `quit`, discards response bodies, and the CS2 RCON password exists only inside command secrets, so neither can produce a player count as-is. (2) `GameServerDeployment.TimeStampStarted` is never written, so uptime must come from the worker's observed process start. (3) A deployment can have several workers (Arma 3 headless-client hosts are separate workers with their own service identity), so metrics must be attributed to the primary host (`game-server`, ordinal 0). (4) The worker registers with `SupportedGameIdentifiers = ["arma3","cs2"]` and the API rejects a mismatch, so an ASKA worker cannot currently register at all (pre-existing defect on the same code path this phase rewrites).

**Primary recommendation:** Split into waves: (1) worker WebSocket channel as a transport adapter over the unchanged `IWorkerProtocolService`, protocol version 3, HTTP endpoints kept for protocol 1-2 workers during the transition; (2) metrics ingestion, tiered storage, prune worker, REST and browser frame; (3) worker samplers and per-game player sources; (4) React Overview panel with hand-built SVG charts. Use A2S_INFO for CS2 and (after a spike) ASKA, an SQF-addon human count for Arma 3, and report every unmeasurable value as `null` plus a reason code.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Measure CPU and memory of the server machine | Worker (`sources/CloudWorker`) | — | Only the worker runs on the machine; API must never sample (AGENTS.md, MET-05). |
| Measure player count | Worker game adapters | — | D-06: the API never queries a game; loopback A2S/log evidence is only reachable from the worker. |
| Transport of metrics, commands, reports, heartbeats | API worker channel (`sources/Api/Services/WorkerProtocol`) + worker client | HostingContracts (wire types) | D-01/D-02; contracts shared by both sides live in `sources/HostingContracts/WorkerProtocol`. |
| Validate, attribute, persist samples; compute rollups | API service over `DatabaseContext` | Database project (entity) | Control plane owns durable truth; attribution to the authenticated worker's deployment, never the payload. |
| Retention and pruning | API hosted service (`BackgroundService`) | Database | Durable, restart-safe job; registered only when the control plane is not paused, like the other workers. |
| Availability rules (stopped, worker offline, stale, game cannot report) | API read model | Browser (stale-over-time display using a server-sent threshold) | The API knows deployment and worker state; the browser only re-checks age against the threshold the API sent. |
| Authorization of metric reads and pushes | API (`Claims.ResourceBased.Server.View` + `IFleetAudienceSource`) | — | Same audience rule as the fleet and accrued cost; no new visibility rules (deferred). |
| Live push to browsers | API update stream (per-user path) | Browser fleet socket provider | Reuses the Phase 62/66 socket and `FleetChangeStreamPaths.ForUser`; no second browser socket. |
| Charts, tiles, range switch, EN/DE copy | Browser (`sources/ReactApp`) | — | Presentation only; browser never samples, extrapolates or invents values. |

## Standard Stack

### Core

No new NuGet or npm package is recommended. Every capability maps to a framework or an existing dependency.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core WebSockets (`Microsoft.AspNetCore.WebSockets`, in the shared framework) | 10.0.x (repo pins 10.0.9) | Server side of the worker channel; `WebSocketAcceptContext.KeepAliveTimeout` per accepted socket | Already enabled globally via `app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(25) })` at `sources/Api/Program.cs:104`. [VERIFIED: sources/Api/Program.cs:104] |
| `System.Net.WebSockets.ClientWebSocket` (shared framework) | .NET 10 | Worker outbound client; `Options.SetRequestHeader` for the bearer credential, `KeepAliveInterval`, `KeepAliveTimeout` | `KeepAliveTimeout` exists on .NET 9/10, default `InfiniteTimeSpan` (disabled), so the worker must set it explicitly. [CITED: learn.microsoft.com/dotnet/api/system.net.websockets.clientwebsocketoptions.keepalivetimeout] |
| EF Core + Npgsql (existing) | 10.0.9 / 10.0.2 | New `ServerMetricBucket` entity and migration | Existing data layer; migration must be scaffolded. [VERIFIED: AGENTS.md technology stack] |
| `System.Text.Json` (framework) | .NET 10 | Frame serialization on both ends | Already the serializer of the worker protocol (`CloudWorkerJson.Options`, `JsonSerializerDefaults.Web`). |
| xUnit 2.9.3, SQLite in-memory `HostingTestStore`, Vitest 4.1.10 | existing | Tests | Existing infrastructure. [VERIFIED: .planning/codebase/TESTING.md] |
| Hand-built SVG chart components (React 19.2.8, no library) | — | Line chart with min-max band, gap breaks, keyboard data table | See "Chart decision" below. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `date-fns` | 4.1.0 (already a dependency) | Axis and tooltip time formatting | Use the existing app formatters first (`formatInstant`, `formatDate` in `features/hosting`). |
| `@tanstack/react-query` | 5.101.4 (existing) | REST read of metrics and cache updates from frames | Same pattern as `accruedCost` query plus `setQueryData` from frames. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-built SVG charts | A pinned chart library (Recharts, uPlot, visx) | Adds a supply-chain checkpoint, bundle weight and styling that must be forced onto `--utf-*` tokens and a11y requirements; the need is three simple single-series line charts with gap breaks. Recommend hand-built. |
| A2S_INFO for CS2 | RCON `status` | RCON password lives only in per-command secrets (the worker never persists it; do not start); the shipped client discards bodies; sources report CS2 RCON as limited/unreliable. A2S needs no secret. |
| Streaming rollups on the write path | Query-time `date_bin` over raw rows | `date_bin` is PostgreSQL-only and the whole suite runs on SQLite; raw 7-day rows also contradict D-05 "downsampled". |
| Keep HTTP endpoints as fallback | Remove them in this phase | Removing orphans every worker already running an older container image (see Pitfall 1). Keep for protocol 1-2, mark deprecated, remove in a later phase. |

**Installation:** none. (No `npm install`, no `dotnet add package`.)

**Version verification:** No new packages are introduced, so no registry lookups were required. `dotnet --version` reports 10.0.201 and `dotnet ef --version` reports 10.0.9, which matches the repository's EF Core 10.0.9. [VERIFIED: local probe this session]

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | No external package is added by this phase |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

Adding a chart library later would need the `package-legitimacy check` gate and a `checkpoint:human-verify` before install; the recommendation here avoids it.

## Architecture Patterns

### System Architecture Diagram

```
                        CLOUD VM (one per deployment host)
  +-----------------------------------------------------------------------------+
  |  CloudWorker container                                                      |
  |   +-------------------+   +--------------------+   +--------------------+   |
  |   | MetricsSampler    |   | Game adapter       |   | Command executor   |   |
  |   | /proc/stat        |   | ObservePlayers     |   | (existing, journal |   |
  |   | cgroup/meminfo    |   | A2S / SQF evidence |   |  + redelivery)     |   |
  |   +---------+---------+   +----------+---------+   +---------+----------+   |
  |             |  null+reason, never 0  |                       |              |
  |             v                        v                       v              |
  |        +----------------------- WorkerChannelClient ----------------+       |
  |        | one WebSocket, single writer queue, correlated acks,       |       |
  |        | heartbeat timer, backoff reconnect, credential re-open     |       |
  |        +------------------------+-----------------------------------+       |
  +---------------------------------|-------------------------------------------+
                                    | outbound WSS, Authorization: Bearer <worker credential>
                                    v
  +-----------------------------------------------------------------------------+
  | API (control plane)                                                         |
  |  GET lifecycle-workers/{workerId}/channel  (rate limited, anonymous+bearer) |
  |        |                                                                    |
  |        v                                                                    |
  |  WorkerChannelHandler --per frame--> IWorkerProtocolService (UNCHANGED:     |
  |   - frame size cap, type switch        Heartbeat / ClaimCommand / Report*)  |
  |   - ack with outcome (accepted /                                            |
  |     superseded / rejected)             WorkerMetricsService.IngestAsync     |
  |   - WorkerChannelRegistry (1 conn          validate, attribute to worker,   |
  |     per worker, newest wins)               upsert 15s/60s/300s buckets      |
  |        ^                                          |                         |
  |        | push command / superseded                v                         |
  |  WorkerCommandDispatcher <--Notify(workerId)-- LifecycleJobExecutor         |
  |   (claim via same ClaimCommandAsync,         (after WorkerCommand commit)   |
  |    on connect, on notify, on terminal ack,                                  |
  |    low-rate safety sweep)                                                   |
  |                                                                             |
  |  ServerMetricsPrunerWorker (BackgroundService) --delete by tier age         |
  |  ServerMetricsPublisher --audience (IFleetAudienceSource)--> update stream  |
  +------------------+-------------------------------+--------------------------+
                     | PostgreSQL                    | per-user path game-servers/fleet/{userId}
                     v                               v
        ServerMetricBuckets (tiers)          Browser: FleetLiveUpdatesProvider
                     ^                               | frame -> setQueryData(metrics)
                     | REST GET game-servers/{id}/metrics?range=1h|24h|7d
                     +-------------------------------+--> OverviewSection metrics panel
                                                          tiles + 3 SVG charts + data table
```

### Recommended Project Structure

```
sources/
├── HostingContracts/WorkerProtocol/
│   ├── WorkerChannelFrame.cs            # envelope: Type, Id, Payload (JsonElement)
│   ├── EWorkerChannelFrameType.cs       # E-prefixed enum (AGENTS.md)
│   ├── WorkerChannelAck.cs              # Id, Outcome, ErrorMessage
│   ├── EWorkerChannelOutcome.cs         # Accepted, Superseded, Rejected
│   ├── WorkerMetricsSample.cs           # nullable values + reason codes
│   └── WorkerMetricUnavailableReasons.cs # string constants shared by both ends
├── WebSocketContracts/Hosting/
│   └── GameServerMetricsMessage.cs      # browser frame: flat scalars, no Noda/enums
├── Database/Hosting/
│   └── ServerMetricBucket.cs            # + DbSet + scaffolded migration
├── Api/Services/WorkerProtocol/
│   ├── WorkerChannelHandler.cs          # socket loop, frame dispatch, size caps
│   ├── WorkerChannelRegistry.cs         # worker id -> live connection, newest wins
│   ├── WorkerCommandDispatcher.cs       # push claimed commands
│   └── IWorkerCommandSignal.cs          # Notify(workerId) called after command commit
├── Api/Services/ServerMetrics/
│   ├── ServerMetricsService.cs          # ingest + read model + availability evaluation
│   ├── ServerMetricsPublisher.cs        # audience-resolved browser frames
│   └── ServerMetricsOptions.cs          # interval, stale factor, tier retention
├── Api/HostedServices/
│   └── ServerMetricsPruneWorker.cs      # registered in AddHostingControlPlane (not when paused)
├── Api/Controllers/
│   ├── LifecycleWorkersController.cs    # add GET channel; keep POST endpoints
│   └── GameServersController.cs         # add GET {id}/metrics (View claim)
├── CloudWorker/
│   ├── CloudWorkerChannelClient.cs      # replaces the poll loop for protocol 3
│   ├── WorkerMetricsSampler.cs          # CPU/memory
│   ├── A2sInfoClient.cs                 # UDP, challenge handling
│   └── (game executors: ObservePlayersAsync default interface method)
└── ReactApp/src/
    ├── components/server/ServerMetrics*.tsx   # tiles, chart, range switch, table
    ├── components/server/metrics.css
    └── features/hosting/metricsModel.ts, metricsFrames.ts, useMetrics.ts
```

### Pattern 1: Transport adapter over the unchanged protocol service (recommended for D-02)

**What:** `WorkerChannelHandler` authenticates once at upgrade (bearer from the header, same 512-character limit and `TryReadBearerCredential` rule as the controller), then for each inbound frame calls the existing `IWorkerProtocolService` method (`HeartbeatAsync`, `ReportReceivedAsync`, ...) with the held credential, and answers with a correlated ack. Each frame re-verifies the credential inside the service (that is what `AuthenticateWorkerAsync` already does per call), so a revoked or rotated credential stops working on the next frame. Exceptions map to ack outcomes using the same mapping as `LifecycleWorkersController.TryMapException` (`WorkerProtocolAuthenticationException` -> close 1008, `WorkerProtocolConflictException` -> `Superseded`, validation -> `Rejected`).
**When to use:** Always for D-02. It keeps every transactional guarantee in one place and makes the old and new transports share tests.
**Example:**

```csharp
// Source: pattern composed from sources/Api/Controllers/UpdateStreamController.cs (accept + loop)
// and ASP.NET Core WebSockets docs (per-socket KeepAliveTimeout).
[HttpGet("channel")]
public async Task ChannelAsync([FromRoute] Guid workerId, CancellationToken cancellationToken)
{
    if (!TryReadBearerCredential(out var credential)) { Response.StatusCode = 401; return; }
    if (!HttpContext.WebSockets.IsWebSocketRequest) { Response.StatusCode = 400; return; }
    using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync(
        new WebSocketAcceptContext { KeepAliveTimeout = TimeSpan.FromSeconds(30) });
    await channelHandler.RunAsync(workerId, credential, socket, cancellationToken);
}
```

### Pattern 2: One writer, correlated acks (worker side)

**What:** A `WebSocket` allows one outstanding send and one outstanding receive. The worker owns a `Channel<Frame>` drained by a single sender task; a single receiver task completes pending requests by `Id` (`TaskCompletionSource` map) and dispatches server pushes. Heartbeat and metrics are timer tasks that enqueue frames independently of command execution, so liveness continues during a multi-minute SteamCMD install (the old loop only heartbeated from inside the execution wait loop). Command reports await their ack because the current `ReportAsync` returns `false` on 409 and callers branch on it (`RecordSupersededAndCompensateAsync`).
**When to use:** The worker client. Mirrors the per-socket send lock in `UpdateStreamService`.

### Pattern 3: Streaming tiered rollups (history, MET-04)

**What:** On each accepted sample, in one transaction: (a) insert the 15 s bucket row (unique key `(GameServerFk, ResolutionSeconds, BucketStart)`; a duplicate means redelivery and the sample is skipped entirely, including rollups); (b) read-modify-write the 60 s and 300 s bucket rows (`Sum`, `Count`, `Min`, `Max` per metric). The browser derives averages as `Sum / Count`. No in-memory state, so API restart loses nothing and the maintenance job only deletes.
**Tiers (recommended):** 15 s retained 2 h (1 h chart, 240 points), 60 s retained 26 h (24 h chart, 1 440 points), 300 s retained 7 d (7 d chart, 2 016 points). Worst case about 4 000 rows per server. Store min and max next to avg so a spike survives downsampling (D-05); draw the avg line with a min-max band.
**When to use:** Always. Per-metric `Count` is required because players can be unavailable while CPU is available.

### Pattern 4: Reason codes from the API, copy in the browser

**What:** The API returns `null` plus a stable code (`server-not-running`, `worker-offline`, `stale`, `game-does-not-report`, `query-failed`, `measuring`, `unsupported-platform`); ReactApp maps codes to `Server_Metrics_Reason_*` keys in EN/DE. Do not copy the prose-matching approach of `unavailableReasonMapping.ts` (exact English strings); codes are safer and need no `lang="en"` fallback.
**Example (frame handling, same shape as accrued cost):**

```ts
// Source: pattern of sources/ReactApp/src/features/hosting/costFrames.ts + FleetLiveUpdatesProvider.tsx
export function parseMetricsFrame(frame: UpdateStreamFrame): ParsedMetricsFrame | null {
  if (frame.type !== metricsMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  // validate every field (string ids, finite number-or-null values, parseable instants);
  // drop the frame on any failure - the browser never shows a value it cannot trust.
}
```

### Chart decision (D-08): hand-built SVG

Requirements are three single-series line charts with a min-max band, gap breaks, a crosshair tooltip, EN/DE labels, tokens only. This is well inside hand-built scope (about 250 lines of scales, a path builder that starts a new `M` segment when the gap between consecutive points exceeds 2 x bucket seconds, axis ticks). Follow the dataviz rules applied to this case: single series needs no legend (the panel heading names it); recessive grid; thin 2 px line; a crosshair plus tooltip layer; a visible "Show as table" view (a real `<table>`) as the keyboard and screen-reader path; do not use color alone (players is a stepped line, CPU and memory are smooth with different dash or marker, or each chart is separate with its own title, which already satisfies identity). Text uses text tokens, never the series color. Validate any new series color pair against the dark surface; the dataviz script is `scripts/validate_palette.js` in the dataviz skill. Because Phase 61 contrast tests compute ratios for every `--utf-*` pair, add new tokens (for example `--utf-chart-line`, `--utf-chart-band`, `--utf-chart-grid`) to `tokens.css` so the guard covers them.

### Anti-Patterns to Avoid

- **Re-implementing claim, lease or supersede logic in the socket handler.** Call `IWorkerProtocolService`; a second copy of the ordering and `CanExecuteCommand` rules will drift.
- **Using `UpdateStreamService` for the worker socket.** It matches subscribers by raw string prefix and is built for browsers; the worker channel needs one connection per worker and request/response.
- **Storing `0` for an unmeasured value** (also forbidden by MET-05). A first CPU sample has no delta: send `null` with `measuring`.
- **Trusting the payload for attribution.** The deployment, server and worker come from the authenticated worker row; ignore any id in the frame that disagrees (reject if `DeploymentId` mismatches, as `ValidateWorkloadEvidence` does for workloads).
- **Using the worker clock for bucketing.** Use API receipt time; accept the worker timestamp only after a skew sanity check (the existing code rejects observations later than now + 1 minute).
- **Computing availability in the browser from raw state.** The API decides and sends reason codes plus `staleAfterSeconds`; the browser only re-ages the last sample against that threshold.
- **Publishing a fleet-changed signal per sample.** `FleetChangeInterceptor` watches only `GameServer`, `GameServerDeployment` (status, possible cost) and `LifecycleJob` (status); a new `ServerMetricBucket` entity produces no signal. Do not add it to that interceptor.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Command claim ordering, leases, redelivery, supersession | A parallel implementation in the socket handler | Existing `IWorkerProtocolService` methods | Encodes transaction isolation, per-server `FOR UPDATE`, `CanExecuteCommand` cost-stop rules, idempotent transitions. |
| Worker credential verification | New token logic for the socket | `AuthenticateWorkerAsync` inside the service (per frame) | Handles pending-credential promotion, expiry, revocation. |
| WebSocket keep-alive and dead-peer detection | Custom ping frames | `KeepAliveInterval` plus `KeepAliveTimeout` (server per-accept via `WebSocketAcceptContext`, client via `ClientWebSocketOptions`) | Built in on .NET 9/10; the global `UseWebSockets` setting stays untouched for the browser stream. |
| Browser fan-out audience | New per-server permission query | `IFleetAudienceSource.GetViewersAsync` plus `FleetChangeStreamPaths.ForUser` | Same rule as REST fleet; fails closed; already tested. |
| A2S wire parsing | Ad-hoc string slicing | One small `A2sInfoClient` following the documented byte layout and challenge loop | Field order and the challenge retry are subtle (see Code Examples). |
| Time-series storage engine | A custom file store | A PostgreSQL table with a unique index | D-04; durable and testable on SQLite. |
| Chart library | A generic charting layer | A single `MetricLineChart` SVG component | Only one chart shape is needed. |
| Localization plumbing | Inline English strings | Flat `Server_Metrics_*` keys with the existing EN/DE parity gate (`scripts/check-locales.mjs`) | CI fails on missing parity and bad key characters (no `.` or `:`). |

**Key insight:** The risk in this phase is behavioral equivalence of the old protocol, not new technology. Every shortcut that bypasses the service layer or the journal reopens a solved correctness problem (duplicate execution, lost supersession, silent liveness loss that triggers an automatic cost stop).

## Common Pitfalls

### Pitfall 1: Removing the HTTP endpoints strands in-flight workers
**What goes wrong:** Workers run a per-deployment container image pulled at provisioning (`docker image inspect ... || docker pull`). After an API deploy, deployments already running an older image still speak HTTP. If the HTTP endpoints disappear, heartbeats stop, `LastHeartbeatAt` goes stale after `StaleThreshold` (60 s) and `DeploymentHealthReconciler` / `WaitForCommandAsync` fire `dedicated-worker-heartbeat-stale` / `worker-control-lost`, which trigger an automatic cost stop.
**Why it happens:** Registration already carries `ProtocolVersion` (currently 2). Cloud workers are not upgraded with the API.
**How to avoid:** Bump the worker's registration to `ProtocolVersion = 3` for the socket channel, keep the HTTP controller actions working for versions 1-2, mark them deprecated in XML docs, and plan removal for a later phase after a fleet-wide worker upgrade. Make `WorkerProtocolService.RegisterAsync` accept `1..3`.
**Warning signs:** `dedicated-worker-heartbeat-stale` faults immediately after an API deploy.

### Pitfall 2: Aska workers cannot register today
**What goes wrong:** `CloudWorkerRuntime` registers with `SupportedGameIdentifiers = ["arma3", "cs2"]`; `WorkerProtocolService.RegisterAsync` throws `WorkerProtocolConflictException("The worker does not support this deployment's game.")` for protocol >= 2 when the deployment's game identifier is not listed. `AskaWorkerCommandExecutor` is registered in `Program.cs`, so ASKA player counts (D-06) are unreachable until this is fixed.
**How to avoid:** Derive the list from `WorkerGameExecutorRegistry` (or add `"aska"`) in the same change that introduces protocol 3. [VERIFIED: sources/CloudWorker/CloudWorkerRuntime.cs:39; sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs:72-74]

### Pitfall 3: Credential rotation breaks a long-lived socket
**What goes wrong:** The socket authenticates with the credential held at connect. `RotateCredentialIfRequiredAsync` issues a pending credential; it is promoted when it first authenticates, after which the old one is invalid. The next frame on the old socket fails authentication.
**How to avoid:** Keep `register` and `rotate-credential` as HTTP (they are credential lifecycle, not listed in D-02). After a successful rotate and durable save, the worker closes and reopens the socket with the new credential. Treat close code 1008 as "re-read credential store, reconnect" and not as a crash. Test: rotate while connected.

### Pitfall 4: Lease and supersession semantics lose their HTTP probe
**What goes wrong:** Today the worker re-reports `Running` roughly once a second while a command executes, and a `409` is how it learns the control plane superseded the command (cost stop). A socket with only heartbeats would never learn this and could keep a paid process running after a cost-stop decision.
**How to avoid:** Push `command-superseded` from the API when supersession happens (notify from the cost-stop path) and also return superseded command ids in the heartbeat ack as the safety net. Keep `WaitForCommandAsync` (API side, 2 s DB poll) unchanged; it is server-internal and not worker polling. Planner must read `AutomaticCostStopService` and the `CanExecuteCommand` branches before writing the dispatcher.

### Pitfall 5: Heartbeat cadence is coupled to fault cost-stop
**What goes wrong:** `LifecycleWorkerOptions.StaleThreshold` is 60 s and `HeartbeatInterval` is 10 s; `GameServerControlPlaneWorker`, `DeploymentHealthReconciler` and `WaitForCommandAsync` read `LastHeartbeatAt`. If heartbeat frames are queued behind a large metrics or command frame, or the sender is blocked by one stuck send, the deployment can be fault-stopped.
**How to avoid:** Heartbeat has priority in the writer (separate high-priority lane or send before metrics), frames are small (cap incoming at about 256 KiB), the writer applies a send timeout, and the reconnect backoff is shorter than `StaleThreshold` (cap 15-20 s). A dropped socket must not mark the worker offline immediately; keep "stale by time" semantics.

### Pitfall 6: Metrics unavailable semantics leak zero
**What goes wrong:** `double` and `long` default to 0 in DTOs and TypeScript charts coerce `null` to 0.
**How to avoid:** Nullable everywhere (C# `double?`, `int?`, `long?`; TS `number | null`); a property test over (server state x worker status x sample age x game capability) proving a numeric value is present only when every availability condition holds; SVG path builder breaks the line on `null` and on a time gap, never connects across.

### Pitfall 7: Arma 3 headless clients inflate A2S player counts
**What goes wrong:** Headless clients connect like clients and appear in A2S data as players (BattleMetrics feedback and forum posts; no documented way to exclude them in the query response). Arma deployments here run headless clients (colocated or on separate hosts), so an A2S count would over-report.
**How to avoid:** Count players inside the engine: extend `fn_init.sqf` with a second record kind (for example `UTFPL|1|<nonce>|<seq>|<humans>`) computed from `allUsers` excluding entries whose `getUserInfo` index 7 (the headless flag already used by the existing script) is true, and parse it with a second bounded regex in `Arma3JoinMonitor`/`Arma3JoinEvidence`, reusing the nonce, tail-read and sequence rules. This is independent of headless-client topology. [VERIFIED: sources/CloudWorker/Arma3JoinAddonAssets/fn_init.sqf:22-26 (index 7 isHC use); Arma3JoinMonitor.cs] [MEDIUM: HC-in-A2S per web search summaries, not a primary BI doc]

### Pitfall 8: CS2 player count semantics (humans vs bots)
**What goes wrong:** A2S_INFO has separate `player_count` and `bot_count` bytes; whether `player_count` includes bots for CS2 is not documented in what was reachable. Reporting bots as players is a fake number.
**How to avoid:** Report `humans = max(0, player_count - bot_count)` and add a Wave 0 spike against a real CS2 dev server to confirm, with a unit test that fixes the arithmetic. Tag as assumption A3.

### Pitfall 9: Machine metrics inside a memory-limited container
**What goes wrong:** The worker container runs with `--memory 7g` (appsettings `ContainerMemoryLimit`), `--network bridge`, `--read-only`. `/proc/stat` and `/proc/meminfo` show host-wide values; the kill boundary is the cgroup. A VM with more than 7 GiB shows "50% used" while the container is near OOM.
**How to avoid:** Memory: used = `memory.current - inactive_file` from cgroup v2 (`memory.stat`), total = `memory.max` when numeric else `MemTotal`; fall back to `/proc/meminfo` (`MemTotal - MemAvailable`) when cgroup files are unreadable. CPU: `/proc/stat` first line, two reads, `1 - idleDelta/totalDelta` (idle plus iowait as idle). Non-Linux: `null` with `unsupported-platform`. Document the label as "Memory" and "CPU" of the server machine. Tag A4 for confirmation.

### Pitfall 10: Unbounded or abusable worker frames
**What goes wrong:** Anonymous endpoint plus long-lived socket: one worker could flood frames or send huge messages; per-IP rate limiting only applies to the upgrade request.
**How to avoid:** Hard incoming message size cap (close 1009), per-connection frame-rate cap with close 1008 on breach, JSON depth limit, strict frame type allowlist, `MaxLength` validation on every string, and reject metric values outside range (CPU 0-100, memory <= total, players 0-1024). Close and log (ids only, no credential) on malformed frames.

### Pitfall 11: Test harness has no TestHost
**What goes wrong:** `Api.Tests` has no `WebApplicationFactory` or `TestServer`; controller-level WebSocket tests would need a new package.
**How to avoid:** Put the whole socket loop in `WorkerChannelHandler.RunAsync(workerId, credential, WebSocket, ct)` so tests drive it with `WebSocket.CreateFromStream` over a duplex in-memory stream pair (framework API, no package) against `HostingTestStore`. The controller action stays a three-line shim.

### Pitfall 12: Process-local registry
**What goes wrong:** `WorkerChannelRegistry` and the browser push are process-local (same accepted limitation as `FleetChangeNotifier`, documented as D-14 there). A second API instance could receive a `Notify` for a worker connected elsewhere.
**How to avoid:** Document the single-instance assumption; the dispatcher's low-rate safety sweep covers missed notifications after restart; do not claim multi-instance support.

## Code Examples

### A2S_INFO request and response (CS2, optionally ASKA)

```csharp
// Source: field order and challenge loop verified from
// https://raw.githubusercontent.com/Yepoleb/python-a2s/master/a2s/info.py and a2s_sync.py
// Request: FF FF FF FF 'T' "Source Engine Query\0" [+ 4-byte little-endian challenge]
// Reply 0x41 => 4-byte challenge follows; resend the request with it appended (bounded retries).
// Reply 0x49: protocol(u8) name map folder game (cstrings) appId(u16) players(u8) maxPlayers(u8) bots(u8) ...
static byte[] BuildRequest(uint? challenge)
{
    var payload = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x54 };
    payload.AddRange(Encoding.ASCII.GetBytes("Source Engine Query\0"));
    if (challenge is { } c) payload.AddRange(BitConverter.GetBytes(c)); // little-endian on x86/x64
    return payload.ToArray();
}
```

Send over `UdpClient` to `127.0.0.1:<queryPort>` with a short timeout (for example 2 s) and at most 3 challenge rounds. Treat timeout or malformed reply as `query-failed` (null), never 0. Query ports: CS2 uses its game port (worker passes `-port` default 27015); Arma 3 query port is game port + 1 (2303 for 2302) if A2S were ever used; ASKA config renders `steam query port = 27016` (`AskaConfigurationRenderer`). [CITED: python-a2s source; VERIFIED: sources/CloudWorker/AskaConfigurationRenderer.cs:36]

### CPU percent from `/proc/stat`

```csharp
// Source: Linux procfs semantics; two samples, delta over non-idle/total jiffies.
// cpu  user nice system idle iowait irq softirq steal ...
static (long total, long idle) ReadCpu()
{
    var parts = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var v = parts.Skip(1).Select(long.Parse).ToArray();
    var idle = v[3] + (v.Length > 4 ? v[4] : 0);
    return (v.Sum(), idle);
}
// percent = 100 * (1 - (idle2-idle1) / (total2-total1)); first call returns null/measuring.
```

### Metrics sample contract (null plus reason, never zero)

```csharp
// Source: design for this phase; conventions from WorkerHeartbeatRequest.cs (records, IValidatableObject)
public sealed record WorkerMetricsSample
{
    public Guid DeploymentId { get; init; }
    public DateTimeOffset SampledAt { get; init; }
    [Range(0, 100)] public double? CpuPercent { get; init; }
    [MaxLength(64)] public string? CpuUnavailableReason { get; init; }
    public long? MemoryUsedBytes { get; init; }
    public long? MemoryTotalBytes { get; init; }
    [MaxLength(64)] public string? MemoryUnavailableReason { get; init; }
    [Range(0, 1024)] public int? PlayerCount { get; init; }
    [Range(0, 1024)] public int? PlayerCapacity { get; init; }
    [MaxLength(64)] public string? PlayersUnavailableReason { get; init; }
    // Validate: value and reason are mutually exclusive; used <= total; skew <= 1 minute.
}
```

### Gap-breaking path builder (React)

```ts
// Source: design for this phase. Break the polyline when a point is null or the gap is wider than 2 x bucket.
export function buildSegments(points: ReadonlyArray<{ t: number; v: number | null }>, bucketMs: number) {
  const segments: Array<Array<{ t: number; v: number }>> = [];
  let current: Array<{ t: number; v: number }> = [];
  let last: number | null = null;
  for (const p of points) {
    const broken = p.v === null || (last !== null && p.t - last > 2 * bucketMs);
    if (broken && current.length > 0) { segments.push(current); current = []; }
    if (p.v !== null) current.push({ t: p.t, v: p.v });
    last = p.t;
  }
  if (current.length > 0) segments.push(current);
  return segments; // a single-point segment renders as a marker, not a line
}
```

### Entity sketch (scaffold with `dotnet ef`, do not hand-write the migration)

```csharp
// Source: conventions from sources/Database/Hosting/CostAlert.cs (PrimaryKey, Fk suffix, Instant, [Index])
[Index(nameof(GameServerFk), nameof(ResolutionSeconds), nameof(BucketStart), IsUnique = true)]
[Index(nameof(ResolutionSeconds), nameof(BucketStart))] // prune scan
public sealed class ServerMetricBucket : IPrimaryKey<Guid>
{
    [Key] public Guid PrimaryKey { get; set; }
    public Guid GameServerFk { get; set; }   // restrict-delete FK, history spans sessions
    public Guid DeploymentFk { get; set; }
    public Guid WorkerFk { get; set; }       // primary host only in v1
    public int ResolutionSeconds { get; set; }   // 15 | 60 | 300
    public Instant BucketStart { get; set; }
    // per metric: Count, Sum, Min, Max (nullable when Count == 0): Cpu, MemoryUsed, Players
    public long? MemoryTotalBytes { get; set; }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Worker polls HTTP `commands/claim` every `PollInterval` (5 s default) and heartbeats inside the loop | Persistent outbound WebSocket with push delivery and acks | This phase (D-01/D-02) | Removes poll latency and per-request overhead; liveness becomes a timer independent of command execution. |
| ASP.NET WebSocket keep-alive is ping only | `KeepAliveTimeout` aborts a socket whose pong never arrives (server per accept, client via options) | .NET 9 | Dead-peer detection without custom pings. [CITED: learn.microsoft.com/aspnet/core/fundamentals/websockets] |
| A2S_INFO without challenge | Servers answer with 0x41 challenge before info | Source engine query change (python-a2s implements the retry) | Client must handle the challenge round. |

**Deprecated/outdated:**
- `GameServerDeployment.TimeStampStarted` as an uptime source: declared and exposed, never written by hosting code. Do not build MET-01 on it (a fix is possible but would be a separate lifecycle change).
- RCON as the CS2 player source: the worker has no persisted RCON secret and the client discards bodies.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ClientWebSocketOptions.SetRequestHeader("Authorization", ...)` works for the .NET worker upgrade request, and nginx/Kestrel pass the header through (the browser cannot set it, the .NET client can) | Pattern 1 | Credential would need to move into a first frame; rework of auth step. Verify in the first transport task. |
| A2 | Aska's server answers A2S_INFO with a usable player count on its query port 27016 under Wine/Xvfb | MET-02, Standard Stack | If false, ASKA shows `game-does-not-report`; acceptable under D-06, but expectation should be confirmed with the user. |
| A3 | CS2 A2S `player_count` includes bots, so humans = player_count - bot_count | Pitfall 8 | Over- or under-reporting humans; fix with the live spike before shipping. |
| A4 | Reporting memory as cgroup-aware (container-bounded) rather than raw VM memory matches the user's intent for "memory of the server machine" | Pitfall 9 | Tile shows container, not VM, figures; wording or source may need to change. |
| A5 | Arma 3 headless clients appear in A2S player counts (web search summaries of BattleMetrics/BI forum posts, no primary doc) | Pitfall 7 | If untrue, A2S would suffice for Arma; the SQF approach is still correct, just heavier. |
| A6 | A 15 s report interval with 3 x stale threshold (45 s) is acceptable; heartbeat stays 10 s and is independent | Pattern 3, D-09 | Charts too coarse or too chatty; both are options, not protocol constraints. |
| A7 | Per-deployment worker containers are not auto-upgraded with the API, so older protocols must keep working after an API deploy | Pitfall 1 | If all workers always restart on deploy, HTTP removal could be immediate; confirm with the user before removing. |
| A8 | `Arma3JoinMonitor`'s bounded log-tail parser can host a second record kind without weakening the nonce, sequence and size guarantees | Pitfall 7 | Arma player count could regress readiness detection; mitigated by existing `Arma3JoinMonitorTests` plus new cases. |
| A9 | v1 shows metrics of the primary host only (`game-server` ordinal 0); headless-client hosts are excluded | Summary, Pattern 3 | Users may expect HC machine load; deferred, but should be confirmed. |

## Open Questions (RESOLVED)

1. **Does D-06's "existing RCON client" override the recommendation to use A2S for CS2?**
   - What we know: the RCON client only sends `quit`, the password exists only in command secrets, and storing it on the worker contradicts the project's secret-handling rules.
   - What's unclear: whether the user needs RCON specifically (for example to also read `status` details later).
   - Recommendation: Use A2S_INFO behind the adapter; ask the user to confirm at plan time since D-06 named RCON.
   - RESOLVED: D-06 is honored. Counter-Strike 2 reads players through the existing RCON client (`status`, humans only, password held in memory from the latest Start and never persisted or logged) and falls back to A2S_INFO on its game port when no password is held or the reply has no parseable players line; both failing report `query-failed`. After a worker restart the in-memory password is gone, so Counter-Strike 2 uses A2S_INFO until the next Start. Settled by plan 67-09 (Counter-Strike 2 player source; recorded in the decision log by plan 67-20).
2. **Keep or remove the HTTP worker endpoints?**
   - What we know: CONTEXT leaves it to plan time; in-flight workers on old images depend on them (Pitfall 1).
   - Recommendation: keep for protocol 1-2 this phase; schedule removal later; the contract change is not breaking until removal.
   - RESOLVED: the HTTP register and rotate-credential endpoints remain the credential lifecycle path, and the HTTP heartbeat, claim and report endpoints stay available for protocol 1 and 2 worker images; registration accepts protocol versions 1 to 3. Settled by plan 67-03 (authenticated socket transport and protocol version 3).
3. **Which instant is "running since" when the process restarts or a workload is restarting?**
   - What we know: `ProcessStartedAt` resets on restart and is null while stopped.
   - Recommendation: show the primary host's game process start and the reason `server-not-running` otherwise; do not show deployment creation time.
   - RESOLVED: running since is the primary worker's observed `ProcessStartedAt`, never the deployment timestamp, and is `server-not-running` while no game process runs. Settled by plan 67-04 (current readings and availability read model).
4. **ASKA reliable source?**
   - What we know: UDP 27016 bound-check exists for readiness; no document found on A2S support.
   - Recommendation: Wave 0 spike; fall back to the explicit "game does not report players" state.
   - RESOLVED: ASKA reports players from an A2S_INFO query on its loopback steam query port 27016 (players minus bots); a timeout or malformed reply reports `query-failed`, never a number. The live check is an end-of-phase human UAT item (plan 67-20). Settled by plan 67-05 (A2S client and ASKA player source).
5. **Should there be a 67-UI-SPEC.md before planning?**
   - What we know: `workflow.ui_phase` is true and the phase has UI hint yes; only CONTEXT exists in the phase directory. Chart layout, tile copy, reason copy and a11y rules are decided here only at the principle level.
   - Recommendation: run `/gsd-ui-phase 67` or put the chart contract in the plan.
   - RESOLVED: `67-UI-SPEC.md` exists and is approved; the browser plans 67-15 to 67-19 implement its tile, chart, copy and accessibility contract.
6. **Browser live update for 24 h and 7 d ranges.**
   - Recommendation: frames update the tiles and merge into the 1 h series; for 24 h and 7 d merge the sample into the last bucket using the REST `count`, or simply invalidate the active range once per bucket interval (still no polling of a worker; one coalesced refetch). Planner picks; the merge function is a pure unit-testable helper.
   - RESOLVED: live frames update the tiles and merge into the 1 hour series through a pure helper; the 24 hour and 7 day ranges refetch the history once per bucket interval as a single coalesced request, never by polling a worker. Settled by plan 67-17 (live metric frames and history invalidation).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.201 | — |
| `dotnet-ef` | scaffold migration | yes | 10.0.9 | — |
| Node.js | ReactApp build/tests | yes (mismatch) | v25.5.0; `package.json` engines require `>=24 <25` | Use Node 24 if `npm ci` enforces engines; otherwise warnings only |
| npm | ReactApp | yes | 11.8.0 | — |
| Docker | optional dev-worker container, A2S/proc checks in Linux | yes | 29.5.3 | — |
| PostgreSQL | production DB; migration verification | not probed (`pg_isready` absent) | — | Tests run on SQLite in-memory; scaffold needs no live DB |
| Live CS2 / Arma 3 / ASKA server for A2S spike | A2S and SQF verification | not available here | — | Unit tests with recorded packets; flag live acceptance as human verification (ties to `arma-3-live-acceptance.md`) |

**Missing dependencies with no fallback:** none blocking planning.

**Missing dependencies with fallback:** live game servers (use recorded A2S byte fixtures and an in-process fake UDP responder; live check is a human UAT item per `human_verify_mode: end-of-phase`); Node 25 vs engines range (verify `npm run ci` runs, or use Node 24).

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (API, CloudWorker), Vitest 4.1.10 + Testing Library (ReactApp), Playwright 1.62.1 e2e (optional) |
| Config file | `tests/Api.Tests/Api.Tests.csproj`, `tests/CloudWorker.Tests/CloudWorker.Tests.csproj`, `sources/ReactApp/vitest.config.ts` |
| Quick run command | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServerMetrics\|FullyQualifiedName~WorkerChannel"` and `cd sources/ReactApp && npx vitest run tests/hosting` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MET-01 | `runningSince` equals the primary worker's `ProcessStartedAt` when Running and fresh; null with `server-not-running` otherwise | unit/integration (SQLite) | `dotnet test tests/Api.Tests --filter ServerMetricsReadModelTests` | Wave 0 |
| MET-02 | A2S parser (challenge round, bots arithmetic, timeout -> null); SQF record parser (nonce, sequence, bounds); game without source -> `game-does-not-report` | unit | `dotnet test tests/CloudWorker.Tests --filter "A2sInfoClientTests\|Arma3PlayerEvidenceTests"` | Wave 0 |
| MET-03 | `/proc/stat` delta math; first sample null+`measuring`; unreadable -> null+reason; memory cgroup vs meminfo fallback | unit with fixture files | `dotnet test tests/CloudWorker.Tests --filter WorkerMetricsSamplerTests` | Wave 0 |
| MET-04 | Streaming rollup exactness (sum/count/min/max), duplicate sample idempotent, tier selection per range, prune by tier age and restart-safe, spike survives (max) | integration (SQLite) | `dotnet test tests/Api.Tests --filter "ServerMetricIngestTests\|ServerMetricsPruneTests"` | Wave 0 |
| MET-04 | Chart builds gap-broken segments, no interpolation, table view parity; range switch; reload shows REST history | Vitest + RTL | `npx vitest run tests/hosting/metrics-*.test.tsx` | Wave 0 |
| MET-05 | Only the worker channel ingests metrics; wrong credential/other deployment rejected; frame cap/rate cap close codes; stopped server/offline worker/stale/game-silent -> null with reason (property test over the state product); browser frame parser drops malformed frames | integration + property | `dotnet test tests/Api.Tests --filter "WorkerChannelHandlerTests\|ServerMetricsAvailabilityTests"` | Wave 0 |
| D-02 | Channel parity: heartbeat updates `LastHeartbeatAt`; claim returns redelivered `Received`/`Running` first; terminal report idempotent; 409-equivalent superseded ack; revoked credential closes 1008; credential rotation then reconnect; API restart (new handler over same store) redelivers | integration (`WebSocket.CreateFromStream`) | `dotnet test tests/Api.Tests --filter WorkerChannelHandlerTests` and `tests/CloudWorker.Tests --filter CloudWorkerChannelClientTests` | Wave 0 |

### Sampling Rate
- **Per task commit:** the filtered quick command for the touched area.
- **Per wave merge:** `dotnet test X39.UnitedTacticalForces.slnx` plus `npm run test` in ReactApp.
- **Phase gate:** full solution tests, `npm run ci`, `dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <tmp>` then `cmp` with `sources/ReactApp/openapi/utf-api.json`, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs` - covers D-02 and MET-05 transport (fake duplex WebSocket pair, `HostingTestStore`)
- [ ] `tests/Api.Tests/GameServerHosting/ServerMetricIngestTests.cs`, `ServerMetricsPruneTests.cs`, `ServerMetricsReadModelTests.cs`, `ServerMetricsAvailabilityTests.cs`
- [ ] `tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs`, `A2sInfoClientTests.cs` (in-process fake UDP responder), `WorkerMetricsSamplerTests.cs`, extensions to `Arma3JoinMonitorTests.cs` / `Arma3JoinAddonTests.cs`
- [ ] `sources/ReactApp/tests/hosting/metrics-*.test.ts(x)` and a contrast-guard entry for new chart tokens
- [ ] A hand-written `RecordingWorkerChannel`/duplex-stream helper (no new package)

## Security Domain

`security_enforcement` is enabled (absent means enabled); ASVS level 1, block on high.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Deployment-scoped bearer credential verified per frame via existing `AuthenticateWorkerAsync`; never in a query string; credential never logged |
| V3 Session Management | yes (connection lifetime) | One live connection per worker (newest wins); close on revoke/expiry; reconnect with the rotated credential |
| V4 Access Control | yes | REST metrics endpoint `[Authorize(Claims.ResourceBased.Server.View)]`; browser push only to `IFleetAudienceSource` viewers on their per-user path; worker attribution only from the authenticated worker row |
| V5 Input Validation | yes | Frame size/rate caps, JSON depth, type allowlist, `DataAnnotations` ranges, mutual exclusion of value and reason, skew checks; browser frame validation drops malformed frames |
| V6 Cryptography | yes (transport) | WSS/TLS only for non-loopback (reuse `ValidateTransport`); no new crypto, never hand-roll |
| V7/V8 Logging, Data protection | yes | Log ids only; commands carry secrets (`WorkerCommandClaimResponse.Secrets`) so the channel must never log frames and must redact like `WorkerSecretRedactor` |
| V13 API/Web service | yes | Rate limit policy on upgrade (`worker-protocol-per-ip`), plus per-connection limits |

### Known Threat Patterns for ASP.NET Core WebSocket + worker control plane

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stolen or replayed worker credential opens a channel and receives commands with secrets | Spoofing / Info disclosure | Short-lived, rotating, hashed credentials (existing); per-frame verification; one connection per worker; audit worker connect and disconnect |
| Worker sends metrics for another deployment | Tampering | Attribute to the authenticated worker's deployment; reject payload id mismatch |
| Oversized or flooding frames exhaust the API | Denial of service | Message size cap (1009), frame-rate cap (1008), bounded buffers, per-IP upgrade rate limit |
| Browser user sees metrics of a server they cannot view | Elevation / Info disclosure | Same audience and View claim as fleet; fail closed on audience read error |
| Fake values hide an outage (zero CPU, invented players) | Repudiation / Integrity | Nullable values plus reason codes end to end; property test; chart gap rule |
| Cross-site WebSocket hijacking of the browser stream | Spoofing | Existing cookie-authenticated `update-stream/subscribe`; consider `WebSocketOptions.AllowedOrigins` (not part of this phase unless already configured) |
| Secret leakage via logs of command frames | Info disclosure | Never log frame bodies; reuse redaction; test with a recording logger like existing secret-leak tests |

## Sources

### Primary (HIGH confidence)
- Repository source read this session: `sources/Api/Controllers/LifecycleWorkersController.cs`, `sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs` (lines 28-440, 438-700), `IWorkerProtocolService.cs`, `WorkerProtocolRateLimitPolicy.cs`, `sources/HostingContracts/WorkerProtocol/WorkerHeartbeatRequest.cs`, `sources/CloudWorker/CloudWorkerRuntime.cs`, `CloudWorkerApiClient.cs`, `CloudWorkerConnectionConfig.cs`, `Program.cs`, `IWorkerCommandExecutor.cs`, `WorkerRuntimeStatus.cs`, `Arma3JoinMonitor.cs`, `Arma3JoinEvidence.cs`, `Arma3JoinAddon.cs`, `Arma3JoinAddonAssets/fn_init.sqf`, `CounterStrike2RconClient.cs`, `CounterStrike2WorkerCommandExecutor.cs`, `AskaWorkerCommandExecutor.cs`, `AskaProcessController.cs`, `sources/Api/Services/GameServerLifecycle/DeploymentHealthReconciler.cs`, `LifecycleWorkerOptions.cs`, `LifecycleJobExecutor.cs` (2020-2210), `FleetChangeNotifier.cs`, `FleetChangeInterceptor.cs`, `FleetChangeStreamPaths.cs`, `CostAlertPublisher.cs`, `HostingControlPlaneServiceCollectionExtensions.cs`, `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`, `sources/Api/Controllers/UpdateStreamController.cs`, `sources/Database/Hosting/LifecycleWorker.cs`, `CostAlert.cs`, `GameServerDeployment.cs`, `sources/Api/DTO/GameServerDeploymentDto.cs`, `sources/Api/Services/WorkerProtocol/CloudInitWorkerBootstrapPayloadRenderer.cs` (docker run flags), `sources/CloudWorker/Dockerfile`, ReactApp `OverviewSection.tsx`, `FleetLiveUpdatesProvider.tsx`, `costFrames.ts`, `AccruedCost.tsx`, `SlotPlaceholder.tsx`, `updateStream.ts`, `package.json`, `tokens.css`, locale files.
- `.planning/phases/67-server-metrics/67-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/codebase/TESTING.md`, `docs/features/cloud-based-game-server-hosting/control-plane-workers-and-jobs.md`, `decision-log.md`, `.planning/phases/66-accrued-cost-cost-alerts/66-RESEARCH.md` (sibling format and the Phase 66 push pattern).
- [ASP.NET Core WebSockets docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0) - KeepAliveInterval/KeepAliveTimeout (global and per accepted socket), keep pipeline alive for socket lifetime, client-disconnect detection.
- [ClientWebSocketOptions.KeepAliveTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocketoptions.keepalivetimeout?view=net-10.0) - available .NET 9/10, default InfiniteTimeSpan.
- [python-a2s info.py](https://raw.githubusercontent.com/Yepoleb/python-a2s/master/a2s/info.py) and [a2s_sync.py](https://raw.githubusercontent.com/Yepoleb/python-a2s/master/a2s/a2s_sync.py) - A2S_INFO request bytes, field order, 0x41 challenge retry, 0xFFFFFFFF single-packet header.

### Secondary (MEDIUM confidence)
- Web search summaries of [Valve Source Server Queries](https://developer.valvesoftware.com/wiki/Source_Server_Queries) (direct fetch returned 403): challenge mechanism, players/max/bots bytes.
- Web search summary of [BI STEAMWORKSquery](https://community.bistudio.com/wiki/STEAMWORKSquery) and BI forum/BattleMetrics feedback: Arma 3 query port game port + 1; headless clients appear as players in query data (direct fetch of the BI wiki returned 403).

### Tertiary (LOW confidence)
- CS2 RCON `status` format and reliability (hosting-provider pages only; not used for the recommendation).
- ASKA A2S support: no source found; treated as a spike.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; every component is in the repo or the .NET 10 framework and was read this session.
- Architecture (transport, storage, read model, push): HIGH for fit with existing code; MEDIUM for the exact dispatcher/supersession wiring, which depends on `AutomaticCostStopService` and `GameServerControlPlaneWorker` internals not fully read.
- Pitfalls: HIGH for the in-repo ones (1, 2, 3, 5, 6, 11); MEDIUM for game-specific ones (7, 8, 9) pending live spikes.

**Research date:** 2026-11-21
**Valid until:** 2026-12-21 (stable stack; revisit A2/A3/A5 after the live spike)

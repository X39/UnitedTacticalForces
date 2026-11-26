# Phase 67: Server Metrics - Context

**Gathered:** 2026-11-21
**Status:** Ready for planning

<domain>
## Phase Boundary

A user sees how a running server is doing in the Overview metrics slot (reserved in Phase 63): uptime, current player
count, CPU and memory of the server machine, and a retained history as charts that survives page reload. A metric that
cannot be measured is shown as "unavailable" with a reason, never as zero or an invented value. The worker measures and
reports; the API never samples the game process. Requirements: MET-01..MET-05. Scope: `sources/ReactApp`, API,
database, `sources/HostingContracts`, `sources/CloudWorker`.

Does not build auto-stop or idle shutdown (Phase 68, which consumes the player/activity signal).

</domain>

<decisions>
## Implementation Decisions

### Worker transport (MET-05)
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

### History storage and retention (MET-04)
- **D-04:** History lives in a **PostgreSQL table** (durable across API restarts and reloads), added through a scaffolded
  `dotnet ef migrations add` migration. — **Reversibility:** costly — schema plus data migration.
- **D-05:** Retain **7 days, downsampled**. The tiering (raw interval, rollup resolutions, whether to store min/avg/max)
  is Claude's discretion; spikes should survive downsampling, and pruning must be a durable background job.

### Players per game (MET-02)
- **D-06:** Show a real player count for **all three games where possible**: Arma 3 (existing join monitor), Counter-Strike 2
  (existing RCON client) and Aska if its process exposes a reliable source. Any game or moment where the count cannot be
  read shows "unavailable". Player count is reported by the worker through the game adapters; the API never queries the
  game.

### Overview display (MET-01, MET-03, MET-04)
- **D-07:** **Tiles plus charts** in the Overview metrics slot: current-value tiles (uptime as "running since", players,
  CPU, memory) with a full chart per metric below and a range switcher (1 hour, 24 hours, 7 days).
- **D-08:** Chart implementation (hand-built accessible SVG vs an exact-pinned library with a supply-chain checkpoint) is
  Claude's discretion. ReactApp has no chart library today; charts must use `--utf-*` tokens, be keyboard/screen-reader
  accessible, and be localized in EN and DE.

### Unavailable and stale rules (MET-05)
- **D-09:** A metric is **unavailable** when the server is stopped, the worker is offline, the sample is stale, or the game
  does not report it. Tiles show "Unavailable" with a plain reason (stopped / worker offline / game does not report
  players). Charts **break the line at gaps** and never interpolate or draw zero. A sample older than about three report
  intervals counts as stale. Exact interval and threshold are Claude's discretion.

### Claude's Discretion
- Downsampling tiers and sampling interval (D-05).
- Chart implementation (D-08).
- Exact stale threshold and unavailable-reason wording (D-09), following Phase 61 UI-SPEC copy rules.
- Whether the browser receives metric updates live (reuse the fleet/workspace socket) or by query refresh.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior phase contracts
- `.planning/phases/63-workspace-shell-activity/63-CONTEXT.md` - Overview metrics slot, WebSocket reuse, "unavailable, never fake" principle.
- `.planning/phases/61-foundations-localization-member-access/61-CONTEXT.md` - State model, tokens, locale ownership.
- `.planning/phases/61-foundations-localization-member-access/61-UI-SPEC.md` - Design and copy contract.
- `.planning/phases/66-accrued-cost-cost-alerts/` - Most recent phase; alert channels Phase 68 will reuse.

### Requirements and roadmap
- `.planning/ROADMAP.md` - Phase 67 goal, success criteria, Cross-Phase Constraints.
- `.planning/REQUIREMENTS.md` - MET-01..MET-05.

### Project rules and architecture
- `AGENTS.md` - Architecture boundaries (no game execution in the API), EF migration rule, commit rules, OpenAPI/client regeneration.
- `.planning/codebase/CONVENTIONS.md`, `.planning/codebase/TESTING.md`
- `docs/features/cloud-based-game-server-hosting/` - Worker protocol and hosting specification.

### Code touchpoints
- `sources/Api/Controllers/LifecycleWorkersController.cs`, `sources/Api/Services/WorkerProtocol/` - Current HTTP worker protocol.
- `sources/HostingContracts/WorkerProtocol/` - Worker contracts (`WorkerHeartbeatRequest`, claim and report types).
- `sources/CloudWorker/CloudWorkerRuntime.cs`, `CloudWorkerApiClient.cs`, `FlatFileWorkerCommandJournal.cs` - Worker loop, client, journal.
- `sources/CloudWorker/Arma3JoinMonitor.cs`, `CounterStrike2RconClient.cs`, `Aska*` - Player count sources.
- `sources/Api/Services/GameServerLifecycle/DeploymentHealthReconciler.cs` - Worker health handling.
- `sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx` - Metrics slot (currently a placeholder).
- `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx` - Browser socket client.

No further external specs.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WorkerHeartbeatRequest` / `HeartbeatAsync`: existing liveness data and interval handling.
- Game adapters in `sources/CloudWorker` (join monitor, RCON client, process observation) as player-count sources.
- `SlotPlaceholder` metrics slot, `StatePanel`, `--utf-*` tokens, shared server components.
- `UpdateStreamService` / `IFleetChangeNotifier`: existing server-side WebSocket and change notification patterns.

### Established Patterns
- Worker protocol: deployment-scoped credentials, rate limit policy, idempotent report endpoints, durable worker journal.
- Flat i18next keys (`Server_{Area}_{Item}`) with EN/DE parity gate; style guard on `components/server/*`.
- Generated Kiota clients and OpenAPI snapshot must be regenerated after API changes.

### Integration Points
- New worker WebSocket endpoint in the API beside `LifecycleWorkersController`; new metrics entity and prune job in `Database`/`Api`.
- Overview metrics slot in `OverviewSection.tsx`.
- Phase 68 idle detection will consume the player/activity signal produced here.

</code_context>

<specifics>
## Specific Ideas

- User expectation: a proper connection between the worker and the API that does not rely on polling.

</specifics>

<deferred>
## Deferred Ideas

- Disk and network metrics, per-member metric visibility rules, and metric alerting were not discussed; revisit only if requested.

</deferred>

---

*Phase: 67-Server Metrics*
*Context gathered: 2026-11-21*

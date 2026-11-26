# Phase 68: Auto-Stop & Schedules - Research

**Researched:** 2026-11-22
**Domain:** Control-plane automation (durable timers, schedules, idle detection, pre-stop warnings) on an existing ASP.NET Core / EF Core / PostgreSQL hosting control plane, plus a React workspace surface.
**Confidence:** HIGH for codebase facts and reuse points (all read this session); MEDIUM for the semantic decisions marked `[ASSUMED]` (they need user confirmation, see Assumptions Log).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
None. `68-CONTEXT.md` states: "All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions."

The approved `68-UI-SPEC.md` (status approved 2026-11-22) is treated as a locked input for the React surface: decisions D-01 to D-13, the "Data The UI Needs" table, the copy contract and the interaction contract. Where this research proposes a deviation it is listed under "Resolved assumptions" or "Open Questions" and never silently applied.

### Claude's Discretion
All implementation choices (verbatim from CONTEXT.md): "All implementation choices are at Claude's discretion — discuss phase was skipped per user setting."

### Deferred Ideas (OUT OF SCOPE)
`68-CONTEXT.md` lists none ("None — discuss phase skipped."). `68-UI-SPEC.md` records these as out of scope: a postpone or "keep running" action on the warning, one-off (dated) schedules and calendar or event-driven starts, start-and-stop window pairing, per-day idle times, configurable warning lead time, notifying the owner through the channels when a scheduled start is skipped, a fleet-card indicator for pending or scheduled automation, bulk apply of settings to several servers, any client-side calculation of idle time, stop instants or next runs.

Carry-forward working agreements (CONTEXT.md `<specifics>`): extend `sources/Api/Data/UatFleetScenarioSeeder.cs` and `scripts/Start-Uat.ps1` with scenarios that make the automation observable within minutes so the seeded-stack Playwright suite proves it; real-environment checks are deferred to the end of the milestone.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUT-01 | Owner can set an idle shutdown after N minutes without players or activity | Idle signal = `IServerMetricsService.EvaluateCurrentAsync` (Phase 67) plus in-flight lifecycle jobs; persisted idle state with hold-on-unknown; capability flag on the game definition; settings entity + PUT endpoint (sections "Data model", "Idle timer") |
| AUT-02 | Owner can set a maximum runtime after which the server stops | `GameServerDeployment.TimeStampStarted` is the run clock (set once when the Start job completes); same pending-stop pipeline as idle |
| AUT-03 | Owner can schedule start and stop times | Weekly rules, per-server IANA zone, NodaTime `LenientResolver`, persisted watermark plus `ServerScheduleRun` unique occurrence rows, consent-gated scheduled start |
| AUT-04 | Owner is warned shortly before an automatic stop | `AutomationPendingStop` row is the single source of the warning; delivered by extending the Phase 66 `CostAlert` + outbox + publisher with an `AutoStop` kind; workspace notice from the server DTO |
| AUT-05 | Automatic stops run as durable lifecycle jobs with an audit entry and verified non-billable teardown | Enqueue a normal `Stop` lifecycle job (the manual "Shut down and stop billing" action) through a refactored shared enqueue core with a system actor, a job `Trigger`, and seven new audit kinds |
| AUT-06 | Schedules and idle timers survive API restarts | All timer state in PostgreSQL (state row, watermark, pending-stop row, run rows); claim-by-conditional-update plus unique indexes make a due stop happen exactly once; restart-simulation integration tests |
</phase_requirements>

## Summary

Phase 68 is a control-plane feature with a thin React surface. The repository already contains every building block it needs; the work is composition, not invention. The reusable pieces are: the hosted-worker pattern (`CostAlertWorker`: scoped pass, `Instant now` passed explicitly, no state in memory, try/catch around the pass), the persist-intent-first pattern (`CostAlertEvaluator`: unique index makes a crossing once-only across restarts), the outbox and channel stack of Phase 66 (`CostAlert`, `CostAlertDelivery`, `CostAlertDeliveryProcessor`, `CostAlertPublisher`, Discord DM and SMTP adapters), the Phase 67 reading (`IServerMetricsService.EvaluateCurrentAsync` already decides `PlayerCount` versus an unavailable reason in one place), and the durable `LifecycleJob` pipeline whose `Stop` kind performs the manual "Shut down and stop billing" action (save, preserve, then `ReleaseDeploymentAsync(terminate: false)` with verified release).

Three findings change the plan shape. (1) `EnqueueActionAsync` is principal-driven (it needs `Server.Operate` and `CanUseCostOwnerAsync` on a `ClaimsPrincipal`), so a system-initiated Stop or Start needs a refactor into a shared core that accepts a null actor and a job trigger; the ROADMAP hint "reuse the durable Terminate path" would lose data, so the automatic stop must be a normal `Stop` job (the UI contract already says so). (2) The existing `FleetChangeInterceptor` only watches `GameServer`, `GameServerDeployment` and `LifecycleJob`; automation settings and pending-stop rows must be added or the notice and status list will not refresh live. `GameServersController`'s operations feed also filters on a hard-coded `operationalKinds` list, so the seven new audit kinds are invisible in Activity unless added. (3) Time is not injectable (`NodaSystemClock` is `NodaTime.SystemClock`), so determinism comes from passing `Instant now` into the evaluator, as `CostAlertEvaluator` already does.

**Primary recommendation:** Build one `ServerAutomationEvaluator` (pure decision functions plus a thin persistence shell, invoked by a new `ServerAutomationWorker` registered inside the not-paused block of `AddHostingControlPlane`) that persists a single `AutomationPendingStop` row per warning episode, claims it with a conditional update, and enqueues a normal `Stop` (or, for schedules, `Start`) job through a refactored lifecycle enqueue core; extend the Phase 66 alert record with an `AutoStop` kind instead of building a second notification pipeline.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Idle, max-runtime and schedule timers; due-time calculation | API / Backend (control plane hosted service) | Database / Storage | AGENTS.md: the control plane owns schedules and timers; state must survive restarts, so it lives in PostgreSQL |
| Idle signal (player count) | Worker reports, API decides | Database (`ServerMetricSnapshot`) | Phase 67: "Only the worker measures players"; the API never queries a game |
| Enqueue the automatic Stop / scheduled Start | API / Backend (lifecycle service) | — | Lifecycle jobs are API-owned durable records; the external worker only executes commands |
| Verified non-billable teardown | API / Backend (provider reconcile inside `LifecycleJobExecutor`) | — | Provider teardown must not depend on the worker or an API-hosted game process |
| Pre-stop warning delivery (Discord, email) | API / Backend (outbox + dispatcher) | — | Reuses the Phase 66 outbox; at-least-once with deterministic email Message-Id |
| In-app warning (notice, toast, menu row) | Browser / Client (display only) | API (record + frame) | UI-SPEC: "the browser displays, it never calculates" |
| Settings editing, draft, validation echo | Browser / Client | API (authoritative validation, 409 on stale version) | Draft-then-save in the panel; API enforces limits it sends in `limits` |
| Authorization (`canManageAutomation`, consent) | API / Backend | — | UI only reads the flag; consent bound to cost owner on the server |
| Time zone evaluation of weekly rules | API / Backend (NodaTime TZDB) | — | The control plane evaluates rules, never the browser |
| Activity trigger line, audit entries | API (projection) | Browser (maps) | Closed vocabulary rendered by the client from the job `trigger` and audit `kind` |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| NodaTime | 3.3.2 (already referenced) | Zone-aware recurrence, `Instant` arithmetic, TZDB zone lookup | `[VERIFIED: sources/Api/Api.csproj:13]` `<PackageReference Include="NodaTime" Version="3.3.2" />`; the whole codebase uses `Instant`; TZDB is embedded so there is no OS tzdata dependency `[ASSUMED]` |
| EF Core 10.0.9 + Npgsql 10.0.2 | existing | Persistence, conditional `ExecuteUpdateAsync` claims, filtered unique indexes | Same pattern as `CostAlertDeliveryProcessor.TryClaimAsync` |
| `IHostedService` / `BackgroundService` | .NET 10 | The evaluation loop | `CostAlertWorker`, `HostingCostReconciliationWorker` precedent |
| xUnit 2.9.3 + SQLite in-memory (`HostingTestStore`) | existing | Backend tests | `tests/Api.Tests/GameServerHosting/*` precedent |
| Vitest + Testing Library + Playwright (`@axe-core/playwright`) | existing | Frontend model, component, e2e and seeded-stack UAT | Phases 61-67 precedent; UI-SPEC lists the suites |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Intl` (browser) | native | Weekday names, time, zone list, relative time | UI-SPEC: "No new npm dependency"; native `type="time"` input |
| Kiota 1.32.2 (installed global tool) | 1.32.2 | Legacy Blazor client regeneration | Only if a breaking DTO change would break the legacy WebApp (AGENTS.md); additive changes were not regenerated in Phase 62 precedent |
| dotnet-ef | 10.0.9 | Scaffold the migration | `[VERIFIED: dotnet tool list -g]` shows `dotnet-ef  10.0.9`; AGENTS.md mandates `dotnet ef migrations add` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written weekly recurrence with NodaTime | Cronos / NCrontab / Quartz.NET / Hangfire | New dependency, a second persistence model for timers, no benefit for 8 weekly rules; rejected (the ROADMAP asks for "a scheduler that persists next-run state and catches up safely", which the unique-run table plus watermark gives) |
| Extending `CostAlert` with a kind | A new `AutoStopWarning` entity with its own deliveries, dismissals, list endpoint, socket frame, dispatcher | Duplicates the whole Phase 66 stack; the UI-SPEC already assumes "a `kind` on the Phase 66 alert record" |
| Terminate job for automatic stop | Normal Stop job | Terminate skips worker evidence and preservation (data loss); the UI contract says an automatic stop is the same "Shut down and stop billing" job (`Server_Action_Stop_Label`) |

**Installation:** No new packages. Nothing to install.

**Version verification:** NodaTime 3.3.2 is the version referenced by both `sources/Api/Api.csproj` and `sources/Database/Database.csproj` (read this session). No registry lookup is needed because no package is added.

## Package Legitimacy Audit

No external packages are installed by this phase. `Packages removed due to [SLOP] verdict:` none. `Packages flagged as suspicious [SUS]:` none. The planner must not add a scheduling library; if one is proposed during planning it needs a `checkpoint:human-verify` and a legitimacy check first.

## Architecture Patterns

### System Architecture Diagram

```
 Owner (React panel) --PUT game-servers/{id}/automation (version, startConsent)--> AutomationSettingsService
        |                                                                            | validate limits, zone, rules
        | (draft, 409 on stale version)                                              | authorize canManageAutomation (+ consent => CanUseCostOwner)
        |                                                                            v
        |                                                              PostgreSQL: ServerAutomationSettings (+Version)
        |                                                                          ServerSchedule rows (id, action, days, minuteOfDay, EffectiveFrom)
        |                                                                          audit: AutomationSettingsChanged / ScheduledStartsConfirmed
        |                                                                            |
 ServerAutomationWorker (hosted, not-paused only, every ~15-30 s, `Instant now`)     |
   scope per pass --> ServerAutomationEvaluator.EvaluateAsync(now) <----------------+
        |
        |-- per server (own try/catch, bounded by servers that have settings or schedules)
        |     1. Schedules: scan (watermark, now]; per occurrence insert ServerScheduleRun (unique ScheduleFk+OccurrenceAt)
        |           Start rule -> grace, consent, cost-owner affordability, running? -> enqueue Start job (system actor, Trigger=Schedule)
        |           Stop rule  -> via pending-stop pipeline (below)
        |     2. Candidates: idle (metrics reading + active job + ServerAutomationState), max runtime (deployment.TimeStampStarted + H), next scheduled stop
        |     3. Reconcile ONE AutomationPendingStop per deployment (partial unique index Status=Pending):
        |           create (warn)  -> txn: pending row + CostAlert(kind AutoStop) + deliveries + audit AutomaticStopWarned
        |           cancel         -> txn: Status=Cancelled + skip undelivered deliveries + audit AutomaticStopCancelled + dismissal frame
        |           execute (StopsAt<=now, revalidated) -> txn: conditional claim Pending->Executed + Stop LifecycleJob(Trigger) + audit AutomaticStopRequested
        v
 After commit (best effort, never throws): CostAlertPublisher.PublishRaised/Dismissed -> user sockets; FleetChangeInterceptor -> server(id) frame
        |
 CostAlertDispatcher (existing) --> Discord DM / SMTP (Mailpit in UAT) rendered by CostAlertMessageRenderer (+ AutoStop branch)
        |
 GameServerControlPlaneWorker (existing, durable lease) claims the Stop job --> LifecycleJobExecutor.ExecuteStopAsync
        --> worker command (preserve + stop) --> ReleaseDeploymentAsync(terminate:false) --> verified non-billable release --> Succeeded
        |
 React: server(id) DTO { automation{settings,limits,idleShutdown,status,startConsent}, canManageAutomation } --> AutomationPanel,
        AutoStopNotice (shell), Overview fact, alert menu row/toast; Activity shows job.trigger + audit kinds
```

### Recommended Project Structure
```
sources/HostingContracts/
├── EAutoStopReason.cs, EAutoStopStatus.cs, EServerScheduleAction.cs,
├── EServerScheduleRunOutcome.cs, ELifecycleJobTrigger.cs, ECostAlertKind.cs    # E-prefixed enums, one per file
└── EHostingAuditEventKind.cs        # +7 members (83..89)
sources/Database/Hosting/
├── ServerAutomationSettings.cs      # 1:1 per GameServer, Version token, consent columns
├── ServerAutomationState.cs         # evaluator-owned runtime state (idle clock, watermark); never bumps Version
├── ServerSchedule.cs, ServerScheduleRun.cs, AutomationPendingStop.cs
├── CostAlert.cs                     # +Kind, +PendingStopFk (filtered unique index change)
└── LifecycleJob.cs                  # +Trigger, +TriggerLimit
sources/Database/Migrations/         # ONE migration scaffolded with dotnet ef migrations add
sources/Api/Services/ServerAutomation/        # new folder (ServerMetrics precedent)
├── ServerAutomationOptions.cs       # Hosting:Automation (limits, lead, grace, interval)
├── IServerAutomationSettingsService.cs / ServerAutomationSettingsService.cs
├── IServerAutomationEvaluator.cs / ServerAutomationEvaluator.cs
├── IdleClock.cs                     # pure: Advance(state, observation, now)
├── ScheduleCalculator.cs            # pure: occurrences in a range, next occurrence
├── ServerAutomationProjector.cs     # builds the `automation` DTO block
└── AutomationDecision*.cs           # small records (one type per file)
sources/Api/HostedServices/ServerAutomationWorker.cs
sources/Api/Controllers/GameServerAutomationController.cs  # or an action on GameServersController
sources/Api/DTO/ServerAutomationDto.cs (+ Update payload, limits, status, pending stop, consent)
sources/Api/Services/GameServerLifecycle/  # enqueue core refactor, CostAlert* extensions, interceptor extension
sources/Api/Properties/Language.resx + Language.de.resx   # AutoStop mail/Discord strings (EN + DE)
tests/Api.Tests/GameServerHosting/ (or ServerAutomation/)  # new test classes
sources/ReactApp/src/features/hosting/{automationModel.ts,useAutomationSettings.ts,useAutoStopNotice.ts,AutomationPanel.tsx}
sources/ReactApp/src/components/server/{AutomationStatusList,ScheduleRuleFields,ScheduledStartConsentDialog,AutoStopNotice}.tsx + automation.css
```

### Pattern 1: One evaluator, `Instant now` injected, no in-memory state
**What:** Mirror `CostAlertWorker` + `CostAlertEvaluator`: the worker creates a scope per pass and calls `EvaluateAsync(NodaSystemClock.Instance.GetCurrentInstant(), token)`; the evaluator never reads the clock itself. Everything needed lives in the database, so a restart neither loses nor repeats a decision.
**When to use:** Always for this phase; tests pass explicit instants.
**Example:**
```csharp
// Source: sources/Api/HostedServices/CostAlertWorker.cs (read this session) - same loop shape
using var scope = scopeFactory.CreateScope();
var result = await scope.ServiceProvider.GetRequiredService<ICostAlertEvaluator>()
    .EvaluateAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
    .ConfigureAwait(false);
```
`NodaSystemClock` is the alias `[VERIFIED: sources/Api/GlobalUsings.cs:4]` `global using NodaSystemClock = NodaTime.SystemClock;`. It is a static singleton, not an `IClock` service, so there is no seam to fake; pass `Instant now`.

### Pattern 2: Persist intent first, once-only through constraints, claim by conditional update
**What:** A warning episode is a row. The row is inserted (with the alert, deliveries and audit) in one transaction before any side effect; execution flips `Status` with a conditional `ExecuteUpdateAsync` that also inserts the job in the same transaction. Concurrency and duplicates are rejected by the database, not by in-process locks.
**Example (the claim shape to copy):**
```csharp
// Source: sources/Api/Services/GameServerLifecycle/CostAlertDeliveryProcessor.cs TryClaimAsync (read this session)
var affected = await dbContext.CostAlertDeliveries
    .Where(DuePredicate(now))
    .Where(q => q.PrimaryKey == deliveryId)
    .ExecuteUpdateAsync(setters => setters.SetProperty(q => q.Status, ECostAlertDeliveryStatus.Sending) /* ... */, cancellationToken);
return affected == 1;
```
Apply the same idea: `UPDATE AutomationPendingStops SET Status = Executed, LifecycleJobFk = @job WHERE PrimaryKey = @id AND Status = Pending` and only the winner (affected == 1) inserts the `LifecycleJob`.

### Pattern 3: Enqueue through a shared core with an optional actor
**What:** `GameServerLifecycleService.EnqueueActionAsync` rejects anything without a `ClaimsPrincipal`: it begins with `if (!principal.TryGetUserId(out var actorId) || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Operate)) throw new HostingForbiddenException(...)` and for Start calls `CanUseCostOwnerAsync(principal, ...)`. Extract everything after those checks (the server `FOR UPDATE` lock, deployment creation for Start, the Stop branch with `IsProvisioningBlocked = true` and `CancelPendingWorkAsync`, sequence allocation, audit, `SaveChanges`, commit) into a private core that takes `Guid? actorId`, an optional `LifecycleJobTrigger`, and a flag that says the caller already proved authorization. The public method keeps its checks and calls the core; a new internal method `EnqueueAutomatedActionAsync(serverId, actionId, trigger, ...)` calls it with a null actor and replaces the principal checks with the consent and affordability checks done by the evaluator. This keeps one implementation of the Stop and Start preconditions (`deployment.Status is not Running`, `hasPendingAction`, `server.IsLaunchDisabled`, frozen cost owner, unresolved resources).

### Pattern 4: Pure decision functions with a thin persistence shell
**What:** Put the arithmetic in static, side-effect-free types (`IdleClock.Advance`, `ScheduleCalculator.OccurrencesBetween`, `ScheduleCalculator.NextAfter`, `PendingStopPlanner.Plan(...)` returning create/cancel/execute decisions). Unit-test them exhaustively with explicit instants; integration-test only the persistence shell against SQLite. This is the cheapest way to cover DST, hold-on-unknown, grace windows and exactly-once.

### Anti-Patterns to Avoid
- **Terminate for automatic stops:** loses unpreserved data; also the Terminate path in `AutomaticCostStopService` is deliberately a fault path (`CancelledByCostStop`, `Priority = int.MaxValue`). Use a normal `Stop`.
- **In-memory timers (`Task.Delay` until due, `System.Threading.Timer`):** violates AUT-06. The loop only polls and decides from persisted rows.
- **Evaluating idle in the browser or accepting a client-sent `stopsAt`:** violates the UI-SPEC rule; `stopsAt`, `nextRunAt`, `idle.since` are API-calculated.
- **Writing evaluator state onto `ServerAutomationSettings`:** it would bump the concurrency `Version` on every pass and cause 409s for the owner. Keep runtime state in `ServerAutomationState`.
- **A new socket or alert pipeline:** extend `CostAlert`/`CostAlertPublisher`; do not add a second channel setting.
- **Hard-coding limits in the UI:** the API sends `limits`.
- **`LocalTime` columns:** the test context only maps `Instant` and `LocalDate` (see `NodaTimeSqliteApiDbContext`); store time of day as `MinuteOfDay` (int 0..1439).

## Data model (recommended)

Enums are `E`-prefixed per AGENTS.md. Existing enum values quoted for the planner:

`[VERIFIED: sources/HostingContracts/ELifecycleJobKind.cs]` `Start = 0, Stop = 1, Terminate = 2, Restart = 3, Install = 4, Update = 5, CollectLogs = 6, Custom = 7`.
`[VERIFIED: sources/HostingContracts/ELifecycleJobStatus.cs]` `Queued = 0, Running = 1, RetryPending = 2, WaitingForWorker = 3, Succeeded = 4, Failed = 5, Skipped = 6, CancelledByCostStop = 7`.
`[VERIFIED: sources/HostingContracts/EDeploymentStatus.cs]` `Provisioning = 0, WaitingForWorker = 1, Preparing = 2, Running = 3, Stopping = 4, PreservationFailed = 5, Terminating = 6, Stopped = 7, TerminatingDueToFault = 8, TerminatedDueToFault = 9, Failed = 10, Terminated = 11`.
`[VERIFIED: sources/HostingContracts/ECostImpact.cs]` `NoCostImpact = 0, StartsCost = 1, StopsCost = 2, KeepsCostRunning = 3, Unknown = 4`.
`[VERIFIED: sources/HostingContracts/EHostingAuditEventKind.cs]` the last member is `CostOwnerUnfrozen = 82`, so new kinds start at 83: `AutomaticStopWarned`, `AutomaticStopRequested`, `AutomaticStopCancelled`, `ScheduledStartRequested`, `ScheduledRunSkipped`, `AutomationSettingsChanged`, `ScheduledStartsConfirmed` (the exact names from the UI-SPEC).
`[VERIFIED: sources/Database/Hosting/LifecycleJob.cs]` precedent for a filtered index: `public const string ActiveDeploymentIndexFilter = "\"Status\" IN (1, 2, 3)";`.

| Entity | Key columns | Notes |
|--------|-------------|-------|
| `ServerAutomationSettings` | `GameServerFk` (unique), `IdleShutdownEnabled`, `IdleShutdownMinutes`, `MaxRuntimeEnabled`, `MaxRuntimeHours`, `TimeZone` (IANA, max 64), `Version` (int, claimed with conditional update like `CostAlertSetting.Revision`), `StartConsentCostOwnerFk`, `StartConsentByFk`, `StartConsentAt`, `StartConsentHourlyEstimateEur`, `UpdatedByFk`, timestamps | Disabled numbers keep their value (UI-SPEC). `Version` bumps only when normalized content changes (A6) |
| `ServerAutomationState` | `GameServerFk` (unique), `IdleDeploymentFk`, `IdleSince`, `IdleHeldSince`, `SchedulesEvaluatedThrough` | Written only by the evaluator; initialize the watermark to the settings save instant so enabling schedules never fires retroactively |
| `ServerSchedule` | `Id` (the rule id the UI sees), `GameServerFk`, `Action` (`EServerScheduleAction` Start, Stop), `DaysMask` (Mon=1 ... Sun=64), `MinuteOfDay`, `Position`, `EffectiveFrom` (Instant) | `EffectiveFrom` is reset when action, days or time change; an occurrence earlier than it is ignored |
| `ServerScheduleRun` | `ScheduleFk`, `GameServerFk`, `OccurrenceAt`, `Outcome`, `LifecycleJobFk?`, `RecordedAt` | Unique `(ScheduleFk, OccurrenceAt)` is the exactly-once key for schedules; cascade-deleted with the rule (audit keeps history) |
| `AutomationPendingStop` | `GameServerFk`, `DeploymentFk`, `Reason`, `Limit?`, `ScheduleFk?`, `WarnedAt?`, `StopsAt`, `Status` (Pending, Executed, Cancelled, Superseded), `CancelReason?`, `LifecycleJobFk?`, `ResolvedAt?` | Partial unique index on `DeploymentFk` where `Status = Pending`: at most one open warning per deployment. This row is the DTO `pendingStop` and the alert source |
| `CostAlert` (extend) | `+Kind` (`ECostAlertKind` CostThreshold = 0, AutoStop = 1; default 0 so existing rows stay valid), `+PendingStopFk?` | Change the existing unique index `(DeploymentFk, ThresholdAmount)` to a filtered unique index `WHERE Kind = 0`; add a unique index on `PendingStopFk`. AutoStop rows carry `ThresholdAmount = 0` and `AccruedAmount = 0` so `decimal` columns and the existing DTO types do not become nullable |
| `LifecycleJob` (extend) | `+Trigger` (`ELifecycleJobTrigger` Manual = 0, Idle, MaxRuntime, Schedule), `+TriggerLimit?` | Automatic jobs have `RequestedByFk = null`, which the projection already renders as "no actor" |
| `GameServerDefinition` (extend, API metadata) | `+ReportsPlayerCount` (bool, default false) | `true` for Arma 3, Aska and Counter-Strike 2; drives `idleShutdown.available` (structural). All three worker executors implement `ObservePlayersAsync` `[VERIFIED: grep sources/CloudWorker: Arma3WorkerCommandExecutor.Players.cs, AskaWorkerCommandExecutor.cs:160, CounterStrike2WorkerCommandExecutor.cs:186]` |

Migration: exactly one migration, scaffolded with the AGENTS.md command (`dotnet ef migrations add AddServerAutomation --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations`). Phase 66 and 67 each used one migration commit (`a5ad70c8 feat(database): add the cost alert migration`, `3937f387 feat(database): add the server metrics migration`) `[VERIFIED: git log -- sources/Database/Migrations]`. Hand-written index filters need the fluent API in `DatabaseContext` (not the `[Index]` attribute), like the existing `ActiveDeploymentIndexName`.

## Core algorithms

### Idle clock (AUT-01)
Inputs: settings (enabled, minutes N), active deployment (must be `Running`), the reading from `EvaluateCurrentAsync` (`PlayerCount` non-null = known, otherwise unavailable), whether any lifecycle job of the deployment is in `Queued`, `Running`, `RetryPending` or `WaitingForWorker` (the same set `GameServerProjectionService` uses for `pendingStatuses`, read at lines 828-835), the persisted `ServerAutomationState`.

| Condition | New state | `idle.state` |
|-----------|-----------|--------------|
| Idle disabled, no running deployment, or `ReportsPlayerCount` false | clear `IdleSince`, `IdleHeldSince` | `NotRunning` |
| Known count > 0, or an active job exists | clear both | `PlayersOnline` |
| Known count = 0 | if `IdleDeploymentFk` differs from the deployment, start `IdleSince = now`; if `IdleHeldSince` is set, shift `IdleSince` forward by `now - IdleHeldSince` and clear it | `Idle` |
| Unavailable (any reason: stale, worker-offline, measuring, query-failed) | keep `IdleSince`; set `IdleHeldSince = now` if not already held; never fire | `Unknown` |

Hold-on-unknown (not reset) is what makes idle timers survive an API restart: right after a restart `GetCurrentAsync` reports `stale` until the worker's next 15 s sample `[VERIFIED: ServerMetricsService.cs Gate()/snapshotStale; ServerMetricsOptions.ReportInterval 15 s, StaleAfter = ReportInterval * 3]`. Resetting on unknown would zero every idle clock at every restart and would let a flapping signal postpone shutdown forever.

### Stop instants and the warning window (resolution A11, recommended; needs confirmation)
Keep the approved copy true: the notice says "Nobody has been on the server for {{count}} minutes" and "The server reached its maximum runtime of {{count}} hours". Therefore the warning starts when the limit is reached and the stop follows after the warning lead:
- Idle: warn when `now >= IdleSince + N`; `StopsAt = max(IdleSince + N, now) + lead`.
- Max runtime: warn when `now >= deployment.TimeStampStarted + H`; `StopsAt = warnedAt + lead` (or the earlier of the two if the limit already passed during downtime, see below).
- Schedule: `StopsAt` = the occurrence instant; warn when `now >= occurrence - lead`.
This removes the interaction between the 10-minute lead and the 5-minute minimum idle time that D-10 wanted to avoid. Cost: an idle or max-runtime stop lands at most `lead` after the configured limit, and the panel's status rows already show the API-calculated `stopsAt`. The alternative (exact limit, warn `lead` before) needs two sentences in the notice amended to future tense and an effective lead of `min(lead, N)`. See Open Question 1.

### Pending-stop reconcile per deployment (AUT-04, AUT-05)
1. Compute candidates (idle, max runtime, next scheduled stop) and pick the one with the earliest `StopsAt`.
2. No open pending and a candidate warns now: insert `AutomationPendingStop(Pending)` + `CostAlert(AutoStop)` + `CostAlertDelivery` rows + audit `AutomaticStopWarned` in one transaction, then publish after commit.
3. Open pending whose condition no longer holds (players joined, idle turned `Unknown`, setting disabled or changed, max runtime raised, rule removed or changed, deployment no longer `Running`): conditional update to `Cancelled` (or `Superseded` when a job already exists), mark its still-`Pending` deliveries `Skipped` with a new reason `stop-resolved`, audit `AutomaticStopCancelled`, and send a dismissal frame. A cancelled-on-unknown warning is re-created with a full new lead once the signal returns, so a stop never happens without a complete warning window.
4. Open pending with `StopsAt <= now`: revalidate with a fresh reading (idle must be a known zero; settings must still demand it; for a schedule the rule must be unchanged and `EffectiveFrom <= occurrence`), then in one transaction claim `Pending -> Executed` (conditional update), insert the `Stop` job through the enqueue core with `Trigger` and `TriggerLimit` snapshots, and write `AutomaticStopRequested`. If the enqueue throws `HostingConflictException` because another action is pending, leave the row `Pending` and retry on the next pass; if the deployment is no longer `Running`, mark `Superseded`.
5. Due with no pending row (the platform was down through both the warning and the stop): for max runtime and schedule, create the row with `WarnedAt = null` and execute immediately, with audit detail "came due while the platform was not running; no warning could be sent" (A5). For idle, always warn first (a fresh live reading is required anyway).
6. Stop-occurrence relevance rule: a scheduled Stop occurrence is executed only if the active deployment was already running at the occurrence instant (`deployment.TimeStampStarted <= occurrence`); otherwise record `SkippedMissed`. Without this, a manual start after a long outage would be killed immediately by a stale occurrence from before the outage.

### Schedules (AUT-03)
- Occurrence calculation: `ZoneLocalMapping` with `Resolvers.LenientResolver`. `[CITED: nodatime.org/3.3.x/api/NodaTime.TimeZones.Resolvers.html]` the lenient resolver never throws, resolves ambiguous times to the earlier of the two matches, and moves skipped times forward by the length of the gap (behavior since Noda Time 2.0). This matches A3 for ambiguous times; for a skipped time the run lands at "wall time + gap length" (for example 02:30 becomes 03:30 in a one hour gap), which is later than the first valid instant 03:00. Document the sentence in tests; derive transition instants from `zone.GetZoneIntervals`, do not hard-code dates.
- Zone validation: `DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)`; reject null (injection and typos). `Intl.supportedValuesOf("timeZone")` ids in the browser are IANA ids, the same vocabulary `[ASSUMED]`.
- Scan: per server, `OccurrencesBetween(rule, zone, from: max(watermark, rule.EffectiveFrom), to: now]` in chronological order; insert one `ServerScheduleRun` per occurrence (unique key = once-only), advance the watermark in the same transaction. A crashed pass repeats the scan and the unique index swallows duplicates.
- Start rule outcome order: `SkippedMissed` (older than `StartGraceWindow`, default 15 min, A4) -> `SkippedConsent` (consent cost owner differs from `CurrentCostOwnerFk`, or never given) -> `SkippedRunning` (an unresolved deployment exists) -> `SkippedBusy` (a pending action exists or the enqueue conflicts) -> `SkippedCost` (cost owner frozen or cannot reserve the first slice) -> `Started` (job enqueued). Stop rule outcomes: `Stopped`, `SkippedOff`, `SkippedMissed`, `SkippedBusy`. `Failed` is derived at read time when the run's job later ended `Failed` (the UI shows "did not finish" with the activity link). Each skip writes `ScheduledRunSkipped` audit with the reason in `Summary`/detail.
- Cost-owner-can-pay: the executor already enforces affordability at Start (`ReserveInitialSliceAsync` throws `HostingAffordabilityException` and `RejectStartAsync` code `provider-cost-unaffordable`, `LifecycleJobExecutor.cs:326-366`); the formula is `availableAfter = owner.Balance + (owner.Allowance ?? 0m) - otherReservations - amount` compared with `policy.MinimumSafetyRangeEur` (`HostingCostSliceService.cs:60-76`). For the early `SkippedCost` outcome, extract that formula into a shared read-only method on `IHostingCostSliceService` (for example `CanReserveInitialSliceAsync`) so the pre-check and the reservation cannot drift (the ROADMAP's Phase 64 note says to add such a signal freely). The executor remains authoritative; a start that still fails shows as a failed job.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Zone-aware recurrence, DST | Manual UTC offset math | NodaTime `DateTimeZone` + `LenientResolver` | Gaps and overlaps are the classic scheduler bug |
| Idle reading and "unavailable vs zero" | A second player-count query | `IServerMetricsService.EvaluateCurrentAsync(serverId, now, ct)` | Decides count vs reason in one place (the UI shows the same reading); the API never queries a game |
| Alert records, per-channel outbox, retry, dismissal, audience | A new notification pipeline | Extend `CostAlert`, `CostAlertDelivery`, `CostAlertDeliveryProcessor`, `CostAlertPublisher`, `CostAlertRecipientResolver` | At-least-once outbox with leases, deterministic email Message-Id, per-viewer dismissals already exist and are tested |
| Durable stop | Direct provider calls or a new teardown | A `Stop` `LifecycleJob` through the enqueue core | `ExecuteStopAsync` -> `ReleaseDeploymentAsync(terminate:false)` is the verified non-billable path |
| Server mutation lock | Custom locking | Server-row `FOR UPDATE` on PostgreSQL, a no-op `UPDATE ... SET TimeStampUpdated = TimeStampUpdated` on SQLite (pattern in `AutomaticCostStopService` and `BeginServerMutationAsync`) | Same transaction semantics for both providers used by the tests |
| Email/Discord text | String concatenation | `CostAlertMessageRenderer` pattern (`Language.resx` / `Language.de.resx`, `SanitizeTitle`) | Title sanitation prevents header injection; locale from the recipient |
| Optimistic concurrency | `rowversion`/xmin tricks | Integer `Version` claimed with conditional `ExecuteUpdateAsync` (`CostAlertSettingsService.SaveAsync`) | Works identically on SQLite and PostgreSQL |
| Exactly-once delivery of a due action | An in-process "already ran" set | Unique indexes plus conditional claim | Survives restart and a second instance |

**Key insight:** every durability property the phase needs (once-only, restart survival, redelivery) already has a proven mechanism in Phase 66 or the lifecycle queue; the plan should look like copy-and-adapt of those mechanisms, with the new logic confined to pure date and idle arithmetic.

## Runtime State Inventory

Not a rename or migration phase. Omitted. One migration-adjacent note: existing `CostAlerts` rows must get `Kind = 0` (the column default) and the existing unique index is replaced by a filtered one; verify the migration's `Up` on a database that already contains alerts (tests use `EnsureCreatedAsync`, so add a note to run the migration against the UAT database through `-Fresh`-less restart once).

## Common Pitfalls

### Pitfall 1: The new audit kinds never appear in Activity
**What goes wrong:** `GameServerProjectionService.GetOperationsAsync` filters on a hard-coded `operationalKinds` array (read lines 614-642); a kind not in it is never returned.
**How to avoid:** add all seven kinds to the array and to `OperationStatus` (`ScheduledRunSkipped` and `AutomaticStopCancelled` map to `"warning"` per the UI-SPEC tone; `AutomaticStopRequested` and `ScheduledStartRequested` are echo kinds the client dedupes). Test through `GetOperationsAsync`.
**Warning signs:** audit rows exist in the database but the Audit filter is empty.

### Pitfall 2: No live refresh of the notice and status list
**What goes wrong:** `FleetChangeInterceptor.Collect` only watches `GameServer`, `GameServerDeployment` and `LifecycleJob` entries (read lines 230-257), so saving settings or creating a pending-stop row emits no frame and the UI waits for the 30 s fallback.
**How to avoid:** add `ServerAutomationSettings` and `AutomationPendingStop` (added or modified) to `Collect` with `GameServerFk`, plus a test beside `FleetChangeInterceptorTests`. Do not add `ServerAutomationState` (it changes on every pass).

### Pitfall 3: System-initiated enqueue fails authorization
**What goes wrong:** calling `EnqueueActionAsync` with a synthetic principal either throws `HostingForbiddenException` or smuggles an administrator claim.
**How to avoid:** the shared-core refactor (Pattern 3). Never fabricate claims.

### Pitfall 4: Idle clock reset on every restart or signal blip
See "Idle clock": hold, do not reset; shift `IdleSince` by the held duration on recovery.

### Pitfall 5: Stale scheduled Stop kills a freshly started server
See rule 6 under the reconcile: relevance check against `deployment.TimeStampStarted`.

### Pitfall 6: Retroactive firing when a schedule is added or edited after downtime
**What goes wrong:** the watermark may be old (API was down), so a newly saved rule would "fire" for occurrences before it existed.
**How to avoid:** `EffectiveFrom` per rule, reset on change; scan from `max(watermark, EffectiveFrom)`.

### Pitfall 7: Evaluator writes bump the user-visible `Version`
Use `ServerAutomationState`; bump `Version` only on a PUT whose normalized content differs (A6: a consent-only PUT keeps the version).

### Pitfall 8: SQLite versus PostgreSQL differences in tests
`[VERIFIED: CostAlertEvaluator.cs remarks]` "Amounts are compared in memory because the test provider stores decimals as text and cannot compare them in SQL"; `Instant` is mapped to long in `NodaTimeSqliteApiDbContext` (so `Instant` comparisons in SQL work, as `CostAlertDeliveryProcessor.DuePredicate` proves), `LocalTime` has no converter, `FOR UPDATE` does not exist in SQLite. Keep decimals out of predicates, use `MinuteOfDay`, copy the provider branch from `AutomaticCostStopService`.

### Pitfall 9: Cancel races a pending stop that is being delivered
A pending row cancelled after its deliveries were claimed still sends. Mark undelivered `Pending` deliveries `Skipped` (`stop-resolved`) in the cancel transaction; accept that a `Sending` row may still go out (at-least-once, documented), and make the message text neutral enough ("shuts down at ... unless someone joins" for idle).

### Pitfall 10: Control plane paused by default
`[VERIFIED: HostingControlPlaneState.cs]` when `Hosting:ControlPlane:Paused` is unset the state is `IsPaused = true`, and `AddHostingControlPlane` registers the hosted services only when `!state.IsPaused`; the pause middleware also answers 503 `hosting_control_plane_paused` for non-read `/game-servers` requests. The automation worker must be registered in that same block, and the PUT inherits the pause behaviour. The UI must treat a 503 like a save error.

### Pitfall 11: Group cost owners and the alert audience
`CostAlertRecipientResolver` resolves a personal owner or every holder of `co:use` for a group owner. The warning is addressed to those recipients (the people who pay), not to the user who saved the settings. The in-app workspace notice reaches every viewer through the server DTO. Per-viewer dismissal (`CostAlertDismissal`, unique `(CostAlertFk, UserFk)`) is unchanged: dismissing hides the record only for that viewer `[VERIFIED: CostAlertService.DismissAsync / CostAlertDismissal.cs]`.

### Pitfall 12: The emulated worker cannot finish a Stop
`UatWorkerChannelEmulator` only sends heartbeat and metrics frames and drains the rest (`DrainAsync` discards everything, read this session), and `WaitForCommandAsync` loops until the worker command succeeds, fails or is superseded (`LifecycleJobExecutor.cs:2116-2147`). A stop job on the UAT stack will therefore sit in `Running`/`WaitingForWorker` unless the emulator claims and succeeds the stop command (a 67-UAT item for the manual stop was left "skipped: Deferred follow-up", so completion on the emulated stack is unproven `[ASSUMED]`). See Open Question 3.

## Resolved UI-SPEC assumptions A1 to A10 and the two unresolved questions

| Id | Recommendation | Basis |
|----|----------------|-------|
| A1 (who may configure) | `canManageAutomation` = `HasServerPermission(principal, serverId, Claims.ResourceBased.Server.Modify)` (administrators, `sv` holders, `sv:modify` holders) AND (administrator OR `CanUseCostOwnerAsync(principal, server.CurrentCostOwnerFk)`). The second clause is what lets the consent be "bound to the cost owner" honestly. Not archived. Read-only notice copy: "the server owner or an administrator" is acceptable shorthand; amend only if the user wants it precise. | `[VERIFIED: HostingAuthorizationService.cs:69-96]` `HasServerPermission` returns true for administrators and `sv` holders; `CanUseCostOwnerAsync` rule read. `[VERIFIED: Claims constants]` `public const string All = "sv"; ... Modify = "sv:modify"; Operate = "sv:operate";`. A member with only `Operate` can still stop manually, so they should not rewrite automation. Decision about the cost-owner clause is `[ASSUMED]` (Assumption A-3) |
| A2 (activity) | Activity = known player count > 0 OR any lifecycle job of the active deployment in `Queued/Running/RetryPending/WaitingForWorker`; the warning is withdrawn automatically when it resumes (cancel path above) | Same pending-status set as the projection |
| A3 (DST) | NodaTime `LenientResolver` (ambiguous = earlier, skipped = shifted forward by the gap) | `[CITED: nodatime.org/3.3.x/api/NodaTime.TimeZones.Resolvers.html]` |
| A4 (missed) | Stops execute once if the deployment was running at the occurrence; starts execute only within `StartGraceWindow` (default 15 min), else `SkippedMissed`; a pending stop found due after downtime executes at the first pass | Exactly-once via unique run row and conditional claim |
| A5 (no warning when due at startup) | Confirmed for max runtime and schedule (immediate execution, audit detail says so). Idle always receives a fresh warning window | Idle needs a live reading anyway |
| A6 (consent) | Consent columns on the settings row bound to `StartConsentCostOwnerFk`; state is computed: no Start rule = `NotRequired`, consent cost owner equals current cost owner = `Given`, otherwise `Needed`. No hook is needed on `ChangeCostOwnerAsync` because the comparison happens at read and at run time. A consent-only PUT keeps `Version` | `[VERIFIED: GameServerLifecycleService.cs:723]` `ChangeCostOwnerAsync` exists and changes `CurrentCostOwnerFk`; `CostOwnerChanged` audit exists |
| A7 | One PUT replaces everything; new rules send `id = null` and the API assigns the Guid; the response returns the saved settings with ids | Mirrors `CostAlertSettingsController.UpdateAlertSettingsAsync` |
| A8 | Options `Hosting:Automation`: `IdleMinutesMin/Max` (5/1440), `MaxRuntimeHoursMin/Max` (1/168), `MaxSchedules` (8), all sent in `limits`; UAT overrides the idle minimum to 1 | `CostAlertOptions` (`Hosting:CostAlerts`, `Validate()`) is the options precedent |
| A9 | `WarningLead` option default 10 minutes, sent as `warningLeadMinutes`; UAT 1 to 2 minutes | Option precedent |
| A10 | Max runtime counts from `GameServerDeployment.TimeStampStarted`, which the executor sets once when the Start job completes `[VERIFIED: LifecycleJobExecutor.cs:798-803]` `deployment.Status = EDeploymentStatus.Running; ... deployment.TimeStampStarted ??= now;` | A restart job does not reset it |
| Unresolved 1 (who receives `canManageAutomation`) | See A1 | |
| Unresolved 2 (group dismissal) | Per viewer, exactly as Phase 66 (`CostAlertDismissal` is per `(alert, user)`); a record dismissed by one recipient stays visible to the other holders | `[VERIFIED: CostAlertDismissal.cs, CostAlertService.cs]` |

## API surface (additive, recommended names)

| Change | Detail |
|--------|--------|
| Server DTO | `GameServerDto.Automation` (nullable block: `settings`, `limits`, `idleShutdown{available}`, `status{idle, maxRuntimeStopsAt, nextScheduled, pendingStop}`, `startConsent{state}`) and `CanManageAutomation` (bool). Fill the block only in `GetServerAsync` (detail), not in the list and notifications paths: `ProjectServerAsync` is shared by `GetServersAsync` (line 194), `GetServerAsync` (line 214) and the notifications projection (line 550), and the block needs a metrics read and schedule math per server. Add an `includeAutomation` parameter |
| New endpoint | `PUT game-servers/{gameServerId}/automation` returning the saved block; statuses 200, 400 (field-addressable problem details; the existing hosting controllers return `{ error }` only, so add a `fields` array), 403, 404, 409, 503 (paused). Add the `HostingForbiddenException`/`HostingValidationException`/`HostingConflictException` mapping like `CostAlertSettingsController.TryMapException` |
| Job DTO | `LifecycleJobDto.Trigger` and `TriggerLimit` (absent means Manual); `ProjectJob` already maps `RequestedByName` null to "no actor" |
| Alert | `CostAlertDto` gains `Kind`, `StopsAt`, `StopReason`, `StopLimit`; `CostAlertRaisedMessage` gains the same optional members; `CostAlertService.ListAsync` must exclude `AutoStop` alerts whose pending row is no longer `Pending`; resolution sends `CostAlertDismissedMessage` frames to the cost-owner audience through `GetCostOwnerViewAudienceAsync` |
| Audit | seven kinds (see Data model); `ScheduledRunSkipped` summary carries the reason (`ApiText` on the client) |
| OpenAPI | after the code change run the export `dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <path>` to refresh `sources/ReactApp/openapi/utf-api.json`, then `npm run contracts:generate`; CI compares the snapshot byte for byte `[CITED: .planning/phases/62-fleet-quick-actions/62-02-PLAN.md line 80]`. The Kiota client under `sources/WebApp/UtfApi` is only regenerated when a change would break the legacy WebApp (AGENTS.md); every change here is additive |
| Messages | `Language.resx` and `Language.de.resx` keys for the AutoStop subject, one body per reason (idle, max runtime, schedule), footer; render through a new `RenderAutoStop` beside `CostAlertMessageRenderer.Render` (same `SanitizeTitle`, same UTC time format, same server link); `CostAlertDeliveryProcessor.DeliveryFacts` must carry `Kind`, `StopsAt`, `StopReason`, `StopLimit` and branch in `Render` |

## Code Examples

### Interceptor extension shape
```csharp
// Source: sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs Collect (read this session) - add siblings of this loop
foreach (var entry in context.ChangeTracker.Entries<LifecycleJob>())
{
    if (!IsRelevant(entry)) continue;
    (serverIds ??= []).Add(entry.Entity.GameServerFk);
}
// add: Entries<ServerAutomationSettings>() and Entries<AutomationPendingStop>() for Added/Modified, using GameServerFk
```

### Persist-with-state audit (single SaveChanges)
```csharp
// Source: AutomaticCostStopService.TriggerAsync (read this session): the job, the state change and the audit row share one SaveChanges
dbContext.LifecycleJobs.Add(job);
auditWriter.Add(dbContext, EHostingAuditEventKind.Recovery, "...", serverId: deployment.GameServerFk,
    deploymentId: deployment.PrimaryKey, lifecycleJobId: job.PrimaryKey, costOwnerId: deployment.CostOwnerFk,
    details: "...", costImpact: ECostImpact.StopsCost);
await dbContext.SaveChangesAsync(cancellationToken);
```
For automation use the new kind (`AutomaticStopRequested`, `costImpact: ECostImpact.StopsCost`; `ScheduledStartRequested`, `ECostImpact.StartsCost`) and pass `actorId: null`.

### Pure schedule occurrence (illustrative)
```csharp
// Source: NodaTime public API (LenientResolver per nodatime.org docs); names illustrative, verify signatures when implementing
var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(settings.TimeZone) ?? throw new HostingValidationException("...");
var local = date.At(LocalTime.FromMinutesSinceMidnight(rule.MinuteOfDay));
var instant = zone.ResolveLocal(local, Resolvers.LenientResolver).ToInstant();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| NodaTime 1.x lenient resolver (ambiguous = later, skipped = start of next interval) | 2.0+: ambiguous = earlier, skipped = shifted forward by gap | Noda Time 2.0 `[CITED: nodatime.org/3.3.x/api/NodaTime.TimeZones.Resolvers.html]` | Do not copy older blog snippets; use 3.3.2 behavior |
| ROADMAP hint: reuse Terminate path | Normal `Stop` job through a shared enqueue core | This research | Keeps data; same verified teardown |

**Deprecated/outdated:** `Server_Settings_AutomationUnavailable` stops being used (kept in locale files until the Phase 69 cleanup, UI-SPEC).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A-1 | Warning semantics A11: warn at the limit, stop `lead` later (idle and max runtime); schedule stops exact, warned `lead` earlier | Core algorithms | If the user wants "never longer than H hours" exactly, two notice sentences need rewording and the effective lead becomes `min(lead, N)` |
| A-2 | Automatic stop uses the `Stop` job and does NOT auto-escalate to `Terminate` when preservation fails or the worker is lost (the existing heartbeat-stale fault cost-stop still applies) | Summary, Anti-patterns | If the user prefers guaranteed billing stop over data, add an escalation path (data loss decision) |
| A-3 | `canManageAutomation` = Modify AND (admin OR can use the cost owner) | Resolved A1 | A modifier without `co:use` sees read-only; widening it requires a per-feature consent right |
| A-4 | Pending warnings are addressed to cost-owner recipients (people who pay) rather than to the user who saved the settings | Pitfall 11 | If the "owner" in AUT-04 means the server creator, add that user to the recipient set |
| A-5 | TZDB is embedded in NodaTime (no OS tzdata needed) and `Intl.supportedValuesOf("timeZone")` ids are accepted by `Tzdb.GetZoneOrNull` | Schedules | An unknown browser zone id would be rejected with a 400; mitigate with the tested option list |
| A-6 | The UAT emulated stack can complete a Stop job only after extending the emulator to succeed worker commands | Pitfall 12 | UAT would show a never-finishing stop; fall back to API integration tests for completion |
| A-7 | `ServerMetricSnapshot` reading is acceptable for idle: `EvaluateCurrentAsync` per server per pass is cheap for servers with idle enabled and a `Running` deployment | Idle clock | Heavy fleets may need batching; the pass is already restricted to servers with settings |
| A-8 | Idle stays counted across an API restart (downtime counted as idle if the first fresh reading is zero) | Idle clock | Players who joined and left during downtime are not seen; accepted trade-off |

## Open Questions

1. **Warning semantics for idle and max runtime**
   - What we know: the approved notice copy states the limit has been reached; D-10 wants no interaction between lead and minimum idle time.
   - What's unclear: whether a stop `lead` after the limit is acceptable for "maximum runtime".
   - Recommendation: adopt A11 (warn at the limit, stop after the lead); if the user insists on an exact limit, amend `Server_Automation_Notice_Reason_Idle_*` and `_Max_*` to future tense and use `min(lead, N)`. Decide at plan review.
2. **Who is "the owner" for AUT-04 delivery**
   - What we know: Phase 66 delivers to cost-owner recipients with the cost owner's channel flags; D-09 of the UI-SPEC says the same.
   - What's unclear: whether the server creator should also be a recipient when the cost owner is someone else.
   - Recommendation: keep cost-owner recipients only (A-4); every viewer sees the in-app notice.
3. **Can the UAT stack finish a Stop?**
   - What we know: the emulator ignores commands; Phase 67's manual-stop UAT was deferred.
   - What's unclear: whether the emulated stop completes today.
   - Recommendation: add a UAT-only emulation step that claims and succeeds the stop command (or generalize the emulator to several servers and make it ack the `stop` action) and treat it as a Wave task with its own test; if it proves too large, assert job creation, trigger line and audit in UAT and leave completion to integration tests plus the deferred real-environment check.
4. **Automatic stop when the worker is gone or preservation fails**
   - Recommendation: no escalation in this phase (A-2); the attention state and the existing fault cost-stop on stale heartbeat cover it. Confirm with the user.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.201 | none needed |
| dotnet-ef | migration scaffold (AGENTS.md) | yes | 10.0.9 `[VERIFIED: dotnet tool list -g]` | none |
| Node | ReactApp tests, `contracts:generate` | yes | v25.5.0 | none |
| Podman / Docker | seeded UAT stack (Aspire PostgreSQL, Mailpit) | yes (podman and docker found on PATH) | not probed | `Start-Uat.ps1` prefers podman |
| Kiota global tool | legacy WebApp client (only if breaking) | yes | 1.32.2 | skip when additive |
| PostgreSQL / Mailpit | UAT only | started by AppHost | — | SQLite in-memory for unit and integration tests |

No blocking missing dependency. The app and builds were not started or run in this research, per the instruction that another agent is using the build outputs.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with SQLite in-memory (`HostingTestStore`, `NodaTimeSqliteApiDbContext`); Vitest + Testing Library; Playwright mocked e2e (`tests/e2e`) and seeded-stack UAT (`tests/uat`, config `sources/ReactApp/playwright.uat.config.ts`, tags `@fresh` and `@restarted`, selected by `UAT_PHASE`) |
| Config file | `tests/Api.Tests/Api.Tests.csproj`, `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `sources/ReactApp/playwright.uat.config.ts` |
| Quick run command | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServerAutomation"` and `npm --prefix sources/ReactApp test -- tests/hosting/automation` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |

The UAT suite exists in the working tree (`cost-accrued.spec.ts`, `cost-alerts.spec.ts`, `cost-design-shots.spec.ts`, `metrics.spec.ts`, `support/uat.ts`; it is currently uncommitted per `git status`), so Phase 68 adds `tests/uat/automation.spec.ts` in the same style (`uatTitles`, `listMail`, `evidence`, `readHandOver`/`writeHandOver` helpers).

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUT-01 | Idle clock: zero starts, count resets, unknown holds and shifts, active job counts as activity, disabled/not running clears, structural unavailable never arms | unit (pure `IdleClock`) | `dotnet test tests/Api.Tests --filter FullyQualifiedName~IdleClockTests` | ❌ Wave 0 |
| AUT-01 | Settings PUT validates range from options, zone, rules; 403/404/409 mapping; non-manager sees `canManageAutomation=false`; `idleShutdown.available` follows `ReportsPlayerCount` | integration | `... --filter FullyQualifiedName~ServerAutomationSettingsTests` | ❌ Wave 0 |
| AUT-01 | Panel read-only with notice for a non-manager; unavailable block; structural reason; draft/dirty/invalid/save/409/403; leave guard shared with staged changes | Vitest + Playwright e2e | `npm --prefix sources/ReactApp test -- tests/hosting/automation-model.test.ts tests/hosting/automation-panel.test.tsx` and `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts` | ❌ Wave 0 |
| AUT-02 | Max runtime from `TimeStampStarted`: warns at limit, stops after lead, cancels when raised, not armed before `Running` | integration | `... --filter FullyQualifiedName~MaxRuntimeTests` | ❌ Wave 0 |
| AUT-03 | `ScheduleCalculator`: weekdays, zone, DST gap and overlap (instants derived from the zone), `EffectiveFrom`, watermark windows | unit | `... --filter FullyQualifiedName~ScheduleCalculatorTests` | ❌ Wave 0 |
| AUT-03 | Start rule outcomes: Started, SkippedMissed (beyond grace), SkippedConsent (cost owner changed), SkippedRunning, SkippedBusy, SkippedCost; Stop rule: Stopped, SkippedOff, SkippedMissed (deployment started after the occurrence) | integration | `... --filter FullyQualifiedName~ScheduledRunTests` | ❌ Wave 0 |
| AUT-03 | Consent dialog on new/changed Start rule, consent-needed notice, Remove/Add focus, time zone select, duplicate detection | Vitest + Playwright | as AUT-01 UI | ❌ Wave 0 |
| AUT-04 | Pending row + alert + deliveries + audit in one transaction; Discord/email delivered through `RecordingDiscordDirectMessageSender`/`RecordingEmailSender`; cancel marks deliveries `stop-resolved` and sends a dismissal; per-viewer dismissal; list excludes resolved alerts; rendering EN and DE | integration | `... --filter FullyQualifiedName~AutoStopWarningTests` | ❌ Wave 0 |
| AUT-04 | Shell notice for every viewer (link only for managers), toast once per new alert id, cancellation toast, menu row cap and badge | Vitest | `npm --prefix sources/ReactApp test -- tests/hosting/auto-stop-notice.test.tsx tests/hosting/cost-alert-menu.test.tsx` | ❌ Wave 0 (menu test exists, extend) |
| AUT-05 | Automatic stop creates a normal `Stop` job (Priority 50, system actor, trigger snapshot), audit `AutomaticStopRequested`, Activity shows trigger line; job completes only via verified release (reuse `LifecycleJobExecutorCloudTests` harness with `TestRuntimeProvider`) | integration | `... --filter FullyQualifiedName~AutomaticStopJobTests` | ❌ Wave 0 |
| AUT-05 | `GetOperationsAsync` returns the seven kinds with correct status; interceptor emits a frame for settings and pending-stop changes | integration | `... --filter "FullyQualifiedName~AutomationOperationsTests|FullyQualifiedName~FleetChangeInterceptorTests"` | ❌ Wave 0 / ✅ interceptor tests exist |
| AUT-06 | Restart simulation: pass at T0 (state persisted), new scope and evaluator at T0 + due, assert exactly one job; double evaluation at the same `now` and concurrent evaluators yield one job, one alert, one run row; idle clock survives a new evaluator | integration | `... --filter FullyQualifiedName~AutomationRestartTests` | ❌ Wave 0 |
| AUT-06 | Seeded-stack: after `Start-Uat.ps1` restart without `-Fresh`, the Activity entry is single (`@restarted`) | Playwright UAT | `npm --prefix sources/ReactApp run test:uat` with `UAT_PHASE=restarted` | ❌ Wave 0 |
| All | Parity rows `AUT-settings-panel`, `AUT-warning-notice`, `AUT-activity-trigger`, `AUT-overview-fact`; copy guards (banned button labels, cost-stop vocabulary), locales EN/DE, no `setInterval` or `refetchInterval` in automation files, contract check | Vitest | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` and `npm --prefix sources/ReactApp run locales:check` / `contracts:check` | ✅ guards exist, extend |
| All | axe (WCAG 2.1 AA) on panel states, notice and consent dialog, no horizontal scroll at 320 and 390, EN and DE | Playwright | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-a11y.spec.ts` | ✅ extend |

### Seeded-stack UAT design (carry-forward agreement)
Extend `UatFleetScenarioSeeder` (scenario list near `Scenarios`, helpers `AddServer`, `AddPrimaryWorker`, `UatMetricHistorySeeder`) with titles `UAT 11 ...` upward, all credited by the existing funded cost owner:
- Idle: a running server with a primary worker row, idle enabled at the UAT minimum (UAT option `IdleMinutesMin = 1`), and an emulated worker that reports `PlayerCount = 0` (generalize `UatWorkerChannelEmulator.PrepareAsync`, which currently resolves a single server by `MetricsScenarioTitle`, into several sessions with a per-scenario player profile).
- Max runtime: `TimeStampStarted = now - (H - ~2 min)` with H = 1 hour and the same worker feed with players above zero (proves players do not prevent a max-runtime stop).
- Schedules: Stop and Start rules whose next occurrence is one to two minutes after the seed (UTC zone, all days), plus a rule that gets skipped for cost (a server whose current cost owner is a second `CostOwner` with no credit) and a server whose consent cost owner differs from its current one (consent-needed notice).
- UAT options (via `appsettings` keys through AppHost `WithEnvironment`, like `Hosting__ControlPlane__Paused`): `Hosting__Automation__WarningLead` about 1-2 minutes, `EvaluationInterval` about 10 s, `StartGraceWindow`.
- The mail assertion uses Mailpit (`listMail` helper); the warning notice, relative line, menu row, toast, Activity trigger line and single audit entry are asserted live; completion of the Stop in UAT depends on Open Question 3.
- Per the user's feedback memory: run the Playwright UAT against the seeded stack before handing checkpoints to the user, include copy-paste start/stop/seed commands, and mark real-environment and post-gap-closure re-checks as "Deferred follow-up:" skips.

### Sampling Rate
- **Per task commit:** the quick filtered command for the touched area
- **Per wave merge:** full `dotnet test X39.UnitedTacticalForces.slnx` plus `npm run ci` for React waves
- **Phase gate:** full suites green, OpenAPI snapshot and generated types in sync, seeded UAT run recorded before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/Api.Tests/.../IdleClockTests.cs`, `ScheduleCalculatorTests.cs`, `ServerAutomationSettingsTests.cs`, `ScheduledRunTests.cs`, `MaxRuntimeTests.cs`, `AutoStopWarningTests.cs`, `AutomaticStopJobTests.cs`, `AutomationRestartTests.cs`, `AutomationOperationsTests.cs` plus a `ServerAutomationTestData` helper (seed settings, rules, a running deployment with a primary worker and a metrics snapshot; use `HostingTestData.SeedServerAsync`, `ServerMetricsTestData`)
- [ ] `sources/ReactApp/tests/hosting/automation-*.test.ts(x)`, `tests/e2e/hosting-automation.spec.ts`, `tests/e2e/support/automationMocks.ts`, `tests/uat/automation.spec.ts`
- [ ] Framework install: none (all frameworks present)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no new auth) | existing cookie/bearer pipeline |
| V3 Session Management | no | existing |
| V4 Access Control | yes | `HasServerPermission` (Modify) plus `CanUseCostOwnerAsync`; non-viewers get 404 not 403; `canManageAutomation` computed server-side; consent bound to cost owner id |
| V5 Input Validation | yes | Server-side limits from options, IANA id validated against TZDB, weekday mask 1..127, minute 0..1439, rule count cap, duplicate (day, time) rejection; field-addressable 400 |
| V6 Cryptography | no | no secrets handled; nothing hand-rolled |
| V7 Error Handling and Logging | yes | Audit every settings change, consent, warning, cancellation, request and skip with ids; log ids and fixed reason codes only; sanitize titles (`CostAlertMessageRenderer.SanitizeTitle`, `HostingFailureSanitizer`) |
| V11 Business Logic | yes | Once-only via unique indexes and conditional claims; scheduled start needs recorded consent and an affordability check; rate bounds (max rules, evaluator bounded to servers with automation); stale `Version` gives 409 |
| V13 API | yes | Additive OpenAPI; `[Authorize]` controller; pause middleware applies |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR on `gameServerId` | Information disclosure / Elevation | Resolve permission before loading; 404 for non-viewers; never trust ids in the body |
| Member without the right changes automation or gives consent | Elevation | `canManageAutomation` server-side on PUT; consent requires `CanUseCostOwnerAsync` |
| Cost-bearing start the owner never confirmed | Tampering / repudiation | Consent row bound to cost owner id; mismatch means `SkippedConsent`; audit `ScheduledStartsConfirmed` records who and the hourly estimate |
| Double stop or double start from restart, two instances or retries | Tampering | Unique `(ScheduleFk, OccurrenceAt)`, partial unique pending-per-deployment, conditional claim, server-row lock during enqueue |
| Warning spam | Denial of service | One pending row per episode, one alert per pending (unique `PendingStopFk`), idempotent deliveries |
| Header/Discord injection through the server title | Tampering | `SanitizeTitle`; deterministic Message-Id |
| Unbounded schedules or evaluator load | Denial of service | `MaxSchedules` (8), pass restricted to servers with settings, per-server try/catch |
| Time zone id abuse | Tampering | Accept only TZDB ids from `DateTimeZoneProviders.Tzdb` |
| Information leak of cost owner via the pending-stop DTO | Information disclosure | The block carries no owner names; the consent dialog facts use the existing `canSeeOwner` privacy rule (`CostOwnerName` fallback "Assigned owner") |

## Suggested plan slicing (for the planner)

1. Contracts, entities, enums, audit kinds, options, one scaffolded migration; `ReportsPlayerCount` on definitions.
2. Pure calculators (`IdleClock`, `ScheduleCalculator`, planner) with exhaustive unit tests.
3. Lifecycle enqueue-core refactor with system actor, job trigger, affordability pre-check method; tests proving the manual path is unchanged.
4. Settings service, DTO block, controller PUT, projection (`canManageAutomation`, `automation`, job `trigger`), operations whitelist and statuses, interceptor extension.
5. Evaluator, worker registration, pending-stop pipeline, alert extension (entity, evaluator branch, publisher, resolution, delivery render, resources EN/DE), restart and concurrency tests.
6. OpenAPI snapshot, generated types, parity matrix rows, locale keys.
7. React model and hook, panel, rule fields, consent dialog, notice, Overview fact, alert row and toast, Activity trigger/audit labels, leave-guard sharing.
8. UAT seed scenarios, emulator generalization, UAT options, mocked e2e and seeded-stack UAT specs, axe and screen capture.

## Sources

### Primary (HIGH confidence, read in this session)
- `sources/Api/Services/GameServerLifecycle/AutomaticCostStopService.cs`, `HostingCostReconciler.cs`, `GameServerLifecycleService.cs` (lines 302-585, 723, 1160-1170), `LifecycleJobExecutor.cs` (lines 60-150, 300-370, 780-810, 1180-1321, 2116-2200, 2921-2990, 3117-3145), `HostingAuthorizationService.cs` (lines 60-110), `HostingAuditWriter.cs`, `GameServerProjectionService.cs` (lines 560-870, 822-935), `FleetChangeInterceptor.cs`, `HostingCostSliceService.cs` (lines 30-100), `CostAlertEvaluator.cs`, `CostAlertPublisher.cs`, `CostAlertDeliveryProcessor.cs`, `CostAlertService.cs`, `CostAlertSettingsService.cs`, `CostAlertRecipientResolver.cs`, `CostAlertOptions.cs`, `CostAlertDeliveryReasons.cs`, `HostingControlPlaneServiceCollectionExtensions.cs`, `HostingControlPlaneState.cs`
- `sources/Api/HostedServices/CostAlertWorker.cs`, `CostAlertDispatcher.cs`, `HostingCostReconciliationWorker.cs`, `GameServerControlPlaneWorker.cs`, `UatWorkerChannelEmulator.cs`, `UatWorkerChannelEmulatorOptions.cs`
- `sources/Api/Services/ServerMetrics/ServerMetricsService.cs`, `ServerMetricsOptions.cs`; `sources/Database/Hosting/*` (GameServer, GameServerDeployment, LifecycleJob, CostAlert, CostAlertDelivery, CostAlertDismissal, CostAlertSetting, ServerMetricSnapshot, ServerMetricBucket); `sources/HostingContracts/E*.cs`; `sources/HostingContracts/WorkerProtocol/WorkerMetricUnavailableReasons.cs`
- `sources/Api/Data/UatFleetScenarioSeeder.cs`, `scripts/Start-Uat.ps1`, `sources/AppHost/Program.cs` (grep), `sources/Api/Program.cs` (lines 160-235), `sources/Api/Middleware/HostingControlPlanePauseMiddleware.cs`
- `sources/Api/DTO/GameServerDto.cs`, `LifecycleJobDto.cs`; `sources/Api/Controllers/CostAlertSettingsController.cs`; `sources/Api/Services/Notifications/CostAlertMessageRenderer.cs`
- `tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs`, `HostingTestStore.cs`, `HostingTestData.cs`, `NodaTimeSqliteApiDbContext.cs`, `GlobalUsings.cs`
- `sources/ReactApp/playwright.uat.config.ts`, `tests/uat/*`, `package.json` scripts; `.planning/ROADMAP.md` (Phase 68 and carry-over notes), `.planning/phases/67-server-metrics/67-UAT.md`, `67-VALIDATION.md`, `67-VERIFICATION.md`
- `dotnet tool list -g`, `dotnet --version`, `node --version`

### Secondary (MEDIUM confidence)
- NodaTime Resolvers documentation via search results: https://nodatime.org/3.3.x/api/NodaTime.TimeZones.Resolvers.html (lenient resolver behavior since 2.0)

### Tertiary (LOW confidence, marked for validation)
- Embedded TZDB without OS dependency and `Intl` id compatibility with `Tzdb` (Assumption A-5)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new dependencies; all reused components read in this session.
- Architecture: HIGH for reuse points and pitfalls (interceptor, operations whitelist, enqueue principal coupling, emulator limits); MEDIUM for the exact semantics of the warning window (Assumption A-1).
- Pitfalls: HIGH, each cites the file and lines that cause it.

**Research date:** 2026-11-22
**Valid until:** 2026-12-22 (codebase moves with the milestone; re-check the interceptor, `operationalKinds` and the enqueue core if Phase 69 lands first)

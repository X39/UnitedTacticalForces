# Phase 66: Accrued Cost & Cost Alerts - Research

**Researched:** 2026-11-21
**Domain:** ASP.NET Core control-plane cost accounting, durable alert outbox (in-app / Discord DM / SMTP email), WebSocket push, React 19 workspace UI
**Confidence:** HIGH for the in-repo architecture and data model (all read this session); MEDIUM for Discord DM delivery edge cases (docs-level, not exercised live); MEDIUM for SMTP (library verified, no SMTP server available here)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Accrued cost basis (CST-03)**
- **D-01:** A session is one active `GameServerDeployment` (created to resolved). Accrued cost = sum of charged `ProviderCostSlice` ledger entries for that deployment plus the in-progress slice prorated by rate x elapsed. It advances while the server is costing (`PossibleCost`) and stops advancing once cost has stopped. Never a fake zero; unknown shows as unavailable.
- **D-02:** **No polling where avoidable.** The API calculates accrued cost and exposes it by REST (initial load and fallback) and **pushes updates over the existing WebSocket update stream** while a session is costing. The browser displays what the API sends (value plus as-of time); it does not own the calculation. Cadence of pushes and any smoothing between pushes is Claude's discretion, but must not require the client to poll.

**Thresholds (CST-04)**
- **D-03:** Thresholds are configured **per cost owner** and apply to all sessions billed to that owner.
- **D-04:** A threshold is an **absolute EUR amount of accrued session cost**; an owner can configure **several** levels (e.g. 5, 10, 25). Each crossing alerts once.
- **D-05:** **No default thresholds**: no alerts fire until the owner configures at least one. The configuration UI must make this state clear.
- **D-06:** The owner edits thresholds and per-channel on/off in the **React cost-owner view**, owner-authorized (new API endpoint and DTO fields allowed per AGENTS.md; regenerate OpenAPI snapshot and clients). In-app is always on.

**In-app alert (CST-04)**
- **D-07:** Each crossing creates a **persisted alert record** (server, amount, threshold, time). It appears in the existing ProfileMenu notifications list and as a **banner in that server's workspace**, with a link that opens the shared cost-stop confirmation ("Shut down and stop billing"). Delivered via WebSocket push, not polling.
- **D-08:** An alert stays visible **until the owner dismisses it** (per-alert dismissal), including after the session ends.
- **D-09:** A cost owner with no deliverable Discord or email address still receives the in-app alert.

**Discord and email delivery (CST-05, CST-06)**
- **D-10:** Discord DM goes through the existing `DiscordBot` (`WithDiscordAsync`); no DM code exists yet.
- **D-11:** Email uses a **provider-neutral `IEmailSender` with an SMTP implementation** configured under a new config section; the channel is cleanly disabled (delivery marked skipped) when unconfigured. Never log credentials.
- **D-12:** Email recipient: `User.EMail` or any other mail source the app knows, but **exactly one email is sent per alert, preferring the app-local address**. The user's **locale must be recorded** and used to render the message (EN/DE, text in API resource files per AGENTS.md). The researcher confirms whether a per-user locale is stored today and adds it in the owning layer if not.
- **D-13:** Recipients for non-personal cost owners: whoever the owner type resolves to; researcher confirms from the `CostOwner` model. Anyone authorized on the owner sees the in-app alert; unresolvable means in-app only.

**Durability (success criterion 4)**
- **D-14:** A **persisted delivery record per alert and channel** (unique alert+channel; status pending/sent/failed/skipped). A background dispatcher claims pending rows, retries transient failures a bounded number of times with backoff, and never resends a `sent` row. Crossings are recorded once per (deployment, threshold) so an API restart cannot re-alert. A new session (new deployment) starts fresh crossings. Persist intent before sending (same discipline as provider mutation). EF migration must be scaffolded with `dotnet ef`.

### Claude's Discretion
WebSocket message shape and push cadence, alert/threshold/delivery table design, dispatcher structure, retry count and backoff, exact copy and German strings, cost-strip presentation of the accrued value, component split under `src/components/server/` and `src/features/hosting/`, SMTP library choice (MailKit suggested).

### Deferred Ideas (OUT OF SCOPE)
- Replacing the existing ProfileMenu 15s notification poll for non-cost notifications with push (broader than this phase).
- Percentage-of-allowance thresholds and per-server thresholds.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CST-03 | User sees accrued cost for the current session (control plane calculates it) | Slice-proration formula over `HostingCostLedgerEntry` (section "Accrued cost calculation"); REST endpoint plus per-recipient WebSocket value frames; `AccruedCost` React row. Prepaid-slice caveat is the key correctness point. |
| CST-04 | Cost owner receives an in-app alert when a configured cost threshold is reached | `CostAlertSetting`/`CostAlertThreshold` per owner, `CostAlert` unique per (deployment, threshold), per-viewer dismissal, alert REST list plus socket frames, banner and ProfileMenu rows. |
| CST-05 | Same alert as a Discord DM through the existing bot | New outcome-returning DM method on `DiscordBot` (the existing `WithDiscordAsync` silently no-ops when disconnected), `CostAlertDelivery` outbox with channel `Discord`. |
| CST-06 | Same alert by email through a new email channel | `IEmailSender` + MailKit SMTP implementation, config section `Email:Smtp`, delivery rows `Skipped` when unconfigured, per-user locale for EN/DE rendering. |
</phase_requirements>

## Project Constraints (from CLAUDE.md / AGENTS.md)

`./CLAUDE.md` does not exist; `AGENTS.md` is authoritative [VERIFIED: AGENTS.md, read in the task context]. Directives the plan must satisfy:

- All identifiers, comments, XML docs in English; localized end-user text in resource files (API: `Language.resx` / `Language.de.resx`; React: `en.json` / `de.json`).
- Every enum type is named `E<PascalCase>`; every C# type and member gets meaningful XML docs (`<summary>` plus `<param>`, `<returns>`, `<value>`, `<exception>`); one top-level type per file named after the type (generic-arity families excepted). Generated code (Kiota, EF migration designers, `Language.Designer.cs`, `src/generated/api.ts`) is exempt and never hand-edited.
- EF migrations **only** via `dotnet ef migrations add <Name> --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations`; custom SQL may be added to generated `Up`/`Down`.
- Architecture: no game-process execution in the API; cross-cutting/server-wide features need an explicit user request (this phase has one); API, contract and database changes needed by a feature are in scope; put each change in the layer that owns the data; regenerate OpenAPI snapshot and clients.
- Do not touch the legacy Blazor WebApp (`sources/WebApp`) except to keep API compatibility.
- Commit cohesive units; subject format `type(scope)!,type(scope): summary` under 80 chars; breaking commits carry `BREAKING CHANGE:`.
- Secrets (SMTP credentials, tokens) never in the frontend or unredacted logs. UI: reuse shared components; no raw `<input>` as a button.
- Memory: UAT checkpoints must be practical and include copy-paste start/stop/seed commands; no `<details>` / ugly select / misaligned dialogs in React UI (UI-SPEC already honours this).

## Summary

The control plane already persists everything needed to compute accrued cost: `HostingCostLedgerEntry` rows of kind `ProviderCostSlice` with `SliceStartsAt`/`SliceEndsAt`/`Amount`, one unique per (`DeploymentFk`, `SliceStartsAt`). The one trap is that slices are **charged in advance** (15-minute reservation slices, renewed 30 s before expiry). The newest slice is therefore always partly in the future, so "sum of charged slices" overstates cost and "charged slices plus a prorated in-progress slice" double-counts the in-progress slice. The correct, restart-safe formula is to prorate **each** slice by the elapsed fraction of its interval, with "now" frozen at the resolution instant once cost has stopped. That formula is pure, deterministic from durable state, and needs no new accounting column.

The push infrastructure exists but is deliberately id-only today (`GameServerFleetChangedMessage` carries only a server id so the socket "never discloses titles, states, costs or owners"). D-02 and D-07 require value-carrying frames, so this phase must add a **cost-owner-aware audience** (users who may view the server AND the cost owner), mirrored on the existing `CanViewCostOwnerAsync` rule, and per-recipient sends through the existing singleton `IUpdateStreamService` on the existing per-user path `game-servers/fleet/{userId}`. The `FleetChangeInterceptor` does not observe ledger inserts, so a dedicated hosted worker must tick to compute accrued values, publish them, and evaluate thresholds. Discord DM and email must be driven by a separate persisted outbox plus dispatcher so that crossings and deliveries are exactly-recorded and survive restarts; the existing `DiscordBot.WithDiscordAsync` silently does nothing when disconnected, so a new outcome-returning DM method is required or an undelivered DM would be wrongly marked `Sent`. No per-user locale exists today (confirmed), so `User` gains a `Locale` column, recorded when the owner saves alert settings.

**Primary recommendation:** Build one `CostAccrualCalculator` (pure, slice-prorated), one `CostAlertWorker` (tick: compute, push value frames, insert crossings plus delivery rows in one transaction, push alert frames), and one `CostAlertDispatcher` (claims `CostAlertDelivery` rows, sends Discord/SMTP via outcome-returning adapters, bounded backoff). Keep thresholds and per-viewer dismissal relational (SQLite-test-friendly), add `User.Locale`, add MailKit 5.0.0, and expose accrued cost through its own REST endpoint plus value frames rather than the fleet list.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Accrued cost calculation | API / Backend | Database | Computed from durable ledger slices; browser only displays (CST-03, D-02). |
| Accrued value push | API / Backend (hosted worker + `IUpdateStreamService`) | Browser (cache write) | Per-recipient authorization must be applied at publish time; client writes frame into query cache only. |
| Threshold evaluation and crossing record | API / Backend (hosted worker) | Database | Must be restart-safe; unique (deployment, threshold) constraint is the once-only guarantee. |
| Alert delivery (Discord, email) | API / Backend (dispatcher) | Database (outbox) | Provider/credentials server-side only; durable state machine. |
| In-app alert surface | Browser / Client | API / Backend | Banner, menu rows, dismissal UI; list and dismissal state owned by API. |
| Threshold settings | API / Backend (authorization, validation, 409) | Browser (draft form) | Owner-authorized; server validates range, count, precision. |
| User locale | Database / Storage (`User`) | API / Backend | Owning layer for per-user data; read by the dispatcher to render messages. |
| Message text (Discord/email) | API / Backend (resource files) | -- | AGENTS.md: API-owned localized text in `.resx`. |
| Cost-stop confirmation from alert | Browser / Client | API / Backend | Reuses `CostConfirmationProvider`; API revalidates the action. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MailKit | 5.0.0 | SMTP client for `SmtpEmailSender` | Microsoft marks `System.Net.Mail.SmtpClient` "not recommended for new development" and points to MailKit [CITED: learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient]. Latest stable 5.0.0, owner `jstedfast`, verified prefix, about 298M downloads [VERIFIED: azuresearch-usnc.nuget.org query, this session]. Depends on MimeKit.Core 5.0.0 [VERIFIED: api.nuget.org mailkit 5.0.0 nuspec]. |
| Discord.Net | 3.20.1 (already referenced) | DM through the existing bot | `UserExtensions.SendMessageAsync(IUser, ...)` sends a DM; documented to throw `HttpException` and to catch `DiscordCode` 50007 [VERIFIED: Discord.Net.Core.xml in local NuGet cache]. |
| EF Core + Npgsql | 10.0.9 / 10.0.2 (already referenced) | New entities, migration | Existing stack [VERIFIED: sources/Api/Api.csproj]. |
| @tanstack/react-query | 5.101.4 (already) | Accrued/alerts caches written from socket frames | Existing stack [VERIFIED: sources/ReactApp/package.json]. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NodaTime | 3.3.2 (already) | `Instant` arithmetic in the calculator | Same clock/instant types as the ledger (`NodaSystemClock.Instance`). |
| Mailpit container (`axllent/mailpit`) | latest | Local SMTP sink for AppHost UAT | Optional dev harness only; `[ASSUMED]`. Not a production dependency. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| MailKit | `System.Net.Mail.SmtpClient` | No new package, but Microsoft recommends against it (above). Reject. |
| Child table for thresholds | PostgreSQL `numeric[]` column | Arrays do not run on the SQLite in-memory test store (`HostingTestStore` uses `UseSqlite`) [VERIFIED: tests/Api.Tests/GameServerHosting/HostingTestStore.cs:36-37]. Use a child table. |
| Value frames to a cost-aware audience | Id-only "accrued changed" signal plus REST refetch | Safer authorization story, but N viewers x REST call per tick, and contradicts UI-SPEC "client writes the received value into the query cache". Use value frames with a fail-closed audience. |
| Accrued in `GameServerDto` / fleet list | Separate `GET game-servers/{id}/accrued-cost` | Fleet list would need a ledger query per server and an N+1 on every fleet refetch. UI-SPEC state 1 ("server loaded, accrued value not") already assumes a separate query. Use a separate endpoint. |

**Installation:**
```bash
# API (sources/Api/Api.csproj)
dotnet add sources/Api/Api.csproj package MailKit --version 5.0.0
# React: no new dependency (UI-SPEC Registry Safety)
```

**Version verification:** `MailKit 5.0.0` is the latest listed in `https://api.nuget.org/v3-flatcontainer/mailkit/index.json` (last entry `"5.0.0"`) [VERIFIED: api.nuget.org, this session].

## Package Legitimacy Audit

The `gsd-tools package-legitimacy check` seam supports only `npm|pypi|crates`; running it with `--ecosystem nuget` returned `Error: Usage: gsd-tools package-legitimacy check --ecosystem <npm|pypi|crates> <pkg1> ...` [VERIFIED: seam run this session]. The NuGet package was therefore vetted manually against the NuGet registry and Microsoft documentation, not via the seam. The planner should still gate the install with one `checkpoint:human-verify` because the seam did not return `OK`.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| MailKit 5.0.0 | NuGet | Multi-year (versions 4.x through 5.0.0) | about 298M total | github.com/jstedfast/MailKit | Seam unsupported; manual: owner `jstedfast`, verified prefix, named by Microsoft docs | Approved, with a human-verify checkpoint before install |
| MimeKit.Core 5.0.0 (transitive) | NuGet | Multi-year | MimeKit about 355M total | github.com/jstedfast/MimeKit | transitive of MailKit | Approved |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none (seam not applicable to NuGet)

## Architecture Patterns

### System Architecture Diagram

```
                       (every ~30 s, not paused)
 PostgreSQL --------> CostAlertWorker ---------------------------------------------+
  ledger slices         |  1. load unresolved PossibleCost deployments (+ recently  |
  deployments           |     resolved ones, bounded lookback)                      |
  owner settings        |  2. CostAccrualCalculator -> AccruedCost per deployment   |
                        |  3. cost-aware audience -> per-recipient value frames --> IUpdateStreamService
                        |  4. for each owner threshold <= amount: ONE transaction:  |        |  game-servers/fleet/{userId}
                        |       INSERT CostAlert (unique deployment+threshold)      |        v
                        |       INSERT CostAlertDelivery rows (Pending/Skipped)     |   Browser (FleetLiveUpdatesProvider)
                        |  5. after commit: alert-raised frame to recipients -------+--> cache write: accrued(serverId), alerts
                        v
                 CostAlertDelivery (outbox) <-------- claim (lease) ---- CostAlertDispatcher (every ~5 s)
                                                          |                 |
                                                          |  Discord: DiscordBot.TrySendDirectMessageAsync -> outcome
                                                          |  Email:   IEmailSender (SMTP/MailKit) -> outcome
                                                          v
                                          Sent | retry w/ backoff (bounded) | Failed | Skipped(reason)

 Browser REST:  GET  game-servers/{id}/accrued-cost        (initial load, reconnect, refresh)
                GET  cost-alerts                            (viewer's undismissed alerts)
                POST cost-alerts/{id}/dismiss, POST cost-alerts/dismiss (all for a server)
                GET/PUT cost-owners/{id}/alert-settings     (409 on stale revision; records caller locale)
 FleetChangeInterceptor (existing, id-only) -> fleet signal -> client also refetches accrued(serverId)
                                              (covers the cost-stopped / final transition without a value frame)
```

### Recommended Project Structure
```
sources/
├── Database/Hosting/                      # CostAlertSetting, CostAlertThreshold, CostAlert, CostAlertDismissal,
│                                          # CostAlertDelivery, plus enums ECostAlertChannel, ECostAlertDeliveryStatus
├── Database/Authority/User.cs             # + Locale (nullable string)
├── WebSocketContracts/Hosting/            # GameServerAccruedCostMessage, CostAlertRaisedMessage, CostAlertDismissedMessage
├── Api/Services/GameServerLifecycle/      # CostAccrualCalculator, CostAlertEvaluator, CostAlertAudienceSource,
│                                          # CostAlertSettingsService, CostAlertService (list/dismiss), CostAlertOptions
├── Api/Services/Notifications/            # IEmailSender, SmtpEmailSender, EmailOptions, CostAlertMessageRenderer,
│                                          # IDiscordDirectMessageSender (adapter over DiscordBot)
├── Api/HostedServices/                    # CostAlertWorker, CostAlertDispatcher (registered in AddHostingControlPlane)
├── Api/Controllers/                       # CostAlertsController (list/dismiss), alert-settings actions, accrued-cost action
├── Api/DTO/                               # AccruedCostDto, CostAlertDto, CostAlertSettingsDto, update payloads
├── Api/Properties/Language*.resx          # CostAlert_* keys EN and DE (no designer regeneration, see Pitfall 7)
sources/ReactApp/src/
├── components/server/                     # AccruedCost, CostAlertBanner, CostAlertMenuItem, cost-alerts.css
├── features/hosting/                      # accruedModel.ts, alertSettingsModel.ts, useAccruedLive.ts, useCostAlerts.ts,
│                                          # CostAlertSettingsPanel.tsx, CostAlertsPage.tsx
└── locales/en.json, de.json               # Server_Alerts_* / Server_Workspace_Cost_* keys
tests/Api.Tests/GameServerHosting/         # CostAccrualCalculatorTests, CostAlertEvaluatorTests, CostAlertDispatcherTests, ...
```

### Accrued cost calculation (resolves D-01 ambiguity)

Facts [VERIFIED: sources/Api/Services/GameServerLifecycle/HostingCostSliceService.cs]:
- `CalculateAmount` is `rate * hours` rounded to 4 decimals, minimum `0.0001m` (lines 380-385).
- `ChargeSlice` inserts one `HostingCostLedgerEntry` with `Kind = EHostingLedgerEntryKind.ProviderCostSlice`, `Amount = -amount`, `SliceStartsAt`, `SliceEndsAt`, `EstimatedHourlyRate = rate` (lines 308-338).
- The next slice starts at the previous `CostAuthorizationExpiresAt` and ends at least one `ReservationSlice` after now (lines 206-212), so the newest slice normally ends in the future; `HostingFinanceOptions` defaults are `ReservationSlice = TimeSpan.FromMinutes(15)` and `ReconciliationInterval = TimeSpan.FromSeconds(30)` (HostingFinanceOptions.cs:12,14).
- The ledger has a unique index `[Index(nameof(DeploymentFk), nameof(SliceStartsAt), IsUnique = true)]` (HostingCostLedgerEntry.cs:14).

Recommended formula (planner: implement as a pure static function so it is unit-testable without a database):

```
asOf   = deployment.TimeStampResolved is set and !PossibleCost ? deployment.TimeStampResolved : now
amount = SUM over ProviderCostSlice entries e of deployment:
           (-e.Amount) * clamp((min(asOf, e.SliceEndsAt) - e.SliceStartsAt) / (e.SliceEndsAt - e.SliceStartsAt), 0, 1)
isFinal = TimeStampResolved != null && !PossibleCost
```

Summing in memory (fetch the deployment's slice rows, then sum) is required because decimal aggregation does not translate on the SQLite test provider; the existing code sums client-side for the same reason (`reservations.Sum()` in `GetOtherReservationTotalAsync`, lines 277-292).

Do not apply `DeploymentCostCorrectionCredit/Debit` or `LedgerReversal` rows: they move cost between owners after the fact and `IsReversible` logic is finance-side; accrued is "what this session cost while it ran", derived from provider-cost slices only. Note this as a documented decision (Open Question 1 covers charged-total vs prorated).

State mapping (feeds `AccruedCostDto`): no deployment ever -> `accruedCost = null`. Latest deployment has no slice rows and `!PossibleCost` -> `null`. No slice rows and `PossibleCost` -> `amount = null`, reason `not-yet-charged`. Viewer who may not view the owner -> `amount = null`, reason `hidden` (UI maps unknown reasons to no extra sentence, UI-SPEC row 3). Latest session is the active deployment, else the most recent resolved one (UI-SPEC A5, "Last session total").

### Pattern 1: Crossing evaluation, once per (deployment, threshold)
**What:** For each unresolved or recently resolved deployment, compute accrued, load the cost owner's thresholds, and for each threshold `<= amount` not yet recorded, insert one `CostAlert` and its `CostAlertDelivery` rows in one transaction. The unique index `(DeploymentFk, ThresholdAmount)` makes re-evaluation after restart, duplicate worker passes and concurrent API instances idempotent; catch the unique violation and continue.
**When to use:** every worker tick; also on first start after restart (a threshold already exceeded fires then, UI-SPEC A1).
**Example:**
```csharp
// Source: pattern derived from HostingCostSliceService (persist intent in a transaction before side effects)
await using var tx = await db.Database.BeginTransactionAsync(ct);
foreach (var threshold in thresholds.Where(t => t <= accrued.Amount && !alreadyCrossed.Contains(t)))
{
    var alert = new CostAlert
    {
        PrimaryKey = Guid.NewGuid(),
        DeploymentFk = deployment.PrimaryKey,
        GameServerFk = deployment.GameServerFk,
        CostOwnerFk = deployment.CostOwnerFk,        // init-only on the deployment: stable for the session
        ThresholdAmount = threshold,
        AccruedAmount = accrued.Amount,
        Currency = "EUR",
        TimeStampCreated = now,
    };
    db.CostAlerts.Add(alert);
    foreach (var delivery in plan.DeliveriesFor(alert))   // recipient resolved at crossing time, status Pending or Skipped
        db.CostAlertDeliveries.Add(delivery);
}
try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
catch (DbUpdateException) when (IsUniqueViolation) { /* another pass recorded it: idempotent */ }
// publish AFTER commit; frames are best effort, REST list is the source of truth
```

### Pattern 2: Outbox dispatcher with lease, bounded retry, outcome-typed adapters
**What:** The dispatcher claims `Pending` rows whose `NextAttemptAt <= now` by setting `Status = Sending`, `LeaseExpiresAt`, `AttemptCount++` and saving **before** sending (persist intent first). Adapters return a typed outcome (`Sent`, `Transient`, `Permanent`, `Unavailable`) and never throw for expected delivery failures. `Sent` is terminal and never re-claimed; a `Sending` row whose lease expired is retried (at-least-once; Open Question 4). Backoff e.g. 30 s, 2 min, 10 min, 1 h, then `Failed` after 5 attempts (Claude's discretion; make it an option).
**When to use:** all Discord and email deliveries. The lease/expired-lease recovery shape mirrors `LifecycleJobQueue` (`LeaseOwner`, `LeaseExpires`) [VERIFIED: sources/Api/Services/GameServerLifecycle/LifecycleJobQueue.cs lines 32-33, 90-91 via grep].
**Example:**
```csharp
// Source: Discord.Net docs in Discord.Net.Core.xml (UserExtensions.SendMessageAsync, error 50007)
public async Task<EDirectMessageOutcome> TrySendDirectMessageAsync(ulong userId, string text, CancellationToken ct)
{
    if (!_enabled) return EDirectMessageOutcome.ChannelUnavailable;           // Discord:Enabled false
    if (_discordSocketClient.ConnectionState is not ConnectionState.Connected)
        return EDirectMessageOutcome.ChannelUnavailable;                      // NOT "sent": WithDiscordAsync would no-op here
    try
    {
        var user = await _discordSocketClient.GetUserAsync(userId, new RequestOptions { CancelToken = ct });
        if (user is null) return EDirectMessageOutcome.Permanent;
        await user.SendMessageAsync(text, allowedMentions: AllowedMentions.None);   // user-supplied server title: no pings
        return EDirectMessageOutcome.Sent;
    }
    catch (HttpException ex) when (ex.DiscordCode is DiscordErrorCode.CannotSendMessageToUser
                                   or DiscordErrorCode.CannotSendMessagesToThisUserDueToHavingNoMutualGuilds)
    { return EDirectMessageOutcome.Permanent; }   // 50007 / 50278: user blocks DMs or shares no guild
    catch (Exception ex) when (ex is not OperationCanceledException) { return EDirectMessageOutcome.Transient; }
}
```
`CannotSendMessageToUser = 50007` and `CannotSendMessagesToThisUserDueToHavingNoMutualGuilds = 50278` are `DiscordErrorCode` members [VERIFIED: ilspycmd decompile of Discord.Net.Core.dll 3.20.1, this session].

### Pattern 3: Value frames to a cost-aware, fail-closed audience
**What:** Add `IHostingAuthorizationService.GetCostOwnerViewAudienceAsync(costOwnerIds)` mirroring `CanViewCostOwnerAsync` as a set of users, and intersect with the server view audience. The existing view rule is: hosting administrator OR `fin`/`fin:view-all` empty claim OR `co:view` claim with the owner id OR the owner's own `UserFk` [VERIFIED: HostingAuthorizationService.cs:78-93]. The existing notifier fails closed per batch and sends one frame per recipient to `FleetChangeStreamPaths.ForUser(userId)` [VERIFIED: FleetChangeNotifier.cs:194-253]. Reuse that path and discipline; add an audience drift test next to `HostingFleetAudienceTests` (the class doc says any change to one rule "must be mirrored in the other").
**Wire format:** new messages in `sources/WebSocketContracts/Hosting/` (namespace `X39.UnitedTacticalForces.WebSockets.Contracts.Hosting`); the wire `type` is `typeof(T).FullName` [VERIFIED: WebSocketMessage.cs]. **Use `DateTimeOffset`, not NodaTime `Instant`**: `UpdateStreamService` serializes with its own `JsonSerializerDefaults.Web` options and `ReferenceHandler.Preserve` with no NodaTime converter [VERIFIED: UpdateStreamService.cs lines 15-21], unlike the controller JSON options. `EventChangedMessage` uses `DateTimeOffset?` for the same reason [VERIFIED: EventChangedMessage.cs].

### Pattern 4: Client write-through from frames
`FleetLiveUpdatesProvider` already owns the single per-user socket and dispatches by `frame.type` [VERIFIED: FleetLiveUpdatesProvider.tsx:14-15,69-84]. Extend its `onMessage` with a typed frame registry (accrued, alert raised, alert dismissed) that writes `queryClient.setQueryData(...)`; keep the existing id-only fleet branch untouched. Guard accrued writes by `asOf` (ignore a frame older than the cached value) so a reordered REST refetch cannot move the number backwards. Additionally, when a fleet-changed signal arrives for a server, invalidate `accrued(serverId)` exactly: this is how the cost-stopped (final) transition reaches the strip without polling, because `FleetChangeInterceptor` already fires on `Status`/`PossibleCost` changes [VERIFIED: FleetChangeInterceptor.cs:297-304].

### Pattern 5: Settings with optimistic concurrency (409)
No concurrency-token pattern exists in the repo (grep for `ConcurrencyCheck|IsRowVersion|[Timestamp]|xmin|RowVersion` over `Database` and `Api` returned nothing outside migrations/obj) [VERIFIED: grep this session]. Add `int Revision` on `CostAlertSetting`; the PUT carries the revision it loaded, the service compares and increments inside one transaction, and throws `HostingConflictException` on mismatch. The controller helper already maps `HostingConflictException` to 409 (`GameServersController.cs:458-460`).

### Anti-Patterns to Avoid
- **Client-side extrapolation or timers for the accrued number** (UI-SPEC, D-02): the strip shows only what the API sent; a source-scan test asserts no `setInterval`/`refetchInterval` in the accrued files.
- **Marking a Discord/email delivery `Sent` because the call did not throw.** `WithDiscordAsync` no-ops silently when the client is not connected [VERIFIED: DiscordBot.cs `WithDiscordAsync`]; add an outcome-returning method instead.
- **Evaluating thresholds in `HostingCostReconciler` or the lifecycle executor.** They hold row locks on deployments/owners; alert I/O must stay outside those transactions.
- **Putting game/provider concerns or Discord/SMTP types in `HostingContracts`.** Keep wire messages in `WebSocketContracts`, delivery adapters in `Api`.
- **Sending frames inside the evaluation transaction.** Publish after commit; frames are best effort and the REST list is the truth.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SMTP transport, TLS negotiation, MIME encoding, header encoding | Raw sockets or `SmtpClient` | MailKit `SmtpClient` + `MimeMessage` | Header injection, UTF-8 subjects (German umlauts), STARTTLS/implicit TLS, auth mechanisms. |
| Once-only crossing | In-memory "already alerted" sets | Unique index on `(DeploymentFk, ThresholdAmount)` plus catch of the unique violation | Survives restart and concurrent instances (success criterion 4). |
| Retry bookkeeping | Ad-hoc `Task.Delay` loops per message | Persisted `AttemptCount`, `NextAttemptAt`, lease columns (as `LifecycleJob` does) | Restart safety. |
| Per-user socket fan-out | New WebSocket endpoint or a second socket | Existing `IUpdateStreamService` + `FleetChangeStreamPaths.ForUser` + the single client socket | Existing prefix-collision protections (fixed 55-char path) and reconnect logic. |
| Locale-aware money/time formatting in React | Manual string building | `Intl.NumberFormat` via `formatMoney`, `Intl.DateTimeFormat`, `Intl.ListFormat` (UI-SPEC) | German decimal comma, 24 h time. |
| Server-side money/time formatting for messages | `ToString("F2")` | `decimal.ToString("C"/"N2", CultureInfo)` with the user's `Locale` culture | Culture-correct EN/DE amounts. |
| Concurrency token | Homemade ETag header | Integer `Revision` compared in a transaction | Simple, testable, fits the existing 409 mapping. |

**Key insight:** every hard part of this phase (once-only, restart safety, authorization at publish time, delivery truthfulness) is already solved by a repo pattern (unique index plus transaction, lease/retry columns, audience source, outcome-typed adapters). The risk is in the seams: prepaid slices, Instant serialization, silent Discord no-op, and cost-vs-server audience.

## Common Pitfalls

### Pitfall 1: Double-counting or overstating the in-progress slice
**What goes wrong:** Summing every `ProviderCostSlice` (the newest is prepaid up to 15 min ahead) or adding a prorated slice on top of the sum shows cost that has not happened yet, and fires thresholds early.
**Why it happens:** The ledger records *authorization* of the next slice, not elapsed usage.
**How to avoid:** Prorate every slice by elapsed fraction (formula above); freeze `asOf` at `TimeStampResolved` when cost has stopped.
**Warning signs:** Accrued jumps by about 0.025 EUR (15 min at 0.10 EUR/h) the moment a slice is charged; `Last session total` exceeds elapsed-time x rate.

### Pitfall 2: NodaTime `Instant` in WebSocket messages
**What goes wrong:** `UpdateStreamService` has no NodaTime converter, so an `Instant` property serializes as an empty object or wrong shape and the client's `asOf` is garbage.
**How to avoid:** `DateTimeOffset` in message classes; round-trip test through the real `UpdateStreamService` options.

### Pitfall 3: Silent no-op Discord delivery
**What goes wrong:** `WithDiscordAsync` returns without running the action when not connected; `Discord:Enabled` defaults to **on** when the key is absent (`?? true`), but `ExecuteAsync` then tries to log in and retries every 60 s [VERIFIED: DiscordBot.cs]. A DM "sent" through that path is lost.
**How to avoid:** outcome-typed `TrySendDirectMessageAsync`; `Unavailable` keeps the row `Pending` with backoff (bounded) rather than `Sent`.
**Also:** DM can fail permanently (50007 blocked/disabled DMs, 50278 no mutual guild): mark `Failed` with a sanitized reason, never retry forever. The in-app alert is unaffected (D-09).

### Pitfall 4: Missing or stale ticks around restart and resolution
**What goes wrong:** The cost can cross a threshold between the last tick and `TimeStampResolved`; if the worker only looks at unresolved deployments the final crossing is lost. After an API restart the first tick must recompute from the ledger, not from process memory.
**How to avoid:** evaluate unresolved `PossibleCost` deployments plus deployments resolved within a bounded lookback (option, e.g. 24 h); idempotency makes re-evaluation harmless.

### Pitfall 5: Cost audience wider than the server audience (privacy leak)
**What goes wrong:** Sending accrued amounts to everyone who may view the server leaks cost to viewers who cannot see the cost owner (`CostOwnerName` is "Assigned owner" for them, `CostOwnerUserId` nulled) [VERIFIED: GameServerProjectionService.cs:896-902].
**How to avoid:** intersect server-view and cost-owner-view audiences; fail closed (drop the batch with a warning) when the audience read fails, exactly as `FleetChangeNotifier` does.

### Pitfall 6: SQLite-vs-PostgreSQL differences in tests
**What goes wrong:** Postgres arrays, `FOR UPDATE`, decimal `SUM`, partial unique index filters written in Postgres quote syntax.
**How to avoid:** thresholds in a child table; sum decimals in memory; guard raw SQL with the same `IsPostgreSql` check the slice service uses; filtered indexes already work in this codebase (`HasFilter("\"...\" IS NOT NULL")`), keep new filters in that style or avoid them.

### Pitfall 7: Resource designer is generated and not regenerated by the CLI
**What goes wrong:** `Language.Designer.cs` is `<auto-generated>` and is produced by the VS `ResXFileCodeGenerator`; `dotnet build` does not regenerate it. Hand-editing it violates AGENTS.md.
**How to avoid:** add `CostAlert_*` keys to `Language.resx` and `Language.de.resx` and read them with `Language.ResourceManager.GetString(<const>, culture)` where `ResourceManager` is `internal` (visible to `Api`) [VERIFIED: Language.Designer.cs]; keep key names in one handwritten constants class; add a parity test (every key present in both resx files, placeholders match). `Language.de.resx` currently has 112 `<data>` entries vs 165 in `Language.resx`, so missing German keys already exist; the new test must scope to `CostAlert_*` keys only [VERIFIED: grep -c, this session].

### Pitfall 8: Header/mention injection through the server title
**What goes wrong:** Server titles are user data up to 256 characters. In an email subject they can carry CR/LF; in a Discord DM they can contain `@everyone` text.
**How to avoid:** strip control characters from titles used in subjects; plain-text email body (no HTML) so nothing needs escaping; Discord `AllowedMentions.None`.

### Pitfall 9: Idempotent settings save races the evaluator
**What goes wrong:** Owner adds threshold 5 while the session is at 7; the next tick fires immediately (UI-SPEC A1). Several low thresholds added at once fire several alerts at once.
**How to avoid:** accept per A1, document in `Server_Alerts_Amounts_Description` only if the user decides otherwise (Open Question 5). Never delete `CostAlert` rows when a threshold is removed, so re-adding it cannot re-alert the same session.

### Pitfall 10: `dotnet-ef` tool older than the EF packages
**What goes wrong:** Global `dotnet-ef` is 10.0.5 while packages are 10.0.9 [VERIFIED: `dotnet ef --version`, Api.csproj], which emits a version-mismatch warning and can generate subtly different snapshot output.
**How to avoid:** `dotnet tool update -g dotnet-ef --version 10.0.9` (or a local tool manifest) before scaffolding; commit only tool-generated migration artifacts.

## Code Examples

### Slice-prorated accrual (pure function)
```csharp
// Source: derived from HostingCostSliceService.CalculateAmount/ChargeSlice (sources/Api/Services/GameServerLifecycle)
internal static AccruedCostResult Calculate(
    IReadOnlyCollection<SliceFact> slices, Instant asOf, bool isFinal)
{
    if (slices.Count == 0) return AccruedCostResult.NotYetCharged;
    var total = 0m;
    foreach (var s in slices)
    {
        var span  = (s.EndsAt - s.StartsAt).TotalSeconds;                 // > 0 enforced by CK_HostingCostLedgerEntries_Slice
        var spent = (Instant.Min(asOf, s.EndsAt) - s.StartsAt).TotalSeconds;
        var ratio = (decimal)Math.Clamp(spent / span, 0d, 1d);
        total += s.Amount * ratio;                                        // s.Amount = -ledger.Amount (positive)
    }
    return new AccruedCostResult(decimal.Round(total, 4, MidpointRounding.AwayFromZero), asOf, isFinal);
}
```

### MailKit send with outcome mapping
```csharp
// Source: MailKit SmtpClient API (github.com/jstedfast/MailKit); security options must not disable cert validation
using var client = new MailKit.Net.Smtp.SmtpClient();
await client.ConnectAsync(o.Host, o.Port, o.SecureSocket, ct);      // SecureSocketOptions.Auto / StartTls
if (o.Username is { Length: > 0 }) await client.AuthenticateAsync(o.Username, o.Password, ct);
var msg = new MimeMessage { MessageId = $"{alertId:N}.{recipientId:N}@{o.MessageIdDomain}" }; // deterministic id helps dedupe
msg.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
msg.To.Add(MailboxAddress.Parse(address));
msg.Subject = subject; msg.Body = new TextPart("plain") { Text = body };
await client.SendAsync(msg, ct); await client.DisconnectAsync(true, ct);
// SmtpCommandException with a 5xx StatusCode (recipient rejected) -> Permanent; 4xx, IOException, SocketException -> Transient
```

### React: accrued row presentation model (pure, unit-tested)
```typescript
// Source: 66-UI-SPEC.md "Accrued value states" and "Accrued amount rule"
export function formatAccruedAmount(amount: number, currency: string, costing: boolean, t: TFn): string {
  if (amount >= 0.01) return formatMoney(amount, currency);
  if (amount > 0 || (amount === 0 && costing)) return t("Server_Workspace_Cost_AccruedTiny", { amount: formatMoney(0.01, currency) });
  return formatMoney(0, currency); // exactly 0 and final: nothing was charged, which is true
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `System.Net.Mail.SmtpClient` | MailKit | Microsoft marks SmtpClient not recommended (docs updated 2026-10-16 per page metadata) | Use MailKit. |
| Poll REST for money values | Socket value frames + reconnect refetch | User directive D-02 | Cache write-through, `asOf` ordering guard. |
| Id-only fleet signal | Id-only signal retained for state; value frames only for accrued and alerts | This phase | Two frame families on one socket; both per-recipient. |

**Deprecated/outdated:**
- `Server_Workspace_Cost_AccruedUnavailable` ("Not available yet"): retired, kept in locale files until the Phase 69 cleanup (UI-SPEC).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Final "Last session total" should be the prorated amount clipped to `TimeStampResolved`, not the sum of all charged slice amounts (which includes up to one prepaid, unused slice). | Accrued cost calculation | Strip total could differ from the finance ledger by up to one slice (about 0.025 EUR at 0.10 EUR/h); user may prefer the charged total. |
| A2 | Recipients of a group (Verein/ExternalOrganization) owner are active users holding `co:use` with the owner id, directly or via a role; personal owner is `CostOwner.UserFk`. `co:view` holders and finance staff are not notified. | Pattern/Recipients | Wrong people notified or nobody for Verein owners; D-13 explicitly delegates this call to research. |
| A3 | `canManage` on alert settings = hosting administrator, or owner's own user, or `co:use` holder for that owner. | Settings authorization | Finance staff may expect to manage alerts; UI reads the flag only. |
| A4 | A crash between a successful send and the `Sent` write may duplicate a message (at-least-once); acceptable for money alerts. A deterministic email `Message-Id` limits visible duplicates. | Dispatcher | A rare duplicate alert; the alternative (at-most-once) risks a lost alert. |
| A5 | Dismissal is per viewer (table `CostAlertDismissal`), so one group member dismissing does not hide the alert for others. | Data model (UI-SPEC A2, unresolved) | If the user wants shared dismissal, change one query and the dismiss frame scope. |
| A6 | Discord `GetUserAsync` plus `SendMessageAsync` works for a user who shares a guild with the bot; error codes 50007 and 50278 are permanent. Not exercised against live Discord. | Discord adapter | Some DMs fail permanently for users without a mutual guild; documented in the channel hint. |
| A7 | `Discord.Id == 0` means "not linked" (default `ulong`); `DiscordUser.Id` is `ulong`. | Recipient resolution | A linked user with id 0 is impossible, so low risk; verify against `UsersController.FormatExternalId` while planning. |
| A8 | Mailpit (`axllent/mailpit`) is a suitable AppHost dev SMTP sink. | Environment/UAT | Only the optional dev harness changes. |
| A9 | Registering the worker and dispatcher inside `AddHostingControlPlane`'s `!IsPaused` block is the right pause behavior (a paused host collects no cost and sends nothing). | Registration | Paused hosts stall pending deliveries until unpaused. |

## Open Questions

1. **Final amount: prorated vs charged total (A1).**
   - What we know: slices are prepaid; resolving mid-slice leaves an unused remainder that finance may later correct.
   - What's unclear: whether the user wants the strip to match ledger charges or elapsed cost.
   - Recommendation: prorated, clipped to `TimeStampResolved`; revisit only if finance reports confusion.
2. **Accrued row for viewers who cannot see the cost owner.**
   - What we know: UI-SPEC does not define it; cost owner name is hidden for them.
   - Recommendation: API returns `amount = null`, reason `hidden`, and sends them no value frames; the row shows "Not available for this session." with no reason sentence.
3. **Cost owners without any server-section claim.**
   - What we know: the fleet socket is only opened for users with a server-section claim (`CanAccessServerSection`, UpdateStreamController.cs:131-146 and `canAccessGameServers` gate in the provider), so such an owner gets no live push.
   - Recommendation: alert REST list and Discord/email still work; document that live push needs the server section; do not widen the socket gate in this phase.
4. **At-least-once vs at-most-once on crash during send (A4).** Recommend at-least-once.
5. **Several thresholds crossed in one pass (A1 UI-SPEC).** Recommend one alert row and one message per threshold (matches D-04 "each crossing alerts once"); coalescing would be an explicit user decision.
6. **Group dismissal scope (A5, UI-SPEC A2).** Recommend per viewer.
7. **Delivery uniqueness for group owners.** D-14 says unique alert+channel, but a group owner has several recipients. Recommend unique `(CostAlertFk, Channel, RecipientUserFk)`; the personal-owner case reduces to alert+channel.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests, migration | yes | 10.0.201 | -- |
| dotnet-ef (global tool) | `dotnet ef migrations add` | yes (older) | 10.0.5 (packages are 10.0.9) | `dotnet tool update -g dotnet-ef --version 10.0.9` |
| Node | React build/tests (`engines`: >=24 <25) | yes, outside range | v25.5.0 | Use Node 24 LTS for CI parity if `npm ci` warns on engines |
| Docker | Aspire Postgres, optional Mailpit | yes | 29.5.3 | -- |
| Local PostgreSQL | migration SQL checks | no `psql` on PATH | -- | Aspire AppHost supplies Postgres; unit tests use SQLite |
| SMTP server | real email delivery | not verifiable here | -- | Mailpit container in AppHost UAT; channel cleanly `Skipped` when unconfigured |
| Live Discord bot | real DM delivery | not verifiable here | -- | Human UAT checkpoint with a real bot token; unit tests use a fake sender |

**Missing dependencies with no fallback:** none for planning/building. Live Discord and SMTP behavior can only be confirmed by UAT.
**Missing dependencies with fallback:** psql (use Aspire), SMTP (Mailpit), Discord (fake sender plus UAT).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework (API) | xUnit 2.9.3 on Microsoft.NET.Test.Sdk 18.7.0, EF Core SQLite in-memory via `HostingTestStore` [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Framework (React) | Vitest 4.1.10 + @testing-library/react 16.3.2 + msw 2.12.10; Playwright 1.62.1 with @axe-core/playwright 4.13.0 for e2e [VERIFIED: sources/ReactApp/package.json] |
| Config file | `tests/Api.Tests/Api.Tests.csproj`; `sources/ReactApp/vitest.config.ts`, `playwright.config.ts` |
| Quick run command (API) | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostAccrual\|FullyQualifiedName~CostAlert"` |
| Quick run command (React) | `cd sources/ReactApp && npx vitest run tests/hosting/accrued-model.test.ts tests/hosting/cost-alert-banner.test.tsx` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CST-03 | Proration math: mid-slice, multi-slice, catch-up slice, frozen at resolution, tiny/zero, null states | unit | `dotnet test tests/Api.Tests --filter FullyQualifiedName~CostAccrualCalculatorTests` | No, Wave 0 |
| CST-03 | Accrued endpoint authorization (owner hidden -> null amount) and shape | integration | `--filter FullyQualifiedName~AccruedCostEndpointTests` | No, Wave 0 |
| CST-03 | Value frame goes only to cost-owner audience; `asOf` is `DateTimeOffset` over real serializer options | unit | `--filter FullyQualifiedName~CostAlertAudience` | No, Wave 0 |
| CST-03 | React: row states 1-7, tiny/zero rule, stale line, no `setInterval`/`refetchInterval` source scan | unit | `npx vitest run tests/hosting/accrued-model.test.ts tests/hosting/accrued-cost.test.tsx` | No, Wave 0 |
| CST-04 | Crossing once per (deployment, threshold); restart re-evaluation records nothing new; new deployment starts fresh; no thresholds -> no alert | integration | `--filter FullyQualifiedName~CostAlertEvaluatorTests` | No, Wave 0 |
| CST-04 | Settings validation (range 0-10000, 2 decimals, max count, duplicates), 409 on stale revision, authorization | integration | `--filter FullyQualifiedName~CostAlertSettingsTests` | No, Wave 0 |
| CST-04 | List/dismiss: per-viewer, idempotent, IDOR (cannot dismiss/see another owner's alert) | integration | `--filter FullyQualifiedName~CostAlertServiceTests` | No, Wave 0 |
| CST-04 | React banner/menu rows, dismiss focus rules, badge count, one toast per new id, settings form (dirty/invalid/409/read-only), leave guard | unit | `npx vitest run tests/hosting/cost-alert-*.test.tsx tests/hosting/alert-settings-model.test.ts` | No, Wave 0 |
| CST-05 | Dispatcher: Sent terminal, unavailable keeps Pending, 50007 -> Failed, backoff bound, no resend of Sent | unit | `--filter FullyQualifiedName~CostAlertDispatcherTests` | No, Wave 0 |
| CST-06 | Email: Skipped when unconfigured, one email per alert, EN/DE rendering by `User.Locale`, deterministic Message-Id, 5xx/4xx mapping | unit | `--filter FullyQualifiedName~CostAlertEmailTests` | No, Wave 0 |
| CST-04/05/06 | D-09: no Discord id and no email still yields in-app alert and `Skipped` rows | integration | `--filter FullyQualifiedName~CostAlertEvaluatorTests.NoDeliverableAddress` | No, Wave 0 |
| All | EF migration scaffolded, model snapshot has no pending changes | integration | `dotnet ef migrations has-pending-model-changes` (same args as scaffold) | No, Wave 0 |
| All | OpenAPI snapshot and `api.ts` regenerated, `contracts:check` green; locale parity | contract | `cd sources/ReactApp && npm run contracts:check && npm run locales:check` | Existing scripts |
| CST-04 | Real first WebSocket alert push into banner/menu | manual (UAT) | Human checkpoint with copy-paste AppHost start (`Uat:Enabled`), seeded costing session, Mailpit; justification: mocks cannot prove the live push (UI-SPEC marks banner and menu populated as backstop) | UAT |
| CST-05 | Real Discord DM to a linked account | manual (UAT) | Optional: skippable without blocking (memory: UAT must not be contrived); fake sender covers logic | UAT |

### Sampling Rate
- **Per task commit:** the relevant `--filter` slice above (under 30 s) or the single `npx vitest run <file>`.
- **Per wave merge:** `dotnet test tests/Api.Tests/Api.Tests.csproj` plus `npm run test` in `sources/ReactApp`.
- **Phase gate:** full `dotnet test X39.UnitedTacticalForces.slnx`, `npm run ci`, axe scan of settings page, workspace with banner, profile menu with alert rows, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs` - CST-03
- [ ] `tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs` - CST-04, D-09, restart/idempotency, new-session reset
- [ ] `tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs` + fakes for `IDiscordDirectMessageSender` and `IEmailSender` - CST-05, CST-06
- [ ] `tests/Api.Tests/GameServerHosting/CostAlertAudienceTests.cs` (drift test vs `CanViewCostOwnerAsync`, modeled on `HostingFleetAudienceTests`)
- [ ] `tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs`, `CostAlertServiceTests.cs`
- [ ] `tests/Api.Tests/GameServerHosting/CostAlertResourceTests.cs` - `CostAlert_*` keys present in both resx, placeholders match
- [ ] Test data helpers in `HostingTestData.cs` / `HostingTestSeed.cs` for ledger slices and deployments (extend, do not fork)
- [ ] React: `tests/hosting/accrued-model.test.ts`, `accrued-cost.test.tsx`, `cost-alert-banner.test.tsx`, `cost-alert-menu.test.tsx`, `cost-alert-settings.test.tsx`, `alert-settings-model.test.ts`, `cost-alerts-source-scan.test.ts` (no `setInterval`/`refetchInterval`, draft never stored), parity-matrix rows `CST-accrued`, `CST-alert-banner`, `CST-alert-menu`, `CST-alert-settings`
- [ ] Update `tests/hosting/cost-strip.test.tsx` for six rows and the retired placeholder
- Framework install: none (xUnit and Vitest already present)

## Security Domain

### Applicable ASVS Categories (level 1, `security_enforcement` enabled)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no change | Existing cookie/JWT; all new endpoints `[Authorize]`. |
| V3 Session Management | no change | Socket uses the existing authenticated subscribe endpoint. |
| V4 Access Control | yes | Alert list scoped to recipient set; dismiss checks ownership (no IDOR); settings `canManage`; accrued endpoint requires server view and cost-owner view; frames per-recipient, fail closed. |
| V5 Input Validation | yes | Server-side validation of thresholds (0 < x <= 10000, at most 2 decimals, max count, no duplicates), locale whitelist (`en`/`de` or BCP-47 length-capped), GUID route constraints. |
| V6 Cryptography | yes | No hand-rolled crypto; SMTP TLS via MailKit (`SecureSocketOptions.Auto`/`StartTls`), never disable certificate validation; SMTP password only from configuration/secrets. |
| V7 Error Handling and Logging | yes | Never log SMTP credentials, bot token, or recipient email addresses; log ids and sanitized reasons via `HostingFailureSanitizer`. |
| V9 Communications | yes | TLS to SMTP; Discord over the library's HTTPS/WSS. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cost leak over WebSocket to viewers without owner visibility | Information Disclosure | Cost-owner-aware audience intersected with server view; drift test; fail closed. |
| IDOR on alert dismiss / settings of another owner | Elevation of Privilege | Authorize by owner membership inside the service; return 404 for unknown/unauthorized alert ids. |
| Email header injection / Discord mass mention via server title | Tampering | Strip control characters in subjects; plain-text body; `AllowedMentions.None`. |
| Credential disclosure in logs/config | Information Disclosure | SMTP secrets under `appsettings.secret.json` (template updated); redacted logging; options validated at startup without echoing values. |
| Alert flooding (many thresholds, repeated saves) | Denial of Service | Max thresholds per owner (default 10, option), unique crossing index, bounded retries, single dispatcher. |
| Replayed or stale settings overwrite | Tampering | `Revision` optimistic concurrency, 409. |
| Duplicate alert after restart | Repudiation/Integrity | Persisted crossing and delivery rows; terminal `Sent`. |

## Sources

### Primary (HIGH confidence)
- In-repo files read this session: `HostingCostSliceService.cs`, `HostingCostReconciler.cs`, `HostingFinanceOptions.cs`, `HostingAuthorizationService.cs`, `FleetChangeNotifier.cs`, `FleetChangeInterceptor.cs`, `FleetAudienceSource.cs`, `FleetChangeStreamPaths.cs`, `GameServerProjectionService.cs` (notifications and `ProjectServerAsync`), `UpdateStreamService.cs`, `UpdateStreamController.cs`, `WebSocketMessage.cs`, `DiscordBot.cs`, `User.cs`, `CostOwner.cs`, `GameServerDeployment.cs`, `HostingCostLedgerEntry.cs`, `DatabaseContext.cs` (ledger config), `Constants.cs`, `Api.csproj`, `Program.cs` (wiring), `Claims.cs`, React `FleetLiveUpdatesProvider.tsx`, `updateStream.ts`, `CostStrip.tsx`, `ProfileMenu.tsx`, `CostConfirmationProvider.tsx`, `costConfirmationModel.ts`, `useCostConfirmation.ts`, tests layout.
- `66-CONTEXT.md`, `66-UI-SPEC.md`, `REQUIREMENTS.md`, `AGENTS.md`.
- Discord.Net.Core 3.20.1 XML docs and decompiled `DiscordErrorCode` (local NuGet cache).
- api.nuget.org / azuresearch-usnc.nuget.org (MailKit 5.0.0, owners, downloads, nuspec).
- learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient (SmtpClient not recommended, use MailKit).

### Secondary (MEDIUM confidence)
- MailKit API usage shown in examples is from training knowledge of the public API; confirm member names against the 5.0.0 package while planning.

### Tertiary (LOW confidence)
- Mailpit as the dev SMTP sink; Discord DM behavior for users without a mutual guild beyond the documented error codes.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH for MailKit choice and repo stack; MEDIUM for MailKit 5.0.0 member names (not compiled here).
- Architecture: HIGH; derived from code read this session, with the prepaid-slice formula the main novel finding.
- Pitfalls: HIGH for in-repo seams (Instant serialization, silent Discord no-op, audience width, SQLite limits); MEDIUM for Discord live edge cases.

**Research date:** 2026-11-21
**Valid until:** 2026-12-21 (stable stack; re-check MailKit and Discord.Net versions if planning slips)

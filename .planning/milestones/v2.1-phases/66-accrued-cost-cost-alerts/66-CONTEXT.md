# Phase 66: Accrued Cost & Cost Alerts - Context

**Gathered:** 2026-11-21
**Status:** Ready for planning

<domain>
## Phase Boundary

A cost owner sees what the current session has cost so far (workspace cost strip, accrued slot reserved in Phase 63),
and is alerted in-app, by Discord DM, and by email when a configured cost threshold is reached. The control plane
calculates cost and sends alerts; the browser never calculates. Requirements: CST-03, CST-04, CST-05, CST-06.

</domain>

<decisions>
## Implementation Decisions

### Accrued cost basis (CST-03)
- **D-01:** A session is one active `GameServerDeployment` (created to resolved). Accrued cost = sum of charged
  `ProviderCostSlice` ledger entries for that deployment plus the in-progress slice prorated by rate x elapsed. It
  advances while the server is costing (`PossibleCost`) and stops advancing once cost has stopped. Never a fake zero;
  unknown shows as unavailable.
  *Amended 2026-11-21 by D-15:* the session is no longer prorated by elapsed time; its billable time rounds up to the
  provider's billing unit. Everything else in D-01 stands.
- **D-02:** **No polling where avoidable.** The API calculates accrued cost and exposes it by REST (initial load and
  fallback) and **pushes updates over the existing WebSocket update stream** while a session is costing. The browser
  displays what the API sends (value plus as-of time); it does not own the calculation. Cadence of pushes and any
  smoothing between pushes is Claude's discretion, but must not require the client to poll.

### Thresholds (CST-04)
- **D-03:** Thresholds are configured **per cost owner** and apply to all sessions billed to that owner.
- **D-04:** A threshold is an **absolute EUR amount of accrued session cost**; an owner can configure **several**
  levels (e.g. 5, 10, 25). Each crossing alerts once.
- **D-05:** **No default thresholds**: no alerts fire until the owner configures at least one. The configuration UI
  must make this state clear.
- **D-06:** The owner edits thresholds and per-channel on/off in the **React cost-owner view**, owner-authorized (new
  API endpoint and DTO fields allowed per AGENTS.md; regenerate OpenAPI snapshot and clients). In-app is always on.

### In-app alert (CST-04)
- **D-07:** Each crossing creates a **persisted alert record** (server, amount, threshold, time). It appears in the
  existing ProfileMenu notifications list and as a **banner in that server's workspace**, with a link that opens the
  shared cost-stop confirmation ("Shut down and stop billing"). Delivered via WebSocket push, not polling.
- **D-08:** An alert stays visible **until the owner dismisses it** (per-alert dismissal), including after the session
  ends.
- **D-09:** A cost owner with no deliverable Discord or email address still receives the in-app alert.

### Discord and email delivery (CST-05, CST-06)
- **D-10:** Discord DM goes through the existing `DiscordBot` (`WithDiscordAsync`); no DM code exists yet.
- **D-11:** Email uses a **provider-neutral `IEmailSender` with an SMTP implementation** configured under a new config
  section; the channel is cleanly disabled (delivery marked skipped) when unconfigured. Never log credentials.
- **D-12:** Email recipient: `User.EMail` or any other mail source the app knows, but **exactly one email is sent per
  alert, preferring the app-local address**. The user's **locale must be recorded** and used to render the message
  (EN/DE, text in API resource files per AGENTS.md). The researcher confirms whether a per-user locale is stored today
  and adds it in the owning layer if not.
- **D-13:** Recipients for non-personal cost owners: whoever the owner type resolves to; researcher confirms from the
  `CostOwner` model. Anyone authorized on the owner sees the in-app alert; unresolvable means in-app only.

### Durability (success criterion 4)
- **D-14:** A **persisted delivery record per alert and channel** (unique alert+channel; status
  pending/sent/failed/skipped). A background dispatcher claims pending rows, retries transient failures a bounded
  number of times with backoff, and never resends a `sent` row. Crossings are recorded once per (deployment,
  threshold) so an API restart cannot re-alert. A new session (new deployment) starts fresh crossings. Persist intent
  before sending (same discipline as provider mutation). EF migration must be scaffolded with `dotnet ef`.

### Gap closure decisions (user, 2026-11-21, after the end-of-phase UAT in `66-UAT.md`)
- **D-15:** Accrued cost follows the provider's billing granularity (UAT gap 1, CST-03). A provider-neutral **billing
  unit per runtime provider**: the provider declares its unit, and the accrued-cost calculator rounds the charged time
  up to that unit instead of prorating by elapsed time. Hetzner's started-hour rule is the first implementation (a
  session that has run 5 minutes has incurred a full hour). Threshold crossings use the same granularity-aware accrued
  value. Provider and runtime boundaries stay provider-neutral (`IRuntimeProvider` area, AGENTS.md architecture).
  Focused tests: a 5-minute session costs a full hour, the hour boundary, several slices, a provider stub with another
  unit, and a threshold crossing at minute 1.
- **D-16:** Design pass over all three cost alert surfaces (UAT gap 2): the workspace alert banner, the account menu
  alert rows, and the cost alerts settings page (`CostAlertSettingsPanel`, also on the finance account page). The
  specifics are the planner's call. Reuse shared components under `sources/ReactApp`, keep EN/DE locale parity, and
  respect the recorded React UI dislikes (no `<details>`, no ugly native select, no misaligned review dialog). The
  UI-SPEC stays the contract unless a plan explicitly amends it.
- **D-17:** Nothing but the viewer dismisses a cost alert (UAT gap 3). Audit every path that could remove or hide an
  alert (a redelivered or invalidated alert list, WebSocket frames, refetches), fix any cause found with a test, and
  give every dismissal visible, localized (EN/DE) feedback such as a toast or an inline confirmation.
- Out of scope for the gap closure: the profile badge count (todo
  `2026-11-21-replace-profile-badge-with-notification-count-and-popup`).

### Claude's Discretion
- WebSocket message shape and push cadence, alert/threshold/delivery table design, dispatcher structure, retry
  count and backoff, exact copy and German strings, cost-strip presentation of the accrued value, component split
  under `src/components/server/` and `src/features/hosting/`, SMTP library choice (MailKit suggested).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning
- `.planning/ROADMAP.md` Phase 66 and Cross-Phase Constraints - goal, success criteria, per-phase gates
- `.planning/REQUIREMENTS.md` - CST-03..CST-06, FND-04 (cost confirmation), L10N rules
- `.planning/phases/63-workspace-shell-activity/63-CONTEXT.md` - D-05/D-06 accrued slot, D-07 WebSocket reuse, D-12 vocabulary
- `.planning/phases/65-change-a-server/65-CONTEXT.md` - D-00a API not frozen, D-00b routes not stable
- `AGENTS.md` - architecture boundaries, EF migration rule, XML docs, enum `E` prefix, commit format

### Code
- `sources/Api/Services/GameServerLifecycle/HostingCostSliceService.cs` - slice rate and amount calculation
- `sources/Api/Services/GameServerLifecycle/HostingFinanceOptions.cs` - finance config section
- `sources/Database/Hosting/HostingCostLedgerEntry.cs`, `GameServerDeployment.cs`, `CostOwner.cs` - cost data model
- `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs` (`GetNotificationsAsync`),
  `FleetChangeNotifier.cs`, `FleetChangeInterceptor.cs`, `sources/Api/DTO/GameServerNotificationDto.cs` - notifications and push
- `sources/Api/HostedServices/DiscordBot.cs` - bot access for DMs
- `sources/Database/Authority/User.cs` - `EMail`, Discord data
- `sources/ReactApp/src/components/server/CostStrip.tsx` (accrued slot), `CostConfirmationProvider.tsx`,
  `src/components/ProfileMenu.tsx` (notification list, 15s poll), `src/features/hosting/FleetLiveUpdatesProvider.tsx`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CostStrip` accrued slot with `SlotPlaceholder` and keys `Server_Workspace_Cost_Accrued*`: replace placeholder text.
- `useCostConfirmation().request(...)`: the alert link target.
- `FleetChangeNotifier` and the Phase 62 WebSocket client: push channel for accrued and alerts.
- `HostingCostLedgerEntry` slices: already durable and idempotent per (deployment, slice start).

### Established Patterns
- Persist intent before side effects; background services resume from durable state.
- API-owned localized text in resource files; React text in `en.json`/`de.json` with parity gate.
- No secrets in logs; SMTP credentials follow existing config conventions.

### Integration Points
- New alert/threshold/delivery entities in `sources/Database/Hosting`; dispatcher as hosted service in `sources/Api`.
- ProfileMenu currently polls notifications every 15s; move alerts to push per D-02/D-07.
- No SMTP or email infrastructure exists yet.

</code_context>

<specifics>
## Specific Ideas

- User wants WebSocket-first delivery and to reduce polling across the board where possible (scope here: accrued
  cost and alerts only).
- Only one email per alert, app-local address preferred.

</specifics>

<deferred>
## Deferred Ideas

- Replacing the existing ProfileMenu 15s notification poll for non-cost notifications with push (broader than this phase).
- Percentage-of-allowance thresholds and per-server thresholds.

</deferred>

---

*Phase: 66-Accrued Cost & Cost Alerts*
*Context gathered: 2026-11-21*

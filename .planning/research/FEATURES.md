# Feature Research

**Domain:** Member-first operations UI for cloud-hosted, cost-bearing, owner-attributed game servers (milestone v2.1 "Server Experience Reimagined", UI-only, `sources/ReactApp/src/features/hosting`)
**Researched:** 2026-11-18
**Confidence:** MEDIUM overall. Pattern-level findings are HIGH (several independent products converge). Exact current UI layouts of Crafty 4, Fly dashboard, and Hetzner Console could not be confirmed from docs and are marked LOW where relied on. Existing-code dependencies are HIGH (read directly from the repo).

## Framing: what the competitor set actually teaches

The reference products fall into three groups, and each teaches a different lesson. Copy the lesson, not the product.

| Group | Products | Lesson for UTF |
|-------|----------|----------------|
| Game-server panels (expert/operator audience) | Pterodactyl, Crafty, AMP | Power controls always visible and per-server; mod/config changes take effect on restart; per-server permissions; mod stores filtered by game version and loader. Their weakness is exactly UTF's stated pain: dense tab sets, expert vocabulary, no cost concept. |
| Consumer game hosts (non-expert audience) | Aternos, Shockbyte/exaroton-style hosts | One obvious Start button, a visible "what is happening" state (queue, loading), mods installed from a searchable library in a few clicks, compatibility handled for the user. |
| Cloud consoles (cost-aware audience) | Hetzner Cloud Console, Fly.io, Railway, Vercel | Cost is first-class. Staged "review then apply" changes (Railway), one status tile plus instant rollback/undo (Vercel), scale-to-zero semantics with an explicit stopped-means-not-billed contract (Fly). Hetzner is the cautionary tale: a powered-off server still bills until deleted, which is the same trap UTF's "teardown is the cost-stop" decision already addresses. |

UTF's unusual position: it is a panel for non-experts that also has real money attached and an owner who pays. That makes the cloud-console patterns (cost, review-before-apply, explicit stop semantics) more relevant than the panel patterns, while the consumer-host patterns (one button, plain language) set the audience bar.

## Feature Landscape

### Table Stakes (Users Expect These)

Missing these = the UI keeps feeling "unpolished, can't find things, disconnected, unclear state".

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| One primary action per server, visible in the fleet row AND on the workspace header, without opening a tab | Pterodactyl shows Start/Stop/Restart/Kill above the console; Crafty places start/stop/restart next to the server name and keeps them visible when the card is collapsed; Aternos is one big Start. | MEDIUM | `primaryServerAction()` already ranks actions (cost-stop first when possibleCost, then start, then stop). Today it renders only inside the Overview tab body. Promote it to a persistent header and to the fleet card. Reuse `ActionConfirmation`. |
| Single, plain-language status per server (Off / Starting / Running / Stopping / Needs attention / Cleaning up) with the deployment stage as secondary detail | Vercel collapses build state to one tile; Aternos shows loading/queue as a first-class state. UTF currently shows `humanizeToken(deployment.status)` plus stage plus worker status in three places. | MEDIUM | Needs a derived "member status" view-model mapping deployment status, stage, workerStatus, pendingJobCount, faultSummary, possibleCost. Pure client-side in `model.ts`. Must be testable like the existing `model.test.ts`. |
| Fleet overview that answers "which servers exist, which are on, who owns/pays, what do I do next" in one scan | Hetzner project list and Railway canvas give name, state, and size per resource at a glance. Members "usually see most servers disabled", so the default view must make off-by-default servers look intentional, not broken. | MEDIUM | `ServerFleetView`, `ServerCard`, `orderFleetServers` exist. Redesign: group/sort by "running or costing first", then "mine", then disabled; search and filter (game, state, owner). Do not hide disabled servers; collapse them under a labelled group. |
| Running-cost visibility on every server surface: "costing now" vs "not costing", hourly rate, owner who pays | Hetzner exposes usage/cost only as a separate Usage view; Railway/Vercel show usage on the project. For a cost-bearing member action, "this will cost about X/h, paid by Y" must be visible before and after pressing Start. | MEDIUM | Data exists: `estimatedHourlyCostEur`, `estimatedMonthlyCostEur`, `costOwnerName`, `deployment.possibleCost`, action `costImpact`/`costDescription`. Build one reusable cost chip + cost-owner chip. No accrued-so-far figure exists (see Gaps). |
| Start/stop confirmation that states cost and duration consequences in plain words | Hetzner docs and community threads show confusion that power-off still bills. Fly documents "stopped/suspended means no CPU/RAM billing" explicitly. | LOW | Already supported by `confirmationText`, `confirmationChallenge`, `costDescription`, `expectedDurationSeconds`. Reword and present consistently; keep typed-challenge only for the destructive/teardown path. |
| Honest in-progress feedback for long lifecycle jobs (provisioning, installing, tearing down) with progress, current step, and an expected duration | Aternos queue/loading state; Vercel build states. Starting a cloud server takes minutes; silent buttons read as "broken". | MEDIUM | `LifecycleJob.progressPercent`, `stage`, `expectedDurationSeconds`, `pending` exist. Surface a single "what is happening now" strip; keep the raw Jobs tab as the expert view. |
| Create-server flow that is a guided sequence, not one long form | Crafty creation is "pick type, pick source, set RAM, Create"; Aternos is minimal-choice. UTF's `ServerCreationFormValues` has 12 fields (provider, profile, storage strategy, GiB, mod pack, terrain, scenario, profile, cost owner, launch-disabled). | HIGH | See Create-wizard design notes below. Keep `createServerSchema`/`createServerModel` as the validation core. |
| Connection info one click away once running: address, copy button | Every panel and consumer host surfaces the join address prominently; members start servers to join them. | LOW | `deployment.connectionEndpoint`/`publicAddress`/`connectionProtocol` exist, currently buried in facts lists. Show only when the server is actually reachable. |
| Failure states that say what failed, whether it is costing money, and the next step | Vercel stuck/"unknown" status threads show the cost of vague errors. UTF has `faultSummary`, `failureReason`, `possibleCost` already. | MEDIUM | Lead with "Needs attention: <reason>. It may still be costing money. Next: <action>". Keep diagnostics (SSH command, cloud-init commands) behind an expert disclosure. |
| Navigation that connects the pages: breadcrumbs, back to fleet, create button on fleet, links between server, cost owner finance account, configuration profile, mod pack | Cited pain "disconnected". Cloud consoles keep project/resource breadcrumbs always. | MEDIUM | Routes exist under `command/...`. Add consistent `PageHeader` with breadcrumb and cross-links (server to its profile, to its cost account in `hosting-finance/accounts/:costOwnerId`). |
| Role-aware controls: show disabled actions with a reason instead of hiding them; hide expert tabs from members who cannot use them | Pterodactyl/Crafty per-server permissions; Hetzner hides nothing but fails late. | LOW | `canModify`, `canManageSecrets`, `unavailableReason`, `enabled` exist. Replace the brittle literal-string compare for the "paused by operator" reason (see Dependencies). |
| Mobile-usable server list and workspace | Members will start a test server from a phone before an event. Known debt UI-60-001: selected-server tab clipping on mobile. | MEDIUM | Fix as part of the tab/navigation redesign, not as a patch. |
| Localized copy (English and German) | Project constraint. | LOW | RESX-driven. The workspace currently has hard-coded English ("Credentials", "Terrain", "Scenario"); new work must not add more. |

### Differentiators (Competitive Advantage)

Align with Core Value: durable, auditable, cost-owned operations. These are where UTF can beat generic panels for its own community.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| "Cost owner is always visible and changing nothing is the default" cost strip (rate, owner, "costing now", teardown = stop-cost) | No game panel has a cost concept; cloud consoles hide it in a separate Usage page. Putting "who pays, how much per hour, and the one button that stops it" on the server header makes the cost-bearing model feel safe. | MEDIUM | Pure presentation of existing fields. Name the cost-stop action in member language ("Shut down and stop billing") while keeping the contract id. |
| Staged "review changes, then apply" for mod/config edits (Railway pattern) | Members change mods/config on an existing server. Railway batches edits, shows a diff of old vs new, lets you discard individual changes, and redeploys only what is affected. For UTF this removes "did my change save? do I need to restart?" ambiguity. | HIGH | Client-side staging is feasible: `ServerConfigurationPanel` already has `dirty` state and a save mutation. A real "apply" step with diff needs a pending-changes concept; the restart/apply semantics may need an API signal (see Gaps). Recommend a changes bar ("3 changes, not yet applied: Review, Apply, Discard") as the target; ship a smaller version first. |
| Explicit "change takes effect on restart" status per server ("Running with older settings. Restart to apply.") | Pterodactyl: startup variables apply only on restart. The Blueprint mod installer explicitly leaves restart to the user. Panels leave this implicit; naming it removes the most common member question. | MEDIUM | Needs a drift signal: compare current pinned revision ids (`pinnedModPackRevisionId`, `pinnedConfigurationProfileRevisionId`, terrain/scenario versions) against what the running deployment started with. The server DTO exposes the pinned ids but there is no "started with" counterpart (see Gaps). |
| Guided "Add a mod / change mods" flow with compatibility handled for the user | AMP's store filters to the server's game version and loader; Aternos requires picking a loader first and warns about version mismatch. The pain is non-experts choosing incompatible mods. | HIGH | Capability DTO already exposes `supportsModPacks`, `requiresModPack`, `modInstallerIdentifiers`, `modProviderBindings`, `modPackFormats`. Existing create flow selects a mod pack definition. Scope v2.1 to: view current mods/mod pack, pick a different pack or revision from known definitions, review, apply. Searching external mod catalogs (Modrinth/CurseForge/Workshop) is a backend feature, not UI-only (see Anti-Features and Gaps). |
| Create-server presets/templates ("Arma 3 test server for an event": disabled by default, sensible size, default profile, my cost account) | Aternos and Crafty minimize choices; cloud consoles use templates. Members "usually start disabled-by-default servers", so the default path should be 3 decisions, not 12. | MEDIUM | `isLaunchDisabled`, configuration profiles, `RuntimeProfile.estimatedHourlyCostEur`, `PlacementEstimate` exist. Preset = pre-filled `ServerCreationFormValues` + progressive disclosure ("Advanced: provider, storage"). Client-defined presets are enough for v2.1; server-defined presets are a later API item. |
| Live cost estimate that updates as the member picks size/storage, shown before Create | `combinedHourlyCost()` and `PlacementEstimate` already compute this; most consoles show price only at the end or in a separate page. | LOW | Move the estimate into a sticky summary panel in the wizard. Add "per event (4h)" style framing only if trivially derived. |
| Per-server "what happened" timeline written for humans (started by X, mod pack changed to Y, shut down, cleaned up) with the raw job/command detail one click deeper | Vercel's deployment list and Railway's Activity (with undo/revert) make history legible. UTF has audit operations and jobs, but they are split across Jobs and Audit tabs and use raw tokens. | MEDIUM | `HostingAuditFeed`, `HostingOperation`, `LifecycleJob` exist. Merge into one Activity view with plain headlines and an "Details" expander. Do not remove the existing job retry/skip, move it under the failed entry. |
| Safe-by-default error recovery: one "Retry" on the failed step, explicit "this may still cost money" warning | Cloud consoles bury retry; UTF has `canRetry`/`canSkip`. | LOW | Keep skip behind an expert disclosure; members should see Retry only. |
| Cost-owner transparency links (server shows owner and balance/"frozen" state, links to account) | UTF-only: owner attribution is the core value. `isFrozen` already blocks creation (`isCostOwnerSelectionValid`). | LOW | Surface frozen state early in the wizard and on Start, not after a failed request. |
| Consistent "next action" ranking carried everywhere (fleet card, header, notifications) | UTF already ranks actions; making one source of truth drive all surfaces is the connected feeling members lack. | LOW | Reuse `primaryServerAction` and `ServerNotification.costStopAction`. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Web console / RCON terminal as the main control surface (Pterodactyl's console-first layout) | Panels lead with the console; operators like it. | Console-first screens are expert-first and put raw process control in the member's face. Command execution is a worker/control-plane concern and a security surface; this milestone is UI-only. | Keep the existing Logs and Runtime views as secondary tabs; lead with status, cost, and the primary action. |
| "Kill" / force-stop button beside Stop | Present in Pterodactyl's power set. | Data-loss risk for non-experts; and UTF's real cost control is teardown, not process kill. A second stop-like button re-creates the Hetzner "powered off but still billing" confusion. | One member-facing stop path that states cost consequences; keep force-style actions in the expert action list behind the existing typed-confirmation (`confirmationChallenge`). |
| Showing every server setting as a flat editable form (Pterodactyl Startup tab, AMP instance settings) | Completeness. | Non-experts cannot tell safe from dangerous fields. UTF `GameConfigurationEntry` already carries `group`, `required`, `sensitive`, `isControlPlaneOnly`, `warning`. | Group by purpose with a "Common" section first and "Advanced" collapsed; never render control-plane-only or sensitive entries inline. |
| Free-form file editing as the way to add a mod (upload into the file tree) | Panels and some hosts (exaroton paid tier) rely on file access. | Easy to break a server; no compatibility checking; hard to review or revert. | Keep the Files panel for experts. Member path is structured mod/mod-pack selection with review and apply. |
| Instant live-apply of config/mod changes to a running server | Feels responsive; Fly/Vercel apply immediately. | Silent restarts or half-applied state on a cost-bearing game session with players connected. Pterodactyl and mod installers explicitly leave restart to the user. | Stage, show "applies on next start / Restart to apply", let the member choose when. |
| Auto-stop on idle / scheduled start (Fly autostop, AMP scheduler, Crafty schedules) as part of this milestone | Cost savings; Fly proves the model. | Needs backend lifecycle policy, worker readiness signals, and billing semantics; Fly itself warns that mismatched autostop/autostart strands services. UI-only milestone cannot own this. | Record as a future API+UI feature. In UI, make the cost-stop action impossible to miss and show "costing now". |
| Server-wide metrics dashboards (CPU/RAM graphs, player counts) | Panels all have them. | No such data contract exists in the API projection; fake charts mislead. Worker disk telemetry is the only live number today. | Show the telemetry that exists (disk) and defer the rest. |
| Hiding disabled/off servers from the fleet by default | Declutters. | Members mostly see disabled servers; hiding them removes the entry point for "start the test server". | Show them in a labelled "Off" group with the Start action inline. |
| Provider-flavored vocabulary in member UI ("deployment stage cloud-init", "worker heartbeat", "provider resources", Hetzner terminology) | Accuracy. | Violates the provider-neutral constraint and fails the audience. | Member status in plain words; keep provider/worker/resource detail in an "Infrastructure" expert section (the current Cloud tab content). |
| Billing/budget features inside the server UI (budgets, invoices, alerts) | Cloud consoles have Usage/alerts. | Hosting finance already exists as its own area; duplicating it here fragments cost truth. | Link from the server cost strip to the existing finance account page. |
| Redesigning events/wiki/mod-pack/users pages | Visual consistency. | Out of scope; PROJECT.md limits this to a light consistency pass. | Shared layout, header, spacing, and component fixes only. |

## Feature Dependencies

```
Derived member status (model.ts view-model)
    └──feeds──> Fleet card status + primary action
    └──feeds──> Server header (status, cost strip, primary action)
                    └──requires──> primaryServerAction + ServerAction DTO (existing)
                    └──requires──> Cost chip + owner chip components

Server header (persistent)
    └──enables──> Re-organised workspace navigation (fewer, clearer sections)
                      └──enables──> Mobile tab fix (UI-60-001)

Create wizard
    └──requires──> createServerModel schema (existing)
    └──requires──> Live cost estimate (combinedHourlyCost, PlacementEstimate; existing)
    └──enhanced by──> Presets (client-defined defaults)
    └──requires──> Cost-owner frozen check early (isCostOwnerSelectionValid; existing)

Staged changes bar (review/apply/discard)
    └──requires──> Configuration panel dirty state (existing)
    └──enhanced by──> Drift signal "running with older settings"  (needs API decision)
    └──enhanced by──> Mod/mod-pack guided flow (supportsModPacks, capability DTO)

Unified Activity timeline
    └──requires──> HostingAuditFeed + jobs (existing)
    └──enhanced by──> Job progress strip (progressPercent, expectedDurationSeconds)

Light consistency pass (layout/header/spacing/shared components)
    └──should precede──> Fleet and workspace redesign (shared PageHeader, StatusBadge, chips)

Auto-stop / scheduling ──conflicts──> UI-only scope (needs API + worker)
Live-apply of config ──conflicts──> Staged apply model
```

### Dependency Notes

- **Shared components and header come first:** every redesigned surface needs the same status badge, cost chip, owner chip, and page header. Build them once in `src/components` (project rule: reuse shared components, no raw inputs posing as buttons) before touching fleet, create, or workspace.
- **Derived member status is the keystone:** fleet card, header, notifications, and the next-action ranking all consume it. It should be a pure, unit-tested function beside `model.ts`, using only existing DTO fields.
- **Staged changes requires the drift decision:** a client-only staged bar works with the existing save mutation. A truthful "restart to apply" badge requires knowing what the running deployment started with. That is an API projection gap and, per PROJECT.md, needs an explicit decision before it is promised.
- **Create wizard requires no new API:** all inputs (profiles, providers, owners, mod packs, terrains, scenarios, profiles, estimates) already exist.
- **Mod flow is capability-driven:** zero references to Minecraft exist in `features/hosting` today. Arma 3 is the only React implementation (`implementations/arma.ts`). Build the member mod flow against `ServerCapability` fields so a Minecraft implementation can plug in via `implementations/registry.ts`, and validate it with the Arma mod pack path. Do not hard-code Minecraft concepts.

## MVP Definition

### Launch With (v2.1 core)

Minimum set that makes "start a server" and "change a server's mods/config" succeed without help.

- [ ] Shared building blocks: status badge, cost chip, owner chip, page header with breadcrumb, persistent primary-action button (reusing `ActionConfirmation`) — everything else depends on them
- [ ] Derived member status view-model with unit tests — single plain-language state per server
- [ ] Redesigned fleet: one-scan rows/cards with status, owner, cost, inline primary action; group off servers, search/filter, prominent Create
- [ ] Server header on the workspace: status, cost strip, primary action, connection info, then a simpler section navigation (fixes UI-60-001)
- [ ] Guided create flow with sensible defaults, disabled-by-default awareness, live cost estimate, early cost-owner validation
- [ ] "Change this server" flow for mods and configuration: common settings first, review of what changes, explicit apply, clear statement of when it takes effect
- [ ] In-progress and failure presentation: job progress strip, "needs attention + may be costing" panel, Retry as the member action
- [ ] Cost-stop clarity: member-language shutdown action with cost consequence text, cost-owner link to finance
- [ ] Light consistency pass: layout, nav, headers, spacing on the rest of the React app

### Add After Validation (v2.1.x)

- [ ] Unified Activity timeline merging Jobs and Audit with plain headlines — add once the header and status model are stable
- [ ] Client-defined presets ("Event test server") — add when the wizard is in use and recurring choices are observable
- [ ] Review diff with per-change discard in the changes bar — add after the basic apply flow proves clear
- [ ] Drift badge "Running with older settings" — add only once an API signal exists

### Future Consideration (v2.2+)

- [ ] Auto-stop on idle, scheduled start/stop — needs lifecycle policy in API and worker
- [ ] Accrued cost so far per session and cost alerts — needs billing data in the server projection
- [ ] Player count, readiness, resource metrics — needs worker telemetry contracts
- [ ] Mod catalog search (Modrinth/CurseForge/Steam Workshop) and per-mod install — backend integration; the capability DTO hints at `modProviderBindings`, but a search/install contract is not a UI feature
- [ ] Server-defined presets/templates managed by admins
- [ ] Undo/revert of applied changes (Railway/Vercel style) — needs revision history semantics beyond profile revisions

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Shared status/cost/owner components and page header | HIGH | LOW | P1 |
| Derived member status view-model | HIGH | MEDIUM | P1 |
| Persistent primary action (fleet + header) | HIGH | MEDIUM | P1 |
| Fleet redesign (grouping, search, inline action) | HIGH | MEDIUM | P1 |
| Server header with cost strip and connection info | HIGH | MEDIUM | P1 |
| Guided create flow with live estimate | HIGH | HIGH | P1 |
| Workspace navigation restructure + mobile fix | HIGH | MEDIUM | P1 |
| Member "change mods/config" flow with review and explicit apply | HIGH | HIGH | P1 |
| Progress strip and failure panel | HIGH | MEDIUM | P1 |
| Light consistency pass elsewhere | MEDIUM | MEDIUM | P1 |
| Unified Activity timeline | MEDIUM | MEDIUM | P2 |
| Create presets (client-defined) | MEDIUM | LOW | P2 |
| Per-change diff and discard | MEDIUM | MEDIUM | P2 |
| Drift badge ("restart to apply") | HIGH | MEDIUM (needs API decision) | P2 |
| Expert section for infrastructure/diagnostics | MEDIUM | LOW | P2 |
| Auto-stop/scheduling | HIGH | HIGH (backend) | P3 |
| Accrued cost, alerts | MEDIUM | HIGH (backend) | P3 |
| Mod catalog search | HIGH | HIGH (backend) | P3 |
| Metrics, player counts | LOW | HIGH (backend) | P3 |

**Priority key:** P1 must have for v2.1; P2 should have when possible; P3 deferred.

## Create-Server Wizard Design Notes

Evidence: Crafty creation is "type, source, memory, Create"; Aternos exposes minimal choices; the UTF form has 12 fields, of which only about 4 should be a member decision.

- **Step 1, What and why:** game (capability list), title, and a preset if any (for example "Event test server"). Everything else defaults.
- **Step 2, Content:** mod pack / terrain / scenario / configuration profile, only the fields the selected capability requires (`requiresModPack`, `requiresTerrain`, `requiresScenario`). Use the capability flags already present rather than game-specific branches.
- **Step 3, Size and cost:** profile choice with plain labels ("Small, about EUR X/h"), storage under "Advanced" (IncludedDisk default), `PlacementEstimate` shown as a fit check, cost owner defaulted to the current user with frozen-account blocking surfaced here.
- **Step 4, Review:** one summary with cost per hour and per month, who pays, and a single explicit choice: "Create and keep off (recommended)" vs "Create and start now". `isLaunchDisabled` maps to the first. State that creating does not bill until it is started if that is true for the provider flow; verify with the API before writing that sentence.
- Persist progress in the URL or form state so Back never loses entries; validation errors appear at the step that owns the field.

## Change-a-Server (Mods and Config) Design Notes

Evidence: Railway stages edits as a changeset with a diff and applies them together; Pterodactyl and mod-installer addons apply on restart and leave restart to the user; AMP/Aternos filter content by game version and loader.

- Entry points: a "Change mods" and "Change settings" action on the header, not a deep tab.
- Show current state first (current mod pack and revision, current profile), then the editable choice, then a "Review changes" summary of old value to new value.
- Apply semantics must be stated plainly per server state. If off: "Applies the next time you start." If running: "Restart to apply" with cost consequence of the restart. Never silently restart.
- Only expose fields marked editable and not control-plane-only or sensitive; group Common vs Advanced.
- The Files panel remains the expert escape hatch, labelled as such.

## Competitor Feature Analysis

| Feature | Pterodactyl / Crafty / AMP | Hetzner Cloud / Fly / Railway / Vercel | Our Approach |
|---------|---------------------------|----------------------------------------|--------------|
| Start/stop/restart | Pterodactyl: Start, Stop, Restart, Kill above the console. Crafty: three buttons beside the server name, visible when collapsed (Crafty 3 docs; Crafty 4 layout unconfirmed). | Hetzner: power-off is not cost-stop; deleting is. Fly: stopped or suspended machines are not billed for CPU/RAM; autostop/autostart via proxy. Vercel: promote/redeploy/rollback from a deployment tile. | One primary action per server on card and header; cost-stop is the separately named "shut down and stop billing" action that UTF already models as provider teardown. No Kill. |
| Fleet overview | Panel server lists with inline power and resource stats. | Project resource lists/canvas with state and size. | Status, owner, cost, inline next action; Off servers grouped, not hidden. |
| Create | Crafty: pick type, source, RAM. Pterodactyl: admin-side eggs/nests (operator task). Aternos: choose software, play. | Cloud consoles: long forms with price estimate on the side. | Short guided wizard with presets, live estimate, review with keep-off default. |
| Mod changes | AMP: store with game-version/loader filtering and updates. Pterodactyl: addon installers from Modrinth/CurseForge; restart left to user. Aternos: library installs; one modpack at a time; version/loader must match. | Railway: staged changeset with diff, discard, apply; only affected services redeploy. | Capability-driven structured selection, review, explicit apply with stated effect time. Catalog search deferred (backend). |
| Config apply/restart | Pterodactyl: startup variables only applied on restart. | Railway/Vercel: change then deploy; Vercel offers undo rollback. | Stage, show "applies on restart", and add a drift badge when the API can provide it. |
| Status | Console-centric, text state. | Vercel: one deployment status tile; common complaints are vague "unknown"/stuck states with no error shown. | One plain status plus a reason and a next step; no state without an explanation. |
| Cost visibility | None in panels. | Hetzner: current-month usage only, in a separate Usage menu. Fly: billing separate from dashboard. | Cost strip on every server surface: rate, owner, "costing now"; links to existing finance pages. |
| Permissions | Pterodactyl/Crafty/AMP: per-server roles. | Cloud consoles: project roles. | Reuse existing `canModify`/`canManageSecrets`/action `enabled` and `unavailableReason`. |

## Gaps and Items Needing an Explicit Decision

These are UI-adjacent needs that the API projection may not satisfy. API changes are in scope when a feature needs them (see AGENTS.md); the current API is not a frozen contract.

| Gap | Impact | Recommendation |
|-----|--------|----------------|
| No "settings changed since start / restart required" signal. `GameServerDto` exposes `pinned*RevisionId` for the intended state, but no value for what the running deployment started with. | Blocks a truthful drift badge. | Ship staged apply with plain "applies on next start" copy; raise the drift field as a small, explicit projection request. |
| "Hosting lifecycle execution is paused by the operator." is detected by comparing `unavailableReason` to an English literal in `ServerWorkspacePage`. | Brittle and untranslatable; breaks if the API wording changes. | Ask for a stable reason code, or isolate the literal in one adapter and cover it with a test. |
| No accrued cost for a session, no running-since time, no player count. | Cost strip can show rates and "costing now" only. | Present estimates honestly as estimates; defer accrued figures. |
| Minecraft is not implemented in `features/hosting` (no references). The milestone's second headline scenario ("add a mod to a Minecraft server") has no game implementation to exercise. | Risk of designing a flow nothing can run end to end. | Design game-neutral against `ServerCapability`; verify with Arma 3 mod pack flow; treat Minecraft as the portability test, not a deliverable. |
| `modProviderBindings` and `modPackFormats` exist on the capability DTO but are not mapped into the React `ServerCapability` type. | Mod flow may need to widen the type. | Investigate during the mod-flow phase; adding fields to the client alias type is UI-only. |
| Hard-coded English in the workspace ("Credentials", "Terrain", "Scenario"). | Violates localization constraint. | Fix as part of the redesign; add RESX keys in en and de. |

## Existing Code Reuse Map

| Existing asset (path under `sources/ReactApp/src/features/hosting`) | Reuse in v2.1 |
|-------------------------------------------------------------------|---------------|
| `workspaceModel.ts` `primaryServerAction` | Drives the persistent primary action everywhere. Keep; extend, do not fork. |
| `components.tsx` `ServerCard`, `ServerStatus`, `serverTone`, `ActionConfirmation`, `ServerActions`, `HostingError`, `HostingLoading` | Restyle/recompose; `ActionConfirmation` stays the single confirmation path. |
| `FleetPages.tsx` `ServerFleetView`, `orderFleetServers`, `HostingOverviewView` | Redesign target. Keep route manifest entries (`routes.tsx`) so certified route parity is not broken. |
| `ServerWorkspacePage.tsx` (nine tabs: overview, runtime, cloud, logs, files, configuration, credentials, jobs, audit; `?tab=` search param) | Restructure into header plus fewer sections; keep `workspaceTabFromSearch` URL compatibility or add redirects for deep links. Existing exports (`ServerWorkspaceView`) are likely tested; check tests before refactoring. |
| `CreateServerPage.tsx`, `createServerModel.ts` (`createServerCreationSchema`, `combinedHourlyCost`, `isCostOwnerSelectionValid`), `PlacementEstimate.tsx` | Wizard keeps this validation and estimation core. |
| `ServerConfigurationPanel.tsx`, `GameConfigurationEntry` (group, required, sensitive, isControlPlaneOnly, warning), `implementations/registry.ts` | Base for the member change flow and Common/Advanced grouping. |
| `HostingAuditFeed.tsx`, `LifecycleJob`, `HostingOperation` | Unified Activity timeline source. |
| `HostingFinancePage.tsx`, `HostingFinanceAccountPage.tsx` | Link targets from the cost strip; do not duplicate. |
| `authorization.ts`, `financeAuthorization.ts` | Keep as the only permission logic. |
| `copy.ts`, RESX resources | All new strings go through these (en and de). |
| `src/components/primitives` (`PageHeader`, `StatePanel`, `StatusBadge`, `ConfirmDialog`) | Foundation of the shared-component work and the light consistency pass. |

## Sources

Confidence tags follow source authority: official docs and release notes MEDIUM-HIGH, vendor community/forum posts MEDIUM, third-party guides and marketplaces LOW. All web searches were run through the built-in search tool; results were not individually cross-verified beyond the convergences noted.

- [Pterodactyl panel features and startup parameters (Mintlify mirror, unofficial)](https://www.mintlify.com/pterodactyl/panel/servers/startup-parameters) — startup variables apply only on restart; editable-flag gating. LOW-MEDIUM (unofficial mirror).
- [How to Use the Console in Pterodactyl](https://cptcr.featurebase.app/help/articles/9445126-how-to-use-the-console-in-pterodactyl) — Start/Stop/Restart/Kill above the console. LOW-MEDIUM.
- [Pterodactyl panel changelog](https://www.github.com/pterodactyl/panel/blob/develop/CHANGELOG.md) — official project history. MEDIUM.
- [Modpack & Mods Installer (Blueprint addon)](https://builtbybit.com/resources/modpack-mods-installer-blueprint.93733/updates) — one-click Modrinth/CurseForge installs; restart left to the user. LOW (marketplace listing).
- [Crafty Controller server creation docs](https://docs.craftycontrol.com/pages/user-guide/server-creation/minecraft/) and [Crafty 3 Web Console wiki](https://wiki.craftycontrol.com/en/3/Web%20Console/) — start/stop/restart buttons beside the server name; creation flow. MEDIUM for v3 layout, LOW for v4.
- [Crafty Controller Guide (space-node, 2026)](https://space-node.net/blog/crafty-controller-minecraft-panel-guide-2026) — third-party overview. LOW.
- [AMP 'Deimos' 2.7 release notes](https://discourse.cubecoders.com/t/amp-deimos-2-7-0-release-notes/38578) — Store plugin, game-version/loader-aware content, Modrinth and Paper Hangar sources. MEDIUM-HIGH (vendor). Note: later-source claims about CurseForge/Workshop status conflict; treat as unresolved.
- [AMP game server panel guide (space-node)](https://space-node.net/blog/amp-cubecoders-game-server-panel-2026) — per-instance console/files/scheduling. LOW.
- [Hetzner Cloud billing FAQ](https://docs.hetzner.com/cloud/billing/faq/) — powered-off servers still bill; delete to stop; snapshot alternative. MEDIUM-HIGH (official).
- [Fly.io autostop/autostart docs](https://fly.io/docs/launch/autostop-autostart) — stop vs suspend, matching autostop/autostart, no CPU/RAM billing when stopped. MEDIUM-HIGH (official). Dashboard UI not documented; LOW for any dashboard claim.
- [Railway staged changes docs](https://docs.railway.com/deployments/staged-changes) and [GA changelog](https://railway.com/changelog/2024-01-26-staged-changes-generally-available.md) — changeset, diff review, discard, apply, affected-only redeploy. MEDIUM-HIGH (official).
- [Vercel Instant Rollback docs](https://vercel.com/docs/instant-rollback) — status tile, rollback, undo rollback. MEDIUM-HIGH (official). Community threads on vague stuck states: LOW-MEDIUM.
- [Aternos queue](https://support.aternos.org/hc/en-us/articles/360026950812), [modpacks](https://support.aternos.org/hc/en-us/articles/4409967265425-Install-modpacks-Minecraft-Java-Edition), [plugins](https://support.aternos.org/hc/articles/360026950892) — queue/confirm start, loader-first mod flow, one modpack at a time. MEDIUM (vendor support).
- Repository (read directly, HIGH): `.planning/PROJECT.md`; `sources/ReactApp/src/features/hosting/` (`routes.tsx`, `ServerWorkspacePage.tsx`, `workspaceModel.ts`, `createServerModel.ts`, `types.ts`, `components.tsx`, `FleetPages.tsx`, `ServerConfigurationPanel.tsx`); `sources/ReactApp/openapi/utf-api.json` (`GameServerDto`, `GameServerActionDto`, `GameServerNotificationDto`, `GameServerCapabilityDto`).

---
*Feature research for: member-first cloud game-server operations UI*
*Researched: 2026-11-18*

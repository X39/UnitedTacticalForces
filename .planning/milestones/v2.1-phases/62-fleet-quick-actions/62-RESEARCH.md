# Phase 62: Fleet & Quick Actions - Research

**Researched:** 2026-11-18
**Domain:** React 19 fleet list (search/filter/URL state, quick start/stop through a shared confirmation) plus one small ASP.NET Core WebSocket "fleet changed" signal and one additive DTO field
**Confidence:** HIGH for the React integration surface (read from code this session); MEDIUM for the backend signal design (pattern is verified, the concrete placement is a recommendation)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Member entry point (ACC-02)**
- **D-01:** The Command Center stays the home of the server section. Command Center and the public-facing site are deliberately separate concepts: most members never see the Command Center, and those who do get a distinctive, less vibrant, more task-focused interface. Do NOT add a "Servers" link to the public/community header and do not blur the two surfaces.
- **D-02:** Members holding only `sv` or `sv:view` already pass `canAccessCommandCenter` and `canAccessGameServers` (`sources/ReactApp/src/auth/claims.ts`). Keep entering through the current path (community header "Command Center" link, then the `servers` nav item). Add a test proving a bare `sv` and a bare `sv:view` user see the Command Center entry and the servers item.
- **D-03:** Rename the nav item label to **Servers** (DE: **Server**) by changing the value behind `Hosting_Nav_Servers` (currently "Game servers"). ACC-02 is met by this label plus a fleet page whose title and copy carry no admin-oriented wording. The Command Center shell framing itself is not changed.

**Card content and small screens (FLT-01, FLT-05) - recommendations adopted**
- **D-04:** Each card shows: server title, game identifier, `ServerStateBadge`, `CostIndicator`, cost owner, one primary action, and an Open link. Runtime provider, queue count, and "updated" time leave the card (relocated to the workspace in Phase 63; mark the matching parity-matrix rows `relocated`, never deleted). The stage shows only while the server is starting, stopping, or working.
- **D-05:** One card layout at every width: a grid on wide screens, a single column at narrow widths. No separate compact-row layout. Actions wrap to full width; facts stack. Must be usable with every action reachable and no clipped content at 320px (and 390px).
- **D-06:** The three-tile summary strip is replaced by counts on the filter controls (All, Mine, Running, Off). When any server may still cost, one warning line stays above the list.

**Search, filter, grouping (FLT-02, FLT-03)**
- **D-07:** Search is case-insensitive and locale-aware over title, game identifier, and cost owner name.
- **D-08:** Filters are **All, Mine, Running, Off**. "Mine" means the signed-in user is the server owner or the cost owner, using fields already present in the fleet DTO; the researcher confirms which fields exist and whether a minimal projection is needed (any such API addition must stay inside the existing hosting DTO boundary).
- **D-09:** Search text and filter live in URL search params on the same route (`?q=` and `?filter=`), written with history replace so typing does not create history entries. Reload and shared links restore the view.
- **D-09a:** Results are grouped into two sections: **Active** (every state except `off`, including starting, stopping, attention, unconfirmed, and any server that may still cost) first, then **Off** below it with its count. The Off section is expanded by default and is never collapsed away or hidden. Grouping uses `deriveServerState` only, so a server that may still cost can never land in Off (FND-03). Empty search/filter results show an explicit "no matches" panel with a clear-filters action, distinct from the "no servers shared with you" empty state.

**Quick start and stop (FLT-04)**
- **D-10:** Primary action per state: **Start server** (gold) when off; **Shut down and stop billing** (danger) when running. States starting, stopping, working, or with a pending job show the action disabled with a visible reason. For attention and unconfirmed states, show the safest available cost-stopping action if the API offers it, otherwise Open. Every start and cost-stop opens `CostConfirmDialog` through `useCostConfirmation().request({ serverId, actionId, source: "fleet-card" })`. Actions the member cannot perform are shown disabled with the reason (`UnavailableReason`).
- **D-11:** **Terminate is removed from the fleet card.** It stays reachable from the workspace and through the `?action=terminate` deep link. Mark the fleet-card-terminate parity row `relocated` with evidence.
- **D-12:** After a confirmed action: toast, immediate invalidation and refetch of the fleet and notification queries, and the card shows its working state with the action disabled until the refreshed state arrives. No optimistic status flip.

**Live updates over WebSocket (FLT-04 "updates without a page reload")**
- **D-13:** Replace fleet polling with a **push signal**. The API sends a lightweight "server changed" message (server id only, no server data) on the existing path-scoped update stream; the React app invalidates the fleet query and refetches through the normal authorized endpoint. Because no server data crosses the socket, no per-viewer authorization filtering is needed on the broadcast. Remove the 10s `refetchInterval` from `useFleetQuery`.
- **D-14:** Signals originate from the durable lifecycle executor and job transitions (deployment status, `possibleCost`, pending job count changes). The stream is process-local like today's `UpdateStreamService`; document that a multi-instance API would need a shared backplane. Contract type lives in `sources/WebSocketContracts` (wire messages only); no game-process or provider logic enters the API.
- **D-15:** **No polling fallback** (user decision). The fleet relies on the socket plus the existing manual Refresh button. Claude's discretion: automatic socket reconnect with a refetch on (re)connect, and whether to show a visible "live updates disconnected" indicator, so a dropped socket never leaves a silently stale fleet. These do not reintroduce interval polling.
- **D-16:** The React app has no WebSocket client today. Add one reusable client/hook (not fleet-specific) under a shared location so Phase 63 can reuse it for the workspace. Other existing pollers (notifications, workspace) are not changed in this phase.

### Claude's Discretion
- Exact card layout, spacing, and section header styling within the `--utf-*` tokens; ordering inside each section (existing order: may-cost first, pending work, then name, is the starting point).
- Mapping of the "attention/unconfirmed" primary action when the API offers several actions.
- WebSocket reconnect/backoff mechanics and the disconnected indicator wording and placement.
- Component split for the fleet toolbar, section, and card under `src/components/server/` and `src/features/hosting/`.

### Deferred Ideas (OUT OF SCOPE)
- Workspace-level live updates and replacing the remaining pollers (notifications, workspace queries): Phase 63 can reuse the new WebSocket client.
- Full-state payloads over the socket with per-viewer authorization filtering: only if a signal-and-refetch proves too slow.
- Shared backplane for multi-instance API fan-out of update messages: future infrastructure work.
- Terminate from the fleet (overflow menu): not planned; workspace only.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FLT-01 | User sees every visible server with status, owner, cost indicator, and primary action in one scan | `FleetServerCard` over `deriveServerState` + `ServerStateSummary`; `primaryActionFor` against the API's real action availability (see "Action availability reality" under Pitfalls); cost-owner placeholder handling; Pitfall on `.button` `white-space: nowrap` |
| FLT-02 | User can search and filter servers (mine, running, off) | Pure `fleetModel` (search fold, filter predicates, counts); URL state through react-router `useSearchParams` with `replace`; `Mine` needs `costOwnerUserId` (verified: `CostOwnerId` is a CostOwner PK, not a user id) |
| FLT-03 | Off servers are grouped but remain visible | `partitionFleet` using `deriveServerState().state === "off"` only; Off section never collapsible; invariant already enforced by `deriveServerStateFromInput` |
| FLT-04 | User can start or stop a server from the fleet through the cost confirmation; card updates without a reload | `useCostConfirmation().request(... source: "fleet-card")`; additive `onSubmitted` hook for the D-12 working state; fleet-changed WebSocket signal, `useUpdateStream`, reconnect + refetch; backend notifier design and its three verified hazards |
| FLT-05 | The fleet is usable at 320px width | One-column card grid, `min(100%, var(--utf-fleet-card-min))`, wrapping buttons (legacy `.button` is `nowrap`), 2x2 chip grid at <= 520px, Playwright 320/390 spec + axe |
| ACC-02 | A member can find the server section from main navigation without admin framing | `Hosting_Nav_Servers` / `Hosting_Servers_Title` value change; extend (not duplicate) `member-access.test.tsx`; update the two e2e assertions that hard-code the old labels |
</phase_requirements>

## Project Constraints (from AGENTS.md)

`CLAUDE.md` does not exist. `AGENTS.md` (checked in) applies with the same authority as locked decisions:

- All identifiers, comments, XML docs and technical docs in English; localized user text only in resource files (here: React `en.json`/`de.json`).
- Every handwritten C# type and member needs meaningful XML docs (`<summary>`, `<param>`, `<returns>`, `<value>`, `<exception>`); one top-level type per file named after the type; enums use an `E` prefix. Generated code (Kiota under `sources/WebApp/UtfApi`, EF migrations) is exempt and never hand-edited.
- Architecture: API control plane, cloud runtime providers, hosting contracts, external worker and game implementations stay separated. No game-process execution in the API. "Adding or expanding a cross-cutting or server-wide feature requires an explicit user request and an architectural justification" - the fleet signal is explicitly requested (D-13..D-16); the justification for each touched API surface must be written into the plan.
- No EF migration is needed in this phase (the DTO field is a projection only). If one ever were, it must be scaffolded with `dotnet ef migrations add`.
- Commit cadence: commit each cohesive verified unit; subject format `type(scope)!,type(scope): summary`, under 80 characters, `!` only for breaking public contract changes. Both new items here are additive (no `!`).
- Subagent delegation: independent tasks (backend signal track, DTO track, React tracks) may run in parallel.
- UI: reuse shared components; never a raw `<input>`/`<button>` posing as a control. (`SearchField` wraps a native `<input type="search">`, which is an input, not a button posing as one.)
- Logging: structured templates, no secrets. Tests: add focused tests for every changed behaviour.
- ROADMAP Cross-Phase Constraints: `sources/Common/Ui/routes.json` byte-identical (`git diff --exit-code -- sources/Common/Ui/routes.json`), no new pathnames, `sources/WebApp` never edited, per-phase parity-matrix update, keyboard + 320px.

## Summary

The fleet is a UI rebuild on top of a complete Phase 61 foundation: `deriveServerState`, `ServerStateSummary`, `useCostConfirmation`, `UnavailableReason`, `ActionButton`/`ActionLink`, `StatePanel` and the `--utf-*` tokens all exist and are used by the current `ServerCard`. Nothing new has to be invented for state, cost or confirmation. The risk is concentrated in three places: (1) the new live-update path (a React WebSocket client that does not exist yet, and a backend signal on a shared service with verified concurrency and ordering hazards), (2) the D-12 "submitted" working state, which the shared confirmation provider currently gives the card no way to observe, and (3) the interaction between the API's real action-availability rules and the UI-SPEC's primary-action table (the API never enables `stop` outside `Running`, so attention/unconfirmed cards will always show a disabled stop).

Read-through of the backend produced four findings the planner must act on. First, `WebSocket.SendAsync` does not support concurrent sends, and `UpdateStreamService.SendUpdateAsync` swallows the resulting failure by silently dropping the subscriber from its registry, which with "no polling fallback" would create exactly the silently stale fleet D-15 forbids. A single serialized, coalescing publisher in front of `SendUpdateAsync` removes the hazard without editing the shared service. Second, lifecycle state is written in many places (executor, queue, lifecycle service, reconcilers, worker protocol) and mostly through `SaveChangesAsync` on contexts created from `AddDbContextFactory`; a singleton EF `SaveChangesInterceptor` (plus transaction-commit deferral) covers every site without adding a constructor dependency to services that tests instantiate by hand, with two `ExecuteUpdateAsync` sites needing an explicit call. Third, `GameServerDto.CostOwnerId` is a `CostOwner` primary key, so `Mine` needs the additive `CostOwnerUserId` projection; it must be null whenever the viewer cannot see the cost owner. Fourth, the update-stream subscription path is currently whitelisted for event paths only and the registry matches on a case-sensitive string prefix, so the client must send the exact canonical path.

**Primary recommendation:** Build a pure, fully unit-tested `fleetModel` first, then the presentational components and page, add the `useUpdateStream` hook + `FleetLiveUpdatesProvider`, and deliver the backend as two independent additive tracks (serialized fleet-signal notifier wired by an EF interceptor, and `CostOwnerUserId`) that the React side consumes only through a hand-written wire type and the regenerated OpenAPI types.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Server visibility per viewer | API / Backend | - | `GetServersAsync` filters on `HasServerPermission(... View)`; the browser never recomputes it |
| State, cost, section and filter membership | Browser / Client | - | Single decider `deriveServerState`; pure client function over the fleet payload |
| Search, filter, grouping, counts, URL state | Browser / Client | - | Whole fleet is returned by one endpoint; filtering is client-side by decision (no pagination) |
| "Mine" (owner or cost owner) | Browser / Client | API / Backend | Client compares ids; API supplies `createdById` and the new `costOwnerUserId` (UX filter only, not an authorization boundary) |
| Action availability and reasons | API / Backend | Browser / Client | API projects `enabled`/`pending`/`unavailableReason`; client only renders and re-validates nothing |
| Start/stop confirmation and execution | Browser / Client (shared provider) | API / Backend | `CostConfirmationProvider` owns the dialog and mutation; API re-validates permission, pending state and challenge |
| Fleet-changed signal production | API / Backend | Database / Storage | EF interceptor observes committed lifecycle state; no provider or game logic involved |
| Fleet-changed transport | API / Backend (existing update stream) | Browser / Client | Process-local `UpdateStreamService`; browser `WebSocket` client with reconnect |
| Cache invalidation and refetch | Browser / Client | API / Backend | TanStack Query `invalidateQueries`; data always comes from the authorized REST endpoint |
| Subscription authorization | API / Backend | - | `UpdateStreamController.CanSubscribeAsync` gate for the fleet path |
| Route/nav label | Browser / Client | - | `commandNavigation.tsx` + locale value; route manifest unchanged |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | Already the app runtime [VERIFIED: sources/ReactApp/package.json] |
| react-router | 7.18.2 | `useSearchParams` with `{ replace: true }` for `?q=`/`?filter=` | Already used by `CostConfirmationProvider` for the same API [VERIFIED: package.json, CostConfirmationProvider.tsx] |
| @tanstack/react-query | 5.101.4 | Fleet cache, `invalidateQueries`, `dataUpdatedAt` | Already owns `hostingQueryKeys.fleet` [VERIFIED: package.json, services.ts] |
| i18next / react-i18next | 26.3.6 / 17.0.11 | EN/DE strings, `_one`/`_other` plurals | Existing flat-key locale files and parity gate [VERIFIED: package.json, scripts/check-locales.mjs] |
| @fluentui/react-icons | 2.0.320 | `Checkmark16Regular`, `PlugDisconnected20Regular`, `Dismiss20Regular`, `Warning20Regular` | Already the icon library; all four glyphs exist in the package [VERIFIED: node_modules/@fluentui/react-icons/lib/sizedIcons chunk-24/chunk-10/chunk-28/chunk-10] |
| Browser `WebSocket` | built in | Update-stream client | UI-SPEC: no new runtime dependency |
| ASP.NET Core WebSockets + EF Core `SaveChangesInterceptor` | .NET 10 / EF Core 10.0.9 | Signal transport and signal source | Already in the API; interceptors are the documented seam for save hooks [CITED: learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| vitest + jsdom + @testing-library/react | 4.1.10 (installed) / 28.0.0 / 16.3.2 | Unit and component tests | Every new React unit [VERIFIED: ran `npx vitest run` this session, 104 tests passed] |
| @playwright/test + @axe-core/playwright | 1.62.1 / 4.13.0 | 320/390 layout metrics, axe, `page.routeWebSocket` | `tests/e2e/hosting-fleet.spec.ts` [VERIFIED: `routeWebSocket` present in node_modules/playwright-core/types/types.d.ts] |
| xunit | 2.9.3 | API tests for notifier, interceptor, subscription gate, DTO projection | Backend tracks [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| EF interceptor as signal source | Explicit `notifier.Notify(serverId)` calls at every lifecycle write site | Explicit calls touch ~10 constructors that hand-written tests instantiate, and are easy to forget at the next transition; interceptor is one seam. Keep one explicit call at the `ExecuteUpdateAsync` site |
| Per-socket send lock inside `UpdateStreamService` | Serialized publisher in front of it | Editing the shared service widens core infrastructure (AGENTS.md); the publisher confines the change to the fleet feature |
| Hand-rolled WebSocket hook | `react-use-websocket` or similar | UI-SPEC forbids new runtime dependencies; the needed behaviour (backoff, jitter, visibility, online) is small |

**Installation:** none. No npm or NuGet package is added by this phase.

**Version verification:** versions above were read from `sources/ReactApp/package.json`, `tests/Api.Tests/Api.Tests.csproj` and AGENTS.md this session; no registry lookup is needed because nothing is installed.

## Package Legitimacy Audit

No external packages are installed by this phase (React side uses existing dependencies and the browser `WebSocket`; API side uses in-box ASP.NET Core and EF Core). The audit and `package-legitimacy check` were therefore not run.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | - | - | - | - | - | - |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 Lifecycle writers (HTTP enqueue, executor, queue, reconcilers, worker protocol)
        |  SaveChangesAsync on contexts from AddDbContextFactory<DatabaseContext>
        v
 FleetChangeInterceptor (singleton; ISaveChangesInterceptor + IDbTransactionInterceptor)
   - SavingChanges: collect affected server ids (deployment Status/PossibleCost, job Status/Added,
     server Added/Title/IsArchived/cost owner/IsLaunchDisabled)
   - SavedChanges: no transaction -> publish; inside a transaction -> hold until TransactionCommitted
   - Failed / RolledBack: discard
        |  Notify(serverId)                         (+1 explicit call at the ExecuteUpdateAsync site)
        v
 IFleetChangeNotifier (singleton, Channel<Guid>, single reader loop, coalesce window ~250ms, never throws)
        |  SendUpdateAsync("game-servers/fleet", WebSocketMessage<GameServerFleetChangedMessage>)   sequential
        v
 UpdateStreamService (existing, process-local, prefix match on subscriber path)
        |  text frame {"$id":"1","type":"...GameServerFleetChangedMessage","value":{"$id":"2","serverId":"..."}}
        v                                                  ^ subscribed via
 Browser: useUpdateStream("game-servers/fleet") <-- GET /api/update-stream/subscribe?path=game-servers%2Ffleet
        |  controller gate: authenticated + server-section claim; exact 2-segment path
        |  coalesce (leading + <=1 trailing per 500ms)
        v
 queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true })   (+ notifications exact)
        v
 useFleetQuery refetch -> GET /api/game-servers (authorized, per-viewer) -> fleetModel -> FleetPage
        |                                                                       |
        |  status: connecting|open|reconnecting|unavailable                     +--> FleetLiveNotice (after 3s)
        +--> FleetLiveUpdatesProvider (layout level, only when canAccessGameServers)

 Card primary action --> useCostConfirmation().request({ serverId, actionId, source: "fleet-card",
                         onSubmitted }) --> CostConfirmDialog --> executeAction --> toast + invalidations
                         onSubmitted --> page marks card "submitted" until fleet dataUpdatedAt advances
```

### Recommended Project Structure

```
sources/ReactApp/src/
├── app/updateStream.ts                       # useUpdateStream (resource-agnostic) + pure helpers (backoff, url)
├── features/hosting/
│   ├── fleetModel.ts                         # pure: parse params, search, partition, primaryActionFor, counts, diff
│   ├── useFleetLiveUpdates.tsx               # FleetLiveUpdatesProvider + useFleetLiveUpdates (status, reconnectNow)
│   ├── useFleetQuery.ts                      # refetchInterval removed
│   └── FleetPages.tsx                        # ServerFleetPage composition; HostingOverviewView unchanged
├── components/
│   ├── FilterChipGroup.tsx                   # generic
│   ├── SearchField.tsx                       # generic
│   └── server/
│       ├── FleetToolbar.tsx, FleetSection.tsx, FleetServerCard.tsx, FleetLiveNotice.tsx
│       └── fleet.css                         # tokens only; picked up by the existing style-guard glob
sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs
sources/Api/Services/GameServerLifecycle/  (or Services/FleetUpdates/)
│   ├── IFleetChangeNotifier.cs, FleetChangeNotifier.cs, FleetChangeInterceptor.cs
sources/Api/Controllers/UpdateStreamController.cs   # fleet path gate
sources/Api/DTO/GameServerDto.cs + GameServerProjectionService.cs   # CostOwnerUserId
sources/ReactApp/openapi/utf-api.json + src/generated/api.ts          # regenerated
```

### Pattern 1: Pure model first (no React in `fleetModel`)

**What:** All decisions the UI-SPEC assigns to `fleetModel` are pure functions over `GameServer[]`: `parseFleetSearchParams(URLSearchParams)`, `searchServers`, `partitionFleet`, `primaryActionFor`, `countFilters`, plus `diffFleetStates` for announcements.
**When to use:** Always; it lets the Nyquist sampling run in milliseconds and keeps component tests thin.
**Example:**
```typescript
// Source: derived from serverState.ts (verified) and 62-UI-SPEC.md
import { deriveServerState } from "@/features/hosting/serverState";

export type FleetFilter = "all" | "mine" | "running" | "off";
const filters: ReadonlySet<string> = new Set(["mine", "running", "off"]);

export function parseFleetSearchParams(params: URLSearchParams): { q: string; filter: FleetFilter } {
  const raw = params.get("filter") ?? "";
  return {
    q: (params.get("q") ?? "").trim().slice(0, 200),
    filter: filters.has(raw) ? (raw as FleetFilter) : "all", // invalid values fall back to All
  };
}

export function matchesFilter(server: GameServer, filter: FleetFilter, meId: string | undefined): boolean {
  const derived = deriveServerState(server);
  switch (filter) {
    case "all": return true;
    case "mine": return Boolean(meId) && (server.createdById === meId || server.costOwnerUserId === meId);
    case "running": return derived.cost !== "none";   // live or maybe
    case "off": return derived.state === "off";       // implies cost === "none" (FND-03)
  }
}
```

### Pattern 2: Search fold exactly as specified

```typescript
// Source: 62-UI-SPEC.md "Matching" row
const fold = (value: string, locale: string) =>
  value.normalize("NFD").replace(/\p{M}+/gu, "").toLocaleLowerCase(locale);

export function searchServers(servers: GameServer[], query: string, locale: string): GameServer[] {
  const terms = fold(query.trim(), locale).split(/\s+/).filter(Boolean);
  if (terms.length === 0) return servers;
  return servers.filter((server) => {
    // Exclude the API placeholders so "assigned" never matches every hidden-owner server.
    const owner = costOwnerText(server.costOwnerName);
    const haystack = fold([server.title, server.gameIdentifier, owner].join("\n"), locale);
    return terms.every((term) => haystack.includes(term));
  });
}
```
`currentLocale()` from `@/app/format` supplies the locale. The cost-owner placeholders (`"Assigned owner"`, `"Unknown owner"`) must be excluded from the haystack; see Pitfall 11.

### Pattern 3: URL state with no effect-driven sync (lint-safe)

`eslint-plugin-react-hooks` 7 `flat.recommended` is active (`--max-warnings 0`), which includes the compiler rules (no `setState` in an effect, no ref reads during render). Keep the typed text as `{ value, base }` and derive what is displayed:

```typescript
const [draft, setDraft] = useState<{ value: string; base: string } | null>(null);
const urlQ = parseFleetSearchParams(searchParams).q;
const shown = draft !== null && draft.base === urlQ ? draft.value : urlQ;   // external URL change wins
// onChange: setDraft({ value, base: urlQ }); debounce 250ms then setSearchParams(next, { replace: true })
```
An external navigation (sidebar "Servers" link while `?q=x` is set) changes `urlQ`, so `base` no longer matches and the field resets without an effect.

### Pattern 4: Serialized, coalescing signal publisher (backend)

```csharp
// Source: pattern per WebSocket.SendAsync remarks (CITED) + UpdateStreamService.SendUpdateAsync (VERIFIED)
public interface IFleetChangeNotifier { void Notify(Guid gameServerId); }

internal sealed class FleetChangeNotifier : IFleetChangeNotifier, IHostedService
{
    // Channel<Guid>, one reader loop: drain for ~250 ms, de-duplicate ids, then for each id
    // await updateStreamService.SendUpdateAsync(
    //     $"{Constants.Routes.GameServers}/fleet",
    //     new GameServerFleetChangedMessage { ServerId = id });
    // catch everything; log with a structured template; never block or throw into the caller.
}
```
All sends to the fleet path then happen one at a time from one loop, so two lifecycle writers can never issue concurrent `SendAsync` calls to the same socket.

### Pattern 5: Hook shape for the reusable client

`useUpdateStream(path, onMessage)` returns `{ status, reconnectNow }`. Create the socket inside the effect; keep a per-effect-run `disposed` flag so React StrictMode's mount/unmount/mount (the app renders under `<StrictMode>` in `main.tsx`) never leaves a live reconnect timer; keep `onMessage` in a ref updated in an effect (the `CostConfirmationProvider` pattern) or use `useEffectEvent` (present in the installed `@types/react`, unused elsewhere in the repo). Pure helpers to export and unit-test with fake timers: `nextDelayMs(attempt, random)` (1,2,4,8,16 then 30 s cap, +/-20% jitter), `toSocketUrl(apiBase: URL, path)` (swap `http:`->`ws:`, `https:`->`wss:`), `createCoalescer(fn, windowMs)`.

### Anti-Patterns to Avoid

- **Invalidating with the bare fleet key:** `hostingQueryKeys.fleet` is `["hosting","servers"]` and the per-server, jobs, operations, configuration, files, preservation and secrets keys all start with the same prefix [VERIFIED: services.ts:110-129]. A non-exact `invalidateQueries({ queryKey: hostingQueryKeys.fleet })` on every signal refetches every active server-scoped query of an open workspace. Use `exact: true` for the signal path (the confirmation provider's existing broad invalidation after a confirmed action is intentional and stays).
- **Second source of state truth:** no component compares `deployment.status`; membership, section and cost line all come from `deriveServerState`.
- **Optimistic status flip:** forbidden by D-12; the only local state is the `submitted` marker.
- **Fleet polling anywhere in the new code:** no `setInterval` fallback (D-15).
- **Subscribing from the wrong layer:** the provider mounts inside the layouts, not in `App`, because it needs router + auth + query client and must be skipped for anonymous users.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Server state, cost, section membership | A second status switch in the card | `deriveServerState` | FND-03 invariant ("off" implies no possible cost) lives there; property-tested in Phase 61 |
| Disabled-with-reason | Custom disabled button + title | `ActionButton unavailableReason` + `ActionUnavailableReason`/`UnavailableReason` | Keeps focusability, `aria-disabled`, `aria-describedby`, and the API-reason-to-localized mapping |
| Cost confirmation | A fleet-local dialog or execute call | `useCostConfirmation().request` | Single dialog, challenge, stale-action handling, toast, invalidation, focus return |
| Loading/empty/error/no-match panels | New panel markup | `StatePanel` | Existing roles and tokens |
| Cost-owner placeholder handling | String comparisons in the card | Export the existing `ownerFor` from `costConfirmationModel.ts` (currently module-private) | One definition of `"Assigned owner"`/`"Unknown owner"` |
| Save-hook on lifecycle writes | A `Notify` call in every service | One EF interceptor registered on `AddDbContextFactory` | Interceptors are the supported seam and do not change constructors |
| WebSocket send serialization | A new per-socket lock in `UpdateStreamService` | Serialized notifier loop in front of it | Keeps core infrastructure untouched |
| Plural/number formatting | Manual `s` suffix, `toString()` | i18next `_one`/`_other` + `Intl.NumberFormat(currentLocale())` | Matches `Server_Fleet_PendingJobs_*` precedent |
| Style hygiene | New literals in CSS | `--utf-*` tokens; the style guard globs every `.css` in `components/server/` | `readdirSync(serverDirectory).filter(name => name.endsWith(".css"))` already covers `fleet.css` [VERIFIED: tests/styles/server-style-guard.test.ts] |

**Key insight:** every hard part of this phase (state model, confirmation, reasons, tokens) is already solved. New code should be thin composition plus the two genuinely new things: the socket client and the backend signal.

## Common Pitfalls

### Pitfall 1: Concurrent sends silently unsubscribe the viewer (breaks D-15)
**What goes wrong:** Two lifecycle writers signal at nearly the same time; the second `SendAsync` on a socket throws; `UpdateStreamService` catches it and removes the socket from its registry; the browser socket stays open but never receives anything again. With no polling fallback the fleet becomes silently stale.
**Why it happens:** `WebSocket.SendAsync` supports exactly one outstanding send per socket [CITED: learn.microsoft.com/dotnet/api/system.net.websockets.websocket.sendasync - "Issuing multiple sends at the same time is *not supported* and will result in an *undefined behavior*. You should serialize send operations"]. The service's loop is `await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)` inside `try { ... } catch { webSocketsToRemove.Add(webSocket); }` [VERIFIED: sources/Api/Services/UpdateStreamService/UpdateStreamService.cs, SendUpdateAsync].
**How to avoid:** Pattern 4 (single reader loop, coalesced). Also make the client resilient: because a server-side silent drop is invisible to the browser, the periodic keep-alive from Pitfall 6 plus `reconnectNow` and the reconnect refetch bound the damage.
**Warning signs:** A test that fires two `Notify` calls concurrently against a fake `IUpdateStreamService` records overlapping `SendUpdateAsync` calls.

### Pitfall 2: Signal published before the transaction commits
**What goes wrong:** `SavedChanges` fires inside an explicit transaction (the executor uses `resultTransaction`/`transaction.CommitAsync`); the browser refetches before commit and reads the old state, then waits for a signal that already happened.
**Why it happens:** `SaveChangesAsync` completes before `CommitAsync` when an ambient transaction exists [VERIFIED: LifecycleJobExecutor.cs `await resultTransaction.CommitAsync(...)` after `SaveChangesAsync`; LifecycleJobQueue.cs `await transaction.CommitAsync(...)` after `SaveChangesAsync`].
**How to avoid:** One class implementing both `ISaveChangesInterceptor` and `IDbTransactionInterceptor`: at `SavedChanges`, if `eventData.Context.Database.CurrentTransaction` is not null, hold the ids in a per-context map (`ConditionalWeakTable<DbContext, HashSet<Guid>>`, because the interceptor is a singleton and must be thread-safe) and release them in `TransactionCommitted`; clear in `TransactionRolledBack` and `SaveChangesFailed`. Interceptor singleton rule [CITED: learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors - "Because these interceptors are singletons, they must be thread-safe... use the Context ... property on the event data"]. The notifier's ~250 ms coalesce window is a second line of defense, not the first.
**Warning signs:** An interceptor test that commits a job inside `BeginTransactionAsync` records the signal before `CommitAsync` returns.

### Pitfall 3: `ExecuteUpdateAsync` bypasses the interceptor
**What goes wrong:** Cost-summary updates that use bulk SQL emit no signal.
**Why it happens:** Two bulk sites exist: `LifecycleJobExecutor.cs:444` (`SetProperty(q => q.PossibleCost, true)`, deployment) and `LifecycleJobQueue.cs:235` (lease renewal, irrelevant to the fleet) [VERIFIED by grep for `ExecuteUpdateAsync`].
**How to avoid:** Add one explicit `fleetChangeNotifier.Notify(deployment.GameServerFk)` after the possible-cost `ExecuteUpdateAsync` (the executor already reloads the deployment right after it). Because `LifecycleJobExecutor` is constructed by hand in tests (`new LifecycleJobExecutor` appears in the API tests), make the dependency optional or add a default no-op in the test construction helpers and say so in the plan.

### Pitfall 4: Path string mismatch drops messages silently
**What goes wrong:** The client subscribes with `/game-servers/fleet` or different casing; authorization passes (segments are split with `RemoveEmptyEntries`) but `SendUpdateAsync` never matches.
**Why it happens:** Delivery is `_webSockets.Where(q => q.path.StartsWith(path))` on the raw subscriber string, and `CanSubscribeAsync` splits on `/` with `RemoveEmptyEntries | TrimEntries` [VERIFIED: UpdateStreamService.cs, UpdateStreamController.cs].
**How to avoid:** Define the canonical constant once per side (`game-servers/fleet`; the API half is `$"{Constants.Routes.GameServers}/fleet"`, where `Routes.GameServers = "game-servers"` [VERIFIED: Constants.cs:36]). In the controller, for the fleet path require exactly two segments, equal case-insensitively, and register the canonical string (not the raw query value) with `RegisterAsync`. Accepting `game-servers/fleet/anything` would let a subscriber attach under a prefix the sender uses.

### Pitfall 5: Message envelope carries `$id` and a full type name
**What goes wrong:** A strict client parser rejects the frame.
**Why it happens:** The service serializes with `ReferenceHandler = ReferenceHandler.Preserve` and the envelope `Type` defaults to `typeof(T).FullName` [VERIFIED: UpdateStreamService.cs `_serializerOptions`, WebSocketMessage.cs]. Frames look like `{"$id":"1","type":"X39.UnitedTacticalForces.WebSockets.Contracts.<Folder>.GameServerFleetChangedMessage","value":{"$id":"2","serverId":"<guid>"}}` (property casing from `JsonSerializerDefaults.Web`).
**How to avoid:** The client reads `type` and `value.serverId` defensively, ignores unknown keys, and treats any non-JSON or unknown-type frame as "ignore, no error". The OpenAPI contract does not describe WebSocket payloads, so the TypeScript wire type is hand-written and pinned by a unit test against a captured frame string.

### Pitfall 6: Idle WebSocket closed by the reverse proxy
**What goes wrong:** Every ~60 s the browser reconnects and refetches (churn) or stalls.
**Why it happens:** `deploy/nginx.conf` proxies `/api/` with `Upgrade`/`Connection` headers but sets no `proxy_read_timeout` [VERIFIED: read deploy/nginx.conf lines 76-86]; nginx defaults to 60 s [ASSUMED]; Kestrel's server ping interval defaults to 2 minutes [ASSUMED].
**How to avoid:** `app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(25) })` (one line in `Program.cs`, currently `app.UseWebSockets();` at line 100). Confirm with the user (Assumptions A2) because it also changes the existing event streams.

### Pitfall 7: Subscription gate must be explicit
**What goes wrong:** `CanSubscribeAsync` returns `false` for every non-event path, so the fleet path is rejected with 403 until added; conversely a loose gate lets any logged-in user watch fleet activity.
**Why it happens:** The method only handles `events/{guid}`; it returns `false` afterwards [VERIFIED: UpdateStreamController.cs]. The REST list endpoint is only `[Authorize]` and filters per server in the projection.
**How to avoid:** Allow the fleet path only for a principal holding a `sv` or `sv:view` claim of any value, or `crt:sv`, `adm:sv`, `adm:hosting`, `adm` (mirrors `canAccessGameServers`/`canCreateGameServers` in `claims.ts`). Test both allowed and denied principals. Residual risk (ids and timing of servers the viewer cannot see) is in the Security section.

### Pitfall 8: Layout tests will open real sockets and crash on `getApiClient()`
**What goes wrong:** Adding `FleetLiveUpdatesProvider` to both layouts makes every layout test that renders a server-access user try to open a WebSocket and call `getApiClient()` before the client is initialized (it throws `"API client was used before application bootstrap completed."`).
**Why it happens:** `tests/hosting/cost-confirm-layouts.test.tsx` renders both layouts with a member and never calls `initializeApiClient` [VERIFIED: file read; `initializeApiClient` appears in other tests only]; jsdom ships a `WebSocket`.
**How to avoid:** (a) Build the socket URL from the layouts' `configuration` prop with `resolveApiBaseUrl(configuration)` instead of the singleton; (b) install a deterministic `FakeWebSocket` in `tests/setup.ts` (`vi.stubGlobal`), exposing open/close/message triggers for the hook tests; (c) gate the provider on `canAccessGameServers(auth.user) && !auth.isLoading`.

### Pitfall 9: Icon mock is an explicit export list
**What goes wrong:** `Checkmark16Regular` and `PlugDisconnected20Regular` are undefined under Vitest, so any component using them throws "Element type is invalid".
**Why it happens:** `vitest.config.ts` aliases `@fluentui/react-icons` to `tests/mocks/react-icons.tsx`, which exports named constants one by one; `Dismiss20Regular`, `Warning20Regular`, `Warning16Regular`, `Info16Regular` exist, the two named above do not [VERIFIED: grep of the mock].
**How to avoid:** Add both (and any other new glyph) to the mock in the same plan that introduces the component.

### Pitfall 10: Legacy `.button` is `nowrap` and `fit-content`
**What goes wrong:** "Herunterfahren und Abrechnung beenden" overflows a 320 px card.
**Why it happens:** `.button { width: fit-content; max-width: 100%; white-space: nowrap; ... min-height: var(--control-height) }` [VERIFIED: styles.css:1963-1982].
**How to avoid:** `fleet.css` must scope an override to the card (`white-space: normal; width: 100%`), use `--utf-control-height-touch` inside `@media (max-width: 800px)` (only 520px and 800px breakpoints are allowed by the guard), and make `ActionButton` receive `fullWidth` so the reason wrapper becomes `action-with-reason--full-width`.

### Pitfall 11: Cost owner placeholders are English literals
**What goes wrong:** The card shows the English strings `Assigned owner` / `Unknown owner` in German, and search matches "assigned" on every hidden-owner server.
**Why it happens:** The API sets `CostOwnerName = canSeeOwner ? server.CurrentCostOwner?.DisplayName ?? "Unknown owner" : "Assigned owner"` [VERIFIED: GameServerProjectionService.cs ProjectServerAsync]; the client constants live in `costConfirmationModel.ts` and the helper that maps them (`ownerFor`) is not exported.
**How to avoid:** Export `ownerFor`, map `hidden` to `Server_Confirm_OwnerHidden` and `unknown` to `Server_Confirm_OwnerUnknown`, and index only real names in the search haystack. Note that a user-kind cost owner who is the viewer always sees their own real name (`CanViewCostOwnerAsync` returns true for `q.UserFk == userId`), which is why `Mine` by cost owner is reliable.

### Pitfall 12: Action availability reality vs the primary-action table
**What goes wrong:** Attention and unconfirmed cards (exactly the ones that may still cost money) show a disabled "Shut down and stop billing".
**Why it happens:** `stop` is enabled only when `deployment?.Status is EDeploymentStatus.Running` and `start` only when `deployment is null && !server.IsLaunchDisabled && !server.IsArchived` [VERIFIED: GameServerProjectionService.cs ProjectAction, verbatim `isStop ? deployment?.Status is EDeploymentStatus.Running`]. The only action that works in those states is `terminate`, which D-11 removes from the card.
**How to avoid:** Implement the UI-SPEC table literally (disabled `stop` with the `Server_Reason_State` text, Open server beside it), and record in the plan and UAT that "disabled stop on attention/unconfirmed" is the expected, honest result of the API rules, not a bug. See Open Question 1 for the product call. Also note: `pending` is the same for every action of a server (`pending > 0` of the whole server), and a control-plane pause disables every action with the paused reason, which `UnavailableReason` already maps (`Server_Reason_Paused`).

### Pitfall 13: The card cannot observe a confirmed action
**What goes wrong:** D-12's "submitted" state has no trigger: the card only calls `request(...)`, and the provider runs the mutation and invalidations internally with no success hook [VERIFIED: CostConfirmationProvider.tsx `confirm` -> `execute.mutate(..., { onSuccess })`; `CostConfirmationRequest` has `serverId`, `actionId`, `source`, `returnFocus` only].
**How to avoid:** Additive, optional `onSubmitted?(): void` on `CostConfirmationRequest`, stored on the active request and invoked at the start of the provider's `onSuccess` (before the invalidations). Extend `tests/hosting/cost-confirmation-provider.test.tsx` for it. Page-level state: `submitted: Map<serverId, fleetDataUpdatedAtAtSubmit>`; a card is submitted while `fleet.dataUpdatedAt <= recorded && !fleet.isError`; this clears when the refetch lands (TanStack updates `dataUpdatedAt` on every successful fetch even when the data is structurally equal) and falls back to the original action if nothing changed, so there is no stuck state. No effect is needed for clearing.

### Pitfall 14: Focus loss when a card moves between Active and Off
**What goes wrong:** After the refetch the card's primary button unmounts (different `<ul>`), and the provider's `restoreFocus` ran earlier on the old node.
**Why it happens:** `restoreFocus` runs in `requestAnimationFrame` right after close, before the refetch lands; it falls back to `main h1` only when the invoker is already disconnected [VERIFIED: CostConfirmationProvider.tsx `restoreFocus`].
**How to avoid:** Track the last focused server id with a `focusin` handler on the list container (store `closest("[data-server-id]")`), and in a layout effect after each fleet data change, if `document.activeElement` is `body` or detached, focus `[data-server-id="<id>"]` Open link (`ActionLink` accepts arbitrary anchor props, so `data-server-id` passes through). Test with a fixture that flips a server from `Running` to `Stopped` between renders.

### Pitfall 15: Existing tests and e2e hard-code the old fleet
**What goes wrong:** Several assertions break by design.
**Why it happens / what to change:**
- `tests/hosting/fleet.test.tsx` asserts the terminate link on the card, `2 ausstehend`, the cost-warning paragraph, and renders `ServerFleetView({ servers, canCreate })`; these are replaced by new card/section tests.
- `tests/e2e/public-shell.spec.ts` line 369 expects a link named `Game servers`, line 1175 expects the heading `Spielserver`, and line ~1176+ expects `Diese Bereitstellung kann weiterhin Kosten verursachen.` [VERIFIED: read lines 355-375 and 1160-1180].
- `tests/hosting/member-access.test.tsx` reads `Fleet Server N` headings and counts `article` elements; heading level changes from h2 to h3 only affect role-name queries if they filter by level (they do not).
- The Phase 61 parity guard (`tests/hosting/parity-matrix.test.ts`) requires non-empty cells, a New location for `relocated` rows, and the word `sign-off` for `removed` rows; update statuses, never delete rows.

### Pitfall 16: The overview page keeps its own fleet poll
**What goes wrong:** Removing `refetchInterval` from `useFleetQuery` does not stop all fleet polling: `pages/CommandOverviewPage.tsx` declares its own `useQuery` on `hostingQueryKeys.fleet` with `refetchInterval: 10_000` (lines ~172-176), and `HostingOverviewPage` still polls notifications every 10 s [VERIFIED].
**How to avoid:** Leave both alone (D-16 says other pollers are not changed), but state it in the plan so verification of "no fleet polling" targets `useFleetQuery` and the fleet page, not the whole app. Optional follow-up for Phase 63.

### Pitfall 17: A signal-only design misses permission changes
**What goes wrong:** Granting or revoking a `sv`/`sv:view` claim changes who sees a server but touches no hosting entity, so no signal fires.
**How to avoid:** Accept and document; the reconnect refetch and manual Refresh cover it. Do not widen the interceptor to the claims tables.

### Pitfall 18: Stage line for `working`
**What goes wrong:** A `working` server can have a settled deployment (`Stopped`/`Terminated`) plus a pending job; the spec text shows the deployment-status label, which would read "Stopped" under a "Working" badge.
**How to avoid:** Follow the spec for `starting`/`stopping` (use `derived.detailKey`). For `working` prefer `Server_Fleet_QueuedChange` whenever the deployment status is settled. Low risk; flag for the planner's judgment (Assumption A5).

## Code Examples

### Subscription URL (no singleton needed)

```typescript
// Source: resolveApiBaseUrl in sources/ReactApp/src/app/config.ts (VERIFIED) + browser URL API
export function toSocketUrl(apiBase: URL, path: string): string {
  const url = new URL("update-stream/subscribe", apiBase);
  url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
  url.searchParams.set("path", path);
  return url.toString();
}
// resolveApiBaseUrl(configuration) returns new URL("/api/", window.location.origin) in DEV and the
// configured Api:BaseUrl otherwise; the cookie (HttpOnly, SameSite=Lax) authenticates the handshake.
```

### Provider mount (both layouts, next to the confirmation provider)

```tsx
// Source: CommunityLayout.tsx / CommandCenterLayout.tsx structure (VERIFIED)
<CostConfirmationProvider>
  <FleetLiveUpdatesProvider configuration={configuration}>
    {/* existing shell */}
  </FleetLiveUpdatesProvider>
</CostConfirmationProvider>
// Inside the provider: enabled = !auth.isLoading && canAccessGameServers(auth.user)
```

### Signal handler (exact invalidations)

```typescript
// Source: hostingQueryKeys in services.ts (VERIFIED: fleet ["hosting","servers"], notifications ["hosting","notifications"])
const invalidate = createCoalescer(() => {
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true });
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications, exact: true });
}, 500);
```

### Backend registration (interceptor singleton on the factory)

```csharp
// Source: Program.cs AddDbContextFactory<DatabaseContext>(options => ...) (VERIFIED, lines 215-226);
// the (IServiceProvider, DbContextOptionsBuilder) overload resolves the singleton interceptor.
builder.Services.AddSingleton<FleetChangeInterceptor>();
builder.Services.AddDbContextFactory<DatabaseContext>(
    (serviceProvider, options) =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("database"),
            npgsqlOptions => npgsqlOptions.UseNodaTime());
        options.AddInterceptors(serviceProvider.GetRequiredService<FleetChangeInterceptor>());
        // existing Development-only options stay
    });
```

### New contract message (XML docs mandatory under AGENTS.md)

```csharp
namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>Signals that a game server visible in the fleet changed; carries no server data.</summary>
public sealed class GameServerFleetChangedMessage
{
    /// <summary>Gets the primary key of the changed game server.</summary>
    /// <value>The server identifier; clients must refetch through the authorized endpoint.</value>
    public required Guid ServerId { get; init; }
}
```

### `CostOwnerUserId` projection (privacy-preserving)

```csharp
// Source: ProjectServerAsync (VERIFIED): `var canSeeOwner = await authorizationService.CanViewCostOwnerAsync(...)`
CostOwnerUserId = canSeeOwner && server.CurrentCostOwner?.Kind == ECostOwnerKind.User
    ? server.CurrentCostOwner.UserFk
    : null,
```
`Mine` only ever compares against the viewer's own id, and a viewer's own user-kind cost owner always passes `CanViewCostOwnerAsync`, so nulling it for viewers who may not see the owner loses nothing and leaks no other user's id. Regenerate the snapshot with the CI command (`dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi "<abs path>"`, then copy over `sources/ReactApp/openapi/utf-api.json` and run `npm run contracts:generate`) [VERIFIED: .github/workflows/dotnet.yml:73-76 and package.json `contracts:generate`].

### Test-time WebSocket double (in `tests/setup.ts`)

```typescript
// Pattern: deterministic fake so layout tests never open a real socket
class FakeWebSocket extends EventTarget {
  static instances: FakeWebSocket[] = [];
  readyState = 0; // CONNECTING
  constructor(readonly url: string) { super(); FakeWebSocket.instances.push(this); }
  open() { this.readyState = 1; this.dispatchEvent(new Event("open")); }
  message(data: string) { this.dispatchEvent(new MessageEvent("message", { data })); }
  fail() { this.readyState = 3; this.dispatchEvent(new Event("close")); }
  close() { this.readyState = 3; }
}
vi.stubGlobal("WebSocket", FakeWebSocket);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Fleet polled every 10 s by `useFleetQuery` | Push signal + invalidate/refetch, reconnect refetch, manual Refresh | This phase (D-13..D-16) | Latency drops from <=10 s to near real time; correctness now depends on the socket staying subscribed (Pitfalls 1, 2, 6) |
| Card shows provider, queue, updated time, terminate, cost paragraph | One-scan card: state, cost chip, cost owner, one primary action, Open | This phase (D-04, D-11) | Parity rows relocated to Phase 63 |
| Summary `Metric` tiles | Counts on filter chips plus one fleet-wide cost line | This phase (D-06) | Cost line counts the whole fleet so a filter cannot hide it |
| `ServerFleetView({servers, canCreate})` props API | Page owns URL/filter/submitted state; sections/cards are presentational | This phase | Replace the old view tests rather than patching them |

**Deprecated/outdated:** `ServerCard` (delete once no caller remains), `.next-hosting-card-grid`/`.next-hosting-server-card` legacy CSS for the fleet (leave in `hosting.css` if other pages use them; grep before deleting), `Hosting_Servers_CostWarning` and `Server_Fleet_PendingJobs_*` keys become unused on the fleet (the repo has a precedent commit removing orphaned locale keys; do not leave orphans if the locale gate or a key-usage test flags them).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | nginx's default `proxy_read_timeout` is 60 s and Kestrel's WebSocket `KeepAliveInterval` default is 2 minutes | Pitfall 6 | If wrong the keepalive tweak is unnecessary but harmless; if right and omitted, reconnect+refetch churn every minute |
| A2 | Setting a 25 s `KeepAliveInterval` globally in `Program.cs` is acceptable even though it also affects existing event update streams | Pitfall 6 | A reviewer may consider it a server-wide change needing explicit approval (AGENTS.md); fallback is an nginx `location` timeout instead |
| A3 | Invalidating on the first socket open when `fleet.dataUpdatedAt < openedAt` (instead of "after the first" only) is preferable to close the load-to-subscribe gap | Architecture / hook | Deviates from the approved UI-SPEC wording "every transition to open after the first"; costs at most one extra GET per page load; the planner may keep the UI-SPEC wording |
| A4 | Publishing the signal from an EF interceptor (with transaction deferral) is an acceptable reading of D-14 "from the durable lifecycle executor and job transitions" | Backend track | A reviewer may want explicit call sites; fallback is explicit `Notify` calls at the same seams |
| A5 | For `working` with a settled deployment, showing `Server_Fleet_QueuedChange` is better than the deployment-status label | Pitfall 18 | Minor UI divergence from the spec text |
| A6 | A subscriber holding any `sv`/`sv:view` claim receiving ids and timing of servers it cannot view is acceptable residual risk (D-13 says no filtering needed) | Security Domain | If not acceptable, drop `serverId` from the message (the client already refetches everything) or add per-viewer filtering (deferred) |
| A7 | The installed Playwright Chromium revision matches `@playwright/test` 1.62.1 | Environment | e2e may need `npx playwright install chromium` |

## Open Questions

1. **Attention/unconfirmed cards offer only a disabled stop.**
   - What we know: the API enables `stop` only while `Running`; `terminate` is the only working cost-stop in those states, and D-11/UI-SPEC remove it from the card. Verified in `ProjectAction`.
   - What's unclear: whether the product wants a dead-end disabled button for servers that may still cost.
   - Recommendation: implement the UI-SPEC literally (disabled stop with state reason, Open server visible), document it as expected behaviour, and surface it for the user at plan review. A cheap alternative if the user agrees: promote Open server to `primary` and hide the disabled stop in those two states.

2. **First-open refetch (A3).**
   - Recommendation: invalidate on every open when the fleet data predates the open; otherwise follow the UI-SPEC wording. Decide at plan review.

3. **Where the notifier and interceptor live.**
   - Recommendation: `sources/Api/Services/GameServerLifecycle/` (they observe lifecycle state and publish a wire message); not in `UpdateStreamService` and not in `WebSocketContracts` (wire messages only). Write the placement justification in the plan per AGENTS.md.

4. **Multi-instance documentation (D-14).**
   - Recommendation: document the process-local limitation in the notifier's XML docs and a short paragraph in `docs/features/cloud-based-game-server-hosting/server-states-and-actions.md` or `control-plane-workers-and-jobs.md`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | React build/tests | yes | v25.5.0 (package `engines` says `>=24 <25`; npm will warn) | Use Node 24 if `npm ci` rejects |
| npm | scripts | yes (via node) | not probed | - |
| Vitest suite | unit tests | yes | 4.1.10, 3 hosting files / 104 tests passed in about 4 s | - |
| .NET SDK | API build and tests, OpenAPI export | yes | 10.0.201 | - |
| dotnet-ef | not needed (no migration) | yes | 10.0.5 | - |
| Playwright Chromium | e2e and axe | revision directories present (`chromium-1217` ... `chromium-1255`) | match to 1.62.1 not verified (A7) | `npx playwright install chromium` |
| PostgreSQL / Aspire | not needed for this phase's tests (API tests use in-memory SQLite) | not probed | - | - |

**Missing dependencies with no fallback:** none found.
**Missing dependencies with fallback:** Playwright browser revision confirmation.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 + jsdom 28.0.0 + @testing-library/react 16.3.2 (React); xunit 2.9.3 on net10.0 (API); Playwright 1.62.1 + @axe-core/playwright 4.13.0 (e2e) |
| Config file | `sources/ReactApp/vitest.config.ts` (aliases icons and Fluent components to `tests/mocks/*`, setup `tests/setup.ts`), `sources/ReactApp/playwright.config.ts` (builds assets, serves `http://127.0.0.1:4173/next/`), `tests/Api.Tests/Api.Tests.csproj` |
| Quick run command | `cd sources/ReactApp && npx vitest run tests/hosting/fleet-model.test.ts tests/hosting/fleet-page.test.tsx` |
| Full suite command | `cd sources/ReactApp && npm run ci` (locales:check, contracts:check, format:check, lint, typecheck, test, build:assets); `dotnet test tests/Api.Tests`; `npx playwright test tests/e2e/hosting-fleet.spec.ts` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FLT-01 | Card shows title, game, state badge, cost chip, cost owner (placeholder localized), one primary action and Open | component | `npx vitest run tests/hosting/fleet-card.test.tsx` | no, Wave 0 |
| FLT-01 | `primaryActionFor` table for every derived state incl. missing `start`/`stop`, pending, launch-disabled, paused | unit (table) | `npx vitest run tests/hosting/fleet-model.test.ts` | no, Wave 0 |
| FLT-02 | Search fold (case, diacritics, multi-term AND, blank), placeholders excluded from haystack | unit | `npx vitest run tests/hosting/fleet-model.test.ts` | no, Wave 0 |
| FLT-02 | Filters and counts over the search result; Mine by `createdById` and `costOwnerUserId`; invalid `?filter=` falls back | unit | same | no, Wave 0 |
| FLT-02 | URL state: typing debounced 250 ms, `replace` (no history growth), other params preserved, external nav resets the field, Escape/clear returns focus | component (fake timers + memory router) | `npx vitest run tests/hosting/fleet-page.test.tsx` | no, Wave 0 |
| FLT-03 | `partitionFleet`: only `off` in Off; possible-cost never in Off; Off section always rendered and expanded; ordering Active vs Off; no-match vs empty panels | unit + component | `fleet-model.test.ts`, `fleet-page.test.tsx` | no, Wave 0 |
| FLT-04 | Primary click calls `request({ source: "fleet-card" })` only; disabled primary shows reason and does nothing; submitted state set via `onSubmitted`, clears when `dataUpdatedAt` advances or on error; no optimistic flip | component | `fleet-page.test.tsx`, extend `cost-confirmation-provider.test.tsx` | partly (provider test exists) |
| FLT-04 | Focus fallback to the Open link when a card moves sections | component | `fleet-page.test.tsx` | no, Wave 0 |
| FLT-04 | `useUpdateStream`: url scheme swap, backoff schedule with jitter bounds, reconnect on close/online/visible, refetch on reopen, coalescing (leading + one trailing per 500 ms), unknown frames ignored, cleanup under StrictMode, status transitions, 3 s notice delay, `unavailable` after 5 failures | unit (FakeWebSocket + fake timers) | `npx vitest run tests/app/update-stream.test.tsx tests/hosting/fleet-live-updates.test.tsx` | no, Wave 0 |
| FLT-04 | `useFleetQuery` has no `refetchInterval`; signal invalidates fleet and notifications with `exact: true` and does not invalidate `hostingQueryKeys.server(id)` | unit | `fleet-live-updates.test.tsx` | no, Wave 0 |
| FLT-04 | Announcements: one per changed server up to three then a summary; none on first load; none for submitted servers | unit (`diffFleetStates`) + component | `fleet-model.test.ts`, `fleet-page.test.tsx` | no, Wave 0 |
| FLT-04 (API) | Interceptor emits for deployment status/`possibleCost`, job add and status change, server add/archive; none for progress-only changes; defers inside a transaction and discards on rollback and failed save | xunit (SQLite store with the interceptor) | `dotnet test tests/Api.Tests --filter FullyQualifiedName~FleetChange` | no, Wave 0 |
| FLT-04 (API) | Notifier serializes sends (no overlapping `SendUpdateAsync`), coalesces duplicates, swallows send failures | xunit with a fake `IUpdateStreamService` | same filter | no, Wave 0 |
| FLT-04 (API) | `UpdateStreamController` allows the fleet path only for server-section claims, exact two segments, registers the canonical path; events path unchanged | xunit | `--filter FullyQualifiedName~UpdateStreamFleet` | no, Wave 0 |
| FLT-02 (API) | `CostOwnerUserId` set only for user-kind cost owner visible to the viewer; null for Verein/external and hidden owners | xunit extending `GameServerProjectionServiceTests` | `--filter FullyQualifiedName~CostOwnerUserId` | partly (file exists) |
| FLT-05 | 320x568 and 390x844, EN and DE: `scrollWidth <= clientWidth`; every primary and Open control in viewport after scrolling; axe (wcag2a/aa/21a/21aa, rules evaluated > 0) on populated fleet, no-match panel and live notice | e2e | `npx playwright test tests/e2e/hosting-fleet.spec.ts` | no, Wave 0 |
| FLT-05 | `fleet.css` passes the token guard | unit | `npx vitest run tests/styles/server-style-guard.test.ts` | yes (glob covers new file) |
| ACC-02 | Rendered `CommunityLayout` shows the "Command Center" link and rendered `CommandCenterLayout` nav shows "Servers"/"Server" for a bare `sv` and a bare `sv:view` user; no "Servers" link in the community header; `Hosting_Servers_Title` matches h1 and document title | component | extend `tests/hosting/member-access.test.tsx` and `cost-confirm-layouts.test.tsx` | yes, extend |
| Gate | EN/DE parity, placeholders, plurals for every new key | build gate | `npm run locales:check`; extend `tests/locales/server-keys.test.ts` | yes |
| Gate | Parity matrix rows updated, new rows `FLT-search-filter`, `FLT-live-updates` | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | yes |
| Gate | Route manifest untouched | shell | `git diff --exit-code -- sources/Common/Ui/routes.json` | n/a |
| Gate | 150-server fixture filters and renders without timing out (backstop) | unit | `fleet-model.test.ts` | no, Wave 0 |

### Sampling Rate

- **Per task commit:** the touched test file only (for example `npx vitest run tests/hosting/fleet-model.test.ts`), plus `npx tsc -b` when types changed; for API tasks `dotnet test tests/Api.Tests --filter <new fixture>`.
- **Per wave merge:** `npx vitest run` (whole React suite), `npm run lint`, `npm run locales:check`; `dotnet test tests/Api.Tests`.
- **Phase gate:** `npm run ci`, full `dotnet test`, Playwright fleet spec and the updated `public-shell`/`hosting-a11y` specs, routes.json diff clean, `contracts:check` clean, before `/gsd-verify-work`.

### Wave 0 Gaps

- [ ] `tests/setup.ts`: `FakeWebSocket` stub (Pitfall 8) so layout tests stay offline.
- [ ] `tests/mocks/react-icons.tsx`: add `Checkmark16Regular`, `PlugDisconnected20Regular` (Pitfall 9).
- [ ] `tests/hosting/fleet-model.test.ts`, `fleet-card.test.tsx`, `fleet-page.test.tsx`, `fleet-live-updates.test.tsx`, `tests/app/update-stream.test.tsx`, `tests/e2e/hosting-fleet.spec.ts` (with `page.routeWebSocket('**/api/update-stream/subscribe**', ...)` so the e2e preview server never receives an unmocked socket).
- [ ] API test fixtures: `FleetChangeInterceptorTests`, `FleetChangeNotifierTests`, `UpdateStreamControllerFleetTests`, `CostOwnerUserId` cases.
- [ ] Update `tests/hosting/fleet.test.tsx`, the two `public-shell.spec.ts` assertions, and the e2e shared mock helpers for the new live path.
- [ ] No framework install needed.

## Security Domain

`security_enforcement` is enabled (absent in config means enabled; `.planning/config.json` sets `security_enforcement: true`, `security_asvs_level: 1`, `security_block_on: high`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Existing cookie scheme authenticates the WebSocket handshake; `[Authorize]` on `SubscribeAsync` stays; no token in the URL |
| V3 Session Management | yes | HttpOnly, SameSite=Lax cookie [VERIFIED: Program.cs AddCookie options]; the socket carries no credentials in the payload |
| V4 Access Control | yes | Fleet-path subscription gated by server-section claims; REST list remains the only data source and filters per server with `HasServerPermission(... View)`; `Mine` is a UX filter, never authorization |
| V5 Input Validation | yes | `?q=` trimmed and length-capped, `?filter=` whitelisted; subscription `path` canonicalized and exact-matched; frames parsed defensively |
| V6 Cryptography | no | Transport is `wss` behind existing TLS; nothing hand-rolled |
| V8 Data Protection | yes | No server data, owner, cost or secret crosses the socket; `CostOwnerUserId` is null unless the viewer may see the owner |
| V13 API / WebSocket | yes | Origin handling: empty `AllowedOrigins` means any origin is accepted [CITED: learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.websocketoptions.allowedorigins], mitigated here because SameSite=Lax cookies are not sent on cross-site handshakes and the stream carries only a signal |

### Known Threat Patterns for ASP.NET Core WebSocket + React

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-site WebSocket hijacking | Spoofing | SameSite=Lax session cookie plus signal-only payload; optionally set `AllowedOrigins` from `Cors:AllowedOrigins` (not required, would widen scope) |
| Subscriber sees activity of servers it cannot view (ids and timing) | Information disclosure | Accepted residual risk per D-13 (Assumption A6): GUIDs, authenticated viewers with a server claim only; drop `serverId` from the message if unacceptable |
| Path-prefix confusion in `SendUpdateAsync` (`StartsWith`) | Tampering / Information disclosure | Canonical path, exact two-segment gate, register the canonical string |
| Unbounded signal amplification / slow client stalling the lifecycle executor | Denial of service | Notifier is asynchronous, coalesced, never awaited by lifecycle code, swallows and logs failures |
| Signal storm from progress updates | Denial of service | Interceptor reacts to status/cost/identity fields only, not `Progress`/`Stage` churn; coalescing window |
| Reflected content in announcements/notice | XSS | Server titles rendered as React text children; i18next `escapeValue: false` is safe because output is text, not HTML; no `dangerouslySetInnerHTML` |
| `?filter=`/`?q=` injection | Tampering | Whitelist and length cap; values never reach a URL builder or the DOM as HTML |

## Sources

### Primary (HIGH confidence)
- In-repo files read this session: `sources/ReactApp/src/features/hosting/{FleetPages.tsx,components.tsx,useFleetQuery.ts,serverState.ts,actionVocabulary.ts,services.ts,authorization.ts,types.ts}`, `src/components/{ActionControl.tsx,primitives.tsx,CommunityLayout.tsx,CommandCenterLayout.tsx,commandNavigation.tsx,ProfileMenu.tsx}`, `src/components/server/*`, `src/app/{config.ts,format.ts,i18n.ts}`, `src/api/client.ts`, `src/auth/claims.ts`, `src/styles/tokens.css`, `src/styles.css`, `tests/*` named above, `vite.config.ts`, `eslint.config.js`, `package.json`.
- API files: `Controllers/{UpdateStreamController,GameServersController}.cs`, `Services/UpdateStreamService/*`, `Services/GameServerLifecycle/{GameServerProjectionService,HostingAuthorizationService,LifecycleJobExecutor,LifecycleJobQueue,GameServerLifecycleService}.cs`, `DTO/GameServerDto.cs`, `Database/Hosting/{CostOwner,LifecycleJob,GameServerDeployment}.cs`, `WebSocketContracts/*`, `Program.cs`, `Constants.cs`, `deploy/nginx.conf`, `.github/workflows/dotnet.yml`, `tests/Api.Tests/GameServerHosting/*`.
- learn.microsoft.com WebSocket.SendAsync (concurrent sends unsupported) - https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket.sendasync
- learn.microsoft.com EF Core interceptors (interface list, registration, singleton thread-safety) - https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors

### Secondary (MEDIUM confidence)
- WebSocketOptions.AllowedOrigins default behaviour - https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.websocketoptions.allowedorigins (found via web search; the statement "empty list allows every origin" is consistent between docs and `WebSocketMiddleware` source per the search result)

### Tertiary (LOW confidence)
- nginx `proxy_read_timeout` default and Kestrel keep-alive default (training knowledge, tagged `[ASSUMED]` above).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no additions; every dependency read from manifests.
- Architecture: HIGH for the React side (all collaborators read), MEDIUM for the backend signal placement (verified hazards, recommended design not yet prototyped).
- Pitfalls: HIGH - each cites a file and line region or a Microsoft doc; two infra defaults are tagged assumed.

**Research date:** 2026-11-18
**Valid until:** 2026-12-18 (stable stack; revisit if Phase 63 changes the shared hook contract first)

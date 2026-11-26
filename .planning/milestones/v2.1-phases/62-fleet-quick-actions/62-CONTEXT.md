# Phase 62: Fleet & Quick Actions - Context

**Gathered:** 2026-11-18
**Status:** Ready for planning

<domain>
## Phase Boundary

A member answers "what exists, what is on, who pays, what next" in one scan and starts or stops a server straight from
the fleet list at `/command/servers`. Requirements: FLT-01, FLT-02, FLT-03, FLT-04, FLT-05, ACC-02.

The fleet consumes the Phase 61 foundations (`deriveServerState`, `ServerStateBadge`, `CostIndicator`,
`useCostConfirmation`, `UnavailableReason`, `--utf-*` tokens, React-owned EN/DE JSON). It does not build the workspace
shell, header, or section navigation (Phase 63) or the create flow (Phase 64). The route manifest
(`sources/Common/Ui/routes.json`) stays unchanged; no new pathnames.

Unlike earlier React-only phases, this phase includes one small, explicitly requested backend addition: a fleet-change
push signal over the existing update stream (see D-10..D-13).

</domain>

<decisions>
## Implementation Decisions

### Member entry point (ACC-02)
- **D-01:** The Command Center stays the home of the server section. Command Center and the public-facing site are
  deliberately separate concepts: most members never see the Command Center, and those who do get a distinctive,
  less vibrant, more task-focused interface. Do NOT add a "Servers" link to the public/community header and do not
  blur the two surfaces.
- **D-02:** Members holding only `sv` or `sv:view` already pass `canAccessCommandCenter` and `canAccessGameServers`
  (`sources/ReactApp/src/auth/claims.ts`). Keep entering through the current path (community header "Command Center"
  link, then the `servers` nav item). Add a test proving a bare `sv` and a bare `sv:view` user see the Command Center
  entry and the servers item.
- **D-03:** Rename the nav item label to **Servers** (DE: **Server**) by changing the value behind
  `Hosting_Nav_Servers` (currently "Game servers"). ACC-02 is met by this label plus a fleet page whose title and
  copy carry no admin-oriented wording. The Command Center shell framing itself is not changed.

### Card content and small screens (FLT-01, FLT-05) — Claude's discretion, recommendations adopted
- **D-04:** Each card shows: server title, game identifier, `ServerStateBadge`, `CostIndicator`, cost owner, one
  primary action, and an Open link. Runtime provider, queue count, and "updated" time leave the card (relocated to the
  workspace in Phase 63; mark the matching parity-matrix rows `relocated`, never deleted). The stage shows only while
  the server is starting, stopping, or working.
- **D-05:** One card layout at every width: a grid on wide screens, a single column at narrow widths. No separate
  compact-row layout. Actions wrap to full width; facts stack. Must be usable with every action reachable and no
  clipped content at 320px (and 390px).
- **D-06:** The three-tile summary strip is replaced by counts on the filter controls (All, Mine, Running, Off). When
  any server may still cost, one warning line stays above the list.

### Search, filter, grouping (FLT-02, FLT-03)
- **D-07:** Search is case-insensitive and locale-aware over title, game identifier, and cost owner name.
- **D-08:** Filters are **All, Mine, Running, Off**. "Mine" means the signed-in user is the server owner or the cost
  owner, using fields already present in the fleet DTO; the researcher confirms which fields exist and whether an
  API projection is needed (add it where the data is owned).
- **D-09:** Search text and filter live in URL search params on the same route (`?q=` and `?filter=`), written with
  history replace so typing does not create history entries. Reload and shared links restore the view.
- **D-09a:** Results are grouped into two sections: **Active** (every state except `off`, including starting,
  stopping, attention, unconfirmed, and any server that may still cost) first, then **Off** below it with its count.
  The Off section is expanded by default and is never collapsed away or hidden. Grouping uses `deriveServerState`
  only, so a server that may still cost can never land in Off (FND-03). Empty search/filter results show an explicit
  "no matches" panel with a clear-filters action, distinct from the "no servers shared with you" empty state.

### Quick start and stop (FLT-04)
- **D-10:** Primary action per state: **Start server** (gold) when off; **Shut down and stop billing** (danger) when
  running. States starting, stopping, working, or with a pending job show the action disabled with a visible reason.
  For attention and unconfirmed states, show the safest available cost-stopping action if the API offers it, otherwise
  Open. Every start and cost-stop opens `CostConfirmDialog` through `useCostConfirmation().request({ serverId,
  actionId, source: "fleet-card" })`. Actions the member cannot perform are shown disabled with the reason
  (`UnavailableReason`).
- **D-11:** **Terminate is removed from the fleet card.** It stays reachable from the workspace and through the
  `?action=terminate` deep link. Mark the fleet-card-terminate parity row `relocated` with evidence.
- **D-12:** After a confirmed action: toast, immediate invalidation and refetch of the fleet and notification queries,
  and the card shows its working state with the action disabled until the refreshed state arrives. No optimistic
  status flip.

### Live updates over WebSocket (FLT-04 "updates without a page reload")
- **D-13:** Replace fleet polling with a **push signal**. The API sends a lightweight "server changed" message (server
  id only, no server data) on the existing path-scoped update stream; the React app invalidates the fleet query and
  refetches through the normal authorized endpoint. Because no server data crosses the socket, no per-viewer
  authorization filtering is needed on the broadcast. Remove the 10s `refetchInterval` from `useFleetQuery`.
  Amended 2026-11-19: at UAT test 2 (gap G-62-2) the product owner rejected the premise that no per-viewer
  authorization filtering is needed, because a server id and its change timing are themselves information about a
  server the viewer cannot see. The signal-and-refetch design and the removal of the 10 second poll stand; D-17
  governs delivery.
- **D-14:** Signals originate from the durable lifecycle executor and job transitions (deployment status,
  `possibleCost`, pending job count changes). The stream is process-local like today's `UpdateStreamService`; document
  that a multi-instance API would need a shared backplane. Contract type lives in `sources/WebSocketContracts`
  (wire messages only); no game-process or provider logic enters the API.
- **D-15:** **No polling fallback** (user decision). The fleet relies on the socket plus the existing manual Refresh
  button. Claude's discretion: automatic socket reconnect with a refetch on (re)connect, and whether to show a
  visible "live updates disconnected" indicator, so a dropped socket never leaves a silently stale fleet. These do not
  reintroduce interval polling.
- **D-16:** The React app has no WebSocket client today. Add one reusable client/hook (not fleet-specific) under a
  shared location so Phase 63 can reuse it for the workspace. Other existing pollers (notifications, workspace) are not
  changed in this phase.
- **D-17:** **Claim-scoped delivery** (product owner decision at UAT test 2, gap G-62-2). A user receives a fleet
  change signal for a server only if the REST view rule for that server is true for them at publish time (`sv:view`
  rule: an empty-valued `adm`, `adm:sv` or `adm:hosting` claim, or `sv` or `sv:view` bound to the server id, through
  user or role claims, for a user who is not deleted or banned). Each socket is registered on a per-user path derived
  from the authenticated user id (`game-servers/fleet/{userId}`); the client still requests `game-servers/fleet`.
  Delivery is addressed per recipient and fails closed when the audience cannot be read: nothing is sent for that
  batch and there is no broadcast fallback. The update stream service and the WebSocket contracts stay unchanged.
  Claim or role changes emit no signal, so the affected fleet stays as loaded until the next refetch (reconnect,
  Refresh, or a later signal). This is not the deferred idea of full-state payloads with per-viewer filtering: the
  frame stays signal-only and carries the server id alone.

### Claude's Discretion
- Exact card layout, spacing, and section header styling within the `--utf-*` tokens; ordering inside each section
  (existing order: may-cost first, pending work, then name, is the starting point).
- Mapping of the "attention/unconfirmed" primary action when the API offers several actions.
- WebSocket reconnect/backoff mechanics and the disconnected indicator wording and placement.
- Component split for the fleet toolbar, section, and card under `src/components/server/` and `src/features/hosting/`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 61 foundations (consumed here)
- `.planning/phases/61-foundations-localization-member-access/61-CONTEXT.md` - Locked decisions D-01..D-13 (state
  model, cost confirmation, disabled-with-reason, locale ownership, tokens, parity matrix).
- `.planning/phases/61-foundations-localization-member-access/61-UI-SPEC.md` - Authoritative design contract: state
  and cost model, cost confirmation contract, action vocabulary, copy table, tokens.
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - Rows to mark `kept`/`relocated`
  for fleet capabilities touched here.

### Requirements and roadmap
- `.planning/ROADMAP.md` - Phase 62 goal, success criteria, Cross-Phase Constraints, Phase 62 research notes.
- `.planning/REQUIREMENTS.md` - FLT-01..05, ACC-02 (also FND-06 and L10N-03 enforced as per-phase gates).
- `.planning/PROJECT.md` - v2.1 scope and backend-addition limits.

### Project rules and architecture
- `AGENTS.md` - Architecture boundaries, English-only identifiers, one top-level type per file, XML docs, commit format.
- `.planning/codebase/ARCHITECTURE.md`, `CONVENTIONS.md`, `STRUCTURE.md`, `TESTING.md`, `CONCERNS.md`
- `docs/features/react-ui-beta.md` - React app structure and `/next/` hosting.
- `docs/features/cloud-based-game-server-hosting/` - Control-plane boundaries.

### Code touchpoints
- `sources/ReactApp/src/features/hosting/FleetPages.tsx` - Fleet page, `ServerFleetView`, `orderFleetServers`.
- `sources/ReactApp/src/features/hosting/components.tsx` - `ServerCard`, `ServerActions`, `HostingAccessBoundary`.
- `sources/ReactApp/src/features/hosting/useFleetQuery.ts` - Shared fleet query (10s poll to remove).
- `sources/ReactApp/src/features/hosting/serverState.ts`, `actionVocabulary.ts` - State and action models.
- `sources/ReactApp/src/components/server/*` - `CostConfirmDialog`, `useCostConfirmation`, `ServerStateBadge`,
  `CostIndicator`, `UnavailableReason`.
- `sources/ReactApp/src/components/commandNavigation.tsx` - `servers` nav item, `Hosting_Nav_Servers` label.
- `sources/ReactApp/src/components/CommunityLayout.tsx`, `src/auth/claims.ts` - Command Center entry and claim checks.
- `sources/Api/Services/UpdateStreamService/IUpdateStreamService.cs` and `sources/WebSocketContracts/` - Existing
  update stream and wire contracts (currently event messages only).
- `sources/Common/Ui/routes.json` - Frozen route manifest (must stay unchanged).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `useFleetQuery`: one shared cache entry used by fleet, profile menu, and overview; keep the single query key.
- `useCostConfirmation().request(...)` and `CostConfirmDialog`: the only start/stop path (source `fleet-card`).
- `deriveServerState` + `ServerStateBadge` + `CostIndicator` + `ServerStateSummary`: all status/cost rendering.
- `ActionButton`/`ActionLink` with `unavailableReason`; `StatePanel` for loading, empty, and error states.
- `orderFleetServers`: existing ordering to build the sections from.

### Established Patterns
- Flat i18next keys (`Server_{Area}_{Item}`), EN and DE JSON with the build-failing parity check.
- Style guard: no raw hex or pixel spacing literals in `components/server/*`.
- Command nav items declared in `commandNavigation.tsx` with `isAuthorized` predicates.

### Integration Points
- Update stream: API-side path-scoped broadcast (`IUpdateStreamService.SendUpdateAsync`) from the lifecycle executor;
  new message type in `WebSocketContracts`; new React WebSocket hook invalidating `hostingQueryKeys.fleet`.
- Parity matrix completeness test must keep passing (rows are updated, never removed).
- Route certification/manifest tests must stay green (search params only, no new routes).

</code_context>

<specifics>
## Specific Ideas

- Command Center is intentionally a separate, more focused, less vibrant interface; the fleet should feel like a
  working tool, not a marketing surface.
- Nav and fleet vocabulary: "Servers" / "Server"; cost-stop phrase stays "Shut down and stop billing".
- "Off servers are grouped but remain visible": an Off section, expanded, with a count.

</specifics>

<deferred>
## Deferred Ideas

- Workspace-level live updates and replacing the remaining pollers (notifications, workspace queries): Phase 63 can
  reuse the new WebSocket client.
- Full-state payloads over the socket with per-viewer authorization filtering: only if a signal-and-refetch proves too
  slow.
- Shared backplane for multi-instance API fan-out of update messages: future infrastructure work.
- Terminate from the fleet (overflow menu): not planned; workspace only.

</deferred>

---

*Phase: 62-Fleet & Quick Actions*
*Context gathered: 2026-11-18*

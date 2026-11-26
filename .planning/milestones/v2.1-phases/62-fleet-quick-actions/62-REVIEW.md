---
phase: 62-fleet-quick-actions
reviewed: 2026-11-19T00:00:00Z
depth: standard
files_reviewed: 39
files_reviewed_list:
  - sources/Api/Controllers/UpdateStreamController.cs
  - sources/Api/Program.cs
  - sources/Api/DTO/GameServerDto.cs
  - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
  - sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs
  - sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs
  - sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs
  - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
  - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
  - sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs
  - sources/ReactApp/src/app/updateStream.ts
  - sources/ReactApp/src/components/CommandCenterLayout.tsx
  - sources/ReactApp/src/components/CommunityLayout.tsx
  - sources/ReactApp/src/components/FilterChipGroup.tsx
  - sources/ReactApp/src/components/SearchField.tsx
  - sources/ReactApp/src/components/primitives.tsx
  - sources/ReactApp/src/components/server/CostConfirmationProvider.tsx
  - sources/ReactApp/src/components/server/FleetLiveNotice.tsx
  - sources/ReactApp/src/components/server/FleetSection.tsx
  - sources/ReactApp/src/components/server/FleetServerCard.tsx
  - sources/ReactApp/src/components/server/FleetToolbar.tsx
  - sources/ReactApp/src/components/server/costConfirmationModel.ts
  - sources/ReactApp/src/components/server/useCostConfirmation.ts
  - sources/ReactApp/src/components/server/fleet-card.css
  - sources/ReactApp/src/components/server/fleet-page.css
  - sources/ReactApp/src/components/server/fleet-toolbar.css
  - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
  - sources/ReactApp/src/features/hosting/FleetPages.tsx
  - sources/ReactApp/src/features/hosting/components.tsx
  - sources/ReactApp/src/features/hosting/fleetModel.ts
  - sources/ReactApp/src/features/hosting/useFleetAnnouncements.ts
  - sources/ReactApp/src/features/hosting/useFleetFocusFallback.ts
  - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
  - sources/ReactApp/src/features/hosting/useFleetQuery.ts
  - sources/ReactApp/src/features/hosting/useFleetSubmitted.ts
  - sources/ReactApp/src/features/hosting/hosting.css
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/styles/tokens.css
findings:
  critical: 0
  warning: 6
  info: 11
  total: 17
  necessity: 1
status: issues_found
---

# Phase 62: Code Review Report

**Reviewed:** 2026-11-19T00:00:00Z
**Depth:** standard
**Files Reviewed:** 39
**Status:** issues_found

## Summary

I reviewed the phase 62 source changes (`git diff e928adc0..HEAD`, limited to the paths in scope). On the backend that means the fleet-changed signal (EF interceptor, coalescing notifier, subscription gate, DTO field, and the possible-cost bulk-update hook). On the frontend it means the WebSocket hook, the live-updates provider, the fleet page, cards, toolbar, live notice, the announcement, focus and submitted-state hooks, and the locales. I read the callers and callees needed to check behavior: `UpdateStreamService`, `ActionControl`, `CostConfirmationProvider.restoreFocus`, `HostingAuthorizationService`, `GameServerProjectionService.ProjectActions`, and the code that resolves deployments.

The interceptor's transaction handling holds up. Collection runs through `ChangeTracker.Entries`, which runs DetectChanges. An implicit save transaction publishes from `SavedChanges`. Explicit transactions defer to `TransactionCommitted`. Deferred IDs left by a dispose without commit are discarded on the next `TransactionStarted`. Every place that resolves a deployment also changes `Status`, so the active-deployment switch is always signaled. `CostOwnerUserId` is correctly gated by `canSeeOwner`.

Main concerns:
1. The fleet stream sends every server ID to anyone holding any server-section claim, which bypasses per-server visibility.
2. Keyboard focus is lost or moved wrongly in three places: after a quick action from a card, on every background refetch while Refresh has focus, and through the focus fallback.
3. The notifier has one loop with no per-send timeout, so a single stalled subscriber can block fleet signals for everyone.
4. The new C# private members are missing the XML docs that AGENTS.md requires.

No critical issues found.

## Narrative Findings (AI reviewer)

## Warnings

### WR-01: The fleet stream exposes the IDs and activity timing of servers the subscriber cannot see

**File:** `sources/Api/Controllers/UpdateStreamController.cs:101-130`, `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs:138-140`, `sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs:21`
**Issue:** `CanAccessServerSection()` accepts any claim of type `Claims.ResourceBased.Server.View`/`.All` with any value. That includes a user who can view exactly one server (the claim value is that server's ID). Every subscriber is registered on the single canonical path `game-servers/fleet`, and `SendAsync` broadcasts `ServerId` for every server whose lifecycle changes. So a user limited to one server receives the primary key and the exact change timing (start, stop, job queued) of every server in the system. The REST fleet endpoint hides those servers through `GameServerProjectionService`. The message's own remarks say the stream "never discloses" server data, but it does disclose existence, identity and activity. Authorization is also checked only once, at subscribe time, so a revoked claim keeps receiving frames until the socket drops. The client never uses the ID: `isFleetChangedFrame` only checks that `serverId` is a string, then invalidates the whole fleet query.
**Fix:** Drop the identifier from the wire message, because the client only needs a refetch trigger:
```csharp
public sealed class GameServerFleetChangedMessage
{
    // Intentionally empty: subscribers refetch the authorized fleet endpoint.
}
```
and change `isFleetChangedFrame` to check only `frame.type`. If an ID is needed later, filter per subscriber (resolve the subscriber's visible server set at subscribe time and re-check periodically) instead of broadcasting.

### WR-02: A quick action from a fleet card sends keyboard focus to the page heading

**File:** `sources/ReactApp/src/components/server/FleetServerCard.tsx:71-128`; `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx:148-160,246,264`; `sources/ReactApp/src/components/ActionControl.tsx:111-133`
**Issue:** `onSuccess` calls `notifySubmitted` (which runs `markSubmitted`) before `close()`. In that render the card's primary control changes from an enabled `ActionButton` (a bare `<button>`) to an `ActionButton` with `unavailableReason`, which renders `<span><button aria-disabled/>…</span>`. The root element type changes, so React unmounts the invoking `<button>`. `restoreFocus` then finds `closing.invoker.isConnected === false`. The card passes no `returnFocus`, so focus falls back to `main h1` (`PageHeader` renders it with `tabIndex={-1}`). After every start or stop from the fleet, keyboard and screen-reader users land at the top of the page instead of on the card. `useFleetFocusFallback` cannot recover this: it only acts when focus is on body or a detached element, and focus is on the connected `h1`.
**Fix:** Pass a stable return target from the card, for example the Open server link:
```tsx
const openRef = useRef<HTMLAnchorElement | null>(null);
// ...
request({ serverId: server.primaryKey, actionId: action.id, source: "fleet-card",
          returnFocus: openRef, onSubmitted: () => onSubmitted(server.primaryKey) });
// ...
<ActionLink ref={openRef} ... />
```
Alternatively, make `ActionButton` render the same wrapper structure whether or not a reason is set, so the `<button>` node survives the state change.

### WR-03: The Refresh button drops keyboard focus on every live-signal refetch

**File:** `sources/ReactApp/src/features/hosting/FleetPages.tsx:393-399`
**Issue:** `disabled={fleet.isFetching}` sets the native `disabled` attribute (`ActionButton` without `unavailableReason` renders a plain `<button>`). Polling is gone, but the fleet now refetches whenever any server changes. A focused element that becomes disabled loses focus to `<body>` under the HTML focus-fixup rule. So a keyboard user who has tabbed to Refresh, or just pressed it, loses focus whenever a fleet signal arrives. If the refetch also changes the card signature, `useFleetFocusFallback` then moves focus into the card list (see WR-04).
**Fix:** Keep the control focusable and ignore re-entry instead of disabling it:
```tsx
<ActionButton variant="ghost" aria-busy={fleet.isFetching}
  onClick={() => { if (!fleet.isFetching) void fleet.refetch(); }}>
```
or use `unavailableReason` (which renders `aria-disabled`) while fetching.

### WR-04: The focus fallback can move focus to a stale card long after the user left it

**File:** `sources/ReactApp/src/features/hosting/useFleetFocusFallback.ts:41-63`
**Issue:** `lastFocusedId` is set on `focusin` inside the container but is never cleared when focus moves outside the container or is dropped on purpose. The layout effect then calls `link.focus()` whenever the signature changes and `isFocusLost()` is true. `isFocusLost()` is also true when the user clicked a non-focusable background area, when the focused "Reconnect now" button unmounted after reconnecting (`FleetLiveNotice`), or when Refresh was disabled (WR-03). In each case the next live update that reorders or moves cards pulls focus, and scrolls the page, to whichever card was focused minutes earlier. The documented contract is to recover focus only "because the focused control unmounted" inside a card, but the hook does not check that.
**Fix:** Track the focused element, not just the server ID, and restore only when that element became detached. Also clear the remembered ID when focus leaves the container:
```ts
const lastFocused = useRef<{ id: string; element: Element } | null>(null);
// focusin inside container: lastFocused.current = { id, element: target }
// focusin outside container: lastFocused.current = null
// layout effect:
const last = lastFocused.current;
if (!last || last.element.isConnected || !isFocusLost()) return;
```

### WR-05: One stalled subscriber can block fleet signals for every client, and the queue is unbounded

**File:** `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs:32-37,102-110,134-141`; `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs` (`SendUpdateAsync`, `CancellationToken.None`); `sources/Api/Program.cs:102`
**Issue:** All fleet sends run sequentially on one reader loop and await `UpdateStreamService.SendUpdateAsync`, which sends to each socket with `CancellationToken.None`. A subscriber whose TCP window is full (suspended laptop, half-open connection behind a proxy) blocks that `SendAsync`, and with it the whole loop, until the kernel gives up on retransmission. That can take many minutes, and no other subscriber gets a signal meanwhile. `UseWebSockets` sets `KeepAliveInterval` but no `KeepAliveTimeout`, so dead peers are not detected quickly either. While the loop is blocked, `Notify` keeps writing to an unbounded channel, and duplicates are only collapsed after the loop resumes. `StopAsync` cancels only the coalesce delay, not an in-flight send.
**Fix:** Give each fleet send its own timeout and drop slow sockets, and enable a keep-alive timeout:
```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(25),
    KeepAliveTimeout  = TimeSpan.FromSeconds(30),
});
```
Add a `CancellationToken` overload to `SendUpdateAsync`, and have the notifier pass a linked CTS with a few seconds of timeout. Alternatively, collapse duplicates at write time with a bounded `HashSet` plus a signal channel of capacity 1 (`BoundedChannelFullMode.DropWrite`), so queue size is limited by the number of servers.

### WR-06: New C# private members lack the XML documentation that AGENTS.md requires

**File:** `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs:25-39,97,119,134`; `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs:41-43`
**Issue:** AGENTS.md requires meaningful XML documentation on every C# type and member, regardless of visibility. In the new handwritten files these have no `<summary>`: the fields `_updateStreamService`, `_logger`, `_stopping` (only a `//` comment), `_channel`, `_loop`, the methods `RunAsync`, `WaitCoalesceWindowAsync`, `SendAsync`, and the interceptor fields `_notifier`, `_logger`, `_pending`.
**Fix:** Add `<summary>` (plus `<returns>` and `<param>` where they apply) to each member. Turn the `_stopping` rationale comment into its `<remarks>`.

## Info

### IN-01: The freshness check on open is always true, so every page load fetches the fleet twice

**File:** `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:92-96`
**Issue:** `fleetState.dataUpdatedAt < Date.now()` holds for any data loaded before this millisecond. As a result the first socket open, which follows the initial fleet fetch, always invalidates and refetches both the fleet and notifications. The doc comment presents this as a conditional freshness check.
**Fix:** Record the time of the last disconnect, or of the subscribe call, and refetch only when `dataUpdatedAt` is older than that. Otherwise simplify the code and comment to "always refetch on open".

### IN-02: `useUpdateStream` keeps a stale status after it is disabled or re-subscribed

**File:** `sources/ReactApp/src/app/updateStream.ts:311,324-360`
**Issue:** The effect cleanup disposes the socket but never resets `status`. When `enabled` turns false (for example, access lost) the hook keeps reporting `"open"`. When `path` or `baseHref` changes, the new attempt starts from the previous status instead of `"connecting"`. `sessionRef.everOpened` also carries over between paths, which suppresses `unavailable` for a new path that never opens.
**Fix:** Call `setStatus("connecting")` at the start of each effect run (or `"connecting"`/idle in the `!enabled` branch), and reset `sessionRef.current` when `path` or `baseHref` changes.

### IN-03: The result summary live region speaks on every keystroke and repeats the empty state

**File:** `sources/ReactApp/src/components/server/FleetToolbar.tsx:93-102`; `sources/ReactApp/src/features/hosting/FleetPages.tsx:343-356`
**Issue:** The search filters immediately, without debounce, so the polite, atomic summary region changes with every character typed. When nothing matches, the "No servers match" `StatePanel` (`role="status"`, `aria-live="polite"`) is announced as well.
**Fix:** Feed the summary from the debounced URL `q` instead of the draft, or debounce the summary text separately.

### IN-04: The `FleetToolbar.counts` doc contradicts what the page passes

**File:** `sources/ReactApp/src/components/server/FleetToolbar.tsx:40`
**Issue:** The doc says "over the whole fleet", but `FleetPages.tsx:264` passes `countFilters(searched, meId)`, which is scoped to the search. The `countFilters` doc agrees with the page, not the prop doc.
**Fix:** Change the prop doc to "Number of servers each filter matches within the current search result."

### IN-05: The fleet path is spelled outside `FleetChangeStreamPaths`, despite its own remark

**File:** `sources/Api/Controllers/UpdateStreamController.cs:103`; `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:14`
**Issue:** `FleetChangeStreamPaths` says "No other file may spell the fleet path", yet the controller compares against the literal `"fleet"` and the client hard-codes `"game-servers/fleet"`. A rename of the constant would silently break subscriptions.
**Fix:** Add a `FleetSegment` constant and use it in the controller. Generate the client value or cover it with a contract test, and soften the remark.

### IN-06: Locale keys and CSS left orphaned by the `ServerCard` removal

**File:** `sources/ReactApp/src/locales/en.json` / `de.json` (`Hosting_Common_Open`, `Hosting_Servers_CostWarning`, `Server_Fleet_PendingJobs_*`, `Hosting_Common_Queue`, `Hosting_Common_Updated`); `sources/ReactApp/src/features/hosting/hosting.css:85` (`.next-hosting-cost-warning`)
**Issue:** No `.ts` or `.tsx` file references these after `ServerCard` was deleted from `features/hosting/components.tsx`.
**Fix:** Remove the dead keys and selector, or confirm a remaining consumer.

### IN-07: Shared components import a fleet-specific stylesheet

**File:** `sources/ReactApp/src/components/FilterChipGroup.tsx:4`; `sources/ReactApp/src/components/SearchField.tsx:4`
**Issue:** The reusable `FilterChipGroup` and `SearchField` (under `components/`) depend on `components/server/fleet-toolbar.css`. Reusing them elsewhere pulls in fleet toolbar layout rules, which weakens the UI-reuse convention in AGENTS.md.
**Fix:** Move `.filter-chip*` and `.search-control*` into their own `FilterChipGroup.css` and `SearchField.css` next to the components.

### IN-08: Impossible-state guard: a per-context lock in `FleetChangeInterceptor`

**File:** `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs:262-266,331-342,364-368,397-400,412-415,428`
**Category:** necessity
**Issue:** Each `PendingChanges` is keyed by one `DbContext` and is touched only from that context's save and transaction callbacks. EF Core forbids concurrent operations on one context: its concurrency detector throws "A second operation was started on this context instance". So two threads can never use the same `PendingChanges` at once. Keying by context in the `ConditionalWeakTable` is enough for isolation between contexts, and the lock guards a state EF already rules out. This rests on EF's documented invariant rather than on repository code, so it is Info.
**Fix:** Optionally remove `Lock` and the `lock` blocks and note the EF single-thread invariant in the remarks. Keeping them is harmless.

### IN-09: An optional nullable notifier silently disables the possible-cost signal if DI is misconfigured

**File:** `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs:49,462-463`
**Issue:** `IFleetChangeNotifier? fleetChangeNotifier = null` exists so tests can construct the executor without it. If the registration in `Program.cs` were ever removed, Microsoft DI would supply `null` with no error, and the bulk `PossibleCost` update would stop producing signals without any warning.
**Fix:** Make the parameter required and pass a no-op notifier in tests.

### IN-10: Removing the poll leaves some fleet-visible changes with no refresh path

**File:** `sources/ReactApp/src/features/hosting/useFleetQuery.ts:13-18`; `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs:279-315`
**Issue:** The fleet is now refreshed only by interceptor-covered changes, by reconnects, and by manual refresh. Some changes that are visible in the fleet produce no signal:
- `GameServer.LaunchDisabledReason` (shown as the disabled-start reason)
- a renamed cost owner `DisplayName`
- a new or revoked server resource claim, which changes which servers a viewer may see
- commits on another API instance (D-14)

In the `unavailable` state there is also no automatic fallback.
**Fix:** Add `LaunchDisabledReason` to the `GameServer` relevance check. Consider a long fallback poll (for example 60 s) while `status !== "open"`.

### IN-11: The WebSocket upgrade does not check `Origin`

**File:** `sources/Api/Program.cs:102`
**Issue:** WebSocket handshakes are not subject to CORS. `SameSite=Lax` cookies (`Program.cs:337`) block cross-site use in current browsers, but a same-site origin (another subdomain) can still open the cookie-authenticated fleet stream. `WebSocketOptions.AllowedOrigins` is left empty. This is defense in depth on top of WR-01.
**Fix:** Fill `WebSocketOptions.AllowedOrigins` from the existing `Cors:AllowedOrigins` and `General:ClientBaseUrl` configuration.

---

_Reviewed: 2026-11-19T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

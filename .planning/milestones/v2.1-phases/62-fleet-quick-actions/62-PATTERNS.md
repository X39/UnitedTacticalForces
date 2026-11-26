# Phase 62: Fleet & Quick Actions - Pattern Map

**Mapped:** 2026-11-18
**Files analyzed:** 33 new/modified
**Analogs found:** 31 / 33

All analog paths are git-tracked source under `sources/` or `tests/` (spot-checked with `git ls-files`); no gitignored mirror paths are used. Paths are relative to the repo root unless prefixed `src/` or `tests/`, which are relative to `sources/ReactApp/`.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/features/hosting/fleetModel.ts` (new) | utility (pure model) | transform | `src/features/hosting/serverState.ts` + `orderFleetServers` in `FleetPages.tsx` | role-match |
| `src/features/hosting/FleetPages.tsx` (mod: `ServerFleetPage`, `ServerFleetView` replaced) | component (page) | request-response | itself (current `ServerFleetPage`) | exact |
| `src/components/server/FleetServerCard.tsx` (new) | component | request-response | `ServerCard` in `src/features/hosting/components.tsx` | exact |
| `src/components/server/FleetSection.tsx` (new) | component | transform | `ServerFleetView` grid in `FleetPages.tsx` | role-match |
| `src/components/server/FleetToolbar.tsx` (new) | component | event-driven | `src/components/CommandListToolbar.tsx` | role-match |
| `src/components/server/FleetLiveNotice.tsx` (new) | component | event-driven | `StatePanel` in `src/components/primitives.tsx` + `DeepLinkActionNotice.tsx` | role-match |
| `src/components/server/fleet.css` (new) | config (style) | n/a | `src/components/server/server.css` | exact |
| `src/components/FilterChipGroup.tsx` (new) | component | event-driven | `src/components/ActionControl.tsx` (`ActionButton` + `aria-pressed`) | partial |
| `src/components/SearchField.tsx` (new) | component | event-driven | `src/components/CommandListToolbar.tsx` | role-match |
| `src/components/ActionControl.tsx` (mod: describedby merge) | component | request-response | itself, lines ~95-135 | exact |
| `src/app/updateStream.ts` (new) | hook / service | streaming | none in React app (backend analog `UpdateStreamService.cs`) | no analog (React) |
| `src/features/hosting/useFleetLiveUpdates.tsx` (new) | provider | event-driven | `src/components/server/CostConfirmationProvider.tsx` | role-match |
| `src/features/hosting/useFleetQuery.ts` (mod) | hook | request-response | itself | exact |
| `src/components/server/CostConfirmationProvider.tsx` + `useCostConfirmation.ts` (mod: `onSubmitted`) | provider | request-response | themselves | exact |
| `src/components/server/costConfirmationModel.ts` (mod: export `ownerFor`) | utility | transform | itself | exact |
| `src/components/CommunityLayout.tsx`, `src/components/CommandCenterLayout.tsx` (mod: mount provider) | component (layout) | request-response | `CostConfirmationProvider` mount in `CommunityLayout.tsx` lines 76-217 | exact |
| `src/locales/en.json`, `de.json` (mod) | config (i18n) | n/a | existing `Server_*` / `Hosting_*` keys (lines ~593-630) | exact |
| `src/styles/tokens.css` (mod: `--utf-fleet-card-min`) | config | n/a | existing `--utf-*` tokens | exact |
| `.planning/phases/61-.../61-PARITY-MATRIX.md` (mod) | doc | n/a | itself | exact |
| `tests/hosting/fleet-model.test.ts` (new) | test | transform | `tests/hosting/server-state.test.ts` | role-match |
| `tests/hosting/fleet-card.test.tsx`, `fleet-page.test.tsx` (new) | test | request-response | `tests/hosting/fleet.test.tsx`, `member-access.test.tsx` | exact |
| `tests/hosting/fleet-live-updates.test.tsx`, `tests/app/update-stream.test.tsx` (new) | test | streaming | `tests/hosting/cost-confirmation-provider.test.tsx` | partial |
| `tests/hosting/fleet.test.tsx` (mod/replace) | test | request-response | itself | exact |
| `tests/hosting/member-access.test.tsx`, `cost-confirm-layouts.test.tsx`, `cost-confirmation-provider.test.tsx` (extend) | test | request-response | themselves | exact |
| `tests/setup.ts`, `tests/mocks/react-icons.tsx` (mod) | config (test) | n/a | themselves | exact |
| `tests/e2e/hosting-fleet.spec.ts` (new); `public-shell.spec.ts` (mod) | test (e2e) | request-response | `tests/e2e/hosting-a11y.spec.ts` | role-match |
| `sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs` (new) | model (wire message) | pub-sub | `sources/WebSocketContracts/Eventing/EventChangedMessage.cs` | exact |
| `sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs`, `FleetChangeNotifier.cs` (new) | service | pub-sub | `sources/Api/HostedServices/GameServerControlPlaneWorker.cs` + `UpdateStreamService.cs` | role-match |
| `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs` (new) | middleware (EF interceptor) | event-driven | none | no analog |
| `sources/Api/Controllers/UpdateStreamController.cs` (mod) | controller | streaming | itself, `CanSubscribeAsync` | exact |
| `sources/Api/DTO/GameServerDto.cs` + `GameServerProjectionService.cs` (mod: `CostOwnerUserId`) | model / service | CRUD (projection) | themselves | exact |
| `sources/Api/Program.cs` (mod: DI + KeepAlive), `LifecycleJobExecutor.cs` (mod: one `Notify`) | config / service | event-driven | `Program.cs` lines 215-229; `LifecycleJobExecutor.cs` lines 436-452 | exact |
| `tests/Api.Tests/GameServerHosting/FleetChange*Tests.cs`, `UpdateStreamControllerFleetTests.cs`, `GameServerProjectionServiceTests.cs` (ext.) | test | event-driven | `tests/Api.Tests/GameServerHosting/HostingTestStore.cs` + existing fixtures | role-match |

## Pattern Assignments

### `src/components/server/FleetServerCard.tsx` (component, request-response)

**Analog:** `src/features/hosting/components.tsx` `ServerCard` (lines ~63-218; the card being replaced).

**Imports pattern** (`components.tsx` lines 1-16):
```typescript
import { useTranslation } from "react-i18next";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { ActionUnavailableReason, actionIsUnavailable } from "@/components/server/ActionUnavailableReason";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, ServerAction } from "@/features/hosting/types";
```

**Core pattern** (`components.tsx` ServerCard top): article with `aria-labelledby={titleId}`, `titleId = \`server-${server.primaryKey}-title\``, kicker `server.gameIdentifier`, title heading, `<ServerStateSummary derived={deriveServerState(server)} />`, a `<dl>` of facts. Keep that skeleton; change h2 to h3, drop runtime/queue/updated/cost-warning/terminate (D-04, D-11), add the stage line, `data-server-id` on the Open `ActionLink`, and `aria-describedby` for the card title.

**Disabled-with-reason pattern** (`src/components/ActionControl.tsx` lines ~100-135): pass `unavailableReason={<UnavailableReason .../>}` to `ActionButton`; the component keeps focus, sets `aria-disabled`, and merges `aria-describedby`. The UI-SPEC wants the caller `aria-describedby` merged with the reason id. The current code already merges it:
```typescript
const { "aria-describedby": describedBy, ...rest } = buttonProps;
... aria-describedby={describedBy ? `${describedBy} ${reasonId}` : reasonId}
```
So no ActionControl change is needed unless the enabled branch (`<button {...buttonProps}>`) is checked; it already forwards the prop. Verify, then only add `fullWidth` usage.

**Request pattern** (`useCostConfirmation.ts` lines 4-20): `request({ serverId, actionId, source: "fleet-card", onSubmitted })`. Card must call only this, never execute.

---

### `src/features/hosting/fleetModel.ts` (utility, transform)

**Analogs:** `src/features/hosting/serverState.ts` (`deriveServerState` is the only state decider) and `orderFleetServers` in `FleetPages.tsx`.

**Ordering to reuse** (`FleetPages.tsx` `orderFleetServers`):
```typescript
const collator = new Intl.Collator(currentLocale(), { sensitivity: "base" });
return [...servers].sort(
  (left, right) =>
    Number(Boolean(right.deployment?.possibleCost)) - Number(Boolean(left.deployment?.possibleCost)) ||
    Number(right.pendingJobCount > 0) - Number(left.pendingJobCount > 0) ||
    collator.compare(left.title, right.title),
);
```
Keep this exported for Active; Off sorts by title only. Import `currentLocale` from `@/app/format`. Use RESEARCH Patterns 1 and 2 verbatim for `parseFleetSearchParams`, `matchesFilter`, and the NFD fold search. Export `ownerFor` from `costConfirmationModel.ts` (constants `hiddenCostOwnerName`, `unknownCostOwnerName` at lines 5-8) for placeholder exclusion. No React imports. Add a `docs-style` JSDoc block as in `useFleetQuery.ts`.

---

### `src/features/hosting/FleetPages.tsx` `ServerFleetPage` (component, request-response)

**Analog:** current `ServerFleetPage` (lines ~170-241 of the file).

**Keep** the auth/permission/loading/error scaffold:
```typescript
const auth = useAuth();
const permissions = getHostingPermissions(auth.user);
const fleet = useFleetQuery(!auth.isLoading && permissions.accessServers);
const loadError = <HostingError onRetry={() => void fleet.refetch()} />;
...
<HostingAccessBoundary allowed={permissions.accessServers}>
  {fleet.isPending ? <StatePanel kind="loading">...</StatePanel>
   : fleet.isError && !hasFleetData ? loadError
   : (<>{fleet.isError && loadError}<View .../></>)}
```
**Change:** header action label to `Server_Fleet_Refresh`, Create variant to `primary` (gold only in the creator empty state, which stays the `ServerFleetView` empty branch using `ActionLink variant="gold"`). Add `useSearchParams` with `{ replace: true }` (same API as `CostConfirmationProvider.tsx` `stripAction`, which preserves other params by copying `new URLSearchParams(current)`). Use the `{ value, base }` draft pattern from RESEARCH Pattern 3 (no effect-driven sync; eslint react-hooks 7 is `--max-warnings 0`). `HostingOverviewView`/`HostingOverviewPage` stay unchanged (own 10s poll stays, Pitfall 16).

---

### `src/features/hosting/useFleetQuery.ts` (hook, request-response)

**Analog:** itself (19 lines). Delete only `refetchInterval: 10_000,` and update the JSDoc sentence "one poll interval". Keep the single key `hostingQueryKeys.fleet`.

---

### `src/components/server/CostConfirmationProvider.tsx` + `useCostConfirmation.ts` (provider, additive `onSubmitted`)

**Analog:** themselves. Add `onSubmitted?(): void` to `CostConfirmationRequest` (after `returnFocus`, lines 17-18 of `useCostConfirmation.ts`), store it on `ActiveRequest` (interface at provider lines 26-33, set in `open` at the `const next: ActiveRequest = {...}` block), and invoke it first in the `onSuccess` of `confirm` (provider `execute.mutate(..., { onSuccess: () => { ... } })`) before the invalidations:
```typescript
onSuccess: () => {
  closing.onSubmitted?.();
  const vocabulary = lookupActionVocabulary(action.id);
  ...
```
Do not change the broad invalidations (intentional, Anti-Pattern note). Extend `tests/hosting/cost-confirmation-provider.test.tsx` using its `Probe` + `startRequest` structure (lines ~48-60).

---

### `src/features/hosting/useFleetLiveUpdates.tsx` (provider, event-driven)

**Analog:** `src/components/server/CostConfirmationProvider.tsx` for context/provider shape, `useQueryClient`, ref-synced callback via `useEffect(() => { ref.current = ... })`:
```typescript
const queryClient = useQueryClient();
...
useEffect(() => { pathnameRef.current = location.pathname; stripActionRef.current = stripAction; ... });
const value = useMemo<ContextValue>(() => ({ ... }), [...]);
return <Context.Provider value={value}>{children}</Context.Provider>;
```
Context object and consumer hook split into a `.ts` file like `useCostConfirmation.ts` (context + throwing hook). Invalidations use RESEARCH "Signal handler" with `exact: true` on `hostingQueryKeys.fleet` and `hostingQueryKeys.notifications` (`src/features/hosting/services.ts` lines ~110-129). Build the URL from the layout `configuration` prop via `resolveApiBaseUrl` (`src/app/config.ts`), never `getApiClient()` (Pitfall 8). Gate on `!auth.isLoading && canAccessGameServers(auth.user)` (`src/auth/claims.ts`).

---

### `src/app/updateStream.ts` (hook/service, streaming)

**No React analog.** Use RESEARCH Pattern 5 and "Subscription URL" (`toSocketUrl`, `nextDelayMs`, `createCoalescer`). Wire frame shape is fixed by the backend (`UpdateStreamService.cs` lines 14-18 and 77-82): `ReferenceHandler.Preserve` plus `JsonSerializerDefaults.Web`, so frames look like `{"$id":"1","type":"<FullTypeName>","value":{"$id":"2","serverId":"..."}}`. Parse defensively and ignore unknown frames. Pin the type with a unit test against a captured frame string. Export pure helpers for fake-timer tests. Honour StrictMode (the app renders under `<StrictMode>`): per-effect-run `disposed` flag.

---

### `src/components/server/FleetToolbar.tsx`, `SearchField.tsx` (component, event-driven)

**Analog:** `src/components/CommandListToolbar.tsx` (submit-based search; not changed). Read it before writing for label/field markup conventions; `SearchField` differs by being instant, using `<input type="search">` in a `<label>` with an icon-only clear `ActionButton`:
```tsx
<ActionButton iconOnly aria-label={t("Server_Fleet_SearchClear")} variant="ghost"><Dismiss20Regular /></ActionButton>
```
(`iconOnly` requires `aria-label`, see `ActionControlContentProps` in `ActionControl.tsx` lines 20-34). `Dismiss20Regular` already exists in `tests/mocks/react-icons.tsx`.

### `src/components/FilterChipGroup.tsx` (component, event-driven)

**Analog (partial):** `ActionButton` with `aria-pressed` spread through `ComponentPropsWithoutRef<"button">`. Wrap in `<div role="group" aria-label=...>`; pressed chip adds `Checkmark16Regular` (must be added to the icon mock, Pitfall 9).

### `src/components/server/FleetLiveNotice.tsx`

**Analog:** `StatePanel kind="info"` from `src/components/primitives.tsx` (line 78, `StatePanel({ kind, title, action, children })`, used as in `HostingError` in `components.tsx` lines 48-62):
```tsx
<StatePanel kind="error" title={t("Server_Access_LoadError")}
  action={<ActionButton variant="ghost" onClick={onRetry}>{t("Server_Access_Retry")}</ActionButton>}>
```
Use `kind="info"` and `role="status"`. `PlugDisconnected20Regular` must be added to the icon mock.

### `src/components/server/fleet.css` and `src/styles/tokens.css`

**Analog:** `src/components/server/server.css`. Header comment (lines 1-7) states the guard: tokens only, 1px border width, breakpoints 520px and 800px only. Mirror the doubled-selector override trick (`.status-badge.server-state-host`) to beat legacy `.button { white-space: nowrap; width: fit-content }` (Pitfall 10). The guard test `tests/styles/server-style-guard.test.ts` globs every `*.css` in `components/server/`, so `fleet.css` is covered automatically. Import `fleet.css` wherever `server.css` is imported. Add `--utf-fleet-card-min: 22rem;` beside the other `--utf-*` tokens.

---

### Layouts: `CommunityLayout.tsx`, `CommandCenterLayout.tsx` (mod)

**Analog:** `CommunityLayout.tsx` lines 13 and 76/217: the whole shell is wrapped:
```tsx
<CostConfirmationProvider>
  <div className="site-shell">...</div>
</CostConfirmationProvider>
```
Nest `<FleetLiveUpdatesProvider configuration={configuration}>` directly inside, in both layouts (verify `CommandCenterLayout.tsx` has the same wrapper). Do not add a Servers link to the community header (D-01).

---

### Locale files (mod)

**Analog:** existing flat keys, e.g. `"Hosting_Nav_Servers": "Game servers"` (line 593 en/de) and `"Hosting_Servers_Title"` (line 630). Change values per UI-SPEC "Changed existing keys"; add `Server_Fleet_*` keys with `_one`/`_other` pairs in both files. The parity gate is `npm run locales:check` plus `tests/locales/server-keys.test.ts`. Remove orphaned keys (`Hosting_Servers_CostWarning`, `Server_Fleet_PendingJobs_*`, summary keys) only if no other caller remains (grep first).

---

### Backend: `GameServerFleetChangedMessage.cs` (wire message, pub-sub)

**Analog:** `sources/WebSocketContracts/Eventing/EventChangedMessage.cs` (namespace `X39.UnitedTacticalForces.WebSockets.Contracts.Eventing`; file-scoped; one type per file). New file lives in `sources/WebSocketContracts/Hosting/` with namespace `...Contracts.Hosting`. Unlike the analog, AGENTS.md requires XML docs on type and member (use the snippet in RESEARCH "New contract message"; `WebSocketMessage.cs` is the documented-style reference).

### Backend: `FleetChangeNotifier.cs` / `IFleetChangeNotifier.cs` (service, pub-sub)

**Analogs:** `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs` for the send contract; `sources/Api/HostedServices/GameServerControlPlaneWorker.cs` (primary-constructor hosted service, registered via `AddHostedService` in `HostingControlPlaneServiceCollectionExtensions.cs` lines 21-27) for the background-loop shape.

**Send contract** (`UpdateStreamService.cs` lines 71-95): `SendUpdateAsync<T>(string path, T package)` serializes `WebSocketMessage<T>` and matches `q.path.StartsWith(path)`; the per-socket `SendAsync` is not concurrency-safe and failures silently drop the subscriber, so the notifier must serialize (one reader loop on `Channel<Guid>`, coalesce ~250 ms, catch all, structured logging). Path constant `$"{Constants.Routes.GameServers}/fleet"` (`Constants.cs:36`). DI: use `[Singleton<FleetChangeNotifier, IFleetChangeNotifier>]` attribute style (`UpdateStreamService.cs` line 11) and also register as hosted service the way `DiscordBot` is (`Program.cs` lines 227-228):
```csharp
builder.Services.AddSingleton<DiscordBot>();
builder.Services.AddHostedService<DiscordBot>(sp => sp.GetRequiredService<DiscordBot>());
```
Place under `Services/GameServerLifecycle/` per RESEARCH Open Question 3 with written placement justification (AGENTS.md cross-cutting rule). XML-doc the process-local limitation (D-14).

### Backend: `FleetChangeInterceptor.cs` (EF interceptor)

**No analog in repo.** Follow RESEARCH Pattern/Pitfall 2 and the registration snippet. Registration site: `Program.cs` lines 215-226 `AddDbContextFactory<DatabaseContext>(options => {...})` becomes the `(serviceProvider, options)` overload with `options.AddInterceptors(serviceProvider.GetRequiredService<FleetChangeInterceptor>())`; keep the Development-only `EnableDetailedErrors/EnableSensitiveDataLogging`. Singleton thread-safety via `ConditionalWeakTable<DbContext, HashSet<Guid>>`. Add one explicit `Notify(deployment.GameServerFk)` after the possible-cost bulk update at `LifecycleJobExecutor.cs` lines 436-452 (`ExecuteUpdateAsync` ... `ReloadAsync`); make the executor dependency optional/no-op because tests construct it by hand.

### Backend: `UpdateStreamController.cs` (mod)

**Analog:** itself, `CanSubscribeAsync` (lines ~54-83): segments split with `RemoveEmptyEntries | TrimEntries`, `segments.Length < 2` guard, events branch with `string.Equals(segments[0], Constants.Routes.Events, StringComparison.OrdinalIgnoreCase)`. Add a sibling branch before the final `return false;` for `Constants.Routes.GameServers` + `fleet` with exactly two segments, and check claims with `User.HasClaim(...)` like the events branch (`Claims.Administrative.All`, `Claims.Administrative.Event`). Server-section claims: `sv`/`sv:view` (any value), `crt:sv`, `adm:sv`, `adm:hosting`, `adm` (mirror `src/auth/claims.ts` `canAccessGameServers`; locate matching constants under `sources/Claims`). `SubscribeAsync` must register the canonical path (`"game-servers/fleet"`), not the raw query value (Pitfall 4); the current code passes `path` straight to `RegisterAsync` (line 50), so compute a canonical string from `CanSubscribeAsync`.

### Backend: `GameServerDto.cs` + `GameServerProjectionService.cs` (`CostOwnerUserId`)

**Analog:** the existing projection (`GameServerProjectionService.cs` lines 793-796 `canSeeOwner`, lines 855-858 `CostOwnerId`/`CostOwnerName`; a second projection block at ~895 for deployment). `GameServerDto.cs` line 31-32 declares `CostOwnerId`/`CostOwnerName`. Add `public Guid? CostOwnerUserId { get; init; }` with XML docs next to them and set per RESEARCH snippet (`canSeeOwner && server.CurrentCostOwner?.Kind == ECostOwnerKind.User ? server.CurrentCostOwner.UserFk : null`). Line 692 shows `.Include(q => q.CurrentCostOwner)` is already loaded. Regenerate `sources/ReactApp/openapi/utf-api.json` and `src/generated/api.ts` per RESEARCH; no EF migration.

### Backend tests

**Analog:** `tests/Api.Tests/GameServerHosting/HostingTestStore.cs` (in-memory SQLite, `NodaTimeSqliteApiDbContext`, `EnsureCreatedAsync`):
```csharp
var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(connection).Options;
```
Add `.AddInterceptors(...)` in a dedicated builder for interceptor tests; use a fake `IUpdateStreamService` for notifier tests. Extend `GameServerProjectionServiceTests.cs` for `CostOwnerUserId`. Reuse `HostingTestSeed.cs`/`HostingTestData.cs` for fixtures.

---

### React tests

**`tests/hosting/fleet-page.test.tsx`, `fleet-card.test.tsx` analog:** `tests/hosting/fleet.test.tsx` (server fixture factory lines 11-40 with `as unknown as GameServer`) and `tests/helpers/renderWithProviders.tsx` (`route`, `path`, `queryClient`, `strict` options; wraps in query client, i18n, toasts, memory router and `CostConfirmationProvider`). Use `route: "/command/servers?q=x"` for URL-state tests and `strict: true` for the socket cleanup test.

**Hook tests:** fake timers plus `FakeWebSocket` from RESEARCH "Test-time WebSocket double", installed with `vi.stubGlobal` in `tests/setup.ts` (current file has only jest-dom and dialog polyfills, 9 lines).

**Icon mock:** `tests/mocks/react-icons.tsx` is an explicit export list in the form `export const Dismiss20Regular = Icon;`; add `Checkmark16Regular` and `PlugDisconnected20Regular` (and `Warning20Regular` if absent).

**ACC-02:** extend `tests/hosting/member-access.test.tsx` (`describe("member access to the fleet")`, line 123) and `cost-confirm-layouts.test.tsx`; do not duplicate.

**Breaking-by-design tests to replace:** `tests/hosting/fleet.test.tsx` (terminate link, `2 ausstehend`, cost-warning paragraph, `ServerFleetView({servers, canCreate})` usage); `tests/e2e/public-shell.spec.ts` lines ~369 (`Game servers`), ~1175 (`Spielserver`), ~1176+ (old cost sentence).

---

## Shared Patterns

### State and cost rendering
**Source:** `src/features/hosting/serverState.ts` `deriveServerState`, `src/components/server/ServerStateSummary.tsx`.
**Apply to:** `fleetModel`, `FleetServerCard`, cost line, announcements. Never compare `deployment.status` in a component (FND-03; guarded by `tests/hosting/server-state-single-owner.test.ts`, which will flag a second decider).

### Disabled-with-reason
**Source:** `ActionButton unavailableReason` (`ActionControl.tsx` lines ~95-135) + `UnavailableReason` (`src/components/server/UnavailableReason.tsx`, props `reason`, `stateLabel`, `launchDisabled`, `id`).
**Apply to:** card primary action, header Create control (`Server_Reason_CreatePermission`, already used in `ServerFleetPage`).

### Confirmation
**Source:** `useCostConfirmation().request` with `source: "fleet-card"`.
**Apply to:** start and stop only. Terminate never appears on the card.

### Query keys and invalidation
**Source:** `hostingQueryKeys` in `src/features/hosting/services.ts` (fleet `["hosting","servers"]` is a prefix of per-server keys: use `exact: true` for signal-triggered invalidation).

### Style guard
**Source:** `src/components/server/server.css` header and `tests/helpers/cssGuard.ts`. Tokens only; 1px borders; 520px/800px breakpoints only.

### Backend documentation and file rules (AGENTS.md)
**Apply to:** every new C# file: one top-level type per file named after the type, file-scoped namespace, English XML docs on every type and member with `<summary>`/`<param>`/`<returns>`; enums `E`-prefixed; structured log templates, no secrets. Additive only, so commit subjects carry no `!`.

### Authorization on subscription
**Source:** `UpdateStreamController.CanSubscribeAsync` events branch (claim checks via `User.HasClaim`). **Apply to:** the fleet-path branch.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/app/updateStream.ts` | hook | streaming | React app has no WebSocket client; use RESEARCH Pattern 5 and the backend frame shape |
| `FleetChangeInterceptor.cs` | EF interceptor | event-driven | No `SaveChangesInterceptor` exists in the repo; use RESEARCH Pitfall 2 and the Microsoft interceptor docs |
| `FilterChipGroup.tsx` (partial) | component | event-driven | No existing toggle-chip group component; ad-hoc `.filter-chips` markup exists in admin pages but should not be copied |

## Metadata

**Analog search scope:** `sources/ReactApp/src/{app,components,components/server,features/hosting}`, `sources/ReactApp/tests/{hosting,helpers,mocks}`, `sources/Api/{Controllers,Services/UpdateStreamService,Services/GameServerLifecycle,HostedServices,DTO,Program.cs}`, `sources/WebSocketContracts`, `tests/Api.Tests/GameServerHosting`.
**Files scanned:** about 45 (targeted reads and greps; large files read by range).
**Pattern extraction date:** 2026-11-18

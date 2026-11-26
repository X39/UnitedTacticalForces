# Phase 63: Workspace Shell & Activity - Pattern Map

**Mapped:** 2026-11-19
**Files analyzed:** 38 (new + modified, incl. tests and docs)
**Analogs found:** 34 / 38

Scope reminder: `sources/ReactApp` plus the minimal actor projection on `LifecycleJobDto` / `HostingOperationDto`
(Post-Research Decision). Do not touch `sources/WebApp` (Kiota not regenerated, 62-02 precedent) or
`sources/Common/Ui/routes.json`. All analog paths below are git-tracked source.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `sources/Api/DTO/LifecycleJobDto.cs` (mod: actor field) | model (DTO) | request-response | `sources/Api/DTO/GameServerDto.cs` L34-36 (`CostOwnerUserId`) | exact |
| `sources/Api/DTO/HostingOperationDto.cs` (mod: actor field) | model (DTO) | request-response | same | exact |
| `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs` (mod: `ProjectJob`, `ProjectOperation`, query Includes) | service | CRUD projection | same file L856-862 (privacy-gated owner projection) | exact |
| `tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs` (mod) | test | - | same file L717-747 `CostOwnerUserId_*` | exact |
| `sources/ReactApp/openapi/utf-api.json`, `src/generated/api.ts` (regen) | generated | - | 62-02 Task 2 commands | exact |
| `src/features/hosting/useFleetLiveUpdates.ts` (mod: `subscribe`) | provider context | event-driven | itself | exact |
| `src/features/hosting/FleetLiveUpdatesProvider.tsx` (mod: listener registry) | provider | event-driven / pub-sub | itself | exact |
| `src/features/hosting/useWorkspaceLiveUpdates.ts` (new) | hook | event-driven | `FleetLiveUpdatesProvider.tsx` L38-47, 78-90 | role-match |
| `src/features/hosting/workspaceSections.ts` (new; exports `workspaceTabIds`) | utility/model | transform | `ServerWorkspacePage.tsx` L44-69 | exact |
| `src/features/hosting/jobProgress.ts` (new) | model | transform | `serverState.ts` (`deriveServerState`) + `workspaceModel.ts` L228-246 | role-match |
| `src/features/hosting/activityModel.ts` (new) | model | transform | `workspaceModel.ts` L248-291 (`protocolEntries`) | role-match |
| `src/features/hosting/workspaceFreshness.ts` (new) | model | transform | `FleetLiveNotice.tsx` L40 disconnected rule | partial |
| `src/features/hosting/workspaceModel.ts` (mod: extract `costStopAction`) | utility | transform | itself L228-246 | exact |
| `src/components/server/costConfirmationModel.ts` (mod: extract `serverCostFacts`) | utility | transform | itself L80-95 | exact |
| `src/components/server/useCostConfirmation.ts` (mod: source union) | context | - | itself L4-5 | exact |
| `src/features/hosting/ServerWorkspacePage.tsx` (rewrite: route, queries, deep link, delete) | page/controller | request-response | itself L590-787 | exact |
| `src/features/hosting/WorkspaceShell.tsx` (new) | component | request-response | `ServerWorkspacePage.tsx` L682-765 | exact |
| `src/features/hosting/sections/OverviewSection.tsx` | component | request-response | `ServerWorkspacePage.tsx` L397-550 | exact |
| `src/features/hosting/sections/ActivitySection.tsx` | component | request-response | `ServerLogsPanel.tsx` + `HostingAuditFeed.tsx` | role-match |
| `src/features/hosting/sections/SettingsSection.tsx` | component | CRUD | `ServerWorkspacePage.tsx` L561-568, 719-731, 766-781 | exact |
| `src/features/hosting/sections/MachineSection.tsx` | component | request-response | `ServerWorkspacePage.tsx` L181-331 (`CloudPanel`) | exact (move) |
| `src/components/Breadcrumbs.tsx` (new) | component (shared) | - | `src/components/FilterChipGroup.tsx` (generic shared component shape) | role-match |
| `src/components/SectionNavigation.tsx` (new) | component (shared) | - | `FilterChipGroup.tsx` + nav markup `ServerWorkspacePage.tsx` L382-396 | role-match |
| `src/components/CopyButton.tsx` (new) | component (shared) | event-driven | `FilterChipGroup.tsx` (ActionButton-based) | role-match |
| `src/components/server/WorkspaceHeader.tsx` | component | - | `ServerWorkspacePage.tsx` L399-434 (primary action) | exact |
| `src/components/server/CostStrip.tsx` | component | - | `CostIndicator.tsx` + `costConfirmationModel.ts` | role-match |
| `src/components/server/ServerEndpoint.tsx` | component | - | `ServerWorkspacePage.tsx` L516-518 | role-match |
| `src/components/server/JobProgressStrip.tsx` | component | - | `CostIndicator.tsx` (glyph map) | partial |
| `src/components/server/JobFailurePanel.tsx` | component | request-response | `JobList` L117-138 (retry/skip) | role-match |
| `src/components/server/LiveBadge.tsx` | component | event-driven | `src/components/server/FleetLiveNotice.tsx` | exact |
| `src/components/server/ActivityTimeline.tsx`, `ActivityEntry.tsx` | component | - | `JobList` L139-173 + `HostingAuditFeed.tsx` | role-match |
| `src/components/server/SlotPlaceholder.tsx` | component | - | `StatePanel` (`@/components/primitives`) | partial |
| `src/components/server/workspace.css` (+ `activity.css`) | config (styles) | - | `src/components/server/fleet-toolbar.css` | exact |
| `src/styles/tokens.css` (3 tokens), `src/components/commandCenter.css` (delete tabs sticky rule), `src/features/hosting/hosting.css` (nav overflow) | config | - | existing files | exact |
| `src/locales/en.json`, `de.json`, `identical-allowed.json` | config (i18n) | - | existing flat `Server_*` keys | exact |
| `.planning/phases/61-.../61-PARITY-MATRIX.md`, `tests/hosting/parity-matrix.test.ts` (import) | docs/test | - | itself | exact |
| `.planning/REQUIREMENTS.md` WSP-02, `.planning/ROADMAP.md` SC-2, `docs/features/react-ui-beta.md` | docs | - | - | n/a |
| New tests (see Testing section) | test | - | `tests/hosting/fleet-live-updates.test.tsx`, `fleet-toolbar.test.tsx`, `workspace-header.test.tsx` | exact |
| `tests/e2e/hosting-workspace.spec.ts` (new) | e2e test | - | `tests/e2e/hosting-fleet.spec.ts` + `tests/e2e/support/fleetStream.ts` | exact |

## Pattern Assignments

### API actor projection (Wave 1, mirrors 62-02)

**Analog DTO member** `sources/Api/DTO/GameServerDto.cs` L34-36 (copy doc shape exactly; one-line summary + value to
avoid CRLF diffs in the OpenAPI text):
```csharp
/// <summary>Identifier of the user behind the cost owner when the owner is a user and the viewer may see the cost owner; otherwise null.</summary>
/// <value>The paying user's identifier, or <see langword="null"/> for non-user owners and owners hidden from the viewer.</value>
public Guid? CostOwnerUserId { get; init; }
```
Add to `LifecycleJobDto` (currently undocumented members, L10-27; document only the new member) e.g.
`RequestedByName` (`string?`), and to `HostingOperationDto` e.g. `ActorName` (`string?`). Exact names are the
planner's choice; UI-SPEC requires: non-null name, `null` = system ("The platform"), and a hidden-user placeholder
string mirrored as a TS constant (like `hiddenCostOwnerName`).

**Projection points** in `GameServerProjectionService.cs`:
- `ProjectJob` L633-686 (`new() { ... }` initializer; add the actor next to `FailureReason`). Entity has
  `LifecycleJob.RequestedBy` / `RequestedByFk` (`sources/Database/Hosting/LifecycleJob.cs` L35-42). Ensure the
  `RequestedBy` navigation is loaded wherever jobs are queried (`ServerQuery()` L688-702 includes
  `LifecycleJobs.ThenInclude(WorkerCommands)` only; the jobs endpoint `GameServersController.cs` L155 calls
  `ProjectJob`).
- `ProjectOperation` L704-728 is `static` and the query at L619-626 has no `Include(q => q.Actor)`; add the include
  and the field.
- Privacy comment pattern (L859-862):
```csharp
// Privacy rule: a viewer who cannot see the cost owner learns no user id through this field.
CostOwnerUserId = canSeeOwner && server.CurrentCostOwner is { Kind: ECostOwnerKind.User } costOwner
    ? costOwner.UserFk
    : null,
```
**Test pattern** `tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs` L717-747:
```csharp
await using var store = await HostingTestStore.CreateAsync();
var seed = await HostingTestData.SeedServerAsync(store.DbContext);
var projection = HostingTestData.CreateProjectionService(store.DbContext);
var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);
Assert.NotNull(dto);
```
Each `[Fact]` has `<summary>` + `<returns>` XML docs (AGENTS.md).
**Regeneration** (62-02 SUMMARY, deviation 1): export with an ABSOLUTE path:
`dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <abs>/sources/ReactApp/openapi/utf-api.json`,
then `npm --prefix sources/ReactApp run generate`, verify `contracts:check`, `typecheck`. Separate `build(api)` commit.

---

### `src/features/hosting/useFleetLiveUpdates.ts` + `FleetLiveUpdatesProvider.tsx` (additive listener API)

**Context value** (L5-22) - extend interface and keep a no-op default so `renderWithProviders` keeps working:
```typescript
export const FleetLiveUpdatesContext = createContext<FleetLiveUpdatesValue>({
  status: "open",
  disconnectEpoch: 0,
  reconnectNow: () => undefined,
  // add: subscribe: () => () => undefined,
});
```
**Frame hook point** (provider L85-90) - call listeners with the frame's serverId, outside the fleet coalescer:
```typescript
const onMessage = useCallback(
  (frame: UpdateStreamFrame) => {
    if (isFleetChangedFrame(frame)) signalCoalescer.trigger();
  },
  [signalCoalescer],
);
```
`isFleetChangedFrame` (L28-36) already validates `serverId` is a string. Add `subscribe` to the memoized value
(L109-112). Keep TSDoc on every export (convention in this file).

---

### `src/features/hosting/useWorkspaceLiveUpdates.ts` (new hook)

**Analog:** `FleetLiveUpdatesProvider.tsx`.
Imports (L1-7): `useQueryClient, type QueryClient` from `@tanstack/react-query`; `createCoalescer` from
`@/app/updateStream`; `hostingQueryKeys` from `@/features/hosting/services`.
Exact invalidation (L44-47) - copy shape, swap keys; `exact: true` mandatory because `server(id)` is a prefix of
files/configuration/secrets keys (`services.ts` L113-129):
```typescript
function invalidateFleetQueries(queryClient: QueryClient): void {
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true });
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications, exact: true });
}
```
Coalescer lifecycle (L78-83):
```typescript
const signalCoalescer = useMemo(
  () => createCoalescer(() => invalidateFleetQueries(queryClient), signalCoalesceWindowMs),
  [queryClient],
);
useEffect(() => () => signalCoalescer.cancel(), [signalCoalescer]);
```
Reconnect recovery: watch `disconnectEpoch`/`status` from `useFleetLiveUpdates()` and invalidate on return to `open`.

---

### `src/features/hosting/workspaceSections.ts` (new)

**Analog:** `ServerWorkspacePage.tsx` L44-69 (move + replace ids):
```typescript
export const workspaceTabIds: readonly WorkspaceTab[] = [ "overview", "runtime", ... ];
export function workspaceTabFromSearch(value: string | null): WorkspaceTab {
  return workspaceTabIds.includes(value as WorkspaceTab) ? (value as WorkspaceTab) : "overview";
}
```
New ids `overview|activity|settings|files|machine`; label map `as const satisfies Record<WorkspaceSection,
TranslationKey>`; `sectionSearch(section)` returns `""` for overview else `?tab=<id>` (drops `action`). Update
`tests/hosting/parity-matrix.test.ts` L4 import (`@/features/hosting/ServerWorkspacePage` -> new module) or re-export.
Retired ids render Overview; do NOT rewrite URL.

---

### `src/features/hosting/jobProgress.ts`, `activityModel.ts`, `workspaceFreshness.ts` (pure models)

**Style analog:** `workspaceModel.ts` L221-246 (TSDoc above export, pure functions over DTO types from
`@/features/hosting/types`), `serverState.ts` (exhaustive derivation, single owner).
**Merge/sort analog** `workspaceModel.ts` L253-267 - reuse `protocolEntries` for log entries and its time rule:
```typescript
const leftTime = left.command.timeStampCompleted ?? left.command.timeStampStarted ?? left.command.timeStampCreated;
return rightTime.localeCompare(leftTime) || right.command.sequence - left.command.sequence;
```
Active statuses must match API pending set (`GameServerProjectionService.cs` L786-792): `Queued`, `Running`,
`RetryPending`, `WaitingForWorker`. Exhaustive `switch` over `ELifecycleJobKind`/`ELifecycleJobStatus` from
`src/generated/api.ts`; unknown stage -> generic mode, never throw. Never read `progressPercent` for display.
Freshness disconnected rule from `FleetLiveNotice.tsx` L40:
`const disconnected = status === "reconnecting" || status === "unavailable";`
Audit tone for `problem`: reuse `hostingAuditTone` from `HostingAuditFeed.tsx`.
Must not access `deployment.status` (single-owner guard `tests/hosting/server-state-single-owner.test.ts`).

---

### `workspaceModel.ts` (extract `costStopAction`) and `costConfirmationModel.ts` (extract `serverCostFacts`)

Extract from `primaryServerAction` L235-240 so header/failure/progress-retry/Machine share one rule:
```typescript
const stoppers = candidates.filter((action) => actionCostCategory(action) === "stops");
const costStop = stoppers.find((action) => !action.dangerous) ?? stoppers.find((action) => action.dangerous);
```
Extract from `buildCostConfirmationFacts` L85-91:
```typescript
const hourly = server.deployment?.estimatedHourlyCostEur ?? server.estimatedHourlyCostEur ?? null;
monthly: server.estimatedMonthlyCostEur ?? null,
owner: ownerFor(server.costOwnerName),
```
Keep `buildCostConfirmationFacts` calling the new helper (existing tests `cost-confirm-model.test.ts` stay green).

### `useCostConfirmation.ts` L4-5 (union)
```typescript
export type CostConfirmationSource =
  "workspace-primary" | "workspace-action" | "fleet-card" | "notification" | "deep-link";
```
Add `"workspace-header" | "workspace-failure" | "workspace-machine"`; keep `workspace-primary` (used in
`tests/hosting/cost-confirmation-provider.test.tsx:50`).

---

### `ServerWorkspacePage.tsx` (route component, rewrite) and `WorkspaceShell.tsx`

**Imports** L1-42 (path alias `@/`, `useTranslation`, react-router `useParams/useSearchParams/useNavigate`).
**Queries** L600-618 - replace `refetchInterval: 5_000` with function form; change operations `take` 80 -> 200:
```typescript
const jobs = useQuery({
  queryKey: hostingQueryKeys.jobs(serverId),
  queryFn: ({ signal }) => hostingService.jobs(serverId, signal),
  enabled: permissions.accessServers && Boolean(serverId),
  refetchInterval: 5_000,           // -> () => (hasActiveJob ? 30_000 : false)
});
```
**Deep-link `?action=` handling** L621-656 - keep verbatim (Array.find, handledDeepLink ref, replace-strip).
**Refresh** L657-665 - keep, but make `server(id)` invalidation `exact: true`.
**Retry/skip mutation** L666-670 - keep; pass into `JobFailurePanel` and expanded `ActivityEntry`.
**Delete** L671-681, L719-731, L766-781 - move button + `ConfirmDialog` into `SettingsSection`; keep `canDelete` rule.
**Loading/error/not-found boundary** L684-699 - keep (`HostingAccessBoundary`, `HostingLoading`, `HostingError`).
Remove: `PageHeader` usage (L702-734), raw `Error.message` alert (L735-739), `JobList` (L71-179) incl. `<progress>`,
raw `<button>` nav (L382-396), hard-coded "Credentials"/"Terrain"/"Scenario"/"/h"/"/month".
Paused notice source L379-381 (`pausedReason`) moves into shell, copy `Server_Reason_Paused`.

### `WorkspaceHeader.tsx` - primary action analog L417-433:
```tsx
<ActionButton
  variant={confirmVariantFor(primary.id)}
  unavailableReason={actionIsUnavailable(primary) ? (<ActionUnavailableReason server={server} action={primary} />) : undefined}
  onClick={() => request({ serverId: server.primaryKey, actionId: primary.id, source: "workspace-primary" })}
>
  {primary.pending ? t("Server_Action_InProgress") : <ActionLabel action={primary} />}
</ActionButton>
```
Change source to `"workspace-header"`; wrap in `<header>` containing `h1` (Pitfall 10, `workspace-header.test.tsx`
uses `heading.closest("header")`). State via `<ServerStateSummary derived={deriveServerState(server)} />`.

### `sections/MachineSection.tsx` - move `CloudPanel` L181-331 unchanged in behavior; replace `/h` literals
(L249, 253, 323) with `Server_Machine_ResourceHourly`; replace `Hosting_Detail_PossibleCost` panel (L280-282) with
cost notice + `costStopAction` button (`source: "workspace-machine"`); add `ServerRuntimePanel`.

### `sections/OverviewSection.tsx` - facts `dl` from L445-507 (`next-hosting-facts`, drop cost owner/cost rows);
`ServerActions` from L524-530 excluding primary and `collect-logs` (filter at L357-359); recent activity replaces
`HostingAuditFeed` slice L531-548 with 5 newest activity entries + `ActionLink` to `?tab=activity`.

### `sections/SettingsSection.tsx` - `ServerConfigurationPanel` props from L561-567; `ServerSecretsPanel` only when
`server.canManageSecrets` (L373, L568); `SlotPlaceholder data-slot="automation"`; delete panel.

### `sections/ActivitySection.tsx` / `ActivityTimeline` / `ActivityEntry`
- Command detail rendering from `JobList` L139-173 (sorted by `sequence`, `pre tabIndex={0}`, `ApiText` for reasons).
- Download protocol anchor L145-155:
```tsx
<ActionAnchor variant="ghost"
  href={textDownloadUrl(job.commands.map((command) => buildCommandProtocol({ job, command })).join("\n\n---\n\n"))}
  download={`utf-lifecycle-${job.primaryKey}.txt`}>
  {t("React_ServerWorkspace_DownloadProtocol")}
</ActionAnchor>
```
- "Collect logs" + "Download all logs": lift from `ServerLogsPanel.tsx` (same confirmation path and
  `protocolEntries` download; parity rows JOB-logs-*).
- Audit evidence: reuse `HostingAuditEntry` / `parseHostingAuditDetail` exports of `HostingAuditFeed.tsx`.
- Type filter: `FilterChipGroup` (see below); problems toggle = `ActionButton` with `aria-pressed` and
  `className="filter-chip"`.

---

### Shared components `Breadcrumbs.tsx`, `SectionNavigation.tsx`, `CopyButton.tsx`

**Analog:** `src/components/FilterChipGroup.tsx` (generic, TSDoc on props interface and every member, `@typeParam`,
CSS imported from `components/server/*.css`):
```typescript
import { Checkmark16Regular } from "@fluentui/react-icons";
import { ActionButton } from "@/components/ActionControl";
import { currentLocale } from "@/app/format";
import "@/components/server/fleet-toolbar.css";
```
```tsx
<ActionButton key={item.id} variant="ghost" className="filter-chip" aria-pressed={pressed} onClick={() => onChange(item.id)}>
  {pressed && <Checkmark16Regular className="filter-chip__check" aria-hidden="true" />}
```
- `SectionNavigation`: react-router `Link` per item inside `<nav aria-label><ul>`, `aria-current="page"` (copy
  semantics from current nav L382-396 but as links). No `role="tab"`.
- `CopyButton`: `ActionButton variant="ghost" size="compact"`, `Copy20Regular`/`Checkmark16Regular`, polite
  `role="status"` sibling; guard `navigator.clipboard` undefined; failure keeps message until next attempt.
- `Breadcrumbs`: `nav > ol`, `ChevronRight16Regular aria-hidden` separator, last item `aria-current="page"`.

### `LiveBadge.tsx`

**Analog:** `src/components/server/FleetLiveNotice.tsx` - copy the epoch-keyed 3s anti-flicker logic (L39-48):
```typescript
const [shownEpoch, setShownEpoch] = useState<number | null>(null);
const disconnected = status === "reconnecting" || status === "unavailable";
useEffect(() => {
  if (!disconnected) return undefined;
  const timer = window.setTimeout(() => setShownEpoch(epoch), delayMs);
  return () => window.clearTimeout(timer);
}, [disconnected, epoch, delayMs]);
```
Reconnect action: `ActionButton variant="ghost"` calling `reconnectNow` (L62-64).

### `CostStrip.tsx` / `JobProgressStrip.tsx` glyph map

**Analog:** `src/components/server/CostIndicator.tsx` L1-33:
```typescript
const glyphs = {
  live: { name: "Money16Regular", Icon: Money16Regular },
  ...
} as const satisfies Record<ServerCost, { name: string; Icon: ComponentType }>;
```
Decorative icon `<span aria-hidden="true" data-glyph={name}>`; text always visible. CostStrip embeds
`<CostIndicator derived={...} />`, owner via `ownerFor` -> `Server_Confirm_OwnerHidden`/`_OwnerUnknown`, rates via
`formatHourlyRate`/`formatMoney`, null -> `Server_Confirm_CostUnavailable`; accrued row `data-slot="accrued-cost"`.

### `JobFailurePanel.tsx` - retry/skip analog `JobList` L117-138 (`job.canRetry`, `job.canSkip`, `disabled={busy}`);
replace `c.retryJob`/`c.skipJob` (copy.ts) with new JSON keys; cost-stop via `request({ ..., source:
"workspace-failure" })`; details link `ActionLink` to `?tab=activity#activity-job-<pk>`.

## Shared Patterns

### Cost confirmation (all lifecycle buttons)
**Source:** `src/components/server/useCostConfirmation.ts`; usage `ServerWorkspacePage.tsx` L424-430.
**Apply to:** WorkspaceHeader, JobFailurePanel, JobProgressStrip retry mode, MachineSection, Activity "Collect logs".

### Disabled-with-reason
**Source:** `ActionUnavailableReason` + `actionIsUnavailable` (`ServerWorkspacePage.tsx` L9-12, L419-423). Never `title`.

### Server state / cost single owner
**Source:** `deriveServerState` (`src/features/hosting/serverState.ts`), `ServerStateSummary`, `CostIndicator`.
**Apply to:** every new component; no `deployment.status` reads.

### API text
**Source:** `ApiText` (`@/components/server/ApiText`), used L408, L413. Apply to failure reasons, audit summaries,
raw stage tokens, actor-independent API prose.

### Localization
Flat keys `Server_{Area}_{Item}` in `src/locales/en.json` + `de.json`, `_one/_other` plurals; identical values in
`identical-allowed.json`; never `useHostingCopy` (`copy.ts`) for new strings. Gate: `npm run locales:check`.
CST-02: reuse `Server_Action_Stop_Label`; add guard test scanning new key prefixes for `cost-stop`, `deprovision`,
`terminat`, `tear down`.

### Styles
**Source:** `src/components/server/fleet-toolbar.css` (tracked). Tokens only, no hex/px literals, media breakpoints
only `520px`/`800px` (`tests/helpers/cssGuard.ts` L1-2). `server-style-guard.test.ts` auto-scans new css in
`components/server`. Delete `.command-shell .next-hosting-tabs` sticky rule (`commandCenter.css` ~L240-247) and nav
`overflow-x: auto` (`hosting.css` L135-140).

### Query invalidation
Keys from `hostingQueryKeys` (`services.ts` L109-129); `exact: true` on `server(id)` always.

## Testing Patterns

| New test | Analog |
|---|---|
| `tests/hosting/workspace-live-updates.test.tsx`, extend `fleet-live-updates.test.tsx` | `tests/hosting/fleet-live-updates.test.tsx` L1-80 (`FakeWebSocket` from `../helpers/fakeWebSocket`, hoisted `useAuth` mock, `fleetFrame` JSON with `$id`/`type`/`value.serverId`, `Capture` component) |
| `workspace-sections.test.ts`, `job-progress.test.ts`, `activity-model.test.ts`, `workspace-freshness.test.ts` | `tests/hosting/workspace-model.test.ts`, `server-state.test.ts` (pure unit) |
| `job-progress-strip.test.tsx`, `job-failure.test.tsx`, `cost-strip.test.tsx`, `activity-section.test.tsx` | `tests/hosting/workspace-header.test.tsx`, `fleet-toolbar.test.tsx` with `tests/helpers/renderWithProviders.tsx` |
| `tests/breadcrumbs.test.tsx`, `tests/copy-button.test.tsx` | `tests/hosting/fleet-toolbar.test.tsx` (FilterChipGroup usage) |
| `workspace-vocabulary.test.ts` | `tests/locales/server-keys.test.ts` |
| Fixtures | extend `tests/helpers/fleetFixtures.ts` or add `workspaceFixtures.ts` |
| `tests/e2e/hosting-workspace.spec.ts` | `tests/e2e/hosting-fleet.spec.ts`, `hosting-a11y.spec.ts` route mocks, `tests/e2e/support/fleetStream.ts` |
| Update old `?tab=` ids | `tests/e2e/hosting-placement.spec.ts:153`, `workspace-header.test.tsx:137`, `workspace-page.test.tsx:669-674`, `deep-link.test.tsx:289-320` |

## No Analog Found

| File | Role | Reason |
|---|---|---|
| `src/components/Breadcrumbs.tsx` | shared component | No breadcrumb component exists (only `pathBreadcrumbs` data helper in `workspaceModel.ts` L50 for files); use UI-SPEC + MDN `aria-current` |
| `src/components/CopyButton.tsx` | shared component | No clipboard code in ReactApp; use RESEARCH "Copy button" example |
| `src/features/hosting/jobProgress.ts` stage table | model | No stage model exists; use UI-SPEC step tables |
| `src/components/server/SlotPlaceholder.tsx` | component | No reserved-slot precedent; compose `StatePanel`-like panel with `data-slot` |

## Metadata

**Analog search scope:** `sources/ReactApp/src/{components,features/hosting}`, `sources/ReactApp/tests`,
`sources/Api/{DTO,Services/GameServerLifecycle}`, `tests/Api.Tests/GameServerHosting`, `.planning/phases/62-fleet-quick-actions`
**Files scanned:** ~25
**Pattern extraction date:** 2026-11-19

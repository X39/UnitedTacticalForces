# Architecture Research

**Domain:** React server-operations UI redesign (fleet, create, workspace, content/config, cost/teardown) inside an existing Vite/React 19 command-center app
**Researched:** 2026-11-18
**Confidence:** HIGH (every claim below comes from reading the code in `sources/ReactApp`, `sources/Common/Ui/routes.json`, `docs/features/react-ui-beta.md`, and the existing tests; items marked VERIFY are inferred and must be checked in the phase that touches them)

## Bottom Line

The route surface may change when a better design needs it. The three server route patterns (`/command/servers`, `/command/servers/create`, `/command/servers/{ServerId:guid}`) and the `?tab=` / `?action=` parameters are the current shape, not a contract. If routes change, update `sources/Common/Ui/routes.json` and the route tests together.

Keep the domain core (`services.ts`, `types.ts`, `model.ts`, `createServerModel.ts`, `authorization.ts`, the generated contract layer) and rebuild the presentation layer on top of it. Roughly 70 percent of the 9,000+ hosting lines are presentation that is worth rebuilding. The pure logic (validation, storage math, file-path planning, action ranking) is well tested and worth keeping. Add one new domain module, `serverState.ts`, because "unambiguous server state" is currently derived in four different places.

Two integration constraints need an explicit decision in the roadmap before work starts (see "Decisions the Roadmap Must Make"): where new localized strings live, and whether the manifest may ever change.

## Current Structure Assessment

### What exists

```
sources/ReactApp/src
├── app/            App.tsx (681 lines: router, QueryClient, FluentProvider, both route tables)
│                   routes.ts (manifest loader, matchRoute), RouteCertificationGate.tsx
├── components/     CommandCenterLayout (shell), commandNavigation (nav + title contexts),
│                   primitives.tsx (PageHeader, StatePanel, StatusBadge, Metric, ConfirmDialog,
│                   Markdown, useUnsavedChanges), ActionControl (ActionButton/Link/Anchor),
│                   ProfileMenu (polls hosting notifications), commandCenter.css
├── features/hosting/   45 files, ~9.4k TSX/TS lines + 2,428-line hosting.css + 170-line finance.css
├── styles.css      7,413 lines, global, holds the real design tokens (--gold, --surface, --stroke ...)
└── generated/      api.ts (from openapi snapshot), locales/*.json (from WebApp RESX)
```

### Hosting feature, by size and role

| File | Lines | Role | Verdict |
|------|-------|------|---------|
| `CreateServerPage.tsx` | 1115 | One component: 10 `useQuery`, form, storage fit, cost owner, submit | **Rebuild** (keep schema and queries) |
| `ServerFilesPanel.tsx` | 1372 | Tree, table, upload queue, dialogs, preservation, keyboard nav, 4 mutations | **Split then restyle** (logic mostly sound) |
| `ServerWorkspacePage.tsx` | 797 | Page + 9-tab switch + `JobList` + `CloudPanel` + 3 pollers | **Rebuild shell**, extract panels |
| `services.ts` | 758 | API wrappers, normalizers, `hostingQueryKeys` | **Keep**, add hooks beside it |
| `ServerRuntimePanel.tsx` | 427 | Runtime profile + storage + mod pack + terrain + scenario in one panel | **Split** (cost concerns vs content concerns) |
| `ServerConfigurationPanel.tsx` | 392 | Config entry editor + profile selector | **Restyle/regroup**, keep logic |
| `HostingAuditFeed.tsx` | 382 | Audit parsing and feed | **Keep logic**, rehome in an Activity section |
| `model.ts` | 311 | Storage math, config validation, formatters, status keys | **Keep** |
| `workspaceModel.ts` | 283 | Four unrelated concerns (file paths, config entries, primary action, protocol text) | **Split by concern**, keep code |
| `types.ts` | 270 | Generated-schema aliases plus hand-written interfaces | **Keep** |
| `components.tsx` | 247 | `ServerCard`, `ServerStatus`, `ActionConfirmation`, `ServerActions`, access/loading/error | **Rebuild** card and actions, keep boundary helpers |
| `copy.ts` | 246 | Inline EN/DE copy object (`useHostingCopy`) | **Retire** in favor of RESX keys |
| `FleetPages.tsx` | 235 | Fleet page; also an unmounted `HostingOverviewPage` | **Rebuild** fleet; **delete** overview |
| `ServerLogsPanel.tsx` / `ServerSecretsPanel.tsx` | 142 / 123 | Small, self-contained | **Restyle**, move into new sections |
| Finance and configuration-profile pages | ~2.4k combined | Out of redesign scope | **Leave**, keep their CSS working |

### Findings that shape the plan

1. **Routing is two-layered and the hosting `routes.tsx` is a dead duplicate.** The real route table is inline in `app/App.tsx` (`commandRouteDefinitions`, lazy-loaded via `import("@/features/hosting")`). `features/hosting/routes.tsx` (`hostingRouteDefinitions`) is only consumed by `tests/hosting/routes.test.ts`. It lists `/command` as `HostingOverviewPage`, but `/command` is actually mounted as `CommandOverviewPage`. `HostingOverviewPage`/`HostingOverviewView` in `FleetPages.tsx` are therefore unmounted. Delete both and the test that pins them.
2. **The barrel defeats code splitting.** `lazyComponent(() => import("@/features/hosting"), "ServerWorkspacePage")` imports `index.ts`, which re-exports every page (finance, profiles, create) and imports `hosting.css` (2,428 lines) as a side effect. Every hosting route loads all of hosting. When pages move to per-page lazy imports, each page entry must import its own CSS, or styles silently disappear.
3. **`implementedReactRoutePatterns` is the certification tripwire.** `tests/routes.test.ts` asserts every certified manifest pattern appears in the `manifestPattern` strings in `App.tsx`. It also asserts `countRoutePatterns() === 80` and that non-fallback manifest routes equal the set of Blazor `@page` routes, read straight from `sources/WebApp/Pages`. Any new path breaks one of these.
4. **State derivation is duplicated.** Server "state" comes from `serverTone` (components.tsx), `deploymentStatusKey` (model.ts), `primaryServerAction` (workspaceModel.ts), `deploymentIsActive` and `commandAttentionItems` (CommandOverviewPage.tsx), and the numeric-enum normalization in `services.normalizeServer`. This is the root cause of "unambiguous server state" being hard.
5. **Polling only; no push channel for hosting.** `WebSocketContracts` contains only `Eventing/*` messages. React has no WebSocket client. Hosting freshness is five `refetchInterval` timers: fleet 10s, notifications 10s (also in `ProfileMenu`), and server/jobs/operations 5s each on the workspace regardless of server state. Panels add their own queries with the 30s default `staleTime`. Adding hosting push messages is an allowed API change when a feature needs it.
6. **Styling is global CSS, not Fluent components.** `FluentProvider` supplies a theme, but feature code uses native elements plus global classes (`button`, `button--gold`, `field`, `kicker`, `next-hosting-*`). Only `Spinner` and one `Select` (`ArmaPlacementField`) come from `@fluentui/react-components`. Vitest aliases that package to a 22-line mock (`tests/mocks/react-components.tsx`), so any new Fluent widget fails tests until the mock is extended. `hosting.css` hard-codes hex colors (`#2a3442`, `#d1b665`) even though `styles.css :root` defines `--stroke`, `--gold`, and so on.
7. **Shared-component rule is already violated at scale.** The repo has 312 `className="button ..."` raw usages versus 44 `<ActionButton|ActionLink>` usages, 67 of the raw ones in hosting. `ActionButton` emits the same class names, so migration is mechanical. There are no shared `Field`/`Select`/`Tabs`/`Breadcrumbs`/`Panel` components, so each page re-creates them (`next-hosting-panel` versus `command-overview-panel`).
8. **Localization has three mechanisms.** RESX-generated `t("Hosting_…")` keys, the inline `useHostingCopy` EN/DE object, and hard-coded English (`"Credentials"` tab label, `Select terrain`, `Terrain`/`Scenario` labels, validation text in `workspaceModel.ts`). The RESX files live in `sources/WebApp/Properties/Language.resx` and `Language.de.resx`, and `scripts/export-locales.mjs` reads them from there.
9. **Layout shell is admin-oriented and fixed.** `CommandCenterLayout` renders a grouped sidebar and a topbar. Members with only `sv:view` enter this shell because `canAccessCommandCenter` includes `sv`/`sv:view`. The manifest `shell` field is typed in `routes.ts` but not read anywhere; layouts are chosen by the route groups in `App.tsx`. Nav context titles come from `commandNavigation.tsx` (`CurrentArea_CreateServer`, `_ServerFleet`, `_ServerWorkspace`).
10. **Workspace tab state is already URL-based, file path state is not.** Tabs use `?tab=` (`workspaceTabFromSearch`), action deep links use `?action=` (consumed by `ProfileMenu` notification actions and the fleet "Terminate now" link). `ServerFilesPanel` keeps `path` in `useState`, so a mods folder cannot be deep-linked today.
11. **Mobile tab clipping (UI-60-001)** is a CSS problem in `.next-hosting-tabs` (plain `overflow-x: auto`, no affordance), fixed naturally by the new section navigation.
12. **Mod changes today go through exactly two API surfaces:** `PUT /game-servers/{id}/mod-pack` (pick a mod pack definition, via `mod-pack-options`) and file upload to the persistent context (`/files/{context}`). The API does not project a per-game "mods directory" or a "restart required" flag (VERIFY against `GameServerCapabilityDto`/`GameServerDto` before promising either; see Gaps).

## Recommended Architecture

### System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│ App shell (UNCHANGED routes)                                          │
│  App.tsx route tables ── RouteCertificationGate ── CommandCenterLayout │
│  (3 server patterns + manifestPattern strings stay identical)          │
├──────────────────────────────────────────────────────────────────────┤
│ Pages (thin composition, per-page lazy entries)                       │
│  ┌────────────┐  ┌──────────────┐  ┌───────────────────────────────┐  │
│  │ FleetPage  │  │ CreatePage   │  │ WorkspacePage                 │  │
│  │ toolbar,   │  │ quick path + │  │ header + SectionNav + section │  │
│  │ cards      │  │ advanced     │  │ outlet via ?tab=              │  │
│  └─────┬──────┘  └──────┬───────┘  └──────────────┬────────────────┘  │
│        │                │     sections            │                    │
│        │      Overview · Content&Mods · Configuration · Files ·        │
│        │      Activity(jobs+logs+audit) · Cloud&Cost · Access          │
├────────┴────────────────┴─────────────────────────┴──────────────────┤
│ Hosting UI kit (feature-local)                                        │
│  ServerStatusBadge · ServerActionButton · ActionConfirmation(+cost)   │
│  CostSummary · OwnerChip · EndpointCopy · NextActionCard              │
├──────────────────────────────────────────────────────────────────────┤
│ Domain core (KEEP) + new serverState                                  │
│  serverState.ts(NEW)  model.ts  createServerModel.ts  authorization.ts│
│  files/filePaths.ts   config/configurationEntries.ts  (split of       │
│  workspaceModel.ts)                                                   │
├──────────────────────────────────────────────────────────────────────┤
│ Data (KEEP services + keys, ADD hooks)                                │
│  services.ts · hostingQueryKeys · queries.ts(NEW: useFleet,           │
│  useServerWorkspace, adaptive polling, invalidateServer)              │
├──────────────────────────────────────────────────────────────────────┤
│ Shared components (src/components, ADDITIVE)                          │
│  TextField · SelectField · CheckboxField · SectionNav · Breadcrumbs   │
│  Panel · FactList · CopyValue · (existing) ActionButton/Link,         │
│  PageHeader(+breadcrumbs), StatePanel, StatusBadge, ConfirmDialog     │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | New / Modified / Deleted |
|-----------|----------------|--------------------------|
| `features/hosting/serverState.ts` | Single pure function `deriveServerState(server, jobs?)` returning `{ phase, tone, labelKey, owner, costExposure, primaryAction, attention }`. Replaces `serverTone`, `deploymentIsActive`, scattered `deploymentStatusKey` use, and `primaryServerAction` call sites | **New** (absorbs `primaryServerAction` from workspaceModel) |
| `features/hosting/queries.ts` | `useFleet`, `useServerWorkspace(serverId)` (server + jobs + operations), `invalidateServer(client, id)`, adaptive `refetchInterval` | **New** (uses existing `hostingService`/`hostingQueryKeys`) |
| `ServerStatusBadge` | One badge component used by card, header, overview, command overview | **New** (replaces `ServerStatus` in components.tsx) |
| `ServerActionButton` + `ActionConfirmation` | Start/stop/terminate from any surface; confirmation shows cost, owner, and the server-projected challenge | **New** button; `ActionConfirmation` **Modified** (cost and owner lines) |
| `CostSummary` | Hourly/monthly estimate, owner, possible-cost warning; shared by create, start dialog, and Cloud & Cost | **New** (extracts the math currently inlined in CreateServerPage and `CloudPanel`) |
| `fleet/ServerFleetPage` + `FleetToolbar` + `ServerCard` | Search, status filter, "mine" filter, state-first ordering, inline primary action | **Rebuilt** (`orderFleetServers` logic moves into `serverState`) |
| `create/CreateServerPage` (thin) + section components | Quick path (game, name, owner default) plus Advanced disclosure (provider/profile/storage/content); same `ServerCreationFormValues` and zod schema | **Rebuilt**; `createServerModel.ts` **Kept** |
| `workspace/WorkspacePage` + `WorkspaceHeader` + `SectionNav` | Header (title, state, owner, cost, endpoint, primary action) and section outlet | **Rebuilt** |
| `workspace/sections/*` | Overview, ContentMods, Configuration, Files, Activity, CloudCost, Access | Mix: Overview/Activity/CloudCost **New**; others **Split from** existing panels |
| `files/*` | `FileTree`, `FileTable`, `UploadDialog`, `useUploadQueue`, `PreservationPanel`; path lifted to `?path=` | **Split** from `ServerFilesPanel` |
| `src/components/fields/*`, `SectionNav`, `Breadcrumbs`, `Panel`, `FactList`, `CopyValue` | Shared primitives, tokens-only CSS | **New** |
| `components/primitives.tsx` `PageHeader` | Optional `breadcrumbs` and `status` slots (additive) | **Modified** |
| `commandNavigation.tsx` | Add context title for sub-sections if wanted; no new nav items | **Modified (minor)** |
| `FleetPages.tsx` `HostingOverviewPage`, `hosting/routes.tsx`, `tests/hosting/routes.test.ts` | Dead duplicate route contract | **Deleted** |
| `copy.ts` (`useHostingCopy`) | Inline copy | **Deleted** after migration |

### Recommended Project Structure

```
src/components/                      # shared, additive, tokens-only CSS
├── fields/                          # TextField, SelectField, CheckboxField (label, hint, error wiring)
├── SectionNav.tsx (+css)            # role=tablist, scroll fade, URL-driven; fixes UI-60-001
├── Breadcrumbs.tsx
├── Panel.tsx · FactList.tsx · CopyValue.tsx
└── primitives.tsx                   # PageHeader gains breadcrumbs/status slots

src/features/hosting/
├── services.ts types.ts model.ts createServerModel.ts authorization.ts   # KEEP in place (tests import them)
├── serverState.ts                   # NEW domain module
├── queries.ts                       # NEW hooks
├── ui/                              # ServerStatusBadge, ServerActionButton, ActionConfirmation, CostSummary, NextActionCard
├── fleet/                           # ServerFleetPage, FleetToolbar, ServerCard
├── create/                          # CreateServerPage + sections
├── workspace/                       # WorkspacePage, WorkspaceHeader, sections/
├── files/                           # split ServerFilesPanel + filePaths.ts
├── configuration/                   # split ServerConfigurationPanel + configurationEntries.ts
├── (finance, configuration-profile files untouched)
└── hosting.css -> shrinks to what finance/profile pages still use; new CSS colocated per folder
```

Rationale: the stable domain core stays at the feature root because roughly 20 test files import it by path. New UI goes into subfolders with colocated CSS so each page entry imports exactly its own styles. Existing `.tsx` file names keep the repo's PascalCase component and camelCase module conventions.

## Architectural Patterns

### Pattern 1: Redesign inside the current URL shape unless a change is better

**What:** Use the three pathnames and two query parameters by default; change them freely when that is the better design. Old tab ids need no mapping.
**When to use:** Default; deviate when it improves the design.
**Trade-offs:** Changing routes means updating the manifest and route tests.

```typescript
// workspace/sections.ts
export type SectionId = "overview" | "content" | "configuration" | "files" | "activity" | "cloud" | "access";
const legacyTab: Record<string, SectionId> = {
  runtime: "cloud", cloud: "cloud", logs: "activity", jobs: "activity", audit: "activity",
  files: "files", configuration: "configuration", credentials: "access", overview: "overview",
};
export function sectionFromSearch(value: string | null): SectionId {
  if (value && value in legacyTab) return legacyTab[value]!;
  return (["overview","content","configuration","files","activity","cloud","access"] as const)
    .find((id) => id === value) ?? "overview";
}
```

Contract that must keep working (all are referenced today): `?action=<id>` opens that action's confirmation (`ProfileMenu`, fleet card terminate); `?tab=` legacy ids from bookmarks and e2e; fleet card link target `/command/servers/{id}`; `startsWith("/command/servers")` active state in `commandNavigation`.

### Pattern 2: One derived state object per server

**What:** Every surface renders from `deriveServerState`, never from raw `deployment.status` strings.
**When to use:** Fleet card, workspace header, overview, command overview, `ProfileMenu` notifications.
**Trade-offs:** One more module to test, but it is pure and cheap, and it removes the inconsistent tone and label logic.

```typescript
export type ServerPhase = "offline" | "starting" | "running" | "stopping" | "attention" | "terminating" | "archived";
export interface DerivedServerState {
  phase: ServerPhase;
  tone: "neutral" | "success" | "warning" | "danger" | "info";
  labelKey: TranslationKey;
  costExposure: "none" | "accruing" | "unknown";   // from deployment.possibleCost
  primaryAction: ServerAction | undefined;          // today's primaryServerAction
  isTransitional: boolean;                          // drives polling interval
}
```

Input quirk to preserve: `normalizeServer` converts numeric enum statuses to names. Keep that in `services.ts`; `serverState` consumes the normalized form.

### Pattern 3: Adaptive polling through hooks, not per-page `refetchInterval`

**What:** One `useServerWorkspace` hook; interval is a function of derived state.
**When to use:** Workspace and fleet. Members mostly look at disabled servers, so a fixed 5s triple poll is wasteful.
**Trade-offs:** Slightly more logic; TanStack's function-form `refetchInterval` makes it small. Background tabs already pause (`refetchIntervalInBackground` defaults false).

```typescript
const pollMs = (s?: GameServer) => (!s ? 5_000 : deriveServerState(s).isTransitional ? 3_000 : 15_000);
export function useServerWorkspace(serverId: string, enabled: boolean) {
  const server = useQuery({ queryKey: hostingQueryKeys.server(serverId), enabled,
    queryFn: ({ signal }) => hostingService.server(serverId, signal),
    refetchInterval: (q) => pollMs(q.state.data ?? undefined) });
  const jobs = useQuery({ queryKey: hostingQueryKeys.jobs(serverId), enabled,
    queryFn: ({ signal }) => hostingService.jobs(serverId, signal),
    refetchInterval: () => pollMs(server.data ?? undefined) });
  /* operations likewise; fetch lazily when Activity section or Overview feed is shown */
  return { server, jobs /* , operations */ };
}
```

`invalidateServer` must keep the current narrow invalidation (fleet, server, jobs, operations, notifications). Invalidating the whole `["hosting","servers",id]` prefix would also refetch every cached file listing after each action.

### Pattern 4: Strangler migration of the workspace

**What:** Build the new header and `SectionNav` first and mount the old panels (`ServerFilesPanel`, `ServerConfigurationPanel`, `ServerSecretsPanel`) inside the new sections unchanged. Replace them one at a time.
**When to use:** The workspace, because the existing 648-line `workspace-page.test.tsx` and the e2e journey must stay green between steps.
**Trade-offs:** Temporary visual mix of old and new panels, accepted in exchange for shippable increments.

### Pattern 5: Shared primitives are additive and token-based

**What:** New `src/components/*` take only CSS custom properties (`--surface`, `--stroke`, `--gold`, `--space-*`), never hex. `ActionButton`'s class output stays untouched.
**When to use:** Every new shared piece; this is also the basis of the "light consistency pass" on non-server pages.
**Trade-offs:** `PageHeader` is used app-wide, so its changes must be optional props with unchanged default markup, or every page snapshot shifts.

## Data Flow

### Request Flow (start a server, the primary member journey)

```
Fleet card / header "Start"
   │  ServerActionButton(action from deriveServerState.primaryAction)
   ▼
ActionConfirmation (cost, owner, server-projected challenge text)
   │  confirm
   ▼
hostingService.executeAction(serverId, action.id, challenge)   POST /game-servers/{id}/actions/{id}
   │  onSuccess
   ▼
invalidateServer(fleet, server, jobs, operations, notifications)
   ▼
adaptive poll (3s while transitional) ──► deriveServerState ──► badge/phase updates everywhere
```

The challenge string is whatever the server projects in `action.confirmationChallenge`. Never synthesize it client-side (the e2e at `public-shell.spec.ts` asserts the posted `{ confirmation: title }`).

### State Management

```
Server state  : TanStack Query (hostingQueryKeys), the only source of truth
URL state     : ?tab (section), ?action (open confirmation), ?path (files, NEW)
Form state    : react-hook-form + zod (create flow only; schema unchanged)
Local UI state: dialogs, upload queue (moves into useUploadQueue)
Derived state : deriveServerState (pure)
```

### Key Data Flows

1. **Create:** `useQuery(capabilities)` drives the game picker. The quick path needs game and name only, with provider, profile, and storage preselected from capability defaults (the existing `changeGame`/`changeProvider` logic already selects first available provider and `AutomaticExternal` when supported). `CostSummary` reads `combinedHourlyCost`. Submit posts the unchanged `ServerCreation` payload, then navigates to `/command/servers/{id}`.
2. **Change a mod (Minecraft):** `ContentMods` section reads `modPackOptions`, submits `changeModPack`. For loose mod files it links to `?tab=files&path=<dir>`, which requires lifting the files `path` into the URL. The mods directory itself comes from the game; see the Gaps entry on the missing API projection.
3. **Cost-stop:** Cloud & Cost surfaces the existing cost-stop action (`terminate`, `dangerous`, `costImpact` containing `stopscost`) as the explicit, labelled "stop paying" control. `ProfileMenu` and fleet cards deep-link to it via `?action=`.
4. **Polling:** fleet interval 10s while any server is transitional or `possibleCost`, otherwise 30s. Workspace as in Pattern 3. `ProfileMenu` notification polling is unchanged.

## Route Manifest and Certification Strategy

### Rules for this milestone

| Rule | Why | Enforcement |
|------|-----|-------------|
| `sources/Common/Ui/routes.json` is updated with any route change | It is outside `sources/ReactApp` (milestone constraint) and is linked into `WebApp.csproj` as `wwwroot/ui-routes.json`, so any edit changes the classic build artifact | Phase gate: `git diff --exit-code -- sources/Common/Ui/routes.json` |
| New pathnames are allowed | Each new path needs a manifest row with `classicFallback`, bumps `countRoutePatterns()` from 80, and touches the Blazor-equality test | Use `?tab=`/`?path=` query params; `matchRoute` only looks at the pathname so queries never affect certification |
| Keep `manifestPattern` literals in `App.tsx` in sync with the manifest | `routes.test.ts` requires every certified pattern to be present in `implementedReactRoutePatterns` | Existing test; do not weaken it |
| Keep exported lazy names `ServerFleetPage`, `CreateServerPage`, `ServerWorkspacePage` | `App.tsx` resolves them by export name via `lazyComponent(..., "Name")` | Keep these exports in whatever module `App.tsx` imports |
| Do not flip any `certified` flag | All server routes are already `true`; redesign must keep reads, mutations, auth, polling, loading/empty/error states, i18n, and a11y at parity with `docs/features/react-ui-beta.md` | Per-phase parity checklist (below) |

### If a deep link ever becomes unavoidable

Add a manifest row with `"certified": true`, `"classicFallback": "/command/servers/{ServerId:guid}"`, `"shell": "command"`, add the pattern to `commandRouteDefinitions`, update the `80` count and add a `matchRoute` assertion, and update `docs/schemas/ui-route-manifest.schema.json`. That schema currently omits `classicFallback` while declaring `additionalProperties: false`, so it already disagrees with the manifest; nothing validates it today. This path breaks the "only ReactApp" constraint and needs an explicit decision. Recommendation: do not take it.

### Per-phase certification parity checklist

- Reads: fleet, workspace, jobs, operations, capabilities, files still load for `sv:view`-only members.
- Mutations: start/stop/terminate with challenge, retry/skip job, delete (archive), runtime/storage/mod-pack/content changes, config update, secret set/rotate/reveal, file upload/delete/mkdir, preservation selections, create.
- Authorization: `getHostingPermissions`, `server.canModify`, `server.canManageSecrets`, `HostingAccessBoundary` behavior unchanged.
- Aliases and links: `?action=`, legacy `?tab=` ids, `ProfileMenu` links, fleet "Terminate now".
- Polling, loading, empty, error states present for every new surface.
- Localization: EN and DE strings for every new label; CI `locales:check` clean.
- Accessibility: tablist semantics and keyboard nav in `SectionNav`, native `<dialog>` focus handling retained, no raw `<input>` or `<button>` posing as a control where a shared component exists.
- Cross-variant: `currentClassicUrl()` keeps path and query when switching to classic; do not put non-idempotent state in the query.

## Decisions the Roadmap Must Make

1. **RESX is in `sources/WebApp/Properties/` (needs a ruling).** `export-locales.mjs` reads `Language.resx` and `Language.de.resx` from the classic app, and existing `React_*` keys were added there. New localized strings for the redesign must go somewhere. Options: (a) additive RESX keys in `sources/WebApp/Properties` (precedent; no classic behavior change; regenerates `Language.Designer.cs`, which the repo treats as generated); (b) a React-owned overlay file merged by `export-locales.mjs` (touches only ReactApp, but adds a second source of truth and conflicts with "RESX-driven"). **Recommendation: (a), recorded as "no classic UI code or behavior change; additive resource keys allowed."** If the user rejects (a), (b) is the fallback; do not keep growing `copy.ts` or inline English.
2. **Delete the dead hosting overview and `hosting/routes.tsx`.** Recommended; they mislead and carry a stale route contract.
3. **No Fluent widget adoption.** Continue native elements plus tokens. Adopting Fluent `Tabs`/`Dialog` means extending the vitest mock and re-theming with no gain.
4. **Shell stays `CommandCenterLayout`.** Members reach servers inside the command shell today; moving them to the community shell requires a manifest `shell` change and layout rework. Instead make the command shell friendlier for members through breadcrumbs, state-first fleet, and a clear back-to-site link (already present).

## Build Order (dependencies considered)

```
A Foundations ──► B Fleet + quick start/stop ──┬─► C Workspace shell + Overview/Activity/Cloud&Cost ──► D Content/Mods, Files, Config
                                               └─► E Create flow (parallelizable with C)
                                       C,D,E ──► F Consistency pass + cleanup + cert re-verify
```

| Phase | Scope | Depends on | Why this position |
|-------|-------|-----------|-------------------|
| **A. Foundations** | Decisions 1-4; shared components (`fields/*`, `SectionNav`, `Breadcrumbs`, `Panel`, `FactList`, `CopyValue`); `PageHeader` additive props; `serverState.ts` plus unit tests; `queries.ts` hooks; `ServerStatusBadge`, `CostSummary`; ESLint `no-restricted-syntax` rule banning `<button className="button …">` and raw-input-as-button; delete dead overview/routes | none | Everything else imports these. No visible change, so low regression risk. The lint rule stops new violations while old ones are migrated file by file |
| **B. Fleet + quick actions** | State-first fleet with search/filter/"mine", inline `ServerActionButton` (start/stop/terminate), upgraded `ActionConfirmation` with cost and owner | A | Smallest slice that proves the kit and the polling hooks end to end. Delivers "start a server from the list" early. Replaces `ServerCard`, `orderFleetServers` (tests: `fleet.test.tsx`, `actions.test.tsx`) |
| **C. Workspace shell** | `WorkspaceHeader`, `SectionNav` with legacy-tab mapping, Overview (next action, state, owner, cost, endpoint), Activity (jobs plus logs plus audit merged), Cloud & Cost (runtime profile/storage from `ServerRuntimePanel`, `CloudPanel`, teardown). Old Files/Config/Credentials panels mounted unchanged (strangler) | A, B | Largest user-visible win; fixes UI-60-001. Needs `serverState`, hooks, and `ActionConfirmation` from B |
| **E. Create flow** | Quick path plus Advanced; section components; same schema and payload | A (fields, `CostSummary`), B (status/cost bits); independent of C | Can run in parallel with C after B. Post-create navigation lands in either old or new workspace |
| **D. Content/Mods, Files, Config** | Split `ServerRuntimePanel` content half into `ContentMods`; split `ServerFilesPanel` (extract `useUploadQueue`, tree, table, dialogs; lift `?path=`); regroup `ServerConfigurationPanel`; localize hard-coded validation strings | C (section mount points), A | Highest-risk files; do after the shell is stable so each split ships behind the existing section |
| **F. Consistency and cleanup** | Apply `PageHeader`/`Panel`/`Breadcrumbs` to non-server pages (light touch); migrate remaining raw buttons; shrink `hosting.css` and remove unreferenced `next-hosting-*` selectors (grep before deleting; finance and profile pages share many); retire `copy.ts`; full e2e; manifest byte-diff gate | C, D, E | Last, because shared-component changes ripple app-wide. Per-page lazy imports with explicit CSS imports land here at the latest (earlier if bundle size matters) |

### Test and verification impact

| Existing test | Impact |
|---------------|--------|
| `tests/routes.test.ts` (80-pattern count, Blazor equality, implemented-pattern check) | Must stay green untouched; this is the certification gate |
| `tests/hosting/routes.test.ts` | Deleted with `hosting/routes.tsx` |
| `tests/hosting/fleet.test.tsx`, `actions.test.tsx` | Rewritten in B |
| `tests/hosting/workspace-page.test.tsx` (648 lines, imports `ServerWorkspaceView`, three panels) | Kept passing through the strangler in C, split alongside each panel in D |
| `tests/hosting/create-server-page.test.tsx`, `create-server.test.ts` | `create-server.test.ts` (schema/model) unchanged; page test rewritten in E |
| `tests/hosting/workspace-model.test.ts` | Follows the `workspaceModel.ts` split; imports change, assertions do not |
| `tests/e2e/public-shell.spec.ts` (server workspace and terminate challenge), `hosting-placement.spec.ts` | Run each phase; they exercise `?action=` and the challenge post |
| Vitest Fluent mock | Do not add Fluent imports; if one is added, extend `tests/mocks/react-components.tsx` |

## Scaling Considerations

| Concern | Today (tens of servers, few concurrent members) | If the fleet reaches hundreds |
|---------|-----------------------------------------------|-------------------------------|
| Fleet list | `GET /game-servers` returns everything; client-side filter and sort are fine | Add server-side paging or filtering (API change, allowed if needed); virtualize the card grid |
| Polling load | Adaptive intervals keep idle servers at 15-30s; a transitional workspace polls three endpoints every 3s | Share one fleet poll and derive per-server status from it for non-focused views |
| Bundle | Barrel loads all of hosting for every route | Per-page lazy entries (phase F) |
| Files panel | Per-directory queries plus `useQueries` tree expansion | Keep lazy expansion; avoid prefix invalidation of file queries |

First bottleneck: the polling triple on a single workspace. Second: the barrel-sized hosting chunk.

## Anti-Patterns

### Anti-Pattern 1: Adding nested routes for sections

**What people do:** Turn tabs into `/command/servers/:id/files` style child routes.
**Why it's wrong:** Every child path needs a manifest row, a classic fallback, a new pattern count, and edits to a file outside `sources/ReactApp`. It also risks the Blazor route-equality test.
**Do this instead:** Query parameters (`?tab=`, `?path=`), with legacy tab ids mapped.

### Anti-Pattern 2: Rebuilding on Fluent UI components

**What people do:** Adopt Fluent `Tabs`/`Dialog`/`Field` because the package is installed.
**Why it's wrong:** Feature code is native elements plus global CSS; tests alias Fluent to a mock; mixing two systems worsens the inconsistency this milestone is meant to fix.
**Do this instead:** Native, accessible shared components under `src/components` using the `:root` tokens.

### Anti-Pattern 3: Computing state labels at each call site

**What people do:** `server.deployment?.status === "Running"` in a card, again in the header, again on the overview.
**Why it's wrong:** These already disagree today (`serverTone` versus `deploymentIsActive` versus `primaryServerAction`).
**Do this instead:** `deriveServerState` only, with exhaustive unit tests over all 12 deployment statuses.

### Anti-Pattern 4: Big-bang replacement of the workspace

**What people do:** Delete the nine-tab page and rewrite it in one change.
**Why it's wrong:** It breaks the 648-line page test and the terminate e2e at once, with no incremental certification evidence.
**Do this instead:** Strangler mounting (Pattern 4).

### Anti-Pattern 5: Deleting `next-hosting-*` CSS wholesale

**What people do:** Remove `hosting.css` once the server pages are rebuilt.
**Why it's wrong:** `FinanceProtocol`, configuration-profile pages, and `HostingAuditFeed` still use many of its 445 selectors, and the barrel currently injects it for all of them.
**Do this instead:** Grep per selector before deletion, split shared rules into a small `hosting-shared.css`, and import CSS per page entry.

### Anti-Pattern 6: Hard-coding English or growing `useHostingCopy`

**What people do:** Add `"Credentials"`-style literals or more keys to `copy.ts`.
**Why it's wrong:** Violates the RESX/EN+DE constraint and splits translation across three mechanisms.
**Do this instead:** RESX keys per Decision 1, regenerate with `npm run locales:generate`, and commit generated output (CI fails on drift).

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| API `game-servers/*` | `hostingService` over `getApiClient()` with generated path types; unchanged | No API edits. Projection gaps are decisions, not assumptions |
| OpenAPI snapshot | `openapi/utf-api.json` → `src/generated/api.ts` | Do not regenerate unless the API changes; CI byte-compares |
| Hosting notifications | `ProfileMenu` poll plus `?action=` deep links | Contract to preserve |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Page ↔ hooks (`queries.ts`) | Hook return values only | Pages never call `useQuery` for server data directly |
| UI ↔ `serverState` | Pure function import | Single source for tone, label, primary action, polling cadence |
| `features/hosting` ↔ `src/components` | Shared components imported; shared components never import hosting | Prevents the barrel/cycle problem |
| `App.tsx` ↔ hosting | Lazy import by export name only | The route table and manifest patterns stay in `App.tsx` |
| `ProfileMenu`/`CommandOverviewPage` ↔ hosting | `getHostingPermissions`, `hostingQueryKeys`, `hostingService`, (new) `deriveServerState` | `CommandOverviewPage` should adopt `deriveServerState` in phase F |
| React ↔ classic app | Shared manifest (read-only) plus `utf_ui_variant` cookie | Untouched |

## Gaps to Address

- **API projection for mods and restart-needed:** whether `GameServerCapabilityDto`/`GameServerDto` expose a per-game mods directory or an "applies on next start" indicator is VERIFY in phase D planning. If absent, the UI must say "applies to the next deployment" (as `ServerRuntimePanel` already does for the profile) and not imply live apply. Adding a projection is an allowed API change; no separate decision is needed.
- **Minecraft mod source:** `modInstallerIdentifiers` and `modPackOptions` exist; confirm what a Minecraft "mod" maps to (mod pack definition versus uploaded jar) before designing the Content & Mods section.
- **Files `?path=` lifting:** assumed feasible because `path` is a single `useState`; confirm no other state couples to it (`expandedPaths`, `selection`) in phase D.
- **Manifest schema drift:** `docs/schemas/ui-route-manifest.schema.json` omits `classicFallback`. Only relevant if the manifest ever changes.
- **Playwright journeys for create and mod change:** only terminate and placement are covered end to end; add journeys for "start" and "add mod" as part of B and D.

## Sources

- `sources/ReactApp/src/app/App.tsx`, `routes.ts`, `RouteCertificationGate.tsx` (route tables, manifest loader, gate) - HIGH
- `sources/Common/Ui/routes.json`, `docs/schemas/ui-route-manifest.schema.json`, `sources/WebApp/WebApp.csproj` (manifest consumers) - HIGH
- `docs/features/react-ui-beta.md` (release boundary, parity checklist, generation and CI rules) - HIGH
- `sources/ReactApp/src/features/hosting/**` (all page, panel, model, service, CSS files read or outlined) - HIGH
- `sources/ReactApp/src/components/**` (`CommandCenterLayout`, `commandNavigation`, `primitives`, `ActionControl`, `ProfileMenu`) - HIGH
- `sources/ReactApp/tests/routes.test.ts`, `tests/hosting/*`, `tests/e2e/public-shell.spec.ts`, `vitest.config.ts`, `tests/mocks/react-components.tsx` (certification tripwires, test coupling) - HIGH
- `sources/ReactApp/scripts/export-locales.mjs` (RESX source location) - HIGH
- `sources/WebSocketContracts` (only event messages exist) - HIGH
- `.planning/PROJECT.md`, `.planning/codebase/STRUCTURE.md`, `.planning/milestones/v1.12-MILESTONE-AUDIT.md` (UI-60-001) - HIGH

---
*Architecture research for: React server UI redesign (milestone v2.1)*
*Researched: 2026-11-18*

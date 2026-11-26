# Phase 63: Workspace Shell & Activity - Research

**Researched:** 2026-11-19
**Domain:** React 19 + React Router 7 + TanStack Query 5 workspace shell refactor (`sources/ReactApp`), live
updates over the existing Phase 62 fleet WebSocket signal, client-side merged activity model
**Confidence:** HIGH for codebase facts and integration points; MEDIUM for UX recommendations (stage model,
timeline dedup, sticky layout); one blocking open question (timeline actor data is not in the API DTOs)

## Summary

Phase 63 is a frontend restructuring of one 787-line page (`ServerWorkspacePage.tsx`) into a shell (breadcrumbs,
sticky compact bar, scrolling details block with cost strip and endpoint copy, section navigation) plus five section
components (Overview, Activity, Settings, Files, Machine). Almost every building block exists: `deriveServerState`,
`ServerStateSummary`, `CostIndicator`, `useCostConfirmation`, `ActionUnavailableReason`, `ownerFor`,
`formatHourlyRate`, `primaryServerAction`, `protocolEntries`/`buildCommandProtocol`/`textDownloadUrl`,
`HostingAuditFeed` evidence rendering, and the `FleetLiveUpdatesProvider` socket. No new npm package is needed.

Three findings shape the plan. First, the Phase 62 fleet signal only fires for committed changes to a server's
title/archive/launch/cost-owner, a deployment's status/possible-cost, and a job's add/status. Job stage, worker
command progress, diagnostic output and audit records do not signal on their own, which is exactly why D-07 keeps a
roughly 30 s poll while a job is active; the progress strip is therefore at most about 30 s behind on stage changes and
the Live badge must not claim more than that. Second, the provider currently invalidates only the fleet and
notification keys with `exact: true` and exposes no frame hook, so the workspace needs a small, additive provider API
(listener registry) instead of a second socket. Third, the API's `LifecycleJobDto` and `HostingOperationDto` carry no
actor, although the database has `LifecycleJob.RequestedByFk` and `HostingAuditRecord.ActorFk`. D-10 ("each entry
reads as a plain-language sentence with actor and time") cannot be met from the React app alone; it needs either a
small additive API projection (precedent: Phase 62-02 `CostOwnerUserId`) or a user-approved fallback.

**Primary recommendation:** Split the page into `WorkspaceShell` + five section components driven by a pure,
unit-tested model layer (`workspaceSections.ts`, `jobProgress.ts`, `activityModel.ts`, `workspaceFreshness.ts`); add a
listener API to `FleetLiveUpdatesProvider` and a `useWorkspaceLiveUpdates(serverId)` hook that invalidates exactly
`server(id)`, `jobs(id)` and `operations(id)`; replace the three 5 s pollers with
`refetchInterval: hasActiveJob ? 30_000 : false`; build section navigation and breadcrumbs as shared link-based
components in `src/components/`; resolve the actor question with the user before planning the timeline sentence keys.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Section structure (WSP-02, WSP-03)
- **D-01:** Regroup the nine current tabs (overview, runtime, cloud, logs, files, configuration, credentials, jobs,
  audit) into **five sections**: **Overview**, **Activity** (jobs + logs + audit merged), **Settings** (configuration +
  credentials), **Files**, **Machine** (runtime + cloud). Credentials content stays visible only to members who may
  manage secrets. Every capability keeps a home; update the Phase 61 parity matrix (rows `kept`/`relocated`, never
  deleted) for each moved tab. The user first chose "6 sections", then agreed to 5 because only five names were listed.
- **D-02:** **No backward compatibility for old `?tab=` values** (user decision). This amends WSP-02 and roadmap
  success criterion 2, which say old `?tab=` links still resolve. Section selection stays in `?tab=` with the new ids;
  unknown or retired values fall back to Overview with no mapping table. The requirement text must be amended at plan
  time to match.
- **D-03:** Section navigation must not clip at 320px or 390px (resolves UI-60-001). Exact mechanism (scrollable strip,
  wrap, or collapse) is Claude's discretion; all five sections must stay reachable and keyboard operable.

#### Header and cost strip (WSP-01, CST-01)
- **D-04:** **Compact sticky header.** A slim sticky bar (server name, state badge, primary action) is always visible in
  every section. The full block (owner, cost strip, endpoint with copy) scrolls away, and the compact bar remains. The
  primary action uses `useCostConfirmation().request({ serverId, actionId, source: "workspace-header" })`.
- **D-05:** The cost strip shows cost owner, hourly and monthly rate, and whether the server is costing now (via
  `CostIndicator` and `deriveServerState`; a server that may still cost is never labelled off).
- **D-06:** The reserved accrued-cost slot is **visible** before Phase 66 and shows a muted "not available yet" line
  (never a fake zero or invented figure). Phase 66 replaces the text with the real value.

#### Live updates, progress, failure (WSP-04, WSP-05)
- **D-07:** **Reuse the Phase 62 WebSocket client** for the workspace: change signals invalidate the server, jobs, and
  audit queries. Keep a **slow fallback poll (about 30s) only while a job is active**; the fleet's no-polling rule is
  not extended to the workspace. Replace the current 5s intervals in `ServerWorkspacePage`.
- **D-08:** A progress strip shows the current stage of a long lifecycle job. Stage model, stage naming, and elapsed-time
  display are Claude's discretion; derive stages from job kind/status and worker command data already in the API, and
  never show an invented percentage.
- **D-09:** A failed job shows what failed, whether the server may still incur cost, and the next step, with inline
  actions: **Retry / skip** (existing job actions, plain wording, only when the API allows), **Shut down and stop
  billing** (through the shared cost confirmation, prominent when the server may still cost), and an **Open activity
  details** link to the failing entry in the Activity timeline.

#### Activity timeline (ACT-01, ACT-02)
- **D-10:** One timeline in the Activity section, **newest first**, with a **type filter** (All, Jobs, Logs, Audit) with
  counts and a "problems only" toggle. Each entry reads as a plain-language sentence with actor and time.
- **D-11:** Log freshness uses a **Live badge** driven by the socket connection state plus last-update time, with a
  stale state when the socket is disconnected or data is old. Logs stay available through the timeline.
- **D-12:** Cost-stop wording is one vocabulary everywhere: "Shut down and stop billing" (extends Phase 61 D-03; do not
  rename). CST-02 is met by using this in the header, failure panel, timeline wording, and Machine section.

### Claude's Discretion
- 320/390px section-navigation mechanism (D-03).
- Progress strip stages, naming, and elapsed-time presentation (D-08).
- How raw command output and diagnostics are reached from the timeline (expand in place vs. dedicated raw-log view);
  must keep the existing copy and download controls.
- Breadcrumb form and placement (Servers > Name > Section) and how the Overview metrics and automation areas are
  reserved and what they show while empty (not discussed; follow Phase 61 UI-SPEC and the "unavailable, never fake"
  principle).

### Deferred Ideas (OUT OF SCOPE)
- Grouping timeline entries under their lifecycle job: not chosen; revisit only if the flat timeline proves hard to diagnose with.
- Breadcrumb and empty-slot visuals were not discussed; left to UI-SPEC and planning.

### Scope note (CONTEXT.md domain section)
Scope is `sources/ReactApp` only; `sources/Common/Ui/routes.json` stays byte-identical (in-page state uses search
params). Does not build create (64), change flows (65), accrued cost (66), metrics (67), or automation (68).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| WSP-01 | Persistent server header shows status, owner, cost strip, connection endpoint with copy, and the primary action | Pattern 2 (sticky compact bar + details block), `ownerFor`, `CopyButton` (Don't Hand-Roll), `primaryServerAction`, `source: "workspace-header"` union change (Pitfall 6) |
| WSP-02 | Deep-linkable section navigation (**amend**: old `?tab=` links fall back to Overview per D-02) | Pattern 1 (section registry, link-based nav), Pitfall 3 (tests carrying old ids), requirement amendment task |
| WSP-03 | Section navigation does not clip on mobile (UI-60-001) | Pattern 1 wrap layout, e2e viewport assertions at 320x568 and 390x844 (Validation Architecture) |
| WSP-04 | Progress strip with current stage during long lifecycle jobs | Pattern 4 (`deriveJobProgress`), verified job stage literals and status enums, no percentage (Pitfall 5) |
| WSP-05 | Failed job shows what failed, whether it may still cost, next step | Pattern 5 (failure panel model), `canRetry`/`canSkip` rules, `RetryPending` for Terminate (Pitfall 8) |
| WSP-06 | Breadcrumbs fleet > server > section | Pattern 1 `Breadcrumbs` shared component, `aria-current="page"` (MDN), relocates FLT-back-to-fleet |
| CST-01 | Cost strip: owner, hourly and monthly rate, costing now | Pattern 2 cost facts reuse `buildCostConfirmationFacts` hourly rule; D-06 accrued slot |
| CST-02 | Cost-stop in plain words, one vocabulary | `Server_Action_Stop_Label` reuse; vocabulary guard test; replace "cost-stop" wording in Machine section keys (Pitfall 9) |
| ACT-01 | One timeline merging jobs, logs, audit in readable language | Pattern 6 (`buildActivityEntries`), dedup of audit echoes, window alignment (Pitfall 7), **actor gap (Open Question 1)** |
| ACT-02 | Logs available with a visible freshness indicator | Pattern 3 live updates + `deriveFreshness`, keeps copy/download (JOB-logs-* parity rows) |
</phase_requirements>

## Project Constraints (from AGENTS.md; no CLAUDE.md exists)

- English for identifiers, comments, technical docs; localized user text in resource files (React: `src/locales/en.json`
  and `de.json`, flat keys, EN/DE parity gate `npm run locales:check`).
- Enum naming (`E` prefix) and XML-doc rules apply to C#; TypeScript in this repo uses TSDoc on exported functions
  (observed convention in `serverState.ts`, `FleetLiveUpdatesProvider.tsx`).
- One top-level type per file applies to C#; React files follow "one component or model per file" in practice.
- Generated code (`src/generated/api.ts`, Kiota under `sources/WebApp/UtfApi`) is never hand-edited; regenerate via
  `npm run generate` after an OpenAPI snapshot export.
- Architecture: do not broaden the API, shared contracts or core infrastructure for convenience; a cross-cutting change
  needs an explicit user request. (Relevant to Open Question 1.)
- Cross-Phase Constraints (ROADMAP): React only; `routes.json` byte-identical (`git diff --exit-code --
  sources/Common/Ui/routes.json`); shared components under `src/components`; no raw `<input>` or `<button>` posing as
  a control; every new string in EN and DE JSON; parity matrix updated; keyboard operation and 320 px usability.
- Commit format `type(scope): summary` under 80 chars, e.g. `feat(webapp): ...`; commit each cohesive unit.
- Style guard: no raw hex, colour functions or pixel literals in `src/components/server/*.css`; media breakpoints only
  `520px` and `800px` [VERIFIED: tests/helpers/cssGuard.ts lines 1-2: `const allowedMediaBreakpoints = new Set(["520px", "800px"]);`].
- Single-owner guard: no `deployment.status` / `deployment?.status` / `.statusLabel` access outside
  `features/hosting/serverState.ts` and `features/hosting/services.ts`
  [VERIFIED: tests/hosting/server-state-single-owner.test.ts lines 6-13].

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Section selection and deep link (`?tab=`) | Browser / Client (React Router search params) | — | Route manifest frozen; in-page state only |
| Header state, cost strip, endpoint | Browser / Client (derived from `GameServerDto`) | API / Backend (projection decides owner visibility, endpoint presence, SSH command) | Client derives presentation; API already applies member/operator visibility |
| Primary action and cost-stop | Browser / Client (shared `CostConfirmDialog`) | API / Backend (action projection, enabled flags, execution) | Request path is one dialog; API enforces authorization |
| Live updates | API / Backend (fleet signal, claim-scoped audience) | Browser / Client (invalidate exact keys, 30 s fallback poll) | Signal carries only a server id; data comes from authorized REST |
| Progress stage model | Browser / Client (pure derivation) | API / Backend (job status, stage, commands) | D-08: derive from data already in the API |
| Merged activity timeline | Browser / Client (pure merge of three REST payloads) | API / Backend (jobs: newest 100; operations: newest `take`, operational kinds only) | No new endpoint in scope |
| Timeline actor | API / Backend (only place that knows `RequestedByFk`/`ActorFk`) | Browser / Client (render) | Not projected today; see Open Question 1 |
| Retry / skip | API / Backend (`canRetry`/`canSkip`, endpoints) | Browser / Client (buttons only when allowed) | Existing behaviour |

## Standard Stack

No new dependencies. Everything is already installed and pinned in `sources/ReactApp/package.json`
[VERIFIED: sources/ReactApp/package.json read this session].

### Core (existing)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | Project standard |
| react-router | 7.18.2 | `useSearchParams`, `Link`, memory router in tests | Section state in `?tab=` |
| @tanstack/react-query | 5.101.4 | Queries, `refetchInterval` function form, `invalidateQueries` | Existing query layer and keys |
| react-i18next / i18next | 17.0.11 / 26.3.6 | Localization, plural `_one`/`_other` | Phase 61 locale ownership |
| @fluentui/react-icons | 2.0.320 | Decorative glyphs (mocked in Vitest) | Existing icon set |

### Supporting (existing test stack)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| vitest + jsdom | 4.1.10 / 28.0.0 | Unit and component tests | All model and component tests |
| @testing-library/react / user-event | 16.3.2 / 14.6.1 | Component interaction | Section nav, failure panel, timeline filters |
| @playwright/test + @axe-core/playwright | 1.62.1 / 4.13.0 | e2e viewport and a11y | 320/390 nav, sticky header, axe scans |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Listener API on `FleetLiveUpdatesProvider` | Second `useUpdateStream` socket on the same path in the workspace | Duplicate socket per tab, duplicate reconnect logic; rejected by D-07 ("reuse") |
| Listener API | Provider invalidates per-server keys for every frame id | Simpler, but breaks the provider's documented contract ("a fleet signal must never refetch open workspace queries") and hides workspace policy in the fleet provider |
| CSS-only sticky compact bar | IntersectionObserver that swaps full header for compact header | IO is absent in jsdom, adds JS state and layout shift; D-04 is satisfiable with a permanently sticky bar |
| Wrapping section links | Horizontally scrolling tablist | Scrolling caused UI-60-001 (discoverability); wrap keeps all five visible |

**Installation:** none.

## Package Legitimacy Audit

Not applicable: this phase installs no external packages. **Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** none.

## Architecture Patterns

### System Architecture Diagram

```
Browser URL /command/servers/:serverId?tab=<section>[&action=<id>]
        |
        v
ServerWorkspacePage (route component)
  |-- useQuery server(id) ----\
  |-- useQuery jobs(id) -------+--> refetchInterval: hasActiveJob ? 30s : false
  |-- useQuery operations(id) -/
  |-- useWorkspaceLiveUpdates(id) <-- FleetLiveUpdatesProvider (layout level, one socket)
  |        frame {serverId == id} --coalesce 500ms--> invalidate exact server/jobs/operations
  |        status open after disconnect -----------> invalidate exact server/jobs/operations
  |-- ?action= deep link -> useCostConfirmation().request(source "deep-link")   (unchanged)
  v
WorkspaceShell
  |-- Breadcrumbs: Servers (link) > Server name (link to overview) > Section (aria-current)
  |-- [sticky] CompactBar: h1 title, ServerStateBadge, primary action -> request(source "workspace-header")
  |-- DetailsBlock (scrolls away): CostStrip (owner, hourly, monthly, CostIndicator, accrued slot),
  |                                 Endpoint (+ CopyButton) or "available while running"
  |-- JobProgressStrip   <- deriveJobProgress(newest active job)        (only while a job is active)
  |-- JobFailurePanel    <- deriveFailure(jobs, derivedState, actions)  (retry/skip, cost-stop, open details)
  |-- SectionNavigation  <- workspaceSections(server)  (links ?tab=..., wraps at narrow widths)
  v
Section router (tab from search, unknown -> overview)
  |-- Overview  : facts, secondary actions, recent activity (compact), [metrics slot - Phase 67]
  |-- Activity  : buildActivityEntries(jobs, operations) -> filter(type, problemsOnly) -> timeline
  |               freshness badge <- deriveFreshness(socket status, jobs.dataUpdatedAt, active job)
  |               expand entry -> raw diagnostics with copy + download (buildCommandProtocol/textDownloadUrl)
  |-- Settings  : ServerConfigurationPanel, ServerSecretsPanel (canManageSecrets), [automation slot - Phase 68]
  |-- Files     : ServerFilesPanel (unchanged)
  |-- Machine   : ServerRuntimePanel + CloudPanel (deployment facts, resources, diagnostics)
```

### Recommended Project Structure

```
sources/ReactApp/src/
├── components/
│   ├── Breadcrumbs.tsx            # generic nav>ol breadcrumb, last item aria-current="page"
│   ├── SectionNavigation.tsx      # generic link-based section nav (wraps), aria-current="page"
│   ├── CopyButton.tsx             # clipboard write with visible + announced result, ActionButton based
│   └── server/
│       ├── CostStrip.tsx          # owner, hourly, monthly, CostIndicator, accrued slot (D-06)
│       ├── JobProgressStrip.tsx   # renders deriveJobProgress output, elapsed time, no percentage
│       ├── JobFailurePanel.tsx    # what failed, cost risk, next step, actions
│       ├── LiveBadge.tsx          # Live / Updated x ago / Stale
│       └── workspace.css          # token-only (style guard scans components/server/*.css)
└── features/hosting/
    ├── ServerWorkspacePage.tsx    # route: queries, live updates, deep link, delete dialog
    ├── WorkspaceShell.tsx         # breadcrumbs, compact bar, details block, strips, nav
    ├── workspaceSections.ts       # section ids, parse, labels, visibility (exports workspaceTabIds)
    ├── jobProgress.ts             # deriveJobProgress, activeJob, isActiveJobStatus
    ├── activityModel.ts           # buildActivityEntries, filters, counts, dedup
    ├── workspaceFreshness.ts      # deriveFreshness
    ├── useWorkspaceLiveUpdates.ts # listener registration + exact invalidations
    ├── sections/
    │   ├── OverviewSection.tsx
    │   ├── ActivitySection.tsx
    │   ├── SettingsSection.tsx
    │   ├── MachineSection.tsx     # ServerRuntimePanel + CloudPanel (moved out of the page file)
    │   └── (Files reuses ServerFilesPanel directly)
    └── ActivityEntry.tsx          # one timeline entry, expandable raw output
```

Keep `workspaceTabIds` exported from a module the parity test can import; if it moves out of
`ServerWorkspacePage.tsx`, update the import in `tests/hosting/parity-matrix.test.ts` line 4
[VERIFIED: tests/hosting/parity-matrix.test.ts line 4: `import { workspaceTabIds } from "@/features/hosting/ServerWorkspacePage";`].

### Pattern 1: Section registry and link-based navigation
**What:** One closed registry of section ids; parse with a whitelist; render links, not buttons.
**When to use:** Section nav, breadcrumbs, "Open activity details" link, parity test import.
**Current ids being retired** [VERIFIED: src/features/hosting/ServerWorkspacePage.tsx lines 55-65]:
`"overview", "runtime", "cloud", "logs", "files", "configuration", "credentials", "jobs", "audit"`.
**New ids** (from D-01; lowercase id spelling is a recommendation [ASSUMED]): `overview`, `activity`, `settings`,
`files`, `machine`.

```typescript
// workspaceSections.ts (recommended shape)
export type WorkspaceSection = "overview" | "activity" | "settings" | "files" | "machine";
export const workspaceTabIds: readonly WorkspaceSection[] = [
  "overview", "activity", "settings", "files", "machine",
];
const sectionLabelKeys = {
  overview: "Server_Workspace_Section_Overview",
  activity: "Server_Workspace_Section_Activity",
  settings: "Server_Workspace_Section_Settings",
  files: "Server_Workspace_Section_Files",
  machine: "Server_Workspace_Section_Machine",
} as const satisfies Record<WorkspaceSection, TranslationKey>; // keys are new [ASSUMED names]

/** Untrusted input: whitelist only; retired ids such as "jobs" fall back to overview (D-02). */
export function workspaceSectionFromSearch(value: string | null): WorkspaceSection {
  return workspaceTabIds.includes(value as WorkspaceSection) ? (value as WorkspaceSection) : "overview";
}
/** Overview has no tab parameter; other sections write only `tab` so a stale `action` never re-fires. */
export function sectionSearch(section: WorkspaceSection): string {
  return section === "overview" ? "" : `?tab=${section}`;
}
```

Render each item as a react-router `Link` with explicit pathname (`to={{ pathname: `/command/servers/${id}`,
search: sectionSearch(s) }}`) and `aria-current="page"` on the current one inside `<nav aria-label=...>`. Do not use
`role="tab"`: MDN states "Don't use `aria-current` as a substitute for `aria-selected` in ... `tab`" and recommends
`aria-current="page"` for the current item of a set of pages [CITED: developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current].
Links give real deep links (open in new tab, copy link) and replace the raw `<button>` elements the current nav uses
(lines 383-395), which violate the "no raw button posing as a control" rule.

**320/390 px (D-03):** use `display: flex; flex-wrap: wrap` (or a `repeat(auto-fit, minmax(...))` grid) with compact
padding so all five links stay visible on two rows; no horizontal scroll. German labels are the width driver
("Einstellungen", "Aktivität"). Remove `overflow-x: auto` from the nav [VERIFIED: src/features/hosting/hosting.css
lines 135-140 `display: flex; gap: 0.25rem; overflow-x: auto;`].

**Breadcrumbs (WSP-06):** `<nav aria-label="Breadcrumb"><ol>` with Servers (link `/command/servers`), server title
(link to overview, `overflow-wrap: anywhere` for 256-char names) and the section name as the current item
(`aria-current="page"`). This relocates the parity row FLT-back-to-fleet ("Back to fleet" header button).

### Pattern 2: Sticky compact bar + scrolling details block (WSP-01, CST-01, D-04..D-06)
**What:** The compact bar is permanently `position: sticky` (no JS). It holds the page `h1` title, the
`ServerStateBadge` (or `ServerStateSummary`), and the primary action. The details block below it (cost strip,
endpoint) is normal flow and scrolls away.
**Why:** Satisfies D-04 literally ("compact bar remains") with zero layout JS; avoids IntersectionObserver, which jsdom
does not implement [ASSUMED].
**Stacking:** the command topbar is already sticky; the existing tab rule uses
`top: var(--command-header-height)` [VERIFIED: src/components/commandCenter.css lines 240-247]. Put the compact bar
and the section nav in one sticky container with that `top` (no offset math between two sticky elements), and place
the details block after the nav, or keep only the compact bar sticky. Check the sticky height at 320x568 in e2e (it
should not exceed roughly a third of the viewport [ASSUMED threshold]).

**Cost facts:** reuse the dialog's rule so the strip and the confirmation never disagree
[VERIFIED: src/components/server/costConfirmationModel.ts lines 80-95 read this session:
`const hourly = server.deployment?.estimatedHourlyCostEur ?? server.estimatedHourlyCostEur ?? null;` and
`monthly: server.estimatedMonthlyCostEur ?? null,` and `owner: ownerFor(server.costOwnerName),`]. Extract a small
`serverCostFacts(server)` helper (or call `ownerFor` plus the same hourly expression) and format hourly with
`formatHourlyRate` (sub-cent safe) and monthly with `formatMoney`. When `hourly`/`monthly` is null show the existing
"Cost estimate not available" copy (`Server_Confirm_CostUnavailable`), never "0".

**Owner placeholders:** the API sends English placeholders `"Assigned owner"` / `"Unknown owner"`
[VERIFIED: src/components/server/costConfirmationModel.ts lines 5 and 8]; map them through `ownerFor` to
`Server_Confirm_OwnerHidden` / `Server_Confirm_OwnerUnknown`, never render the raw API string.

**Costing now:** `CostIndicator` with `deriveServerState(server)`; its invariant guarantees a server that may still
cost is never `off`/`none` [VERIFIED: src/features/hosting/serverState.ts lines 150-155].

**Accrued slot (D-06):** a labelled row ("Accrued this session") whose value is a muted "Not available yet" line; give
it a stable `data-slot="accrued-cost"` hook for Phase 66 [ASSUMED naming].

**Endpoint:** `deployment.connectionEndpoint` is non-null only while the deployment is Running with a public address
and port [VERIFIED: sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs (read 850-925 via Bash,
condition `deployment.Status == EDeploymentStatus.Running && publicAddress is not null && connectionPort is { } port`)
— tag downgraded to ASSUMED for the exact text because it was read with `sed`, not `Read`]. Header rule: show
endpoint + `CopyButton` when `connectionEndpoint` is present, otherwise a plain "Available while the server is
running" line. Do not decide visibility from `deployment.status` in React (single-owner guard).

**Primary action:** `primaryServerAction(server.actions, server.deployment?.possibleCost ?? false)` already prefers a
graceful cost-stop when cost may remain [VERIFIED: src/features/hosting/workspaceModel.ts lines 221-246]. Request
with `source: "workspace-header"`. The overview "next action" panel's button becomes redundant; relocate
FACT-next-action (keep the description/cost description text in Overview if UI-SPEC wants it, but only one primary
button with that name on the page).

### Pattern 3: Workspace live updates (D-07, ACT-02)
**Facts:**
- Fleet stream path and frame type [VERIFIED: src/features/hosting/FleetLiveUpdatesProvider.tsx lines 14 and 17-18]:
  `const fleetStreamPath = "game-servers/fleet";` and
  `"X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage"`.
- The provider invalidates only `hostingQueryKeys.fleet` and `notifications` with `exact: true` and exposes only
  `{ status, disconnectEpoch, reconnectNow }` [VERIFIED: FleetLiveUpdatesProvider.tsx lines 44-47, 109-112;
  useFleetLiveUpdates.ts lines 5-12].
- Connection states [VERIFIED: src/app/updateStream.ts line 11]:
  `"connecting" | "open" | "reconnecting" | "unavailable"`.
- Signal coverage [VERIFIED: sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs lines 279-315]:
  GameServer Added or Modified `Title`/`IsArchived`/`IsLaunchDisabled`/`CurrentCostOwnerFk`; deployment Added or
  Modified `Status`/`PossibleCost`; LifecycleJob Added or Modified `Status`. Remarks line 23: "Stage, progress and
  timestamp churn produce no signal". Worker command rows and audit records are not observed at all.
- Query keys [VERIFIED: src/features/hosting/services.ts lines 113-115]:
  `server: (serverId) => ["hosting", "servers", serverId]`, `jobs: ... [..., serverId, "jobs"]`,
  `operations: ... [..., serverId, "operations"]`.
- `invalidateQueries` refetches only active queries by default ("otherwise `'active'`")
  [CITED: tanstack.com/query/v5/docs/reference/QueryClient].
- `refetchInterval` accepts a function of the query returning `number | false | undefined`
  [CITED: tanstack.com/query/v5/docs/framework/react/reference/useQuery]; `refetchIntervalInBackground` defaults to
  `false`, `refetchOnWindowFocus` defaults to `true` (same source).

**Recommended design:**
1. Extend `FleetLiveUpdatesValue` additively with `subscribe(listener: (serverId: string) => void): () => void` (and
   keep the context default a no-op so `renderWithProviders`, which mounts no provider, still works). The provider
   calls listeners for each valid fleet-changed frame, outside its own coalescer.
2. `useWorkspaceLiveUpdates(serverId)` registers a listener, ignores other ids, and coalesces (reuse `createCoalescer`,
   500 ms) into exact invalidations of `server(id)`, `jobs(id)`, `operations(id)`. It also invalidates the same three
   when `status` returns to `"open"` after a disconnect (track `disconnectEpoch`), recovering missed changes.
3. Every invalidation of `server(id)` must pass `exact: true`: the server key is a prefix of the files,
   configuration, secrets, preservation and other per-server keys (services.ts lines 116-129).
4. Fallback poll on the three workspace queries:
   `refetchInterval: () => (hasActiveJob ? 30_000 : false)` where `hasActiveJob` is
   `server.pendingJobCount > 0 || jobs.some(isActiveJobStatus)`; active statuses match the API's pending set
   [VERIFIED: GameServerProjectionService.cs lines 786-792: `Queued`, `Running`, `RetryPending`, `WaitingForWorker`].
5. Update `docs/features/react-ui-beta.md` ("the server workspace still keep their own pollers until Phase 63").

**Freshness (D-11):** pure `deriveFreshness({ status, dataUpdatedAt, now, hasActiveJob })`:
- `live` when `status === "open"` and (no active job, or `now - dataUpdatedAt <= 60_000`);
- `stale` when the socket is not open, or a job is active and data is older than 60 s [ASSUMED threshold = 2x poll].
Show "Updated {{relative}}" from `jobs.dataUpdatedAt` beside the badge, re-render on a coarse 15-30 s clock only while
the Activity section is visible, and offer `reconnectNow` / refresh when stale.

### Pattern 4: Progress strip stage model (WSP-04, D-08)
**Verified inputs:**
- `ELifecycleJobKind`: `"Start" | "Stop" | "Terminate" | "Restart" | "Install" | "Update" | "CollectLogs" | "Custom"`
  [VERIFIED: src/generated/api.ts line 6844].
- `ELifecycleJobStatus`: `"Queued" | "Running" | "RetryPending" | "WaitingForWorker" | "Succeeded" | "Failed" | "Skipped" | "CancelledByCostStop"`
  [VERIFIED: src/generated/api.ts line 6846].
- `EWorkerCommandStatus`: `"Queued" | "Received" | "Running" | "Succeeded" | "Failed" | "Retried" | "Skipped" | "CancelledByCostStop"`
  [VERIFIED: src/generated/api.ts line 6942].
- `LifecycleJobDto.stage` is a free `string`; `progressPercent` is an int [VERIFIED: api.ts lines 7801-7803].
- Job stage literals written by the control plane, read this session:
  `"claimed"` / `"recovered"` [VERIFIED: LifecycleJobQueue.cs line 191], `"completed"` [line 261],
  `"retry-pending"` / `"failed"` [line 313], `"provider-provisioning"` [VERIFIED: LifecycleJobExecutor.cs line 368],
  `"waiting-for-worker"` [line 716], `"running"` [line 804], `"completed-by-worker"` [line 1376]. Worker command
  evidence copies the worker's own stage string into `job.Stage` [VERIFIED: WorkerProtocolService.cs lines 966-970].
  Other literals (`"skipped-by-user"`, `"cancelled-by-cost-stop"`, `"fault-evidence-window"`, `"provider-teardown"`,
  many worker strings) were seen via grep only [ASSUMED].
- `progressPercent` is a coarse milestone, not a measurement: set to 15, 30, 35, 40, 90, 100 at fixed points
  [VERIFIED: LifecycleJobExecutor.cs lines 368, 717, 805, 1376; WorkerProtocolService.cs lines 971-976]. Do not
  render it as a bar or number (D-08 "never show an invented percentage"); remove the `<progress>` from the job view.

**Recommended derivation** (pure, exhaustive over the enums, unknown stage strings never crash):

| Step | Shown when | Applies to |
|------|-----------|------------|
| Queued | status `Queued` | all |
| Waiting to retry | status `RetryPending` (show the last failure reason) | all |
| Preparing the cloud machine | status `Running`, stage starts with `provider-` or is `claimed`/`recovered`, no commands yet | Start |
| Waiting for the server software | status `WaitingForWorker` or stage `waiting-for-worker` | Start |
| Working on the server | any command `Received`/`Running` | all worker-backed kinds |
| Saving data / releasing the machine | Stop/Terminate after the worker command, stage contains `preservation` / starts with `provider-` | Stop, Terminate |
| Finishing | command `Succeeded` while the job is not terminal | all |

Unknown combinations map to a single generic "Working" step plus the raw stage as secondary technical text in a
`lang="en"` span. Show elapsed time from `timeStampStarted ?? timeStampCreated` ("Running for 4 min"), refreshed on
a coarse clock. Step labels and grouping are a UX recommendation [ASSUMED]; the inputs are verified.

### Pattern 5: Failure panel (WSP-05, D-09)
**Verified rules:** `canRetry` and `canSkip` are true only when the control plane is not paused, the job status is
`Failed`, the failure code does not start with `permanent-`, and the viewer has the operate permission
[VERIFIED: GameServerProjectionService.cs lines 647-664]. Terminate jobs never become `Failed`; they retry
indefinitely as `RetryPending` with `FailureCode`/`FailureMessage` set [VERIFIED: LifecycleJobQueue.cs lines 308-325].
**Model:** `deriveFailure(jobs, derived, actions)` returns the newest job with status `Failed` (or `RetryPending` with
a failure message, shown as "retrying automatically") plus:
- what failed: job kind/action label (vocabulary key, else humanized `lang="en"`) + `failureReason` in `ApiText`;
- cost risk: `derived.cost` (`live` / `maybe` / `none`) mapped to plain sentences;
- next step: cost-stop first when `cost !== "none"`, else retry/skip when allowed, else "open activity details";
- actions: Retry/Skip (`hostingService.retryJob`/`skipJob`, existing mutation), cost-stop via
  `request({ ..., source: <new failure source> })`, and a link to `?tab=activity` targeting the entry.
**Cost-stop action selection:** extract the stop-selection branch of `primaryServerAction` into a shared
`costStopAction(actions)` so header, failure panel and Machine section pick the same action. If only `terminate` is
available, keep its own label ("Shut down without saving") rather than relabelling it as "stop billing" (honesty);
confirm with UI-SPEC [ASSUMED].
**When to show:** only for the newest job (a newer job supersedes it); suggest suppressing it when the newest failed
job is older than the newest succeeded job of any kind [ASSUMED rule].

### Pattern 6: Merged activity model (ACT-01, D-10)
**Inputs:**
- Jobs: newest 100 jobs with all worker commands (diagnostic output included) [ASSUMED from `Take(100)` seen via
  Bash; endpoint requires the view permission].
- Operations: newest `take` (client passes 80, API allows 1-200) audit records, filtered to 26 operational kinds
  [VERIFIED: GameServerProjectionService.cs lines 593-621]: `DeploymentCreated`,
  `DeploymentResourceProvisioningRequested`, `DeploymentResourceProvisioningFailed`, `DeploymentResourceObserved`,
  `DeploymentResourceAdopted`, `DeploymentResourceReleaseRequested`, `DeploymentResourceReleaseFailed`,
  `DeploymentResourceReleased`, `DeploymentStopped`, `DeploymentTerminated`, `LifecycleJobQueued`,
  `LifecycleJobClaimed`, `LifecycleJobRetryPending`, `LifecycleJobFailed`, `LifecycleJobSucceeded`,
  `LifecycleFailed`, `WorkerProvisioned`, `WorkerRegistered`, `WorkerDegraded`, `WorkerRecovered`, `WorkerStopped`,
  `WorkerCommandQueued`, `WorkerCommandFailed`, `WorkerCommandSucceeded`, `ProviderReconciliationDegraded`, `Recovery`.
- `HostingOperationDto` fields: `primaryKey, kind, status, summary, detail, providerIdentifier, resourceKind,
  resourceIdentifier, providerState, deploymentId, lifecycleJobId, workerId, traceId, spanId, timeStampCreated`
  [VERIFIED: src/generated/api.ts lines 7689-7706] — **no actor**.
- `LifecycleJobDto` fields end with `timeStampCreated, timeStampUpdated, timeStampStarted, timeStampCompleted,
  commands` [VERIFIED: api.ts lines 7792-7815] — **no actor**.

**Entry model (recommended):**
```typescript
type ActivityType = "job" | "log" | "audit";
interface ActivityEntry {
  id: string;            // "job:<pk>" | "log:<commandPk>" | "audit:<pk>" — also the DOM anchor id
  type: ActivityType;
  at: string;            // job: completed ?? updated; log: completed ?? started ?? created; audit: created
  problem: boolean;      // job Failed/RetryPending with failure, command Failed/CancelledByCostStop, audit tone danger|warning
  sentenceKey: TranslationKey;          // closed maps over the verified enums
  values: Record<string, string>;       // action label, status, actor (when available)
  apiText?: string;                      // job failureReason / audit summary, rendered in ApiText
  source: LifecycleJob | ProtocolEntry | HostingOperation; // for the expanded raw view
}
```
- Sort newest first by `at` with a stable tie-break on id.
- Dedup: the audit "echo" kinds (`LifecycleJobQueued`, `LifecycleJobClaimed`, `LifecycleJobRetryPending`,
  `LifecycleJobFailed`, `LifecycleJobSucceeded`, `WorkerCommandQueued`, `WorkerCommandFailed`,
  `WorkerCommandSucceeded`) whose `lifecycleJobId` is a loaded job are hidden from **All** but kept in the **Audit**
  filter with full technical evidence (keeps FACT-audit-evidence parity). Counts per filter equal what that filter
  shows [ASSUMED UX rule, confirm in UI-SPEC].
- Window alignment: when `operations.length === take`, entries older than the oldest audit record have incomplete
  audit coverage; show a one-line note at that boundary rather than silently mixing [ASSUMED].
- Render a first page (for example 50 entries) with a "Show more" `ActionButton`.
- Expanded log/job entry keeps the existing copy and download controls (`buildCommandProtocol`, `textDownloadUrl`,
  download all) and `pre tabIndex={0}` for scrollable output; add a `CopyButton` for the raw text.
- "Open activity details" target: `?tab=activity` plus a fragment `#activity-job-<pk>`; an effect scrolls the entry
  into view, expands it and moves focus once data is loaded; reset type/problem filters so the entry is visible.
  Match the fragment with `Array.find`, never index by it (untrusted input).

### Anti-Patterns to Avoid
- **Raw `<button>` section tabs:** the current nav uses raw buttons; use links (`SectionNavigation`).
- **Invalidating `server(id)` without `exact: true`:** refetches files, configuration, secrets and preservation queries.
- **Second WebSocket in the workspace:** duplicates the fleet socket and its backoff.
- **Rendering `progressPercent`:** milestone values look like precise progress.
- **Reading `deployment.status` in new components:** fails the single-owner guard; use `deriveServerState`.
- **New strings in `copy.ts` (`useHostingCopy`) or hard-coded English** ("Credentials", "Terrain", "Scenario",
  "/month", "/h" exist today at lines 373, 489, 495, 470-471): redesigned surfaces read only JSON keys.
- **Rewriting the URL for retired `?tab=` values:** D-02 says fall back, no mapping table; rewriting also interferes with
  the `?action=` strip logic. Render Overview and leave the URL alone.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Server state and cost label | Status-string checks | `deriveServerState` + `ServerStateSummary`/`CostIndicator` | Single owner, invariant tested |
| Cost owner display | Raw `costOwnerName` | `ownerFor` | API placeholders are English |
| Hourly/monthly | New formatter | `formatHourlyRate`, `formatMoney`, dialog hourly rule | Sub-cent rates, dialog parity |
| Action confirmation | Inline confirm | `useCostConfirmation().request` | One dialog, typed challenge, focus return |
| Disabled reasons | `title` tooltips | `ActionButton unavailableReason` + `ActionUnavailableReason` | a11y rule from Phase 61 |
| Socket/reconnect | New `WebSocket` code | `FleetLiveUpdatesProvider` + `createCoalescer` | Backoff, visibility/online retry, claim-scoped audience |
| Protocol text/download | New serializer | `protocolEntries`, `buildCommandProtocol`, `textDownloadUrl` | Parity rows JOB-download-protocol, JOB-logs-* |
| Audit tone/category/evidence | New parser | `hostingAuditTone`, `parseHostingAuditDetail`, `HostingAuditEntry` | Tested legacy detail parsing |
| Action labels | New maps | `lookupActionVocabulary`, `ActionLabel` | Phase 61 vocabulary, CST-02 |
| Clipboard | Inline `navigator.clipboard` in each component | One `CopyButton` built on `ActionButton` | Secure-context requirement and `NotAllowedError` handling in one place |

**Key insight:** Phase 61/62 already centralised every hard decision (state, cost, confirmation, vocabulary, socket).
Phase 63's risk is divergence: any second derivation of "is it costing" or "which action stops cost" will drift.

## Runtime State Inventory

This phase renames in-page section ids (a refactor of URL state).

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no tab value is persisted; `localStorage` is used only for culture and the events view mode (grep of `src/`) | None |
| Live service config | None — no API, Discord bot or classic WebApp code builds `/command/servers/...?tab=` links (grep of `sources/Api`, `sources/WebApp`) | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | None | None |
| External bookmarks | User bookmarks or chat links with `?tab=jobs` etc. | Accepted by D-02: they open Overview |
| Tests carrying old ids | `tests/e2e/hosting-placement.spec.ts:153` (`?tab=configuration`), `tests/hosting/workspace-header.test.tsx:137` (`?tab=cloud`), `tests/hosting/workspace-page.test.tsx:669-674` (`?tab=jobs`/`?tab=audit`), `tests/hosting/deep-link.test.tsx:289-320` (`?tab=jobs` carried) | Update to new ids (code edit) |

## Common Pitfalls

### Pitfall 1: Live badge claims more than the signal delivers
**What goes wrong:** "Live" shown while command output or stage changes are 30 s behind.
**Why:** The interceptor ignores stage/progress churn, worker commands and audit records.
**How to avoid:** Freshness depends on socket status and data age while a job is active; copy says "Updated x ago".
**Warning signs:** A test that expects a stage change to appear after a frame without a poll.

### Pitfall 2: Prefix invalidation storms
**What goes wrong:** Every frame refetches files and configuration of the open server.
**How to avoid:** `exact: true` on `server(id)`; separate exact calls for `jobs(id)` and `operations(id)`.

### Pitfall 3: Parity test and existing tests break on the id change
**What goes wrong:** `parity-matrix.test.ts` runs `it.each([...workspaceTabIds])("has a row for workspace tab %s")`
(line 135); new ids need `TAB-activity`, `TAB-settings`, `TAB-machine` rows; old TAB-runtime/cloud/logs/configuration/
credentials/jobs/audit rows stay and become `relocated` with a New location (rows are never deleted).
**How to avoid:** Update the matrix in the same plan that changes the ids; update the test import if the export moves.

### Pitfall 4: Two sticky elements fighting
**What goes wrong:** The existing `.command-shell .next-hosting-tabs` sticky rule plus a new sticky bar overlap or
eat most of a 568 px viewport.
**How to avoid:** One sticky container; delete or replace the old tab rule; e2e-check its height at 320x568.

### Pitfall 5: Treating `progressPercent` as progress
**What goes wrong:** A bar jumps 15 -> 35 -> 100; violates D-08.
**How to avoid:** Stage steps plus elapsed time only; parity row JOB-list notes progress moved to the stage strip.

### Pitfall 6: Missing confirmation source values
**What goes wrong:** `source: "workspace-header"` fails typecheck.
**Why:** The union is `"workspace-primary" | "workspace-action" | "fleet-card" | "notification" | "deep-link"`
[VERIFIED: src/components/server/useCostConfirmation.ts lines 4-5].
**How to avoid:** Add `"workspace-header"` (and a failure-panel source, e.g. `"workspace-failure"` [ASSUMED name]);
update the Phase 61 UI-SPEC source list reference; keep `"workspace-primary"` only if still used (tests use it at
`tests/hosting/cost-confirmation-provider.test.tsx:50`).

### Pitfall 7: Merged windows of different depth
**What goes wrong:** 100 jobs reach back weeks while 80 audit records cover days; older region looks audit-less.
**How to avoid:** Boundary note when the audit list is full; or raise `take` up to the API maximum of 200
[VERIFIED: GameServersController `if (take is < 1 or > 200)` read via Bash — ASSUMED tag for exact text].

### Pitfall 8: Terminate never "fails"
**What goes wrong:** Failure panel never appears for a stuck shutdown because the job stays `RetryPending`.
**How to avoid:** Treat `RetryPending` with a failure message as "a step failed, retrying automatically" with the cost
sentence; the cost-stop is already the running job, so the next step is "wait or open activity details".

### Pitfall 9: Vocabulary drift (CST-02)
**What goes wrong:** Machine section keeps "cost-stop", "termination", "Deprovision now".
**Evidence:** `Hosting_Detail_PossibleCost` (used twice in the page), `React_ServerWorkspace_ExternalStorageDeleted`
("...deleted during full cost-stop or termination", used in `ServerRuntimePanel.tsx:281`).
**How to avoid:** New `Server_Workspace_*` keys in plain words reusing "Shut down and stop billing"
(`Server_Action_Stop_Label`: EN "Shut down and stop billing", DE "Herunterfahren und Abrechnung beenden"
[VERIFIED via grep of en.json:2932 / de.json:2932 — ASSUMED tag, not opened with Read]); add a guard test that
scans the new keys' EN/DE values for banned synonyms (`cost-stop`, `deprovision`, `terminat`, `tear down`).

### Pitfall 10: Header test structure assumptions
**What goes wrong:** `workspace-header.test.tsx` locates the header with `heading.closest("header")`.
**How to avoid:** Keep the compact bar a `<header>` containing the `h1`, or rewrite those assertions in the same plan.

## Code Examples

### Workspace queries with conditional fallback poll
```typescript
// Source: TanStack Query v5 useQuery reference (refetchInterval function form)
const hasActiveJob = (server.data?.pendingJobCount ?? 0) > 0 || (jobs.data ?? []).some(isActiveJob);
const fallback = () => (hasActiveJob ? 30_000 : false);
const jobs = useQuery({
  queryKey: hostingQueryKeys.jobs(serverId),
  queryFn: ({ signal }) => hostingService.jobs(serverId, signal),
  enabled: permissions.accessServers && Boolean(serverId),
  refetchInterval: fallback,
});
```

### Exact invalidation from the live listener
```typescript
// Source: FleetLiveUpdatesProvider.tsx pattern (createCoalescer + exact keys)
const invalidateWorkspace = (client: QueryClient, id: string) => {
  void client.invalidateQueries({ queryKey: hostingQueryKeys.server(id), exact: true });
  void client.invalidateQueries({ queryKey: hostingQueryKeys.jobs(id), exact: true });
  void client.invalidateQueries({ queryKey: hostingQueryKeys.operations(id), exact: true });
};
```

### Copy button
```typescript
// Source: MDN Clipboard.writeText — secure context only; rejects with NotAllowedError
async function copyText(text: string): Promise<"copied" | "failed"> {
  try {
    await navigator.clipboard.writeText(text);
    return "copied";
  } catch {
    return "failed"; // show the value selectable and a "Copy failed, select the text" status
  }
}
```
`navigator.clipboard` is "available only in secure contexts (HTTPS)" and throws `NotAllowedError` when writing is
not allowed [CITED: developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText]. Announce the result in a polite
`role="status"` region; guard `navigator.clipboard` being undefined.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Nine button tabs, horizontal scroll | Five link sections, wrap | Phase 63 | Resolves UI-60-001, real deep links |
| 5 s polling of server/jobs/operations | Signal-driven exact invalidation + 30 s poll only while a job is active | Phase 63 (D-07) | Fewer requests; doc update required |
| Separate Jobs, Logs, Audit tabs | One Activity timeline with filters | Phase 63 | Dedup needed for readability |
| `progressPercent` bar | Stage steps + elapsed time | Phase 63 (D-08) | No invented precision |

**Deprecated/outdated:** `JobList` progress bar; "Back to fleet" header button (becomes breadcrumb); overview
"Show all" button targeting `?tab=audit` (becomes Activity link).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Section id spellings `activity`, `settings`, `machine` | Pattern 1 | Low; only URL text |
| A2 | jsdom lacks IntersectionObserver, so CSS-only sticky is simpler to test | Pattern 2 | Low |
| A3 | Sticky container should stay under about a third of 568 px | Pattern 2 | Medium; usability at 320 px |
| A4 | 60 s staleness threshold for the Live badge | Pattern 3 | Low; tunable |
| A5 | Progress step names and grouping; unseen stage literals beyond the verified set | Pattern 4 | Medium; unknown stages fall back to "Working" |
| A6 | Failure panel visibility rule (newest job only) | Pattern 5 | Medium; UX |
| A7 | Terminate-only fallback keeps its own label instead of "stop billing" | Pattern 5 | Medium; CST-02 wording |
| A8 | Audit echo dedup in "All" and counts per filter | Pattern 6 | Medium; parity of FACT-audit-evidence preserved via Audit filter |
| A9 | Jobs endpoint returns the newest 100 jobs; `take` max 200; `connectionEndpoint` condition | Patterns 2, 6 | Low; read via Bash, not Read |
| A10 | `Server_Action_Stop_Label` EN/DE values | Pitfall 9 | Low; grep evidence |
| A11 | New key names (`Server_Workspace_*`, `Server_Activity_*`), `data-slot` hooks, `"workspace-failure"` source | several | Low |
| A12 | Actor can be added to the DTOs (Open Question 1) without privacy issues for `sv:view` members | Open Questions | High if user objects or nickname visibility differs |

## Open Questions

1. **Timeline actor (D-10) is not available from the API (blocking for ACT-01 wording).**
   - What we know: `HostingAuditRecord` has `Actor`/`ActorFk` and `LifecycleJob` has `RequestedBy`/`RequestedByFk`
     (grep of `sources/Database/Hosting`), but `ProjectOperation` maps no actor
     [VERIFIED: GameServerProjectionService.cs lines 704-728] and `LifecycleJobDto` has no actor field
     [VERIFIED: api.ts lines 7792-7815]. CONTEXT scope says "sources/ReactApp only"; AGENTS.md forbids broadening the
     API for convenience.
   - What's unclear: whether the user accepts a small additive API change (two nullable display-name fields, privacy
     rule like `canSeeOwner`, OpenAPI snapshot + `npm run generate`, xunit test) as in Phase 62-02, or prefers a
     React-only fallback.
   - Recommendation: ask before planning. If approved, plan it as Wave 1 (API projection + contract refresh +
     test), mirroring 62-02. If declined, amend D-10 wording: entries show a system actor where the event is
     control-plane or worker originated ("The platform", "The server") and omit the actor for user requests.
2. **WSP-02 / roadmap SC-2 amendment (D-02).** Plan a docs task editing REQUIREMENTS.md WSP-02 and ROADMAP Phase 63
   SC-2 to "retired `?tab=` values open Overview".
3. **Stable "paused by operator" reason code (roadmap flag).** No API change in scope; keep exact-string mapping via
   `pausedReason` (`"Hosting lifecycle execution is paused by the operator."`) [VERIFIED via Bash grep of
   unavailableReasonMapping.ts line 19 — ASSUMED tag]. Move the paused notice into the shell so it shows in every
   section.
4. **Member vs operator field visibility (roadmap flag).** The API already gates the administrative SSH command
   (global admin claim) and cost owner name (`CanViewCostOwnerAsync`); retry/skip require operate. The UI must render
   only what the DTO carries and keep credentials behind `canManageSecrets`. No extra rule needed unless UI-SPEC wants
   to hide technical evidence from `sv:view` members.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | build, Vitest | ✓ | v25.5.0 (package `engines` says `>=24 <25`; `.npmrc` has `engine-strict=false`) | Works today; baseline run green |
| npm | scripts | ✓ | 11.8.0 | — |
| Playwright + Chromium | e2e viewport/a11y | ✓ | 1.62.1, browsers installed | — |
| .NET SDK | only if Open Question 1 adds an API field | ✓ | 10.0.201 | — |

Baseline: `npx vitest run tests/hosting/workspace-page.test.tsx tests/hosting/workspace-header.test.tsx
tests/hosting/parity-matrix.test.ts` passed 87/87 in 7.9 s this session.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 + jsdom + Testing Library; Playwright 1.62.1 + axe 4.13.0; xunit (only if Open Question 1 is approved) |
| Config file | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| Quick run command | `npm --prefix sources/ReactApp test -- tests/hosting/<file>` |
| Full suite command | `npm --prefix sources/ReactApp run ci` and `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-workspace.spec.ts tests/e2e/hosting-a11y.spec.ts tests/e2e/hosting-placement.spec.ts tests/e2e/hosting-fleet.spec.ts` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| WSP-01 | Compact bar (title, badge, primary action with `workspace-header`), details block (owner mapping, hourly/monthly, costing now, accrued "not available yet", endpoint + copy or "available while running") in every section | component | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-header.test.tsx tests/copy-button.test.tsx` | header ✅ (rewrite), copy ❌ Wave 0 |
| WSP-02 | Five sections from `?tab=`; retired ids (`jobs`, `cloud`, ...) render Overview; links carry only `tab`; `?action=` still handled | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-sections.test.ts tests/hosting/workspace-page.test.tsx tests/hosting/deep-link.test.tsx` | sections ❌ Wave 0 |
| WSP-03 | No clipping at 320x568 and 390x844 (each nav link inside viewport, no document overflow), keyboard Tab reaches all five | e2e + axe | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-workspace.spec.ts` | ❌ Wave 0 |
| WSP-04 | `deriveJobProgress` exhaustive over `ELifecycleJobKind` x `ELifecycleJobStatus`, unknown stage -> generic step, never a percentage in DOM | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/job-progress.test.ts tests/hosting/job-progress-strip.test.tsx` | ❌ Wave 0 |
| WSP-05 | Failed job: reason, cost sentence by `derived.cost`, retry/skip only when `canRetry`/`canSkip`, cost-stop requests confirmation, details link targets the entry; `RetryPending` Terminate case | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/job-failure.test.tsx` | ❌ Wave 0 |
| WSP-06 | Breadcrumb Servers > name > section, current item `aria-current="page"`, links correct | component | `npm --prefix sources/ReactApp test -- tests/breadcrumbs.test.tsx tests/hosting/workspace-page.test.tsx` | ❌ Wave 0 |
| CST-01 | Cost strip values from the same rule as the dialog; null estimates show "not available"; may-cost never "off" | component | `npm --prefix sources/ReactApp test -- tests/hosting/cost-strip.test.tsx` | ❌ Wave 0 |
| CST-02 | Header, failure panel, timeline and Machine use "Shut down and stop billing"; banned-synonym scan over new keys (EN/DE) | unit (locale guard) + component | `npm --prefix sources/ReactApp test -- tests/locales/server-keys.test.ts tests/hosting/workspace-vocabulary.test.ts` | server-keys ✅ (extend), vocabulary ❌ Wave 0 |
| ACT-01 | `buildActivityEntries`: merge, newest first, stable ties, dedup of echo kinds, filter counts, problems-only, window note; German rendering | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/activity-model.test.ts tests/hosting/activity-section.test.tsx` | ❌ Wave 0 |
| ACT-02 | `deriveFreshness` truth table; listener invalidates exact keys only for the matching id; reconnect refetch; 30 s poll only while active; copy/download controls kept | unit + component (FakeWebSocket, fake timers) | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-freshness.test.ts tests/hosting/workspace-live-updates.test.tsx tests/hosting/fleet-live-updates.test.tsx` | fleet ✅ (extend), others ❌ Wave 0 |
| FND-07 gate | Parity matrix rows for new ids; moved rows `relocated` | unit | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` | ✅ (data update) |
| Gates | Route manifest, style guard, single-owner guard, locales | gate | `git diff --exit-code -- sources/Common/Ui/routes.json`; `npm --prefix sources/ReactApp test -- tests/styles tests/hosting/server-state-single-owner.test.ts`; `npm --prefix sources/ReactApp run locales:check` | ✅ |

### Sampling Rate
- **Per task commit:** touched test files plus `npm --prefix sources/ReactApp run typecheck` and `lint`.
- **Per wave merge:** `npm --prefix sources/ReactApp test` (whole suite), `locales:check`, style and single-owner guards.
- **Phase gate:** `npm --prefix sources/ReactApp run ci`, the four Playwright specs above, `routes.json` diff clean,
  `contracts:check` clean (and `dotnet test tests/Api.Tests` only if the actor projection lands).

### Wave 0 Gaps
- [ ] `tests/hosting/workspace-sections.test.ts` — WSP-02 parsing and link search
- [ ] `tests/hosting/job-progress.test.ts`, `tests/hosting/job-progress-strip.test.tsx` — WSP-04
- [ ] `tests/hosting/job-failure.test.tsx` — WSP-05
- [ ] `tests/breadcrumbs.test.tsx`, `tests/copy-button.test.tsx` — WSP-06, WSP-01 (mock `navigator.clipboard`)
- [ ] `tests/hosting/cost-strip.test.tsx` — CST-01
- [ ] `tests/hosting/workspace-vocabulary.test.ts` — CST-02
- [ ] `tests/hosting/activity-model.test.ts`, `tests/hosting/activity-section.test.tsx` — ACT-01
- [ ] `tests/hosting/workspace-freshness.test.ts`, `tests/hosting/workspace-live-updates.test.tsx` — ACT-02 / D-07
- [ ] `tests/e2e/hosting-workspace.spec.ts` — WSP-03 viewports, sticky bar, axe (reuse `tests/e2e/support/fleetStream.ts` and the route mocks in `hosting-a11y.spec.ts`)
- [ ] Shared fixture builders for jobs with commands and operations (extend `tests/helpers/fleetFixtures.ts` or add `workspaceFixtures.ts`)

## Security Domain

`security_enforcement: true`, ASVS level 1 (`.planning/config.json`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Unchanged cookie auth |
| V3 Session Management | no | Unchanged |
| V4 Access Control | yes | API enforces view/operate/manage-secrets; UI shows retry/skip only on `canRetry`/`canSkip`, credentials only on `canManageSecrets`; actions disabled with mapped reason |
| V5 Input Validation | yes | `?tab=` whitelist; `?action=` and entry fragment matched with `Array.find`/`Map`, never property indexing; frame payload type-checked (`isFleetChangedFrame`) |
| V7 Error Handling and Logging | yes | Do not render raw `Error.message` to members beyond existing patterns; API failure text shown as sanitized `ApiText` |
| V6 Cryptography | no | — |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| XSS via API prose (failure reasons, audit summaries, diagnostic output) | Tampering | Render as React text children only; no `dangerouslySetInnerHTML`; API sanitizes via `HostingFailureSanitizer` |
| Prototype pollution / unexpected keys from URL | Tampering | Whitelists and `Map` lookups (pattern already used in `actionVocabulary.ts`) |
| Information disclosure of cost owner or actor identity to view-only members | Information disclosure | Use `ownerFor`; any new actor field must follow a visibility rule decided server-side (Open Question 1) |
| Secrets in copied or downloaded diagnostics | Information disclosure | Redaction is a server responsibility; the UI adds no new data sources, only re-presents existing protocol text |
| Clipboard misuse | Tampering | Copy only on explicit user activation of `CopyButton`; never auto-copy |

## Sources

### Primary (HIGH confidence)
- Codebase files read this session: `ServerWorkspacePage.tsx`, `types.ts`, `serverState.ts`,
  `FleetLiveUpdatesProvider.tsx`, `useFleetLiveUpdates.ts`, `ServerLogsPanel.tsx`, `workspaceModel.ts`,
  `HostingAuditFeed.tsx`, `actionVocabulary.ts`, `costConfirmationModel.ts`, `useCostConfirmation.ts`,
  `services.ts` (keys), `updateStream.ts` (status type), `generated/api.ts` (enums and DTOs),
  `FleetChangeInterceptor.cs`, `GameServerProjectionService.cs` (operations, job projection, pending statuses),
  `LifecycleJobQueue.cs`, `LifecycleJobExecutor.cs`, `WorkerProtocolService.cs`, parity matrix and its test,
  CSS guard, single-owner guard, `package.json`, `docs/features/react-ui-beta.md`.

### Secondary (MEDIUM confidence)
- TanStack Query v5 `useQuery` reference (refetchInterval function, defaults) — tanstack.com/query/v5/docs/framework/react/reference/useQuery
- TanStack Query v5 `QueryClient.invalidateQueries` (refetchType fallback `'active'`) — tanstack.com/query/v5/docs/reference/QueryClient
- MDN `aria-current` — developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current
- MDN `Clipboard.writeText` — developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText

### Tertiary (LOW confidence)
- UX recommendations (stage names, dedup rule, sticky height threshold, failure visibility) — reasoning, to be
  settled in UI-SPEC.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; versions read from `package.json`.
- Architecture: HIGH for integration points (verified code), MEDIUM for the listener API shape (recommended design).
- Pitfalls: HIGH — each traced to a specific file and line.
- Activity/actor: LOW until Open Question 1 is answered.

**Research date:** 2026-11-19
**Valid until:** 2026-12-19 (stable internal codebase; re-check if Phase 64 lands changes to `ServerWorkspacePage.tsx` first)

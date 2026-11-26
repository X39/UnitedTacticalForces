# Phase 63: Workspace Shell & Activity - Context

**Gathered:** 2026-11-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Opening a server gives a persistent header (state, owner, cost strip, endpoint with copy, primary action), connected
deep-linkable section navigation with breadcrumbs, honest progress and failure feedback for lifecycle jobs, one merged
activity timeline, and the plain-words cost-stop vocabulary. Requirements: WSP-01..06, CST-01, CST-02, ACT-01, ACT-02.

The shell reserves named slots that later phases fill: the accrued-cost figure in the cost strip (Phase 66), a metrics
area on Overview (Phase 67), and an automation settings area (Phase 68). Scope is `sources/ReactApp` only;
`sources/Common/Ui/routes.json` stays byte-identical (in-page state uses search params). Consumes Phase 61/62
foundations (`deriveServerState`, `ServerStateBadge`, `CostIndicator`, `useCostConfirmation`, `UnavailableReason`,
`--utf-*` tokens, React-owned EN/DE JSON, the fleet WebSocket client). Does not build create (64), change flows (65),
accrued cost (66), metrics (67), or automation (68).

</domain>

<decisions>
## Implementation Decisions

### Section structure (WSP-02, WSP-03)
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

### Header and cost strip (WSP-01, CST-01)
- **D-04:** **Compact sticky header.** A slim sticky bar (server name, state badge, primary action) is always visible in
  every section. The full block (owner, cost strip, endpoint with copy) scrolls away, and the compact bar remains. The
  primary action uses `useCostConfirmation().request({ serverId, actionId, source: "workspace-header" })`.
- **D-05:** The cost strip shows cost owner, hourly and monthly rate, and whether the server is costing now (via
  `CostIndicator` and `deriveServerState`; a server that may still cost is never labelled off).
- **D-06:** The reserved accrued-cost slot is **visible** before Phase 66 and shows a muted "not available yet" line
  (never a fake zero or invented figure). Phase 66 replaces the text with the real value.

### Live updates, progress, failure (WSP-04, WSP-05)
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

### Activity timeline (ACT-01, ACT-02)
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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior phase contracts
- `.planning/phases/61-foundations-localization-member-access/61-CONTEXT.md` - State model, cost confirmation, disabled-with-reason, locale ownership, tokens.
- `.planning/phases/61-foundations-localization-member-access/61-UI-SPEC.md` - Authoritative design/copy contract.
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - Rows to update for moved tabs.
- `.planning/phases/62-fleet-quick-actions/62-CONTEXT.md` - Fleet decisions and the WebSocket client (D-13..D-17) reused by the workspace.

### Requirements and roadmap
- `.planning/ROADMAP.md` - Phase 63 goal, success criteria, Cross-Phase Constraints (route manifest, reuse, localization, parity, a11y).
- `.planning/REQUIREMENTS.md` - WSP-01..06, CST-01/02, ACT-01/02 (WSP-02 needs amending per D-02).
- `.planning/milestones/v1.12-phases/60-ux-verification-fake-states-and-guardrails/60-UI-REVIEW.md` - UI-60-001 nav clipping finding.

### Project rules
- `AGENTS.md`, `.planning/codebase/CONVENTIONS.md`, `.planning/codebase/TESTING.md`
- `docs/features/react-ui-beta.md`, `docs/features/cloud-based-game-server-hosting/`

### Code touchpoints
- `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx` - Tabs, `workspaceTabIds`, 5s polling, `JobList`.
- `sources/ReactApp/src/features/hosting/ServerLogsPanel.tsx`, `HostingAuditFeed.tsx`, `ServerRuntimePanel.tsx`, `ServerConfigurationPanel.tsx`, `ServerSecretsPanel.tsx`, `ServerFilesPanel.tsx`
- `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx`, `useFleetLiveUpdates.ts` - Reusable WebSocket client.
- `sources/ReactApp/src/features/hosting/workspaceModel.ts`, `serverState.ts`, `types.ts`
- `sources/ReactApp/src/components/server/*` - Shared state, cost, confirmation, unavailable-reason components.
- `sources/Common/Ui/routes.json` - Frozen route manifest.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `useCostConfirmation`, `CostConfirmDialog`, `ServerStateBadge`, `CostIndicator`, `UnavailableReason`, `ActionButton`/`ActionLink`, `StatePanel`, `PageHeader`.
- `FleetLiveUpdatesProvider` / `useFleetLiveUpdates`: the Phase 62 socket client and reconnect logic.
- `buildCommandProtocol`, `protocolEntries`, `textDownloadUrl` (workspaceModel): command protocol, copy, download.
- `HostingAuditFeed`, `JobList` with retry/skip handlers.

### Established Patterns
- Section state in `?tab=` search param via `selectTab` (history replace for overview).
- Flat i18next keys (`Server_{Area}_{Item}`), EN/DE parity gate, style guard (no raw hex or pixel literals in `components/server/*`).
- React Query keys in `hostingQueryKeys`; mutations invalidate server, jobs, and fleet queries.

### Integration Points
- `ServerWorkspacePage` splits into header shell + section router + per-section components; jobs, logs, and audit data merge into one Activity model.
- Phase 66/67/68 slots: accrued-cost line in the cost strip, metrics area on Overview, automation area in Settings.

</code_context>

<specifics>
## Specific Ideas

- Command Center feel stays focused and less vibrant (Phase 62 D-01); the workspace should read as a working tool.
- The accrued slot is intentionally visible and honest ("not available yet"), not hidden.

</specifics>

<deferred>
## Deferred Ideas

- Grouping timeline entries under their lifecycle job: not chosen; revisit only if the flat timeline proves hard to diagnose with.
- Breadcrumb and empty-slot visuals were not discussed; left to UI-SPEC and planning.

</deferred>

---

*Phase: 63-Workspace Shell & Activity*
*Context gathered: 2026-11-19*

## Post-Research Decisions (plan-phase 63, 2026-11-19)

- **D-10 actor data:** Add a minimal API field exposing the actor (`LifecycleJob.RequestedByFk`, `HostingAuditRecord.ActorFk`) on the job and operation DTOs, like Phase 62-02's `CostOwnerUserId`. Regenerate the OpenAPI snapshot and TypeScript types. AGENTS.md now allows frontend-driven API changes without separate approval.
- **Legacy Blazor WebApp:** Do not update it, except where an API change would break its API compatibility.
- **Requirement text:** Amend WSP-02 and roadmap success criterion 2 to match D-02 (old `?tab=` links are not kept as a requirement).

# United Tactical Forces - Cloud Game Server Orchestration

## What This Is

United Tactical Forces is an ASP.NET Core API with two web clients for managing community operations: users, roles,
events, mod packs, terrain, wiki content, update streams, and cloud game-server hosting. The classic Blazor WebAssembly
client (`sources/WebApp`) is the legacy interface; the React client (`sources/ReactApp`, served at `/next/`) is the
current, actively developed interface. Hosting is a persistent control plane with one dedicated external worker per
active deployment; Hetzner is the first cloud runtime provider.

## Core Value

Authorized users can operate cloud game servers through durable, auditable lifecycle jobs with a clearly assigned cost
owner, including reliable recovery and verified provider cleanup.

## Current State

**Shipped:** v2.1 Server Experience Reimagined (2026-11-26), phases 61–71, 201 plans. The React server section was
rebuilt end to end: fleet with quick actions and live state, guided server creation, a server workspace (overview,
activity, settings with Mods, Configuration, Credentials, Automation, Access, Cost owner and Server record panels),
accrued cost and cost alerts, server metrics, auto-stop and schedules, React-owned en/de localization, a consistency
and accessibility pass with an audited screen registry, drawn form controls, and per-server access grants and
cost-owner transfer. Closed as an override closeout: the milestone-end human and real-environment pass (visual
baselines VER-03, seeded-stack UAT, screen reader, real devices, Hetzner/nginx/Discord) is recorded as follow-up work.

## Next Milestone Goals

Not yet defined (start with `/gsd-new-milestone`). Carried-over candidates:
- Milestone-end verification pass deferred from v2.1 (see `milestones/v2.1-MILESTONE-AUDIT.md` tech debt)
- Upper bound for a scheduled start waiting on an in-flight stop (68 review WR-01)
- Legacy raw colors and gold accent in `styles.css` / `hosting.css`; raw-input admin event editors
- Pending todos: WebSocket update stream review; profile badge replaced by a notification count and popup

<details>
<summary>v2.1 milestone brief (archived)</summary>

**Goal:** Reimagine the React server operations UI so any member can start a new server or change an existing one
quickly, with clear state, cost, and next steps, and bring the rest of the React app to a consistent visual standard.
Game-agnostic: Arma 3 and Minecraft are examples only.

**Target features:**
- Full redesign of the server section in the React UI (fleet, create, workspace, configuration, mods, logs, cost/teardown)
- Member-first flows: start a new (usually disabled-by-default) server such as an Arma 3 test server, and update an
  existing server such as adding a mod to a Minecraft server
- Findable actions, connected navigation, and unambiguous server state (status, owner, cost, next action)
- Light consistency pass over the rest of the React app (layout, navigation, headers, spacing, shared components)
- React-owned localization (migrate off the Blazor RESX files)
- Backend-backed additions explicitly requested for this milestone: accrued cost and cost alerts (in-app, Discord, email),
  server metrics (players, CPU/memory, uptime, short history), and auto-stop/schedules (idle, max runtime,
  scheduled start/stop, warning before stop)

**Key constraints:** The classic Blazor UI in `sources/WebApp` is outdated, stays as-is, and is not modified. The UI
work lives in `sources/ReactApp`. API, worker, and database changes are allowed whenever a feature needs them (no
separate approval; the current API is not a frozen contract) and must follow the architecture boundaries in AGENTS.md
(control plane owns cost, schedules, and alerts; the worker reports metrics through the worker protocol; no game
execution in the API).

</details>

## Business Context

- **Customer**: Community members and administrators of United Tactical Forces
- **Revenue model**: None; hosting costs are attributed to an explicit cost owner
- **Success metric**: A member completes "start a server" or "change a server's mods/config" without help

## Requirements

### Validated

- ✓ Durable lifecycle jobs, Hetzner provisioning/reconciliation/verified teardown, external worker protocol
- ✓ Generic capability projection and server-scoped cloud planning routes (Arma 3 first implementation) — v1.12
- ✓ Server workspace with session status, owner/cost/cleanup summaries, ranked next actions, Logs tab — v1.12
- ✓ Provider teardown as the explicit cost-stop action — v1.12
- ✓ React beta UI certified for all Blazor route patterns, served at `/next/` — pre-v2.1
- ✓ Reimagined React server UI: fleet, guided creation, workspace, change flow, activity — v2.1
- ✓ Accrued cost and cost alerts (in-app, Discord, email), server metrics, auto-stop and schedules — v2.1
- ✓ React-owned en/de localization; consistency, accessibility and verification pass — v2.1
- ✓ Per-server access grants and cost-owner transfer in React — v2.1

### Active

- [ ] Next milestone requirements (defined by `/gsd-new-milestone`)

### Out of Scope

- Changes to `sources/WebApp` (classic Blazor UI) — outdated, retained only as rollback variant
- API, worker, or provider changes that no v2.1 feature needs (feature-driven changes are in scope)
- New game implementations (e.g. Minecraft) — the UI is game-agnostic and driven by server capabilities
- Mod catalog search/install, undo/revert, server-defined presets, restart-required drift badge — deferred
- Full redesign of non-server pages (events, wiki, mod packs, users) — light consistency fixes only

## Context

- Significant work landed outside GSD since 2026-07-22 (CI release pipeline, containerized production deploy,
  configuration profiles, Steam worker fixes, Hetzner contract fixes). Planning is based on `.planning/codebase/`.
- React app: Vite, React 19, Fluent UI React v9, TanStack Query, react-router 7, react-i18next, generated API types
  from `openapi/utf-api.json`. Server UI lives in `sources/ReactApp/src/features/hosting`. See `docs/features/react-ui-beta.md`.
- Known debt: deferred milestone-end verification items (v2.1 audit) and acknowledged items in STATE.md `## Deferred Items`.
- The legacy Kiota client is not regenerated for v2.1 API additions; the legacy WebApp builds and does not use them.
- v2.1 removed `GET /game-servers/{id}/actions` (breaking; actions come from the server detail).
- Members typically see most servers disabled; starting a server is a deliberate, cost-bearing action.

## Constraints

- **Architecture**: Keep API control plane, providers, hosting contracts, worker, and game implementations separated;
  game-process execution never moves into the API (see AGENTS.md and `docs/features/cloud-based-game-server-hosting/`).
- **Client**: React only for UI; no edits to `sources/WebApp` or generated Kiota output.
- **Security**: Steam credentials, provider tokens, bootstrap and worker credentials never reach frontend clients or logs.
- **Localization**: User-facing text comes from React-owned locale files (English and German), never literals.
- **Standards**: Reuse shared components (`src/components`); no raw inputs posing as buttons.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| React UI only for v2.1 | Classic Blazor UI is outdated | ✓ Good — WebApp untouched except build compatibility |
| Migrate React localization to React-owned JSON | RESX lives in the outdated UI and falls back silently | ✓ Good — parity and unused-key checks in CI |
| Pull cost alerts, metrics, auto-stop into v2.1 | Explicit user request; backend work follows AGENTS.md boundaries | ✓ Shipped; real-worker and Hetzner checks deferred |
| Game-agnostic, capability-driven UI | Arma 3/Minecraft are examples only | ✓ Good — creation and actions driven by capabilities |
| Servers first, light touch elsewhere | Server UX is the main pain point | ✓ Good |
| Member-first audience | Any member starts/updates servers | ⚠️ Revisit after the milestone-end UAT with real members |
| Scheduled start waits for an in-flight stop instead of skipping (Phase 68) | UAT G-68-3: a start due during a stop was lost for good | ✓ Shipped; no time bound yet (review WR-01, open) |
| Shared button wraps long labels; no page-local nowrap overrides (Phase 68) | UAT G-68-1: German cost-stop label overflowed at 320 px | ✓ Shipped, guarded by a stylesheet test |
| Drawn form controls and collapsible Settings panels (Phase 70) | User dislikes native selects and details | ✓ Shipped |
| Access and cost-owner UI as a gap-closure phase (Phase 71) | Audit found endpoints without a React consumer | ✓ Shipped; non-admins may only grant permissions they hold |
| Remove redundant GET server actions endpoint | Same data as the server detail, no caller | ✓ Done (breaking change with migration note) |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-11-26 after v2.1 milestone*

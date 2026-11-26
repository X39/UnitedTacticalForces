# UI Review Findings - 2026-06-22

## Scope

Reviewed the active Blazor WebAssembly UI in `sources/WebApp`, focusing on style, information architecture, workflow function, safety, and readiness for the durable game-server/control-plane direction.

Reviewed surfaces:

- App shell and navigation: `App.razor`, `Shared/MainLayout.razor`, global CSS.
- Public/community pages: home, GDPR, event pages.
- Asset pages: mod packs and terrains.
- Operational pages: server fleet and cloud operations.
- Shared UI pieces: dialogs/panels, image/text wrappers, repository-driven loading patterns.

This is a source-driven UI review. I did not modify application code, regenerate clients, or run an authenticated browser session. A follow-up implementation pass should include rendered visual QA with seeded admin/operator data.

## Functionality Analysis

The application currently exposes three different products through one UI:

1. Public community website: landing content, community screenshots, Teamspeak/Discord links, GDPR content.
2. Event participation tool: upcoming events, event archive, event details, slot assignment, modpack download context.
3. Operations console: local game-server lifecycle actions, billing ownership, cloud planning, preflight approval, policy gates, workers, jobs, logs, cost records, reconciliation, provider cleanup, and admin configuration.

The first two are community-facing and benefit from a clearer, more visual experience. The third is an operational control plane and should feel dense, auditable, and safe. The current implementation uses Fluent components across all three, but the page structures do not yet express those different jobs.

The operational functionality is the strongest part of the UI. The `/servers` page contains guided cloud setup, preflight evidence, dry-run review, policy decisions, runtime state, provider teardown, jobs, logs, ownership, workers, and stuck-job cleanup. The `/servers/cloud` page is even more complete as an admin dashboard, with sessions, plans, resources, estimates, reconciliation, policies, configurations, and provider metadata.

The main weakness is not missing capability. It is that capability is exposed as long, implementation-shaped pages with many equal-weight controls. That increases operator load and makes destructive or cost-generating decisions harder to distinguish from routine actions.

## Verdict

The UI is functionally broad but not yet coherent as a product surface. It is usable by developers or experienced admins who already understand the domain model, but it is too dense and inconsistent for the durable billing/control-plane milestone where ownership, audit, recovery, and cost safety must be obvious.

The right direction is a structural redesign, not just a visual polish pass:

- Keep Fluent UI, but introduce a real product shell, shared page primitives, and token-based styling.
- Split community/event browsing from operations-console workflows.
- Rebuild server and cloud pages around selected-object workspaces, evidence checklists, action review, and persistent billing/cost context.
- Make high-risk actions require explicit confirmation and show the active owner, cost impact, session, and cleanup consequence before submission.

## UI Quality Score

| Area | Score | Notes |
| --- | ---: | --- |
| Copywriting | 2 / 4 | Mixed German/English, hardcoded labels, stale notices, technical labels exposed directly. |
| Visual hierarchy | 2 / 4 | Fluent controls are present, but pages rely on large tables, stacked cards, and inline styling. |
| Color system | 2 / 4 | Status colors exist but are duplicated; many hard-coded light backgrounds undermine theme support. |
| Typography | 2 / 4 | Headings and dense metadata use similar weight; operational pages need clearer scan layers. |
| Spacing/layout | 2 / 4 | Good attempts at grids, but server/cloud pages are very dense and often card-heavy. |
| Experience design | 2 / 4 | Strong features, but workflow risk, validation, empty states, and confirmations are inconsistent. |

Overall: 12 / 24. The foundation is workable, but the UI needs a product-level IA and interaction pass before it becomes a reliable operations console.

## Strengths

- Authentication-aware navigation is already present in `Shared/MainLayout.razor`.
- Fluent UI Blazor gives a viable component base and consistent primitive controls.
- Event creation has a meaningful wizard flow with scenario data, mission data, description preview, and final details.
- The cloud lifecycle domain is represented deeply: billing owner, preflight evidence, policy gates, dry-run plans, workers, jobs, logs, runtime status, cost records, reconciliation, and teardown.
- `/servers/cloud` already uses a better master/detail direction than `/servers`, with tabs and a sticky detail panel.
- Critical safety concepts exist in the domain model, especially preflight approval, policy gate review, and provider teardown gating.

## Priority Findings

### P1 - High-risk cloud/server actions are not consistently reviewed before execution

Server row actions directly queue lifecycle jobs from icon buttons in `sources/WebApp/Pages/Servers/Index.razor:189`, including delete at `sources/WebApp/Pages/Servers/Index.razor:236`. Provider teardown has a confirmation flow in `sources/WebApp/Pages/Servers/Index.razor:3114`, but lower-level delete and lifecycle actions do not follow the same explicit action-review model.

Cloud admin provisioning and teardown in `sources/WebApp/Pages/Servers/Cloud.razor:2350` and `sources/WebApp/Pages/Servers/Cloud.razor:2376` call repository actions directly once enabled. For a cost-generating/control-plane UI, "enabled" is not enough. Provisioning and teardown should use a standardized confirmation pattern that shows session, server, owner, estimated cost, resources affected, policy decision, and cleanup consequence.

Recommendation: introduce an `OperationReviewDialog` or action drawer for all cost-generating, destructive, or state-changing lifecycle operations.

### P1 - Operational pages are implementation-shaped rather than workflow-shaped

`sources/WebApp/Pages/Servers/Index.razor` is over 8,000 lines and combines creation, fleet list, guided cloud setup, preflight, dry-run plan review, policy gate review, cloud action results, runtime state, teardown, logs, ownership history, workers, and stuck-job cleanup in one page.

`sources/WebApp/Pages/Servers/Cloud.razor` is also large, but its tabbed master/detail model is closer to the right direction. The server page should be decomposed into a selected-server workspace with tabs such as Overview, Runtime, Cloud, Jobs/Logs, Ownership/Audit, and Danger Zone.

Recommendation: make `/servers` the operator-focused Server Fleet workspace, and make `/servers/cloud` the admin/reconciliation workspace. Share components for status, cost, policy, evidence, jobs, and action review.

### P1 - Theme support is undermined by hard-coded light colors

The app sets Fluent themes in `sources/WebApp/App.razor`, but operational CSS contains many hard-coded light backgrounds. For example, `sources/WebApp/Pages/Servers/Cloud.razor.css:41` sets summary cards to `#f7f9fb`, `sources/WebApp/Pages/Servers/Cloud.razor.css:91` sets panels to `#ffffff`, and `sources/WebApp/Pages/Servers/Cloud.razor.css:106` sets tab backgrounds to `#f7f9fb`.

Recommendation: define app-level semantic tokens for surface, muted surface, stroke, status fill, status foreground, danger fill, warning fill, and focus. Use Fluent tokens and CSS variables instead of fixed colors.

### P2 - Some visible controls are dead or incomplete

Mod pack edit and delete buttons are visible but intentionally not wired: `sources/WebApp/Pages/Data/ModPacks/All/Index.razor:32` and `sources/WebApp/Pages/Data/ModPacks/All/Index.razor:37`. The event archive has an edit icon button without an action in `sources/WebApp/Pages/Events/All/Index.razor`.

Recommendation: remove incomplete controls from production UI or move them behind disabled states with clear tooltips only if implementation is scheduled immediately.

### P2 - Dialog validation can fail silently after the panel closes

`CreateModPackPanel.OpenAsync` validates after the panel result returns and then returns null on invalid data at `sources/WebApp/Panels/CreateModPackPanel.razor.cs:89`. `CreateTerrainPanel.OpenAsync` does the same at `sources/WebApp/Panels/CreateTerrainPanel.razor.cs:62`. From the user's perspective, clicking Create can close the panel with no inline validation or recovery.

Recommendation: keep validation inside the panel, prevent close on invalid data, and show field-level errors before submitting.

### P2 - Terrain delete does not refresh the list after success

`DeleteTerrainAsync` deletes the terrain in `sources/WebApp/Pages/Data/Terrains/All/Index.razor:114` but does not reset `_terrainsCount` or refresh the grid afterward. The row may remain visible until a later reload.

Recommendation: match create/modify behavior by resetting count, refreshing the data grid, and showing a success/failure message.

### P2 - Localization and copy are inconsistent

The UI mixes localized resources, hardcoded English, and German page content. Examples include hardcoded "Cloud operations" in the main navigation, hardcoded operational action labels in server/cloud pages, and German public copy on the home page. Some operational notices say live Hetzner/provisioning is deferred while the same page exposes provisioning and teardown actions, which creates uncertainty about what is real.

Recommendation: define copy by audience. Public/community copy can remain German-first if that is the community language; operations copy should be concise, localized consistently, and remove stale "deferred" messages where the action exists.

### P2 - Accessibility coverage is incomplete

Many image usages omit meaningful alternate text, and `FluentImage` defaults to an empty `Alt`. Several icon-only buttons rely on `Title`, but should also have accessible labels. Dense tables and status badges should use text, not color alone, to communicate state.

Recommendation: add accessible-label standards to shared button, image, badge, and action components.

### P2 - Event pages need clearer states and progressive disclosure

Upcoming events use fixed-size cards, no strong empty state, and limited filter/sort controls. Event details rely heavily on inline layout and expose slot groups through accordion rows that can become hard to scan. The archive grid fetches owner, host, modpack, and terrain data through async template providers, which may feel delayed and inconsistent at scale.

Recommendation: redesign events around an event board, clear mission cards, slot-progress indicators, and a two-column event detail with mission brief and roster/action panel.

### P2 - Data loading patterns can degrade perceived performance

Several grids use per-row async lookups and cache dictionaries in page code. This works for small data but creates scattered loading behavior and many repository calls. The UI would benefit from API DTOs or list endpoints that include the display fields required for the grid.

Recommendation: when redesigning high-traffic grids, define list DTOs that include display owner/host/terrain/modpack summaries.

### P3 - Public home page feels disconnected from the operations product

`sources/WebApp/Pages/Index.razor` uses inline CSS, duplicated `.content` rules, skewed screenshot cards, and marketing sections that do not align visually with the rest of the app. It is acceptable as a placeholder but does not establish a strong brand or route users into event participation.

Recommendation: turn the home page into a community front door: large real operation imagery, next event preview, join links, server/community status, and a simpler gallery.

### P3 - Visual style is inconsistent across pages

The app alternates between plain Fluent tables, inline-styled public pages, dense operational cards, nested detail sections, and duplicated status badge systems. The gold brand accent is configured in the theme but not used as a deliberate design element.

Recommendation: create shared page primitives and a small visual language: page header, attention summary, status badge, metadata list, command bar, table shell, empty state, form panel, evidence checklist, and operation review dialog.

## Page-Level Notes

| Area | Current Function | Main UI Issue | Improvement Direction |
| --- | --- | --- | --- |
| App shell | Auth-aware nav and footer links. | Flat top nav becomes crowded and mixes public, asset, and operations destinations. | Use grouped navigation with Community, Assets, and Operations. |
| Home | Public recruitment/community page. | Inline CSS and dated gallery treatment. | Build a community landing surface with real imagery and next-event entry point. |
| Upcoming events | Shows event cards and slot navigation. | Fixed cards, weak empty states, limited scan metadata. | Event board with date, image, terrain, modpack, host, slot progress, and status. |
| Event detail | Mission brief and slot assignment. | Inline layout and dense accordions. | Two-column layout: mission brief/media left, roster and user's slot action right. |
| Event archive | Admin grid and create action. | Dead edit action and per-row async loading. | Archive table with complete row DTOs, filters, and working actions. |
| Mod packs | List, open detail, create panel. | Dead edit/delete controls; validation can fail after close. | Asset library with working actions and in-panel validation. |
| Terrains | List, create, modify, delete. | Delete does not refresh; panel validation is minimal. | Same asset pattern as mod packs, with image thumbnails and clear feedback. |
| Servers | Full server lifecycle and cloud workflow. | Too many workflows on one page; high-risk actions blend into row icons. | Server Fleet workspace with selected-server detail tabs and reviewed actions. |
| Cloud operations | Admin dashboard for sessions/plans/resources/policy/config. | Strong data coverage, but too many equal summary cards and direct high-risk actions. | Cloud Command Center with attention-first summary, master/detail tabs, and action review. |
| GDPR | Legal/privacy content. | Long accordion content, mixed language quality. | Keep simple; align typography and external-link styling. |

## Design Risks To Address First

1. Operators must always see who owns billing responsibility before starting, provisioning, transferring, stopping, or cleaning up a hosting session.
2. "Runtime stopped" and "provider cost stopped" must be visually distinct states.
3. Teardown and delete actions need a consistent action-review pattern.
4. Policy, preflight, and cost information should be summarized as gates, not scattered as tables.
5. Dark and light modes must use the same semantic token system.
6. Empty, loading, failed, stale, and unauthorized states must be designed intentionally for every operational page.


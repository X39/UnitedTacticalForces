# UI Redesign Direction and Implementation Plan - 2026-06-22

## Design Direction

Build the app as two connected experiences:

1. Community front door: public pages for recruitment, upcoming events, event details, modpack context, and community links.
2. Operations console: authenticated server, cloud, worker, billing, policy, and audit workflows for authorized operators.

The visual direction should be calm, tactical, and operational without becoming decorative military cosplay. Use the existing UTF gold as a restrained brand accent, neutral surfaces for data density, and clear semantic colors for operational state.

The redesign should not try to make every page look like a marketing site. Public pages can use imagery and richer layout. Operational pages should prioritize scanning, comparison, evidence, ownership, and safe action.

Additional landing-page exploration: `.planning/ui-reviews/2026-06-22-landing-page-concept-exploration.md`.

Implementation handoff contract for the chosen dual-shell direction: `.planning/ui-reviews/2026-06-22-cinematic-dual-shell-ui-contract.md`.

## Core Product Principles

- Billing owner is always visible for hosting sessions and cost-generating actions.
- Cost-generating, destructive, and provider-cleanup actions are reviewed before execution.
- Runtime state, provider state, policy state, and billing state are separate visual concepts.
- Tables are for comparison; selected-object workspaces are for decisions.
- Status colors are semantic and backed by text labels.
- Dark and light themes use shared tokens, not hard-coded color values.
- Public pages are German/community-friendly; operations copy is concise, consistent, and localizable.

## Information Architecture

### Primary Navigation

Use grouped navigation instead of one flat list.

Community:

- Home
- Upcoming Events
- Event Archive
- GDPR

Assets:

- Mod Packs
- Terrains

Operations:

- Server Fleet
- Cloud Operations
- Cloud Configuration
- Workers and Jobs, either as a Cloud Operations tab or future standalone page

On desktop, this can be a left rail with a compact top bar for identity/theme/login. On mobile, collapse groups into a drawer. Keep the TeamSpeak and Discord links in a utility area rather than competing with primary routes.

## Visual System

### Palette

- Background: neutral app canvas.
- Surface: tokenized panel background for cards/tables/forms.
- Muted surface: subtle summary strips and tab bars.
- Brand accent: UTF gold for selected nav, important brand marks, and primary community accents.
- Interactive: Fluent accent/focus color, distinct from warning and status colors.
- Success: completed/running healthy.
- Warning: stale, approval required, partial cleanup, cost still active.
- Danger: failed, destructive, orphaned, deletion/teardown.
- Info: queued, provisioning, informational policy state.

Avoid page-wide single-hue themes. Do not rely on blue for every operational state.

### Typography

- Use compact, legible headings in operational pages.
- Use small uppercase labels only for metadata labels and summary-card labels.
- Keep table text dense but readable.
- Reserve large type for public/community hero and page titles, not admin panels.

### Layout

- Use full-width page bands and constrained inner content for public pages.
- Use master/detail workspaces for operational pages.
- Avoid cards inside cards. Repeated items can be cards; page sections should usually be unframed regions or single-level panels.
- Use stable table/action column widths so icons and labels do not shift during loading.

## Page Concepts

### Home

Purpose: introduce United Tactical Forces and drive users to the next event or community join action.

Layout:

- First viewport: full-bleed real/generated operation image with UTF name, concise value copy, and actions for upcoming events, Discord, and TeamSpeak.
- Next-event strip: date, terrain, modpack, slots available, and "View slots".
- Community sections: realistic operation gallery, what to expect, required setup, and links.

Use imagery here. This is the one public page where a strong generated or curated visual asset is useful.

### Upcoming Events

Purpose: help members decide what to attend.

Layout:

- Event board with filter chips: upcoming, open slots, training, operation, hosted by me.
- Cards show mission image, date/time, title, terrain, modpack, host, slot count, and user's acceptance state.
- Empty state tells the user whether no events exist or filters are hiding them.

### Event Detail and Slots

Purpose: understand the mission and pick or change a slot.

Layout:

- Left column: image, title, date, terrain, modpack, mission description/briefing.
- Right column: sticky "Your attendance" and slot action panel.
- Roster below or beside it: grouped slots with role, assigned user, visibility, and available state.
- Admin users get a command bar for edit, publish/visibility, and slot management.

### Event Archive

Purpose: admin and historical browsing.

Layout:

- Table with complete row data: date, title, terrain, modpack, owner, host, status, slots.
- Filters for time range, terrain, owner, and text search.
- Working row actions only.

### Asset Library: Mod Packs and Terrains

Purpose: manage reusable operation assets.

Layout:

- Shared asset library shell with search, filters, and create action.
- Mod packs: title, latest revision, composition marker, owner, last update, open/download.
- Terrains: thumbnail, title, owner, usage count if available, modify/delete.
- Create/edit panels keep validation in-panel and do not close on invalid input.

### Server Fleet

Purpose: day-to-day operator workspace for local and cloud-backed game servers.

Layout:

- Page header: total servers, running servers, active jobs, stale workers, cost warnings.
- Left/main table: server, lifecycle state, runtime state, billing owner, latest job, cloud session, attention reason.
- Right selected-server panel or detail route with tabs:
  - Overview: identity, owner, controller, current lifecycle state.
  - Runtime: start/stop/restart/update/install/logs with status and logs.
  - Cloud: session, provider state, preflight, dry-run, cost, policy gate.
  - Jobs and Logs: lifecycle jobs, worker assignment, recent logs.
  - Ownership and Audit: owner history, cost responsibility audit.
  - Danger Zone: delete server, provider teardown, forced cleanup.

Actions:

- Normal runtime actions can be one-click only when the cost/provider impact is low and state is clear.
- Delete, provider teardown, provisioning, cleanup, and ownership transfer use the operation review pattern.

### Guided Cloud Provisioning

Purpose: make live cloud provisioning safe and understandable.

Layout:

- Dedicated wizard/drawer, not embedded as one more section in the server detail.
- Steps:
  1. Server and billing owner.
  2. Runtime package: modpack, mission/runtime profile, server title.
  3. Provider shape: server type, location, image, storage, firewall/RCON/SSH rules.
  4. Dry run and cost estimate.
  5. Preflight approvals and policy gate.
  6. Provision review.

The final review screen must explicitly state when billing starts, what resources will be created, who owns the cost, and how cleanup will be performed.

### Cloud Operations

Purpose: admin command center for sessions, plans, resources, estimates, reconciliation, policy, and provider metadata.

Layout:

- Attention row first: active cost, cleanup required, orphan findings, stale metadata, policy approvals, stale workers.
- Tab workspace:
  - Sessions
  - Plans
  - Resources
  - Estimates
  - Reconciliation
  - Policies
  - Configurations
  - Metadata
- Sticky detail panel for the selected row, with contextual actions and evidence.

Actions:

- Provision and teardown from this page must open the same operation review dialog used by Server Fleet.
- Row buttons should select and review; direct destructive actions should not live inside dense tables.

### Cloud Configuration

Purpose: maintain templates, runtime profiles, and mission references.

Layout:

- Three configuration lanes or tabs: Templates, Runtime Profiles, Mission References.
- List on the left, editor on the right.
- Create/edit/disable reasons are visible and validated.
- Disable actions require confirmation when the config is referenced by active plans or sessions.

## Shared Components To Build

- `AppShell`: grouped navigation, identity area, theme toggle, utility links.
- `PageHeader`: title, subtitle, primary command, status counts.
- `AttentionSummary`: compact warnings for active jobs, cost, stale workers, cleanup, policy approvals.
- `StatusBadge`: shared semantic statuses with text and icon support.
- `CommandBar`: stable action layout with primary, secondary, and danger zones.
- `DataTableShell`: table wrapper with search/filter/loading/empty/error states.
- `MasterDetailWorkspace`: list plus sticky detail panel, responsive drawer on mobile.
- `OperationReviewDialog`: confirmation for provisioning, teardown, delete, cleanup, and ownership changes.
- `EvidenceChecklist`: preflight/policy/cost requirements with pass/warn/fail states.
- `CostSummary`: current cost, estimated next cost, owner, last snapshot, confidence.
- `JobTimeline`: queued/running/completed/failed lifecycle jobs and worker assignment.
- `FormPanel`: right-side create/edit panels with inline validation and dirty-state handling.
- `ImageTile`: consistent event/terrain/modpack image display with required alt text.
- `EmptyState`: standard no-data, no-permission, filtered-empty, and failed-load states.

## Interaction Standards

- Any action that creates provider resources, deletes records, tears down provider resources, changes billing owner, or may leave billable resources behind must use `OperationReviewDialog`.
- Disabled actions should expose a reason through tooltip/help text.
- Success and failure messages should stay near the action context and also be available in job/audit history.
- Tables should not contain hidden dead buttons. If a feature is not implemented, do not show its icon.
- Forms should not close on invalid input.
- Every image must have meaningful alt text or be explicitly decorative.
- Every icon-only action must have an accessible label.

## Implementation Plan

### Phase 1 - Design Tokens and Shell

Deliverables:

- App-level CSS tokens for surfaces, text, strokes, status colors, focus, spacing, and shadows.
- Grouped `AppShell` navigation.
- Shared `PageHeader`, `StatusBadge`, `CommandBar`, and `EmptyState`.
- Remove hard-coded light backgrounds from operational CSS.

Verification:

- Light and dark modes render readable UI.
- Navigation fits desktop and mobile without overlap.

### Phase 2 - Public and Event Experience

Deliverables:

- Redesigned home page with real/generated hero image and next-event entry.
- Event board for upcoming events.
- Event detail layout with mission brief, roster, and sticky attendance action.
- Event archive table with working actions only.

Verification:

- Empty, loading, unauthenticated, authenticated, and admin states are covered.
- Event cards and slot rows do not overflow on mobile.

### Phase 3 - Asset Library

Deliverables:

- Shared asset library shell for mod packs and terrains.
- Mod pack dead edit/delete controls removed or implemented.
- Terrain delete refreshes the list after success.
- Create/edit panels validate inline before closing.

Verification:

- Create, invalid create, edit, delete, cancel, and failed repository calls have visible outcomes.

### Phase 4 - Server Fleet Workspace

Deliverables:

- Server Fleet list with selected-server detail tabs.
- Runtime actions grouped into a command bar.
- Cloud, jobs/logs, ownership/audit, and danger-zone sections moved into tabs.
- Shared action review for delete, cleanup, provider teardown, provisioning, and ownership-sensitive actions.

Verification:

- Billing owner is visible on every hosting/session action.
- Runtime stopped and provider cost stopped are visually distinct.
- Operator can find active jobs, stale workers, and cleanup risks within one scan.

### Phase 5 - Cloud Operations Command Center

Deliverables:

- Attention-first cloud summary.
- Tab workspace using shared `MasterDetailWorkspace`.
- Sessions, plans, resources, estimates, reconciliation, policy, config, and metadata details standardized.
- Provision/teardown actions routed through `OperationReviewDialog`.

Verification:

- Direct destructive row actions are gone.
- Reconciliation findings and cleanup risks are visible before provider actions.

### Phase 6 - Accessibility, Responsive, and Performance QA

Deliverables:

- Accessible labels for icon actions.
- Meaningful image alt text.
- Mobile layouts for home, event detail, server fleet, and cloud operations.
- Rendered Playwright screenshot checks for desktop and mobile.
- Review of grid data-loading DTOs to reduce per-row async lookups where necessary.

Verification:

- No text overlaps at mobile width.
- No unreadable dark-theme panels.
- Keyboard focus and screen-reader labels are present for primary workflows.

## Generated Concept Images

Generated 7 concept images for the redesign phase. They are stored in `.planning/ui-reviews/concepts/`.

1. Public home hero: `.planning/ui-reviews/concepts/01-home-hero-concept.png`
2. Event board: `.planning/ui-reviews/concepts/02-upcoming-events-board-concept.png`
3. Event detail and slots: `.planning/ui-reviews/concepts/03-event-detail-slots-concept.png`
4. Server Fleet workspace: `.planning/ui-reviews/concepts/04-server-fleet-workspace-concept.png`
5. Guided cloud provisioning review: `.planning/ui-reviews/concepts/05-cloud-provisioning-review-concept.png`
6. Cloud Operations command center: `.planning/ui-reviews/concepts/06-cloud-operations-command-center-concept.png`
7. Cloud Configuration editor: `.planning/ui-reviews/concepts/07-cloud-configuration-editor-concept.png`

These are concept/mockup images for design alignment. Treat exact labels, icons, dates, and sample values as placeholder content, not implementation copy. Production operational pages should mostly use real app data and component UI, not decorative generated art. Public/community pages can use generated or curated imagery more directly.

## Acceptance Criteria For The New UI

- Users can distinguish public, event, asset, and operations areas immediately.
- Every cost-generating hosting session shows an owner from creation through cleanup or transfer.
- Every high-risk action shows a review step with owner, session, resources, policy state, and cost/cleanup consequence.
- Server and cloud pages no longer require scrolling through many unrelated sections to complete a common workflow.
- Dark mode and light mode both use semantic tokens and remain readable.
- No visible controls are dead or intentionally unimplemented.
- Validation errors appear before panels close.
- All primary pages have designed loading, empty, failed, and unauthorized states.
- Mobile layouts do not overlap and keep primary actions reachable.

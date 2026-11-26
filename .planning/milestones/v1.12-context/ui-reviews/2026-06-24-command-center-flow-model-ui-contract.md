# Command Center Flow Model UI Contract - 2026-06-24

## Purpose

This document supersedes `.planning/ui-reviews/2026-06-22-cinematic-dual-shell-ui-contract.md`.

It preserves the general United Tactical Forces visual direction while removing the earlier layout pressure
that made many Command Center pages feel like they needed a secondary detail pane.

The new direction is:

> Keep the cinematic Community Shell and operational Command Center Shell, but make most Command Center
> workflows route-based: overview/list, selection/edit, and create stepper.

The shell split is a navigation and product-mode decision. It is not a requirement that every management page
be a split workspace.

## Design Direction

United Tactical Forces should use one shared dark/gold cinematic operations design system:

- Dark neutral application background.
- Charcoal panels for dense information.
- UTF gold for brand, selected navigation, current steps, and primary intent.
- Semantic green, amber, red, and blue for operational state.
- Compact, readable type on management surfaces.
- Richer imagery on public/community pages.

The public side may feel cinematic. The management side must feel operational.

Avoid:

- Background imagery behind tables, forms, logs, policy data, billing data, or audit records.
- Gold as a warning color.
- Blue-dominated enterprise styling.
- Decorative gradient blobs.
- Cards inside cards.
- Dense management routes inside public navigation.
- Treating every selected object as a mandatory right-side panel.

## Application Shells

### Community Shell

Use for public and member-facing experiences:

- Home.
- Upcoming events.
- Event detail and slots.
- Event archive when presented for normal member browsing.
- Unit/member pages.
- Public wiki/media content.
- Community links.

The Community Shell can use a cinematic top navigation, mobile hamburger menu, and richer landing/event imagery.
Authorized users can get one clear `Command Center` entry, preferably through the profile menu or a distinct
management action.

### Command Center Shell

Use for management and administrative workflows:

- Management overview.
- Event administration.
- Server fleet and runtime operations.
- Cloud operations.
- Workers and lifecycle jobs.
- Mod packs and terrains.
- Users, roles, claims.
- Billing owners.
- Audit log.
- Settings.

The Command Center Shell provides the operational frame:

- Management navigation.
- Current area context.
- Search or command entry where useful.
- Alerts/attention indicators where useful.
- Current user.
- `Back to site`.

The Command Center Shell does not decide the inner layout of every page. Each resource area should choose the
route type that matches the task.

## Command Center Page Model

Most Command Center areas should be modeled as three route-level surfaces.

### 1. Overview/Table/List/All Page

Purpose:

- Show the collection.
- Support scanning, filtering, sorting, and finding.
- Show attention state.
- Provide entry points to selection/edit routes.
- Provide a create action when creation is allowed.

Typical examples:

- `/command/events`
- `/command/servers`
- `/command/mod-packs`
- `/command/terrains`
- `/command/members`
- `/command/audit`

This page is not a required pre-page obstacle. It should answer:

- What exists?
- What needs attention?
- What can I open or create?

Use:

- Page header with concise status context.
- Search/filter controls when the collection is large.
- Table, list, board, or cards based on the data shape.
- Stable row/card actions that navigate or perform low-risk direct actions.
- Loading, empty, filtered-empty, failed-load, and unauthorized states.

Avoid:

- Forcing a secondary detail pane.
- Editing large forms inline in the table.
- Hiding destructive or cost-changing actions as casual row icons.
- Requiring every resource to fit the same table layout.
- Showing dead or placeholder actions.

### 2. Selection/Edit Page

Purpose:

- Work with one selected object.
- Edit fields.
- Review related state.
- Execute low-risk actions with clear feedback.
- Reach high-risk operation reviews.

Typical examples:

- `/command/events/{id}`
- `/command/events/{id}/edit`
- `/command/servers/{id}`
- `/command/mod-packs/{id}`
- `/command/terrains/{id}`
- `/command/members/{id}`

The exact URL shape can follow the existing route conventions. The important part is that selected-object work
gets its own focused surface when the workflow is more than quick scanning.

Use:

- Selected-object header with name, status, owner, and key identifiers.
- Form sections or tabs only when the object has enough complexity to need them.
- Inline validation.
- Dirty-state handling when leaving with unsaved changes.
- Nearby repository/API error feedback.
- Audit/history links when changes are operationally significant.

Avoid:

- Making every selection a side panel attached to the list.
- Keeping complex edit workflows inside modals when they need route state, refresh, or deep linking.
- Collapsing important owner, policy, billing, or runtime state below unrelated visual chrome.

### 3. Create Stepper

Purpose:

- Guide creation from intent to review.
- Collect required fields in a predictable order.
- Validate before committing.
- Make ownership, policy, and cost consequences explicit.

Typical examples:

- `/command/events/create`
- `/command/servers/create`
- `/command/mod-packs/create`
- `/command/terrains/create`

Use a stepper when creation is multi-part, risk-sensitive, or dependent on earlier choices.

Common step categories:

1. Type or intent.
2. Required details.
3. Ownership or responsibility.
4. Configuration.
5. Validation or preflight.
6. Review and create.

For cost-generating or hosting workflows, creation must show:

- Billing owner.
- Runtime/controller choice.
- Resources or provider impact, if applicable.
- Policy or preflight evidence, if applicable.
- Cleanup expectation.
- Whether billing starts immediately or after a later action.

Avoid:

- Starting cost-generating work before ownership is assigned.
- Hiding required validation in a final backend error.
- Using a generic modal when the workflow needs multiple steps, review, or recovery from failure.

## When A Split Workspace Is Appropriate

Dual-pane, master/detail, and persistent secondary panels are allowed, but they are opt-in.

Use them only when they materially improve the workflow, such as:

- Rapid triage across many records where selection changes frequently.
- Comparing a selected item against collection-level context.
- Reviewing logs, findings, jobs, or resources while keeping the list visible.
- Assigning slots or matching related objects where side-by-side context reduces mistakes.

Do not use them just because the page is in the Command Center.

If a split workspace is used, it must still work as:

- A single-column route or stacked view on mobile.
- A deep-linkable selected state when the selected object is important.
- A layout with no compressed desktop table forced onto narrow screens.

## Interaction Rules

### Navigation

- Public routes stay in the Community Shell.
- Management routes stay in the Command Center Shell.
- The only normal bridge from Community to management is `Command Center`.
- The normal bridge back is `Back to site`.
- Do not render a hybrid shell with both full public navigation and full management navigation.

### Actions

Low-risk actions can be direct when state and effect are obvious.

High-risk actions must open an operation review.

High-risk includes actions that:

- Create provider resources.
- Start live billing.
- Delete provider resources.
- Delete server or business records.
- Change billing owner.
- Clean up runtime or provider resources.
- Can leave billable resources behind.

### Forms

- Validate inline.
- Do not close dialogs, panels, or steppers on invalid submit.
- Show required field errors next to fields.
- Show repository failures in the same page, panel, or step.
- Preserve entered values after failed submit.

### States

Every page type needs:

- Loading state.
- Empty state.
- Filtered-empty state where filtering exists.
- Failed-load state.
- Unauthorized/no-permission state where applicable.
- Save/create failure state where mutation exists.

## Page Guidance By Area

### Command Center Overview

Use the Command Center Shell.

This is the management landing page, not a secondary shell. It should summarize what needs attention now:

- Next event requiring admin action.
- Active lifecycle jobs.
- Stale workers.
- Active cost exposure.
- Pending policy approvals.
- Cleanup risks.
- Recent audit records.
- Links to the relevant overview/list pages.

### Event Administration

Use:

- Overview/list page for event scanning, filters, and archive/admin entry points.
- Selection/edit page for event details, publication state, slot administration, and metadata edits.
- Create stepper for new events when setup requires multiple decisions.

Normal public event participation remains in the Community Shell.

### Asset Management

Use:

- Overview/list pages for mod packs and terrains.
- Selection/edit pages for revisions, composition, thumbnails, metadata, and usage state.
- Create stepper when creation requires source selection, validation, upload, or review.

Do not require a side-by-side table/editor split for ordinary asset CRUD.

### Server Fleet

Use:

- Overview/list page for fleet scanning, status, owner, latest job, and attention indicators.
- Selection/edit pages for server runtime, configuration, files, jobs, ownership, audit, and danger actions.
- Create stepper for server creation, especially when billing owner, controller, runtime, or provider choices matter.

Runtime stopped and provider billing stopped must remain visually distinct. Billing owner must be visible before
hosting or cost-generating actions.

### Cloud Operations

Use:

- Overview/list page for sessions, plans, resources, estimates, reconciliation, policies, configurations, and metadata.
- Selection/edit pages for the chosen cloud object or finding.
- Create/provisioning stepper for workflows that create resources or start billing.

Operation review is required before provisioning, teardown, cleanup, or ownership-sensitive changes.

### Members, Roles, Claims, Verein, And Governance

Use:

- Overview/list pages for scanning and filters.
- Selection/edit pages for individual records and approval decisions.
- Create stepper only when creation has multiple steps, validation dependencies, or review requirements.

Simple single-field creation can remain a focused create page or compact form, but it still needs inline validation
and clear failure handling.

## Component Direction

Shared primitives should support all three route types:

- `UtfPageHeader`
- `UtfCommandBar`
- `UtfStatusBadge`
- `UtfAttentionSummary`
- `UtfDataTableShell`
- `UtfListShell`
- `UtfEmptyState`
- `UtfFormSection`
- `UtfStepper`
- `UtfOperationReviewDialog`
- `UtfEvidenceChecklist`
- `UtfCostSummary`
- `UtfJobTimeline`
- `UtfImageTile`

`UtfMasterDetailWorkspace` can exist, but it is no longer a default page contract.

## Reference Samples

The generated samples remain useful for tone and component feel:

- Landing desktop reference: `.planning/ui-reviews/concepts/landing-exploration/03-event-first-homepage-variant.png`
- Landing mobile reference: `.planning/ui-reviews/concepts/landing-exploration/04-mobile-homepage-variant.png`
- Events board reference: `.planning/ui-reviews/concepts/cinematic-system/01-events-board-dark-cinematic.png`
- Event detail reference: `.planning/ui-reviews/concepts/cinematic-system/02-event-detail-slots-dark-cinematic.png`
- Asset management reference: `.planning/ui-reviews/concepts/cinematic-system/03-asset-library-dark-cinematic.png`
- Server fleet reference: `.planning/ui-reviews/concepts/cinematic-system/04-server-fleet-dark-cinematic.png`
- Cloud operations reference: `.planning/ui-reviews/concepts/cinematic-system/05-cloud-operations-dark-cinematic.png`
- Operation review reference: `.planning/ui-reviews/concepts/cinematic-system/06-operation-review-dark-cinematic.png`
- Compact admin reference: `.planning/ui-reviews/concepts/cinematic-system/07-compact-admin-dark-cinematic.png`

Generated samples are visual direction only. Do not copy text, invented icons, generated logos, dates, provider
names, or sample values literally.

## Implementation Direction

When applying this contract to existing pages:

1. Keep or create the correct shell first.
2. Classify the page as overview/list, selection/edit, or create stepper.
3. Remove secondary-detail pressure unless the workflow clearly benefits from it.
4. Move complex edit/create flows to route-level pages when they need state, validation, or deep linking.
5. Route high-risk actions through operation review.
6. Verify mobile behavior for each route type.

Do not redesign every page into the same layout. The model standardizes workflow shape, not exact table, form,
or panel structure.

## Fresh Session Prompt

Use this prompt to start implementation in a fresh Codex session:

```text
Read A:\dev\git\UnitedTacticalForces\.planning\ui-reviews\2026-06-24-command-center-flow-model-ui-contract.md.

Apply the United Tactical Forces command-center flow model.

Keep the existing Community Shell and Command Center Shell direction, but do not force Command Center pages into
master/detail or persistent secondary-panel layouts.

For each management area touched, classify routes into:
- Overview/table/list/all page.
- Selection/edit page.
- Create stepper when creation is multi-step or risk-sensitive.

Keep high-risk and cost-generating actions behind operation review.
Do not modify backend behavior unless the UI flow requires an existing API contract.
Do not regenerate generated API clients unless required.
Preserve authorization behavior.
Run dotnet build X39.UnitedTacticalForces.slnx before finishing.
```

## Acceptance Criteria

The implementation is on-direction when:

- Public/community pages and management pages use distinct shells.
- Command Center pages no longer assume a default secondary detail pane.
- Collection pages support scanning and route into selected-object pages.
- Selected-object pages hold complex edit/review workflows.
- Create flows use steppers when decisions, validation, ownership, policy, or cost make creation non-trivial.
- Tables/lists are not forced into one universal layout.
- High-risk operations require a review step.
- Billing owner is visible for hosting and cost-generating actions.
- Mobile layouts use stacked routes, drawers, or stepper pages instead of compressed desktop tables.
- The dark/gold visual system does not weaken warning, danger, or policy semantics.

## Non-Goals

- Do not implement Hetzner provisioning as part of this UI contract.
- Do not replace Fluent UI.
- Do not rebuild the app as a separate SPA framework.
- Do not make generated concept art production assets with baked-in text.
- Do not force every page to use large background imagery.
- Do not move authorization logic to the client.
- Do not require every management page to be master/detail.

---
phase: 41
slug: cloud-template-domain-and-audit-foundation
status: approved
shadcn_initialized: false
preset: none
created: 2026-06-21
reviewed_at: 2026-06-21T22:01:12Z
---

# Phase 41 - UI Design Contract

> Visual and interaction contract for cloud template administration and template selection surfaces. Generated inline by the gsd-ui-phase workflow and verified against the gsd-ui-checker dimensions.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none - Blazor WebAssembly with Microsoft Fluent UI Blazor |
| Preset | not applicable |
| Component library | Microsoft Fluent UI Blazor |
| Icon library | Microsoft Fluent UI Icons |
| Font | Fluent UI/system stack, inheriting the existing app shell font |

---

## Visual Hierarchy

Primary screen focal point: the template table row selection and adjacent detail panel. The page should prioritize scannable operational state over decorative presentation.

Hierarchy:

1. Page heading and compact count strip: total templates, enabled templates, superseded versions, disabled versions.
2. Toolbar actions: `Refresh templates` and primary `Create cloud template`.
3. Table or data panel: template name, game type, provider, version, enabled state, default shape, override bounds, updated timestamp.
4. Detail panel: selected template version, audit/update history, bounds summary, and disable/supersede state.
5. Validation and redaction messages: warnings and errors use Fluent message bars above the affected table/form area.

Icon-only actions must include `Title` text and an accessible label. Use text labels for unfamiliar or destructive actions.

---

## Spacing Scale

Declared values (must be multiples of 4):

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Icon gaps, inline label/status gaps |
| sm | 8px | Table cell inner gaps, compact toolbar gaps, badge padding |
| md | 16px | Page padding, panel padding, form grid gaps |
| lg | 24px | Empty-state padding, major intra-panel breaks |
| xl | 32px | Wide layout gaps when table and detail panel split |
| 2xl | 48px | Rare page-level separation on large screens |
| 3xl | 64px | Reserved; do not use inside dense admin panels |

Exceptions: none.

---

## Typography

Use exactly these four sizes and two weights.

| Role | Size | Weight | Line Height |
|------|------|--------|-------------|
| Label and table header | 13px | 600 | 1.3 |
| Body and table cell | 14px | 400 | 1.5 |
| Section heading | 16px | 600 | 1.25 |
| Page heading | 22px | 600 | 1.25 |

---

## Color

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | #ffffff | Main table/detail surfaces and page background |
| Secondary (30%) | #f7f9fb | Summary cards, tab strip background, empty states, inactive row hover |
| Accent (10%) | #2563eb | Primary create button, active Templates tab, selected template row marker, focused bounds warning anchor |
| Destructive | #b42318 | Disable template action, redaction failure, invalid/stale template blockers |

Accent reserved for: primary `Create cloud template` action, active template-management tab, selected row marker, focused validation anchor for template bounds. Do not use accent for every button or status badge.

Semantic badges:

- Neutral: #475467 text on #f7f9fb for inactive or historical versions.
- Info: #175cd3 text for default/snapshot metadata.
- Warning: #b54708 text for stale or superseded versions that remain selectable only historically.
- Success: #027a48 text for enabled current templates.
- Danger: #b42318 text for disabled templates, redaction blocks, and destructive confirmations.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | Create cloud template |
| Secondary CTA | Refresh templates |
| Empty state heading | Cloud templates have not been configured |
| Empty state body | Create the first Arma 3 cloud template so authorized users can plan with approved provider defaults and bounds. |
| Error state | Template changes could not be loaded. Refresh templates; if the problem remains, review API logs for the template endpoint. |
| Validation state | Template bounds block this value. Choose a value within the allowed provider, storage, port, and runtime limits. |
| Destructive confirmation | Disable template: Disable this template version? Existing dry-run and provisioning snapshots keep their historical version, but users cannot select it for new planning. |

Destructive actions:

- Disable template version: require an explicit confirmation dialog with the template name, version, and a non-empty reason when the API requires audit context.
- Supersede template by creating a new version: present as a normal create/update flow, not as a destructive action.

---

## Component Contract

Use existing app patterns:

- Page shell: `.cloud-page` or a template-specific equivalent with 16px page padding and 16px vertical gap.
- Toolbar: existing `cloud-toolbar`/`servers-toolbar` shape with count chips and right-aligned Fluent buttons.
- Table: FluentDataGrid or the existing compact table pattern; columns must stay scannable on desktop and overflow horizontally on narrow screens.
- Detail panel: right-side sticky panel on wide screens, stacked under the table below 1100px.
- Forms: 16px grid gap, label text at 13px semibold, inputs/selects at 32px minimum height.
- Status: reuse `status-badge` variants for enabled, disabled, superseded, stale, and redaction-blocked states.
- Messaging: use FluentMessageBar for warning/error/success states; do not hide authorization or redaction failures in inline-only text.

Responsive requirements:

- At widths below 1100px, collapse table/detail layout to one column.
- At widths below 720px, toolbar actions become full-width and tabs/buttons reach at least 44px height.
- Long template names, provider ids, and audit reason text must wrap with `overflow-wrap: anywhere`.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| none | none | not applicable - Blazor/Fluent UI project, no third-party registry blocks |

---

## Checker Sign-Off

- [x] Dimension 1 Copywriting: PASS
- [x] Dimension 2 Visuals: PASS
- [x] Dimension 3 Color: PASS
- [x] Dimension 4 Typography: PASS
- [x] Dimension 5 Spacing: PASS
- [x] Dimension 6 Registry Safety: PASS

**Approval:** approved 2026-06-21

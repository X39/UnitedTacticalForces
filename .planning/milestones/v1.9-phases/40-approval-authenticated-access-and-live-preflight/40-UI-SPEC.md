---
phase: 40
slug: approval-authenticated-access-and-live-preflight
status: approved
shadcn_initialized: false
preset: none
created: 2026-06-21
reviewed_at: 2026-06-21
---

# Phase 40 - UI Design Contract

Visual and interaction contract for the Phase 40 approval, authenticated access, and live preflight surfaces.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | Microsoft Fluent UI Blazor |
| Preset | not applicable |
| Component library | Microsoft.FluentUI.AspNetCore.Components 4.11.8 |
| Icon library | Microsoft Fluent UI Icons |
| Font | Fluent UI default system stack; treat Segoe UI/system sans as the visual baseline |

Contract:

- Extend the existing `/servers` and `/servers/cloud` operational UI instead of introducing a new product redesign.
- Use `FluentButton`, `FluentMessageBar`, `FluentProgress`, `FluentDataGrid`, `FluentDialogProvider`, `FluentTooltipProvider`, `FluentTabs`/existing tab button patterns, and existing status badge CSS patterns.
- Use generated Kiota clients through repository facades; UI must not bind raw generated DTOs where a hand-written safe model exists.
- Never display or persist cookies, authorization headers, bearer tokens, OAuth material, provider tokens, Steam credentials, worker secrets, worker hashes, or raw secret-bearing payloads.

---

## Spacing Scale

Declared values (must be multiples of 4):

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Label/input gaps, icon/text gaps, status badge internals |
| sm | 8px | Compact row actions, field validation spacing, table cell rhythm |
| md | 16px | Page padding, panel gaps, form grid gaps, section padding |
| lg | 24px | Empty state padding, evidence checklist grouping |
| xl | 32px | Major transition between preflight groups when vertical space permits |
| 2xl | 48px | Reserved for full-page empty/error states only |
| 3xl | 64px | Not used in Phase 40 operational screens |

Exceptions: existing Fluent control internals may keep their library defaults; icon-only row actions must preserve at least a 32px visual control with a 44px touch target on mobile.

---

## Typography

| Role | Size | Weight | Line Height |
|------|------|--------|-------------|
| Body | 14px | 400 | 1.5 |
| Label | 13px | 600 | 1.3 |
| Heading | 22px | 600 | 1.25 |
| Display | 22px | 600 | 1.25 |

Usage:

- Do not introduce hero-scale typography; Phase 40 is an operational evidence workflow.
- Keep table, metadata, validation, and message-bar copy dense enough for scanning.
- Use at most the two weights above. Emphasis comes from placement, badges, and status color, not additional font weights.

---

## Color

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | #ffffff | Main panels, tables, form surfaces |
| Secondary (30%) | #f7f9fb | Summary cards, empty states, grouped evidence sections, inactive tab surface |
| Accent (10%) | #BAA05A | Primary approval/preflight CTA, active nav indicator, focus emphasis where Fluent theme applies |
| Destructive | #b42318 | Blocking evidence, denied approval, unsafe mutation warning, destructive or cost-risk actions only |

Accent reserved for: `Record preflight approval`, active Phase 40 evidence focus, current navigation/step state, and keyboard focus emphasis. Do not use accent for every interactive element.

Semantic status colors remain the established cloud/server palette:

- Info: #2563eb for refresh/reconciliation/progress details.
- Warning: #b54708 for missing approval, stale metadata, unresolved policy, or pending evidence.
- Success: #027a48 for verified non-pending evidence rows.
- Danger: #b42318 for blockers, denied approval, forbidden material, or unsafe mutation risk.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | Record preflight approval |
| Secondary CTA | Refresh preflight evidence |
| Empty state heading | No preflight evidence recorded |
| Empty state body | Record approval, authenticated access, provider readiness, billing owner, dry-run, policy, cost, and reconciliation evidence before live provisioning can continue. |
| Error state | Preflight evidence could not be loaded. Refresh the evidence view; if the problem remains, keep live provisioning blocked and review the server audit history. |
| Blocking state | Live provisioning is blocked until every preflight row is verified. |
| Success state | Preflight complete. Live provisioning may continue through the approved workflow. |
| Destructive confirmation | Clear preflight evidence: confirm that Phase 41 will remain blocked and the existing approval package will be replaced. |

Rules:

- Prefer action labels with a verb and noun: `Record preflight approval`, `Refresh preflight evidence`, `Review policy approval`, `Open server evidence`.
- Never label live mutation actions as ready unless all Phase 40 rows are verified.
- If an approval or evidence row stores only a token reference, say `token reference recorded`; never render the token value.

---

## Visual Hierarchy And Interaction Contract

Primary focal point: the preflight readiness summary that states either `Live provisioning is blocked` or `Preflight complete`.

Secondary hierarchy:

1. Approval package evidence.
2. Authenticated administrator/server-owner access evidence.
3. Provider, metadata, billing owner, dry-run, cost, policy, and reconciliation readiness.
4. Audit history and redaction notes.

Screen behavior:

- The first viewport must show the readiness status, required next action, and whether Phase 41 remains blocked.
- Evidence rows use stable columns: check, evidence item, current status, source/timestamp, operator note, next action.
- Row status badges use the established `status-success`, `status-warning`, `status-danger`, `status-info`, and `status-neutral` semantics.
- Icon-only actions must have `Title` text and an accessible label; prefer visible text for primary actions.
- Missing or pending evidence must not hide behind hover-only UI.
- Mobile layout collapses evidence rows into single-column cards with the same status, source, timestamp, and next action fields.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| shadcn official | none | not applicable - project uses Blazor/Fluent UI |
| third-party | none | no third-party registry blocks used |

---

## Checker Sign-Off

- [x] Dimension 1 Copywriting: PASS
- [x] Dimension 2 Visuals: PASS
- [x] Dimension 3 Color: PASS
- [x] Dimension 4 Typography: PASS
- [x] Dimension 5 Spacing: PASS
- [x] Dimension 6 Registry Safety: PASS

**Approval:** approved 2026-06-21

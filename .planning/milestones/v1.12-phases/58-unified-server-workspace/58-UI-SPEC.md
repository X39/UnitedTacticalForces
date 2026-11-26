---
phase: 58
slug: unified-server-workspace
status: approved
shadcn_initialized: false
preset: none
created: 2026-07-05
---

# Phase 58 - UI Design Contract

> Visual and interaction contract for the unified selected-server workspace.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none |
| Preset | not applicable |
| Component library | Microsoft Fluent UI Blazor plus existing `Utf*` components |
| Icon library | Fluent UI icons already used in WebApp command surfaces |
| Font | Inherit existing WebApp font stack from `sources/WebApp/wwwroot/css/app.css` |

---

## Layout Contract

- Keep the selected-server workspace inside the existing command-center shell.
- Overview is a work surface, not a landing page. It must show current status and the next relevant action in the first viewport.
- Use one primary next-action area plus a compact secondary action row. Do not duplicate every tab on Overview.
- Show every server as session-timed. Server age, owner, current job, runtime state, cost exposure, and cleanup/teardown state are normal workspace facts.
- Add a dedicated Logs tab. Runtime should not contain the primary log reading experience.
- Keep owner transfer, file operations, full audit, full job history, and deep diagnostics on their own tabs or secondary panels.
- Add a collapsed admin-only diagnostics panel inside the workspace for users with the required admin/provider visibility.

---

## Spacing Scale

Declared values must align to existing `app.css` tokens and remain multiples of 4.

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Icon gaps, badge internals, inline metadata |
| sm | 8px | Compact rows, tab gaps, small form gaps |
| md | 16px | Default panel body spacing, form grids |
| lg | 24px | Section padding and primary action groups |
| xl | 32px | Overview grid gaps and tab section breaks |
| 2xl | 48px | Major workspace region breaks only |
| 3xl | 64px | Avoid inside this workspace unless matching existing shell rhythm |

Exceptions: none.

---

## Typography

| Role | Size | Weight | Line Height |
|------|------|--------|-------------|
| Body | 14px | 400 | 1.45 |
| Label | 12px | 600 | 1.35 |
| Heading | 20px | 700 | 1.25 |
| Display | 24px | 700 | 1.2 |

- Do not scale text with viewport width.
- Keep button and badge labels short enough to fit at mobile widths.
- Use task nouns in visible headings: `Overview`, `Runtime`, `Cloud`, `Logs`, `Ownership`, `Files`, `Jobs`, `Audit`.

---

## Color

Use existing CSS custom properties rather than introducing a new palette.

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | `var(--utf-bg)` / `#070b0f` | Page background and command shell |
| Secondary (30%) | `var(--utf-surface)` / `#111820` | Panels, tabs, data surfaces |
| Accent (10%) | `var(--utf-gold)` / `#baa05a` | Primary action, selected tab, active row, focus highlight |
| Destructive | `var(--utf-danger)` / `#ef4444` | Delete, failed cleanup, destructive confirmations |

Accent reserved for: primary next action, selected navigation state, focus ring, active row, and important non-danger cost-stop guidance. Status semantics use existing success, warning, danger, and info tokens.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | State-driven, examples: `Start server`, `Review cloud setup`, `Teardown provider resources`, `Open logs` |
| Empty state heading | `No server activity yet` |
| Empty state body | `Run an action from this workspace. New jobs and logs will appear here.` |
| Error state | Problem plus exact next step, example: `Cloud setup is blocked. Review the missing owner and run Review cloud setup again.` |
| Destructive confirmation | `Teardown provider resources: this stops provider cost for this server.` |

Copy rules:
- Use `Server record`, `Local server`, and `Cloud server` labels from earlier phases.
- Prefer task language over implementation nouns. `Review cloud setup` is preferred over dry-run wording in normal UI.
- Provider teardown wording must explicitly say it stops provider cost.
- Runtime cleanup wording must explicitly say it does not stop provider cost.
- Do not show `Collect logs` as a user action. Logs should already be readable from the Logs tab; download is optional there.
- Non-admin users should see what they can do, not locked admin inventory.

---

## Interaction Contract

- Overview primary action is selected from current server state. Provider teardown wins when billable provider resources remain and teardown is safe/available.
- Overview secondary actions should stay compact and state-relevant. Do not show long disabled command walls.
- Logs tab contains log list/read experience and the only log download action. It may also show refresh/retry states if logs are unavailable.
- Cloud tab contains full cloud setup, provisioning, runtime/provider status, cost/policy summary, teardown, and cleanup-risk details.
- Admin diagnostics are collapsed by default and permission-gated. They may show detailed provider/fleet state for the selected server, but normal owner summaries remain server-scoped.
- When provider teardown succeeds, show cost-stopped state plus remaining billable count or cleanup failure follow-up only if needed.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| shadcn official | none | not required |
| third-party | none | not allowed without explicit review |

---

## Checker Sign-Off

- [x] Dimension 1 Copywriting: PASS
- [x] Dimension 2 Visuals: PASS
- [x] Dimension 3 Color: PASS
- [x] Dimension 4 Typography: PASS
- [x] Dimension 5 Spacing: PASS
- [x] Dimension 6 Registry Safety: PASS

**Approval:** approved 2026-07-05

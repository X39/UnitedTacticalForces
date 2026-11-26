---
phase: 59
slug: lifecycle-log-and-status-feedback-repair
status: approved
shadcn_initialized: false
preset: none
created: 2026-07-05
reviewed_at: 2026-07-05
---

# Phase 59 - UI Design Contract

> Visual and interaction contract for lifecycle, log, and status feedback repair in the selected-server workspace.

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

- Keep changes inside the existing selected-server workspace. Do not create a new operations page.
- Primary visual focal point is the Overview primary next-action/status band when an operation blocks, waits, fails,
  or affects cost/cleanup risk.
- Jobs tab uses timeline rows with stable columns for job, status reason, attempts, worker, timestamps, and next step.
- Logs tab keeps persisted logs visible while a status strip above the list shows freshness, current job, progress,
  last refreshed time, and failure/next step.
- Detailed worker/provider/cost/policy facts stay in compact workspace cards and tabs. Diagnostics are visible but
  owner-safe and secondary to the normal task flow.
- Do not use `n/a` for selected-server operational labels. Use the server-projected English status labels.

---

## Spacing Scale

Declared values must align to existing `app.css` tokens and remain multiples of 4.

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Icon gaps, badge internals, inline metadata |
| sm | 8px | Compact rows, status-strip gaps, small form gaps |
| md | 16px | Default panel body spacing and timeline row padding |
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
| Heading | 18px | 600 | 1.25 |
| Display | 20px | 600 | 1.2 |

- Do not scale text with viewport width.
- Keep status and action labels short enough to fit at mobile widths.
- Use task nouns in headings: `Overview`, `Runtime`, `Logs`, `Jobs`, `Cloud`, `Diagnostics`.

---

## Color

Use existing CSS custom properties rather than introducing a new palette.

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | `var(--utf-bg)` / `#070b0f` | Page background and command shell |
| Secondary (30%) | `var(--utf-surface)` / `#111820` | Panels, tabs, timeline rows, data surfaces |
| Accent (10%) | `var(--utf-gold)` / `#baa05a` | Primary next action, selected tab, active row, focus highlight |
| Destructive | `var(--utf-danger)` / `#ef4444` | Provider teardown, delete, clear logs, failed cleanup |

Accent reserved for: primary next action, selected navigation state, focus ring, active job row, and important
non-danger cost-stop guidance. Status semantics use existing success, warning, danger, neutral, and info tokens.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | State-driven, examples: `Follow running job`, `Open logs`, `Start server`, `Stop server`, `Review policy approval`, `Teardown provider resources` |
| Empty state heading | `No lifecycle jobs yet` |
| Empty state body | `Run a server action. Jobs, progress, and final results will appear here.` |
| Error state | `The last operation failed. Review the reason below, then run the available server action again.` |
| Destructive confirmation | `Teardown provider resources: this stops provider cost for this server.` |

Copy rules:

- Use `Waiting for worker`, `Unavailable until setup completes`, `Stale`, `Failed`, `Not available`, and
  `Not supported` exactly for the shared taxonomy.
- Use `Logs current`, `Refreshing after stop`, `Last refreshed`, `Waiting for worker`, `Collection unavailable`, and
  `Collection failed` for log freshness.
- Do not show `Collect logs` as a user-facing action. The backend lifecycle kind may still exist.
- When a failed action leaves state usable, show failure context and keep the normal action label, such as
  `Start server`.
- Provider teardown wording must explicitly say it stops provider cost. Runtime cleanup must explicitly say it does
  not stop provider cost.

---

## Interaction Contract

- Opening Logs reloads persisted logs and follows an already-running relevant lifecycle job. It must not enqueue a
  new job.
- `Stop server` includes final log capture and automatic log refresh after completion. It is presented as one user
  operation even if implementation uses worker-side substeps.
- Active lifecycle jobs promote follow-job or open-logs actions unless provider teardown/cost risk has higher
  priority.
- Conflicting runtime actions are disabled or omitted with a `Waiting for worker` or current-job reason.
- Diagnostics are always visible for now, but content remains selected-server scoped and redacted.

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

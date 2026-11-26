---
phase: 60
slug: ux-verification-fake-states-and-guardrails
status: approved
shadcn_initialized: false
preset: none
created: 2026-07-05
reviewed_at: 2026-07-05
---

# Phase 60 - UI Design Contract

> Visual and interaction contract for mock-backed UX verification, fake state coverage, and redaction guardrails.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none |
| Preset | not applicable |
| Component library | Microsoft Fluent UI Blazor plus existing `Utf*` components |
| Icon library | Fluent UI icons already used in WebApp command surfaces |
| Font | Roboto, `Segoe UI`, system-ui, sans-serif from `sources/WebApp/wwwroot/css/app.css` |

---

## Layout Contract

- Keep verification work inside the existing `/servers` creation flow and selected-server workspace. Do not add a
  separate marketing, admin-only, or test-only UX surface for normal users.
- The coherent browser walkthrough starts at server creation, proves wizard steps do not persist setup state, creates a
  fake-backed server, lands in the workspace, then inspects Overview, Logs, Jobs, Cloud, diagnostics, and the current
  primary next action.
- Focused fake-state checks must use the same visual hierarchy as the real workspace: identity and quick status first,
  primary next action second, supporting cards/tabs third, diagnostics last.
- Browser assertions must prove visible meaning by text, role, route, target section, enabled/disabled action, and
  scenario-specific labels. Screenshots and traces are supporting evidence only.
- Responsive verification must cover desktop workspace grids and mobile single-column states. No state label, action
  button, card value, job row, or diagnostics row may overlap or truncate its longest expected word.
- Diagnostics stay collapsed or secondary by default where the page already does so, but must remain reachable for
  server owners with selected-server scoped, redacted content.

---

## Fake State Matrix Contract

Each fake state used by browser/API tests must define and assert these fields:

| Field | Required Proof |
|-------|----------------|
| Scenario name | Visible fake-safe identifier, never a real provider id, token, username, cookie, or project name |
| Selected game | Generic game label plus Arma 3 compatibility where applicable |
| Hosting mode | Local record, local managed, or cloud-capable mode shown in user-facing language |
| Billing owner | Owner label present for cost-generating or cloud-capable states |
| Current state | Stable workspace status label, never `n/a` |
| Reason | Owner-safe reason for waiting, stale, failed, deferred, unavailable, or unsupported states |
| Primary action | Exact expected primary CTA and target section |
| Conflicting actions | Disabled or omitted actions with a current-job, setup, policy, or safety reason |
| Evidence tab/panel | Overview, Logs, Jobs, Cloud, diagnostics, ownership, configuration, or files |
| Mock/live boundary | Evidence text states `mock backend`, `fake provider state`, or `no live provider mutation` |

Required scenarios: local record, local managed install, cloud planned, cloud provisioned, running, failed job,
stuck/recovered job, log collection, cleanup needed, provider teardown ready, unknown, unavailable, unsupported,
waiting for worker, stale, failed, deferred, and unavailable until setup completes.

---

## Spacing Scale

Declared values must align to existing `app.css`, `Create.razor.css`, and `Details.razor.css` tokens and remain
multiples of 4.

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Icon gaps, inline metadata, badge internals |
| sm | 8px | Compact state rows, diagnostics rows, status-strip gaps |
| md | 16px | Workspace cards, timeline rows, default panel padding |
| lg | 24px | Section padding, primary action groups, wizard panel breaks |
| xl | 32px | Desktop grid gaps and tab group separation |
| 2xl | 48px | Major workspace region breaks only |
| 3xl | 64px | Avoid inside this verification surface unless matching the existing shell rhythm |

Exceptions: none.

---

## Typography

| Role | Size | Weight | Line Height |
|------|------|--------|-------------|
| Body | 14px | 400 | 1.45 |
| Label | 12px | 700 | 1.35 |
| Heading | 18px | 650 | 1.25 |
| Display | 20px | 700 | 1.2 |

- Do not scale text with viewport width.
- Keep action labels short and verb-led: `Start server`, `Stop server`, `Open logs`, `Follow running job`,
  `Review policy approval`, `Review billable resources`, `Teardown provider resources`.
- Use task nouns in tab and panel labels: `Overview`, `Logs`, `Jobs`, `Cloud`, `Diagnostics`.
- Long fake-state reasons must wrap with `overflow-wrap: anywhere` or equivalent existing classes.

---

## Color

Use existing CSS custom properties rather than introducing a new palette.

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | `var(--utf-bg)` / `#070b0f` | Page background and command shell |
| Secondary (30%) | `var(--utf-surface)` / `#111820` | Panels, workspace cards, fake-state rows, diagnostics |
| Accent (10%) | `var(--utf-gold)` / `#baa05a` | Primary next action, selected tab, focus highlight, active scenario row |
| Destructive | `var(--utf-danger)` / `#ef4444` | Provider teardown, delete, clear logs, failed cleanup |

Accent reserved for: primary next action, selected navigation state, selected fake-state row, focus ring, active job row,
and important non-danger cost-stop guidance. Use existing success, warning, danger, info, and neutral tokens for state
semantics; do not make the fake-state matrix a one-color dashboard.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | State-driven, examples: `Start server`, `Stop server`, `Open logs`, `Follow running job`, `Review policy approval`, `Review billable resources`, `Teardown provider resources` |
| Empty state heading | `No lifecycle jobs yet` |
| Empty state body | `Run a server action. Jobs, progress, and final results will appear here.` |
| Error state | `The last operation failed. Review the reason below, then run the available server action again.` |
| Destructive confirmation | `Teardown provider resources: this stops provider cost for this server.` |

Copy rules:

- Use `mock backend`, `fake provider state`, and `no live provider mutation` in Phase 60 evidence artifacts.
- Use `Waiting for worker`, `Unavailable until setup completes`, `Stale`, `Failed`, `Not available`, `Not supported`,
  `Deferred`, and `Unknown` exactly for operational taxonomy checks.
- Use `Logs current`, `Refreshing after stop`, `Last refreshed`, `Waiting for worker`, `Collection unavailable`, and
  `Collection failed` for log freshness checks.
- Do not show `Collect logs` as a user-facing action in `Details.razor`.
- Provider teardown wording must explicitly say it stops provider cost. Runtime cleanup must explicitly say it does not
  stop provider cost.
- Do not use real-looking tokens, cookies, provider identifiers, usernames, or project names in mock state labels,
  screenshots, traces, summaries, or final evidence.

---

## Interaction Contract

- Browser tests must prove one coherent create-to-workspace journey and focused state-matrix checks. The journey and
  matrix may share fixtures, but both evidence types must remain readable in test names and final summary text.
- Fake backend scenario helpers should expose explicit expected labels, target sections, and forbidden actions so tests
  fail when the UI drifts.
- Opening Logs reloads persisted logs and follows existing relevant jobs only. It must not enqueue a lifecycle job.
- Active lifecycle jobs promote follow-job or open-logs actions unless provider teardown or billable-resource risk has
  higher priority.
- Safe failed operations keep the normal action available when the runtime state allows it. Corrupted or intermediate
  states may show a retry/recover action.
- Provider teardown is the cost-stop action and ranks ahead of ordinary runtime controls when billable exposure exists.
- Diagnostics must render only selected-server scoped allowlisted facts: session state, worker status, public endpoint,
  policy label, latest job, log freshness, and audit count.

---

## Redaction And Evidence Contract

- Source scans and browser assertions must use exact unsafe sentinel values and forbidden field names, not broad matches
  that would flag safe explanatory security copy.
- Guardrails must fail if provider tokens, Steam credentials, worker credentials, worker credential hashes,
  authorization headers, bearer tokens, cookies, raw secret-bearing payloads, generated DTOs, Kiota transport types, or
  `AdditionalData` reach Razor-facing models, rendered UI, browser diagnostics, test evidence, or user-facing logs.
- Final evidence must state that live Hetzner provisioning, live Steam/Arma runtime, authenticated operator browser
  walkthroughs, provider teardown, final billable count `0`, and full live redaction review remain deferred.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| shadcn official | none | not required |
| third-party | none | not allowed without explicit review |

No new frontend packages, registries, icon sets, or design-system dependencies are needed for this phase.

---

## Checker Sign-Off

- [x] Dimension 1 Copywriting: PASS
- [x] Dimension 2 Visuals: PASS
- [x] Dimension 3 Color: PASS
- [x] Dimension 4 Typography: PASS
- [x] Dimension 5 Spacing: PASS
- [x] Dimension 6 Registry Safety: PASS

**Approval:** approved 2026-07-05

---
phase: 47-live-runtime-and-visibility-evidence
type: ui-spec
subsystem: webapp
tags:
  - blazor
  - fluent-ui
  - cloud-runtime
  - live-evidence
created: 2026-06-22
---

# Phase 47 UI Design Contract

## Product Intent

The selected-server `/servers` workflow must let an operator inspect cloud worker bootstrap and Arma 3 runtime evidence, or see the exact runtime environment blocker, without leaving the existing operational surface.

The UI must stay dense, utilitarian, status-first, and consistent with the current server operations panel. Do not add a landing page, marketing copy, or decorative layout.

## Primary Surface

Use the existing selected-server sections in `sources/WebApp/Pages/Servers/Index.razor`:

- Cloud runtime
- Cloud status
- Lifecycle jobs
- Logs
- Cloud operation result
- Cost, policy, reconciliation, billing owner, template/profile context

If new Phase 47-specific display is needed, add it as a compact evidence subsection inside the selected-server runtime area. Do not create nested cards.

## Required Evidence Labels

Render or verify display paths for these evidence labels:

| Label | Meaning |
|-------|---------|
| Worker bootstrap | Bootstrap credentials/status were issued and consumed or are explicitly blocked. |
| Worker registration | Worker identity and registration status are visible. |
| Worker heartbeat | Last heartbeat timestamp and freshness are visible. |
| Worker capabilities | Runtime action/capability labels are visible without secrets. |
| Install/configure | Install or configure job evidence is visible or explicitly human-needed. |
| Runtime start | Start job/runtime-running evidence is visible or explicitly human-needed. |
| Runtime stop | Stop job/runtime-stopped evidence is visible or explicitly human-needed. |
| Runtime logs | Recent log timestamp and log rows are visible through safe log collection. |
| Authenticated admin view | Admin visibility path is identified as evidence or human-needed. |
| Authenticated owner view | Server-owner visibility path is identified as evidence or human-needed. |
| Runtime owner | Billing owner and assignment are visible for the selected runtime session. |
| Cost/policy/reconciliation | Cost source, policy decision, and reconciliation state are visible. |
| Cleanup separation | Runtime cleanup is explicitly not provider teardown or cost stop. |
| Runtime blocker | Missing live provisioning/worker/runtime prerequisites are explicit. |
| Redaction check | Runtime evidence does not expose provider, Steam, worker, bearer, cookie, or raw secret-bearing material. |

## Interaction Rules

- Runtime action buttons may remain available only through existing permission and repository gates.
- If no provisioned live session or worker evidence exists, show blocked or human-needed copy rather than implying success.
- Runtime cleanup must remain separate from provider teardown; provider teardown is the cost-stop action.
- Refresh controls must remain available so operators can reload runtime, job, log, cost, policy, and reconciliation data.
- Use existing Fluent UI buttons, message bars, status badges, metadata grids, compact tables, and evidence rows.

## Visual Rules

- Reuse `detail-section`, `status-badge`, `meta-grid`, `runtime-actions`, and Phase 46 evidence-grid patterns.
- Fixed-format rows must have stable dimensions and wrap long ids inside their parent row.
- Buttons use icons and concise labels.
- Avoid broad instructional text in the app. Keep copy short and operational.

## Safety Copy

Allowed blocked copy:

- `Runtime evidence is human-needed until live provisioning and worker bootstrap are supplied.`
- `Runtime cleanup does not stop provider billing.`
- `Provider teardown remains the cost-stop boundary.`
- `No live runtime pass is claimed without install, start, stop, and log evidence.`

Do not claim live Steam, Arma, provider teardown, final billable-resource, Phase 30, or CR acceptance success from automated tests alone.

## Redaction Rules

Display only allowlisted labels, ids, states, timestamps, counts, endpoint host/port, owner references, cost labels, policy references, worker display names, lifecycle job summaries, and sanitized log summaries.

Do not display credential values, Steam credentials, provider secret values, worker secrets, browser session material, or secret-bearing transport content.

## Responsive Behavior

At widths below 860px:

- Evidence rows collapse to one column.
- Action buttons wrap or stretch consistently with existing guided/runtime actions.
- Long ids, endpoint values, and progress messages must wrap inside their parent row.

## Verification Hooks

Boundary tests should assert:

- Required runtime evidence labels remain present.
- Runtime actions still use repository facade methods rather than raw browser HTTP.
- Evidence display helpers avoid forbidden display markers.
- Runtime cleanup copy remains distinct from provider teardown/cost stop.
- Verification artifacts do not claim live runtime pass status without captured evidence.

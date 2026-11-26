---
phase: 48-provider-teardown-cleanup-and-redaction
type: ui-spec
subsystem: webapp
tags:
  - blazor
  - fluent-ui
  - provider-teardown
  - cleanup-evidence
created: 2026-06-22
---

# Phase 48 UI Design Contract

## Product Intent

The selected-server `/servers` workflow must let an operator inspect provider teardown and cleanup evidence, or see exactly why cleanup cannot yet be proven, without leaving the existing operational surface.

The UI must stay dense, utilitarian, status-first, and consistent with the current server operations panel. Do not add a landing page, marketing copy, or decorative layout.

## Primary Surface

Use the existing selected-server sections in `sources/WebApp/Pages/Servers/Index.razor`:

- Cloud operation result
- Provider teardown
- Cloud status
- Cost, policy, reconciliation, billing owner, template/profile context
- Audit context

If new Phase 48-specific display is needed, add it as a compact evidence subsection inside the selected-server provider teardown area. Do not create nested cards.

## Required Evidence Labels

Render or verify display paths for these evidence labels:

| Label | Meaning |
|-------|---------|
| Provider teardown action | Teardown was executed through the control plane or is explicitly human-needed. |
| Teardown session | The selected cloud session id is visible through safe ids only. |
| Cleanup state | Cleanup state is visible from workflow/status/cost evidence. |
| Manual fallback | Manual cleanup fallback is visible only as a retry/fallback need when automated teardown fails or leaves billable resources. |
| Provider-side counts | Provider-side resource counts are visible from safe workflow/status/cost/reconciliation projections. |
| Servers count | Provider server count is explicit or human-needed. |
| Floating IP count | Floating IP count is explicit or human-needed. |
| Volume count | Volume count is explicit or human-needed. |
| Firewall count | Firewall count is explicit or human-needed. |
| Final billable count | Final billable resource count is `0` before pass classification, otherwise review/human-needed. |
| Remaining billable | Remaining billable resources are visible and block pass classification when greater than zero. |
| Unknown provider resources | Unknown provider resources remain review/blocker evidence. |
| Audit records | Teardown/audit context is visible without secrets. |
| Redaction review | Evidence redaction review is safe or blocked before any pass classification. |

## Interaction Rules

- Provider teardown action buttons may remain available only through existing permission, selected-session, policy, and repository gates.
- If no live session or teardown result exists, show human-needed or blocked copy rather than implying cleanup success.
- Runtime stop and runtime cleanup must remain separate from provider teardown; provider teardown is the cost-stop action.
- Refresh controls must remain available so operators can reload workflow result, cost snapshot, provider status, policy, reconciliation, and audit state.
- Use existing Fluent UI buttons, message bars, status badges, metadata grids, compact tables, and evidence rows.

## Visual Rules

- Reuse `detail-section`, `status-badge`, `meta-grid`, `provider-teardown-actions`, and Phase 46/47 evidence-grid patterns.
- Fixed-format rows must have stable dimensions and wrap long ids inside their parent row.
- Buttons use icons and concise labels.
- Avoid broad instructional text in the app. Keep copy short and operational.

## Safety Copy

Allowed blocked copy:

- `Provider teardown remains human-needed until a live session teardown result or explicit cleanup blocker is supplied.`
- `Provider teardown is the cost-stop boundary.`
- `Final billable resource count must be 0 before pass classification.`
- `Manual cleanup fallback is only evidence when automated teardown fails or billable resources remain.`

Do not claim live provider teardown, final billable-resource closure, Phase 30, or CR acceptance success from automated tests alone.

## Redaction Rules

Display only allowlisted labels, ids, states, timestamps, counts, endpoint host/port, owner references, cost labels, policy references, workflow summaries, lifecycle job summaries, and sanitized failure summaries.

Do not display credential values, provider secret values, Steam credentials, worker secrets, browser session material, authorization headers, bearer tokens, cookies, raw provider payloads, or raw secret-bearing logs.

## Responsive Behavior

At widths below 860px:

- Evidence rows collapse to one column.
- Action buttons wrap or stretch consistently with existing guided/runtime/provider actions.
- Long ids, endpoint values, and status summaries must wrap inside their parent row.

## Verification Hooks

Boundary tests should assert:

- Required teardown evidence labels remain present.
- Provider teardown still uses repository facade methods rather than raw browser HTTP.
- Evidence display helpers avoid forbidden display markers.
- Runtime cleanup copy remains distinct from provider teardown/cost stop.
- Verification artifacts do not claim live teardown or final billable-count pass status without captured evidence.

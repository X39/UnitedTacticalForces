---
phase: 46-controlled-live-provisioning-evidence
type: ui-spec
subsystem: webapp
tags:
  - blazor
  - fluent-ui
  - cloud-provisioning
  - live-evidence
created: 2026-06-22
---

# Phase 46 UI Design Contract

## Product Intent

The selected-server `/servers` workflow must let an operator inspect controlled live provisioning evidence, or see why live provisioning remains blocked, without leaving the existing operational surface.

The UI must feel like the current server operations panel: dense, utilitarian, status-first, and built for repeated inspection. Do not add a landing page, marketing copy, or decorative layout.

## Primary Surface

Use existing selected-server sections in `sources/WebApp/Pages/Servers/Index.razor`:

- Live preflight
- Dry-run plan review
- Policy decision
- Cloud operation result
- Cost responsibility and reconciliation surfaces

If new Phase 46-specific display is needed, add it as a compact `detail-section` inside the selected-server detail grid. Do not create nested cards.

## Required Evidence Labels

Render or verify display paths for these evidence labels:

| Label | Meaning |
|-------|---------|
| Reviewed plan | Selected dry-run plan used as source of truth. |
| Verified preflight | Matching selected `OverallReadiness` is verified. |
| Provisioning action | Operator-triggered reviewed provisioning action. |
| Cloud session | Resulting cloud session id and state. |
| Provider resources | Allowlisted provider resource identifiers and states. |
| Endpoint | Public endpoint host/port when present. |
| Billing owner | Active billing account and assignment. |
| Template/profile | Template, runtime profile, and mission context. |
| Cost source | Estimate, snapshot, or reconciliation source label. |
| Policy state | Policy gate status and approval reference. |
| Reconciliation state | Unknown, duplicate, cleanup-needed, and finding counts. |
| Cleanup risk | Follow-up action when provisioning fails or partially completes. |
| Audit records | Sanitized audit ids, kinds, actor ids, and timestamps. |
| Redaction check | Evidence has no secret/session transport material. |

## Interaction Rules

- Provisioning controls stay unavailable until Phase 45 readiness is verified for the selected dry-run plan.
- If live evidence is missing, show blocked or human-needed copy rather than implying success.
- If provisioning fails or partially completes, show retry/cleanup risk and owner attribution before any next action.
- Refresh controls must remain available so operators can reload plan review, policy status, preflight status, cloud operation result, cost, and reconciliation data.
- Use existing Fluent UI buttons, message bars, status badges, metadata grids, and compact tables.

## Visual Rules

- Use the existing `detail-section`, `cloud-result-*`, `status-badge`, `meta-grid`, and table patterns.
- Fixed-format rows must have stable dimensions and wrap long ids with existing metadata overflow behavior.
- Buttons use icons and concise labels.
- Avoid broad instructional text in the app. Keep copy short and operational.

## Safety Copy

Allowed blocked copy:

- `Live provisioning remains blocked until selected preflight readiness is verified.`
- `Provisioning evidence is human-needed for the selected live scenario.`
- `Review cleanup risk before retrying or moving to teardown evidence.`

Do not claim live provider, runtime, teardown, final billable-resource, or CR acceptance success from automated tests alone.

## Redaction Rules

Display only allowlisted labels, ids, states, timestamps, counts, endpoint host/port, owner references, cost labels, policy references, and audit summaries.

Do not display credential values, browser session material, provider secret values, worker secrets, or secret-bearing transport content.

## Responsive Behavior

At widths below 860px:

- Evidence rows collapse to one column.
- Action buttons wrap or stretch consistently with existing guided actions.
- Long ids and endpoint values must wrap inside their parent row.

## Verification Hooks

Boundary tests should assert:

- Required evidence labels remain present.
- Provisioning UI references verified selected preflight readiness.
- Evidence display helpers avoid forbidden display markers.
- API-side `ValidateReadyForProvisioningAsync` remains present as the authoritative boundary.

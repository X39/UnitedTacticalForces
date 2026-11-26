# Phase 24 UI Specification: Policy Visibility

## Users

- Server administrators managing cloud spending policies, approvals, denials, and automatic actions.
- Server owners inspecting why a cloud action was warned, blocked, pending approval, or automatically acted on for servers they can access.

## Surfaces

### Admin Cloud Operations (`/servers/cloud`)

Extend the existing cloud operations page with policy visibility:

- Add policy metrics to the existing summary strip:
  - pending approvals
  - recent denied decisions
  - automatic actions requiring attention
  - owners over warning/approval/automatic-action thresholds
- Add a `Policies` tab in the current tab row.
- The tab table should show billing account/server/session scope, effective policy, current threshold usage, decision status, pending approval status, automatic action status, and latest evaluation time.
- The detail panel should show the selected policy visibility row:
  - effective policy limits and thresholds
  - current/projected cost and currency
  - decision reason code and risk indicators
  - pending approval or latest approval/denial
  - automatic action status, lifecycle job, retry/failure summary
  - clear cleanup wording: runtime cleanup is separate from provider teardown.

### Server List (`/servers`)

Extend the selected-server cloud runtime detail:

- Show policy status only for the selected server.
- Include effective policy name, current threshold usage, latest decision, pending approval or blocked reason, and automatic action status.
- Keep action buttons unchanged except for existing cleanup action; add only descriptive warning/status badges and next-step guidance.

## Visual Treatment

- Reuse existing `.summary-card`, `.cloud-table`, `.status-badge`, `.meta-grid`, `.risk-note`, and detail panel styles.
- Use warning/danger badges for blocked actions, stale reconciliation, cleanup-needed billable resources, and failed automatic actions.
- Use neutral/info badges for missing policy, no decisions, or notification-only automatic actions.
- Avoid nested cards. Policy details in the side panel should use definition lists and compact lists.
- Keep text short and scan-focused. Do not add in-app instructions or feature explanations.

## Responsive Behavior

- Preserve the existing responsive behavior: summary strip collapses to two columns and then one column; data table scrolls horizontally; detail panel stacks below the table on narrower screens.
- Ensure long reason codes, ids, and sanitized failure summaries wrap with `overflow-wrap: anywhere`.

## Non-Goals

- No policy editing UI in this phase.
- No live provider UAT claim.
- No invoice-grade cost display.
- No raw browser HTTP calls.

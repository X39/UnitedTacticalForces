---
phase: 19
slug: cost-and-reconciliation-visibility
status: draft
created: 2026-06-18
---

# Phase 19 - UI Design Contract

## Surface Scope

- Admin cloud operations view: `sources/WebApp/Pages/Servers/Cloud.razor`
- Server owner detail view: `sources/WebApp/Pages/Servers/Index.razor`
- Styling: existing scoped CSS files for those pages.
- Component library: Microsoft Fluent UI Blazor.

## Layout Contract

- Keep `/servers/cloud` as the primary admin operations dashboard. Add cost and reconciliation data
  into its existing summary strip, tabs, and detail panels.
- Keep `/servers` as a dense server operations page. Add cost and reconciliation data inside the
  selected server detail area instead of creating a separate owner route.
- Use compact cards only for top-level summary values. Do not nest cards inside cards.
- Tables should remain horizontally scrollable on narrow widths and should not wrap action buttons into
  unstable row heights.
- Owner cost/risk data must stay visibly tied to the selected server and must not show unattributed
  provider orphan findings.

## Information Hierarchy

Admin summary strip order:

1. Provider and metadata health.
2. Current estimated runtime cost.
3. Accumulated estimated runtime cost.
4. Latest reconciliation status.
5. Findings and orphans.
6. Billable cleanup risk.

Admin table additions:

- Sessions tab: source label, accumulated cost, freshness/confidence, finding count, cleanup billable count.
- Reconciliation tab: run status, live counts, finding counts, orphan count, cleanup-needed billable count.
- Finding detail panel: safe summary/details, provider resource kind/id/name, attribution, expected local state,
  provider state.

Owner detail additions:

- Cloud runtime section: add cost source, accumulated cost, freshness/confidence, billable resource count.
- Cloud status section: add relevant finding count and cleanup-needed billable resources for each session.
- Add one concise note near cost/risk fields: "Provider teardown stops billable resources; runtime cleanup does
  not delete provider resources."

## Copy Contract

Use these labels exactly where applicable:

- "Estimated runtime cost"
- "Reconciled runtime cost"
- "Adjusted runtime cost"
- "Accumulated estimate"
- "Cost source"
- "Metadata freshness"
- "Confidence"
- "Billable resources"
- "Cleanup risk"
- "Provider teardown"
- "Runtime cleanup"

Avoid these claims:

- "Actual cost"
- "Invoice total"
- "Billed amount"
- "Guaranteed cost"

If source is `Estimated`, displayed values must include "estimated" in either the label or adjacent badge.

## Visual State Contract

- Cost source badges:
  - Estimated: neutral/info badge.
  - Reconciled: success badge.
  - Adjusted: warning badge.
- Metadata freshness:
  - Fresh: success badge.
  - Stale: warning badge.
  - Unknown: neutral badge.
- Reconciliation:
  - Completed with zero findings: success badge.
  - Completed with findings: warning badge.
  - Running: info badge.
  - Failed: danger badge.
- Cleanup risk:
  - No billable cleanup resources: neutral/success.
  - Any cleanup-needed billable resource: danger.
  - Orphan findings: warning unless critical severity exists, then danger.

## Accessibility And Responsiveness

- Icon buttons must keep `Title` text.
- Summary cards and tables must have text labels, not icon-only meaning.
- Monetary values should render with two decimals and currency code.
- Dates should use existing local short date/time formatting.
- Empty states should state that snapshots or reconciliation runs do not exist yet, not imply an error.
- Mobile layouts may stack detail sections, but cost and risk labels must remain adjacent to their values.

## Implementation Constraints

- Do not introduce a new UI palette. Extend existing `status-badge`, `summary-card`, `detail-section`,
  `cloud-table`, and `data-table` patterns.
- Do not use decorative gradients, hero sections, marketing copy, or large illustrative content.
- Do not add visible instructional text describing how to use the whole application.
- Do not introduce raw browser HTTP requests for Phase 19 data; consume repository facades only.


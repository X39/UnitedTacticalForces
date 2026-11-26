# Plan 28-02 Summary: Reconciled And Adjusted Cost Projection

## Completed

- Added `CloudBillingReconciliationSummaryDto`.
- Extended cost visibility DTOs with provider allocated, adjusted, match-record, and applied-adjustment fields.
- Updated `CloudCostVisibilityService` to project billing match records and applied adjustment overlays into admin and server-scoped cost visibility.
- Updated runtime cost summaries, owner intervals, and resource contributions to distinguish estimated, reconciled, and adjusted totals.
- Preserved raw imports, match records, and estimated cost snapshots as immutable source evidence.

## Safety Properties

- Applied adjustments affect read projections only; they do not mutate imported line items, match records, or cost snapshots.
- Pending adjustments remain review-visible but do not alter adjusted totals.
- Server-scoped visibility filters billing evidence by game server before projection.
- Billing row interval matching now uses overlap semantics so provider rows can reconcile partial or precision-rounded intervals.

## Verification

- Covered by `CloudCostVisibilityServiceTests` and the focused CloudBilling slice.

# Phase 28: Reconciled Cost Visibility And Verification - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Surface the v1.6 billing reconciliation workflow through admin and owner-visible projections. This phase connects durable imports, matching records, and applied adjustments to API visibility and WebApp repository/client surfaces. It does not create payment, invoicing, taxation, or official provider-bill semantics.
</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

Use the existing cloud-cost visibility and `CloudAdminRepository` paths. Do not add a raw browser HTTP bridge. Keep imports, matching, and adjustments as separate audit evidence while projecting reconciled and adjusted totals as read models.

### Proposed Shape

- Add match-run and match-record DTO/API endpoints so administrators can trigger matching and inspect review rows.
- Extend cost visibility with reconciliation summary fields derived from match records and applied adjustments.
- Preserve estimated snapshot data and raw import rows; adjusted totals are computed overlays.
- Refresh Kiota after server API shape changes and map new generated models into `CloudAdminModels`.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets

- `CloudBillingImportService` and `CloudBillingImportsController` expose import batches and line items.
- `CloudBillingMatchingService` already persists match runs and records, but lacks an API controller.
- `CloudBillingAdjustmentService` exposes reviewed adjustment create/apply/reject/read APIs.
- `CloudCostVisibilityService` already composes admin and game-server-scoped cloud cost views.
- `CloudAdminRepository` maps generated Kiota DTOs into WebApp-facing models.

### Established Patterns

- DTOs live under `sources/Api/DTO` and projections live in `Transform`.
- Admin cloud endpoints use `[Authorize(Claims.Administrative.Server)]`.
- Server-owner scoped cost visibility remains under `GameServersController` with resource authorization.
- WebApp generated client files under `sources/WebApp/UtfApi` are regenerated through Kiota and wrapped by repository facades.
</code_context>

<specifics>
## Specific Ideas

- Add `CloudBillingMatchesController` under a new `cloud-billing-matches` route.
- Add a `CloudBillingReconciliationSummaryDto` to expose imported, allocated, estimated, discrepancy, adjusted, pending adjustment, and unresolved counts.
- Mark runtime cost summary source as `Adjusted` when applied adjustments are present and `Reconciled` when provider match records exist.
- Add WebApp model and mapping fields for provider allocated and adjusted cost totals while retaining estimated labels.
</specifics>

<deferred>
## Deferred Ideas

- Official invoice/export generation and treasurer reports remain future billing extensions.
- Live authenticated browser and provider UAT remain operator-gated under OPS-02.
</deferred>

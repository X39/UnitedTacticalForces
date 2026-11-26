---
phase: 19
slug: cost-and-reconciliation-visibility
status: draft
created: 2026-06-18
---

# Phase 19 - Research

## Existing Cost Foundation

`CloudRuntimeCostSnapshotService` already has the cost computation needed for visibility:

- `RefreshSessionSnapshotAsync(sessionId, actor, cancellationToken)` creates or replaces the latest
  estimated snapshot for one cloud session.
- `GetLatestSessionSnapshotAsync(sessionId, cancellationToken)` loads the latest estimated snapshot and
  ordered contribution rows.
- `GetSessionCostSummaryAsync(sessionId, cancellationToken)` returns accumulated owner intervals and
  resource contribution summaries.

The service is intentionally local-data based and does not depend on `IHetznerCloudClient`, `HttpClient`,
or live provider reads. That means Phase 19 API endpoints can expose and refresh current estimates
without adding provider mutation risk.

## Existing Reconciliation Foundation

Phase 18 persisted runs and findings and exposed admin endpoints:

- `POST cloud-inventory/reconciliation/runs`
- `GET cloud-inventory/reconciliation/runs`
- `GET cloud-inventory/reconciliation/runs/{runId}`
- `GET cloud-inventory/reconciliation/runs/{runId}/findings`

The generated WebApp client does not yet include these endpoints, so the WebApp cannot consume them
through repository facades until Kiota is refreshed or equivalent generated files are produced.

## API Surface Shape

Recommended API additions:

- Admin:
  - `GET cloud-inventory/cost-visibility`
  - `GET cloud-inventory/sessions/{sessionId}/cost-summary`
  - `POST cloud-inventory/sessions/{sessionId}/cost-snapshot`
- Server scoped:
  - `GET game-servers/{gameServerId}/cloud-cost-visibility`
  - `POST game-servers/{gameServerId}/cloud-cost-snapshot`

Recommended DTO additions:

- `CloudRuntimeCostSummaryDto`
- `CloudRuntimeCostOwnerIntervalDto`
- `CloudRuntimeCostResourceContributionDto`
- `CloudSessionCostVisibilityDto`
- `CloudReconciliationVisibilitySummaryDto`

`CloudSessionCostVisibilityDto` should compose one session status, optional latest cost summary, source
label, current cost freshness/confidence, billable contribution counts, latest relevant reconciliation
run summary, relevant finding counts, orphan indicator, and cleanup risk flags.

## Authorization Rules

- Admin cloud inventory visibility uses `Claims.Administrative.Server`, matching existing cloud
  inventory routes.
- Server-owner visibility uses the existing server-scoped route style:
  `[Authorize($"{Claims.ResourceBased.Server.All}:*")]`.
- Server-scoped reconciliation risk filters by `GameServerFk == gameServerId`. Findings without proven
  game-server attribution are admin-only.
- Cost snapshot refresh must resolve the authenticated database user. Missing users return unauthorized.

## WebApp Surface Shape

Admin `/servers/cloud` already has summary cards and tabs for sessions, plans, resources, estimates, and
metadata. Phase 19 should extend that surface rather than adding a new landing page:

- Add summary cards for current estimated runtime cost, accumulated estimated runtime cost, latest
  reconciliation status, finding counts, orphan count, and billable cleanup risk.
- Add session columns for cost source, accumulated cost, freshness/confidence, finding count, and cleanup
  billable count.
- Add a reconciliation tab or panel showing recent runs and findings with safe summary/details.
- Preserve dry-run estimate wording separately from runtime cost snapshot wording.

Owner `/servers` already has selected server cloud runtime and cloud status sections. Phase 19 should add
compact cost and reconciliation rows inside those sections:

- Estimated/reconciled/adjusted source label.
- Accumulated cost and currency.
- Metadata freshness and confidence.
- Relevant finding count and cleanup risk for the selected server only.
- Copy that states provider teardown is the cost-stop boundary.

## Kiota Refresh Notes

`sources/WebApp/UtfApi/kiota-lock.json` points at `https://localhost:7072/openapi/v1.json` and current
generated files do not include Phase 18 reconciliation endpoints. The ideal path is:

1. Run API locally and expose OpenAPI.
2. Run Kiota generation with the existing lock/output.
3. Commit generated `UtfApi` changes and hand-written repository mappings.

If API build/OpenAPI generation is blocked by the current NuGet socket restriction, the implementation
may need to manually mirror Kiota-generated patterns for the new request builders and models, then record
the blocker in verification. New repository methods still must call `Client.*` generated builders, not
raw browser `HttpClient`.


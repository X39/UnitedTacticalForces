---
phase: 19
slug: cost-and-reconciliation-visibility
status: draft
created: 2026-06-18
---

# Phase 19 - Context

## Goal

Surface current cost, accumulated cost, cost source labels, reconciliation status, and cleanup risk to
administrators and authorized server owners through generated API clients and existing WebApp surfaces.

## Requirements

| ID | Requirement | Phase Scope |
|----|-------------|-------------|
| COST-05 | User can see running cost snapshots while real cloud resources exist. | Covered by API cost visibility DTOs/endpoints and WebApp cost panels. |
| COST-08 | UI labels cloud costs as estimated, reconciled, or adjusted. | Covered by DTO source labels and explicit UI copy. |
| VIS-12 | Administrator can see current cost, accumulated cost, reconciliation status, finding counts, orphan indicators, and cleanup risk in the cloud operations surface. | Covered by `/servers/cloud` admin view updates. |
| VIS-13 | Server owner can see current cost, accumulated cost, cost source label, and relevant reconciliation risk for their allowed cloud server scope. | Covered by server-scoped API and `/servers` detail updates. |
| VIS-14 | Admin and owner views distinguish runtime cleanup from provider teardown and show billable resources that still exist after runtime stop. | Covered by risk copy, resource contribution display, and billable-resource counts. |
| VIS-15 | Cost and reconciliation DTOs are available through generated WebApp API clients and repository facades, not raw browser HTTP bridges. | Covered by generated client refresh plus repository facade methods. |

## Explicit Non-Goals

- No provider invoice import, provider billing API integration, or invoice-grade actual cost claim.
- No automatic deletion of orphan or cleanup-needed provider resources.
- No new cloud provisioning, worker dispatch, or Hetzner label mutation behavior.
- No new authorization model; use existing admin and server-resource policies.
- No replacement of existing local game-controller behavior.

## Existing Local Facts

- Phase 17 added `ICloudRuntimeCostSnapshotService`, persisted `CloudCostSnapshot` rows, source enum
  values `Estimated`, `Reconciled`, and `Adjusted`, owner intervals, billable resource contributions,
  freshness, confidence, assumptions, and idempotent refresh behavior.
- Phase 18 added durable `CloudProviderReconciliationRun` and `CloudProviderReconciliationFinding`
  records plus admin-only trigger/read endpoints in `CloudInventoryController`.
- `CloudSessionStatusDto` already projects session, inventory resources, bootstrap, worker, cleanup,
  orphan, and teardown state for admin and server-scoped reads.
- `CloudRuntimeStatusDto` already drives the server owner cloud runtime section but does not include
  cost or reconciliation risk yet.
- `sources/WebApp/UtfApi` is stale relative to Phase 18 and does not contain reconciliation builders
  or models.
- `CloudAdminRepository` already maps generated cloud inventory DTOs into WebApp-local models; it does
  not expose reconciliation runs/findings or runtime cost summaries yet.
- `GameServerRepositoryImpl` still contains some raw `HttpClient` bridge methods. Phase 19 must not add
  new raw browser HTTP bridges for cost and reconciliation surfaces.

## Design Boundaries

- Admin endpoints can expose all reconciliation findings, including unknown provider resources that
  cannot be attributed to a UTF game server.
- Server-owner endpoints must only expose findings that can be attributed to the requested game server.
  Unknown provider resources with no proven `GameServerFk` remain admin-only.
- UI and DTO copy must use source labels: `Estimated`, `Reconciled`, or `Adjusted`. Current calculated
  runtime snapshots are `Estimated` until later invoice/provider-billing work creates other sources.
- Runtime cleanup and provider teardown stay distinct: runtime cleanup may stop or clean worker-local
  state, but provider deletion remains the cost-stop boundary for billable Hetzner resources.
- Cost details must remain secret-safe. DTOs may include assumptions, metadata freshness, confidence,
  source label, currency, owner intervals, and resource contribution totals, but not provider tokens,
  worker credentials, Steam credentials, bearer headers, bootstrap secret values, or raw user data.


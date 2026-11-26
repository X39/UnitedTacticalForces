# Phase 26: Cost Matching And Discrepancy Engine - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Match durable provider billing import line items from Phase 25 to local cloud inventory, sessions, billing owner intervals, and estimated cost snapshot contributions. This phase creates persistent matching and discrepancy records for COST-21 and COST-22. It does not implement operator adjustment workflows or WebApp visibility; those remain Phase 27 and Phase 28.
</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

Use the existing Hosting bounded context and cloud cost/reconciliation conventions. Prefer deterministic exact provider-resource matching first, then bounded lower-confidence fallback records. Preserve unresolved rows explicitly instead of silently assigning costs.

### Proposed Model Shape

- Add a match run entity keyed to one import batch and an algorithm version.
- Add match record rows that can represent one imported line item split across one or more billing owner intervals.
- Persist match method, confidence score, review status, imported/allocated/estimated/discrepancy amounts, currency, related inventory/session/game-server/billing/cost contribution FKs, and sanitized explanations.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets

- Phase 25 added `CloudBillingImportBatch` and `CloudBillingImportLineItem` under `sources/Api/Data/Hosting`.
- `CloudResourceInventoryRecord` stores provider identifier, provider resource id, resource kind, session, game server, billing account, billing assignment, state, and deletion timestamp.
- `CloudGameServerSession` links cloud runtime sessions to game servers and current billing assignment snapshots.
- `GameServerBillingAssignment` stores historical owner intervals with assigned and unassigned timestamps.
- `CloudCostSnapshotContribution` stores estimated resource/owner interval cost facts and can be compared against imported provider amounts.
- `CloudCostIntervalCalculator.Intersect` already models resource/owner interval intersection logic.
- `LifecycleFailureReasonSanitizer` is the existing safe-summary/details mechanism.

### Established Patterns

- Add Hosting entities under `sources/Api/Data/Hosting`, stable DTO-facing enums under `sources/Contract/GameServer` when later API surfaces will need them, and EF relationship configuration in `ApiDbContext`.
- Use SQLite-backed `tests/Api.Tests` service tests for persistence, idempotency, and edge cases.
- Avoid provider mutation and raw secret output in all reconciliation/cost paths.
</code_context>

<specifics>
## Specific Ideas

- Match by `(ProviderIdentifier, ResourceKind, ProviderResourceId)` before any name/display fallback.
- Treat no exact match, multiple exact matches, missing owner interval, and missing estimate contribution as review-visible outcomes.
- Split imported line-item amounts across overlapping billing assignment intervals by overlap duration.
- Compare allocated provider amount to overlapping estimated snapshot contribution amount in matching currency.
</specifics>

<deferred>
## Deferred Ideas

Operator review actions, manual adjustments, audit of adjustments, generated API client refresh, and WebApp visibility are deferred to Phases 27-28.
</deferred>

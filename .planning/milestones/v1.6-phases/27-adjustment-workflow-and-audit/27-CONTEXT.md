# Phase 27: Adjustment Workflow And Audit - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Add immutable adjustment overlays for reviewed provider billing matches. This phase stores administrator corrections linked to imports, match records, resources, owner assignments, and audit history. It does not mutate raw import rows, match records, estimated snapshots, or provider state.
</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

Use the Hosting bounded context and existing admin-only API authorization. Treat adjustments as immutable evidence overlays with status transitions rather than in-place corrections to source facts. Applying an adjustment changes only adjustment status and audit state.

### Proposed Model Shape

- Add adjustment status enum.
- Add `CloudBillingAdjustment` linked to a match record and denormalized historical scope fields.
- Store previous amount, provider allocated amount, adjusted amount, currency, reason, actor/status timestamps, and rejection/apply metadata.
- Add service-level duplicate prevention for pending/applied adjustments per match record.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets

- `CloudBillingMatchRecord` provides the reconciled scope: import line item, match run, inventory/session/game server, billing account, billing assignment, contribution, allocation interval, amounts, and currency.
- `HostingAuditService` and `HostingAuditRecord` are the standard immutable audit mechanism.
- Controllers already use `[Authorize(Claims.Administrative.Server)]` for cloud administrative operations.
- `LifecycleFailureReasonSanitizer` redacts secret-like text for persisted details and audit output.

### Established Patterns

- Store API payloads under `sources/Api/DTO/Payloads`.
- Add API DTO projections through `Transform`.
- Use focused SQLite service/controller tests in `tests/Api.Tests`.
</code_context>

<specifics>
## Specific Ideas

- Valid adjustment scope requires a match record with historical owner attribution (`BillingAccountFk`, `BillingAssignmentFk`, allocation interval, and currency).
- Creation stores the historical owner/scope from the match record so later owner transfers do not rewrite adjustment attribution.
- Applying and rejecting require pending status.
- Duplicate pending/applied adjustments for the same match record are rejected.
</specifics>

<deferred>
## Deferred Ideas

Reconciled-cost visibility that applies adjustment overlays to UI/API aggregate projections remains Phase 28.
</deferred>

# Phase 25: Billing Import And Normalization - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Add a durable provider billing import foundation that stores Hetzner invoice/consumption exports as normalized, redacted, idempotent billing facts. This phase covers import batch persistence, normalized provider line items, parser diagnostics, duplicate detection, admin-only API boundaries, and focused verification for COST-13 and COST-20.

</domain>

<decisions>
## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss was skipped by project configuration. Use the existing Hosting bounded context, EF Core persistence patterns, admin-only cloud controller authorization, redacted diagnostics, and API test style already used by cloud reconciliation and spending-policy phases.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ApiDbContext` already owns Hosting `DbSet` registration and relationship configuration.
- `CloudProviderReconciliationRun` and `CloudProviderReconciliationFinding` provide close analogs for durable provider-oriented records, safe DTO projection, controller endpoints, and tests.
- `LifecycleFailureReasonSanitizer` and `HostingAuditService` provide existing redaction and immutable audit mechanics.
- `HetznerProviderConfiguration.HetznerProviderIdentifier` is the stable provider identifier to reuse for Hetzner imports.

### Established Patterns
- Add Hosting entities under `sources/Api/Data/Hosting`.
- Add shared enum contracts under `sources/Contract/GameServer` when DTOs need stable status values.
- Expose admin cloud operational endpoints from API controllers with `[Authorize(Claims.Administrative.Server)]`.
- Use focused SQLite-backed tests under `tests/Api.Tests` for persistence, restart-safety, and redaction.

### Integration Points
- Phase 25 records feed later v1.6 phases for matching, adjustment, and visibility.
- Import API belongs near existing cloud inventory/reconciliation admin surfaces.
- Duplicate prevention should be database-backed through source hash/provider metadata, not process memory.

</code_context>

<specifics>
## Specific Ideas

No additional user-specific requirements were provided. Use ROADMAP success criteria and existing cloud cost/reconciliation conventions as the implementation contract.

</specifics>

<deferred>
## Deferred Ideas

Matching imported facts to resources, discrepancy review, adjusted overlays, and WebApp visibility are deferred to Phases 26-28.

</deferred>

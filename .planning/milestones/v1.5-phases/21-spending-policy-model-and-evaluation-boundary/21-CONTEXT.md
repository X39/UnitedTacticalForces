# Phase 21: Spending Policy Model And Evaluation Boundary - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Add durable policy configuration and an evaluation boundary that can classify billable cloud actions using v1.4 cost snapshots, owner assignments, and reconciliation risk.

Success criteria from the roadmap:

1. Policy entities, enums, EF configuration, migrations, and DTOs store spending limits, warning thresholds, approval thresholds, automatic-action settings, owner/global scope, effective dates, and audit metadata.
2. Policy evaluation service produces allow, warn, approval-required, or deny decisions for a proposed billable action without mutating provider resources.
3. Evaluation consumes current estimated cost, projected cost, billing owner, metadata freshness, reconciliation freshness, cleanup-needed resources, and unknown billable provider-resource risk.
4. Policy decisions, errors, and input snapshots are persisted in a restart-safe way with sanitized failure details.
5. Tests cover policy validation, effective-policy selection, threshold math, stale-data risk handling, and restart-safe idempotency.

</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use the roadmap phase goal, success criteria, requirements COST-07, COST-19, and RELY-17, and codebase conventions to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research.

</code_context>

<specifics>
## Specific Ideas

No specific requirements beyond the roadmap phase description and success criteria.

</specifics>

<deferred>
## Deferred Ideas

None.

</deferred>

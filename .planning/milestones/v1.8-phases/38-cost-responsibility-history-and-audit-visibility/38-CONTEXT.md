# Phase 38: Cost Responsibility, History, And Audit Visibility - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Authorized users can inspect active and historical cloud cost responsibility for cloud sessions without confusing internal attribution with payment, customer invoicing, taxes, checkout, or legal invoice generation. The phase should preserve authorization-scoped projections for owner assignments, estimated/reconciled/adjusted labels, reconciliation status, cleanup risk, lifecycle context, policy context, and sanitized audit context.

</domain>

<decisions>
## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion - discuss phase was skipped per project setting. Use the ROADMAP goal, COST-01/COST-02, existing generated-client/repository conventions, and prior v1.8 decisions to guide implementation. Prefer a first-class cost-responsibility/history projection when that keeps attribution, history, and audit evidence clearer than extending the operational cost dashboard.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Initial recovery notes indicate relevant foundations already exist in `BillingAccount`, `GameServerBillingAssignment`, `HostingAuditRecord`, cloud sessions, cost snapshots, billing matches/adjustments, reconciliation findings, policy decisions, `CloudCostVisibilityService`, `GameServersController`, `IGameServerRepository`, `GameServerRepositoryImpl`, `/servers`, and `/servers/cloud`.

</code_context>

<specifics>
## Specific Ideas

- Keep owner-facing wording around "cost responsibility", "cost owner", and "owner assignment"; avoid payment, checkout, invoice, customer billing, or tax language.
- Server-scoped authorized users should see relevant responsibility/history for scopes they can inspect; administrators can retain broader fleet/admin visibility.
- Historical rows should remain inspectable after ownership transfer, provider teardown, runtime cleanup, and local server deletion without re-enabling operations on inactive servers.
- Provider invoice/import references should stay internal reconciliation evidence, not customer-facing invoice workflow.
- Generated Kiota files and EF migrations remain generated-only; use repository facades for WebApp integration.

</specifics>

<deferred>
## Deferred Ideas

- Live Hetzner, Steam, Arma, authenticated browser, provider mutation, provisioning, teardown, final provider billable-resource verification, and Phase 30 UAT evidence remain deferred.
- Payment processing, customer invoicing, taxes, checkout, legal invoice generation, direct Hetzner billing API adapters, and treasurer export workflows remain out of scope.

</deferred>

# Phase 24 Context: Policy Visibility And Verification Hardening

## Roadmap Intent

Phase 24 closes v1.5 by making spending policy state visible to administrators and affected server owners, refreshing generated WebApp clients, and collecting final safety verification. Phases 21-23 already added policy configuration, dry evaluation, persisted decisions, approval gates, scoped exceptions, and automatic action execution. This phase should not add a new policy engine; it should project the existing durable state in useful operational views.

## Requirements

- VIS-16: Administrator can inspect effective policy, current threshold usage, pending approvals, recent denials, automatic actions, and policy evaluation history.
- VIS-17: Server owner can see relevant policy warnings, approval requirements, blocked-action reasons, and automatic shutdown status for cloud servers they are allowed to inspect.
- VIS-18: Policy DTOs are available through generated WebApp API clients and repository facades, not raw browser HTTP bridges.
- RELY-20: Tests cover policy persistence, threshold math, stale-data risk handling, approval expiry, authorization boundaries, automatic-action job enqueueing, restart-safe idempotency, and secret redaction.

## Existing Implementation Baseline

- `CloudSpendingPoliciesController` exposes admin-only policy configuration, effective policy lookup, dry evaluations, approvals, and automatic-action processing.
- `CloudSpendingPolicyDecision`, `CloudSpendingPolicyApprovalRequest`, and `CloudSpendingPolicyAutomaticActionAttempt` already persist the policy evidence needed for visibility.
- `ICloudCostVisibilityService` exposes admin and game-server-scoped current cost/reconciliation projections.
- `GameServersController` exposes server-scoped cloud runtime and cost visibility through resource authorization.
- `CloudAdminRepository` and `GameServerRepository` wrap generated Kiota request builders for WebApp cloud and server details.
- `/servers/cloud` is a dense admin operations page with summary cards, tabs, tables, and detail panel.
- `/servers` selected-server details already include cloud runtime, cost snapshot, reconciliation findings, and cleanup guidance.

## Scope

Add read-focused policy visibility projections and WebApp views. Mutating behavior remains limited to the existing approval, policy configuration, and automatic action endpoints. OPS-02 live/authenticated/provider UAT remains operator-gated and should be recorded as deferred evidence rather than claimed as passed.

## Constraints

- Do not expose Steam credentials, provider tokens, worker credentials, or raw unredacted failure details.
- Keep policy state server-authorized: admin aggregate visibility requires server admin, and server-owner visibility must be scoped to authorized game-server access.
- Keep generated Kiota code generated; hand-written behavior belongs in WebApp repositories and pages.
- Do not conflate runtime cleanup with provider teardown in labels, DTOs, or guidance.

---
phase: 23-automated-shutdown-and-policy-action-execution
type: context
autonomous: true
discussion: skipped
requirements:
  - COST-18
  - RELY-19
created: 2026-06-18
---

# Phase 23 Context

Phase 23 starts after the Phase 21 policy/evaluation foundation and Phase 22 approval gates. The API can now persist policy decisions, create approval requests, block direct billable action endpoints, and accept acknowledged warnings or approved scoped exceptions.

The remaining Phase 23 problem is automatic action execution: when a persisted evaluation decision reaches the configured automatic-action threshold, the system needs durable, idempotent attempts that can enqueue the correct action without conflating runtime stop, runtime cleanup, notification-only behavior, or provider teardown.

## Current Inputs

- `CloudSpendingPolicyDecision` stores `ConfiguredAutomaticAction`, `AutomaticActionThreshold`, and `IsAutomaticActionThresholdReached`.
- `CloudSpendingPolicyAction` has `None`, `NotifyOnly`, `StopRuntime`, `CleanupRuntime`, and `DeleteProviderResources`.
- `GameServersController` can enqueue lifecycle jobs through `IGameServerLifecycleJobService`.
- `CloudInventoryController` can execute provider teardown through `ICloudTeardownService`.
- Runtime cleanup is currently represented as a lifecycle `Delete` job against an active cloud worker and does not delete provider resources.
- Provider teardown is a direct provider mutation and is the cost-stop boundary.

## Constraints

- Do not enqueue duplicate automatic actions for the same policy decision/action/scope.
- Do not delete provider resources unless the effective policy explicitly selected `DeleteProviderResources`.
- Preserve owner attribution on jobs and action records.
- Keep notification-only, runtime stop, runtime cleanup, and provider teardown behavior distinct.
- Do not add browser/client work; Phase 24 owns visibility and generated clients.
- Avoid secrets and raw provider payloads in action records, logs, and DTOs.

## Open Execution Assumptions

- Automatic action execution should be service-driven and explicit, not hidden inside the Phase 21 evaluator.
- Phase 23 can expose an admin API to process pending automatic policy actions manually or scheduler-style; always keep the operation idempotent.
- Provider teardown should be represented as a durable action attempt before calling the provider teardown service.

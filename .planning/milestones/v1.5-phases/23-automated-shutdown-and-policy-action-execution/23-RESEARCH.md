# Phase 23 Research: Automated Shutdown And Policy Action Execution

## Existing Behavior

Policy evaluation already computes whether the configured automatic-action threshold was reached. It persists enough context to identify provider, game server, session, billing account, billing assignment, current/projected costs, and the policy decision reason.

Lifecycle jobs already snapshot billing account and billing assignment when enqueued through the lifecycle job service. Provider teardown already deletes provider resources through a separate service boundary.

## Implementation Direction

Add an automatic action attempt model and service under `CloudPolicies`/Hosting:

- one durable attempt per policy decision/action/scope
- stable idempotency key or unique index
- status for pending, enqueued, succeeded/no-op, failed
- action kind copied from `ConfiguredAutomaticAction`
- references to decision, policy, game server, session, billing account, billing assignment, lifecycle job when applicable
- sanitized failure fields and timestamps

The service should:

- find decisions with `IsAutomaticActionThresholdReached` and actionable configured actions
- skip `None` and treat `NotifyOnly` as a durable no-op/notification record without enqueuing mutation work
- enqueue `StopRuntime` as a lifecycle stop job when a game server exists
- enqueue `CleanupRuntime` as a lifecycle cleanup/delete job only when an active cloud session exists
- call provider teardown only for `DeleteProviderResources`, and only when session and owner scope are known
- keep all actions idempotent for the same decision/action/scope

## Safety Notes

Provider teardown is destructive and cost-stopping. It must not be reused for runtime cleanup. Runtime cleanup removes worker-local runtime/files and may not stop Hetzner billing. Stop runtime is a lifecycle stop request and should not imply cleanup or provider deletion.

## Test Direction

Focused backend tests should cover:

- automatic attempt idempotency across repeated service calls and DbContext restart
- `NotifyOnly` does not enqueue lifecycle or provider teardown
- stop/cleanup enqueue lifecycle jobs with owner attribution
- provider teardown only runs for `DeleteProviderResources`
- failed action attempts persist sanitized failure information
- retry visibility/status transitions remain durable

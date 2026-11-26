# Phase 22 Research: Approval Gates And Exception Workflow

## Scope

Phase 22 builds on Phase 21. The durable policy model and dry evaluator already exist; this phase must use them to gate billable cloud actions and add durable exception approvals.

Requirements:

- COST-15: User can see a policy evaluation result before a billable cloud provisioning, start, restart, update, or future resize action proceeds.
- COST-16: Billable cloud actions are allowed, warned, blocked, or marked approval-required based on owner policy, projected cost, current accumulated cost, and reconciliation risk.
- COST-17: Authorized approver can approve or deny a policy exception with durable scope, expiration, reason, and audit history.
- RELY-18: Policy gates are authorization-sensitive and cannot be bypassed by directly calling provisioning, lifecycle, approval, or automatic-action endpoints.

## Existing Surfaces

- `CloudSpendingPolicyEvaluationService` persists dry evaluation decisions and already classifies `Allow`, `Warn`, `ApprovalRequired`, and `Deny`.
- `CloudInventoryController.ProvisionDryRunPlanAsync` directly calls `ICloudProvisioningService.ProvisionDryRunPlanAsync` after admin authorization and actor lookup.
- `CloudInventoryController.TeardownSessionAsync` directly calls `ICloudTeardownService.TeardownSessionAsync` after admin authorization and actor lookup.
- `GameServersController` lifecycle endpoints route start, restart, install/update, stop, collect logs, and cloud-runtime cleanup through `EnqueueLifecycleJobAsync`.
- The billable or cost-sensitive cloud lifecycle actions for this phase are cloud provisioning, cloud runtime start, restart, update/install/upgrade, cloud runtime cleanup, and provider teardown. Local-only game servers should keep existing behavior.
- Future resize does not have a production endpoint yet, but `ECloudBillableActionKind.ProviderResize` exists. The gate service should expose a reusable evaluation method so a resize endpoint can use the same contract later.

## Implementation Direction

- Add a policy gate service in `sources/Api/Services/CloudPolicies` that resolves the action target, builds a Phase 21 evaluation payload, persists the decision, and returns a gate outcome.
- Keep provider/lifecycle mutation outside the evaluator. The gate runs before provisioning, teardown, or lifecycle enqueueing.
- Preserve existing authorization attributes. Gate checks supplement authorization; they do not replace endpoint claims.
- For `Allow`, proceed.
- For `Warn`, return a policy gate result before proceeding unless the caller supplies a matching acknowledged decision id.
- For `ApprovalRequired`, create or return a pending durable approval request and block execution unless the caller supplies a matching approved, unexpired request.
- For `Deny`, block execution and return the persisted policy decision.
- Projected additional cost should come from the active dry-run cost estimate for provisioning when available. Runtime start/restart/update and teardown can evaluate with `0` projected additional cost while still considering current cost and risk inputs.
- Store only bounded sanitized request/approval reasons. Do not persist raw provider payloads, provider tokens, Steam credentials, worker credentials, bearer headers, or user-data text.

## Data Model Direction

Add a durable approval request entity under `sources/Api/Data/Hosting` with:

- primary key, status, provider identifier, action kind
- decision/policy references
- scope references: game server, cloud session, billing account, billing assignment, dry-run plan/resource when available
- requested projected cost/currency and decision reason
- requester, approver/denier, created/resolved/expiration timestamps
- bounded sanitized request reason, approval reason, denial reason, and audit details

Use restrict-delete relationships so approval history survives owner/resource cleanup.

## API Direction

Use existing admin-only policy controller or a closely named policy approvals controller for approval APIs:

- list pending/recent approval requests
- get one request
- approve one request with expiration and reason
- deny one request with reason

Billable action endpoints should return a gate result DTO when policy blocks immediate execution. That DTO should include the policy decision DTO, approval request DTO when applicable, and flags such as `canProceed`, `requiresAcknowledgement`, and `requiresApproval`.

## Test Direction

Add focused backend tests around:

- gate service maps provisioning and cloud runtime actions to the right `ECloudBillableActionKind`
- warning decisions require acknowledgement before the action proceeds
- denied decisions do not call provisioning/lifecycle/teardown services
- approval-required decisions create durable pending approval requests
- approved unexpired requests allow the action and are rechecked at execution time
- expired, denied, wrong-scope, or wrong-action approvals do not bypass gates
- approval endpoints require admin server claim and resolve actors server-side

## Risks

- Existing `tests/Api.Tests` has unrelated compile drift in CloudCosts, CloudPlanning, and CloudReconciliation. Continue to add focused tests, but record when the full test project cannot compile because of pre-existing files.
- Returning gate results from endpoints that formerly returned provisioning/lifecycle DTOs may require OpenAPI/client refresh in Phase 24. For Phase 22, backend DTOs and tests are sufficient.
- Teardown is cost-stop rather than cost-generating, but it is destructive and already represented by `ProviderTeardown`. Gate it as a sensitive policy action while preserving explicit teardown boundaries.

## Open Questions Resolved

- Warnings are not silently ignored. A warning gate returns a visible result first; the caller must acknowledge the specific decision before execution.
- Approvals are scoped. A valid approval must match action, owner, and resource/session scope, and must be unexpired when the action is executed.
- Local game-server lifecycle operations are not gated by cloud spending policy unless an active cloud session exists for the target server.


# Phase 37: Provisioning, Runtime Operation, And Teardown - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Authorized users can provision reviewed plans, operate cloud runtime jobs, and tear down provider resources while seeing cost-stop and cleanup boundaries clearly.

Success criteria from ROADMAP:

1. Authorized user can provision provider resources from the reviewed valid cloud dry-run plan and see session, resource, bootstrap, and worker status.
2. Authorized user can operate install, update, start, stop, restart, collect logs, and runtime cleanup actions with durable job progress and sanitized failure visibility.
3. Authorized user can tear down provider resources through an explicit cost-stop action that is separate from runtime stop, runtime cleanup, and local server deletion.
4. Authorized user can see retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states after provisioning or teardown.
5. Runtime status and provider teardown status remain visible enough for the user to understand which resources may still cost money.

Implementation boundaries:

- Keep the first cloud shape to one Arma 3 server per Hetzner-backed worker.
- Do not add multi-provider or multi-session worker scope.
- Keep provider teardown as the explicit cost-stop boundary.
- Do not claim live Phase 30 UAT closure.
- Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, and teardown UAT remain deferred by user instruction.

</domain>

<decisions>
## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, prior phase artifacts, and codebase conventions to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Phase 37 builds on the Phase 33-36 cloud workflow entry, generated-client/repository gate foundation, dry-run review, and policy approval UX.

</code_context>

<specifics>
## Specific Ideas

- Preserve the explicit distinction between runtime stop/cleanup and provider teardown cost-stop.
- Surface retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states without exposing secrets or raw provider payloads.
- Reuse existing lifecycle job, policy gate, repository facade, and `/servers` UI patterns where possible.

</specifics>

<deferred>
## Deferred Ideas

- Live Hetzner provider mutation proof.
- Steam and Arma runtime UAT proof.
- Authenticated browser UAT.
- Phase 30 closure evidence.

</deferred>

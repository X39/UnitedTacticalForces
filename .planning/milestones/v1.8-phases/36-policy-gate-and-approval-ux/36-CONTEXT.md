# Phase 36: Policy Gate And Approval UX - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Authorized users and approvers can resolve spending policy gates as explicit workflow states before billable actions continue.

This phase consumes the existing policy gate and approval foundations from prior phases. It must not redesign policy
administration, auto-bypass warnings, approvals, denials, expiration, scope, or decision identity, or mutate provider
resources before a matching gate state allows the requested action.

Success criteria from ROADMAP:
- Authorized user can see allow, warning, approval-required, and denied policy outcomes as actionable workflow states.
- Authorized user can acknowledge warning decisions or follow approval next steps only for the matching policy scope,
  expiration, and decision identity.
- Authorized approver can approve or deny provisioning and teardown requests from the cloud server workflow with
  durable audit context.
- Expired, denied, mismatched, or missing approvals keep the requested billable action blocked and show the user the
  next required action.

</domain>

<decisions>
## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP
phase goal, success criteria, existing policy gate foundations, prior cloud workflow conventions, and codebase patterns to
guide decisions.

### Verification Boundary
- All UAT is deferred per user instruction. Do not require or claim manual authenticated browser testing, live provider
  mutation, Steam, Arma, Hetzner, provisioning, teardown, or Phase 30 UAT as passed for this phase.
- Automated verification should focus on durable gate state behavior, authorization-sensitive action boundaries,
  approval/denial transitions, expiration/mismatch blockers, repository/client contracts, and UI boundary rendering.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research.

</code_context>

<specifics>
## Specific Ideas

No additional UX preferences were gathered because discuss phase is skipped. Preserve the existing Blazor/Fluent UI
workflow style and make gate outcomes explicit, actionable, and scope-bound.

</specifics>

<deferred>
## Deferred Ideas

Manual/authenticated/browser/live-provider/Steam/Arma/Hetzner/provisioning/teardown/Phase 30 UAT is deferred.

</deferred>

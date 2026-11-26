# Phase 22: Approval Gates And Exception Workflow - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Gate billable provisioning and lifecycle paths with policy evaluation and provide a durable approval/denial workflow for authorized exceptions.

Success criteria from the roadmap:

1. Provisioning, runtime start/restart/update, teardown-sensitive actions, and future resize extension points evaluate policy before creating or continuing billable cloud usage.
2. Users receive a policy result that explains allowed, warned, blocked, or approval-required decisions before the billable action proceeds.
3. Approval requests persist requested action, owner, server/session/resource scope, projected cost, threshold trigger, requester, expiration, and sanitized reason.
4. Authorized approvers can approve or deny requests, and approved exceptions are scoped, expiring, auditable, and checked again at action execution time.
5. Authorization tests prove direct endpoint calls cannot bypass policy gates or approve exceptions without the required claims.

</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use the roadmap phase goal, success criteria, requirements COST-15, COST-16, COST-17, and RELY-18, the completed Phase 21 policy/evaluation foundation, and codebase conventions to guide decisions.

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

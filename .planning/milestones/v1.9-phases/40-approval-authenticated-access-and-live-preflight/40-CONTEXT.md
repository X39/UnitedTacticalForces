# Phase 40: Approval, Authenticated Access, And Live Preflight - Context

**Gathered:** 2026-06-21
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Live execution is either approved and ready with complete preflight evidence, or blocked with exact missing operator/environment prerequisites before any provider mutation.

Implementation boundaries from ROADMAP:

- Do not create, update, or delete provider resources in this phase.
- Store token references only.
- Never store cookies, authorization headers, bearer tokens, OAuth material, provider tokens, Steam credentials, worker secrets, worker hashes, or raw secret-bearing payloads.

Success criteria:

1. Approval record names approver, timestamp, project, token reference, resource shape, budget ceiling, maximum lifetime, cleanup window, selected game server, billing account, dry-run plan, and policy approval identifier when required.
2. Authenticated administrator and server-owner access to `/servers/cloud`, `/servers`, and required API surfaces is verified without storing session material.
3. Provider enabled/verified status, metadata freshness, selected game server, active billing assignment, valid dry-run plan, cost estimate, policy gate, and reconciliation status are recorded.
4. The selected shape is within budget and cleanup window, or the blocker is classified before live mutation.
5. Phase 41 remains blocked until all required preflight rows have non-pending evidence.

</domain>

<decisions>
## Implementation Decisions

### The Agent's Discretion

All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, existing codebase conventions, and security constraints to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research.

</code_context>

<specifics>
## Specific Ideas

No additional user-specific requirements were provided. Refer to ROADMAP phase description and success criteria.

</specifics>

<deferred>
## Deferred Ideas

None.

</deferred>

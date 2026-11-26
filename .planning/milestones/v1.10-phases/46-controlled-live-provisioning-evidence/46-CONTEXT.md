# Phase 46: Controlled Live Provisioning Evidence - Context

**Gathered:** 2026-06-22
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Operator can run or safely block exactly one approved live WebApp provisioning path with complete evidence and no hidden billable resources.

The reviewed `/servers` plan is the source of truth. Provisioning evidence must capture session id, provider resource identifiers, endpoint, billing owner, template/profile context, cost source, policy state, reconciliation state, and sanitized audit records. Failure or partial provisioning must capture cleanup risk, retry/manual cleanup action, and visible owner attribution. Reconciliation or provider-side inspection must confirm no duplicate or hidden billable resources beyond the approved scenario.

</domain>

<decisions>
## Implementation Decisions

### The agent's discretion

All implementation choices are at the agent's discretion because discuss was skipped. Use ROADMAP phase goal, success criteria, requirements, existing Phase 45 preflight evidence, and codebase conventions to guide decisions.

### Safety boundary

Do not execute live provider mutation without operator-supplied approval/readiness evidence. If evidence is unavailable in this workspace, classify the live execution path as `human_needed` or blocked instead of claiming pass status.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Phase 45 completed the approval, access, readiness checklist, and selected readiness UI gate.

</code_context>

<specifics>
## Specific Ideas

- Preserve the existing `/servers` reviewed-plan and repository-facade flow.
- Add evidence capture around successful, failed, and blocked provisioning results if code gaps exist.
- Keep provider teardown as the later cost-stop boundary; Phase 46 should only classify cleanup risk and duplicate-resource evidence for provisioning.
- Avoid exposing provider credential, browser session, worker credential, or secret-bearing transport material in UI, logs, comments, or verification artifacts.

</specifics>

<deferred>
## Deferred Ideas

Provider teardown, runtime validation, final billable count closure, and CR acceptance classification stay in Phases 47-49.

</deferred>

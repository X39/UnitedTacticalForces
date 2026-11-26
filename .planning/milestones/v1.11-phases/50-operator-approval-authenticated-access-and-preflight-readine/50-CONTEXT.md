# Phase 50: Operator Approval, Authenticated Access, And Preflight Readiness - Context

**Gathered:** 2026-06-22
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase completes or truthfully blocks the operator-controlled preflight package required before any live
Hetzner provider mutation. It records approval, authenticated access, provider configuration, metadata freshness,
active billing owner, current dry-run, template/profile usability, cost budget, policy state, reconciliation state,
Phase 40 readiness refresh, and redaction readiness. It must not mutate provider resources, run provisioning, or
claim Phase 30 or CR-0001 acceptance pass status.

</domain>

<decisions>
## Implementation Decisions

### Approval Package And Selected Scenario
- Use `.planning/v1.7-LIVE-UAT-EVIDENCE.md` plus the existing live-preflight WebApp/API evidence rows as the source of truth, updating only safe reference values and blocker classifications.
- Record Hetzner project and token reference only; never record raw token material or secret-bearing provider output.
- Treat budget ceiling, maximum lifetime, cleanup window, and selected resource shape as required before live mutation can be classified ready.
- If any required operator field remains unavailable, preserve `blocked-pending-operator-input` with exact blocker rows instead of claiming readiness.

### Authenticated Access And Safe Evidence
- Verify `/servers/cloud`, `/servers`, and required protected API surfaces for administrator and server-owner roles.
- Record only route, role/scope, timestamp, visible state summary, and sanitized screenshot or note references as browser evidence.
- Record explicit blockers per role and surface when authenticated sessions are unavailable.
- Reject cookies, authorization headers, bearer values, OAuth material, session ids, and raw browser storage entirely.

### Readiness Gate, Mutation Boundary, And Classification
- Treat refreshed Phase 40 readiness as supporting evidence only; it is not authorization by itself.
- Require provider configuration, metadata freshness, active billing owner, valid current dry-run, template/profile usability, cost budget, policy state or approval, reconciliation state, and redaction readiness before mutation can proceed.
- When all gates pass, classify the package as `ready-for-live-execution` and leave actual provisioning to Phase 51 only.
- When gates do not pass, keep `blocked-pending-operator-input` or a concrete blocker classification with follow-up actions, without changing CR acceptance.

### the agent's Discretion
The agent may choose the safest artifact structure, helper naming, and verification approach consistent with the
existing v1.10 evidence surfaces, repository facades, boundary tests, and redaction constraints.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `.planning/v1.7-LIVE-UAT-EVIDENCE.md` already contains the approval, authenticated access, provider, billing owner, dry-run, runtime, cleanup, redaction, and final classification template.
- `.planning/milestones/v1.10-phases/49-cr-acceptance-classification-and-handoff/49-HANDOFF.md` lists the live evidence still blocking CR-0001.
- `CloudLivePreflightService` persists approval packages, access evidence, readiness records, audit rows, and forbidden-term checks.
- `GameServersController` already validates live preflight readiness before reviewed provisioning can reach provider mutation.
- The `/servers` WebApp workflow already exposes live preflight actions and repository-backed evidence recording.

### Established Patterns
- Planning artifacts must distinguish implementation-ready evidence from live operator/provider/runtime evidence.
- Browser and provider evidence is allowlisted: record safe labels, ids, timestamps, route/scope/outcome, counts, and references, not raw session or secret material.
- WebApp cloud workflows use generated Kiota clients through repository facades, and Razor surfaces bind hand-written safe models.
- Boundary tests enforce required evidence labels, repository-safe access, and absence of forbidden secret/session markers.

### Integration Points
- Phase 50 should update the live UAT evidence artifact and Phase 50 planning artifacts, then verify exact markers with focused scans.
- If code changes are needed, likely integration points are the existing live preflight service, `GameServersController`, `/servers` page, game-server repository facade, and cloud workflow boundary tests.
- Phase 51 is the only phase that may execute the approved provisioning path after Phase 50 produces `ready-for-live-execution`.

</code_context>

<specifics>
## Specific Ideas

Use the accepted Phase 49 handoff and `.planning/v1.7-LIVE-UAT-EVIDENCE.md` pending fields as the checklist for
Phase 50. Prefer explicit blockers over optimistic readiness whenever operator credentials, sessions, provider
configuration, budget, dry-run, policy, reconciliation, or redaction evidence is unavailable.

</specifics>

<deferred>
## Deferred Ideas

Live provisioning, cloud worker runtime execution, provider teardown, final provider-side billable count `0`, and
CR-0001 pass classification are deferred to later v1.11 phases and must not be completed in Phase 50.

</deferred>

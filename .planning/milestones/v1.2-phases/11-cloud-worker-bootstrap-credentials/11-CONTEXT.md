# Phase 11: Cloud Worker Bootstrap Credentials - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Prepare provisioned VMs to connect back as cloud workers using short-lived, scoped credentials and secret-safe bootstrap configuration.

**Requirements:** HETZ-06, WORK-05, WORK-06, WORK-07

**Success Criteria:**

1. API can create a cloud worker identity tied to one provisioned cloud session.
2. API can issue short-lived bootstrap credentials scoped to that worker identity or cloud session.
3. VM bootstrap receives only the API endpoint, worker identity, bootstrap credential, and non-secret runtime configuration required for registration.
4. Provider tokens, Steam credentials, and worker credential material are redacted from DTOs, logs, audit records, lifecycle job errors, and provider status output.

</domain>

<decisions>
## Implementation Decisions

### Agent Discretion

All implementation choices are at the agent's discretion. Discuss phase was skipped per workflow settings, so roadmap phase goal, success criteria, project constraints, and existing codebase conventions guide planning and implementation decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research.

</code_context>

<specifics>
## Specific Ideas

No additional operator-specific requirements were provided. Preserve the existing game-controller abstraction, keep credentials server-side, and continue the v1.2 cloud control-plane work without implementing cloud Arma 3 runtime operations.

</specifics>

<deferred>
## Deferred Ideas

Cloud Arma 3 install/start/update operations remain deferred beyond v1.2.

</deferred>

# Phase 34: Generated Client And Repository Gate Foundation - Context

**Gathered:** 2026-06-19
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Cloud workflow actions return consistent, typed, redacted WebApp results through generated clients and repository facades.

This phase depends on Phase 33 and addresses COST-03. It must use generated Kiota clients behind WebApp repository facades, must not add raw browser HTTP bridges, and must not hand-edit generated client files.

User-visible cloud workflow results must keep raw provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, and secret-bearing payloads out of results and UI surfaces.

</domain>

<decisions>
## Implementation Decisions

### Roadmap-Locked Constraints

- User-facing cloud planning, policy, provisioning, lifecycle, teardown, cost, and audit actions surface consistent success, policy, validation, and failure states through repository-backed WebApp code.
- Policy gate responses preserve decision identity and continuation details needed for follow-up actions without exposing raw secret-bearing payloads.
- User-visible cloud workflow views show only allowlisted fields and never display provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, or raw secret-bearing payloads.
- Generated WebApp clients may be consumed, but generated files must not be hand-edited.
- Browser-facing code must not add raw HTTP bridges for the cloud workflow.
- Live Hetzner, Steam, Arma, authenticated browser UAT, provider mutation, provisioning, and teardown evidence remain deferred by operator instruction.

### the agent's Discretion

Implementation details, repository DTO shape, adapter naming, redaction helper placement, focused tests, and the exact number of plan waves are at the agent's discretion, subject to existing codebase conventions and the roadmap boundaries above.

</decisions>

<code_context>
## Existing Code Insights

Phase 33 established the `/servers` guided entry path and repository-backed WebApp facade pattern. Phase 34 should build on those patterns rather than routing UI code directly to generated request builders or raw HTTP calls.

Codebase context will be expanded during plan-phase research.

</code_context>

<specifics>
## Specific Ideas

- Inspect existing cloud planning, policy, provisioning, lifecycle, teardown, cost, and audit API routes and generated Kiota artifacts.
- Create repository-level result types or adapters only where they reduce duplication and preserve redaction.
- Add source or unit coverage that proves sensitive fields are not displayed or returned through user-facing WebApp result projections.

</specifics>

<deferred>
## Deferred Ideas

- Manual/browser/authenticated UAT is deferred.
- Live provider, Steam, Arma runtime, provisioning, teardown, and Phase 30 evidence remain deferred.
- Payment processing, customer invoicing, checkout, taxes, multi-provider orchestration, and multi-session workers remain out of scope.

</deferred>

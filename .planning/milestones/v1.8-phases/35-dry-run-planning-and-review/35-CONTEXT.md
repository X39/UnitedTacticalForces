# Phase 35: Dry-Run Planning And Review - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Authorized users can generate and review a non-mutating Arma 3 cloud dry-run plan for a selected game server and billing owner before any provider resource is created.

This phase covers the PLAN-01, PLAN-02, and PLAN-03 requirements:

- Generate a non-mutating Arma 3 cloud dry-run plan for the selected game server and billing owner.
- Review planned resources, assumptions, firewall rules, validation failures, metadata freshness, hourly estimates, monthly estimates, and cost source labels before provider mutation.
- Block provisioning from stale, invalid, mismatched, superseded, or owner-mismatched dry-run plans.

Provider provisioning, live Hetzner resource creation, live Steam or Arma runtime operation, and policy approval workflow expansion remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Phase Boundary
- Dry-run planning must remain non-mutating with respect to cloud providers.
- Provider provisioning must not be available from stale, invalid, mismatched, superseded, or owner-mismatched plans.
- Review content must be sanitized and must preserve Phase 34 redaction guarantees for tokens, credentials, headers, worker hashes, and raw secret-bearing payloads.
- Cost estimates must clearly label source and freshness rather than implying reconciled provider billing.

### Product Flow
- Continue the v1.8 owner-facing path from `/servers`; `/servers/cloud` remains the admin/provider/fleet surface.
- Use generated Kiota clients and WebApp repository facades for WebApp integration, preserving the Phase 34 boundary against raw browser HTTP bridges.
- Prefer regenerating a plan when owner assignment, inputs, metadata freshness, or validation state makes the current reviewed plan unusable.
- Treat plan invalidity and supersession as actionable workflow states, not silent failures.

### Verification Boundary
- Automated/source verification should cover non-mutating behavior, stale-plan blocking, owner mismatch blocking, supersession, regeneration, and redaction.
- Manual browser/authenticated/live-provider/Steam/Arma UAT is deferred by operator instruction and must not be claimed as passed.
- Phase 30 live evidence remains deferred and out of scope unless a separate operator-approved live evidence milestone is started.

### the agent's Discretion
Implementation choices are otherwise at the agent's discretion, guided by ROADMAP.md, REQUIREMENTS.md, existing Phase 33 and Phase 34 workflow patterns, and current codebase conventions.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 33 established the guided `/servers` entry flow, billing owner selection, and Arma 3 planning input boundaries.
- Phase 34 established generated-client/repository integration for cloud workflow actions and redacted `CloudWorkflowActionResult` UI presentation.
- Existing WebApp server owner workflow files under `sources/WebApp/Pages/Index.*` should remain the primary UI integration point unless plan research identifies a narrower established surface.
- Existing API cloud workflow and cost visibility DTO/controller/repository patterns should be reused rather than introducing a parallel raw-HTTP path.

### Established Patterns
- API controllers project EF entities and domain state into DTOs; WebApp repositories wrap generated Kiota request builders.
- Frontend cloud workflow UX uses actionable result panels, explicit disabled states, result taxonomy, and sanitized summaries.
- Repository boundary tests should scan for forbidden raw markers and verify generated-client usage where security-sensitive workflow data is exposed.

### Integration Points
- `/servers` owner workflow for plan generation, review, stale-state handling, and regeneration.
- API cloud workflow endpoints and generated Kiota client surface for dry-run plan actions.
- Existing cost metadata and policy visibility projections for source labels, freshness, and gating context.
- Phase 34 continuation/result infrastructure for rendering actionable next steps without exposing secrets.

</code_context>

<specifics>
## Specific Ideas

- Show plan review content as concrete planned resources, assumptions, firewall rules, validation failures, metadata freshness, and hourly/monthly estimates.
- Surface stale, invalid, mismatched, superseded, and owner-mismatched plans with clear regeneration or correction actions.
- Keep provider mutation unavailable until a future phase explicitly adds provisioning from a valid, fresh, owner-matched plan.
- Preserve compact operational UI density consistent with the existing Fluent UI server workflow rather than introducing a marketing-style redesign.

</specifics>

<deferred>
## Deferred Ideas

- Live Hetzner provisioning, provider-console verification, final provider billable resource verification, SteamCMD/DepotDownloader validation, and native Arma runtime UAT.
- Policy approval UX expansion beyond whatever is required to show that invalid or blocked plans cannot be provisioned.
- Payment processing, external invoicing, taxes, checkout, multi-provider orchestration, multi-session workers, resize, snapshots, and backups.
- Broad redesign of server administration pages outside the dry-run planning and review workflow.

</deferred>

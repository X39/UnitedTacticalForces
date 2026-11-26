# Phase 48: Provider Teardown, Cleanup, And Redaction - Context

**Gathered:** 2026-06-22
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Operator can prove that provider-side billable resources are deleted or safely classified, and all evidence is redacted before any pass classification.

The selected `/servers` workflow is the primary operator surface for provider teardown. Phase 48 must make provider teardown action evidence, cleanup fallback state, provider-side resource counts, final billable count, and redaction review visible as safe evidence. It must not classify Phase 30 or CR acceptance as passed unless provider-side final billable resource count is `0` and redaction review is complete.

</domain>

<decisions>
## Implementation Decisions

### The agent's discretion

All implementation choices are at the agent's discretion because discuss was skipped. Use ROADMAP phase goal, success criteria, requirements, Phase 46/47 verification, existing `/servers` provider teardown panel, and codebase conventions to guide decisions.

### Safety boundary

Phase 46 and Phase 47 classified live provisioning and runtime evidence as `human_needed`. Phase 48 may harden UI evidence and automated checks, but actual provider teardown, provider-side inspection, manual cleanup fallback, and final billable-resource closure remain `human_needed` or blocked until an operator supplies live provider evidence.

</decisions>

<code_context>
## Existing Code Insights

- `sources/WebApp/Pages/Servers/Index.razor` already renders a selected-server `Provider teardown` panel when a cloud session id is available.
- Provider teardown actions already route through `ConfirmTeardownProviderResourcesAsync`, `ExecuteCloudWorkflowActionAsync`, and `IGameServerRepository.TeardownCloudSessionResultAsync`.
- Existing helper methods expose cleanup risk, remaining billable resources, unknown provider resources, orphan risk, retry state, audit records, reconciliation state, and redaction-blocked workflow state.
- Phase 46 added a stable provisioning evidence checklist. Phase 47 added a stable runtime evidence checklist. Phase 48 should follow the same compact evidence-row pattern in the teardown panel.

</code_context>

<specifics>
## Specific Ideas

- Add a compact teardown evidence checklist inside the existing selected-server `Provider teardown` panel.
- Keep labels stable for provider teardown action, cleanup state, manual fallback, provider-side counts, servers, floating IPs, volumes, firewalls, final billable count, resource inventory, retry/fallback, audit records, and redaction review.
- Reuse existing status badges, provider teardown helpers, workflow result summaries, cost visibility, reconciliation rows, and cloud inventory projections.
- Create a Phase 48 verification artifact that classifies automated UI/source evidence separately from live teardown and provider-count evidence.

</specifics>

<deferred>
## Deferred Ideas

Phase 30 pass/fail classification and final CR-0001 acceptance mapping stay in Phase 49.

</deferred>

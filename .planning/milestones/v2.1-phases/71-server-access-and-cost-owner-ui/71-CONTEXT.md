# Phase 71: Server Access and Cost Owner UI - Context

**Gathered:** 2026-11-26
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Server owners and admins can manage who may see and operate a server and who pays for it, directly in the React server workspace: list the per-server access grants (`GET /game-servers/{id}/access`), grant and revoke per-server claims such as `sv:view` through the existing grant and revoke endpoints, and transfer the cost owner (`PUT /game-servers/{id}/cost-owner`) with a clear confirmation. Claim-gated, localized en/de, following the Phase 70 UI conventions (Settings panels, drawn controls, ConfirmDialog). Source: v2.1 milestone audit integration finding 2 (2026-11-26): these endpoints had no React consumer and were absent from the Phase 61 parity matrix.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions.

Standing user preferences that apply (from project memory):
- No native `<details>` elements, no ugly native selects; long lists of people or entries need filter/search.
- No layout shift when values are staged; dialogs aligned and compact.
- The API is not a frozen contract: if an endpoint needs a field or a change for a good UI, change it in the owning layer and regenerate the OpenAPI snapshot and the generated client.
- Never touch the legacy Blazor WebApp unless an API change would break it.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Known starting points: `GameServersController.cs` (access, grant, revoke, cost-owner, actions endpoints), `src/features/hosting/services.ts`, `SettingsSection.tsx` and `SettingsPanel` (Phase 70), the Phase 61 parity matrix.

</code_context>

<specifics>
## Specific Ideas

No specific requirements — discuss phase skipped. Refer to ROADMAP phase description and success criteria.

</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.

</deferred>

# Phase 44: Template/Profile WebApp Experience And Verification - Context

**Gathered:** 2026-06-22
**Status:** Ready for execution
**Mode:** Autonomous

<domain>
## Phase Boundary

Admins and authorized server creators must manage and consume cloud server templates, Arma 3 runtime profiles, and mission references through the Blazor WebApp.

Implementation boundaries from ROADMAP:

- Keep `/servers` as the server-owner workflow and `/servers/cloud` as the admin provider/fleet console.
- Use generated Kiota clients and repository facades; do not introduce raw browser HTTP bridges.
- Razor components bind hand-written safe models, not generated transport DTOs.
- Do not claim authenticated browser, live provider, Steam, Arma runtime, teardown, cleanup, or Phase 30 evidence.

Success criteria:

1. Admin WebApp surfaces support template/profile listing, detail, create/edit/disable, and validation feedback through repository models.
2. `/servers` cloud creation displays active template/profile choices, applies defaults, shows constraints, and carries selections into dry-run review.
3. Razor components bind hand-written safe models, not generated transport DTOs or raw HTTP responses.
4. UI and API verification cover authorization-sensitive views, stale/disabled template states, redaction, and regression safety for existing creation workflows.
5. Milestone evidence states that live UAT remains deferred until CR-0001 implementation is otherwise complete.
</domain>

<code_context>
## Existing Code Insights

- `sources/WebApp/UtfApi/**` is Kiota-generated and currently lacks `CloudServerTemplate`, `Arma3RuntimeProfile`, `Arma3MissionReference`, and `CloudSelectionContext` generated models/routes.
- `sources/WebApp/Services/CloudAdminRepository` already owns admin cloud provider, inventory, cost, policy, and reconciliation facade models and mappings.
- `sources/WebApp/Services/GameServerRepository` owns server-owner workflows, guided cloud entry, dry-run review, policy gates, provisioning, runtime actions, teardown, live preflight, and result mapping.
- `/servers/cloud` is the admin provider/fleet console; `/servers` is the user-facing server creation and operation workflow.
- Existing WebApp boundary tests reject raw HTTP usage, generated DTO binding, forbidden secret fields, and live-UAT pass claims in owner workflow surfaces.
- Phase 41 added cloud template APIs and safe DTOs. Phase 42 added runtime profile and mission reference APIs and safe DTOs. Phase 43 added template/runtime selection to dry-run/review/provisioning/cost/policy/reconciliation context.
</code_context>

<decisions>
## Implementation Decisions

- Phase 44 should refresh the generated WebApp client first, then add hand-written repository models that intentionally hide raw DTOs and secret-bearing fields.
- Admin management can be split from `/servers` creation UX: first repository/generated model foundation, then admin management UI, then user creation/review integration.
- Existing boundary-test style should be extended for template/profile models and UI files before broad manual browser checks.
- Any authenticated browser or live provider verification remains documented as deferred evidence, not a pass/fail claim.
</decisions>

<deferred>
## Deferred Items

- Live provider/browser/runtime evidence remains deferred until CR-0001 implementation is otherwise complete.
- Template marketplace, multi-session cloud workers, backups, snapshots, and resize flows remain out of scope.
</deferred>

# Phase 58: Unified Server Workspace - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 58 consolidates selected-server operation into one workspace. Users must immediately understand core server status and run relevant server-scoped actions without jumping between scattered cloud or admin pages. The workspace summarizes identity, selected game, hosting mode, owner, runtime state, latest work, worker/provider state, cost/policy status, cleanup risk, logs, and audit context while preserving authorization, redaction, and provider teardown as the cost-stop boundary.

</domain>

<decisions>
## Implementation Decisions

### Summary And Status Hierarchy
- **D-01:** Overview must combine core status and core actions. It should not be just a read-only status page, and it should not hide important state behind tabs.
- **D-02:** Treat every server as session-timed in the UI and technical model. Do not create separate short-lived versus forever server modes.
- **D-03:** Overview should make server age, current runtime/job, active owner, cost exposure, cleanup risk, and provider teardown state first-class signals when relevant.
- **D-04:** The expected standard lifespan of a server is about 3 hours, with only a few living forever. This should shape urgency and visibility, but not create a separate UI or persisted category.

### Workspace Action Placement
- **D-05:** Overview should use one primary next action based on state plus a compact secondary action row. This supports the session model without duplicating every tab.
- **D-06:** Administrative or detailed actions stay on dedicated tabs by default: owner transfer, file browser/upload/delete, full audit history, full job table, and detailed diagnostics.
- **D-07:** Cloud setup and provisioning actions are state-dependent workspace actions. Show actions such as `Continue cloud setup`, `Review cloud setup`, `Provision`, or `Refresh status` on Overview only when they are the relevant next step for this server; keep the Cloud tab as the detailed flow.
- **D-08:** Logs need a dedicated Logs tab. Remove the user-facing `Collect logs` action entirely. Log collection should be API/internal plumbing that makes logs accessible; clients should not need to download logs before reading them. The Logs tab should show accessible logs and provide the only download option.

### Cleanup, Teardown, And Cost Stop
- **D-09:** Provider teardown belongs in the Cloud tab and may be promoted to Overview only when it is the relevant next action because of active provider cost or cleanup risk.
- **D-10:** Use direct repeated wording: provider teardown is the cost-stop action, and runtime cleanup does not stop provider cost.
- **D-11:** When runtime cleanup and provider teardown are both possible and provider resources are still billable, make provider teardown the primary next action because it stops provider cost. Runtime cleanup remains secondary or contextual.
- **D-12:** After provider teardown succeeds, show a cost-stopped state with remaining billable count or cleanup failure follow-up only if needed. Do not rely on a generic success banner.

### Owner And Admin Boundary
- **D-13:** Server owners see only server-scoped provider summaries: billable resource count, cleanup risk, provider state, and safe next actions for this server. They do not see broad provider inventory.
- **D-14:** Administrators should have an admin-only diagnostics panel inside the selected-server workspace rather than needing to leave the workspace for provider/fleet detail.
- **D-15:** Admin diagnostics should be collapsed or visually secondary by default. They must be available to admins without competing with normal server operation.
- **D-16:** Non-admin owners should see owner-safe summaries and actions. Do not mention hidden admin inventory unless a blocked action needs administrator help.

### the agent's Discretion
- The agent may choose the exact Overview layout, state-ranking rules, and primary-next-action algorithm as long as core status and core action are immediately visible.
- The agent may choose exact tab names and component splits, but logs should move to a dedicated Logs tab and detailed/admin actions should stay off Overview by default.
- The agent may choose exact wording, but provider teardown must be explicitly described as the cost-stop action wherever it appears, and runtime cleanup must not imply provider billing has stopped.
- The agent may choose how to reuse or refactor the existing large `Details.razor`, provided Razor remains repository-backed and safe models stay behind the WebApp repository boundary.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 58 goal, boundaries, success criteria, and planned split.
- `.planning/REQUIREMENTS.md` - WORK-01 through WORK-04, OPS follow-on context, and v1.12 no-live-UAT/out-of-scope constraints.
- `.planning/PROJECT.md` - current milestone direction, generic server UX goals, provider cost-stop semantics, and security boundaries.
- `.planning/STATE.md` - current project state, Phase 58 readiness, and pending warnings.
- `.planning/phases/55-generic-game-hosting-capability-contracts/55-CONTEXT.md` - generic public contract decisions, hosting modes, and no Arma-specific public shims.
- `.planning/phases/56-generic-runtime-template-and-plan-snapshots/56-CONTEXT.md` - generic selector, dry-run review, stale-state, worker-recipe, and task-oriented review decisions.
- `.planning/phases/57-guided-server-creation-journey/57-CONTEXT.md` - creation flow decisions, workspace handoff, cloud continuation, and task-oriented labels.

### Codebase Maps
- `.planning/codebase/CONVENTIONS.md` - C#, Razor, repository, generated-client, and file organization conventions.
- `.planning/codebase/STRUCTURE.md` - active source locations, WebApp page/component/service layout, and generated-code boundaries.
- `.planning/codebase/STACK.md` - Blazor WebAssembly, Fluent UI, Kiota, ASP.NET Core, and .NET 9 stack context.

### Current Server Workspace And Operations
- `sources/WebApp/Pages/Servers/Details.razor` - current selected-server workspace with Overview, Runtime, Cloud, Configuration, Files, Jobs, Ownership, Audit, Danger, cloud setup, log, cleanup, and teardown surfaces.
- `sources/WebApp/Pages/Servers/Advanced.razor` - existing advanced operations surface referenced by Phase 57 cloud continuation and still relevant for cloud setup reuse or consolidation.
- `sources/WebApp/Pages/Servers/Index.razor` - server fleet entry point and selected-server navigation.
- `sources/WebApp/Pages/Servers/Create.razor` - Phase 57 creation wizard handoff into the selected-server workspace.

### Repository And API Boundaries
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - WebApp repository surface for server details, lifecycle, cloud workflow, ownership, logs, files, audit, cost, and policy.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - generated-client and repository mapping implementation for workspace operations.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - action result, lifecycle job, runtime, cost, policy, teardown, and audit summary mapping.
- `sources/Api/Controllers/GameServersController.cs` - server-scoped API routes for details, lifecycle jobs, cloud status, runtime status, cost/policy visibility, provider teardown, billing owners, audit, logs, configuration, files, and delete.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `sources/WebApp/Pages/Servers/Details.razor` already provides the selected-server workspace shell, route sections, summary strip, runtime/cloud/jobs/ownership/audit/danger sections, cloud continuation, and action wiring. Phase 58 should reshape and split this rather than inventing a second workspace.
- `UtfCommandPageHeader`, `UtfCommandButton`, `UtfStatusBadge`, `UtfAlert`, and existing server CSS classes provide local UI patterns for a command-center style workspace.
- `IGameServerRepository` already exposes most server-scoped data and operations needed for the workspace: server details, jobs, owner/history, cloud runtime, cost, policy, audit, logs, lifecycle actions, cleanup, teardown, configuration, and files.
- `CloudWorkflowResultMapper` already produces user-facing action state, required next step, cost, policy, teardown, and audit summaries that can feed the primary next action and state-driven Overview.

### Established Patterns
- Razor should bind hand-written repository models and call generated Kiota clients only through repository implementations.
- Server-scoped pages should not require the normal user to navigate to admin-only cloud inventory pages.
- Provider teardown/deletion is the cost-stop boundary; runtime stop and runtime cleanup are not cost-stop actions.
- Secret-bearing values, provider tokens, Steam credentials, worker credentials, bearer headers, cookies, and raw payload dumps must not appear in frontend models, logs, audit details, or planning artifacts.
- The current workspace still contains user-facing implementation wording such as Arma/dry-run planning labels and a user-facing collect logs action. Phase 58 should repair these where they are part of the selected-server workspace.

### Integration Points
- Rework Overview into a session-oriented status plus state-driven action surface.
- Add or extract a dedicated Logs tab; remove `Collect logs` from user-facing action placement while preserving API/internal log collection mechanics as needed.
- Move owner transfer, file operations, full audit history, full job table, and deep diagnostics off Overview into dedicated tabs or collapsed admin-only panels.
- Promote provider teardown to Overview only when it is the cost-stop next action for active billable resources or cleanup risk.
- Add an admin-only collapsed diagnostics panel inside the workspace for provider/fleet detail, while keeping owner-visible provider data server-scoped and summarized.

</code_context>

<specifics>
## Specific Ideas

- The normal server lifespan is about 3 hours, though a few servers may live forever. The product should not model or present these as different server classes.
- The workspace should feel like the active session control surface: what is this server, what is happening now, what is costing money, and what should I do next?
- Users should read logs directly in the UI. Download is optional and belongs only on the Logs tab.
- Unpackaged sketches were detected under `.planning/sketches/MANIFEST.md`, but no packaged sketch-findings skill was available. Downstream agents should not treat sketch findings as canonical unless wrapped up separately.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 58-Unified Server Workspace*
*Context gathered: 2026-07-05*

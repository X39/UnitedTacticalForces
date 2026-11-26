# Phase 57: Guided Server Creation Journey - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 57 rebuilds server creation as one guided, game-first user journey. Users choose a supported game, choose a hosting mode, provide ownership and first-action setup inputs, confirm the wizard, and only then create a durable server record. The created server opens in the server workspace, where follow-up setup actions can continue. The phase does not create live provider resources without the existing review, policy, approval, and provisioning gates, and it must not make normal server creation depend on admin-only cloud pages as a discovery path.

</domain>

<decisions>
## Implementation Decisions

### Journey Shape
- **D-01:** Build server creation as a step-by-step wizard rather than a single progressive page or mode-first tabs.
- **D-02:** The wizard must not save a durable server record, setup draft, dry-run plan, review record, lifecycle job, or other persistent setup state during intermediate wizard steps. Partial saved state would confuse users.
- **D-03:** The final wizard submit creates the server and opens its workspace. Further setup wizards or task prompts may be offered from the workspace.
- **D-04:** The initial wizard should collect enough information for the first runnable action, while saving nothing until final submit. Record-only creation needs enough to create the record; local server creation needs enough to queue or prepare the first local setup action; cloud server creation needs enough to continue into cloud review/setup without provider mutation.

### Hosting-Mode Language
- **D-05:** Use `Server record`, `Local server`, and `Cloud server` as the user-facing hosting mode labels.
- **D-06:** Mode descriptions should be short task-focused one-liners. Do not lead with lifecycle jobs, dry-run plans, policy gates, workers, provider resources, or other implementation nouns.
- **D-07:** Internal terms such as dry-run plan, policy gate, billing assignment, and lifecycle job may appear only in details, diagnostics, or exact error context. The normal wizard path should stay task-worded.
- **D-08:** Keep the field label `Billing owner` in the wizard.

### Cloud Continuation
- **D-09:** After a Cloud server is created, cloud setup continues through the existing advanced operations page.
- **D-10:** The server workspace should make the continuation discoverable through a prominent action, such as `Continue cloud setup`, that deep-links to advanced operations with the new server and wizard choices preselected.
- **D-11:** Wizard choices carried into advanced operations are navigation defaults only. Preselect server, template, runtime/profile, mod pack, and runtime basics where available, but do not persist extra setup draft state and do not create dry-run or review records until the user explicitly starts that action.
- **D-12:** The next cloud action should be labeled `Review cloud setup`. Technical dry-run wording remains available only in detail or error context.
- **D-13:** Policy warning, approval-required, blocked, expired, or stale policy states should appear inline in the cloud setup flow with a concrete next action such as request approval, acknowledge warning, or refresh policy.

### Blocker Feedback
- **D-14:** Missing required choices should be shown inline on the relevant step and summarized on the review step before final submit.
- **D-15:** Unavailable options such as no active billing owner, no compatible cloud template, no runtime profile, or missing provider metadata should be visible as disabled states with fix actions. Do not silently hide them.
- **D-16:** Stale cloud setup or review state should explain what changed and offer `Review cloud setup again`, rather than silently refreshing or exposing stale-plan wording as the primary message.
- **D-17:** Failed or blocked final submit should keep the user in the wizard, preserve entered choices, and point to exact fix targets. If final submit fails before server creation, do not navigate away.

### the agent's Discretion
- The agent may choose the exact wizard component structure, step names, route/query parameter names, and deep-link mechanics, provided intermediate wizard steps remain non-persistent and the final submit opens the server workspace.
- The agent may choose the exact workspace placement for the `Continue cloud setup` action, but it must be prominent enough that users do not need to discover admin cloud pages manually.
- The agent may choose exact validation grouping and copy, provided blockers are field-specific, task-oriented, and include fix actions where possible.
- The agent may choose how much existing advanced operations code to reuse or refactor in Phase 57, as long as user-facing labels and navigation follow the decisions above and generic public contracts remain intact.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 57 goal, implementation boundaries, success criteria, and planned split.
- `.planning/REQUIREMENTS.md` - CREATE-01 through CREATE-04 and v1.12 no-live-UAT/out-of-scope constraints.
- `.planning/PROJECT.md` - current milestone direction, generic server UX goals, security boundaries, and parked live evidence context.
- `.planning/STATE.md` - current project state, Phase 57 readiness, and pending warnings.
- `.planning/phases/55-generic-game-hosting-capability-contracts/55-CONTEXT.md` - generic public contract decisions, hosting modes, and no Arma-specific public shims.
- `.planning/phases/56-generic-runtime-template-and-plan-snapshots/56-CONTEXT.md` - generic selector, dry-run review, stale-state, and task-oriented review decisions.

### Codebase Maps
- `.planning/codebase/CONVENTIONS.md` - C#, Razor, repository, generated-client, and file organization conventions.
- `.planning/codebase/STRUCTURE.md` - active source locations, WebApp page/component/service layout, and generated-code boundaries.
- `.planning/codebase/STACK.md` - Blazor WebAssembly, Fluent UI, Kiota, ASP.NET Core, and .NET 9 stack context.

### Current Server Creation And Workspace UI
- `sources/WebApp/Pages/Servers/Create.razor` - current create page with record/local/cloud modes, Arma3Cloud enum, cloud selections, final navigation, and validation logic to replace.
- `sources/WebApp/Pages/Servers/Index.razor` - fleet entry point and create/workspace navigation.
- `sources/WebApp/Pages/Servers/Details.razor` - server workspace that should receive users after final wizard submit and expose follow-up setup actions.
- `sources/WebApp/Pages/Servers/Advanced.razor` - existing advanced operations and cloud setup surface to reuse for cloud continuation.

### WebApp Repository Boundary
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - repository surface for creation, guided cloud entry, dry-run/review/policy, lifecycle, and cloud operations.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationOptionsInfo.cs` - current creation options projection.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationControllerInfo.cs` - supported hosting-mode information used by creation UI.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationBillingOwnerInfo.cs` - active billing owner options for creation.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationInfo.cs` - current create payload facade.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationResultInfo.cs` - current create result facade and lifecycle-job fields.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `sources/WebApp/Pages/Servers/Create.razor` already provides a command-center create page, creation-mode toolbar, local/record creation, cloud inputs, validation messages, and create/navigation methods.
- `sources/WebApp/Pages/Servers/Details.razor` already provides a server workspace shell with tabs, summary, runtime/cloud/jobs/ownership/audit sections, and route support for server-scoped follow-up actions.
- `sources/WebApp/Pages/Servers/Advanced.razor` already contains guided cloud setup, dry-run review, policy gate, provisioning, and workflow result panels. Phase 57 can reuse or deep-link into this surface rather than inventing a second cloud operations implementation.
- `IGameServerRepository` and the `GameServerCreation*Info` records are the right client-side boundary for Razor-facing creation models. Generated Kiota models should remain behind repository facades.

### Established Patterns
- WebApp pages consume hand-written repository models and call generated clients only through repository implementations.
- Fluent UI and existing `UtfCommand*`, `UtfStatusBadge`, `UtfAlert`, and command-center page patterns should be reused for wizard controls and task feedback.
- Server-scoped API/WebApp contracts are generic after Phases 55 and 56. New creation UI should not branch on Arma 3 in public repository or Razor-facing names.
- Provider teardown remains the cost-stop boundary; runtime setup, review, and provisioning language must not imply that non-provider actions stop provider cost.
- Secret-bearing values, raw provider payloads, Steam credentials, worker credentials, bearer headers, cookies, and raw setup payload dumps must not appear in frontend models, logs, audit details, or planning artifacts.

### Integration Points
- Replace the current mode-first create page with a step-by-step game-first wizard under the normal create entry point.
- Remove or hide user-facing `Arma3Cloud` assumptions from creation labels and active Razor flow; cloud setup should be generic and driven by supported game capability/hosting mode.
- Final submit should call the appropriate repository create path only once, then navigate to the server workspace.
- Workspace continuation should deep-link to advanced operations with non-persistent defaults for server, template, runtime/profile, mod pack, and runtime basics where supported.
- Validation should cover empty options, unsupported hosting mode, missing owner, missing metadata, stale setup/review state, policy gate states, and successful creation paths.

</code_context>

<specifics>
## Specific Ideas

- Nothing saves while the user is stepping through the initial wizard. This includes server records and setup drafts.
- The user's selected final-submit behavior is: create the server, open the workspace, and offer further setup wizards or task actions from there.
- Cloud continuation intentionally reuses the existing advanced operations page, but discovery should happen from the workspace through a prominent action rather than manual admin-page navigation.
- User-facing cloud labels should use `Review cloud setup` instead of `dry-run plan`.
- Unpackaged sketches were detected under `.planning/sketches/MANIFEST.md`, but no packaged sketch-findings skill was available. Downstream agents should not treat sketch findings as canonical unless they are wrapped up separately.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 57-Guided Server Creation Journey*
*Context gathered: 2026-07-05*

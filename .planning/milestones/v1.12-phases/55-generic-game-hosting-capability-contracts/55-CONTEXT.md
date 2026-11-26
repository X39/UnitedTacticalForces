# Phase 55: Generic Game Hosting Capability Contracts - Context

**Gathered:** 2026-07-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 55 defines the generic supported-game hosting capability contracts across API and WebApp. It replaces Arma-3-named creation, cloud entry, and planning surfaces with generic contracts, generated clients, repository methods, and regression guards. Arma 3 remains the first supported game capability implementation, but no legacy Arma-specific public API, WebApp repository, DTO, or persisted contract shape is preserved for compatibility.

</domain>

<decisions>
## Implementation Decisions

### Capability Metadata Shape
- **D-01:** Use a layered supported-game capability contract: stable core capability metadata plus extension sections such as cloud, runtime, and mods. Arma 3 may be the only implementation filling extension data initially.
- **D-02:** Represent hosting modes explicitly as enum-like modes: `RecordOnly`, `LocalManaged`, and `CloudManaged`.
- **D-03:** Model lifecycle operations as server-specific job actions behind a generic interface. The generic API should not assume every game has the same fixed action set.
- **D-04:** Action descriptors are intentionally sparse: stable identifier, display label, and risk marker only. Do not model action availability; unsupported actions are omitted from the capability and therefore cannot be shown by the UI.

### Generic API And WebApp Contracts
- **D-05:** Add real generic API routes in Phase 55 and regenerate the Kiota API client for the new surface. Do not support new generic routes through handwritten client extensions.
- **D-06:** Remove old Arma-specific API routes instead of keeping compatibility shims. The product is not deployed, so there is no backward-compatibility requirement.
- **D-07:** Remove Arma-specific methods from the active public WebApp repository interface and replace them with generic creation, cloud-entry, and planning methods now.
- **D-08:** Server-scoped cloud planning routes should be generic, for example `/game-servers/{id}/cloud-dry-run-plans`. The API derives the game type from the server record/capability; the client must not provide game type for server-scoped actions.

### Clean-Cut Arma Boundary
- **D-09:** No legacy Arma compatibility is required. Arma-specific data can be removed or transformed into generic variants because there is no deployed migration cost.
- **D-10:** Arma 3 becomes the first generic capability implementation. Externally visible contracts, routes, repository methods, and active data shapes must be generic.
- **D-11:** Phase 55 may remove or transform Arma-specific persisted data where needed for generic capability contracts.
- **D-12:** If plans encounter Arma-specific runtime/profile/template types while working on Phase 55, transform them aggressively where touched even if the change reaches into Phase 56 territory.

### Regression Guards
- **D-13:** Use a strict active-source ban for old Arma-specific public names, with explicit exceptions only for genuine game implementation internals such as `Arma3GameServerController` and generic capability metadata values.
- **D-14:** Tests should verify regenerated Kiota request builders/models exist for the new generic API surface and that WebApp repositories use those generated builders.
- **D-15:** Tests should scan active source to reject old Arma-only public routes, repository methods, DTOs, persisted contract names, and handwritten client-route extensions for the new generic surface.
- **D-16:** Generic API/WebApp contracts must not branch on the Arma 3 game identifier. Arma-specific branching belongs only in server-side game capability implementations or adapters.

### the agent's Discretion
- The agent selected strict active-source regression guards with narrow implementation-internal exceptions after the user said "You decide".
- The agent selected both generated-client presence checks and source scans after the user said "You decide".

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 55 goal, boundaries, success criteria, and planned split.
- `.planning/REQUIREMENTS.md` - v1.12 requirements, especially GEN-01 and GEN-02.
- `.planning/PROJECT.md` - milestone constraints, clean generic-hosting direction, security and no-live-UAT boundaries.
- `.planning/STATE.md` - current milestone state, pending todos, and parked live-UAT constraints.

### Codebase Maps
- `.planning/codebase/ARCHITECTURE.md` - API, WebApp repository, generated Kiota, and game-controller architecture.
- `.planning/codebase/STRUCTURE.md` - active source/test locations and generated-code boundaries.
- `.planning/codebase/TESTING.md` - existing test projects and test style.

### Existing API And Controller Contracts
- `sources/Api/Controllers/GameServersController.cs` - current creation, cloud-entry, and Arma-specific planning routes to replace.
- `sources/Api/DTO/GameServerCreationControllerOptionDto.cs` - current creation option contract with Arma-specific cloud flag.
- `sources/Api/Services/GameServerController/IGameServerController.cs` - current generic runtime operation abstraction.
- `sources/Api/Services/GameServerController/IGameServerControllerCreatable.cs` - controller identifier and creation discovery contract.
- `sources/Api/Services/GameServerController/GameServerControllerFactory.cs` - controller discovery currently exposing identifiers only.

### Existing WebApp Repository Contracts
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - current public repository surface with Arma-specific creation/planning methods.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - current generated-client and custom-route usage to replace with generated generic calls.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationControllerInfo.cs` - current WebApp creation model with Arma-specific cloud flag.
- `sources/WebApp/Services/GameServerRepository/GameServerCreationOptionsInfo.cs` - current WebApp creation options model.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPlanningSelectionModels.cs` - current planning selector models with Arma-specific runtime and mission shapes.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IGameServerControllerCreatable` and `GameServerControllerFactory`: existing controller discovery can be expanded from identifier-only discovery into supported-game capability projection.
- `IGameServerController`: existing runtime operation abstraction is the implementation-side anchor for game-specific action descriptors.
- `IGameServerRepository`: existing WebApp repository facade remains the correct boundary for generated Kiota calls and Razor-facing safe models.
- `Api.Tests` and `Ui.Tests`: active test projects can host API contract checks, source scans, generated-client checks, and repository/UI boundary regressions.

### Established Patterns
- API controllers project DTOs instead of returning EF entities.
- Generated Kiota files under `sources/WebApp/UtfApi` are not hand-edited; new API routes must be regenerated into the client.
- WebApp Razor should consume hand-written repository models, not generated DTOs directly.
- Server-scoped operations already route through durable lifecycle jobs and billing owner attribution; generic action descriptors should preserve that model.

### Integration Points
- Replace `GameServersController` Arma-named creation/cloud-entry/planning routes with generic route names.
- Replace `GameServerCreationControllerOptionDto.SupportsArma3GuidedCloud` with layered generic capability data.
- Replace public WebApp repository methods such as `CreateArma3GuidedCloudPlanningServerAsync`, `CreateArma3CloudDryRunPlanResultAsync`, and `ReviewArma3CloudDryRunPlanAsync` with generic equivalents.
- Update tests that currently assert `cloud-entry/arma3-server` and `cloud-dry-run-plans/arma3` to assert generic routes and clean-cut source boundaries instead.

</code_context>

<specifics>
## Specific Ideas

- Keep the client "rather stupid" for server-scoped planning: it calls a generic server route, and the API derives game behavior from the server's selected capability.
- A clean cut is preferred over migration compatibility because the product is not deployed.
- Action risk belongs in the descriptor; availability does not, because omitted actions are unavailable by definition.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 55-Generic Game Hosting Capability Contracts*
*Context gathered: 2026-07-03*

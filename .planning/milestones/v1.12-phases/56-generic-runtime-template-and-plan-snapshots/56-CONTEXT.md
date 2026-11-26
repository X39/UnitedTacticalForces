# Phase 56: Generic Runtime, Template, And Plan Snapshots - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 56 generalizes cloud templates, runtime profiles, mission/runtime references, dry-run inputs, review/policy models, persisted snapshots, and worker-facing payloads so they are selected and validated by game type instead of hard-coded Arma 3 contracts. Arma 3 remains the first concrete game implementation, but active public API/WebApp contracts, selectors, dry-run review models, and worker envelopes should be generic. Live provider mutation, new game products, multi-provider work, and the later guided creation/workspace UI rebuild remain out of scope.

</domain>

<decisions>
## Implementation Decisions

### Selector Shape
- **D-01:** Use one unified game-scoped selector response with typed option groups for templates, runtime profiles, missions, and future game-specific choices.
- **D-02:** Selector option DTOs should have stable generic fields such as id, label, version, enabled/current state, game type, summary, and validation state. Arma-specific details may appear only in safe typed detail sections where needed.
- **D-03:** Normal creation selectors return only current enabled options by default. Admin, review repair, stale-plan repair, and audit paths may explicitly request superseded or disabled versions.
- **D-04:** Game-type filtering is enforced server-side. The API derives selected game behavior from the server/capability and returns only compatible selector groups; the WebApp should not filter broad unscoped lists.

### Snapshot Compatibility And Schema Strategy
- **D-05:** There is no deployed production database and no historical data to preserve. Local test databases and containers can be deleted, so Phase 56 may aggressively rename, replace, or rebuild Arma-named runtime/profile/mission/schema shapes around the generic model.
- **D-06:** Migrations may be deleted and recreated if that materially produces a cleaner generic model, but migrations must be created through EF tooling first and only modified afterwards when necessary.
- **D-07:** Preserve behavior and safety coverage through fresh migrations and tests, not through compatibility with old local test rows.
- **D-08:** Worker-facing runtime snapshots should use a generic envelope with a game-specific body/recipe. Workers need game-specific execution behavior, but the worker protocol should identify the selected game/runtime safely and generically.
- **D-09:** Prefer an API-prepared execution recipe plus worker executor model. The API should resolve selectors, policy, templates, stale state, cost, and plan context; the worker should execute a bounded allowlisted recipe with small reusable primitives and should not infer plan state.
- **D-10:** Do not require Phase 56 to invent a fully generic worker pipeline unless it naturally falls out of removing Arma-specific payload coupling. The current worker has a generic claim loop but an Arma 3 executor; that is acceptable if the boundary becomes explicit and extensible.

### Dry-Run Review Model
- **D-11:** Dry-run review and policy review should become fully generic for the user-facing path: generic routes/request/response DTOs, generated clients, WebApp repository models, and UI-facing mapper names.
- **D-12:** Generic dry-run orchestration owns owner, selected game, hosting mode, template/runtime selections, input summaries, freshness, stale-plan classification, policy gates, and redaction boundaries.
- **D-13:** Provider-specific resource validation remains in provider adapters/services. Hetzner-specific server type, location, image, firewall, pricing, and metadata checks should not be flattened into a fake all-provider validator.
- **D-14:** Remove or rename public Arma-named dry-run DTOs, repository models, mapper methods, generated-client expectations, and review/policy route assumptions. Keep Arma names only inside implementation-internal game recipe/adapters where they describe real Arma execution.
- **D-15:** User-facing dry-run review should default to a decision-ready summary: selected game, hosting mode, template, runtime profile, mission/runtime details, cost, policy, metadata freshness, provider implications, and stale/superseded blockers in generic labels.
- **D-16:** Debug-style details should still be available for testing and operator inspection. Normal users work from summaries, but feature verification needs enough detail to inspect snapshots and gates.

### Stale And Superseded Plan Rules
- **D-17:** Treat dry-run as a provider mutation and billing/policy gate, not a runtime correctness guarantee. It cannot prove that an Arma instance, mission, or mod pack will stay under disk limits after future content changes.
- **D-18:** Split stale behavior by what changed. Provider, cost, owner, policy, template, storage, exposed port, SSH/admin CIDR, provider metadata, selected game, or hosting-mode changes hard-block provisioning until regeneration.
- **D-19:** Runtime recipe/content changes such as mission version, mod pack revision, config preset, launch recipe details, or other non-provider-impact runtime data require runtime re-review/acknowledgement, but should not require a full provider dry-run regeneration unless they affect provider assumptions.
- **D-20:** Provider-affecting runtime fields are explicit: storage bounds/size, exposed ports, SSH/admin CIDR, provider template, server type/location/image, billing owner, policy/cost metadata, selected game, and hosting mode. Do not treat every mission/mod/profile content edit as provider-affecting.
- **D-21:** Changed runtime content with unchanged provider assumptions should require review/acknowledgement before provisioning or running, and should not be silently auto-updated.
- **D-22:** The review model should expose explicit state labels such as provider plan stale, runtime review required, valid provider plan, blocked policy, and current recipe, each with clear next actions.
- **D-23:** Existing behavior that supersedes all older valid plans when a new plan is generated should be revisited under the split model. Supersession should distinguish provider-plan replacement from runtime recipe re-review.

### the agent's Discretion
- The agent may choose exact class/entity names and adapter boundaries as long as active public API/WebApp contracts are generic and Arma-specific names remain implementation-internal only.
- The agent may decide whether to replace old migrations wholesale or use a minimal generated migration, but any migration must originate from EF tooling.
- The agent may define exact fingerprint groups in tests, provided they honor the locked provider-impact versus runtime-content split.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 56 goal, implementation boundaries, success criteria, and planned split.
- `.planning/REQUIREMENTS.md` - GEN-03 and GEN-04, plus v1.12 server UX/generic-hosting constraints.
- `.planning/PROJECT.md` - current milestone context, no-live-UAT boundary, worker/control-plane direction, and security constraints.
- `.planning/STATE.md` - current phase state and pending warnings.
- `.planning/phases/55-generic-game-hosting-capability-contracts/55-CONTEXT.md` - locked Phase 55 decisions: clean-cut generic public contracts, server-scoped planning derives game type, and no legacy Arma public shims.

### Codebase Maps
- `.planning/codebase/STACK.md` - .NET, EF Core, Blazor, Kiota, and test stack.
- `.planning/codebase/ARCHITECTURE.md` - API/WebApp repository/generated-client/game-controller architecture.
- `.planning/codebase/INTEGRATIONS.md` - Steam, Hetzner, worker, and configuration integration context.

### Current API Planning And Snapshot Contracts
- `sources/Api/DTO/Payloads/CloudDryRunPlanRequestPayload.cs` - current dry-run request shape with Arma-specific runtime/port comments.
- `sources/Api/DTO/Payloads/CloudDryRunPlanCreationPayload.cs` - current persisted dry-run creation payload and snapshot fields.
- `sources/Api/DTO/CloudWorkerRuntimeProfileSnapshotDto.cs` - current worker-facing runtime profile snapshot, currently Arma-shaped.
- `sources/Api/DTO/GuidedCloudPlanningSelectionOptionsDto.cs` - current selector response with template/runtime/mission lists.
- `sources/Api/Controllers/LifecycleWorkersController.cs` - worker registration, heartbeat, job claim, progress/log/status, succeed/fail endpoints.

### Current WebApp Selector And Review Boundary
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudPlanningSelectionModels.cs` - current repository selector models with Arma-named runtime and mission choices.

### Current Cloud Worker
- `sources/CloudWorker/CloudWorker.csproj` - active cloud worker executable project included in the solution.
- `sources/CloudWorker/Program.cs` - worker entry point and current Arma executor wiring.
- `sources/CloudWorker/CloudWorkerRuntime.cs` - registration, heartbeat, claim loop, runtime-status recovery, and hard-coded Arma capability advertisement.
- `sources/CloudWorker/CloudWorkerApiClient.cs` - worker-facing HTTP protocol and current claim body records.
- `sources/CloudWorker/Arma3LifecycleJobExecutor.cs` - current Arma install/update/configure/start/stop/restart/log/cleanup executor.
- `sources/CloudWorker/Arma3ConfigurationRenderer.cs` - Arma configuration rendering from claim snapshots.
- `sources/CloudWorker/Arma3LaunchBuilder.cs` - Arma process launch construction from claim snapshots.
- `sources/CloudWorker/CloudWorkerOptions.cs` - worker bootstrap/state options and Arma-specific runtime options.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `sources/Api/DTO/GameHostingCapabilityDto.cs`, `GameHostingCloudCapabilityDto.cs`, `EGameHostingMode.cs`, and related Phase 55 capability DTOs provide the generic game/capability vocabulary to attach selectors and runtime choices to.
- `CloudDryRunPlanRequestPayload`, `CloudDryRunPlanCreationPayload`, `CloudDryRunPlan`, `CloudGameServerSession`, and cost/policy services already hold owner, provider, cost, input fingerprint, and snapshot state that can be generalized.
- `CloudWorkerRuntime` already provides a reusable worker shell: bootstrap, credential storage, registration, heartbeat, claim loop, and report endpoints.
- `Arma3LifecycleJobExecutor`, `Arma3ConfigurationRenderer`, `Arma3LaunchBuilder`, `ICommandRunner`, and process runner types are reusable as the first game-specific executor/recipe implementation.
- API and UI test projects already contain capability, cloud worker, runtime profile, dry-run, policy, and WebApp boundary tests that can be expanded or replaced.

### Established Patterns
- API controllers project DTOs rather than exposing EF entities.
- Generated Kiota files under `sources/WebApp/UtfApi` are not hand-edited; new generic routes should regenerate into the client.
- WebApp Razor binds hand-written repository models, not generated DTOs.
- Provider teardown remains the cost-stop boundary; runtime stop/cleanup must stay separate from provider deletion.
- Secret-bearing values, worker credentials, provider tokens, Steam credentials, bearer headers, cookies, and raw payload dumps must not appear in frontend models, logs, audit details, snapshots, or planning artifacts.

### Integration Points
- Replace Arma-named runtime profile and mission reference selector contracts with generic game-scoped selector groups.
- Replace Arma-named dry-run/review/policy DTOs and WebApp repository models with generic equivalents.
- Generalize worker claim payloads into a generic envelope plus allowlisted game-specific execution recipe/body.
- Rework stale-plan tests to distinguish provider-plan staleness from runtime-recipe review requirements.
- Rebuild EF schema/migrations around the generic model if cleaner, using EF tooling first.

</code_context>

<specifics>
## Specific Ideas

- Dry-run is a provider mutation, billing, cost, policy, and readiness gate; it should not be framed as proving runtime correctness or exact disk usage after future mission/mod changes.
- The user is not a fan of dry-run as a product concept, so downstream planning should keep user-facing labels task-oriented and avoid making users reason about raw "cloud plan" internals beyond necessary review gates.
- Users usually work from decision-ready summaries, while debug-style details are important for testing and operator inspection.
- The cloud worker is our own executable, not something Hetzner provides. Hetzner only provisions the VM and runs bootstrap/user-data; game work happens in `sources/CloudWorker`.
- The worker should do as little inference as possible. API-prepared recipes should tell it what to execute safely, while the worker owns game-specific execution mechanics.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 56-Generic Runtime, Template, And Plan Snapshots*
*Context gathered: 2026-07-04*

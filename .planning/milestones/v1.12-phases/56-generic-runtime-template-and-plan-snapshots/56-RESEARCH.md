# Phase 56: Generic Runtime, Template, And Plan Snapshots - Research

**Phase:** 56-generic-runtime-template-and-plan-snapshots  
**Date:** 2026-07-04  
**Mode:** Inline research, text-mode fallback  
**Requirements:** GEN-03, GEN-04

---

## Research Questions

1. Which active API, WebApp, persistence, worker, and test surfaces still expose Arma-specific runtime/profile/template/dry-run contracts?
2. Where should Phase 56 draw the boundary between generic public contracts and Arma 3 implementation internals?
3. Can the EF model be renamed/rebuilt aggressively without preserving existing local rows?
4. How should stale and superseded dry-run behavior split provider-plan invalidation from runtime recipe re-review?
5. Which validation and security checks are required to keep snapshots and worker payloads bounded and secret-safe?

---

## Key Findings

### Selector And Runtime Profile Surface

- `GameServersController.GetGuidedCloudPlanningSelectionOptionsAsync` currently returns one unscoped selector response from `CreateGuidedCloudPlanningSelectionOptionsAsync`.
- The selector query returns all current/enabled `CloudServerTemplates`, `Arma3RuntimeProfiles`, and `Arma3MissionReferences` without server-derived game-type filtering.
- `GuidedCloudPlanningSelectionOptionsDto` is generic in name, but its option groups still expose `GuidedCloudPlanningRuntimeProfileOptionDto` and `GuidedCloudPlanningMissionReferenceOptionDto` with Arma-specific summaries and fields.
- WebApp `CloudPlanningSelectionModels` still defines `Arma3RuntimeProfileChoice` and `Arma3MissionReferenceChoice`, and server details bind those types directly.
- Phase 56 should make the normal selector server/game-scoped. Admin, audit, repair, and stale-plan flows can explicitly include disabled or superseded options.

### Persistence And Schema

- `ApiDbContext` still exposes `Arma3MissionReferences`, `Arma3RuntimeProfiles`, and `Arma3RuntimeProfileConfigurationEntries`.
- `CloudDryRunPlan` and `CloudGameServerSession` still hold FK navigation properties to `Arma3RuntimeProfile` and `Arma3MissionReference`.
- Persisted snapshot fields are generic in name (`RuntimeProfileSnapshotJson`, `MissionReferenceSnapshotJson`) but currently deserialize to Arma DTOs in planning, review, cloud-selection, worker-dispatch, provisioning, policy, and reconciliation code.
- The repository currently has a single initial EF migration (`20260628220722_Initial`) plus the model snapshot. Context decision D-05 says there is no deployed production database or historical data to preserve, so Phase 56 may rename or rebuild the Arma-named schema aggressively.
- Any migration change still must be produced through EF tooling first, then hand-adjusted only when necessary.

### Dry-Run And Review Surface

- `CloudPlanningController` uses generic route names but still advertises and returns `Arma3CloudDryRunPlanDto` and `Arma3CloudDryRunPlanReviewDto`.
- `Arma3CloudDryRunPlannerService` validates the server as Arma 3, resolves template/runtime/mission snapshots, creates Arma-labeled resource inventory display names, and produces one combined normalized input fingerprint.
- `Arma3CloudDryRunPlanReviewService` compares one combined fingerprint and returns states such as `ValidFresh`, `ValidStaleMetadata`, `InputMismatch`, `Superseded`, and runtime/template mismatch states.
- Phase 56 needs generic review/policy DTOs for the user-facing path, generic WebApp repository mapper names, and explicit state labels for provider plan stale, runtime review required, valid provider plan, blocked policy, and current recipe.
- Provider-specific validation should stay in provider adapters/services. Hetzner metadata, pricing, firewall, image, location, and server-type rules should not be hidden behind a fake all-provider validator.

### Worker Payload And Execution Boundary

- The cloud worker executable already has a generic runtime shell: bootstrap, credential storage, registration, heartbeat, claim loop, job progress/log/status reporting, and recovery.
- The claim body still contains `CloudWorkerRuntimeProfileSnapshotBody? RuntimeProfile`, and the API-side `CloudWorkerRuntimeProfileSnapshotDto` is explicitly Arma-shaped.
- The worker currently advertises `["arma3"]` and dispatches to `Arma3LifecycleJobExecutor`, which is acceptable for Phase 56 if the protocol becomes a generic runtime recipe envelope with an allowlisted Arma body.
- The API should prepare execution recipes. The worker should execute bounded game-specific commands and avoid inferring selector, policy, stale-plan, or provider state.

### WebApp And Generated Client Boundary

- Phase 55 established generic routes and generated-client-backed repository behavior for creation/planning surfaces.
- Current generated request builders for dry-run and review still return Arma DTOs because the API produces Arma DTO response types.
- The WebApp repository maps generated Arma dry-run/review DTOs through `CloudWorkflowResultMapper.FromArma3DryRunPlan` and related Arma-specific mapper methods.
- Razor must continue to consume hand-written safe repository models, not generated DTOs. Phase 56 should rename/rework those repository models and mappers without binding generated DTOs in Razor.

### Tests And Regression Guards

- Existing tests already cover capability boundaries, cloud planning, Arma runtime profiles, dry-run review state, cloud worker dispatch, cloud provisioning, spending policy, and WebApp repository/redaction boundaries.
- Several existing boundary tests currently assert Arma-specific selectors and admin runtime-profile repository behavior. They should be updated or narrowed so user-facing selector/dry-run/review/worker payload contracts become generic while genuine Arma admin/executor internals remain allowed.
- Stale-plan tests need a new split:
  - Provider-impacting changes block provisioning until plan regeneration.
  - Runtime recipe/content changes require runtime review/acknowledgement and must not silently auto-update execution recipes.
  - Supersession distinguishes a replacement provider plan from a runtime recipe re-review.

---

## Recommended Plan Split

### 56-01 - Game-scoped runtime/template selector contracts

Create the generic selector and persistence foundation:

- Generic option DTOs for templates, runtime profiles, mission/runtime references, and typed game details.
- Server-derived game-type filtering in selector endpoints.
- Generic EF entity names for runtime profiles, mission references, and configuration entries if chosen by the implementer.
- Updated API/WebApp selector models, generated client, and boundary tests.

### 56-02 - Generic dry-run/review/policy facade with Arma 3 compatibility

Move user-facing planning/review/policy transport to generic DTOs:

- Generic dry-run result and review DTOs.
- Generic review state enum with provider-plan stale and runtime-review-required labels.
- Generic service interfaces with Arma implementation adapters behind them.
- Fingerprint split into provider-plan and runtime-recipe groups.
- WebApp repository mapper/model rename from Arma-specific dry-run/review names to generic names.

### 56-03 - Snapshot, audit, worker payload, and stale-plan safety tests

Close the safety boundary:

- Generic worker runtime recipe envelope with an allowlisted Arma body.
- Cloud session and dry-run snapshots store selected game, hosting mode, template/runtime/mission identifiers, provider-impact fingerprint, and runtime-recipe fingerprint.
- Provisioning, policy, reconciliation, and worker dispatch consume generic snapshots without losing Arma behavior.
- Tests for stale/superseded split, redaction, worker payload shape, audit context, and Arma 3 compatibility.

---

## Security Research

### Assets

- Provider tokens and metadata.
- Steam credentials, SteamCMD/DepotDownloader paths, and future provider/worker credentials.
- Worker bootstrap credentials and claimed lifecycle job payloads.
- Billing owner, cost estimate, policy decision, audit, and cloud inventory records.
- Runtime profile, mission, mod pack, template, and execution recipe snapshots.

### Risks

- Generic selector DTOs could accidentally expose Steam/provider/worker secret fields from game-specific profile data.
- Generic recipe envelopes could become raw JSON passthroughs that let clients or workers smuggle unvalidated commands.
- WebApp review summaries could bind generated DTOs directly and display raw planned-operation or validation JSON.
- Combining provider and runtime fingerprints could either over-block runtime-only edits or under-block provider-affecting changes.
- Rebuilding migrations could accidentally weaken FK, max-length, index, or delete-behavior constraints.

### Required Mitigations

- All selector, review, snapshot, and worker recipe DTOs must be allowlisted and secret-safe.
- API derives selected game/runtime behavior from persisted server/capability state, not from arbitrary client-supplied route or payload values.
- Provider-affecting and runtime-content fingerprint groups must be explicit and tested.
- Generated DTOs remain inside repository/service boundaries; Razor consumes safe hand-written models.
- Migration output must be reviewed for FK/delete behavior, string bounds, and lookup indexes.
- Worker recipes must carry a game identifier and recipe kind, then deserialize only known game-specific bodies.

---

## Validation Architecture

Phase 56 requires automated validation because it changes persistence, generated API/WebApp contracts, worker protocols, stale-plan state transitions, and secret-safe DTO boundaries.

Required validation layers:

- Focused API tests for game-scoped selectors, generic dry-run/review DTOs, provider/runtime fingerprint split, stale/superseded states, and Arma compatibility.
- WebApp boundary tests for generated client usage, safe repository models, generic mapper names, and absence of Arma DTO binding in Razor.
- Cloud worker tests for generic claim envelope shape, Arma recipe compatibility, redaction, and lifecycle executor dispatch.
- EF migration/model tests or build verification after any schema replacement.
- Source scans for public Arma-named dry-run/review/selector/worker-payload markers outside implementation-internal allowlists.
- `dotnet test X39.UnitedTacticalForces.slnx` as the broad verification command when feasible.

---

## Open Implementation Choices

- Exact entity names: `GameRuntimeProfile`, `GameMissionReference`, and `GameRuntimeProfileConfigurationEntry` are natural choices, but implementers can choose equivalent names if the public surface remains generic.
- Snapshot storage: keep separate `RuntimeProfileSnapshotJson`/`MissionReferenceSnapshotJson` columns or replace with a single generic `RuntimeRecipeSnapshotJson`/`RuntimeSelectionSnapshotJson` pair. Either is acceptable if tests cover compatibility and redaction.
- Admin runtime/mission management can remain Arma-focused when it is genuinely game-administration UI. User-facing selector, dry-run, review, policy, worker protocol, and repository workflow paths must be generic.
- The worker can keep only the Arma executor in Phase 56. The important contract change is the generic recipe envelope and explicit dispatch boundary.

---

## RESEARCH COMPLETE

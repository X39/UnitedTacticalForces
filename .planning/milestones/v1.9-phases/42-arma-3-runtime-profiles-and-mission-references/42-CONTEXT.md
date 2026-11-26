# Phase 42: Arma 3 Runtime Profiles And Mission References - Context

**Gathered:** 2026-06-21
**Status:** Ready for execution
**Mode:** Autonomous

<domain>
## Phase Boundary

Administrators can manage reusable Arma 3 runtime profiles and mission references that later cloud planning can select and project safely.

Implementation boundaries from ROADMAP:

- Do not upload secret-bearing files.
- Do not expose Steam credentials, provider tokens, worker credentials, cookies, bearer tokens, authorization headers, or raw secret-bearing payloads.
- Keep headless clients and Docker packaging out of scope.
- Keep live Hetzner, Steam, Arma, authenticated-browser, teardown, and cleanup UAT deferred.

Success criteria:

1. Runtime profiles persist versioned references to modpack revision, mission reference, configuration preset, directory assumptions, port policy, and runtime options.
2. Runtime-profile selection validates active Arma 3-compatible modpack, mission, terrain, and configuration references.
3. Dry-run and worker-facing snapshots receive allowlisted runtime-profile data.
4. Users can review selected runtime profile and mission context before provisioning without seeing secrets or raw worker payloads.
5. Tests cover persistence, validation, projection safety, and worker snapshot redaction.
</domain>

<code_context>
## Existing Code Insights

- Phase 41 established append-only version rows with `IsCurrent`, `IsEnabled`, stable ids, supersession FKs, sanitized audit reasons, DTO projections, controller policies, and focused redaction tests.
- `Arma3CloudDryRunPlannerService` persists non-secret input summaries, planned operation JSON, and dry-run/session fingerprints through `CloudInventoryService`.
- `Arma3CloudDryRunPlanReviewService` reconstructs current fingerprints from selected inputs and projects typed owner-safe review DTOs.
- `CloudWorkerDispatchService` returns a bounded claim snapshot with game server, session, active configuration entries, and modpack revision data.
- Worker-side `CloudWorkerApiClient` has an independent claim body record that should stay backward compatible when runtime profile data is absent.
</code_context>

<decisions>
## Implementation Decisions

- Mission references and runtime profiles use the Phase 41 append-only versioning pattern.
- Runtime profiles reference one active `ModPackRevision`, one current enabled mission reference, and existing active terrain through the mission reference.
- Configuration preset data is represented as allowlisted non-sensitive configuration entries on the profile, separate from game-server `ConfigurationEntry` rows.
- Dry-run planning stores runtime profile FK/id/version and safe JSON snapshots on both `CloudDryRunPlan` and `CloudGameServerSession`.
- Worker claims include a typed runtime profile snapshot parsed from the session snapshot JSON; no worker claim includes raw request payloads or secret-bearing fields.
</decisions>

<deferred>
## Deferred Items

- WebApp generated client/repository/UI consumption remains Phase 44.
- Template-aware guided entry and provisioning enforcement remains Phase 43.
- Live UAT remains deferred until CR-0001 implementation is otherwise complete.
</deferred>

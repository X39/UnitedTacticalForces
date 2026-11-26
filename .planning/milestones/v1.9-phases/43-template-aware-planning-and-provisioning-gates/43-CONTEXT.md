# Phase 43: Template-Aware Planning And Provisioning Gates - Context

**Gathered:** 2026-06-21
**Status:** Ready for execution
**Mode:** Autonomous

<domain>
## Phase Boundary

Guided entry, dry-run review, provider provisioning, cost, policy, reconciliation, ownership, and audit flows must honor selected cloud server templates and Arma 3 runtime-profile snapshots.

Implementation boundaries from ROADMAP:

- Template use must not bypass billing-owner, stale-plan, policy, reconciliation, provider-teardown, or cleanup boundaries.
- Provider provisioning must reject stale or invalid template/profile snapshots before token resolution and provider mutation.
- Do not expose provider tokens, Steam credentials, worker credentials, cookies, authorization headers, bearer tokens, or raw secret-bearing payloads.
- Live Hetzner, Steam, Arma, authenticated-browser, teardown, cleanup, and Phase 30 evidence remain deferred.

Success criteria:

1. Guided cloud planning can default inputs from an active template and runtime profile while exposing user-visible constraints.
2. Dry-run review persists template/runtime-profile snapshots and flags stale, disabled, or constraint-violating inputs before provisioning.
3. Provider provisioning rejects stale or invalid template/profile snapshots before provider token resolution and provider mutation.
4. Cost estimate, policy gate, reconciliation, ownership, and audit projections include template/profile context without secret leakage.
5. Tests cover stale template, disabled template, override bounds, owner/policy/reconciliation enforcement, and provider-mutation ordering.
</domain>

<code_context>
## Existing Code Insights

- Phase 41 added append-only `CloudServerTemplate` versions with stable ids, enabled/current state, provider defaults, storage/port/runtime bounds, DTO projection, admin controller/service behavior, and redaction tests.
- Phase 42 added append-only Arma 3 runtime profiles and mission references, optional runtime profile selection in dry-run/review payloads, safe runtime/mission snapshot JSON on `CloudDryRunPlan` and `CloudGameServerSession`, worker claim projection, and redaction tests.
- `Arma3CloudDryRunPlannerService` currently defaults provider fields from configuration/metadata, persists normalized input fingerprints through `CloudInventoryService`, and stores planned operations/resource details as bounded JSON.
- `Arma3CloudDryRunPlanReviewService` recomputes current input fingerprints from the review payload and active references, then classifies stale, invalid, owner-mismatched, superseded, and input-mismatched plans.
- `CloudProvisioningService` is the authoritative provider-mutation gate. It validates plan/session/resource state, active owner, metadata freshness, and only then resolves the Hetzner token.
- Cost, policy, inventory/reconciliation, and audit surfaces already project persisted session/plan/resource data; Phase 43 should enrich those projections with allowlisted template/profile labels and ids, not raw payloads.
</code_context>

<decisions>
## Implementation Decisions

- Template selection is optional for backward compatibility, but when present it becomes part of the dry-run input fingerprint and persisted plan/session snapshot.
- Template defaults fill omitted dry-run fields; explicit overrides are accepted only when the template allows them and the requested values stay within template bounds.
- Template snapshots are stored as allowlisted JSON plus stable FK/id/version columns on dry-run plans and cloud sessions, matching the runtime-profile snapshot pattern.
- Review and provisioning validate current enabled template/profile state against the stored snapshot before marking a plan usable for provider mutation.
- Provider token resolution remains after all template/profile, owner, metadata, and plan/session/resource validation.
</decisions>

<deferred>
## Deferred Items

- WebApp generated client, repository, and UI consumption remains Phase 44.
- Live provider/browser/runtime evidence remains deferred until CR-0001 implementation is otherwise complete.
</deferred>

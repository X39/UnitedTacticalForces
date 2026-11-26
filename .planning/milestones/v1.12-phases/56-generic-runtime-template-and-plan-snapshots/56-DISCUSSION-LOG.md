# Phase 56: Generic Runtime, Template, And Plan Snapshots - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 56-Generic Runtime, Template, And Plan Snapshots
**Areas discussed:** Selector shape, Snapshot compatibility, Dry-run review model, Stale/superseded plan rules

---

## Selector Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Unified groups | One game-scoped selector response with typed option groups for templates, runtime profiles, missions, and future game-specific choices. | yes |
| Separate lists | Keep separate template/runtime/mission lists, but rename them generic and filter each by selected game. | |
| You decide | Let the planner choose the smallest generic shape that preserves Phase 55 boundaries and Arma 3 behavior. | |

**User's choice:** Unified groups.
**Notes:** Follow-up decisions selected typed generic option DTOs, current-enabled options by default with optional history for admin/review repair, and server-side game filtering only.

---

## Snapshot Compatibility

| Option | Description | Selected |
|--------|-------------|----------|
| Generic payloads, cautious DB | Expose generic API/WebApp/worker snapshot DTOs now, but keep existing DB table names unless a migration is clearly low-risk and fully covered. | |
| Aggressive rename/rebuild | Rename/transform DB entities, tables, DTOs, services, and audit names into generic runtime/profile/mission concepts in this phase. | yes |
| DTO facade only | Leave persistence and worker snapshots mostly Arma-named, but add generic WebApp-facing DTOs for selectors and review. | |

**User's choice:** Aggressive rename/rebuild.
**Notes:** The user clarified the app is not deployed and local test databases can be deleted. Migrations may be deleted/recreated if cleaner, but migrations must be generated via EF tooling first and only modified afterwards. Worker contract discussion locked a generic envelope with game-specific body and API-prepared execution recipe plus worker executor.

---

## Dry-Run Review Model

| Option | Description | Selected |
|--------|-------------|----------|
| Fully generic user-facing path | Generic request/response/review/policy DTOs and WebApp repository models, with Arma 3 only inside server-side adapters and worker recipes. | yes |
| Generic entry, mixed internals | Generic routes and repository methods, but review/cost/policy DTOs may keep some Arma-specific names internally for this phase. | |
| Review facade only | Keep most dry-run/review/policy implementation Arma-named and expose only generic labels to the UI. | |

**User's choice:** Fully generic user-facing path.
**Notes:** Provider-specific validation should live behind provider adapters. Public Arma-named dry-run DTOs, mapper names, repository models, generated-client expectations, and routes should be removed/renamed except implementation-internal Arma recipe/adapters. Reviews should default to decision-ready summaries, with debug-style details available for testing/operator inspection.

---

## Stale/Superseded Plan Rules

| Option | Description | Selected |
|--------|-------------|----------|
| Split provider vs runtime staleness | Provider/cost/policy changes block provisioning until regenerated; runtime recipe/content changes require review/acknowledgement but not full provider re-planning unless they affect provider assumptions like storage/ports. | yes |
| Strict block everything | Any selected template/runtime/mission/modpack snapshot change requires full dry-run regeneration before provisioning. | |
| Loose warning model | Most changes are warnings unless owner/provider/cost policy gates fail. | |

**User's choice:** Split provider vs runtime staleness.
**Notes:** The user challenged what dry-run is actually supposed to achieve, noting mission and mod-pack changes are common and that dry-run cannot prove future Arma disk usage. The locked decision treats dry-run as a provider mutation/billing/policy gate, not a runtime correctness guarantee. Provider-impact fields are explicit; runtime recipe/content changes require review/acknowledgement but not full provider re-planning when provider assumptions are unchanged. Review states should be explicit and user-visible.

---

## the agent's Discretion

- The agent may choose exact class/entity names and adapter boundaries as long as active public API/WebApp contracts are generic and Arma-specific names remain implementation-internal only.
- The agent may decide whether replacing old migrations wholesale is worthwhile, but any migration must originate from EF tooling.
- The agent may define exact fingerprint groups in tests, provided they honor the locked provider-impact versus runtime-content split.

## Deferred Ideas

None.

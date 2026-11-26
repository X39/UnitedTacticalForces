# Phase 64: Guided Server Creation - Context

**Gathered:** 2026-11-19
**Status:** Ready for planning

<domain>
## Phase Boundary

A member creates a server through a short guided flow generated from server capabilities, sees what it will cost
before committing, and chooses whether it starts now or stays off. Replaces the ~12-field `CreateServerPage`
(`sources/ReactApp/src/features/hosting/CreateServerPage.tsx`). Requirements CRT-01..CRT-06. Depends on Phase 62.
Changing an existing server (Phase 65) and cost history (Phase 66) are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Presets (CRT-05)
- **D-01:** A create preset is built from what already exists: **configuration profiles** (admin-managed, per game,
  with a default) and **mod packs**. They are not named "presets" in the product today; this phase makes them
  **proper presets** in the creation flow: a first-class, friendly-named preset picker, with API, table, or admin
  UI changes allowed wherever they give a better preset experience. The user said "we can make them proper
  presets"; the exact naming and metadata shown per preset is Claude's discretion.
- **D-02:** **Presets default to keep off:** choosing or defaulting a preset pre-selects "Create and keep off".
  Starting is always an explicit choice (CRT-04, CRT-05).

### Delegated to Claude (user answered "You decide")
- **D-03:** Up-front decisions (at most three, CRT-01): recommended **Name, game, and size** (title, game, compute
  profile with price). Preset, provider, storage, mod pack, terrain/scenario and cost owner default sensibly and live
  under an **Advanced** disclosure. Cost owner shows as a read-only "Billed to ..." line up front so billing is never
  hidden; changing it is in Advanced. The preset picker is an entry shortcut, not a fourth decision.
- **D-04:** Layout: **one page, capability-driven**, with a sticky cost summary and create bar. Fields for a game's
  declared capabilities (terrain/scenario, mod packs, configuration profiles, storage strategies) render generically
  from `hostingService.capabilities` and related option queries, only when the capability exists, so no game-specific
  code is in the flow (CRT-06). Must work at 320px and 390px.
- **D-05:** Live cost estimate (CRT-02) reuses `combinedHourlyCost` and `estimateStorage` and updates as choices
  change: hourly and monthly figures, never an invented number; show "not available" when a price is unknown.
- **D-06:** Early block (CRT-03): the create action is disabled with a visible reason (`UnavailableReason`) when the
  selected cost owner is frozen or otherwise cannot pay, before submit. Current data exposes only `isFrozen` on
  `GameServerCostOwnerOptionDto`; the researcher confirms whether balance or affordability data exists and, if a
  cannot-pay signal beyond frozen is required, adds the minimal projection in the owning layer (per AGENTS.md,
  regenerate the OpenAPI snapshot and clients).
- **D-07:** "Create and start" (CRT-04) creates the server, then starts through the shared
  `useCostConfirmation().request({ serverId, actionId: "start", source: "create" })`. If the member dismisses the
  confirmation or start fails, the server remains created and off, and the member lands in the workspace with the
  reason shown. "Create and keep off" never starts anything.
- **D-08:** Existing form capabilities must not be lost: every `CRT-*` row in
  `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` is marked `kept` or `relocated`
  (never deleted), and the parity completeness tests keep passing.

### Claude's Discretion
- All mechanics of D-03..D-08, copy, component split under `src/components/server/` and `src/features/hosting/`,
  preset naming and display, and German copy in `src/locales/en.json` and `de.json`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning
- `.planning/ROADMAP.md` Phase 64 - goal and success criteria
- `.planning/REQUIREMENTS.md` CRT-01..CRT-06
- `.planning/phases/61-foundations-localization-member-access/61-CONTEXT.md` - state/cost derivation, cost confirmation, locale and token rules
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - `CRT-*` and `SVC-*` rows owned by Phase 64
- `.planning/phases/62-fleet-quick-actions/62-CONTEXT.md` - fleet create entry, live updates
- `.planning/phases/63-workspace-shell-activity/63-CONTEXT.md` - workspace landing after create

### Code
- `sources/ReactApp/src/features/hosting/CreateServerPage.tsx` - current form (replaced)
- `sources/ReactApp/src/features/hosting/createServerModel.ts` - schema, cost owner validity, `combinedHourlyCost`
- `sources/ReactApp/src/components/server/useCostConfirmation.ts` - shared cost confirmation
- `sources/ReactApp/src/generated/api.ts` - `GameServerCreationPayload`, `GameServerCostOwnerOptionDto`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `useCostConfirmation`, `CostConfirmDialog`, `UnavailableReason`, `ServerStateBadge`, `CostIndicator`
- `hostingService`: `capabilities`, `creationModPacks`, `profileOptions`, `terrains`, `scenarios`, `creationCostOwners`, `estimateStorage`, `create`
- `combinedHourlyCost`, `isCostOwnerSelectionValid`, `isProviderAvailable` in `createServerModel.ts`

### Established Patterns
- Flat `Server_{Area}_{Item}` locale keys, `--utf-*` tokens, no raw hex or pixel literals in `components/server/*`
- Types come from generated OpenAPI; API changes are allowed when needed; regenerate clients

### Integration Points
- Fleet header and empty-state create links (Phase 62), workspace route after creation (Phase 63)

</code_context>

<specifics>
## Specific Ideas

Presets are the existing configuration profiles and mod packs, surfaced properly rather than newly invented.

</specifics>

<deferred>
## Deferred Ideas

- Admin-defined or per-user saved presets with new persistence, only if later needed.

</deferred>

---

*Phase: 64-Guided Server Creation*
*Context gathered: 2026-11-19*

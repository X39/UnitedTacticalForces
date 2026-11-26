# Phase 65: Change a Server - Context

**Gathered:** 2026-11-20
**Status:** Ready for planning

<domain>
## Phase Boundary

A member changes an existing server (mods, configuration, files) by staging changes, reviewing them, and applying them
explicitly, with clear notice of when they take effect (next start). Covers CHG-01..CHG-07. Scope is the React app
(`sources/ReactApp`) plus any API, shared-contract, or database change the feature needs. Mod catalog search/install
(FUT-01), undo/revert (FUT-04), and server-defined presets (FUT-03) stay out.

</domain>

<decisions>
## Implementation Decisions

### Project-wide policy changes made in this discussion (user decisions)
- **D-00a:** The current API is **not a frozen contract**. Docs that told agents to keep API changes minimal or
  avoid them were amended (AGENTS.md, PROJECT.md, ROADMAP.md, Phase 62/64 CONTEXT, research docs). Add or change API
  projections, endpoints, and behavior whenever the feature is better for it; keep architecture boundaries and
  regenerate the OpenAPI snapshot and generated clients.
- **D-00b:** **URLs are not a stable contract.** Routes, pathnames, and query parameters may change; the route
  manifest need not stay byte-identical. When routes change, update `sources/Common/Ui/routes.json` and the route
  tests together and keep them green. VER-01 and roadmap criterion 69.4 were reworded accordingly.

### Staging model (CHG-03)
- **D-01:** One staged draft covers **mod pack, configuration profile, and configuration values** (and secret
  changes, see D-08). **File operations (upload, delete, create folder) stay immediate**; they act on files, not
  config, and uploads are slow.
- **D-02:** Draft is **in-memory only** (no browser-storage persistence), so there is no stale-draft risk.
- **D-03:** Review UI placement is Claude's discretion; default intent is a sticky pending-changes bar plus a
  before/after review step with Apply and Discard, usable at 320px and 390px.
- **D-04:** Partial apply failure behavior is Claude's discretion; default intent: applied items leave the draft,
  failed items stay staged with the reason, nothing is rolled back silently.

### Mod picker (CHG-01)
- **D-05:** Capability-driven, game-agnostic **mod pack chooser** (current pack marked) using the server-scoped
  `GetModPackOptions` endpoint and `ChangeModPack`, with unavailable packs shown with a reason. The review shows a
  **contents diff** (current vs new pack). The API may gain a read-only pack-contents projection if the options DTO
  lacks it (D-00a). Validate with Arma 3 mod packs; Minecraft is not defined in the API.

### Settings and secrets (CHG-02, CHG-07)
- **D-06:** **The API declares Common vs Advanced**: add a group/priority field to each configuration option in the
  game definition so every game decides its own Common set; the UI renders generically.
- **D-07:** Secret values are **masked with an explicit Reveal**, offered only to members allowed to manage secrets
  (`canManageSecrets`). Revealed values are fetched on demand, auto-hidden, and never stored in staged drafts, URLs,
  or logs.
- **D-08:** Setting or rotating a secret is a **staged change** in the review showing only "changed", never the value.

### Take effect and leave guard (CHG-04, CHG-05)
- **D-09:** Add an **API "applies on next start" / pending-restart signal** so the UI shows a persistent banner only
  when applied changes truly wait for a start, plus a notice in the review step and after apply. This brings the
  restart-required drift badge (FUT-02) into scope for the signal needed here.
- **D-10:** The unsaved-changes guard covers **everything**: section switches (`?tab=`), router navigation
  (`useBlocker`), and browser tab close/reload (`beforeunload`). The prompt offers Review, Discard, or Stay.

### Files (CHG-06)
- **D-11:** Current folder lives in `?path=` combined with the Files section; folder changes **push history** so Back
  returns to the previous folder. Invalid or missing paths fall back to root with a notice. Final mechanics are
  Claude's discretion.

### Claude's Discretion
- Review UI placement (D-03), partial-failure semantics (D-04), exact pack-contents projection shape (D-05), the
  group field naming and fallback for options without a group (D-06), banner design (D-09), all copy and German
  strings, and component split under `src/components/server/` and `src/features/hosting/`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning
- `.planning/ROADMAP.md` - Phase 65 goal, success criteria, Cross-Phase Constraints (amended routes/API policy)
- `.planning/REQUIREMENTS.md` - CHG-01..CHG-07, FUT-01..04
- `.planning/phases/63-workspace-shell-activity/63-CONTEXT.md` - Section structure (Settings = config + credentials; Files section)
- `.planning/phases/64-guided-server-creation/64-CONTEXT.md` - Capability-driven option patterns, cost confirmation use
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - Rows to update for moved/kept capabilities
- `AGENTS.md` - Architecture boundaries, amended API-change policy

### Code
- `sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx` - Current immediate-apply config and profile UI
- `sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx` - File manager (local `path` state today)
- `sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx` - Secret set/rotate/reveal
- `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx` - Section host
- `sources/Api/Controllers/GameServersController.cs` - `GetModPackOptions`, `ChangeModPack`, config, profile, files endpoints

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ServerConfigurationPanel`, `ServerFilesPanel`, `ServerSecretsPanel`: existing behaviors to relocate/stage, not lose.
- `AdvancedOptions`, `PresetField`, `OptionListState` (Phase 64): capability-driven option rendering with unavailable reasons.
- `useCostConfirmation`, `UnavailableReason`: shared confirmation and disabled-with-reason patterns.

### Established Patterns
- Flat `Server_{Area}_{Item}` locale keys in `en.json`/`de.json`; `--utf-*` tokens; no raw `<input>`/`<button>` controls.
- Types come from generated OpenAPI; regenerate snapshot and clients after API changes.

### Integration Points
- Settings and Files sections in the workspace shell; `?tab=` selects section, `?path=` selects folder.
- Game definition catalog (`sources/Api/Services/GameServerDefinition`) for the Common/Advanced group field.

</code_context>

<specifics>
## Specific Ideas

User stressed twice that agents wrongly treat the API and URLs as frozen; planners must not reintroduce either constraint.

</specifics>

<deferred>
## Deferred Ideas

- Mod catalog search/install (FUT-01), undo/revert (FUT-04): remain future.

</deferred>

---

*Phase: 65-Change a Server*
*Context gathered: 2026-11-20*

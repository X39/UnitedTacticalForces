---
phase: 69-consistency-accessibility-verification
plan: 20
subsystem: ui
tags: [react, mod-packs, terrains, select-field, action-button, state-views, status-tones, ratchet, localization]

requires:
  - phase: 69-01
    provides: "PageStack, shared-primitives header and state styles"
  - phase: 69-03
    provides: "StateViews, the empty-state, dismiss and tab keys, closed navigation labels"
  - phase: 69-04
    provides: "family.admin-modpacks-terrains.json ratchet and its writer"
  - phase: 69-06
    provides: "discovery findings (none were assigned to the mod pack or terrain pages)"
provides:
  - "Mod packs list, create, workspace and content editor on shared controls, shared states and the padded SelectField"
  - "Terrains list, create, detail and image field on shared controls, shared states and named dismiss labels"
  - "src/features/admin/assetTones.ts: tone maps for asset lifecycle, readiness, mod origin and edit state"
  - "src/features/admin/terrains.css: two-class rules that keep the legacy terrain layout with shared buttons"
  - "tests/admin-asset-pages.test.tsx (31 tests)"
affects: [69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: b0b16d949a4ca7363b5cb8cd82e2ab0558e4f403
commits: 4

actuals:
  tokens: 20000
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "A 404 with no body becomes null in the queryFn so the not-found state is reachable (a query cannot resolve to undefined)"
    - "Early-return states share one small wrapper component that renders the page header above the state view (TerrainDetailState)"
    - "Metric buttons are ghost ActionButtons with a two-class selector in the family stylesheet instead of an edit to styles.css"

key-files:
  created:
    - sources/ReactApp/src/features/admin/assetTones.ts
    - sources/ReactApp/src/features/admin/terrains.css
    - sources/ReactApp/tests/admin-asset-pages.test.tsx
  modified:
    - sources/ReactApp/src/features/admin/ModPacksPage.tsx
    - sources/ReactApp/src/features/admin/ModPackCreatePage.tsx
    - sources/ReactApp/src/features/admin/ModPackWorkspacePage.tsx
    - sources/ReactApp/src/features/admin/ModPackContentEditor.tsx
    - sources/ReactApp/src/features/admin/modPacks.css
    - sources/ReactApp/src/features/admin/TerrainsPage.tsx
    - sources/ReactApp/src/features/admin/TerrainCreatePage.tsx
    - sources/ReactApp/src/features/admin/TerrainDetailPage.tsx
    - sources/ReactApp/src/features/admin/TerrainImageField.tsx
    - sources/ReactApp/tests/ratchets/family.admin-modpacks-terrains.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/e2e/public-shell.spec.ts

key-decisions:
  - "The mod pack and terrain list h1 is the navigation label (Mod packs / Mod-Pakete, Terrains / Karten); the kicker keeps the navigation label like the Members and Roles pages."
  - "The content editor has no grid, so nothing stays on the ratchet: it converted completely and the family file is now empty (the plan allowed one entry)."
  - "The final forward action of each create or edit flow is gold, header links to the list are ghost, the Next and Review steps follow the existing page rhythm."
  - "The deactivate confirm label stays Common_Delete because the terrain dialog body says delete and no 'Delete terrain' key exists (XPF-69-20-1)."

patterns-established:
  - "Mod origin and readiness badges resolve through toneOf over feature tone maps (assetTones.ts), not inline ternaries"

requirements-completed: [CON-01, CON-02, CON-03, L10N-03]

coverage:
  - id: D1
    description: "Mod packs and terrains list h1 equals the navigation label in English and German, above forbidden, loading and error states"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-asset-pages.test.tsx#mod packs list / terrains list (h1, German, forbidden)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Mod pack game picker is the padded SelectField; every mod pack and terrain control is a shared control and no raw control remains in the family"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-asset-pages.test.tsx#renders the game picker as the shared padded select; tests/ratchets/ratchet.test.ts (family file empty)"
        status: pass
    human_judgment: true
    rationale: "The padded chevron geometry (text never under the arrow) is asserted by class only in jsdom; nobody looked at the select in a browser."
  - id: D3
    description: "Workspace tab labels come from locale keys in both languages; no hard-coded Content label"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/admin-asset-pages.test.tsx#labels the content and danger tabs (English and German)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Deactivate dialogs name keeping the mod pack or terrain active, never Cancel"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-asset-pages.test.tsx#names the effect of the deactivate dialog dismiss button; #names keeping the terrain in the deactivate dialog"
        status: pass
    human_judgment: false
  - id: D5
    description: "Every page renders exactly one h1 above loading, not-found, forbidden and error states; badges use the tone lexicon"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-asset-pages.test.tsx (forbidden, not-found, retryable error per page; badge tones)"
        status: pass
      - kind: e2e
        ref: "playwright (temporary private-port config): public-shell.spec.ts terrain tests, 2 passed"
        status: pass
    human_judgment: false

duration: ~3h wall clock (start time not captured)
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 20: Mod pack and terrain administration convergence Summary

**Mod packs and terrains now share the page header (h1 equal to the navigation label), state views, the padded SelectField, ActionButton and the tone lexicon; the content editor lost every raw control and the family ratchet is empty.**

## Performance

- **Tasks:** 3 (1 tracer, 2 auto) plus one tone follow-up commit
- **Files:** 3 created, 12 modified (plus this summary)

## Accomplishments

- **Mod packs list:** h1 is `Shared_CommandCenter_Nav_ModPacks`; search is `SearchField`, filters and ownership are two `FilterChipGroup`s, metrics are ghost `ActionButton`s, forbidden/loading/error/empty use `StateViews` with stale rows kept under a failed refetch, the create link is gold.
- **Mod pack create:** game picker is `SelectField` (label from `React_Terrains_GameLabel`), title is `TextField`, no-game state is an `EmptyState` with the 69-03 keys, footer actions are `ActionButton`s.
- **Mod pack workspace:** tab labels come from keys (`React_ModPackWorkspace_Tab_Content`, `_Tab_Danger`), not-found is a titled `EmptyState` with a way back, the load error fills the previously raw `{0}` and `{1}` placeholders of `Auto_FailedToLoadModPack_8b2eb217` and retries all three reads, the deactivate dialog dismisses with `React_ModPackWorkspace_Deactivate_Dismiss` and starts on the dismiss button.
- **Content editor:** searches are `SearchField`, source entry, provider, installer, usage and pinned revision fields are shared fields, the game format import is `FileChooserButton`, the catalog items are ghost `ActionButton`s inside list items, the nested pack options are `CheckboxField`s. Validation and payload code is untouched (`ModPackContentEditor.test.ts` and `modPacks.normalized.test.ts` stay green).
- **Terrains:** h1 and table caption are `Shared_CommandCenter_Nav_Terrains`; list on `StateViews`, `SearchField`-based toolbar plus a `FilterChipGroup`; create page on `SelectField`/`TextField` with stepper buttons as `ActionButton`s; detail page on `TextField`, `ActionButton`, named dismiss label, and one h1 over every state including an invalid or unknown id.
- **Tones:** `assetTones.ts` (lifecycle, readiness, mod origin, edit state) replaces the inline badge ternaries.
- **Ratchet:** `family.admin-modpacks-terrains.json` went from 8 files (63 controls, 40 look-alikes, 1 literal) to 0 files. `modPacks.css` lost five off-grid pixel values (ceiling 9 to 4).

## Task Commits

1. **Task 1 (tracer): mod packs list** - `51987110` (refactor)
2. **Task 2: mod pack create, workspace, content editor** - `e6f76f2d` (refactor)
3. **Tone follow-up and mod pack not-found fix** - `9b19434d` (refactor)
4. **Task 3: terrain pages** - `b0b16d94` (refactor)

Tracer gate: the tracer `<verify>` is automated only; it was re-run end to end and passed before expansion. Commits were created with the standard hooks and signed or unsigned as the machine allowed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Plan premise differed from code] No select on the mod packs list and no grid in the content editor**
- **Found during:** Task 1 and Task 2
- **Issue:** The plan puts the "mod pack select" on `ModPacksPage` (key link pattern `SelectField` there) and expects the editor's grid inputs to stay on the ratchet. `ModPacksPage` has no select (its filters are chips); the unpadded select is the game picker on `ModPackCreatePage`. The content editor has no grid, only ordinary fields.
- **Fix:** The select was converted where it exists; the editor converted completely, so the family file is empty instead of holding one entry. `ModPacksPage` therefore has no `SelectField` (the acceptance greps for that page, `Pages_ModPacks_AllModPacks` count 0 and no raw controls, pass).
- **Commits:** `51987110`, `e6f76f2d`

**2. [Rule 1 - Bug] A missing mod pack or terrain never showed its not-found state**
- **Found during:** writing the not-found tests
- **Issue:** `normalizedWorkspace` and `loadTerrain` resolve to `undefined` on 404, and TanStack Query turns an `undefined` result into an error, so the `!workspace` and `!terrain` branches were unreachable and a 404 showed the load error.
- **Fix:** the queryFn maps `undefined` to `null`.
- **Files modified:** `ModPackWorkspacePage.tsx`, `TerrainDetailPage.tsx`
- **Commits:** `9b19434d`, `b0b16d94`

**3. [Rule 1 - Bug] Unfilled placeholders in the mod pack load error**
- **Issue:** `Auto_FailedToLoadModPack_8b2eb217` is "Failed to load mod pack {0}: {1}" and the page rendered it with the placeholders unfilled.
- **Fix:** `formatResource` with the route id and the error message.
- **Commit:** `e6f76f2d`

**4. [Rule 1 - Bug] Nested `main` landmark on the terrain create page**
- **Fix:** the step container is a `div` (the layout already provides the main landmark).
- **Commit:** `b0b16d94`

**5. [Rule 2 - Missing critical] Destructive dialogs start on the dismiss button**
- **Fix:** `initialFocus="dismiss"` on both deactivate dialogs (T-69-01 intent).
- **Commits:** `e6f76f2d`, `b0b16d94`

**6. [Rule 3 - Blocking] Existing e2e expected the old terrain h1**
- **Issue:** `tests/e2e/public-shell.spec.ts` asserted the heading "All Terrains".
- **Fix:** it now asserts "Terrains". The two terrain tests of that spec pass on a private port.
- **Commit:** `b0b16d94`

**7. [Scope] Files outside `files_modified`**
- `src/features/admin/assetTones.ts` and `src/features/admin/terrains.css` (new), `tests/styles/legacy-ratchet.json`, `tests/e2e/public-shell.spec.ts`. The terrain layout rules live in `styles.css`, which this plan may not edit, so the shared buttons needed their own two-class rules in a new stylesheet.

**Total deviations:** 6 auto-fixed (1 premise, 3 Rule 1, 1 Rule 2, 1 Rule 3) plus the scope note.

## Notes for the orchestrator

- **New stylesheet needing a legacy ratchet entry:** `src/features/admin/terrains.css` with a zero ceiling entry in `tests/styles/legacy-ratchet.json` (added in `b0b16d94`, next to `features/admin/modPacks.css`, whose `offGridPx` ceiling was lowered from 9 to 4). Siblings edit that file too; expect a trivial merge.
- `tests/mocks/react-icons.tsx` lists only the icons the tests use; the plan's arrow icons were not in it, so the editor keeps its `↑` and `↓` glyphs inside icon-only `ActionButton`s.
- `files_deleted:` none.

## Cross-plan findings

| Id | File | Problem | Proposed fix |
|----|------|---------|--------------|
| XPF-69-20-1 | `src/locales/en.json`, `de.json`; `TerrainDetailPage.tsx` confirm label | The plan names a "Delete terrain" confirm label but no such key exists; the dialog confirm reads the generic `Common_Delete` ("Delete"). The dialog actually deactivates the terrain while its body says delete. | Add `Pages_Data_Terrains_All_ConfirmDeleteTerrain_Confirm` ("Delete terrain" / "Karte löschen") and use it, or align the body and confirm with "Deactivate". |
| XPF-69-20-2 | `ModPackContentEditor.tsx`, `ModPackCreatePage.tsx`, `ModPackWorkspacePage.tsx` | 64 `t("English sentence")` lookups still have no locale entry (for example "Review mod pack", "Creating…", "Immutable", "Effective mod list"), so those strings render in English in German. This is the 65-item list from `deferred-items-69-03.md`, one fewer because the editor labels now reuse existing keys. This plan may not edit locale files. | 69-33 adds keyed entries and rewrites the three `t = (key) => translate(key, { defaultValue: key })` shims to typed keys. |
| XPF-69-20-3 | `src/locales/en.json` | `Auto_CreateModPack_04e0630f` ("Create Mod Pack") and `Auto_CreateTerrain_636ac171` ("Create Terrain") are title case while the navigation labels are sentence case. | Change the values to sentence case in 69-33 (and update `public-shell.spec.ts` line 1442 and `admin-asset-pages.test.tsx`). |
| XPF-69-20-4 | `src/locales/*.json` | `Pages_ModPacks_AllModPacks` and `Pages_Terrains_AllTerrains` now have no reader. | Retire in 69-33 with the other unused keys. |

## Authentication Gates

None.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored, nothing installed).
- `npm run build:assets` (run by the temporary Playwright config) rewrote `src/generated/api.ts` with line endings only; it was restored with `git checkout` and never committed. The temporary Playwright config was deleted after the run.
- A tracing `tsc` run was moved to the background twice by the 3 minute foreground limit under load; both finished clean.

## Verification

- `npx vitest run tests/admin-asset-pages.test.tsx tests/admin-mod-packs.test.ts tests/admin-terrains.test.ts tests/admin-terrain-services.test.ts src/features/admin tests/ratchets tests/styles tests/locales tests/command-overview.test.ts tests/status-tones.test.ts`: 19 files, 393 passed, 1 skipped (the opt-in ratchet writer).
- `npm run lint` (`--max-warnings 0`), `npx tsc -b` and `npm run locales:check` (3980 keys, 0 failures, 0 warnings): all clean.
- Playwright on a private port (temporary config, deleted): `public-shell.spec.ts -g terrain`, 2 passed.
- Acceptance greps: `Common_Cancel` in `TerrainDetailPage.tsx` 0, `Pages_Terrains_AllTerrains` in `TerrainsPage.tsx` 0, `Pages_ModPacks_AllModPacks` in `ModPacksPage.tsx` 0, raw controls in `ModPacksPage.tsx` 0, `className="button` in the workspace page and the editor 0, family file keys 0 (at most 1).
- Not run: the `UTF_A11Y_DISCOVERY` pass and the registry audit (the mod pack and terrain screens are in no registry slice yet; they belong to the Wave 3 plans).

## Known Stubs

None.

## Threat Flags

None. T-69-26 is mitigated: `ModPackContentEditor.test.ts` and `modPacks.normalized.test.ts` pass unchanged, and the payload code (`materializeRevision`, `validateModPackEditor`) was not touched.

## Next Phase Readiness

Mod pack and terrain pages are ready for the Wave 3 registry audit; 69-33 owns the locale clean-up listed above.

## Self-Check: PASSED

- Created files exist: `assetTones.ts`, `terrains.css`, `admin-asset-pages.test.tsx`.
- Commits `51987110`, `e6f76f2d`, `9b19434d`, `b0b16d94` are ancestors of HEAD.

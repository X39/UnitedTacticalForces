---
phase: 64-guided-server-creation
plan: 02
subsystem: webapp
tags: [react, hosting, create-server, pure-model, zod, presets]
requires: []
provides:
  - "deriveCreateReadiness, estimateCreateCost, resolveCreateDefaults and the preset projection in createServerModel.ts"
  - "Shared create-flow fixtures in tests/helpers/createFixtures.ts"
affects: [64-04, 64-05, 64-06, 64-07, 64-08]
tech-stack:
  added: []
  patterns:
    - "Pure React-free model module shared by page, summary and option components"
    - "Touched-aware default resolution: only still-valid choices are kept"
key-files:
  created:
    - sources/ReactApp/tests/helpers/createFixtures.ts
    - sources/ReactApp/tests/hosting/create-presets.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/createServerModel.ts
    - sources/ReactApp/tests/hosting/create-server.test.ts
key-decisions:
  - "A frozen owner is the only cannot-pay signal; CreateReadinessInput has no balance input (D-06)"
  - "estimateCreateCost never returns 0 for an unknown price; external strategies without capacity are unavailable; monthly omitted when the API monthly figure is null (D-05, OQ2)"
  - "requiresModPack is not enforced by the schema (OQ1); a one-line code comment records that this awaits user confirmation"
  - "Default size considers the storage fit only for IncludedDisk and is recomputed only while the size is untouched"
  - "An invalid explicit FixedExternal capacity is field validation and is excluded from the storage-misfit reason"
requirements-completed: [CRT-01, CRT-02, CRT-03, CRT-05]
duration: 12 min
completed: 2026-11-20
status: complete
actuals:
  tokens: 40000
  tasks: 3
  commits: 3
plan_head_before: 3f8978785865ba34e2df94fddc90c08ada89f42d
plan_head_after: 46fdb26f5ad755fc13a5c4c7e3d44e19cf4ce4b8
coverage:
  - deliverable: "Readiness rule returns the first blocking reason in UI-SPEC order"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-server.test.ts#create readiness"
        status: pass
  - deliverable: "Cost rule reports calculating, unavailable or known figures without inventing a number"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-server.test.ts#create cost estimate"
        status: pass
  - deliverable: "Touched-aware defaults, capability-driven validation and DOM field order"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-server.test.ts#create defaults"
        status: pass
  - deliverable: "Presets projected from configuration profiles and mod packs"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-presets.test.ts#create presets"
        status: pass
---

# Phase 64 Plan 02: Create Model Summary

Pure, React-free create model: readiness (first blocking reason, frozen owner as the only cannot-pay signal), a money rule that never invents an amount, touched-aware defaults, capability-driven validation, DOM field order, and a client-side preset projection of configuration profiles and mod packs.

## Accomplishments

- **Task 1 (tracer)**: `deriveCreateReadiness` with the nine-reason `createBlockReasonOrder`, `effectiveCostOwner`, `estimateCreateCost` (`calculating` / `unavailable` / `known`, `hoursPerMonth = 730`), and the shared fixture helper (`createProfile`, `createProvider`, `createCapability`, `counterStrikeCapability`, `createStorageDemand`, `createOwners`, `createContentOptions`). Commit `faa9cb80`.
- **Task 2**: `resolveCreateDefaults`, `defaultSizeIdentifier`, `isExternalCapacityEntryValid`, `isStorageMisfit`, the optional `ServerCreationRequirements` second parameter of `createServerCreationSchema` (required terrain and scenario and invalid fixed capacity as field errors via `superRefine`), `createFieldOrder`, `advancedCreateFields`, `invalidCreateFields`. Commit `e4a14b1c`.
- **Task 3**: `projectCreatePresets`, `presetSelectValue`, `presetSelection`, `defaultPresetValue`, `customPresetValue`; prototype-key values select nothing. Commit `46fdb26f`.

## Verification

- `npm test` (whole ReactApp suite): 127 files, 1325 tests passed. The create-server (27) and create-presets (5) cases pass; all pre-existing create-server cases are unchanged.
- `npm run typecheck` and `npm run lint` exit 0 (zero warnings).
- `prettier --check` is clean for every file this plan touched. `npm run format:check` as a whole reports 390 pre-existing files in this worktree (untouched files such as `fleetFixtures.ts` are listed; a line-ending checkout difference, out of scope).
- No `react` import in `createServerModel.ts`, no game identifier literal in it, and `sources/Common` has no diff against the plan base.
- All task acceptance greps pass (`deriveCreateReadiness`, `estimateCreateCost`, `"estimate-failed"`, `counterStrikeCapability`, `resolveCreateDefaults`, `isExternalCapacityEntryValid`, `createFieldOrder`, `superRefine`, `projectCreatePresets`, `presetSelection`, `__proto__` in the preset test).

## Deviations from Plan

None - plan executed exactly as written.

Notes: `npm ci` was run in the worktree because `node_modules` was missing (lockfile install, no package changes). The branch started on a non-descendant HEAD and was reset to the expected base `3f897878` per the worktree branch check.

## Assumptions to confirm

- OQ1: a mod pack is never required at create, even when `requiresModPack` is true (deliberate deviation from the UI-SPEC Mod pack row). The user should confirm.
- Callers must reset game-dependent content (terrain, scenario, mod pack, profile) when the game changes; `resolveCreateDefaults` only drops values that a loaded list lacks, because it cannot see a game change.

## Known Stubs

None.

## Threat Flags

None. T-64-04 mitigated (`presetSelection` uses `Array.find` on exact values; `__proto__`, `constructor`, `toString`, empty and unknown values are tested). T-64-05 accepted (frozen check is advisory; the API is the guard).

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/hosting/createServerModel.ts
- FOUND: sources/ReactApp/tests/helpers/createFixtures.ts
- FOUND: sources/ReactApp/tests/hosting/create-presets.test.ts
- FOUND commits: faa9cb80, e4a14b1c, 46fdb26f

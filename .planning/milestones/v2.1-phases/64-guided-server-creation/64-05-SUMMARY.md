---
phase: 64-guided-server-creation
plan: 05
subsystem: webapp
tags: [react, hosting, create-server, react-hook-form, capability-driven, storage]
status: complete
requires:
  - phase: 64-02
    provides: estimateCreateCost, isExternalCapacityEntryValid, createFixtures
  - phase: 64-03
    provides: SelectField, TextField, CheckboxField, Disclosure
provides:
  - "AdvancedOptions: collapsed disclosure with capability-driven fields and an always visible summary line"
  - "StorageOptions: strategy, external capacity and estimate facts"
  - "StorageDemandDetails: demand breakdown behind a nested Storage details disclosure"
  - "Game-literal source scan over the create files"
affects: [64-07, 64-08]
tech-stack:
  added: []
  patterns:
    - "Fields registered with form.register so form.setFocus reaches them; selects keyed on their option list so the form value is written into the DOM once the options arrive"
    - "One retry panel for all failed option lists, shown at the first failed field"
key-files:
  created:
    - sources/ReactApp/src/components/server/AdvancedOptions.tsx
    - sources/ReactApp/src/components/server/StorageOptions.tsx
    - sources/ReactApp/src/components/server/StorageDemandDetails.tsx
    - sources/ReactApp/src/components/server/create-options.css
    - sources/ReactApp/tests/hosting/create-capability-driven.test.tsx
    - sources/ReactApp/tests/hosting/create-storage.test.tsx
  modified: []
key-decisions:
  - "A mod pack is never marked required, even when requiresModPack is true (orchestrator decision OQ1, awaiting user confirmation); a code comment records it"
  - "Provider summary piece uses the provider identifier because RuntimeProvider carries no display name"
  - "The self cost-owner option keeps the carried Hosting_Create_CostOwnerSelf(Existing) copy and is not disabled when frozen, because a disabled empty option would make the browser select another owner in the DOM while the form value stays null; readiness (plan 64-02) blocks a frozen self owner instead"
  - "Selecting the explicit external strategy prefills the provider minimum capacity (old page behaviour); other strategies reset it to null"
commits: 2
plan_head_before: a1f0535226903fad254c3816110f20f2c1a898b0
plan_head_after: 36f54701
requirements-completed: [CRT-01, CRT-06]
duration: 35 min
completed: 2026-11-20
actuals:
  tokens: 18000
  tasks: 2
  commits: 2
coverage:
  - deliverable: "Same AdvancedOptions code renders different fields for Arma-shaped and Counter-Strike-shaped capabilities (CRT-06)"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-capability-driven.test.tsx#capability-driven advanced options"
        status: pass
  - deliverable: "No create file names a game"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-capability-driven.test.tsx#create files stay game-agnostic"
        status: pass
  - deliverable: "Storage strategy, capacity, estimate facts, fixed failure copy and the demand breakdown"
    human_judgment: false
    verification:
      - kind: test
        ref: "sources/ReactApp/tests/hosting/create-storage.test.tsx#storage options"
        status: pass
---

# Phase 64 Plan 05: Advanced Options and Storage Components Summary

Generic, capability-driven Advanced create options plus the storage configuration and demand breakdown moved out of the old page into reusable components, proven first by a test that renders two differently shaped capabilities with the same code and a source scan that finds no game identifier in the create files.

## Accomplishments

- **Task 1 (tracer)**: `AdvancedOptions` renders, in the UI-SPEC order, Provider (one provider as a read-only "Runs on" line, several as a select, unavailable ones listed with mapped copy), the storage slot, Mod pack (optional), Terrain and Scenario (only when the capability requires them, labelled by the capability's own `terrainLabel` and `scenarioLabel`), Configuration profile, Cost owner, Keep launch disabled and a nested collapsed "About this game". The closed disclosure always shows a wrapping summary line (provider, storage, mod pack, configuration, required content). The source scan covers `CreateServerPage.tsx`, `createServerModel.ts` and every create-prefixed file in `components/server`. Commit `83aab0fc`.
- **Task 2**: `StorageOptions` (external strategies only with external block storage support, capacity only for the explicit strategy with provider bounds, facts with the combined price from `estimateCreateCost` and never 0 for an unknown price, fixed failure copy with Try again, misfit warning) and `StorageDemandDetails` (the old demand summary behind a nested collapsed Storage details disclosure, locale-aware number helper, `create-storage-*` classes). Commit `36f54701`.

## Verification

- `npm test` (whole ReactApp suite): 132 files, 1415 tests passed, including the 16 capability-driven, 16 storage, style guard and parity-matrix tests.
- `typecheck` and `lint --max-warnings 0` exit 0; `prettier --check --end-of-line auto` is clean for `src/components/server` and `tests/hosting`.
- Acceptance greps hold: one `export function` each, no game literal and no `form.watch` in `AdvancedOptions.tsx`, `requiresTerrain` present, `estimateCreateCost` and a single `Hosting_Create_EstimateFallback` in `StorageOptions.tsx`.
- `git diff` against the plan base shows no change to `CreateServerPage.tsx` or `sources/Common/Ui/routes.json`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] node_modules missing and HEAD not a descendant of the expected base**
- `npm ci` from the lockfile (no package changes); the branch was reset to `a1f05352` per the worktree branch check.

**2. [Rule 1 - Bug avoided] Cost owner self option is not disabled when frozen**
- The old form disabled the empty option for a frozen self owner. With an uncontrolled registered select a disabled empty option lets the browser display another owner while the form value stays null. The frozen suffix marks it and the readiness rule blocks creation. Other frozen owners stay disabled.

**3. Selects keyed on their option list**
- Registered native selects are uncontrolled; a key derived from loading state and list length remounts them when the options arrive so react-hook-form writes the current value into the DOM.

Deferred: `npm run format:check` as a whole reports pre-existing CRLF checkout differences repo-wide in this worktree (out of scope); every touched file passes `prettier --check --end-of-line auto`.

## Assumptions to confirm

- OQ1: a mod pack is not required at create even when `requiresModPack` is true (deviation from the UI-SPEC Mod pack row).
- Plan 64-07 composes these components: it must pass the selected provider, size and demand to `StorageOptions`, key option state per game, and reset game-dependent values on a game change.

## Known Stubs

None.

## Threat Flags

None. T-64-10 mitigated (API strings render as React text or plain option labels, English API text inside `ApiText`, no `dangerouslySetInnerHTML`, raw error text never printed). T-64-11 mitigated on the client (capacity field bounds and the schema validation from plan 64-02); the API validates again.

## Self-Check: PASSED

- FOUND: AdvancedOptions.tsx, StorageOptions.tsx, StorageDemandDetails.tsx, create-options.css, create-capability-driven.test.tsx, create-storage.test.tsx
- FOUND commits: 83aab0fc, 36f54701

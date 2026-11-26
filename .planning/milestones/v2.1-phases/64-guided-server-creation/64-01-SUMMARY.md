---
phase: 64-guided-server-creation
plan: 01
subsystem: ui
tags: [react, react-router, i18n, cost-confirmation, hosting]
status: complete
requires:
  - phase: 61-cost-confirmation
    provides: CostConfirmationProvider and the shared start confirmation dialog
  - phase: 63-server-workspace
    provides: ServerWorkspacePage with the WorkspaceShell notice slot
provides:
  - createStartIntent router-state contract (createStartState, hasCreateStartIntent)
  - workspace consumption that opens the shared start confirmation with source create
  - CreateStartNotice for a created server whose start was not confirmed
  - every Server_Create_* string in English and German (77 keys)
affects: [64-02, 64-03, 64-04, 64-05, 64-06, 64-07, 64-08, 64-09]
tech-stack:
  added: []
  patterns:
    - "Router-state intent consumed once and replaced with null state"
    - "Deferred confirmation request (window.setTimeout 0) to outlive the provider pathname effect"
    - "Marker-split localized sentence to wrap an API-originated title in an element"
key-files:
  created:
    - sources/ReactApp/src/features/hosting/createStartIntent.ts
    - sources/ReactApp/src/components/server/CreateStartNotice.tsx
    - sources/ReactApp/src/components/server/create.css
    - sources/ReactApp/tests/hosting/create-start-flow.test.tsx
  modified:
    - sources/ReactApp/src/components/server/useCostConfirmation.ts
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/CreateServerPage.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts
key-decisions:
  - "Router-state key is createdStartPending; only an own property equal to the literal true counts, and the server id always comes from the route and the loaded server"
  - "The workspace issues the confirmation request from window.setTimeout(0) and guards on the server id an issued request was made for, so StrictMode opens exactly one dialog"
  - "The notice wraps the title via a marker split of the localized sentence, keeping the UI-SPEC text unchanged"
  - "Server_Create_Cost_StorageValue and Server_Create_Summary_Content are identical in both languages and allow-listed as interpolation-only format strings"
patterns-established:
  - "Create page never imports the confirmation hook; the workspace owns the request"
requirements-completed: [CRT-04]
duration: 40min
completed: 2026-11-20
actuals:
  tokens: 70000
  tasks: 3
  commits: 3
plan_head_before: 3f897878
plan_head_after: 56ba5a1db90667bb1d706a321f8e744b1817d1be
---

# Phase 64 Plan 01: Create-then-start tracer, not-started notice and Phase 64 copy Summary

**Creating a server with the start intent lands in the workspace and opens the shared Phase 61 start confirmation (source `create`), with a dismissible not-started notice, StrictMode and reload safety, and all 77 `Server_Create_*` strings in English and German.**

## Performance

- **Duration:** about 40 min
- **Tasks:** 3 of 3
- **Files:** 12 (4 created, 8 modified)

## Accomplishments

- Tracer proven end to end with the real `CostConfirmationProvider` in a two-route test, covering both the fetched-server and cached-server cases. A mutation check confirmed the cached case fails when the request is issued without the deferral (RESEARCH Pitfall 1).
- The workspace adopts the intent during render, replaces the history entry with `state: null` right after reading it, and requests the start only when the start action exists and is available. Keep off, a failed create, a reload and StrictMode were each covered by a test.
- `CreateStartNotice` (role status) renders in the shell notice slot while the derived state is `off`, shows the mapped unavailable reason when the start is unavailable, and is cleared by dismiss, by `onSubmitted`, or when the state leaves `off`.
- 77 `Server_Create_*` keys in both languages with equal interpolation names; plural summary renders in both languages; the button-label guard now covers the create buttons; the model test enumerates `create`.

## Task Commits

1. Task 1 (tracer): `300c2ad4` feat(webapp): open the start confirmation after creating a server
2. Task 2: `28109083` feat(webapp): explain a created server whose start was not confirmed
3. Task 3: `56ba5a1d` feat(webapp): add the guided server creation copy in English and German

## Verification

- `tests/hosting`, `tests/locales`, `tests/styles`: 69 files, 1022 tests passed.
- `locales:check` (3380 keys per language, 0 failures, 0 warnings), `typecheck` and `lint --max-warnings 0` exit 0.
- `sources/Common/Ui/routes.json` unchanged; no file under `sources/WebApp`, `sources/Api` or `src/generated` touched.
- `grep -c useCostConfirmation CreateServerPage.tsx` is 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Dependencies installed in the fresh worktree**
- **Found during:** Task 1 verification setup
- **Issue:** `sources/ReactApp/node_modules` was missing in the worktree.
- **Fix:** `npm ci` from the committed lockfile (no new packages, nothing added to package files).

**2. [Rule 1 - Bug in test design] Confirm-start test targets the create request**
- **Found during:** Task 2
- **Issue:** `onSubmitted` belongs to the create-issued request. Confirming through the workspace primary button leaves the notice (the server mock stays off), so the plan's "confirming clears the notice" behavior is asserted by confirming the dialog that the create opened.
- **Files modified:** `sources/ReactApp/tests/hosting/create-start-flow.test.tsx`

**3. [Rule 3 - Blocking] `identical-allowed.json` extended**
- **Found during:** Task 3
- **Issue:** `Server_Create_Summary_Content` is identical in both languages as specified, which `locales:check` warns about alongside the planned `Server_Create_Cost_StorageValue`.
- **Fix:** both keys allow-listed with the reason "interpolation-only format string".

### Notes

- The create-page tests need `window.confirm` mocked to true because the unsaved-changes guard prompts when the dirty form navigates; the new flow test does so in `beforeEach`.
- `format:check` fails repo-wide in this Windows worktree only because of `core.autocrlf` CRLF checkouts; `prettier --check --end-of-line auto src tests` passes.
- The plan's Task 1 text says the workspace notice arrives in Task 2; the notice slot is wrapped in a fragment from Task 1 on so Task 2 only adds an element.

## Known Stubs

None. The interim "Create server and start" control on the old create page is intentional and is replaced by plan 64-07.

## Threat Flags

None. T-64-01 to T-64-03 mitigations are implemented as planned (literal own-property check, state replaced after read, title and reasons rendered as React text only, start still needs explicit confirmation).

## Issues Encountered

None beyond the deviations above.

## Next Phase Readiness

Plans 64-02 to 64-09 can rely on stable `Server_Create_*` keys, the `create` confirmation source, the `createStartState` contract and `create.css`. Plan 64-07 must navigate with `createStartState` and re-point the integration test at the After creating choice.

## Self-Check: PASSED

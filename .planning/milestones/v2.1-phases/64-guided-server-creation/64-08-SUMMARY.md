---
phase: 64-guided-server-creation
plan: 08
subsystem: ui
tags: [react, react-hook-form, hosting, create-server, presets, accessibility, live-region]
status: complete
requires:
  - phase: 64-06
    provides: PresetField
  - phase: 64-07
    provides: guided CreateServerPage with touched, afterCreating and advancedOpen state
provides:
  - Presets through the page (profile or mod pack, always Keep it off, announced)
  - Focus on the first invalid field in DOM order, opening Advanced first
  - Auto-open of Advanced for missing required content or misfit storage
  - Busy and inert pending state, focused request error with owner refetch
  - One polite live region for preset, game and debounced estimate announcements
affects: [64-09]
requirements-completed: [CRT-01, CRT-03, CRT-05, CRT-06]
commits: 3
plan_head_before: 8350f59a2016010fcc648ea14c87c52f99891ccc
plan_head_after: 66405eba909f7dc8a8698e0e90e936822e630683
actuals:
  tokens: 45000
  tasks: 3
  commits: 3
key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/CreateServerPage.tsx
    - sources/ReactApp/src/components/server/create.css
    - sources/ReactApp/tests/hosting/create-server-page.test.tsx
    - sources/ReactApp/tests/hosting/create-capability-driven.test.tsx
key-decisions:
  - "A preset choice writes the two form values with shouldDirty and shouldValidate, marks both touched so the defaults never overwrite them, sets After creating to Keep it off and announces it; the select value is always derived from the form (presetSelectValue), so Advanced edits read Customized"
  - "forceAdvanced (storage misfit or required terrain/scenario without a value once its list has loaded) opens Advanced through the guarded adjust-state-while-rendering pattern only when it flips from false to true, so the member can close it again"
  - "A useRef flag guards the create request in addition to create.isPending, so two submits before the next render cannot create two servers"
  - "Estimate announcements keep the last announced hourly figure in a ref; the first known figure is only recorded and a changed figure is announced from a 600ms timer that the effect cleanup cancels"
  - "Tests that need fake timers fake only setTimeout and clearTimeout and fire DOM events with fireEvent, because testing-library detects only Jest fake timers and its user-event wrapper would wait forever on a faked setTimeout"
coverage:
  - id: D1
    description: "Presets set profile or mod pack, keep the server off, are announced, never block and reach the created server"
    requirement: "CRT-05"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#presets"
        status: pass
    human_judgment: false
  - id: D2
    description: "Invalid submit focuses the first invalid field in DOM order and opens Advanced for hidden fields; Advanced opens itself for missing required content"
    requirement: "CRT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#invalid submit and Advanced auto-open"
        status: pass
    human_judgment: false
  - id: D3
    description: "The same page renders different fields per capability; requiresModPack true still creates with a null mod pack"
    requirement: "CRT-06"
    verification:
      - kind: unit
        ref: "tests/hosting/create-capability-driven.test.tsx#capability-driven create page"
        status: pass
    human_judgment: false
  - id: D4
    description: "Pending is busy and inert, a failed create shows a focused fixed error and refetches the owners, announcements fire once"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#pending and request failure"
        status: pass
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#announcements"
        status: pass
    human_judgment: false
---

# Phase 64 Plan 08: Presets, Validation Focus and Create States Summary

The guided create page is complete: presets are a shortcut over the two existing form values that always keep the server off, nothing required stays hidden in the collapsed disclosure, and creation shows progress, failure and changes plainly.

## Accomplishments

- **Task 1 (tracer)**: Composed `PresetField` between the game and the size, fed by `projectCreatePresets` and `presetSelectValue`. Choosing a preset sets the configuration profile and mod pack, marks both touched, forces Keep it off and writes "Preset applied: ..." into the new always-mounted polite live region. A failed preset list shows the options error with Try again and never blocks the create. Commit `236521ae`.
- **Task 2**: `handleSubmit` now gets an `onInvalid` that focuses the first invalid field in DOM order, opening Advanced inside `flushSync` first when the field lives there. Advanced opens by itself when the storage does not fit or a required terrain or scenario has no value, and an effect triggers validation for those fields so their errors show. Page-level capability cases (Arma-shaped, Counter-Strike-shaped, `requiresModPack: true`) were added. Commit `bcf2d7ac`.
- **Task 3**: The main column is `inert` and the form `aria-busy` while creating; a failed create shows the fixed error in a focused alert, keeps the form editable and refetches the cost owners. Game changes announce "Options reset for {game}." and a changed hourly figure announces once, 600ms after the last change; the first figure is not announced. A dimmed cue for the inert column was added to `create.css`. Commit `66405eba`.

## Verification

- `tests/hosting tests/locales tests/styles`: 75 files, 1191 tests passed.
- `typecheck`, `lint --max-warnings 0` and `locales:check` exit 0; `prettier --check --end-of-line auto` passes for all four touched files (repo-wide `format:check` fails in this CRLF checkout for pre-existing files, as noted in 64-07).
- `sources/Common/Ui/routes.json` has no diff; no commit touches `sources/WebApp`, `sources/Api` or generated code.
- Acceptance greps all found: `<PresetField`, `presetSelectValue`, `Server_Create_Preset_Announce`, `aria-live="polite"`, `advancedCreateFields`, `invalidCreateFields`, `inert={create.isPending}`, `Server_Create_Cost_Announce`, `Server_Create_Game_Announce`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing advanced-options test became ambiguous**
- **Found during:** Task 1
- **Issue:** the preset select repeats the "Operations" and "Public operation (revision 3)" options, so `findByRole("option", ...)` in the "sends every Advanced choice" test matched twice.
- **Fix:** the test now scopes the option lookups to the Mod pack and Configuration profile selects.
- **Files modified:** `sources/ReactApp/tests/hosting/create-server-page.test.tsx`
- **Commit:** `236521ae`

**2. [Rule 2 - Missing critical functionality] Double-submit guard beyond `isPending`**
- **Found during:** Task 3
- **Issue:** `handleSubmit` validates asynchronously, so two submits can both pass the `isPending` check before the mutation state renders.
- **Fix:** a `creating` ref is set synchronously in the submit handler and cleared in `finally`.
- **Commit:** `66405eba`

**3. [Plan wording] Fake timers without `userEvent.setup({ advanceTimers })`**
- The plan names `userEvent.setup({ advanceTimers })`. Testing-library's async wrapper waits on a real-looking `setTimeout(0)` unless Jest fake timers are detected, so with Vitest fake timers every user-event call hung. The two debounce tests fake only `setTimeout` and `clearTimeout` after the page is ready and use `fireEvent` for the change events.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. T-64-16 mitigated (a rejected create shows the fixed error, focuses it and refetches the owners; tests reject with a secret-bearing error and assert it is not rendered). T-64-17 mitigated (`noValidate`, DOM-order focus that opens Advanced first, and auto-open for missing required content).

## Notes for plan 64-09

- Assumption carried from 64-02 and 64-07 (OQ1): a mod pack is never required at create; the new page test proves a `requiresModPack: true` capability creates with a null mod pack. The user should confirm.
- The plan-commit ledger file could not be written under the shared git path in this worktree, so the base SHA is recorded in the frontmatter (`plan_head_before`).

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/hosting/CreateServerPage.tsx
- FOUND: sources/ReactApp/src/components/server/create.css
- FOUND: sources/ReactApp/tests/hosting/create-server-page.test.tsx
- FOUND: sources/ReactApp/tests/hosting/create-capability-driven.test.tsx
- FOUND commits: 236521ae, bcf2d7ac, 66405eba

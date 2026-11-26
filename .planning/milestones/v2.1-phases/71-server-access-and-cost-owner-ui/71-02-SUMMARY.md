---
phase: 71-server-access-and-cost-owner-ui
plan: 02
subsystem: ui
tags: [react, combobox, aria, accessibility, design-tokens, vitest]

requires:
  - phase: 69-server-ui-foundation
    provides: TextField, ActionButton, token-only CSS guard, Tier A literal scan, contrast guard
  - phase: 70-form-controls
    provides: --utf-hover layer, compact control height, shared form-field styles
provides:
  - SearchPicker shared combobox primitive with an inline listbox (PickerOption, SearchPickerTexts, SearchPickerProps)
  - useDebouncedValue hook for the two pickers that query the API
  - --utf-listbox-max-height token (16rem)
  - Guard coverage: Tier A literal scan, token-only style guard, contrast cases
affects: [71-05 server access panel, 71-07 cost owner panel, later profile access migration (D-20)]

actuals:
  tokens: 9480
  tasks: 2
  commits: 2
plan_head_before: 6b56ecb80f069080c1cc2eec19ce1bef13cb0901
plan_head_after: 5db7c53d96bd78dd04baf52f6b58bd6fe4cdaf18

tech-stack:
  added: []
  patterns:
    - "Active option held as an id, not an index, so the active option survives option list changes without an effect"
    - "Focus requests as a serial-numbered state value consumed by an effect that only reads refs (lint safe)"

key-files:
  created:
    - sources/ReactApp/src/components/SearchPicker.tsx
    - sources/ReactApp/src/components/search-picker.css
    - sources/ReactApp/src/app/useDebouncedValue.ts
    - sources/ReactApp/tests/search-picker.test.tsx
    - sources/ReactApp/tests/app/debounced-value.test.tsx
  modified:
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/ratchets/rules.json
    - sources/ReactApp/tests/styles/server-style-guard.test.ts
    - sources/ReactApp/tests/styles/tokens-contrast.test.ts
    - sources/ReactApp/tests/styles/legacy-ratchet.test.ts

key-decisions:
  - "SearchPickerTexts.results is a function (count) so the caller's i18next plural selects the form, instead of the UI-SPEC resultsOne/resultsOther pair"
  - "The active option fill is the shared --utf-hover layer as a background image, not color-mix, because the style guard rejects colour functions"
  - "Enter retries while the error row shows, because the retry button inside the listbox is unreachable by Tab (Tab closes the listbox)"
  - "After a choice, focus moves to the token clear control so focus never rests on the removed input"

patterns-established:
  - "SearchPicker: every string is a required prop; one listbox, state rows are role none, never options"

requirements-completed: [D-10, D-18, D-19, D-20]

coverage:
  - id: D1
    description: "SearchPicker is an ARIA 1.2 combobox with list autocomplete, wrapping arrow keys, Home and End, Enter that never submits while an option is active, Escape, Tab and pointerdown selection that keeps focus"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/search-picker.test.tsx#SearchPicker combobox contract"
        status: pass
    human_judgment: false
  - id: D2
    description: "Chosen token replaces the input, clear returns focus to the input; loading, error with retry, no-match and idle states and the polite live region"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/search-picker.test.tsx#SearchPicker chosen token and SearchPicker states"
        status: pass
    human_judgment: false
  - id: D3
    description: "useDebouncedValue returns the new value only after the delay and restarts on each change"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/app/debounced-value.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "Picker styles are token-only, the listbox height is a token, colour pairs pass the contrast guard and no dependency was added"
    requirement: "D-19"
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps search-picker.css free of raw colours, pixel literals and viewport type"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#search picker contrast"
        status: pass
    human_judgment: false
  - id: D5
    description: "How the picker looks and behaves in a real browser at the three viewports (geometry of the token versus the input, listbox scroll, forced colors)"
    verification: []
    human_judgment: true
    rationale: "No consumer exists until 71-05 and 71-07; the visual check belongs to the Playwright screens S-09 and S-10 of those plans"

duration: 30min
completed: 2026-11-26
status: complete
---

# Phase 71 Plan 02: SearchPicker Summary

**SearchPicker, a token-styled ARIA 1.2 combobox with an inline listbox built on TextField and ActionButton, plus the useDebouncedValue hook, the listbox height token and guard coverage; no dependency added.**

## Performance

- **Duration:** about 30 min
- **Tasks:** 2 (tracer, auto)
- **Files modified:** 10 (5 created, 5 modified)

## Accomplishments

- `SearchPicker` (`src/components/SearchPicker.tsx`): the input is a shared `TextField` with `role="combobox"`, `aria-autocomplete="list"`, `aria-expanded` (true only while the listbox shows), `aria-controls`, `aria-activedescendant`, `aria-busy` while loading, `autocomplete="off"` and `spellcheck="false"`; focus never leaves the input. ArrowDown/ArrowUp wrap and skip disabled options, Home/End jump while open, Enter chooses the active option with the default prevented, the first Escape closes and the second clears, Tab closes without choosing. Options are chosen on `pointerdown` with the default prevented.
- Chosen token with the same border box as the input and an icon-only clear control (`Dismiss16Regular`) that returns focus to the input.
- State rows (loading with previous options kept, error with a real retry, no match), the idle hint under the input and a visually hidden polite live region that announces the count or the no-match text only for a settled (`ready`) query.
- `useDebouncedValue` (state set inside the timer callback, cleared in cleanup) for the two pickers.
- `search-picker.css`, token only, with the `--utf-listbox-max-height` token (16rem), 44px touch targets and a `forced-colors` block.
- Guards: `SearchPicker.tsx` is in `tierAPaths` (not `primitiveFiles`), `search-picker.css` is in the style guard, the token is in the contract list, three contrast cases were added.
- 35 unit tests for the picker, the hook and the ratchets; no new locale key (every string is a prop); `package.json` and `package-lock.json` unchanged.

## Task Commits

1. **Task 1: Tracer, SearchPicker from typing to a chosen token, with the debounce hook** - `8d86c11b` (feat)
2. **Task 2: Picker styles, the listbox height token and the guards** - `5db7c53d` (feat)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] color-mix is rejected by the style guard**
- **Found during:** Task 2
- **Issue:** The plan asks for the active-option fill `color-mix(in srgb, var(--utf-text) 8%, var(--utf-surface-overlay))`. `findStyleViolations` flags every colour function, including `color-mix` (a planted color-mix test exists), so the sheet could not pass the style guard.
- **Fix:** The fill is the existing `--utf-hover` layer (ink at 8%) painted as `background-image: linear-gradient(var(--utf-hover), var(--utf-hover))`, the same value and the same technique every shared control uses. The contrast cases composite `--utf-hover` over the overlay and the raised surface.
- **Files modified:** `sources/ReactApp/src/components/search-picker.css`, `sources/ReactApp/tests/styles/tokens-contrast.test.ts`
- **Commit:** `5db7c53d`

**2. [Rule 3 - Blocking] Legacy stylesheet ratchet required an entry for the new sheet**
- **Found during:** Task 2
- **Issue:** `tests/styles/legacy-ratchet.test.ts` lists every `src/**/*.css` that is not a guarded sheet and failed on `components/search-picker.css`. The plan expected no ratchet change.
- **Fix:** Added `components/search-picker.css` to `isGuarded` (it is scanned by `server-style-guard.test.ts` like `action-control.css`), instead of recording a legacy ceiling for a token-only sheet.
- **Files modified:** `sources/ReactApp/tests/styles/legacy-ratchet.test.ts`
- **Commit:** `5db7c53d`

**3. [Rule 2 - Missing critical functionality] Keyboard path for retry, error announcement and focus after a choice**
- **Found during:** Task 1
- **Issue:** Tab closes the listbox, so the retry button inside the error row cannot be reached by keyboard; the error text was not announced; and choosing an option unmounts the focused input, leaving focus on the body.
- **Fix:** Enter retries while the error row shows and no option is active; the error text is a `role="alert"` span; after a choice focus moves to the token clear control. An optional `inputRef` prop (like `SearchField`) lets a parent focus the input after a parent-driven clear, as the Give access flow requires.
- **Files modified:** `sources/ReactApp/src/components/SearchPicker.tsx`, `sources/ReactApp/tests/search-picker.test.tsx`
- **Commit:** `8d86c11b`

**4. [Rule 1 - Bug] ESLint `no-undef` for `beforeEach` in a test file**
- **Fix:** Imported `afterEach` and `beforeEach` from `vitest` in `tests/app/debounced-value.test.tsx`.
- **Commit:** `8d86c11b`

## Issues Encountered

None beyond the deviations above. `SearchPickerTexts.results(count)` replaces the UI-SPEC `resultsOne`/`resultsOther` pair on purpose (stated in the plan).

## Known Stubs

None.

## Threat Flags

None. T-71-06 is mitigated: labels and details render as React text inside `bdi`, there is no `innerHTML`. T-71-SC accepted: no package was installed.

## Cross-plan findings

- **XPF-71-02-1:** The clear control of the chosen token is 44px wide at 800px and below but 42px high (the token border consumes 2px of the 44px touch height, and the guard forbids a pixel literal to compensate). It meets the 24px AA target size; 71-05 and 71-07 UI review may accept it or change the token geometry.
- **XPF-71-02-2:** `SearchPicker` derives its `idle` state from the caller. Callers must compute `status` from the immediate query text, not a debounced copy, or the listbox waits for the debounce before opening; this is documented in the `status` prop JSDoc and matters for 71-05 and 71-07.

## Verification

- `npx vitest run tests/search-picker.test.tsx tests/app/debounced-value.test.tsx tests/styles tests/ratchets`: 8 files, 187 passed, 2 skipped (pre-existing skips).
- `npx vitest run --maxWorkers=4` (full suite): 217 files, 3583 passed, 2 skipped.
- `npm run lint`, `npm run typecheck`, `locales:check` (0 failures), `locales:unused` (46 pre-existing warnings, none for this plan) and prettier (`--end-of-line auto`) pass.
- Acceptance greps: `SearchPicker.tsx` in `rules.json` 1, raw `input|button|select|datalist` 0, `role="combobox"` 1, `export function useDebouncedValue` 1; `package.json` and `package-lock.json` untouched.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/components/SearchPicker.tsx
- FOUND: sources/ReactApp/src/components/search-picker.css
- FOUND: sources/ReactApp/src/app/useDebouncedValue.ts
- FOUND: sources/ReactApp/tests/search-picker.test.tsx
- FOUND: sources/ReactApp/tests/app/debounced-value.test.tsx
- FOUND commit 8d86c11b, FOUND commit 5db7c53d

---
phase: 70-ui-polishing
plan: 05
subsystem: ui
tags: [react, forms, accessibility, css-tokens, forced-colors]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-01 tokens (check size, switch dimensions, chip and card minimums, hover layer), 70-03 Disclosure in form-fields.css"
provides:
  - "Project-drawn CheckboxField and RadioGroupField marks over native inputs"
  - "SwitchField, choice cards, segmented bar, chip variant, labelEnd slot, designed select surface"
  - "Style guard rules for accent-color and appearance auto"
affects: [70-06, 70-07, 70-08, 70-09, 70-10, 70-11, 70-12, 70-13, 70-14]

actuals:
  tokens: 14000
  tasks: 3
  commits: 4
plan_head_before: 95539d14bdea4265715a2f24ce6b7b8d8791582f
plan_head_after: 5c0e91e29906b67c233e91e15ed7b3a3fda270db

tech-stack:
  added: []
  patterns:
    - "Invisible native input overlaid on a positioned label row, aria-hidden drawn mark as a sibling, focus ring on an ancestor of the input"
    - "Additive optional prop with unchanged markup when absent (labelEnd, variant, layout)"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/src/features/hosting/ConfigurationProfileAccessSection.tsx
    - sources/ReactApp/tests/form-fields.test.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/helpers/cssGuard.ts
    - sources/ReactApp/tests/styles/server-style-guard.test.ts
    - sources/ReactApp/tests/e2e/hosting-automation.spec.ts
    - sources/ReactApp/tests/admin-media-pages.test.tsx

key-decisions:
  - "The .check-mark wrapper is deliberately not positioned so the overlaid input resolves against the positioned label and covers the whole row or card (C-07)"
  - "The switch thumb inset subtracts the 1px border as 0.0625rem inside calc() so both end gaps are equal without a pixel literal"
  - "Segmented and chip hover use the --utf-hover background-image layer; the 520px card stack is the max-width override of the multi-column default because the guard allows max-width breakpoints only"
  - "Both switch state words share one grid cell and the inactive word is hidden by visibility, so the row never changes width"

patterns-established:
  - "Ring ownership: .check-mark (box), .choice-card, .segmented__option, .toggle-chip, .switch-control, each matched with :has(.check-mark__input:focus-visible)"

requirements-completed: [D-01, D-02, D-03, D-04, D-05, D-06, D-17]

duration: 40min
completed: 2026-11-26
---

# Phase 70 Plan 05: Drawn form controls Summary

Native checkbox, radio, switch, chip and segment inputs now stay in the accessibility tree under project-drawn marks, with a designed select surface and a `labelEnd` slot every later plan composes.

## Accomplishments

- Task 1 (tracer): `CheckboxField` and `RadioGroupField` render `label > span.check-mark > (input.check-mark__input + span.check-mark__box[aria-hidden])`. The input is invisible and overlays the whole row. `accent-color` is gone. The S-06 and S-03 axe audits (en and de) pass on the new structure, which settles assumption A7 (`aria-hidden-focus`, `target-size` and the ring probe accept it).
- Task 2: `RadioGroupField` renders choice cards by default (`data-layout`, `data-columns` for two or three options) and a segmented bar with `layout="segmented"`; `hideLegend` keeps the legend as `sr-only`. New `SwitchField` (native checkbox with `role="switch"`, required `onLabel` and `offLabel`, no authored `aria-checked`). `CheckboxField variant="chip"`. Forced-colors rules and 800px touch heights. The profile access subject choice is segmented.
- Task 3: designed closed select surface (raised fill, control border, hover layer), a `@supports (appearance: base-select)` token-styled popup, `color-scheme: dark` on date and time controls, `labelEnd` on `TextField`, `TextAreaField`, `SelectField` and `SwitchField` through `div.form-field__head`, and guard rules rejecting `accent-color` and `appearance: auto`.

## Verification

- `tests/form-fields.test.tsx`, `tests/styles`, `tests/hosting/configuration-profiles.test.tsx`, `tests/hosting/create-choices.test.tsx`: pass. `lint` and `typecheck` clean.
- Playwright `a11y-audit.spec.ts` S-06 and S-03 (en, de): 4 passed after each task. `hosting-automation.spec.ts` and `control-target-size.spec.ts`: 19 passed, 13 skipped (screenshot-only).
- Full vitest run: 3394 passed, 3 failed. Two were 5s timeouts under parallel-worktree load (`activity-section`, `workspace-page`, plus one more `workspace-page` case); they pass when their files are re-run alone. The third was a real fallout, fixed below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Keyboard-walk ring assertion read the old row-wide ring**
- **Found during:** Task 1
- **Issue:** `tests/e2e/hosting-automation.spec.ts` measured the outline on `.form-field__check` or `.form-field__radio-row`; the ring now sits on the mark wrapper, the card, the chip or the segment, so the assertion would fail.
- **Fix:** the test walks from the invisible input to the first ancestor that draws an outline. The test passes.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-automation.spec.ts`
- **Commit:** e571da3b

**2. [Rule 1 - Bug] jest-dom `toBeVisible` rejects the invisible native input**
- **Found during:** full vitest run after Task 3
- **Issue:** `tests/admin-media-pages.test.tsx` asserted `toBeVisible()` on the Featured checkbox, which now has `opacity: 0` by design.
- **Fix:** assert the input is in the document and its row text is visible.
- **Files modified:** `sources/ReactApp/tests/admin-media-pages.test.tsx`
- **Commit:** 5c0e91e2

**3. [Plan reading] Hover tint and 520px stack**
- The plan's hover uses the `--utf-hover` layer (no `color-mix`), and "above 520px" is implemented as a multi-column default with a `max-width: 520px` stack override, because the guard allows `max-width` breakpoints only. No behavior difference.

## Authentication Gates

None.

## Known Stubs

None.

## Deferred Issues

- Deferred follow-up: real-device look of the `appearance: base-select` popup, the visual look of switch, cards, segments and chips (no screen consumes `SwitchField`, the chip variant or `labelEnd` yet, so only unit tests and the stylesheet guard exercise them), screen reader pass and VER-03 baselines are milestone-end human items.
- The "one literal allowed beside the breakpoints" for the card group is the `--utf-choice-card-min` token, already defined by 70-01.

## Threat Flags

None. No new network, auth or schema surface. T-70-06 (labels stay React text nodes, no `dangerouslySetInnerHTML`) and T-70-07 (native `disabled` stays on the input; unit tested for checkbox, chip, switch and card) are mitigated.

## Self-Check: PASSED

- FOUND: `export function SwitchField` in `sources/ReactApp/src/components/FormFields.tsx`
- FOUND: `check-mark__box`, `toggle-chip`, `forced-colors`, `base-select` in `form-fields.css`; `accent-color` count 0 there
- FOUND: `Checkmark16Filled` in `tests/mocks/react-icons.tsx`; `accent-color` rule in `tests/helpers/cssGuard.ts`
- FOUND: `layout="segmented"` in `ConfigurationProfileAccessSection.tsx`
- FOUND commits: e571da3b, 48c2f9ad, a71b4c82, 5c0e91e2

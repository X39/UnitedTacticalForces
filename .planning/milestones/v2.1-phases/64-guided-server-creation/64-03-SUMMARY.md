---
phase: 64-guided-server-creation
plan: 03
subsystem: ui
tags: [react, react-hook-form, accessibility, forms, disclosure, css-tokens]
status: complete

requires:
  - phase: 62-fleet-overview
    provides: tokens.css layout tokens and the server style guard that covers components/server CSS
provides:
  - TextField, SelectField, CheckboxField and RadioGroupField shared labelled controls
  - Controlled native Disclosure with an always-visible summary line
  - Token-only form-field and disclosure styles
  - --utf-create-summary-width and --utf-create-bar-reserve layout tokens
affects: [64-guided-server-creation, 65-server-change-forms]

commits: 3
plan_head_before: 3f8978785865ba34e2df94fddc90c08ada89f42d
plan_head_after: 22531d12e0892a64bad2c0c1d70f08ee10ceaf86

actuals:
  tokens: 9000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Shared controls own form-field / disclosure class names, never the global .field class"
    - "Props types based on ComponentPropsWithRef so react-hook-form register spreads and setFocus work (React 19 ref as prop)"

key-files:
  created:
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/src/components/Disclosure.tsx
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/tests/form-fields.test.tsx
    - sources/ReactApp/tests/disclosure.test.tsx
  modified:
    - sources/ReactApp/src/styles/tokens.css

key-decisions:
  - "Hint and error ids derive from the control id (`<id>-hint`, `<id>-error`); a caller aria-describedby is appended after them"
  - "Radio help text lives inside the wrapping label so the whole row is the target; the radio's accessible name therefore includes its help text"
  - "Chevron rotation only inside prefers-reduced-motion: no-preference, timed by --utf-motion-base"

patterns-established:
  - "Generic controls take all text as props; only a decorative aria-hidden asterisk is rendered by the component"

requirements-completed: [CRT-01]

coverage:
  - id: D1
    description: "TextField and SelectField give a visible label, linked hint and error, aria-invalid only with an error, role alert error, and bind to react-hook-form including setFocus"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/form-fields.test.tsx#TextField and SelectField"
        status: pass
    human_judgment: false
  - id: D2
    description: "CheckboxField and RadioGroupField with full-row targets, help text wiring and a visible reason linked to disabled radios"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/form-fields.test.tsx#CheckboxField and RadioGroupField"
        status: pass
    human_judgment: false
  - id: D3
    description: "Controlled native Disclosure with aria-expanded, aria-controls and a summary line, plus the two create layout tokens"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/disclosure.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/styles (server style guard and tokens contrast)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-20
---

# Phase 64 Plan 03: Shared Form Controls and Disclosure Summary

Accessible, token-styled TextField, SelectField, CheckboxField, RadioGroupField and a controlled native Disclosure for the guided create page, all bindable to react-hook-form.

## Accomplishments

- Tracer (Task 1): TextField and SelectField with a visible bound label, hint/error linked in `aria-describedby` (hint first), `aria-invalid` only with an error, `role="alert"` error with an aria-hidden icon, native optgroup support, and `register` / `setFocus` compatibility. Verified end to end before expanding.
- Task 2: CheckboxField (label wraps the input, full-row target) and RadioGroupField (fieldset and legend that may hold a heading, per-option help text, disabled options stay visible, one visible reason linked to every disabled radio).
- Task 3: Disclosure on native `details` with `aria-expanded`, `aria-controls`, title as accessible name and summary line as description; `--utf-create-summary-width: 22rem` and `--utf-create-bar-reserve: 12rem` tokens.

## Verification

- `npm --prefix sources/ReactApp test -- tests/disclosure.test.tsx tests/form-fields.test.tsx tests/styles`: 4 files, 51 tests passed.
- `typecheck` and `lint` (max-warnings 0): clean.
- `grep -c 'className="field"' FormFields.tsx` prints 0; `sources/Common/Ui/routes.json` unchanged.
- Prettier check passes for all six touched files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] node_modules missing in the fresh worktree**
- **Found during:** Task 1
- **Fix:** ran `npm --prefix sources/ReactApp ci` (lockfile install, no new packages).

**2. Disclosure and checkbox/radio CSS landed in the Task 1 commit**
- The full `form-fields.css` was written once, so Task 1's commit already contains the checkbox, radio and disclosure rules that Tasks 2 and 3 use. Tests and behavior are unaffected.

**3. Test adjustments**
- `form.setFocus` focuses inside a `setTimeout`, so the focus test uses `waitFor`.
- The Fluent icon mock ignores `className`, and jsdom does not expose `summary` as a button role, so the disclosure tests locate the summary and chevron structurally.

### Deferred Issues

- `npm run format:check` over the whole project reports 391 files (all pre-existing files, CRLF working-copy line endings in this checkout). This is unrelated to this plan; every file touched here passes `prettier --check`. Logged as out of scope.

## Known Stubs

None.

## Threat Flags

None. All caller values render as React children or attribute values; no `dangerouslySetInnerHTML` (T-64-06 mitigated).

## Self-Check: PASSED

- FOUND: FormFields.tsx, Disclosure.tsx, form-fields.css, form-fields.test.tsx, disclosure.test.tsx
- FOUND commits: abe93115, 58fd1f45, 22531d12

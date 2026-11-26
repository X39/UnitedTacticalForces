---
phase: 69-consistency-accessibility-verification
plan: 02
subsystem: ui
tags: [react, accessibility, wai-aria, disclosure, select, form-fields, vitest, playwright]

requires:
  - phase: 68-react-app-sections
    provides: Disclosure primitive, shared FormFields and the create page Advanced options
provides:
  - Button disclosure primitive (WAI-ARIA) with an unchanged public API
  - Shared padded chevron on every SelectField
  - aria-required forwarding on TextField, TextAreaField and SelectField
affects: [69-wave-2-family-plans, hosting-create, hosting-change, server-configuration]

actuals:
  tokens: 8300
  tasks: 2
  commits: 2
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: 8caf602c2ce61c579ea5ee1f8801b7ff8b7111d5

tech-stack:
  added: []
  patterns:
    - "Disclosure is div.disclosure[data-open] > button.disclosure__summary + div[role=region][hidden]"
    - "SelectField wraps the native select in .form-field__select with one aria-hidden chevron in the end padding"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/Disclosure.tsx
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/tests/disclosure.test.tsx
    - sources/ReactApp/tests/form-fields.test.tsx
    - sources/ReactApp/tests/hosting/create-capability-driven.test.tsx
    - sources/ReactApp/tests/hosting/create-server-page.test.tsx
    - sources/ReactApp/tests/hosting/create-storage.test.tsx
    - sources/ReactApp/tests/e2e/hosting-create.spec.ts
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts

key-decisions:
  - "Chevron drawn by a decorative aria-hidden wrapper element instead of a background image, because a background image would need a raw colour literal that the style guard forbids"
  - "Chevron centred with inset-block 0 and margin-block auto so no raw transform literal is needed"
  - "A caller supplied aria-required wins over the value derived from required"

patterns-established:
  - "Open state of a Disclosure is read from aria-expanded on its button in tests and e2e, never from a native open attribute"

requirements-completed: [FND-06, CON-03]

coverage:
  - id: D1
    description: "Disclosure renders a button with aria-expanded and aria-controls, an aria-hidden chevron and a labelled region, with no native disclosure element or marker triangle"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/disclosure.test.tsx"
        status: pass
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-create.spec.ts#advanced options open"
        status: pass
    human_judgment: false
  - id: D2
    description: "Disclosure toggles with Enter and Space and keeps its public API"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/disclosure.test.tsx#toggles with Enter and Space through the native button"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every SelectField shows the shared padded chevron (appearance none, end padding --utf-space-xl, control height token)"
    requirement: "CON-03"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/form-fields.test.tsx#SelectField"
        status: pass
    human_judgment: true
    rationale: "Computed styles and the stylesheet rules are asserted, but how the chevron looks beside long option text in a real browser needs an eye"
  - id: D4
    description: "Required converted form fields expose aria-required"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/form-fields.test.tsx#required fields"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 02: Button Disclosure and Padded Select Chevron Summary

**WAI-ARIA button disclosure with an unchanged Disclosure API, one padded decorative chevron on every SelectField, and aria-required on required text, textarea and select controls.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files modified:** 10

## Accomplishments

- `Disclosure` is now `div.disclosure[data-open]` holding a native `button type="button"` (`aria-expanded`, `aria-controls`, named by the title, described by the summary line) and a `div role="region"` labelled by the title that carries `hidden` while closed. No `details`, no `summary`, no marker rule. Enter and Space come from the native button.
- The webkit marker rule is gone; the button gets a reset (transparent, no border, inherited font) and keeps the focus ring and the 44px touch height at or below 800px. The chevron rotation now keys on `[data-open]` and still respects reduced motion.
- Tracer gate passed: the create page Advanced options and the About section, the storage details and the change page Advanced settings all work through the button in jsdom and in Chromium (33 of 33 e2e tests pass).
- `SelectField` wraps the select in `.form-field__select` with one `aria-hidden` chevron. `.form-field__control--select` has `appearance: none`, `padding-inline-end: var(--utf-space-xl)` and the control height tokens, including the touch height.
- `TextField`, `TextAreaField` and `SelectField` forward `required` as `aria-required` unless the caller passes its own value.
- Every test that queried the native element now queries the button by role and name and asserts `aria-expanded`; the two e2e specs changed only their locator lines.

## Task Commits

1. **Task 1: Tracer, button disclosure end to end** - `66c1c4f3` (feat)
2. **Task 2: Padded chevron and aria-required** - `8caf602c` (feat)

## Verification

- `npm --prefix sources/ReactApp test` (full suite): 178 files, 2610 tests pass.
- `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-create.spec.ts tests/e2e/hosting-change.spec.ts --project=chromium`: 33 passed, run after each task.
- `npm --prefix sources/ReactApp run lint` and `run typecheck`: clean.
- All task acceptance criteria greps pass (no `details` or `summary` in Disclosure, `role="region"` and `type="button"` present, no webkit marker rule, no `querySelector("summary")` in the disclosure test, `appearance: none` and `aria-required` present).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree had no node_modules**
- **Found during:** Task 1
- **Issue:** The worktree checkout contains no installed dependencies, so no test command could run.
- **Fix:** Created an NTFS junction from the worktree `sources/ReactApp/node_modules` to the main checkout's existing install. No package was installed and nothing is tracked (`node_modules/` is gitignored).
- **Files modified:** none tracked

**2. [Rule 1 - Test adaptation] Test mock drops icon props**
- **Found during:** Task 2
- **Issue:** `tests/mocks/react-icons.tsx` renders every icon without its `className`, so the chevron cannot be found by class in jsdom.
- **Fix:** The test asserts the decorative `svg` sibling of the select inside `.form-field__select` instead. The shared mock was left untouched.
- **Files modified:** `sources/ReactApp/tests/form-fields.test.tsx`

**3. [Rule 1 - Test adaptation] Hidden region has no computed accessible name in jsdom**
- **Found during:** Task 1
- **Issue:** `toHaveAccessibleName` returns an empty name for a `hidden` element, so the closed region cannot be asserted by name.
- **Fix:** The closed state asserts `aria-labelledby` points at the title id; the open state asserts `getByRole("region", { name })` is visible.
- **Files modified:** `sources/ReactApp/tests/disclosure.test.tsx`

**Total deviations:** 3 (1 blocking environment fix, 2 test adaptations). **Impact:** none on behaviour or scope.

## Issues Encountered

- GPG signing timed out, so both task commits are unsigned, per the project rule for an unavailable signer.
- `npm run build:assets` (run by the Playwright web server) regenerates `src/generated/api.ts` with line-ending-only differences; the file was restored with `git checkout -- <file>` and is not part of any commit.

## Known Stubs

None.

## Threat Flags

None. Presentation-only primitive change; no new endpoint, auth path or trust boundary.

## Next Phase Readiness

The seven remaining raw `details` blocks (`EventRoleSymbolPicker`, `CostConfirmDialog`, `ConfigurationProfileRevisionHistory`, `HostingAuditFeed`, `CloudMachinePanel`, `ServerLogsPanel`, `ServerFilesPanel`) are untouched and ready for the Wave 2 family plans to convert onto this primitive. `tests/e2e/event-authoring.spec.ts:260-261` still targets the native `details.event-role-symbol-picker`, which stays valid until the event family plan converts that picker.

## Self-Check: PASSED

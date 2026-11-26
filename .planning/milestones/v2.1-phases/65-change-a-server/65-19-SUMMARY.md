---
phase: 65-change-a-server
plan: 19
subsystem: ui
tags: [react, game-server-secrets, credentials, layout, e2e, a11y, playwright]
gap_closure: true
gap_ids: [G-65-6]

requires:
  - phase: 65-change-a-server
    provides: "65-16 chosen-value contract, 65-17 shown-once rotated value and draft provider, 65-18 chosen value input, validation and Show typed value toggle"
provides:
  - "SecretRow in fixed slots: head with an always-rendered badge slot, one status line, value block, edit block with a context slot and a toggle slot"
  - "Token-based reservations in credentials.css: two-line status, control-height slots (touch height at 800px and below)"
  - "Server_Credentials_StagedSet and a shortened Server_Credentials_RotateStaged, with a 40 character guard for every credential status line"
  - "Reveal value that stays in place while a change is staged and hides when the staged entry changes after the reveal (stagedAtReveal)"
  - "Browser evidence for G-65-6 items 1 to 3 and T-65-44: steady row layout, shown-once value, chosen value, token rule, axe and wrap"
affects: []

actuals:
  tokens: 17000   # chars/4 over the full diff (8 files, 68073 chars)
  tasks: 2
  commits: 2
plan_head_before: 3e79d3a0b05144dc19279e274c135103a8819e00
plan_head_after: 42433ead9cddd574d8e302f77abb327273379517

tech-stack:
  added: []
  patterns:
    - "Fixed slots with unconditional reserved heights instead of conditional layout remedies"
    - "One button element per slot whose label and handler swap by kind, so focus survives the swap"
    - "Staged-entry reference captured at reveal time to hide a revealed value when the entry changes"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/src/components/server/credentials.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The Show typed value toggle now always holds its slot while an input exists and is disabled until a value is typed; this replaces the 65-18 choice to mount it only while staged, because a draft-dependent child would break the fixed-slot construction"
  - "The context slot is one ActionButton whose kind (undo, keep, rotate) drives label, accessible name and handler, so the DOM node and focus stay in place when Rotate value becomes Keep current value"
  - "The e2e measures positions relative to the row, so page scrolling cannot move a baseline"

patterns-established:
  - "Row structure invariance is unit-tested by comparing child class lists across staging states"

requirements-completed: [CHG-07, CHG-03]

coverage:
  - id: D1
    description: "Credential rows keep every control in place across staging, keeping, typing and undoing: the same direct children in every state, an always-rendered badge slot, one status line and reserved action slots"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel fixed slots"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-files-secrets.spec.ts#credential staging layout (en/de, 320x568 and 390x844)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A member without the reveal permission sees an applied rotation once with Copy value and Hide value, and the value leaves no trace in storage, cookies, the URL, the console or the DOM after Hide or reload"
    requirement: "CHG-07"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-files-secrets.spec.ts#credential values without reveal shows an applied rotation once with Copy value and leaves no trace"
        status: pass
    human_judgment: false
  - id: D3
    description: "A typed chosen value is sent once through PUT /secrets as UserProvided, never shown in the review; a value that is not a token keeps the review closed"
    requirement: "CHG-03"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-files-secrets.spec.ts#credential values without reveal sends a typed chosen value once as a member-provided value"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-files-secrets.spec.ts#credential values without reveal keeps the review closed for a value that is not a token"
        status: pass
    human_judgment: false
  - id: D4
    description: "The credentials panel showing the one-time value has zero axe WCAG 2.1 AA violations at 390x844 in both languages, and a long one-time value wraps without horizontal scroll at 320px"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-files-secrets.spec.ts#one-time value layout and accessibility"
        status: pass
    human_judgment: false
  - id: D5
    description: "Whether the restructured row reads well to a member on a real phone (visual balance of the reserved slots)"
    verification: []
    human_judgment: true
    rationale: "Automated checks prove positions stay within 1px, not that the layout looks right"

duration: 20min
completed: 2026-11-21
status: complete
---

# Phase 65 Plan 19: Credentials Row Fixed Slots and Browser Evidence Summary

**Credential rows now render the same fixed slots in every staging state with token-based reserved heights, and the browser proves the shown-once rotated value, chosen values, the token rule, axe and the long-value wrap in English and German at 320 and 390 pixels.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-11-21T10:21:25Z
- **Completed:** 2026-11-21T10:42Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- `SecretRow` is restructured into fixed slots. The Changed badge is always rendered and only hidden (visibility hidden, `aria-hidden`) while nothing is staged. The separate staged notice is gone; one status line carries the saved status, "A new value is staged." or "A new value is generated on apply.". The edit block holds the input and two reserved slots, a context slot (Rotate value, Keep current value or Undo change) at the inline start and a toggle slot at the inline end.
- `credentials.css` reserves two body lines for the status and the control height (touch height at 800px and below) for each action slot, all unconditional and token based. At 520px the slots stack and the buttons fill the width.
- Reveal value stays in place while a change is staged. The value hides when the staged entry changes after the reveal, through a `stagedAtReveal` reference check; the 30-second timer and every other hide trigger stay.
- Locale: added `Server_Credentials_StagedSet`, shortened `Server_Credentials_RotateStaged`, raised the exact Phase 65 key count to 159 and added a guard that every credential status line stays within 40 characters in both languages.
- Browser evidence in `hosting-files-secrets.spec.ts` with a stateful secrets mock (rotate and set bump the version): steady row measured across five staging states, the shown-once value with Copy value (clipboard equals the value) and no trace after Hide or reload, a chosen value sent once as `UserProvided`, the token rule keeping the review closed, axe with a non-zero pass count and the long-value wrap.
- Parity matrix records the chosen value and shown-once evidence for SVC-rotateSecret, CHG-secrets-set and CHG-secrets-rotate.

## Task Commits

1. **Task 1: Tracer - credentials row in fixed slots, measured steady at 320/390 EN/DE** - `82c0684f` (fix)
2. **Task 2: End-to-end proof of the one-time value, chosen values and the token rule, axe and wrap, parity evidence** - `42433ead` (test)

## Verification

- Task 1 verify block: unit tests for credentials-panel, change-flow, configuration-panel, styles and locales (212 passed), `locales:check`, `typecheck`, `lint`, prettier on every touched file, e2e `hosting-files-secrets.spec.ts` (18 passed at that point), and the credentials-row boundary `git diff --quiet c24a26c8 -- ...` exits 0. All Task 1 acceptance greps hold.
- Task 2 verify block: `parity-matrix.test.ts` (81 passed), `lint`, prettier on the spec, e2e `hosting-files-secrets.spec.ts`, `hosting-change.spec.ts` and `hosting-a11y.spec.ts` (59 passed on the final run), `sources/WebApp` untouched.
- Plan-level: full unit suite 150 files and 1837 tests passed; `contracts:check` clean; `sources/Common/Ui/routes.json` unchanged.
- The staging layout test passed 48 of 48 with `--repeat-each 12` after the font fix below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Lint rule `react-hooks/refs` rejected the context action object**
- **Found during:** Task 1
- **Issue:** The first version held label, accessible name and handler in one object whose handler read `inputRef.current`; the lint rule flagged the object as a ref access during render.
- **Fix:** The context button is now driven by a `contextKind` (undo, keep, rotate) with a label table and a separate `runContextAction` handler.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx`
- **Commit:** `82c0684f`

**2. [Rule 1 - Bug] The steady-row e2e was flaky under parallel load**
- **Found during:** Task 2 verification
- **Issue:** Intermittent failures of about 31 to 59 pixels in the first measured state. Cause: a web font that loaded between the baseline and a later measurement re-wrapped the input hint, which is not a staging shift.
- **Fix:** The test awaits `document.fonts.ready` before measuring the baseline. The test then passed 48 of 48 with `--repeat-each 12`.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts`
- **Commit:** `42433ead`

**3. [Plan interpretation] Show typed value toggle is always present**
- **Issue:** The plan's slot rule says the toggle slot holds the toggle whenever an input exists, independent of the draft. 65-18 mounted it only while a typed value was staged.
- **Fix:** The toggle is always rendered in its slot when an input exists and is disabled until a value is typed. The unit test that asserted its absence before typing now asserts a disabled button.
- **Commit:** `82c0684f`

**Total deviations:** 2 auto-fixed (2 Rule 1), 1 plan interpretation. **Impact:** none on scope; all changes stay inside the credentials row.

## Known Limits

- **The e2e layout test is only weakly sensitive to the reservations.** I ran mutation checks on the built bundle. Removing the two-line status reservation, and removing the idle badge slot from the layout, both still passed, because at the tested widths every status string fits one line and the badge is shorter than the name line. The test does catch the old design (a notice line and a changing button row appearing), and the reservations are a safety margin enforced by the locale length guard and the unit structure tests. A stricter e2e would need a fixture that forces a status wrap.
- **`npm run format:check` reports 32 files outside this plan** (for example `tests/hosting/fleet-page.test.tsx`). They are stored with LF in git and checked out with CRLF in this working tree (`git ls-files --eol` shows `i/lf w/crlf`). Every file this plan touched passes prettier. `npm run ci` therefore fails at that step on this machine regardless of this plan.
- `65-UI-SPEC.md` and `65-11-PLAN.md` still quote the old "A new value will be generated when you apply." copy as historical text; they were not edited.

## Known Stubs

None.

## Threat Flags

None. T-65-52 is mitigated: the revealed value is fetched only on an explicit Reveal by a member with canRevealSecrets, stays in component state and drops when the staged entry changes after the reveal, after 30 seconds, when the tab hides, on unmount, on another reveal and on a newer version. Unit tests cover the staged-entry change and the timer.

## Issues Encountered

None beyond the deviations above.

## Next Phase Readiness

This is the last plan of Phase 65. Gap G-65-6 (items 1 to 3) is closed in code and in the browser; UAT test 6 is ready to be re-run by a person, and phase verification can proceed.

## Self-Check: PASSED

- Commits `82c0684f` and `42433ead` are ancestors of HEAD.
- All files listed under key-files exist and the plan's acceptance greps hold.

---
phase: 61-foundations-localization-member-access
plan: 02
subsystem: webapp-design-tokens
tags: [react, css, design-tokens, wcag, vitest]

requires:
  - phase: 61-foundations-localization-member-access
    provides: plan 61-01 locale and server state work (no direct dependency)
provides:
  - src/styles/tokens.css, the additive --utf-* semantic token layer imported before styles.css
  - tests/helpers/cssTokens.ts, a dependency-free CSS custom-property parser, var() resolver and WCAG contrast helper
  - tests/styles/tokens-contrast.test.ts, the contrast, inventory, reduced-motion and import-order guard
affects: [61-07, 61-08, 61-09, phase-62, phase-63, phase-69-accessibility-walk]

actuals:
  tokens: 4600
  tasks: 2
  commits: 2
plan_head_before: 28afe90e3baafe641960e2b1fa3e6e59791ced79
plan_head_after: 6f3cfc3425ffaa03073c3f8e57b623891f1c3442

tech-stack:
  added: []
  patterns:
    - "New design tokens alias legacy variables through var() so tokens.css and styles.css cannot drift"
    - "Contrast is measured by parsing both CSS files, resolving var() chains and compositing 12% alpha tints over each surface"
    - "A guard test proves itself live by mutating an in-memory copy of the CSS and expecting a failure"

key-files:
  created:
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/helpers/cssTokens.ts
    - sources/ReactApp/tests/styles/tokens-contrast.test.ts
  modified:
    - sources/ReactApp/src/main.tsx

key-decisions:
  - "The guard discovers state and cost families by scanning tokens.css for -fg names instead of a hard-coded list, so a new family is covered automatically"
  - "The guard-is-live check lives permanently in the test file as a mutation of an in-memory copy (danger tint at 30%), not as a one-off manual experiment"
  - "parseCustomProperties reads only top-level :root rules by default and exposes a media option, so the reduced-motion block never leaks into base values"

patterns-established:
  - "Test helpers resolve project paths with import.meta.dirname (import.meta.url is not a file URL under jsdom)"

requirements-completed: [FND-01]

coverage:
  - id: D1
    description: "tokens.css defines the full additive --utf-* layer and is imported before styles.css"
    requirement: "FND-01"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#defines every token the UI contract lists"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#imports tokens.css before styles.css in main.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every state and cost foreground meets 4.5:1 on its own 12% tint over base, raised and overlay surfaces; a lightened tint is detected"
    requirement: "FND-01"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#keeps every state and cost foreground at 4.5:1 on its own 12% tint over each surface"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#fails when a state tint is lightened above 12%"
        status: pass
    human_judgment: false
  - id: D3
    description: "Text tokens reach 4.5:1 on all surfaces and the canvas; control border and focus ring reach 3:1 on every surface"
    requirement: "FND-01"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#keeps text tokens at 4.5:1 on every surface and the canvas"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#keeps the control border and the focus ring at 3:1 on every surface"
        status: pass
    human_judgment: false
  - id: D4
    description: "All three motion duration tokens resolve to 0.01ms under prefers-reduced-motion: reduce"
    requirement: "FND-01"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#resolves all three motion durations to 0.01ms under reduced motion"
        status: pass
    human_judgment: false

duration: 6min
completed: 2026-11-18
status: complete
---

# Phase 61 Plan 02: Design token layer and contrast guard Summary

**An additive `--utf-*` semantic token layer (space, type, text, surface, border, state, cost, focus, elevation, radius, motion, dialog layout) now loads before `styles.css`, and a Vitest guard fails the build when any state, cost, text, control-border or focus-ring pair drops below WCAG AA.**

## Performance

- **Duration:** about 6 min
- **Completed:** 2026-11-18T17:28Z
- **Tasks:** 2 (both auto, the second TDD-flavoured)
- **Files changed:** 4 (3 created, 1 modified)

## Accomplishments

- `tokens.css` holds the full UI-SPEC token inventory plus `--utf-dialog-width` and `--utf-dialog-gutter`. Every colour token aliases a legacy variable or is a documented rgb alpha tint; `--utf-text-subtle` (`#8b97a7`) and `--utf-border-control` (`#6f7f92`) are the only new colour literals. No legacy variable in `styles.css` was touched.
- The reduced-motion media rule sets `--utf-motion-fast`, `-base` and `-slow` to `0.01ms`.
- `cssTokens.ts` provides `parseCustomProperties`, `resolveToken`, `parseColor`, `compositeOver`, `contrastRatio` and the small extras `extractColor` and `readProjectFile`; the resolver follows `var()` chains across `tokens.css` first and `styles.css` second and throws on cycles and missing targets.
- The guard measures 24 tint pairs (8 foreground families x 3 surfaces), 12 text pairs and 6 non-text pairs, prints the computed ratio in every assertion message, and includes a permanent mutation test (danger tint raised from 12% to 30%) proving the guard is live.

## Task Commits

1. **Task 1: Author tokens.css and import it before styles.css** - `ac3c2eaa` (feat)
2. **Task 2: Contrast, inventory, reduced-motion and import-order guard** - `6f3cfc34` (test)

**Plan metadata:** committed separately (docs commit carrying this SUMMARY, STATE.md and ROADMAP.md).

## Verification Results

- `npm test -- tests/styles/tokens-contrast.test.ts`: 11 tests pass.
- `npm run typecheck` and `npm run lint` (`--max-warnings 0`): pass.
- `npm run format:check`: passes for every file except the pre-existing `tests/hosting/arma-placement.test.tsx` (CRLF working copy, see Deferred Issues; also recorded by Plan 61-01).
- Task 1 acceptance: `grep -c "styles/tokens.css" main.tsx` is 1; `grep -c "--utf-state-danger-fg" tokens.css` is 1.
- Task 2 acceptance: the helper file exists; lightening the danger tint above 12% fails Test 1's measurement (asserted by "fails when a state tint is lightened above 12%").

## Decisions Made

- Families are discovered from the CSS rather than listed, so adding `--utf-state-foo-fg` without a sibling `-bg` or `-border`, or with poor contrast, fails automatically.
- The tracked contrast numbers are not hard-coded; only the 4.5 (text) and 3.0 (non-text) thresholds are asserted.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] import.meta.dirname instead of import.meta.url**
- **Found during:** Task 2 design (same failure Plan 61-01 recorded: `new URL(..., import.meta.url)` is not a file URL under jsdom)
- **Fix:** `cssTokens.ts` resolves the project root with `import.meta.dirname` and `node:path` rather than `node:url`.
- **Files modified:** `sources/ReactApp/tests/helpers/cssTokens.ts`
- **Commit:** `6f3cfc34`

**Total deviations:** 1 auto-fixed (1 blocking). **Impact:** none on scope or behaviour.

## Deferred Issues

- `tests/hosting/arma-placement.test.tsx` still fails `prettier --check` locally because of CRLF line endings in its working copy; unchanged and out of scope (carried over from Plan 61-01).

## Known Stubs

None.

## Threat Flags

None. T-61-04 is mitigated by `tokens-contrast.test.ts`; T-61-05 and T-61-SC are accepted (static CSS, no packages installed).

## Next Phase Readiness

Plans 61-07 and 61-08 can build `components/server/*` CSS from `--utf-*` tokens only. The style-guard test for raw hex and pixel literals in that directory is owned by Plan 61-07 as planned.

## Self-Check: PASSED

- `tokens.css`, `cssTokens.ts` and `tokens-contrast.test.ts` exist on disk; commits `ac3c2eaa` and `6f3cfc34` are ancestors of HEAD; all task acceptance criteria re-run green.

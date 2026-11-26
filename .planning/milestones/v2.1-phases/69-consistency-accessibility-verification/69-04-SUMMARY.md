---
phase: 69-consistency-accessibility-verification
plan: 04
subsystem: testing
tags: [eslint, vitest, typescript-ast, ratchet, localization, react]

requires:
  - phase: 68-server-automation
    provides: the current ReactApp source tree whose controls and literal text are measured
provides:
  - One rules.json shared by ESLint and Vitest (closed control list, look-alike pattern, primitives, literal props, neutral tokens, Tier A paths)
  - A TypeScript AST scanner (scan.ts) counting raw controls, button look-alikes and literal user-facing text per file
  - 17 per-family ratchet files generated from the current tree, plus a ceiling writer that never raises a ceiling
  - An ESLint no-restricted-syntax error for raw controls and look-alikes outside the primitives and the ratchet
  - A static hard-coded-text test for Tier A and non-Tier-A files
  - A locale gate that fails on identical German and English values
affects: [69-08, 69-09, 69-10, 69-11, 69-12, 69-13, 69-14, 69-15, 69-16, 69-17, 69-18, 69-19, 69-20, 69-21, 69-22, 69-23, 69-24, 69-33]

actuals:
  tokens: 13000
  tasks: 3
  commits: 3
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: 584abaa15c8c14c3e61989f225e67f95acc6eb27

tech-stack:
  added: []
  patterns:
    - "Ratchet = per-family ceiling JSON, enforced by Vitest and mirrored as ESLint ignores"
    - "Scanner and lint selectors share one rules.json so they cannot drift"

key-files:
  created:
    - sources/ReactApp/tests/ratchets/rules.json
    - sources/ReactApp/tests/ratchets/scan.ts
    - sources/ReactApp/tests/ratchets/ratchet.test.ts
    - "sources/ReactApp/tests/ratchets/family.<name>.json (17 files)"
    - sources/ReactApp/tests/locales/hard-coded-text.test.ts
  modified:
    - sources/ReactApp/eslint.config.js
    - sources/ReactApp/scripts/check-locales.mjs
    - sources/ReactApp/tests/locales/check-locales.test.ts

key-decisions:
  - "The ceiling writer only adds new entries when UTF_RATCHET_SEED=1 is set, so a normal UTF_RATCHET_UPDATE run can only lower ceilings and a new violating file stays a test failure (T-69-04)"
  - "The look-alike pattern is ^button(\\s|$), so button-group style classes are not look-alikes"
  - "The look-alike selector also covers className={\"button\"} and template literals starting with button, matching the scanner"

patterns-established:
  - "Family ratchet files are the only per-plan editable surface for converters; they run UTF_RATCHET_UPDATE=<family> after converting"

requirements-completed: [CON-01, CON-02, CON-03, L10N-03]

coverage:
  - id: D1
    description: "Raw controls and button look-alikes outside the primitives and the ratchet are ESLint errors"
    requirement: CON-01
    verification:
      - kind: automated_ui
        ref: "npm --prefix sources/ReactApp run lint (green); scratch file with a raw button and three look-alike variants reported 4 errors, then deleted"
        status: pass
    human_judgment: false
  - id: D2
    description: "A Vitest ratchet fails on a ceiling excess, an unmapped violating file, a duplicate mapping or an empty reason"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts#fails when a count exceeds its ceiling"
        status: pass
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts#holds every family at or below its ceiling for the real source tree"
        status: pass
    human_judgment: false
  - id: D3
    description: "A static AST scan fails on literal user-facing text in JSX text, user-facing props and toast or announcer calls"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/locales/hard-coded-text.test.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "locales:check fails when a German value equals the English one and the key is not allow-listed"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/locales/check-locales.test.ts#fails on identical values that are not allow-listed"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false

duration: 14min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 04: Mechanical Gates Summary

**One shared rules.json drives an ESLint raw-control restriction and a TypeScript-AST Vitest ratchet over 17 family files (73 files, 516 raw controls, 280 look-alikes, 14 literals held), and identical German text now fails the locale gate.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-11-25T10:41:00Z
- **Completed:** 2026-11-25T10:55:00Z
- **Tasks:** 3
- **Files modified:** 28 (25 created, 3 modified)

## Accomplishments

- `rules.json` is read by `eslint.config.js` (selectors, ignores) and by `scan.ts` (counts). Ignores are the six primitive files plus every ratcheted file with a non-zero control or look-alike ceiling, so `npm run lint` stays green while each Wave 2 plan converts its own family.
- `scan.ts` uses the TypeScript compiler API: raw control opening elements (self-closing included), look-alike className literals on any element, and literal user-facing text (JSX text with two letters outside `code`, `kbd` and `ApiText`; the D-13 literal props; first string argument of `showToast` and `announce`), with neutral tokens removed first.
- `ratchet.test.ts` enforces ceilings, non-vacuity, unmapped files, duplicate mapping and empty reasons, and hosts the opt-in writer. The pure `checkRatchet` and `lowerCeilings` functions have inline failing and passing samples.
- `hard-coded-text.test.ts` proves the scan with inline samples (`Terrain and scenario`, literal placeholder, toast call, neutral and code text, German letters) and holds Tier A and other files at their literal ceilings.
- `check-locales.mjs` turns the identical-value warning into `[identical]` failures; stale allow-list entries stay warnings. Real locale files: 3832 keys, 0 failures, 0 warnings.

## Starting targets for Wave 2

Measured at HEAD 0dc5b447 (after Phase 68). Writer: `UTF_RATCHET_UPDATE=<family|all> npm --prefix sources/ReactApp test -- tests/ratchets/ratchet.test.ts`.

| Family | Owner plan | Files | Controls | Look-alikes | Literals |
|--------|-----------|-------|----------|-------------|----------|
| shell | 69-08 | 5 | 17 | 0 | 3 |
| hosting-core | 69-09 | 0 | 0 | 0 | 0 |
| hosting-files | 69-10 | 1 | 36 | 15 | 0 |
| hosting-profiles | 69-11 | 6 | 32 | 17 | 0 |
| hosting-finance | 69-12 | 6 | 25 | 0 | 0 |
| hosting-runtime | 69-13 | 5 | 12 | 3 | 5 |
| admin-authority | 69-14 | 3 | 34 | 18 | 0 |
| admin-members | 69-15 | 3 | 41 | 19 | 0 |
| events-admin | 69-16 | 8 | 54 | 24 | 5 |
| events-member | 69-17 | 3 | 27 | 15 | 0 |
| admin-content | 69-18 | 5 | 21 | 27 | 0 |
| admin-media-reports | 69-19 | 5 | 58 | 37 | 0 |
| admin-modpacks-terrains | 69-20 | 8 | 63 | 40 | 1 |
| admin-verein-settings | 69-21 | 7 | 42 | 34 | 0 |
| public-hero | 69-22 | 2 | 12 | 8 | 0 |
| public-forms-wiki | 69-23 | 5 | 41 | 23 | 0 |
| shared-workspace | 69-24 | 1 | 1 | 0 | 0 |
| **Total** | | **73** | **516** | **280** | **14** |

hosting-core and most of shared-workspace start empty because the scanner finds nothing to ratchet there at this HEAD (Phase 68 and earlier phases already use shared controls in those files); their family files exist with empty `files` so the family map stays complete. A converter plan that finds a family empty has nothing to lower and only needs to keep it empty.

## Task Commits

1. **Task 1: Tracer - one shared rule set drives ESLint and the Vitest ratchet** - `d8ee4dd2` (test)
2. **Task 2: Every family ratchet and the static literal-text scan** - `c8eede5a` (test)
3. **Task 3: Identical English and German values fail the locale gate** - `584abaa1` (build)

**Plan metadata:** committed separately (docs).

Tracer gate: automated-only verify re-run end to end passed (`ratchet.test.ts`, `npm run lint`, `npm run typecheck`) - tracer verified, expansion continued.

## Files Created/Modified

- `sources/ReactApp/tests/ratchets/rules.json` - shared element list, look-alike pattern, primitive files, literal props and calls, neutral tokens, Tier A paths
- `sources/ReactApp/tests/ratchets/scan.ts` - AST scanner, family loading, `checkRatchet`, `lowerCeilings`
- `sources/ReactApp/tests/ratchets/ratchet.test.ts` - ceiling enforcement, family map with seed assignment, opt-in writer
- `sources/ReactApp/tests/ratchets/family.*.json` - 17 per-family ceiling files, `converted in <owner>` reasons
- `sources/ReactApp/tests/locales/hard-coded-text.test.ts` - static literal-text scan
- `sources/ReactApp/eslint.config.js` - restricted-syntax block built from rules.json and the family files
- `sources/ReactApp/scripts/check-locales.mjs`, `sources/ReactApp/tests/locales/check-locales.test.ts` - identical values fail

## Decisions Made

- The writer adds new entries only with `UTF_RATCHET_SEED=1`; routine `UTF_RATCHET_UPDATE` runs can only lower ceilings, so a regression in a new file cannot be absorbed silently (T-69-04).
- `lookalikePattern` is `^button(\s|$)` (the word button), keeping `button-group` style classes out of the rule.
- ESLint look-alike selectors cover a plain literal, a literal inside `{}` and a template literal head, matching exactly what `scan.ts` counts.
- Family files use paths relative to `sources/ReactApp` (`src/...`) so ESLint ignores and scanner keys are identical.
- `literalCalls` was added to `rules.json` (beyond the interface block) so the toast and announcer call names are shared data, not hard-coded in the scanner.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] All 17 family files were generated and committed in Task 1, not only family.shell.json**
- **Found during:** Task 1
- **Issue:** The Task 1 verify requires `npm run lint` to pass at the tracer commit. The ESLint rule is on for every `src/**/*.tsx` file outside the ratchet, so with only the shell family present 68 other violating files would have failed lint (Pitfall 3).
- **Fix:** Generated every family file with the writer (`UTF_RATCHET_UPDATE=all UTF_RATCHET_SEED=1`) and committed them with Task 1. Task 2 therefore added only `hard-coded-text.test.ts`; its acceptance criterion (17 family files) already held.
- **Files modified:** `sources/ReactApp/tests/ratchets/family.*.json`
- **Commit:** `d8ee4dd2`

**2. [Rule 3 - Blocking] node_modules linked from the main checkout**
- **Found during:** Task 1
- **Issue:** The fresh worktree had no `node_modules`. A package install was not appropriate (and no package is added by this plan).
- **Fix:** Created an untracked, gitignored directory junction `sources/ReactApp/node_modules` to the main checkout's existing install. No dependency or lockfile change.
- **Verification:** vitest, eslint, tsc and prettier all ran against it.

**Total deviations:** 2 auto-fixed (both Rule 3). **Impact:** none on deliverables; the commit split differs from the plan text only.

## Issues Encountered

- `prettier --check` over the whole project reports pre-existing CRLF-related warnings in 214 files of the Windows checkout; every file touched by this plan passes `prettier --check`.
- The plan's "re-measure" note held: the research counted 529 raw elements and 32 literal hits; this tree has 516 controls (primitive files excluded) and 14 literals (neutral tokens and `code` text excluded), 280 look-alikes as researched.

## Known Stubs

None.

## Threat Flags

None. No network, auth or trust-boundary surface was added (test and lint configuration only).

## Next Phase Readiness

Wave 2 plans can convert their family files in parallel without conflicts; each runs `UTF_RATCHET_UPDATE=<family>` to lower its ceilings. 69-33 should add the unused-key check to `check-locales.mjs` and tighten every ceiling to the final counts.

## Self-Check: PASSED

- Created files exist (rules.json, scan.ts, ratchet.test.ts, 17 family files, hard-coded-text.test.ts) and commits `d8ee4dd2`, `c8eede5a`, `584abaa1` are ancestors of HEAD.
- Acceptance criteria re-run: `no-restricted-syntax` count 1, `ratchets` count 2, `family.shell.json` present; 17 family files, `Terrain and scenario` count 1; `[identical-warning]` count 0, `[identical]` count 1.
- Plan verification: `npm test -- tests/ratchets tests/locales` (101 passed, 1 skipped opt-in writer), `npm run lint`, `npm run locales:check` (0 failures, 0 warnings) and `npm run typecheck` all pass.

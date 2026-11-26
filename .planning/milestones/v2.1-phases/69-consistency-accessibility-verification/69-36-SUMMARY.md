---
phase: 69-consistency-accessibility-verification
plan: 36
subsystem: webapp-localization
tags: [locales, check-locales, german, gap-closure, WR-02]
requires: ["69-34"]
provides:
  - "[identical-case] rule in locales:check"
  - "[english-words] rule and english-words-allowed.json in locales:check"
affects: [sources/ReactApp locale gate]
tech-stack:
  added: []
  patterns: ["documented allow lists with a mandatory reason per key"]
key-files:
  created:
    - sources/ReactApp/src/locales/english-words-allowed.json
  modified:
    - sources/ReactApp/scripts/check-locales.mjs
    - sources/ReactApp/tests/locales/check-locales.test.ts
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
decisions:
  - "english-words-allowed.json starts empty; no correct German value contains a listed function word"
  - "Case-only matches that are correct German (nouns, loanwords, Command Center) are allow-listed in identical-allowed.json, not translated"
metrics:
  tasks: 2
  commits: 2
  files: 5
status: complete
requirements: [L10N-03]
gap_ids: [WR-02]
actuals:
  tokens: 30000
  tasks: 2
  commits: 2
plan_head_before: b5a7800819842c3cc0488e9b06df7435121a0236
plan_head_after: 9ffb754af1626bbf7e5998130ec887421c9d9ff2
completed: 2026-11-25
---

# Phase 69 Plan 36: Harden the German locale gate (WR-02) Summary

`npm run locales:check` now fails German values that equal English apart from letter case (`[identical-case]`) or that contain an English function word (`[english-words]`), and the nine English values behind WR-02 are German.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: case-insensitive identical rule, case-only values translated or allow-listed | 8a4ea2d4 |
| 2 | English function word scan with a documented key allowlist | 9ffb754a |

## What changed

- `check-locales.mjs`: `comparable()` helper (trim, collapse whitespace, lower-case); `[identical-case]` reported only when the value is not byte-identical; the stale check of `identical-allowed.json` compares ignoring case. Added `englishFunctionWords` (24 words, exclusions documented in JSDoc), `englishWordsIn()` (strips `{{x}}`, `{n}`, `{n:fmt}` and URLs, whole-word Unicode-aware match, sorted and de-duplicated), the `--english-allow` option, and validation of `english-words-allowed.json` (`[allow-reason]` fails on an empty reason, `[allow-stale]` warns).
- `de.json` (values only): the five WR-02 values plus `Auto_InitialSlots_9a5c0b97`, `Auto_MissionDetails_6c819bdd`, `Auto_TotalSlots_7dea1c50`, `Open slots`.
- `identical-allowed.json`: the 19 planned correct-German case-only keys, each with a reason.
- Tests: 4 new `[identical-case]` cases and 8 new `[english-words]` cases; `run()` and `runUnused()` pass a temp `--english-allow` file so no temp test reads the real allowlist.

## Verification

- `locales:check`: "Checked 3900 English and 3900 German keys: 0 failure(s), 0 warning(s)."
- `locales:unused`: exit 0 (49 pre-existing `[unused-warn]` lines, unrelated).
- `npm test -- tests/locales`: 155 passed. `npm run lint`: clean.
- RED was confirmed before each implementation step (3 new case tests failed; all tests failed on the unknown `--english-allow` flag).
- Prior to translation the checker reported exactly the 26 case-only keys and the 2 function-word keys the plan predicted; no unplanned keys appeared.

## Deviations from Plan

None in behavior. Process notes:

- `node_modules` was absent in the fresh worktree; ran `npm ci` (lockfile-pinned, no package added) to run Vitest and ESLint.
- Ran Prettier on the three touched source files; `identical-allowed.json` showed only line-ending noise and was left unchanged.

## Known Stubs

None.

## Threat Flags

None. T-69-44 mitigated: both allow lists require a non-empty reason and stale entries warn.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/locales/english-words-allowed.json
- FOUND commits: 8a4ea2d4, 9ffb754a

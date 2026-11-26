---
phase: 68-auto-stop-schedules
plan: 18
subsystem: testing
tags: [playwright, axe, vitest, parity-matrix, react, a11y]

requires:
  - phase: 68-auto-stop-schedules
    provides: automation UI from plans 68-04, 68-12, 68-14, 68-15, 68-16 and 68-17
provides:
  - Mocked browser journeys for every automation surface (panel, consent, notice, toast, menu row, Activity, Overview)
  - axe WCAG 2.1 AA scans and 320/390 px overflow checks in English and German
  - Source-scan guards against polling and automation draft leaks
  - Four AUT- parity rows plus Phase 68 sentences on TAB-settings and FLT-profile-menu-notifications
affects: [68-19, phase-68-verification]

actuals:
  tokens: 90000
  tasks: 3
  commits: 6

plan_head_before: 455f8279
plan_head_after: f794f0f7457ebb6762a211dfc733325f28ed7627

tech-stack:
  added: []
  patterns:
    - "Automation mocks (automationMocks.ts) layered over the shared fleet stream mock"
    - "Instant scrollIntoView in viewport checks because the shell uses scroll-behavior: smooth"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/automationMocks.ts
    - sources/ReactApp/tests/e2e/hosting-automation.spec.ts
    - sources/ReactApp/tests/hosting/automation-source-scan.test.ts
  modified:
    - sources/ReactApp/tests/e2e/support/fleetStream.ts
    - sources/ReactApp/tests/e2e/hosting-a11y.spec.ts
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "Parity rows use status kept with a Phase 68 evidence sentence; the guard has no New status"
  - "Viewport checks jump with behavior: instant and tolerate one subpixel, instead of waiting out the smooth scroll"

requirements-completed: [AUT-01, AUT-02, AUT-03, AUT-04, AUT-05]

coverage:
  - id: D1
    description: "Mocked browser journeys cover the owner save with consent, 409 conflict, invalid input, read-only member, pending stop notice/toast/menu row/cancellation, Activity trigger line and Overview fact"
    requirement: "AUT-01"
    verification:
      - kind: e2e
        ref: "npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "axe WCAG 2.1 AA scans (panel states, workspace notice, consent dialog) in en and de, and no horizontal scroll at 320x568 and 390x844"
    requirement: "AUT-05"
    verification:
      - kind: automated_ui
        ref: "npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-a11y.spec.ts tests/e2e/hosting-automation.spec.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "No setInterval/refetchInterval in the automation files and no draft written to storage, URL or console, with live-pattern proofs"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/automation-source-scan.test.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Parity matrix rows AUT-settings-panel, AUT-warning-notice, AUT-activity-trigger, AUT-overview-fact and the touched TAB-settings and FLT-profile-menu-notifications rows"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Env-gated screen capture of panel, rule block, consent dialog and notice at 1280 and 390 px in both languages for the owner's alignment review"
    verification:
      - kind: e2e
        ref: "UTF_CAPTURE_SCREENS=1 npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts -g 'automation screens'"
        status: pass
    human_judgment: true
    rationale: "The captures run, but judging dialog alignment and select padding is the owner's visual review"

duration: 20min
completed: 2026-11-24
status: complete
---

# Phase 68 Plan 18: Automation UI quality gates Summary

**Mocked-browser, axe, small-viewport, no-polling and parity gates for the automation UI, in English and German, salvaged from an earlier run and re-verified against the current tree with one flaky viewport check fixed.**

## Performance

- **Duration:** about 20 min (salvage continuation)
- **Tasks:** 3 (all satisfied by the salvaged commits; one test stabilization and one formatting fix added)
- **Files modified in this continuation:** 2

## Salvage assessment

This was a continuation of an earlier executor run that committed the work and ended without a SUMMARY. Each task was compared with the plan and found complete; nothing was redone.

| Task | Status | Commit |
| ---- | ------ | ------ |
| 1 Tracer: mocked browser journeys (`automationMocks.ts`, `fleetStream.ts` AutoStop fields, `hosting-automation.spec.ts`, env-gated capture) | Satisfied by salvaged commit, re-verified | 61bf7747 |
| 2 Accessibility and 320/390 viewport scans, keyboard order | Satisfied by salvaged commit, re-verified; one check stabilized | 81974081, cb2e8001 |
| 3 Source-scan guards and parity rows | Satisfied by salvaged commits, re-verified | 6d92a562 (scan), 961c5148 (matrix and guard) |

Plan-owned commits: 61bf7747, 81974081, 6d92a562, 961c5148 (salvaged) and cb2e8001, f794f0f7 (new). The measured range from the pre-plan base (`455f8279..HEAD`) is larger than 6 because it also holds merge commits and unrelated Phase 67 UAT and Phase 69 planning commits.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] German 320 px notice check measured mid-animation**
- **Found during:** Task 2 re-verification against the current tree
- **Issue:** "the German notice title and reason fit within one screen height" failed (reason bottom 850 px, expected at most 568). The shell sets `scroll-behavior: smooth`, so `scrollIntoView({ block: "start" })` was still animating (scrollY 28 of 717) when the boxes were read.
- **Fix:** Jump with `behavior: "instant"` and allow one subpixel on the title's top edge (the settled title sits at -0.125 px). The reason now ends at about 135 px.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-automation.spec.ts`
- **Commit:** cb2e8001

**2. [Rule 3 - Blocker] Prettier violation blocking the plan-level `ci` script**
- **Found during:** plan-level verification (`format:check`)
- **Issue:** `tests/hosting/workspace-section-panels.test.tsx` (added by earlier Phase 68 plans) had four statements Prettier wraps differently, so `npm run ci` could not pass.
- **Fix:** Ran Prettier on that one file; the 30 tests in it still pass.
- **Files modified:** `sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx`
- **Commit:** f794f0f7

**Total deviations:** 2 auto-fixed (1 bug, 1 blocker). **Impact:** test and formatting only; no production code changed.

## Verifications actually run (current tree, base 68cad819)

| Command | Result |
| ------- | ------ |
| `npm --prefix sources/ReactApp test -- tests/hosting/automation-source-scan.test.ts tests/hosting/parity-matrix.test.ts` | pass, 118 tests |
| `npm --prefix sources/ReactApp run lint` | pass, 0 warnings |
| `npm --prefix sources/ReactApp run typecheck` | pass |
| `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts tests/e2e/hosting-a11y.spec.ts` | first run 40 passed, 1 failed (deviation 1); after the fix 41 passed, 12 skipped (the env-gated capture tests), 0 failed |
| `UTF_CAPTURE_SCREENS=1 ... test:e2e -- tests/e2e/hosting-automation.spec.ts -g "automation screens"` | pass, 12 captures |
| `npm --prefix sources/ReactApp test` (full vitest) | run 1: 2558 passed, 1 failed (a file-browser test in an unrelated spec, timing under load); run 2 and run 3: 177 files, 2559 tests, all passed |
| `npm --prefix sources/ReactApp run locales:check` | pass, 3830 en and 3830 de keys, 0 failures |
| Prettier on the whole ReactApp tree with `--end-of-line auto` | pass after deviation 2 |
| Task acceptance greps | all pass (test( count 15, UTF_CAPTURE_SCREENS 3, AutoStop in the spec 1, a11y spec references automation, 320/390 count 4, AUT- rows 4, phase68 in the guard 14, refetchInterval in the scan 5) |

Not run as written: `npm run ci` end to end. In this Windows checkout `format:check` and `contracts:check` fail on line endings alone (autocrlf rewrites every file as CRLF; `generate` rewrites `src/generated/api.ts` with a CRLF-only diff, restored with `git checkout` and no content difference). Each part was run separately instead: locales, lint, typecheck, test, the asset build (done by the Playwright web server) and Prettier with `--end-of-line auto`. The generated contract has no content diff.

Out of scope and not run: the seeded-stack suite (plan 68-19).

## Observations

- `hosting-automation.spec.ts` uses a fixed `stopsAt` (2026-11-23T20:30:00Z), so the notice's relative sentence now reads "yesterday". No assertion depends on it and all tests pass; a fake clock would keep the sentence meaningful if a test ever asserts it.
- The worktree had no `node_modules`; a directory junction to the main checkout's `node_modules` was used (untracked, ignored). It is not part of any commit.

## Known Stubs

None.

## Threat Flags

None. T-68-42 (draft leakage) is mitigated by `automation-source-scan.test.ts`, which proves its patterns on inline samples.

## Self-Check: PASSED

- Created files exist: `automationMocks.ts`, `hosting-automation.spec.ts`, `automation-source-scan.test.ts`.
- Commits 61bf7747, 81974081, 6d92a562, 961c5148, cb2e8001 and f794f0f7 are ancestors of HEAD.

---
phase: 64-guided-server-creation
plan: 09
subsystem: ui
tags: [react, playwright, axe, accessibility, parity-matrix, css]
status: complete
requires:
  - phase: 64-08
    provides: complete guided CreateServerPage with presets, validation focus and create states
provides:
  - Create page e2e at 320x568 and 390x844 in English and German with axe, focus and journey checks
  - Parity matrix: 17 CRT rows and 8 create SVC rows kept or relocated with evidence, behind a guard
  - Create bar height fix so the bottom bar stays within a third of a 568px viewport
  - hosting.css without the old create form rules
affects: [phase-65, phase-69]
requirements-completed: [CRT-01, CRT-02, CRT-03, CRT-04]
commits: 6
plan_head_before: 9b9556368709a2aa4fa6c8cc56b74d8b2c139714
plan_head_after: d94147445180b3de7a5ef3552485a3d6dab0aae5
actuals:
  tokens: 60000
  tasks: 3
  commits: 6
key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-create.spec.ts
  modified:
    - sources/ReactApp/src/components/server/create.css
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - sources/ReactApp/src/features/hosting/hosting.css
key-decisions:
  - "The bar budget (one third of the viewport) was not met by the earlier plans; fixed in create.css rather than loosening the assertion: the hourly sentence uses body size at 520px and below, and the monthly sentence is hidden while a blocking reason or alert is in the bar"
  - "Focus-not-obscured is asserted twice: after real Tab navigation, and with scrollIntoView({block: nearest}) from the top, because a browser centers a script or Tab focus and that hides a missing scroll-padding"
  - "Test copy is a literal per-culture table in the spec (copied from en.json and de.json) instead of importing the locale files, keeping the spec free of build-time JSON imports"
coverage:
  - id: D1
    description: "Create page default, blocked-owner, Advanced-open and invalid-submit states fit 320x568 and 390x844 in EN and DE: no horizontal overflow, bar within a third, axe clean with evaluated rules"
    requirement: "CRT-04"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-create.spec.ts#create page at 320x568 and 390x844"
        status: pass
    human_judgment: false
  - id: D2
    description: "Last Advanced control is not hidden behind the bottom bar at 320x568 in EN and DE"
    requirement: "CRT-04"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-create.spec.ts#focus is not hidden behind the bottom bar"
        status: pass
    human_judgment: false
  - id: D3
    description: "Create with Start it now opens the shared start confirmation over the new workspace, and dismissing it shows the not-started notice"
    requirement: "CRT-02"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-create.spec.ts#create then start journey"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every CRT row is kept or relocated (17 rows), the eight create SVC rows are kept, no row deleted"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#keeps all seventeen create-form rows kept or relocated"
        status: pass
    human_judgment: false
---

# Phase 64 Plan 09: Create Page Browser Proof and Phase Close Summary

The guided create page is proven in a real browser at 320 and 390 px in English and German, create then start reaches the shared confirmation, every old create capability is accounted for behind a parity guard, and the dead create CSS is gone.

## Accomplishments

- **Task 1 (tracer)**: `hosting-create.spec.ts` with a strict API mock (every unmocked request aborted and recorded), a `scan` helper that requires evaluated axe rules, and the 320x568 English default test. Commit `be22994f`.
- **Task 2**: 16 state tests (2 widths x 2 cultures x default, blocked owner, Advanced open, invalid submit), two focus-not-obscured tests, and the create-then-start journey. Commit `36ef6b1e`, with the layout fix `9637fdf6` it needed (see Deviations).
- **Task 3**: 17 CRT rows (3 kept, 14 relocated) and 8 SVC rows kept with locations and evidence (`5a7ef243`), a guard for both (`a319b028`; mutating CRT-title to pending makes it fail), and removal of the retired rules for the ten create-only classes in `hosting.css` (`d9414744`). `next-hosting-storage-fit`, `next-hosting-form-actions` and `next-hosting-check` stay.

## Verification

All commands run in the worktree on Windows with Chromium installed by the environment (no download needed):

- `npm run test:e2e -- hosting-create hosting-a11y hosting-workspace hosting-fleet`: 64 passed. The create spec executed for real (20 tests, including axe with `passes.length > 0`), not skipped.
- `npm run test` (Vitest): 135 files, 1532 tests passed, including `parity-matrix.test.ts` (79).
- `locales:check`, `contracts:check` (exit 0), `lint --max-warnings 0`, `typecheck` and the asset build (run by Playwright's web server) pass.
- `npm run ci` as a whole fails at `format:check` because this checkout is CRLF (418 pre-existing files; noted in 64-07 and 64-08). `prettier --check --end-of-line auto .` passes for the whole app, so every other ci step was run separately and passed.
- `git diff --exit-code -- sources/Common/Ui/routes.json` exits 0; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`.
- Acceptance greps all met (AxeBuilder 2, command/servers/create 2, passes.length 1, 844 1, dialog[open] 2, "de" 2, CRT- in guard 1, form-actions in hosting.css 3, retired classes 0, CRT pending 0, CRT kept or relocated 17, SVC kept 8).

Not run: the human-check of the Task 2 verify block (real development API through Aspire or Vite). No development API is available here; recorded as skipped, the e2e journey covers the same path with mocks.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The create bar exceeded one third of a 568px viewport**
- **Found during:** Task 2
- **Issue:** the plan truth and UI-SPEC require the bar at most 189px. Measured at 320x568: English default 173px, German default 194px, blocked owner 240px (English) and 261px (German), because the hourly sentence at heading size wraps to two lines, the German monthly sentence wraps, and the real owner-frozen reason is three lines (the UI-SPEC assumed two). The invalid alert also exceeded the budget in the same way.
- **Fix:** in `create.css`, the hourly sentence uses body size at 520px and below, and `.create-summary:has(.action-reason, .create-summary__alert)` hides the monthly sentence at 800px and below while a reason or alert needs the room. All four states now fit in both languages.
- **Files modified:** `sources/ReactApp/src/components/server/create.css` (outside the plan's file list, needed to satisfy its must-have truth)
- **Commit:** `9637fdf6`

**2. [Plan wording] Focus-not-obscured needed a second assertion**
- A script `focus()` and a Tab move both centered the control in Chromium, so the check passed even with `scroll-padding-block-end` removed (confirmed by mutation). The test now also scrolls with `block: "nearest"` from the top, which fails without the padding (568 px bottom edge) and passes with it.

**3. [Plan wording] TDD order for the parity guard**
- The matrix was updated before the guard was added, so no red run preceded it. The guard was verified by mutation instead (CRT-title set to pending fails the new case, then restored).

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. T-64-18 mitigated (unmocked requests are aborted, recorded and asserted empty in every test; fixtures hold no secrets). T-64-19 mitigated (no row removed, each changed row cites its evidence tests, the guard keeps CRT rows from reverting to pending).

## Notes for later phases

- The three UI-SPEC backstop rows stay backstops: the held-out visual baselines arrive with the Phase 69 Playwright mobile projects. The unresolved large cost-owner list assumption from 64-08 and the mod-pack-never-required assumption (OQ1) still need user confirmation.
- The plan-commit ledger file was not used in this worktree; the base SHA is recorded as `plan_head_before`.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/e2e/hosting-create.spec.ts
- FOUND: .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
- FOUND commits: be22994f, 9637fdf6, 36ef6b1e, 5a7ef243, a319b028, d9414744

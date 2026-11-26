---
phase: 69-consistency-accessibility-verification
plan: 34
subsystem: testing
tags: [ratchet, parity-matrix, screen-registry, vitest, playwright, legacy-css]
status: complete

requires:
  - phase: 69-33
    provides: "closed findings, locales:unused gate and banned-label scan that the final run builds on"
  - phase: 69-04
    provides: "ratchet writer UTF_RATCHET_UPDATE and the family files"
  - phase: 69-06
    provides: "screen registry guard with the pending-rule marker"
provides:
  - "allowedRemainders in tests/ratchets/rules.json: the only files that may hold a family entry, each with a reason"
  - "checkAllowances: fails for a family entry outside the list, an allowance without a reason and a stale allowance (the list can only shrink)"
  - "ratchet test that every family ceiling equals the current count"
  - "legacy CSS ratchet writer (UTF_LEGACY_RATCHET_UPDATE=1), ceilings equal to current counts, every outline-removal ceiling zero, Tier C hero reason on publicEvents.css and styles.css"
  - "registry guard fails when any slice declares a pending id"
  - "parity matrix closed: 0 pending rows, the 26 Phase 69 rows kept with Phase 69 evidence and test paths, enforced by phase69Offenders and a no-pending test"
affects: []

plan_head_before: a3888f111fd5c2d0a220e714693ece78b0298b72
plan_head_after: 77a0e29ddb387d94f33e3678aa2e528fd412d090
commits: 4

actuals:
  tokens: 12100
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Allowance list as data (rules.json) plus a checker that fails on both an unlisted entry and a stale allowance, so the list only shrinks"
    - "Opt-in ceiling writers that refuse to raise a ceiling (UTF_RATCHET_UPDATE, UTF_LEGACY_RATCHET_UPDATE)"
    - "Per-phase parity guard with a live negative sample (phase66 to phase69 follow one shape)"

key-files:
  created: []
  modified:
    - sources/ReactApp/tests/ratchets/rules.json
    - sources/ReactApp/tests/ratchets/ratchet.test.ts
    - sources/ReactApp/tests/ratchets/scan.ts
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/styles/legacy-ratchet.test.ts
    - sources/ReactApp/tests/hosting/screen-registry.test.ts
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/src/features/admin/ModPackContentEditor.tsx
    - sources/ReactApp/src/features/admin/ModPackCreatePage.tsx

key-decisions:
  - "allowedRemainders lists exactly the eight files that still hold a family entry (four editor internals, FileChooserButton, WikiPage, CommunityLayout, main.tsx), not the wider list in the plan text: ModPackContentEditor, FieldReportEditorPage, GdprContentEditor, PublicPageEditorPage, GalleryAssetPicker and CommandCenterLayout are already converted, and a stale allowance fails the test so they cannot silently come back"
  - "The ratchet writer produced no count change: every family ceiling already equaled its current count, so the family JSON files are unchanged"
  - "The registry pending rule is a separate guard over each slice pending list; the slices were already empty, so the rule locks the state instead of changing it"

requirements-completed: [CON-01, CON-02, CON-03, FND-06, VER-01, VER-03]

coverage:
  - id: D1
    description: "Family ratchet ceilings equal current counts and only documented allowances remain on the ratchet"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts#holds only documented allowances on the ratchet of the real source tree"
        status: pass
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts#holds every ceiling at the current count"
        status: pass
    human_judgment: false
  - id: D2
    description: "Legacy CSS ceilings equal current counts, outline removal is zero everywhere and the Tier C hero reason is recorded"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/styles/legacy-ratchet.test.ts#holds every ceiling at the current count so that a conversion must lower it"
        status: pass
      - kind: unit
        ref: "tests/styles/legacy-ratchet.test.ts#records the Tier C hero reason on the hero stylesheets"
        status: pass
    human_judgment: false
  - id: D3
    description: "The screen registry fails when any slice still declares a pending id"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts#declares no id pending in any slice (D-12 closure)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The parity matrix has no pending row and the 26 Phase 69 rows are kept with Phase 69 evidence and test paths"
    requirement: VER-01
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#keeps the 26 Phase 69 rows kept with Phase 69 test evidence (D-19, FND-07)"
        status: pass
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#leaves no row pending after Phase 69 (D-19)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Screen-reader announcement quality of the fleet, workspace, create flow and Automation panel"
    requirement: FND-06
    verification: []
    human_judgment: true
    rationale: "Spoken output cannot be judged by axe or a unit test; it needs NVDA or VoiceOver (D-20)"

duration: 125min
completed: 2026-11-25
---

# Phase 69 Plan 34: Closed lists, closed matrix and final verification run Summary

**Ratchets now hold only eight documented allowances at their final counts, no registry id or parity row can stay pending, the 26 Phase 69 matrix rows are kept with evidence, and the final run is green except for one intermittent workspace touch-target failure recorded below as a gap.**

## Performance

- **Duration:** about 125 min, of which about 60 min were suite runs
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- **Ratchets (Task 1).** Running the writer with `UTF_RATCHET_UPDATE=all` changed no count: every family ceiling already equaled its current count. `rules.json` gained `allowedRemainders` (eight files with a reason each) and `scan.ts` gained `checkAllowances`. The ratchet test now fails when a family entry is outside the list, when an allowance has no reason, when an allowance has no remaining family entry (stale), and when any ceiling is above or below its current count. `legacy-ratchet.test.ts` gained the opt-in `UTF_LEGACY_RATCHET_UPDATE=1` writer (refuses to raise a ceiling), a test that every ceiling equals the measured count, a test that every outline-removal ceiling is zero (it already was) and a test that `pages/publicEvents.css` and `styles.css` carry `Tier C hero (D-AUTO-4)` (set in `legacy-ratchet.json`). No Tier A file other than `CommunityLayout.tsx` (sign-in dialog allowance) and no Tier B list or detail page is on the ratchet.
- **Registry and parity guards (Task 2).** The registry test fails for any non-empty slice pending list, with an inline negative sample. The parity test gained `phase69CapabilityIds` (26 ids), `phase69Offenders` (kept, owner phase 69, "Phase 69:" sentence plus a tests/ path, "service and tests only" on the two service-only profile rows, no unlisted phase 69 row), a no-pending test and negative samples. The matrix lost all 26 `pending` rows (`grep -c "| pending |"` is 0), each now `kept` with a Phase 69 evidence sentence and test paths; the Phase 68 AUT rows were already kept and stay guarded by the Phase 68 test.
- **Final run (Task 3).** See the results below.

## Final run

### `npm run ci`, step by step (Windows worktree)

| Step | Result |
|------|--------|
| `locales:check` | 3900 English and 3900 German keys, 0 failures, 0 warnings |
| `locales:unused` | 0 failures, 49 warnings (bare-word or sentence keys, recorded in 69-33) |
| `contracts:check` | passes (the regenerated `api.ts` differs only by stat and line endings; restored, never committed) |
| `format:check` | clean with `--end-of-line auto` after the format fix below; the raw command reports 628 files on this CRLF checkout only |
| `lint` | pass, 0 warnings |
| `typecheck` (`tsc -b`) | exit 0 |
| `test` (Vitest) | 207 files, 3266 passed, 2 skipped, 0 failed (77 s) |
| `build:assets` | fails in this environment, see "Environment problem" |

### Mocked Playwright suite (`npm run test:e2e` equivalent)

Run with a temporary private-port copy of the config (4273 and 4274, deleted before the commit) against a build that stubbed the broken package (see below). 1206 tests listed.

| Project | Passed | Skipped | Failed |
|---------|--------|---------|--------|
| chromium | 457 | 77 | 0 |
| mobile-390 | 256 | 82 | 0 |
| mobile-320 | 128 | 206 | 0 |
| Total | 841 | 365 | 0 |

Skips are the `@visual` baseline tests (30 per mobile project, 15 key screens in two languages) and the specs' own conditions (pixel comparison is opt-in with `UTF_VISUAL_BASELINES=1`). No flaky test in this default-workers run (12 workers, retries 0).

### Wall clock, next to the 69-05 measurement

| Run | Result | Wall clock |
|-----|--------|------------|
| 69-05 baseline (248 tests), `CI=true --workers=1` | 212 passed, 36 skipped, 0 failed | 371 s |
| This plan, default workers (12), 1206 tests | 841 passed, 365 skipped, 0 failed | 4.8 min test time, 292 s including server start |
| This plan, `CI=true --workers=1` (what `.mozaic/react-browser.Containerfile` runs), stopped at the time box | 525 of 841 passing tests done after 1862 s, 1 flaky test (see gap) | exceeds 30 min |

**The Mozaic 30-minute budget is exceeded.** The CI-like serial run had finished 525 of the 841 passing tests (chromium done, mobile-390 part way) after 31 minutes; the per-test durations of the parallel run sum to 3319 s, so a full serial run needs about 48 to 55 minutes. This was a stop at the time box and not a full serial measurement. Pipeline files and CI timeouts and workers were not changed (D-AUTO-1). Deferred follow-up for the pipeline owner: raise `timeout_minutes: 30` for `react-browser` or run the Playwright projects as separate jobs or with workers above 1.

## Task Commits

1. **Task 1: ratchets tightened** - `5b6d3171` (test)
2. **Task 2: parity matrix closed** - `d0faccfe` (docs); **registry and parity guards** - `ca70c8f2` (test)
3. **Task 3: final run** - no code change of its own; the format fix `77a0e29d` (style) came out of it. The record is this SUMMARY.

The matrix commit precedes the guard commit so that every commit is green (the guard fails while a pending row exists). The plan listed them in the other order.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Two mod pack files failed `format:check`**
- **Found during:** Task 3 (`npm run ci`)
- **Issue:** `ModPackContentEditor.tsx` and `ModPackCreatePage.tsx` were not Prettier-formatted, which fails the `format:check` step of CI.
- **Fix:** `prettier --write` on those two files only (formatting only, no behavior change).
- **Files modified:** `sources/ReactApp/src/features/admin/ModPackContentEditor.tsx`, `ModPackCreatePage.tsx`
- **Commit:** `77a0e29d`

**2. [Plan wording] Family files unchanged**
- The plan lists the family JSON files as modified; none changed because the writer found every ceiling already at its current count. `allowedRemainders` holds eight files, not the thirteen names in the plan text, for the reason in key-decisions.

**3. [Plan wording] `scan.ts` modified**
- The `Rules` type gained `allowedRemainders` and `checkAllowances` lives next to the other ratchet checks in `scan.ts`, which the plan did not list.

## Environment problem and gap (for the orchestrator)

1. **Shared `node_modules` is damaged.** `sources/ReactApp/node_modules/@fluentui/react-teaching-popover` in the main checkout has lost its ESM `lib` folder and most of `lib-commonjs` (its directory changed at 18:25 today, during a reinstall that finished at 18:27). Vitest still passes, but `vite build` fails with `Rolldown failed to resolve import "@fluentui/react-teaching-popover"`, so `npm run build:assets` and the Playwright web server fail. No package was installed or changed (rule: never install). To run the suite I used an uncommitted, deleted temporary Vite config that aliased the package to a generated stub of its re-exports; the app does not use TeachingPopover. The owner should repair the install (`npm ci` in the main checkout) before the next CI-equivalent run. Eight other worktrees under `.claude/worktrees` still have a `node_modules` junction to this install; none of them was touched.
2. **Gap: intermittent axe target-size failure** in `tests/e2e/audit/a11y-audit.spec.ts` for `S-07 (de) @workspace`, project `mobile-390`: `.workspace-details__refresh > .button--ghost[type="button"]: target-size (serious)`. It passed in the 12-worker full run, failed on the first attempt in the `CI=true --workers=1` run and passed on retry, and failed 2 of 8 serial repeats of that test alone (`--repeat-each=8 --retries=0`). With CI retries the suite reports it as flaky, so the Playwright `fails_when` ("flaky") is met in CI mode. Probable cause (not verified): another element overlapping the "Server aktualisieren" button when the page is scanned right after the saved-settings arrival. Owning area: the workspace slice (69-24/69-26 audit and `WorkspaceShell`). Recorded in `deferred-items-69-34.md`; not fixed here because the plan returns failing tests to the owning plan.
3. The `npm run ci` command as a single invocation therefore does not exit 0 on this machine (CRLF `format:check`, broken `build:assets`); every step passes individually in the form described in the table.

## Deferred follow-up

Deferred follow-up items (D-20; all need a human or real hardware):

1. **First visual baseline generation and acceptance.** `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` (Docker or Podman running), review the 60 images in `sources/ReactApp/tests/e2e/__screenshots__/`, then commit them naming each screen. Pixel comparison runs afterwards with `UTF_VISUAL_BASELINES=1`.
2. **Screen-reader pass.** Walk the fleet, the workspace, the create flow and the Automation panel with NVDA (Windows) or VoiceOver (macOS) and record findings; live-region roles are asserted, the spoken result is not.
3. **Real phone check at 320 and 390 px.** Open the fleet, a workspace section, the create flow and the Automation panel on a physical phone and record layout, touch-target and browser-chrome issues. The touch-target gap above is one candidate to confirm.
4. **Real-environment create to Running.** Run the seeded `tests/uat` journeys with `npm run test:uat` against a real provider and worker; the `Deferred follow-up:` skipped test in `tests/uat/journeys.spec.ts` is the entry point.
5. **Native `beforeunload` prompt behavior.** In a real browser, edit a form that shows the unsaved-change bar (for example a configuration profile) and close or reload the tab to confirm the native leave prompt appears; Playwright cannot show it.
6. **German hero balance.** A human reviews screenshots of the public hero pages and the restyled calendar controls in German for balance and wrapping (backstop from 69-22 and 69-25).

Also for the pipeline owner: the 30-minute `react-browser` budget is exceeded (see above), and the shared `node_modules` needs a repair.

## Known Stubs

None.

## Threat Flags

None. T-69-41 is mitigated as planned: `allowedRemainders` lives in a reviewed file, the test names any entry outside it, and ceilings equal counts so any increase fails. No package was installed (T-69-SC).

## Self-Check: PASSED

- `rules.json` contains `allowedRemainders` once; `ratchet.test.ts` references it six times; `phase69` appears 19 times in `parity-matrix.test.ts`; `grep -c "| pending |"` on the matrix is 0: FOUND.
- Commits `5b6d3171`, `d0faccfe`, `ca70c8f2`, `77a0e29d` are ancestors of HEAD: FOUND.
- `npx tsc -b` exit 0, `npm run lint` clean, `npm run locales:check` and `locales:unused` 0 failures, full `npm test` 3266 passed: confirmed after the last code commit.

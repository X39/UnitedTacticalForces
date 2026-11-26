---
phase: 70-ui-polishing
plan: 02
subsystem: ui
tags: [react, focus-management, i18n, german-glossary, vitest, playwright]
requires: []
provides:
  - Modal dialogs own Tab over the open community menu (WR-01)
  - loadMediaAsset returns exactly null for a missing asset (IN-08)
  - Distinct "Confirmed team" wording in en and de (WR-02)
  - One German term for mod packs (Mod-Paket) and terrains (Karte) with a guard test (IN-06)
affects: [70-ui-polishing]
tech-stack:
  added: []
  patterns:
    - "Hook guards use dialog[open] because every dialog opens through showModal() and jsdom lacks dialog:modal"
    - "Glossary guard test with a key-to-reason allowlist and an in-test negative sample"
key-files:
  created:
    - sources/ReactApp/tests/locales/de-glossary.test.ts
    - sources/ReactApp/tests/locales/de-glossary-allowed.json
    - sources/ReactApp/tests/locales/distinct-wording.test.ts
  modified:
    - sources/ReactApp/src/components/useDrawerFocus.ts
    - sources/ReactApp/src/features/admin/siteImages.ts
    - sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/shared-words-allowed.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/shell-layouts.test.tsx
    - sources/ReactApp/tests/e2e/public-shell.spec.ts
    - sources/ReactApp/tests/admin-site-images.test.ts
    - sources/ReactApp/tests/admin-media-pages.test.tsx
    - sources/ReactApp/tests/admin-asset-pages.test.tsx
    - sources/ReactApp/tests/hosting/create-fields.test.tsx
    - sources/ReactApp/tests/hosting/pending-start-notice.test.tsx
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts
    - sources/ReactApp/tests/e2e/journeys/german-locale.spec.ts
    - sources/ReactApp/tests/uat/journeys.spec.ts
key-decisions:
  - "WR-01 guard is a single early return on document.querySelector('dialog[open]') in cycleTab"
  - "German terrain noun becomes Karte (feminine) with Karten- compounds; Modpack/Modpaket become Mod-Paket"
  - "Team is registered as a shared word (same word in German) rather than rewording Bestätigtes Team"
requirements-completed: [D-16]
status: complete
duration: 55min
completed: 2026-11-26
commits: 3
plan_head_before: b803c3e9fba5523e4c80c759050f6b8f460d90d0
plan_head_after: 384eaaf0e3a285327d3fe9981c4f3146d756c3ce
actuals:
  tokens: 9000
  tasks: 3
  commits: 3
---

# Phase 70 Plan 02: Phase 69 review leftovers Summary

Closed the four Phase 69 review leftovers with regression tests: a modal-dialog guard in the drawer Tab cycle, a null contract for `loadMediaAsset`, a distinct "Confirmed team" label, and a closed German glossary (Mod-Paket, Karte) enforced by a test.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: Tab belongs to the modal dialog over the open community menu (WR-01) | 464c8a06 |
| 2 | loadMediaAsset null contract and distinct Confirmed team wording (IN-08, WR-02) | 7550ba5d |
| 3 | German glossary pass with its guard (IN-06) | 384eaaf0 |

## What changed

- **WR-01 (tracer).** `cycleTab` in `useDrawerFocus.ts` returns early while a `dialog[open]` exists. The source scan from the plan still printed 0 non-modal `.show()` calls and only two native dialogs exist, so the guard is modal-only. Two jsdom cases in `shell-layouts.test.tsx` cover the guarded and unguarded paths. A Playwright test at 390 by 844 opens the sign-in dialog over the open menu and asserts Tab stays inside `dialog[open]`. Tracer gate: the e2e test was verified to fail with the guard removed (mutation) and pass with it, then the whole `public-shell.spec.ts` ran green (32 passed).
- **IN-08.** `loadMediaAsset` returns `Promise<MediaAsset | null>` with JSDoc, and `GalleryAssetPicker` passes the call result directly to `useQuery`. The existing 404 test now asserts `toBeNull()`.
- **WR-02.** `Pages_Events_Detail_Status_ConfirmedTeam` is "Confirmed team" (en) and "Bestätigtes Team" (de). `distinct-wording.test.ts` asserts both files.
- **IN-06.** All 92 German values that used Modpack, Modpaket or Gelände were rewritten (Mod-Paket / Mod-Pakete, Karte / Karten and Karten- compounds, with articles and pronouns adjusted for the feminine noun). `de-glossary.test.ts` fails on any retired term unless the key sits in `de-glossary-allowed.json` (empty) with a reason, checks that allowlist keys exist, and reports an in-memory "Modpack-Filter" sample so it cannot pass vacuously. Six dependent tests were updated to the new values.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Playwright test did not discriminate the guard**
- **Found during:** Task 1 tracer gate (mutation check)
- **Issue:** Chromium focuses the `<dialog>` element itself on a blank click, so the drawer cycle already returned early and the test passed without the guard.
- **Fix:** After the blank click the test blurs the active element so focus sits on the body (the state WR-01 describes), asserts that, then presses Tab. With the guard removed the test fails; with it present it passes.
- **Files modified:** `sources/ReactApp/tests/e2e/public-shell.spec.ts`
- **Commit:** 464c8a06

**2. [Rule 3 - Blocking] Type and checker fallout outside the plan's file list**
- **Found during:** Tasks 2 and 3
- **Issue:** `admin-media-pages.test.tsx` mocked `loadMediaAsset` with `undefined` (type error after the contract change). `locales:check` rejected "Team" in the German value as an English carryover. The old `Modpack` entry in `identical-allowed.json` became stale and warned.
- **Fix:** The mock resolves `null`; `team` was added to `shared-words-allowed.json` ("same word in German"); the stale `Modpack` entry was removed from `identical-allowed.json`. The `en.json` change stays limited to ConfirmedTeam.
- **Files modified:** `sources/ReactApp/tests/admin-media-pages.test.tsx`, `sources/ReactApp/src/locales/shared-words-allowed.json`, `sources/ReactApp/src/locales/identical-allowed.json`
- **Commits:** 7550ba5d, 384eaaf0

**3. [Rule 1 - Bug] Broken German sentence in a touched value**
- **Found during:** Task 3
- **Issue:** `Pages_Data_Terrains_All_ConfirmDeleteTerrain_Body` read "Sind Sie sicher, dass sie das Gelände Löschen möchten?" (formal address, wrong capitalization), which cannot take a simple noun swap and conflicts with the informal du rule.
- **Fix:** Now "Bist du sicher, dass du die Karte löschen möchtest?".
- **Commit:** 384eaaf0

**4. Existing test updated instead of added (IN-08).** `tests/admin-site-images.test.ts` already held a 404 case asserting `undefined`; it was changed to `toBeNull()` rather than adding a duplicate case.

## Verification

- `shell-layouts`, `admin-site-images`, `admin-media-pages`, `tests/locales`: pass.
- `locales:check`: 0 failures, 0 warnings. `locales:unused`: 0 failures (46 pre-existing unused-key warnings).
- `tsc -b` (typecheck) and eslint on all touched files: clean.
- Playwright `public-shell.spec.ts` (32 passed), `hosting-change.spec.ts` and `journeys/german-locale.spec.ts` (14 passed).
- Full vitest run: 3338 passed, 7 failed in `tests/hosting/automation-consent.test.tsx` and `tests/hosting/metrics-performance.test.tsx` (5 s timeouts and a 200 ms performance backstop under parallel-worktree CPU load). Both files pass in isolation (12 of 12), and neither reads any value this plan changed.

## Deferred / environment notes

- `npm run format:check` flags all 635 files at the unmodified base in this worktree (including untouched files), so it could not be used as a signal; touched files pass eslint.
- `sources/ReactApp/src/generated/api.ts` shows as modified in the worktree with a line-ending-only difference (autocrlf); it was not staged or committed.
- `tests/uat/journeys.spec.ts` was updated to the new German value but uses the separate UAT config and was not run (real-stack runs are deferred to milestone end by standing policy).

## Known Stubs

None.

## Threat Flags

None. T-70-02 is mitigated by the `dialog[open]` early return, asserted in jsdom and in the browser.

## Self-Check: PASSED

- Commits 464c8a06, 7550ba5d and 384eaaf0 exist on this branch.
- Created files exist: `tests/locales/de-glossary.test.ts`, `tests/locales/de-glossary-allowed.json`, `tests/locales/distinct-wording.test.ts`.
- Acceptance greps: `dialog[open]` in hook and test; `.show()` scan prints 0; `Promise<MediaAsset | null>` present; fallback removed; ConfirmedTeam values set; German retired-term count 0.

---
phase: 70-ui-polishing
plan: 14
subsystem: ui
tags: [react, files, upload-queue, status-badge, css-tokens, dialogs, playwright]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-07 files.css, FileUploadDropzone and the remove control; 70-12 credentials grid; 70-01 button variants and the ratchet writer; 70-04 gold rules"
provides:
  - "Upload queue as compact rows: path, size, toned status badge, Will replace badge, remove control"
  - "files.css owns every file, upload and file-dialog rule; hosting.css holds none"
  - "Gold new-folder confirm; S-08 states for the upload dialog with collisions and the new folder dialog"
affects: [70-17, 70-18, 70-19, 70-20]

tech-stack:
  added: []
  patterns:
    - "Status to StatusBadge tone map (uploadStatusTone) instead of dot-joined status text"
    - "Native progress bar drawn with ::-webkit-progress-* and ::-moz-progress-bar, because the style guard forbids accent-color"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
    - sources/ReactApp/src/components/server/files.css
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/hosting/files-panel-controls.test.tsx
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - sources/ReactApp/tests/e2e/support/screens/adjacentScreens.ts

key-decisions:
  - "The queue list takes its natural height and the dialog body (already a scrolling, focusable region) scrolls; no nested scroll container, which would add a keyboard stop and an axe scrollable-region obligation"
  - "The inline preservation Add stays variant primary: it shares its view with the gold toolbar Upload, and the UI-SPEC allows at most one gold per view"
  - "New folder, leave and delete dialogs keep the shell width (30rem token); the upload dialog uses --utf-review-dialog-width"

patterns-established:
  - "Queue row: grid icon, name block, end block; at 520px and below the end block wraps under the name"

requirements-completed: [D-14]

duration: ~75 min
completed: 2026-11-26
commits: 2
plan_head_before: 7185d04a3907eda83deb04f673f9d7e185f61721
plan_head_after: 34e7301349c034d40d7c67b909694af6f9e9f2fc
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 70 Plan 14: Upload queue rows and the remaining file styles Summary

The upload queue is a compact list of rows with a toned status badge, the new-folder dialog confirms with its single gold action, and no file, upload or file-dialog rule is left in hosting.css.

## Accomplishments

- Task 1 (tracer, 5989be87): each queue item is `li.file-upload-queue__row` (the `next-hosting-upload-queue` hook stays on the list) with the path in Body 700 that wraps, the size in muted Body, a `StatusBadge` (queued and cancelled neutral, uploading info, succeeded success, failed and rejected danger) with the existing `React_ServerFiles_Status_*` text, the error line, the Will replace badge and the remove control while queued. The summary line (`file-upload-summary`, polite live region, progress bar, danger Cancel uploads while running) sits above the list, and the approval checkboxes follow it inside the same dialog body. Queue, summary and error rules moved from hosting.css to files.css in token form; the hosting.css ceiling fell from 59 to 52 hex colours and from 13 to 11 off-grid pixel literals. Tracer gate: vitest (`files-panel-controls` and `tests/styles`) and the Playwright spec passed before expansion. The new 390x844 spec (en and de) queues five files (two collisions, one 90-character name) and asserts five status badges, two Will replace badges, no overflow on the dialog, the list and every row, no horizontal page scroll, and the approval checkbox and the confirm inside the dialog box and the viewport.
- Task 2 (34e73013): the new-folder `ConfirmDialog` is `confirmVariant="gold"`; delete and leave keep danger. The remaining rules (`.dialog.next-hosting-upload-dialog` width, the stacked-actions rule at 520px) moved to files.css with tokens, and the dead `next-hosting-file-mutations` selectors (no user in `src`) were deleted. `grep -cE "next-hosting-(file|explorer|upload|tree|immediate)" hosting.css` prints 0 and `confirmVariant="primary"` prints 0 in the panel. S-08 now reaches "upload dialog with a queue and collisions" (server.cfg, a generated launch.cfg and a new file, so both approval checkboxes show) and "new folder dialog".

## Verification

- `vitest run tests/hosting/files-panel-controls.test.tsx tests/hosting/screen-registry.test.ts tests/styles`: 7 files pass. `npm run lint` and `npm run typecheck` clean; `prettier --check ... --end-of-line auto` clean on all edited files.
- Playwright (chromium): `hosting-files-secrets.spec.ts`, `hosting-change.spec.ts` and `journeys/change-mods.spec.ts` 71 passed, 1 flaky (the credentials "focuses the one-time block in view" case at 390x844 de, a one-time `y >= 0` assertion under load; passed on retry and on the earlier full run, not related to Files). The first full run of `hosting-files-secrets.spec.ts` with `--project=chromium --project=mobile-390` passed 52 of 52; the spec's tests set their own viewport, so the mobile-390 project added none.
- S-08 audit (`a11y-audit.spec.ts --grep=S-08`) on chromium and mobile-320, en and de: 4 passed, including the two new dialog states.
- The plan's `--grep "S-08"` runs as `--grep=S-08` through the lock helper (same match).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Progress bar used a forbidden `accent-color`**
- **Found during:** Task 1 (style guard)
- **Issue:** the old hosting.css rule used `accent-color: #c4a95d`; moving it to files.css as a token breaks the guard ("draws the browser mark"), and `--gold` is also forbidden in files.css.
- **Fix:** the bar is drawn with `appearance: none` and the `::-webkit-progress-bar`, `::-webkit-progress-value` and `::-moz-progress-bar` pseudo-elements on overlay and info tokens.
- **Files modified:** `files.css`
- **Commit:** 5989be87

**2. [Rule 3 - Blocking] Existing test forbids `32rem` in files.css**
- **Found during:** Task 2
- **Issue:** `files-panel-controls.test.tsx` asserts files.css has no `32rem` (the old explorer minimum); the file-dialog width rule used it.
- **Fix:** the file dialogs drop their own width and use the shell's `--utf-dialog-width` (30rem instead of 32rem); the upload dialog uses `--utf-review-dialog-width` (44rem instead of 46rem).
- **Commit:** 34e73013

**3. [Rule 1 - Bug] Grid rule on dialog forms fought the shell**
- **Found during:** Task 2
- **Issue:** `.next-hosting-file-dialog form { display: grid }` has the same specificity as `.dialog form` (flex column), so the winner depended on stylesheet load order, and a grid form does not let the flex body scroll.
- **Fix:** the rule was dropped; the shell's flex column and gap apply.
- **Commit:** 34e73013

### Plan reading

- "Bounded height that scrolls inside the dialog body" is implemented as the list at natural height inside the dialog body, which already scrolls and is focusable (`bodyLabel`). A second scroll container would need a keyboard stop and a name (a new locale key, which this plan may not add).
- The plan names a "preservation dialog" with a submit Add at `variant="primary"`. That control is an inline form in the Files view (the panel renders it under the card), not a dialog. Making it gold would put two gold buttons in one view, against the UI-SPEC rule "at most one gold per view" and the existing assertion in `files-panel-controls.test.tsx` that Upload is the one gold action. It stays primary. The plan's acceptance grep (`confirmVariant="primary"` is 0) holds because that button uses `variant`, not `confirmVariant`. If the owner wants it gold, the preservation form needs to move into its own dialog first.
- The leave dialog's danger variant is unchanged and is not asserted here (its test lives in `files-panel-location.test.tsx`, outside this plan's file list).
- Orchestrator note (XPF-70-07-2, focusable `bodyLabel` region taking initial focus): the new-folder dialog has no `bodyLabel`, so its first field gets focus normally, and the upload dialog keeps the 70-07 effect that focuses the first choose button. No change was needed.
- The ratchet writer changed `legacy-ratchet.json` in Task 1 only. Task 2 removed rem-only rules, so no further ceiling dropped (59 to 52 hex, 13 to 11 off-grid in total).
- Commits carry the `Co-Authored-By: Claude Sonnet 5.5` line from the session attribution instruction; the executor notes named Opus 5.5.

## Authentication Gates

None.

## Known Stubs

None.

## Deferred Issues

- Deferred follow-up: real-device look of the queue rows and dialogs, a real multi-hundred-file queue (long list scrolls the dialog body, so the summary line scrolls out of view), screen reader pass and the VER-03 baselines are milestone-end human items.
- XPF-70-14-1: `ConfirmDialog` still offers no shared `initialFocus` for a first control inside a `bodyLabel` region (same as XPF-70-07-2); the upload dialog keeps its panel-local focus effect.
- XPF-70-14-2: `hosting-files-secrets.spec.ts` "focuses the one-time block in view above the pending bar at 390x844" (de) failed once on `box.y >= 0` (-13px) and passed on retry; it is a credentials test from 70-12 and timing sensitive under parallel load.

## Threat Flags

None. T-70-21 is mitigated: queued paths stay React text nodes (`strong.file-upload-queue__path`), the upload pipeline, preflight and collision approvals are untouched.

## Self-Check: PASSED

- FOUND commits: 5989be87, 34e73013
- FOUND: `file-upload-queue` in `files.css`, `file-upload-queue__row` in `ServerFilesPanel.tsx`

---
phase: 70-ui-polishing
plan: 09
subsystem: ui
tags: [react, dialog, leave-guard, playwright, css, i18n]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-01 button variants, 70-03 ConfirmDialog closeOnBackdrop/confirmPlacement/dismissVariant, 70-06 SettingsPanel, 70-16 settingsPanels e2e seeding"
provides:
  - "Settings leave dialog in one row: Discard and leave (danger) at the start edge, Stay here (primary, initial focus) and Review changes (gold) at the end edge"
  - "Backdrop click on the leave dialog equals Stay here; on the discard dialog equals Keep editing"
  - "Leave dialog stacked layout at or below 520px in DOM order at the touch height"
  - "dialog-geometry.spec.ts leave describes (en, de) at 1280, 521, 520 and 320px"
affects: [70-17, 70-18]

tech-stack:
  added: []
  patterns:
    - "Dialog-specific action layout lives in change.css under .dialog.dialog--leave, overriding the shared column-reverse shell"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx
    - sources/ReactApp/tests/e2e/audit/dialog-geometry.spec.ts
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts

key-decisions:
  - "No copy change and no padding tightening: the German leave row fits at 521px with the standard button padding"
  - "Width-specific leave geometry tests set the viewport explicitly and run only in the chromium project; the 320x568 German check also runs in mobile-320"

requirements-completed: [D-13]

duration: 35min
completed: 2026-11-26
commits: 2
plan_head_before: bfe88418bc1743f1ff0d814969dceecc4dec594f
plan_head_after: 668ce38c7a9573e84c2446292e7a73f9fcccdd60
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 70 Plan 09: Leave dialog one row and backdrop equals Stay Summary

The Settings leave dialog now shows Discard and leave, Stay here and the gold Review changes in one row with a clear hierarchy, a backdrop click keeps the member on Settings with everything staged, and the discard dialog closes safely on its backdrop as well.

## Accomplishments

- Task 1 (tracer): the leave `ConfirmDialog` gets `confirmPlacement="start"`, `dismissVariant="primary"` and `closeOnBackdrop`; `className="dialog--leave"`, `initialFocus="dismiss"` and the conditional Review `extraAction` are unchanged. The leave rules in `change.css` are replaced: above 520px `.dialog.dialog--leave .dialog__actions` is `flex-wrap: nowrap` with the first action at `margin-inline-end: auto`; at or below 520px it is a DOM-order column with full-width children (the shell's `column-reverse` is overridden) at the 800px touch height. Commit `feat(webapp): lay out the leave dialog in one row and close it on the backdrop`.
- Unit tests (`settings-leave-guard.test.tsx`): button order and variants (danger, primary, gold, none ghost), Stay here focused on open, two buttons and no Review while only the Automation draft is dirty, backdrop pointerdown plus click equals Stay (dialog closes, `?tab=settings` kept, "1 change staged" and the field value 80 kept), and a click inside the dialog box is ignored.
- Browser tests (`dialog-geometry.spec.ts`, en and de): at 1280, 521, 520 and 320px the dialog fits (`checkDialogGeometry`, `expectDialogFitsViewport`), labels match, no label is clipped, no button is ghost; above 520px the three buttons share a row within 1px with Discard at the start edge and Review at the end edge; at 520 and 320px they stack in DOM order, full width, at least 44px high. A second test checks Shift+Tab/Tab order (Stay, Discard, Review) and a real backdrop click at 1280 returning to Settings with the pending bar shown and Slots still 80.
- Task 2: `closeOnBackdrop` on the discard dialog only (`grep -c closeOnBackdrop` prints 2). Unit tests cover the discard backdrop (closes as Keep editing, draft kept) and a negative case for the delete server confirmation (a backdrop click leaves it open). The geometry spec adds the discard backdrop case in both languages and a German 320x568 test (no clipping, last action `Änderungen prüfen` in the viewport) that runs in chromium and mobile-320. Commit `feat(webapp): close the discard dialog on its backdrop`.

## Verification

- `vitest run tests/hosting/settings-leave-guard.test.tsx tests/primitives.test.tsx tests/styles`: 241 passed, 1 skipped (pre-existing skip).
- `tsc -p tsconfig.app.json --noEmit` and eslint on the touched files: clean; prettier `--check --end-of-line auto`: clean.
- Playwright `dialog-geometry.spec.ts` on chromium and mobile-320: 22 passed, 12 skipped (the explicit-width tests skip outside chromium by design); `hosting-change.spec.ts` plus `dialog-geometry.spec.ts` on chromium: 27 passed.
- RED confirmed first: the three new leave unit tests (order and variants, the automation-only button list, backdrop) and the discard backdrop test failed before the props and CSS landed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `hosting-change.spec.ts` asserted the old leave action order**
- **Found during:** Task 1 verification (`hosting-change.spec.ts` is part of the plan's verify command)
- **Issue:** the leave guard test expected `Stay here, Discard and leave, Review changes` via `toHaveText`, which the D-13 order change breaks (en and de).
- **Fix:** the `leaveActions` copy arrays now list Discard, Stay, Review, and the three click indices follow the new order. No behavior or copy change.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-change.spec.ts`
- **Commit:** included in the Task 1 commit

**2. [Rule 1 - Bug] Geometry spec navigated outside the app base path**
- **Found during:** Task 1 (first Playwright run)
- **Issue:** `page.goto(workspaceRoute("settings"))` keeps its leading slash and escapes the configured base URL, so the app never loaded (all new tests timed out waiting for the Slots field).
- **Fix:** strip the leading slash, as the other audit specs do.
- **Commit:** included in the Task 1 commit

The German 521px row fit without tightening the action padding, so no padding change was made and no copy was shortened.

## Auth Gates

None.

## Known Stubs

None.

## Threat Flags

None. T-70-14 is mitigated: the backdrop maps only to Stay and Keep editing, discarding needs the danger button, and the delete confirmation does not opt in (unit tested).

## Notes

- `src/generated/api.ts` received line-ending-only changes from the e2e builds and was restored, never committed.
- Full-suite timeouts were not encountered; only the targeted vitest files above were run.
- Real-device and visual-baseline (VER-03) checks remain deferred to milestone end.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx (`confirmPlacement="start"` 1, `dismissVariant="primary"` 1, `closeOnBackdrop` 2)
- FOUND: `dialog--leave` in sources/ReactApp/tests/e2e/audit/dialog-geometry.spec.ts
- FOUND commits: 9046cc83, 668ce38c

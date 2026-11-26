---
phase: 70-ui-polishing
plan: 13
subsystem: webapp
tags: [react, hosting, review-dialog, css-grid, disclosure, accessibility, playwright]
requires:
  - phase: 70-01
    provides: tokens (--utf-review-name-min, --utf-review-dialog-width, compact control height)
  - phase: 70-03
    provides: inline Disclosure
  - phase: 70-09
    provides: shared dialog shell use, leave dialog order, change.css leave rules
  - phase: 70-11
    provides: Configuration panel that serves the typed fields the capacity test stages
provides:
  - Compact review row (name, before/after on one line, icon-only Remove) in ChangeDiffItem
  - Review dialog on the shared shell with a width-only class, a compact note and caption group headings
  - Mod pack contents diff behind a closed inline disclosure
  - Capacity and scroll geometry tests for 10 and 40 staged settings
affects: [review dialog, Settings section, mod pack panel tests, hosting-change and journey e2e]
tech-stack:
  added: []
  patterns:
    - "A row that holds visually hidden (position: absolute) terms is position: relative, so a scrolling dialog body contains them"
    - "A stylesheet unit test reads the CSS from disk, strips comments and asserts the one dialog--review rule sets only the width"
key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/server/ChangeDiffItem.tsx
    - sources/ReactApp/src/components/server/ReviewChangesDialog.tsx
    - sources/ReactApp/src/components/server/ModPackDiff.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/src/components/server/mods.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx
    - sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/e2e/audit/dialog-geometry.spec.ts
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts
    - sources/ReactApp/tests/e2e/journeys/change-mods.spec.ts
    - sources/ReactApp/tests/e2e/journeys/german-locale.spec.ts
decisions:
  - "The review row keeps the dl with visually hidden Before and After terms, so the accessible reading is unchanged while the sighted layout is one line"
  - "The row's remove control uses a negative block margin of --utf-space-xs, so a single-line row is about 41px high while the 32px (44px at or below 800px) target is kept"
  - "The review dialog does not call checkDialogGeometry: its facts probe matches the first dl, which here is a row value pair; the spec asserts the single left edge and the action edge itself"
  - "The mod diff disclosure drops the inline box (border, fill, inline padding) through two-class selectors in mods.css, so it reads as a line under the values"
metrics:
  completed: 2026-11-26
  tasks: 3
status: complete
commits: 3
plan_head_before: 7185d04a3907eda83deb04f673f9d7e185f61721
plan_head_after: 6c40d740e4fbc1cd421590500ccd75d91410f51d
actuals:
  tokens: 7700
  tasks: 3
  commits: 3
---

# Phase 70 Plan 13: Review dialog that scales Summary

The review dialog is one compact row per change on the shared dialog shell, with a one-line note, caption group headings and the mod contents diff folded behind an inline disclosure; ten staged settings fit at 1280x900 without scrolling and forty scroll inside the body with the title and actions fixed.

## What was built

**Task 1 (tracer), commit 2b689a94.** `ChangeDiffItem` renders `li.change-item` as a three-column grid (`minmax(var(--utf-review-name-min), 1fr)` name, `minmax(0, 2fr)` values column, `auto` Remove). The values column holds the before and after pairs on one wrapping line (before muted with line-through, decorative `ArrowRight16Regular`, after Body 700), then the outcome line (applied, failed with reason, not tried) and the `children` slot (the mod diff). The `dl` stays with `dt` Before and After carrying `sr-only`; a credential row still renders only the after pair. Remove is `ActionButton variant="ghost" size="compact" iconOnly` with `Dismiss16Regular`, the `Server_Change_Item_Remove_Name` label and `data-remove-item`. At or below 520px the name and Remove share the first line and the values take the second. `Server_Change_Item_Remove` was deleted from both locale files and from the Phase 65 key lists in `tests/locales/server-keys.test.ts` (count 162 to 161). The geometry spec serves ten typed Integer entries, stages all ten, opens the review and asserts that the body does not scroll and each row is at most 48px.

**Task 2, commit c84ac45f.** All re-declared review shell rules are gone from `change.css`; one rule (`.dialog.dialog--review { width: min(var(--utf-review-dialog-width), calc(100vw - var(--utf-dialog-gutter))) }`) remains. The info `StatePanel` is replaced by `p.review-note` (aria-hidden `Info20Regular` plus the take-effect sentence; the shared dialog paragraph rule mutes it). Group headings are Label 12/700 captions with a 1px divider, not sticky. Initial focus, the focus after a removal and the results-line focus are unchanged. A unit test reads `change.css` from disk and asserts there is exactly one `dialog--review` rule that sets only the width. The geometry spec adds forty staged settings at 1280x900 (body scrolls, dialog itself does not, title and actions keep their position while the body scrolls to the end) and then the same dialog at 320x568 (Apply reachable, no sideways scroll).

**Task 3, commit 6c40d740.** `ModPackDiff` wraps the Added, Removed and Unchanged groups in an inline `Disclosure` with local state, closed by default, titled `Server_Change_ModDiff_Toggle` with the count of added plus removed mods (`_one` "Show {{count}} mod change", `_other` "Show {{count}} mod changes"; German "{{count}} Mod-Änderung anzeigen" and "{{count}} Mod-Änderungen anzeigen"). The loading, failed, unknown and same-mods lines stay outside it. `mods.css` removes the inline box so the disclosure sits in the row as one line.

## Verification

- `npx vitest run tests/hosting tests/styles tests/locales --maxWorkers=2`: 129 files, 2592 passed, 1 skipped. The plan's narrower commands (`review-changes-dialog`, `change-flow`, `mod-pack-diff`, `tests/styles`) pass as part of that run. The files were run with `--maxWorkers=2` because a full run times out under load.
- `npm run locales:check`: 0 failures. `npm run locales:unused`: no new warning for the added keys; the 46 warnings are existing keys without a source reference.
- `eslint` over `src`, the touched test folders and the touched specs, and `tsc --noEmit`: clean. Prettier applied with `--end-of-line auto`.
- E2E (serialized through the lock helper): `hosting-change.spec.ts`, `audit/dialog-geometry.spec.ts`, `journeys/change-mods.spec.ts` and `journeys/german-locale.spec.ts` on `--project=chromium --project=mobile-320`: 47 passed, 16 skipped (the viewport-setting specs skip outside chromium). That includes the existing axe scan with the review dialog open.

## Deviations from Plan

**1. [Rule 1 - Bug] Visually hidden terms made the dialog itself scroll.** The first run of the forty-row test failed: the dialog's `scrollHeight` was 1803 against 867 while the body scrolled correctly. The `sr-only` `dt` elements are `position: absolute`, the scrolling `.dialog__body` is not positioned, so their containing block was the dialog and they were not clipped by the body. Fix: `.change-item { position: relative }` in `change.css`, with a comment. Found by the new geometry test.

**2. [Rule 3 - Blocking] `checkDialogGeometry` cannot be used for the review dialog.** Its facts probe is `.dialog__facts, dl` and matches the first visible `dl`, which in this dialog is a row value pair (left edge 518 vs kicker 308). The helper lives in `tests/e2e/support/a11y.ts`, outside this plan's files, so the spec carries its own `expectReviewEdges` (kicker, title, group heading and row share one left edge; actions end at the container edge).

**3. [Rule 3 - Blocking] Tests that asserted the always-open diff needed to open the disclosure.** `tests/hosting/mod-pack-panel.test.tsx` (three review cases), `tests/e2e/hosting-change.spec.ts`, `tests/e2e/journeys/change-mods.spec.ts` and `tests/e2e/journeys/german-locale.spec.ts` now click the "Show N mod changes" toggle before asserting the groups. `tests/locales/server-keys.test.ts` lists the two new keys (count now 163). These files were not in the plan's file list.

**4. [Design choice] Row height bound.** The 48px single-line bound is asserted at 1280x900 (the negative margin on the Remove control keeps the 32px target inside the row padding; the spec asserts the 48px bound, not an exact height). At or below 800px the shared icon-only control is 44px by design, so a row there is taller than 48px; the plan's capacity contract is stated at 1280x900 for the 10-row check and only requires wrap-only growth above 520px, which holds at desktop widths.

**5. [Rule 3 - Blocking] Project selection.** The plan's e2e command lists `--project=mobile-320`; the viewport-setting geometry specs skip there (they set their own viewport), so the 10 and 40 row checks run under `--project=chromium`, the 320x568 check inside the 40-row test by resizing the viewport. The mobile-320 project still ran the specs that apply to it.

No auth gates. No threat flags: credential rows still show only the after placeholder (T-70-20); the existing review tests assert the typed value is never in the dialog and a new case asserts it for a credential-only row.

## Known Stubs

None.

## Deferred Items

Real-device, screen-reader and visual-baseline (VER-03) checks stay deferred to milestone end. `hosting-cost-alerts.spec.ts` (stale toast role assertion) and the mobile axe `target-size` flake on `.section-navigation__link` are pre-existing and were not touched.

## Self-Check: PASSED

- 2b689a94, c84ac45f and 6c40d740 are ancestors of HEAD; all planned source files and the specs are modified and committed.
- `grep -c Dismiss16Regular ChangeDiffItem.tsx` is 2, `grep -c iconOnly ChangeDiffItem.tsx` is 1, `"Server_Change_Item_Remove":` appears 0 times in en.json, `dialog--review (.kicker|h2|p|form|...)` appears 0 times in change.css, `review-note` appears in `ReviewChangesDialog.tsx` and `"Server_Change_ModDiff_Toggle_one"` once in en.json.

---
phase: 70-ui-polishing
fixed_at: 2026-11-26T00:00:00Z
review_path: A:/dev/git/UnitedTacticalForces/.planning/phases/70-ui-polishing/70-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 6
skipped: 1
status: partial
---

# Phase 70: Code Review Fix Report

**Fixed at:** 2026-11-26
**Source review:** A:/dev/git/UnitedTacticalForces/.planning/phases/70-ui-polishing/70-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7
- Fixed: 6
- Skipped: 1

**Verification environment:** All fixes were edited, tested and committed in the main checkout, not in an isolated
worktree, because a fresh worktree has no `node_modules` and the vitest, lint and typecheck gates could not run there.
The parent session was waiting on this run, so nothing else touched the tree. Each fix ran from `sources/ReactApp`: the
touched test files, `npx vitest run tests/styles`, `npm run -s lint` and `npm run -s typecheck`, all green. Playwright
was not run. For WR-01, WR-03, IN-01, IN-02 and IN-03 the new test was also run against the pre-fix code and failed there.

## Fixed Issues

### WR-01: A filter chip removes the field the member is editing, dropping focus and the rest of the typed value

**Files modified:** `sources/ReactApp/src/components/server/configurationFilterRules.ts`,
`sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx`,
`sources/ReactApp/tests/hosting/configuration-filter.test.ts`,
`sources/ReactApp/tests/hosting/configuration-panel.test.tsx`
**Commit:** 10d564f4
**Status:** fixed: requires human verification (interaction and state logic)
**Applied fix:** `filterEntries` takes an optional `stickyKeys` set that always passes the chip test (the query still
applies), and a new `chipKeys` helper lists the keys behind a chip. The panel keeps a render-adjusted `pinned` state
that unions every key that matched the pressed chip since it was pressed, so a row stays listed while the member types
into it or undoes its change. The chip counts stay live. The pure rule without `stickyKeys` is unchanged, so the
existing "judges the staged value over the saved one" test still holds; new pure tests cover `stickyKeys` and
`chipKeys`. Two interaction tests cover typing under Needs a value (value, focus and live count) and Undo change under
Changed.

### WR-02: A file dropped next to the upload drop zone navigates the browser away from the app

**Files modified:** `sources/ReactApp/src/components/primitives.tsx`,
`sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx`, `sources/ReactApp/tests/primitives.test.tsx`,
`sources/ReactApp/tests/hosting/files-panel-controls.test.tsx`
**Commit:** 48114bd0
**Applied fix:** Instead of a layout-changing wrapper div, `ConfirmDialog` gained an opt-in `blockFileDrops` prop that
cancels the browser default for `dragover` and `drop` on the whole `<dialog>`, backdrop included. It acts only for drags
that carry `Files`, so text drags keep their default, and it skips events the drop zone already handled, so the zone
keeps its `copy` drop effect. The upload dialog opts in. Tests cover the primitive (opt in, no files, no opt in, zone
precedence) and the upload dialog (stray drag and drop on the limit line and the dialog, nothing queued).

### WR-03: Focus is lost when a queued upload is removed from the dialog

**Files modified:** `sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx`,
`sources/ReactApp/tests/hosting/files-panel-controls.test.tsx`
**Commit:** 004c53a1
**Applied fix:** `removeUpload` records the next removable (queued) row, or the previous one, and an effect on
`uploadQueue` focuses that row's remove button (marked with `data-upload-remove`). When no removable row is left, focus
moves to the first choose control of the drop zone. A test removes two files in turn and asserts both focus targets.

### IN-01: The disclosure chevron only rotates when motion is allowed, so reduced-motion members get no visual open state

**Files modified:** `sources/ReactApp/src/components/server/form-fields.css`, `sources/ReactApp/tests/disclosure.test.tsx`
**Commit:** 3079457b
**Applied fix:** The `rotate(180deg)` rule moved out of the `prefers-reduced-motion: no-preference` block; only the
`transition` stays inside it. A CSS source test asserts the split.

### IN-02: The configuration search lower-cases with the browser's default locale

**Files modified:** `sources/ReactApp/src/components/server/configurationFilterRules.ts`,
`sources/ReactApp/tests/hosting/configuration-filter.test.ts`
**Commit:** b3b15cc4
**Applied fix:** `normalize` uses `toLowerCase()` and the doc comment now says the result does not depend on the
browser locale. The test simulates a Turkish `toLocaleLowerCase` and checks that `INFO` still matches `info line`.

### IN-03: `keepContextFocusRef` can stay armed after a failed context switch

**Files modified:** `sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx`,
`sources/ReactApp/tests/hosting/files-panel-location.test.tsx`
**Commit:** 1bfe67cc
**Status:** fixed: requires human verification (focus-rule logic)
**Applied fix:** The focus effect now settles the armed flag on the first render of the new PUSH location, before the
`loaded` check, and remembers that location's key. That location still skips the heading focus once it loads, and a
later unrelated navigation gets its heading focus. The test switches to a live context whose listing fails, then
navigates to a folder and expects the folder heading to take focus.

## Skipped Issues

### IN-04: The global test setup opens every Settings panel, so most hosting tests cannot catch a collapsed-state regression

**File:** `sources/ReactApp/tests/setup.ts:17-20`
**Reason:** Skipped by instruction. Removing the global `seedSettingsPanels()` would mean opting 13 test files that
render Settings panels (and their many tests) into an explicit seed. The collapsed first-visit default is already
covered by `settings-panel.test.tsx` and the 6 files that call `clearSettingsPanels()`, and plan 70-15's S-14 audit
covers the collapsed default. The non-chromium e2e skips are not code changes and remain follow-up coverage.
**Original issue:** A global `beforeEach` opens all five panels for every test, so a new test that forgets
`clearSettingsPanels()` silently runs against a state no member sees on a first visit.

---

_Fixed: 2026-11-26_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

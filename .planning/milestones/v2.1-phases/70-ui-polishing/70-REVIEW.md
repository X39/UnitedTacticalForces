---
phase: 70-ui-polishing
reviewed: 2026-11-26T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - sources/ReactApp/src/components/primitives.tsx
  - sources/ReactApp/src/components/server/configurationFilterRules.ts
  - sources/ReactApp/src/components/server/form-fields.css
  - sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx
  - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
  - sources/ReactApp/tests/disclosure.test.tsx
  - sources/ReactApp/tests/hosting/configuration-filter.test.ts
  - sources/ReactApp/tests/hosting/configuration-panel.test.tsx
  - sources/ReactApp/tests/hosting/files-panel-controls.test.tsx
  - sources/ReactApp/tests/hosting/files-panel-location.test.tsx
  - sources/ReactApp/tests/primitives.test.tsx
findings:
  critical: 0
  warning: 0
  info: 1
  total: 1
  necessity: 0
status: clean
---

# Phase 70: Code Review Report

**Reviewed:** 2026-11-26
**Depth:** standard
**Files Reviewed:** 11
**Status:** clean (only the carried IN-04 info remains)

## Summary

Iteration 2 re-review of the six fix commits (10d564f4..1bfe67cc) against `34999b07`. Every fix was read in the diff
and traced against its surrounding code. The six touched test files were run:
`disclosure`, `configuration-filter`, `configuration-panel`, `files-panel-controls`, `files-panel-location` and
`primitives` (6 files, 232 tests, all passing). Playwright was not run.

All six original findings are resolved and no regression was found.

- **WR-01 resolved.** The `pinned` state in `ServerConfigurationPanel` unions the keys that matched the pressed chip, so a
  row stays listed while the member types into it or undoes its change. The render-phase `setPinned` terminates: after
  one adjustment `pinned.mode === mode` and every live key is already in the set. A chip change goes through the
  `pinned.mode !== mode` branch and rebuilds the set from the live keys only. `clearFilter` and the review-attempt
  reset both call `setMode("all")`, which resets the pinned set on the next render, and pressing a chip again starts a
  fresh set. `filterEntries` without `stickyKeys` keeps its old behavior, and the query still applies to pinned rows. The
  chip counts come from `countEntries` over the whole list, so they stay live. Design note, not a defect: the pinned
  rows are released only when the chip changes, so after Apply or Discard under a chip the list keeps the rows that
  matched earlier while the chip count reads 0 until the member presses another chip. This is the sticky-for-the-life-of-
  the-chip behavior the original finding proposed.
- **WR-02 resolved.** `blockStrayFileDrag` runs on the `<dialog>` for `dragover` and `drop` and acts only for drags that
  carry `Files`. It skips events the zone already handled (`defaultPrevented`), so the zone keeps its `copy` effect.
  The zone prevents the default itself in both handlers, even when disabled. Text drags keep their default. Only the upload
  dialog opts in, and `blockFileDrops` is correctly omitted from the props forwarded to `ConfirmDialogForm`.
- **WR-03 resolved.** `removeUpload` records the next queued row, then the previous one, and the effect on `uploadQueue`
  consumes the pending record exactly once. The effect is a no-op for the status updates during a batch because
  `afterUploadRemoval` is `null` then. The remove button only renders for a queued row while no batch runs, which
  matches the `status === "queued"` filter. The fallback focuses the first button of the drop zone.
- **IN-01 resolved.** The `rotate(180deg)` rule is outside the `prefers-reduced-motion: no-preference` block and only
  the `transition` stays inside it. Both the plain and the heading variant selectors are kept.
- **IN-02 resolved.** `normalize` uses `toLowerCase()`, and the doc comment is corrected.
- **IN-03 resolved.** The effect settles the armed flag on the first render of the new PUSH location, before the `loaded`
  check, and remembers that key. That location still skips the heading focus once its listing loads, and a later PUSH
  with a different key gets the heading focus whether or not the earlier listing loaded. The only path that arms the flag
  is the radio handler, which always navigates with a PUSH (`navigateTo` returns early only when nothing changes, and
  a radio `onChange` does not fire for the selected option).

## Info

### IN-04: The global test setup opens every Settings panel, so most hosting tests cannot catch a collapsed-state regression

**File:** `sources/ReactApp/tests/setup.ts:17-20`
**Issue:** Carried forward unchanged from the previous review; skipped by design because the 70-15 S-14 audit covers the
collapsed default. A global `beforeEach(() => seedSettingsPanels())` opens all five panels for every test; only tests that
call `clearSettingsPanels()` see the real first-visit default. Several e2e specs also `test.skip` outside chromium
(`control-target-size`, `visual-stability`, `dialog-geometry`), so the geometry guarantees of the new controls are checked
in one engine only.
**Fix:** Prefer an explicit opt-in (`seedSettingsPanels()` in the files that need open panels, or a helper such as
`renderSettings({ open: true })`) over a global seed, and track the non-chromium skips as follow-up coverage.

---

_Reviewed: 2026-11-26_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

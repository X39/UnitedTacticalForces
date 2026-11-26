---
phase: 70-ui-polishing
plan: 10
subsystem: webapp
tags: [react, configuration, layout-shift, switch, settings-panel, mods]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-05 labelEnd slot and SwitchField, 70-06 SettingsPanel and panel state, 70-16 e2e seedSettingsPanels"
provides:
  - "ConfigurationEntryField label-row state slot reserved by capability (D-11)"
  - "Boolean configuration entries as switches (D-02)"
  - "Mods panel composed on SettingsPanel id server-mods with summary and badge (D-09)"
  - "Bounding-box e2e test for no layout shift on staging at 390 and 1280px"
affects: [70-09]

key-files:
  created:
    - sources/ReactApp/tests/hosting/configuration-entry-field.test.tsx
  modified:
    - sources/ReactApp/src/components/server/ConfigurationEntryField.tsx
    - sources/ReactApp/src/components/server/configuration.css
    - sources/ReactApp/src/components/server/ModPackPanel.tsx
    - sources/ReactApp/src/components/server/mods.css
    - sources/ReactApp/tests/hosting/configuration-panel.test.tsx
    - sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts

key-decisions:
  - "The slot is actionable when !protectedEntry && onReset !== undefined, never from staged or disabled, so applying never changes its height"
  - "A non-actionable empty slot is display:none (:empty) so a reader or protected entry gets no phantom row; the actionable slot is never hidden"
  - "Above 520px the head row is nowrap with a shrinking label; at or below 520px it stacks label over slot, so an actionable entry is two lines from the start"
  - "Mods summary uses a bdi around the saved or staged title (String(id) fallback), the no-pack label otherwise; stagedKeys is the single key modPack"

requirements-completed: [D-02, D-06, D-09, D-11]

duration: 55min
completed: 2026-11-26
commits: 3
plan_head_before: bfe88418bc1743f1ff0d814969dceecc4dec594f
plan_head_after: 9e1feaa93b090dd142fa45ac9b2d2fb64a53f2f1
actuals:
  tokens: 14000
  tasks: 3
  commits: 3
---

# Phase 70 Plan 10: Configuration state slot, boolean switches, collapsible Mods panel Summary

Configuration fields keep their size and position when a value is staged, because Changed, the reset text, Undo and Reset now sit in a label-row slot reserved by capability; boolean entries are switches that still stage "true" and "false"; the Mods panel is a collapsible Settings panel summarizing the pack.

## Accomplishments

- Task 1 (tracer, `179e8e1c`): one internal slot element `span.configuration-field__state` (with `--actionable` when the member may modify and the entry is not protected) passed as `labelEnd` through `common` and rendered inside `div.form-field__head` in the Contribution branch; the old conditional `configuration-field__actions` row is gone. CSS reserves the compact control height (touch at or below 800px) and stacks the head at or below 520px. The Playwright test `change layout stability` measures every `.configuration-field` and `section.workspace-panel` in page coordinates before and after staging Maximum players at 390x844 and 1280x800 in en and de (0.5px tolerance). Red check: with the slot reservation set to `min-height: 0` the test fails (received deltas of 44px and 11px); with it, all four variants pass.
- Task 2 (`9f656a2f`): the boolean branch is a `SwitchField` with the Enabled / Disabled state words, `checked={value === "true"}`, no `aria-required`, and the slot in its label row. `configuration-panel.test.tsx` migrated; new unit cases cover checked mapping, the staged strings (T-70-15), the slot placement and the disabled/hint wiring.
- Task 3 (`9e1feaa9`): `ModPackPanel` composes `SettingsPanel id="server-mods"`; the unsupported-game null return stays before it; loading, error and empty states moved into `div.mods-panel` in the body (the `.mods-panel select` hook is kept). The `.mods-panel .workspace-panel__head` rule is deleted; no `mods.css` rule touches the shared select padding. Unit cases cover the bdi summary (saved, none, staged), the badge, collapsed-content mounting, auto-open on staging, no render and no request for unsupported games, and a CSS source guard; an e2e test asserts the computed `padding-inline-end` equals the `--utf-space-xl` token (D-06).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Configuration panel header grew when its change badge appeared**
- **Found during:** Task 1 bounding-box test
- **Issue:** The still-plain `.configuration-panel .workspace-panel__head` grows by 6.19px when the change-count badge appears, which moved every field below it and failed D-11 even though the fields themselves did not change.
- **Fix:** Reserved the badge height with `min-height: var(--utf-control-height-compact)` on that header rule in `configuration.css`. Plan 70-09 turns the panel into a `SettingsPanel`; the rule becomes inert there and can be removed with that header.
- **Files modified:** `sources/ReactApp/src/components/server/configuration.css`
- **Commit:** `179e8e1c`

**2. [Rule 1 - Bug] The test measured hidden elements as moved**
- **Found during:** Task 1
- **Issue:** A collapsed (hidden) Advanced field has a zero box, so `rect.top + scrollY` echoed the scroll offset and looked like a 1203px shift.
- **Fix:** The measurement records top 0 for an element without a box.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-change.spec.ts`
- **Commit:** `179e8e1c`

**3. [Scope note] The layout test uses its own describe at 390x844 and 1280x800**
- The plan said "the layout describe", which runs 320 and 390; 1280 is required by the must-haves, so the test sits in a sibling `change layout stability` describe.

## Verification

- `vitest run tests/hosting` (all 115 files, 2196 tests) passed when re-run with `--maxWorkers=2 --testTimeout=30000`; the first full run under parallel-worktree load timed out in 14 unrelated tests (activity-section, automation-consent, automation-schedules), which pass alone.
- `tests/styles` guard, `typecheck`, eslint and prettier (`--end-of-line auto`) clean on the touched files.
- Playwright (chromium + mobile-390): `hosting-change.spec.ts`, `hosting-placement.spec.ts` and `journeys/change-mods.spec.ts` passed (22 tests) via the serialized helper. Full e2e suite: 852 passed, 1 flaky (`audit/route-walk` command-events under load, unrelated) and 1 failed (`hosting-cost-alerts.spec.ts:494`, the known pre-existing stale toast `role="status"` expectation).

## Known Stubs

None.

## Threat Flags

None. T-70-15 (boolean stages only "true"/"false") and T-70-16 (titles as text nodes through ApiText/bdi) are covered by unit tests.

## Self-Check: PASSED

- Files: `ConfigurationEntryField.tsx`, `configuration.css`, `ModPackPanel.tsx`, `mods.css`, `configuration-entry-field.test.tsx`, `mod-pack-panel.test.tsx`, `configuration-panel.test.tsx`, `hosting-change.spec.ts` exist.
- Commits `179e8e1c`, `9f656a2f`, `9e1feaa9` are ancestors of HEAD.

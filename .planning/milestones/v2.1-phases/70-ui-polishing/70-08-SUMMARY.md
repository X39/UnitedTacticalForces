---
phase: 70-ui-polishing
plan: 08
subsystem: ui
tags: [react, forms, chips, switches, forced-colors, a11y, hosting]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "--utf-day-chip-min token (70-01), SwitchField and the chip variant (70-05), SettingsPanel (70-06)"
provides:
  - "Weekday chips laid out four plus three at 520px and below, one row of seven above"
  - "Idle shutdown and Maximum runtime enable controls as switches with Enabled / Disabled state words"
  - "Cost alert In-app, Discord and Email channels as switches"
  - "Forced-colors e2e assertion on the Automation panel"
affects: [70-verification, milestone-end visual baselines]

actuals:
  tokens: 14000
  tasks: 3
  commits: 4
plan_head_before: e6eece4e142655c27046c8515a4d17ed9555f3c4
plan_head_after: 20c0e8bc

tech-stack:
  added: []
  patterns:
    - "Grid column sets chip width: .schedule-rule__day-grid .toggle-chip { min-width: 0 }"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/server/ScheduleRuleFields.tsx
    - sources/ReactApp/src/components/server/automation.css
    - sources/ReactApp/src/features/hosting/AutomationPanel.tsx
    - sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx
    - sources/ReactApp/tests/e2e/hosting-automation.spec.ts

key-decisions:
  - "The chip's own min-width (3rem) overflowed a four-column row at 320px by about 2.5px, so the day grid zeroes it and lets the column size the chip"
  - "Save button stays variant primary (orchestrator decision 1); no change"

requirements-completed: [D-02, D-04, D-05]
---

# Phase 70 Plan 08: Weekday chips and automation/cost alert switches Summary

Weekday checkboxes are now toggle chips in a seven-column grid (four columns at or below 520px), and the Automation enable settings plus the three cost alert channels are `SwitchField`s with Enabled / Disabled state words.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: weekday chips four to a row at 320px | 29f0867e |
| 2 | Automation enable switches and forced-colors check | 74ed7d6e |
| 3 | Cost alert channel switches | 0ea9c4bf |
| - | Follow-up: remaining role-checkbox queries (settings-section, UAT specs) | 20c0e8bc |

## Accomplishments

- Tracer (D-05): `variant="chip"` on the day fields; `repeat(7, minmax(0, 1fr))` with `repeat(4, minmax(0, 1fr))` inside the 520px block; the touch-height rule for `.form-field__check` was removed. New e2e test per culture (en, de) at 320x568, 390x844 and 1280x800 counts distinct chip tops (2, 2, 1), checks four on the first row on phones, containment inside the grid, no clipped chip text and no horizontal page scroll. New unit test for the chip markup and unchanged toggling.
- Tracer gate: the full `hosting-automation.spec.ts` passed (26 passed, 12 skipped screenshot captures) before Task 2 started.
- Task 2 (D-04): two `SwitchField`s keep id, hint, disabled, aria-disabled and describedBy; role queries migrated in unit and e2e specs; new unit test covers the switch role, state words, Space toggling, enabled minutes field and the dirty state. New e2e test emulates `forcedColors: "active"` and asserts a visible border on switch track, chip and radio card, and that a checked switch track and chip differ from the unchecked ones.
- Task 3 (D-02, D-04): three channel `SwitchField`s; In-app stays on, disabled and readOnly; hints and status badges unchanged; read-only viewers get three disabled switches (existing test, now by role switch). No CSS change was needed in `cost-alerts.css`; the legacy `cost-alert-settings.css` is untouched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Chip overflowed the four-column row at 320px**
- **Found during:** Task 1 (new e2e assertion failed: chip right edge 259.5 vs grid edge 257)
- **Issue:** `.toggle-chip` has `min-width: var(--utf-day-chip-min)` (48px), larger than the 320px column.
- **Fix:** `.schedule-rule__day-grid .toggle-chip { min-width: 0 }` in `automation.css`.
- **Commit:** 29f0867e

**2. [Rule 3 - Blocking] Extra test files held stale role-checkbox queries**
- **Found during:** full `tests/hosting` run and a repo grep
- **Issue:** `tests/hosting/settings-section.test.tsx:297` (failed), `tests/hosting/cost-alerts-page.test.tsx:191`, `tests/uat/automation.spec.ts` and `tests/uat/cost-alerts.spec.ts` (Email channel) were not in the plan's file list.
- **Fix:** migrated to role switch. `workspace-page.test.tsx` was left unchanged: its two checkbox queries belong to the Files panel upload dialog, not these controls.
- **Commits:** 0ea9c4bf, 20c0e8bc

**3. Verification project flags**
- The plan's `--project=mobile-390 --project=mobile-320` do not match `hosting-automation.spec.ts` (those projects only match audit, journeys and mobile specs). The new chip tests set their own viewport via `test.use`, so they run in the `chromium` project at all three widths.

## Verification

- Vitest: `tests/hosting` full run 2225 passed, 1 failed (settings-section, fixed afterwards and re-run green with `--maxWorkers=2`); `tests/styles` green; lint and typecheck clean.
- Playwright (chromium, serialized): `hosting-automation.spec.ts`, `hosting-a11y.spec.ts`, `journeys/automation.spec.ts` pass. In the combined run, 8 `hosting-a11y` confirmation dialog tests failed with a missing `dialog[open]` under load (12 workers); re-running `hosting-a11y.spec.ts` alone with 2 workers passed 22 of 22. Treated as load flake; not related to these controls.
- Not run: real-device, screen-reader and visual baseline checks (deferred to milestone end); the UAT specs edited in the follow-up commit need the seeded stack and were not run.

## Known Stubs

None.

## Threat Flags

None. Disabled props moved unchanged onto `SwitchField` and the chip variant (T-70-13).

## Self-Check: PASSED

- Commits 29f0867e, 74ed7d6e, 0ea9c4bf, 20c0e8bc present on the branch.
- `variant="chip"` count 1, `repeat(4, minmax(0, 1fr))` count 1, `repeat(7, minmax(0, 1fr))` count 1, no `CheckboxField` left in `AutomationPanel.tsx` or `CostAlertSettingsPanel.tsx`, `forcedColors` present in `hosting-automation.spec.ts`.

---
phase: 68-auto-stop-schedules
plan: 17
subsystem: ui
tags: [react, schedules, consent, intl, vitest]

requires:
  - phase: 68-auto-stop-schedules
    provides: "Automation panel with draft, save row and polite region (68-14); automationModel and the settings data layer (68-04, 68-12); translation keys (68-04)"
provides:
  - "ScheduleRuleFields: one weekly start or stop rule block with summary, next-run and last-run lines"
  - "Schedules sub-section in AutomationPanel: time zone select, rule list, empty state, Add row with counter, add and remove focus and announcements"
  - "ScheduledStartConsentDialog and ScheduledStartsPausedNotice on top of ConfirmDialog and StatePanel"
  - "Save routing through needsStartConsent, and reconfirmation of paused scheduled starts for managers"
affects: [68-18, 68-19, phase-68-verification]

actuals:
  tokens: 33000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Rule next-run and last-run lines describe the saved rule matched by id; an edited or new rule reads Not saved yet."
    - "Focus after add and remove goes through a pending-focus ref consumed by an effect after the list rendered"
    - "Consent dialog is mounted only while open; the panel returns focus to Save when it closes"

key-files:
  created:
    - sources/ReactApp/src/components/server/ScheduleRuleFields.tsx
    - sources/ReactApp/src/components/server/ScheduledStartConsentDialog.tsx
    - sources/ReactApp/tests/hosting/automation-schedules.test.tsx
    - sources/ReactApp/tests/hosting/automation-consent.test.tsx
    - sources/ReactApp/tests/helpers/automationPanelHarness.tsx
  modified:
    - sources/ReactApp/src/features/hosting/AutomationPanel.tsx
    - sources/ReactApp/src/components/server/automation.css
    - sources/ReactApp/src/features/hosting/automationModel.ts
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/tests/hosting/automation-panel.test.tsx
    - sources/ReactApp/tests/hosting/automation-model.test.ts
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "The consent dialog opens for any new or changed Start rule via needsStartConsent, regardless of the saved consent state"
  - "A reconfirmation saves the saved settings with startConsent true and leaves a dirty draft untouched; the existing pristine-follow logic picks up the new saved settings"
  - "While the consent dialog is open the status line alerts are suppressed so the failure is told once, inside the dialog"
  - "Zone options are built from Intl.supportedValuesOf plus the saved and the draft zone, so neither is ever lost"

patterns-established:
  - "Rule groups are role=group labelled by the heading paragraph; tests address them by name Schedule N"

requirements-completed: [AUT-03]

coverage:
  - id: D1
    description: "Owner adds, edits and removes weekly start and stop rules in one time zone and saves them with the other automation settings"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-schedules.test.tsx#saves a Friday start and a Saturday stop with ISO days and HH:mm, then shows the saved runs in order"
        status: pass
    human_judgment: false
  - id: D2
    description: "Rule validation, duplicate detection, focus handling, zero/one/limit counter and long German summary"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-schedules.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Scheduled-start consent dialog with cost owner, estimates and start count, error line in the dialog, and the paused notice with reconfirm"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-consent.test.tsx"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-23
status: complete
plan_head_before: d5034250a452a763fa8182295395399396292463
plan_head_after: 2e711c517850a8a59ab39acbb5396c7ad483df75
commits: 3
---

# Phase 68 Plan 17: Schedules and scheduled-start consent Summary

**Weekly start and stop rules with a region-grouped time zone select inside the Automation panel, plus a cost-owner consent dialog and a paused-starts notice with manager reconfirmation.**

## Accomplishments

- `ScheduleRuleFields` renders one rule: Start/Stop radio group with carried labels and help, `type="time"` field, a Monday-first weekday checkbox fieldset, the `Intl` summary sentence (including the long German list form), the next-run line from the saved rule ("Not saved yet." for new or edited rules, "Paused until scheduled starts are confirmed." for Start rules while consent is Needed) and the closed last-run outcome map. A `Failed` outcome adds the Open activity details link; unknown outcomes use the fixed `Last run: {time}.` sentence.
- `AutomationPanel` gains the Schedules sub-section at DOM position 7: `SelectField` time zone (UTC first, region optgroups, saved and draft zone always present, defaults to the browser zone), rule list or the No schedules set panel, Add row with the counter, aria-disabled Add at the limit described by the counter, add/remove focus rules, a polite announcement region, rule validation in the save path with focus on the first invalid field, and display ordering after a save.
- `ScheduledStartConsentDialog` (on `ConfirmDialog`, `initialFocus="dismiss"`, facts through `serverCostFacts`) and `ScheduledStartsPausedNotice` (on `StatePanel kind="warning"`). Save with a new or changed Start rule opens the dialog; Confirm sends one PUT with `startConsent: true`; failures show inside the dialog; Keep editing keeps the draft and returns focus to Save. Paused notice at DOM position 3 with reconfirm for managers.
- Tests: 15 schedule tests and 9 consent tests (English and German), plus a model test for the new helper. Full ReactApp suite: 176 files, 2536 tests pass; typecheck and lint clean.

## Task Commits

1. Task 1 (tracer): `c3e742ae` feat(webapp): edit weekly start and stop schedules
2. Task 2: `79d22b1c` feat(webapp): ask for scheduled start consent
3. Tests: `2e711c51` test(webapp): cover schedules and start consent

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Exported `FieldError` from `FormFields.tsx`**
- **Found during:** Task 1
- **Issue:** The days fieldset needs the shared inline error markup, which was module-private.
- **Fix:** Added `export` to `FieldError` and `FieldErrorProps` (no behavior change) instead of duplicating the markup.
- **Files modified:** sources/ReactApp/src/components/FormFields.tsx
- **Commit:** c3e742ae

**2. [Rule 3 - Blocking] Added `ruleDiffersFromSaved` to `automationModel.ts`**
- **Found during:** Task 1
- **Issue:** The Not saved yet line needs the model's private rule comparison; re-implementing the normalization in the component would diverge.
- **Fix:** Exported a small pure helper and covered it in `automation-model.test.ts`.
- **Files modified:** sources/ReactApp/src/features/hosting/automationModel.ts, sources/ReactApp/tests/hosting/automation-model.test.ts
- **Commit:** c3e742ae

**3. [Rule 3 - Blocking] Icon mock and an existing panel test**
- **Issue:** `tests/mocks/react-icons.tsx` lacked `Delete16Regular`; `automation-panel.test.tsx` used `getByRole("status")`, which is no longer unique now that the empty Schedules panel is a status region.
- **Fix:** Added the icon to the mock; the test now asserts the status line by text and role.
- **Commit:** c3e742ae

**4. [Design choice] `firstFieldRef` prop dropped from `ScheduleRuleFieldsProps`**
- The plan interface listed `firstFieldRef`, but `RadioGroupField` exposes no ref. Focus on add and remove queries the first radio of the target `li` (`data-rule-key`) instead, so the shared component stays unchanged. `index` is zero-based; the heading shows it one-based.

**5. [Design choice] Shared test harness**
- Added `tests/helpers/automationPanelHarness.tsx` for the two new test files rather than duplicating the Harness; the existing panel test is untouched apart from deviation 3.

The consent dialog is mounted only while open (like the cost confirmation provider) so the closed form never adds hidden duplicate DOM.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. T-68-40 is mitigated: the only consent path shows cost owner, hourly and monthly estimate and the start rule count before sending `startConsent: true`. T-68-41: client validation mirrors the API and the API stays authoritative.

## Self-Check: PASSED

- Files exist: ScheduleRuleFields.tsx, ScheduledStartConsentDialog.tsx, automation-schedules.test.tsx, automation-consent.test.tsx, automationPanelHarness.tsx.
- Commits present on HEAD: c3e742ae, 79d22b1c, 2e711c51.
- `git diff` touches nothing under sources/Api or sources/WebApp.

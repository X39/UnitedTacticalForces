---
phase: 65-change-a-server
plan: 10
subsystem: ui
tags: [react, hosting, leave-guard, validation, accessibility, vitest]

requires:
  - phase: 65-03
    provides: useLeaveGuard, leavesWorkspaceSection and the ConfirmDialog extraAction
  - phase: 65-07
    provides: the Settings section draft provider, PendingChangesBar and ReviewChangesDialog
provides:
  - Settings leave guard with the three-action dialog (Stay here, Discard and leave, Review changes) and the native unload prompt
  - Discard confirmation, an invalid-field gate before review with derived field errors and focus on the first invalid field
  - Read-only and archived notice, one polite announcement region, credentials panel changed count
affects: [65-11, 65-12, 65-13]

plan_head_before: 41794732e24a694435f6e9b37d12407c0ddcf50d
plan_head_after: 1b22025ad5634cd4fb14a307fbe55c6c3ea9878f

actuals:
  tokens: 7500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Invalid staged settings are derived during render from reviewAttempt and the staged values; nothing is stored, the focus effect is keyed on reviewAttempt"
    - "Field errors in the configuration panel come from configurationEntryIssue, shown for blurred fields or after a review attempt, with aria-invalid and aria-describedby on the raw controls"
    - "Staging announcements are an effect on count transitions that calls the context announce; a ref set by onAllApplied suppresses the empty message after a complete apply"

key-files:
  created:
    - sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/tests/hosting/change-flow.test.tsx

key-decisions:
  - "Review changes in the leave dialog runs the same invalid-fields gate as the bar, so an invalid draft cannot be reviewed through the leave dialog either"
  - "Count-transition announcements use an effect with a previous-count ref instead of render-phase state, because calling the provider's dispatch while a child renders is a React warning"
  - "reviewAttempt is never reset, so after one attempt a newly invalid value shows its error and the bar alert immediately"

patterns-established:
  - "Page-level leave guard harness: the whole ServerWorkspacePage at ?tab=settings with section links, breadcrumb, router.navigate and a deferred apply promise"

requirements-completed: [CHG-05, CHG-03]

coverage:
  - id: D1
    description: "Leaving Settings with staged changes (section link, breadcrumb, Back, navigate) opens Leave without applying? with Stay here focused; Stay and Escape keep the draft and history; Discard and leave clears and navigates; Review changes stays and opens the review"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "A same-section replace or hash change and a clean draft never open the dialog; beforeunload is prevented only while changes are staged and never after Discard and leave or a full apply; window.confirm is never called; the dialog still appears while an apply runs"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx#never asks for a replace or hash change that stays on Settings"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx#still asks with the same outcomes while an apply is running"
        status: pass
    human_judgment: false
  - id: D3
    description: "Discard changes asks first with the singular or plural body and Keep editing focused; discarding empties the draft without a server call and focuses the Settings heading"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#asks before discarding, keeps the draft on Keep editing and discards without calling the API"
        status: pass
    human_judgment: false
  - id: D4
    description: "An invalid staged value blocks the review without disabling the button: alert in the bar, aria-invalid with the error linked by aria-describedby, focus on the field; correcting it clears everything; blur shows the error before any review"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#stops the review for an invalid value, explains the field and moves focus to it"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#names the range limits and shows an error after the field was left"
        status: pass
    human_judgment: false
  - id: D5
    description: "Read-only and archived notices appear once with every configuration control disabled; one polite region announces the first staged change, the emptied draft and panel messages; the credentials head shows the changed count"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#tells a viewer without Modify rights once and disables every configuration control"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#announces the first staged change, the emptied draft and panel messages in one polite region"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#counts staged credentials in the credentials panel head"
        status: pass
    human_judgment: false
  - id: D6
    description: "Native browser unload prompt wording and a real screen reader reading of the polite region cannot be asserted in jsdom"
    requirement: CHG-05
    verification: []
    human_judgment: true
    rationale: "jsdom proves the handler is registered and prevents the event; the browser-owned prompt text and assistive technology output need a real browser pass"

duration: 10min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 10: Leave guard, discard confirmation and invalid-field gate Summary

**Settings now asks Stay here, Discard and leave or Review changes before any path out while changes are staged, confirms Discard, stops the review for invalid values with a focused field error, tells read-only viewers why controls are disabled, announces staging through one polite region and counts changed credentials.**

## Performance

- **Duration:** 10 min (2026-11-20T21:26Z to 21:36Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 5 (1 created, 4 modified)

## Accomplishments

- Tracer: `SettingsSectionContent` calls `useLeaveGuard({ dirty: count > 0, shouldBlock: leavesWorkspaceSection(..., "settings") })` and renders the three-action `ConfirmDialog` (Stay here focused, Discard and leave clears the draft then proceeds, Review changes is `stay()` followed by the review gate). The guard stays active while an apply runs; the `?action=` cleanup and hash changes never block. The new page-level test file covers ten scenarios including Back with an unchanged history key, `beforeunload` lifecycle and a pending apply.
- Discard changes opens `Discard staged changes?` (singular or plural body, Keep editing focused) and focuses the Settings heading after confirming.
- Validation gate: Review changes marks a review attempt when `validateStagedSettings` finds issues, the bar shows the alert derived during render, the section effect focuses the first `[aria-invalid="true"]`. `ServerConfigurationPanel` renders per-field errors (`Server_Settings_Invalid_*`) for blurred fields or after an attempt, with `aria-invalid` and `aria-describedby`.
- Read-only notice (`Server_Change_ReadOnly` or `_Archived`), one `aria-live="polite"` region fed by count transitions and `announce()`, and a `StatusBadge` count in the credentials head with a `change.css` head layout.

## Task Commits

1. **Task 1 (tracer): ask before leaving settings with staged changes** - `f8f43df8` (feat(webapp))
2. **Task 2: confirm discard, gate review on valid settings, announce staging** - `1b22025a` (feat(webapp))

## Verification Results

- Tracer gate (Task 1): settings-leave-guard, change-flow, leave-guard and deep-link tests green (45), `typecheck` and eslint on touched files clean before expansion. Tracer verified end-to-end, then expanded.
- Task 2 set: change-flow, settings-leave-guard, arma-placement, workspace-page, change-source-scan and tests/styles, 94 tests passed; `locales:check` 3525 keys, 0 failures; `typecheck` clean; full `npm run lint --max-warnings 0` exit 0; Prettier clean on every touched file.
- Acceptance greps: useLeaveGuard 2, leavesWorkspaceSection 2, window.confirm 0, validateStagedSettings 3, Server_Change_DiscardDialog_Title 1, Server_Change_ReadOnly 2, `aria-live="polite"` 1, configurationEntryIssue 2.
- `sources/Common/Ui/routes.json` unchanged; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`.

## Deviations from Plan

### Auto-fixed Issues

None. The deviations below are plan wording that did not match the model.

### Plan wording that did not match the model

- The plan stages "abc" for an integer entry and expects "Enter a whole number."; a number input cannot hold letters (the value is sanitized to empty, which would report "Enter a value."), so the test stages "1.5", which exercises the same `number` code.
- The plan says to update the announcement through render-phase state adjustment; dispatching the provider's reducer while a child renders is a React warning, so the section uses an effect with a previous-count ref (the effect only calls the context `announce`, no local setState).
- `PendingChangesBar.tsx` is listed as modified but needed no change: plan 65-07 already exposes the `alert` slot and the Review button is never disabled.
- The credentials staging control arrives with plan 65-11, so the changed-count test mocks `ServerSecretsPanel` with a stand-in that stages through the draft context; the escape key test dispatches the native `cancel` event, as the existing dialog tests do.
- The old "discards without confirmation" test in `change-flow.test.tsx` was replaced by the confirm-first version.

**Total deviations:** 0 auto-fixed, 5 wording notes. **Impact:** none on scope; every behavior line of both tasks is covered by a test.

## Issues Encountered

None.

## Known Stubs

None. The raw configuration controls still render until plan 65-12 moves the error into the shared entry field; the contribution (Arma placement) fields do not receive `aria-invalid` yet and keep the bar alert as the only signal, which plan 65-12 replaces.

## Threat Flags

None. T-65-27 (leave and discard dialogs and announcements use fixed copy with counts only; no staged value, secret or API text is announced) and T-65-28 (controls disabled for viewers without Modify and for archived servers while the API stays the authority) are mitigated and covered by the tests above.

## Next Phase Readiness

Plans 65-11 to 65-13 can stage credentials, profiles and mod packs on the same draft: the leave guard, discard confirmation and announcements already count every change kind, and `reviewAttempt` plus the `[aria-invalid="true"]` focus effect are ready for the shared entry field in plan 65-12.

## Self-Check: PASSED

- Created and modified files verified on disk; commits `f8f43df8` and `1b22025a` verified as ancestors of HEAD.

---
phase: 65-change-a-server
plan: 07
subsystem: ui
tags: [react, hosting, staged-changes, review-dialog, tanstack-query, vitest, playwright]

requires:
  - phase: 65-02
    provides: serverChangeDraft model, ServerChangeDraftProvider and the 145 Phase 65 copy keys
  - phase: 65-03
    provides: ConfirmDialog bodyLabel, confirmUnavailableReason and the review layout tokens
provides:
  - Settings section owns the staged-change draft; editing a configuration value or a reset stages it instead of saving
  - PendingChangesBar with the count sentence, Review changes and Discard changes
  - ReviewChangesDialog and ChangeDiffItem with grouped Before and After rows for every change kind
  - useApplyServerChanges (sequential apply, exact-key invalidation, toast, per-item results) and useReviewGroups
  - change.css (section reserve, sticky bar, review dialog geometry, diff rows)
  - Source-scan guard test for the draft, bar, dialog and apply files
affects: [65-08, 65-09, 65-10, 65-11, 65-12, 65-13]

plan_head_before: 402fbf8b55a654debc2496ba1ca7d31f88d6e8f4
plan_head_after: b327a9a2fa846d1bea7a9bd2747e5ab48b3ae868

actuals:
  tokens: 23500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Apply is a variable-free mutation (gcTime 0); the plan, which can hold typed credential values, lives in a ref only for the run and is cleared in onSettled"
    - "Saved queries are invalidated with exact keys before the draft clears, so inputs never flash the old saved value"
    - "Results line is the ConfirmDialog error alert (role alert, focused); it is cleared while a retry runs so the same text re-focuses"
    - "Focus after removal is resolved after re-render through data-remove-item on the remove controls"

key-files:
  created:
    - sources/ReactApp/src/components/server/PendingChangesBar.tsx
    - sources/ReactApp/src/components/server/ReviewChangesDialog.tsx
    - sources/ReactApp/src/components/server/ChangeDiffItem.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/src/features/hosting/useServerChangeApply.ts
    - sources/ReactApp/tests/hosting/change-flow.test.tsx
    - sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx
    - sources/ReactApp/tests/hosting/change-source-scan.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx
    - sources/ReactApp/src/components/ActionControl.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/arma-placement.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/e2e/hosting-placement.spec.ts
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "No batch apply endpoint: per-item results come from sequential calls to the existing audited endpoints (UI-SPEC unresolved row resolved as planned)"
  - "A minimal invalid-fields gate sits at Review changes (alert in the bar, dialog not opened); focus movement and per-field errors stay with plan 65-10"
  - "The dialog closes through render-phase state adjustment when the draft empties, so a later staging never reopens it"
  - "A complete apply run stores no results, so Applied states can never decorate a later change"
  - "ActionButton props now accept ref (ComponentPropsWithRef) so the bar can forward reviewRef"

patterns-established:
  - "SettingsSection is a thin provider shell (saved data from the server and the configuration query) around SettingsSectionContent"
  - "Review rows are typed ReviewValue descriptors mapped to copy only in ChangeDiffItem; API text goes through ApiText"

requirements-completed: [CHG-03, CHG-04]

coverage:
  - id: D1
    description: "Editing a configuration value or resetting it to the inherited value stages it; the sticky bar shows the count with Review changes and Discard changes; the original value removes the bar"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#stages a value without saving and shows the pending bar; the original value removes it"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#stages a reset to the inherited value and applies it as a reset"
        status: pass
    human_judgment: false
  - id: D2
    description: "Review dialog lists Before and After from the latest saved data in the fixed group order, with the running or not-running take-effect wording, and applies once through the existing endpoint with a toast, closed dialog and heading focus"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#applies once through the existing endpoint, then toasts, closes and focuses the heading"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-flow.test.tsx#uses the running wording while the server has an active deployment"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#renders the groups in their fixed order with counts and never shows a typed secret"
        status: pass
    human_judgment: false
  - id: D3
    description: "Partial failure keeps failed and not-tried items staged with one of the UI-authored reasons, the results line takes focus, confirm becomes Try n changes again, nothing rolls back, and no raw error text is printed"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#keeps failed and not-tried items with a reason, focuses the results line and retries them"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#maps a forbidden rejection to the forbidden reason"
        status: pass
    human_judgment: false
  - id: D4
    description: "Per-item removal moves focus to the next remove control or the dialog title and closes the dialog when the draft empties; credentials show only the changed or generated notice and the typed value never reaches the DOM"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#removes an item, moves focus on, and closes the dialog when the draft becomes empty"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#describes a generated credential as a new value"
        status: pass
    human_judgment: false
  - id: D5
    description: "While an apply runs the dialog is aria-busy, Escape is blocked, the confirm shows the pending label and the configuration controls are disabled"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx#is busy, blocks Escape and disables the controls until the request settles"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/arma-placement.test.tsx#disables fields while applying and cannot expose a quote before configuration refetch completes"
        status: pass
    human_judgment: false
  - id: D6
    description: "Draft, bar, dialog, diff row, apply hook and section never use browser storage, cookies or console; the last five never read an error text; the apply mutation takes no variables and uses gcTime 0"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/change-source-scan.test.ts"
        status: pass
      - kind: other
        ref: "grep -c 'gcTime: 0' useServerChangeApply.ts returns 2; grep -c '.message' ReviewChangesDialog.tsx returns 0"
        status: pass
    human_judgment: false
  - id: D7
    description: "Placement e2e selects a placement, opens Review changes, applies 1 change and still reloads its estimate; existing Arma placement and workspace tests pass through the staged flow"
    requirement: CHG-03
    verification:
      - kind: e2e
        ref: "playwright:tests/e2e/hosting-placement.spec.ts#applies Arma placement through typed configuration and reloads its estimate"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/arma-placement.test.tsx"
        status: pass
    human_judgment: false
  - id: D8
    description: "Layout of the sticky bar and the review dialog at 320 and 390 px in English and German (no horizontal scroll, bar within one third of the viewport, Apply reachable); the geometry is CSS only and jsdom cannot measure it"
    requirement: CHG-03
    verification: []
    human_judgment: true
    rationale: "Measured reflow at 320x568 and 390x844 is an automated Phase 65 e2e item planned for the later plans and Phase 69; this plan ships the token-only CSS without a measured viewport assertion"

duration: 30min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 07: Staged Settings changes with review and apply Summary

**Settings edits are now staged in the section draft behind a sticky pending bar; a review dialog shows Before and After for every change kind, applies them sequentially through the existing endpoints and reports per-item results with a retry, replacing the old Save button.**

## Performance

- **Duration:** 30 min (2026-11-20T20:20Z to 20:50Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 16 (8 created or modified in `src`, 8 in `tests`; 8 new)

## Accomplishments

- Tracer: `SettingsSection` builds the saved data (server fields plus the configuration query), wraps its content in `ServerChangeDraftProvider`, renders the Settings h2 (focus target) and the DOM-last bar and dialog. `ServerConfigurationPanel` lost its local draft, validation error, update mutation, save bar and the old unsaved-changes hook; values stage through `stageValue`, resets through `stageReset`, and every control is disabled while applying.
- `useApplyServerChanges`: variable-free mutation with `gcTime: 0`, a plan held in a ref only for the run, sequential `runApplyPlan`, exact-key invalidation of the server, configuration, profile options, storage demand, mod pack options, secrets and fleet queries, then draft update, results and `onAllApplied`. `useReviewGroups` reads the review context from the same cached queries as the panels.
- Expansion: `ChangeDiffItem` (name, result state with reason, Before and After, decorative arrow, remove control), the complete `ReviewChangesDialog` (take-effect notice, four ordered groups, removal with focus handoff, results alert, retry label, busy), and `change.css` (section reserve, sticky bar, dialog geometry, 520 and 800 px rules, tokens only).
- The placement e2e and the three older test files moved off the retired Save button; every assertion about typed values, choices, prototype keys and estimates was kept.

## Task Commits

1. **Task 1 (tracer): stage, review and apply settings changes** - `8928bbcc` (feat(webapp))
2. **Task 2: review every change kind and report partial apply results** - `b327a9a2` (feat(webapp))

## Verification Results

- Tracer gate (Task 1): change-flow, workspace-page, arma-placement, workspace-section-panels and server-state-single-owner tests green (57), `locales:check`, `typecheck` and `lint --max-warnings 0` clean before expansion. Tracer verified end-to-end, then expanded.
- Plan-level: `vitest run tests/hosting tests/styles tests/locales` 80 files, 1268 tests passed; `locales:check` 3525 keys, 0 failures; `typecheck` and `lint` exit 0; Prettier clean on every touched file; placement e2e passes.
- Acceptance greps: ServerChangeDraftProvider 3, savebar 0, useUnsavedChanges 0 (panel), `gcTime: 0` 2, `exact: true` 1, "Save overrides" 0 in workspace-page and e2e, `export function ChangeDiffItem` 1, `Server_Change_Results_Partial` 1, `utf-change-bar-reserve` 2, `.message` in the dialog 0.
- `sources/Common/Ui/routes.json` unchanged; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ActionButton could not take a ref**
- **Found during:** Task 1
- **Issue:** `PendingChangesBar` must forward `reviewRef` to the Review button, but `ActionButtonProps` was based on `ComponentPropsWithoutRef`, so `ref` did not typecheck.
- **Fix:** `ActionButtonProps` now uses `ComponentPropsWithRef<"button">` (React 19 passes `ref` as a prop; both render paths spread it).
- **Files modified:** `sources/ReactApp/src/components/ActionControl.tsx`
- **Commit:** 8928bbcc

**2. [Rule 2 - Missing critical] Minimal invalid-fields gate at Review**
- **Found during:** Task 1
- **Issue:** `buildApplyPlan` silently omits an unparseable or out-of-range staged value, so Apply would run an empty or partial plan and leave the draft stuck. The plan assigns the full gate to plan 65-10.
- **Fix:** `SettingsSection` runs `validateStagedSettings` when Review is activated; with invalid values the dialog does not open and the bar shows the `Server_Change_Invalid` alert. Focus movement, per-field errors and Advanced auto-open remain plan 65-10.
- **Files modified:** `SettingsSection.tsx`, `change-flow.test.tsx`
- **Commit:** 8928bbcc

**3. [Rule 1 - Bug] Stale "Applied" state after a complete run**
- **Found during:** Task 2
- **Issue:** Keeping the results of a fully applied run would show "Applied" on a later row with the same step id.
- **Fix:** A complete run stores no results (`setResults(null)`); a regression test covers a second review after an apply.
- **Files modified:** `useServerChangeApply.ts`, `change-flow.test.tsx`
- **Commit:** b327a9a2

**4. [Rule 1 - Bug] `enabled: undefined` fired the secrets request**
- **Found during:** Task 2 (placement e2e: "Unexpected API request: GET .../secrets")
- **Issue:** TanStack treats `enabled: undefined` as enabled; the e2e server fixture has no `canManageSecrets`.
- **Fix:** `useReviewGroups` enables the profile, secrets and mod pack queries only on `=== true`.
- **Files modified:** `useServerChangeApply.ts`
- **Commit:** b327a9a2

**5. [Rule 3 - Blocking] Test icon mock lacked `ArrowRight16Regular`**
- **Found during:** Task 2
- **Fix:** Added the export to `tests/mocks/react-icons.tsx`.
- **Commit:** b327a9a2

### Plan wording that did not match the model

- `change.css` was written in full in Task 1 (the plan assigns it to Task 2) because `SettingsSection` imports it and a stub would have been throwaway; Task 1 uses the plan's simple row, Task 2 swaps in `ChangeDiffItem`.
- The plan's partial scenario ("1 of 4 applied", "Try 3 changes again") is arithmetically inconsistent with a succeeding credential (that gives 2 of 4). The test scenario makes the credential fail too, giving exactly "1 of 4 changes applied" and "Try 3 changes again"; a second case covers the forbidden reason.
- The `.message` scan covers the last five files as the plan says; `useServerChangeDraft.tsx` legitimately reads `action.message` (the live-region text), so it only gets the storage, cookie and console checks.
- The partial-results line is the `ConfirmDialog` `error` alert (role alert, focused by the dialog) instead of a separate paragraph; it is cleared while a retry runs so identical text re-focuses.
- The tracer gate ran in automated-only mode (no human check); the e2e re-run is part of Task 2's verify.

**Total deviations:** 5 auto-fixed (2 bugs, 1 missing critical, 2 blocking) plus 5 wording notes. **Impact:** none on scope; all behavior lines of the plan are covered by tests.

## Issues Encountered

- A full `npm run lint` takes about 3 minutes in this checkout; it was run in the background and finished clean.
- The review-dialog test mounts the real dialog and hooks inside the provider and seeds the draft through staging calls, because the mod pack, profile and credential staging controls arrive in plans 65-10 to 65-12.

## Known Stubs

None. The profile selector and its follow and pin buttons still apply immediately (plan 65-12 stages them) and the credentials panel keeps its current wrapper (plans 65-10 and 65-11); both are recorded in the plan as intentional.

## Threat Flags

None. T-65-18 (variable-free mutation, `gcTime: 0`, plan ref cleared in `onSettled`), T-65-19 (credentials render only the changed or generated notice, failure text from four UI keys, no `.message`) and T-65-20 (UI gating mirrors `canModify` and archived only) are mitigated and covered by the tests and grep gates above.

## Next Phase Readiness

Plans 65-08 to 65-13 can stage mod packs, profiles, credentials and file-adjacent state on the same provider, bar and dialog: `renderItemExtra` and `applyBlockedReason` are ready for the contents diff, `reviewRef` for the leave guard, and the invalid-fields alert slot for plan 65-10's focus handling.

## Self-Check: PASSED

- Created files verified on disk; commits `8928bbcc` and `b327a9a2` verified as ancestors of HEAD.

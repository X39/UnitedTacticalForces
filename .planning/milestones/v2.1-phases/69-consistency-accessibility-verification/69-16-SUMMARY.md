---
phase: 69-consistency-accessibility-verification
plan: 16
subsystem: ui
tags: [react, events-admin, page-header, form-fields, disclosure, leave-guard, tones, ratchet]

requires:
  - phase: 69-03
    provides: "StateViews, toneOf with slotTones and eventTones, the Scenario and Search scenarios keys, the remove dismiss keys"
  - phase: 69-04
    provides: "family.events-admin.json ratchet and the ratchet writer"
  - phase: 69-06
    provides: "strict API mock layer and audit helpers (no event finding was assigned to this plan)"
provides:
  - "Event editor, event list, create page and entity picker on PageHeader, PageStack, shared states, shared fields and ActionButton"
  - "Role symbol picker expanding through Disclosure (button with aria-expanded)"
  - "Slot planner on ActionButton and the slotTones lexicon"
  - "events-admin ratchet reduced to four editor internals with a documented reason"
affects: [69-33, 69-24]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 24b406112edcf09c91fb25891367ce8392800f60
commits: 3
actuals:
  tokens: 19000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Controls that keep a legacy element selector render through ActionButton and restate only the open properties in a token-only family stylesheet"
    - "Every state of a routed editor renders the shared PageHeader above the shared state view"
    - "A missing entity resolves to null in the query function so the not-found branch is reachable"

key-files:
  created:
    - sources/ReactApp/src/features/admin/eventAuthoring.css
  modified:
    - sources/ReactApp/src/features/admin/EventAdminPage.tsx
    - sources/ReactApp/src/features/admin/EventCreatePage.tsx
    - sources/ReactApp/src/features/admin/EventEditPage.tsx
    - sources/ReactApp/src/features/admin/EventEntityPicker.tsx
    - sources/ReactApp/src/features/admin/EventSlotPlanner.tsx
    - sources/ReactApp/src/features/events/EventRoleSymbolPicker.tsx
    - sources/ReactApp/tests/admin-event-page.test.tsx
    - sources/ReactApp/tests/admin-event-entity-picker.test.tsx
    - sources/ReactApp/tests/event-slot-planner.test.tsx
    - sources/ReactApp/tests/events-member/event-role-symbol-picker.test.tsx
    - sources/ReactApp/tests/e2e/event-authoring.spec.ts
    - sources/ReactApp/tests/ratchets/family.events-admin.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json

key-decisions:
  - "The events list h1 is the navigation label (Shared_CommandCenter_Nav_Events); the former kicker with the same text was dropped instead of repeating the title."
  - "The four metric cards stay filter toggles but render through ActionButton with aria-pressed, so no interaction was removed."
  - "Tab strip, stepper, picker options and metric cards use ActionButton plus a small token-only stylesheet instead of staying raw."
  - "EventObjectiveIconPicker and EventSidePicker are unchanged: their only control is the radio or toggle grid, which the UI-SPEC allows on the ratchet."

patterns-established:
  - "Editor leave flow: useLeaveGuard plus ConfirmDialog with initial focus on Keep editing"

requirements-completed: [CON-01, CON-02, CON-03, L10N-03]

coverage:
  - id: D1
    description: "Event editor renders one PageHeader h1 in loading, error, not-found, forbidden and loaded states"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-event-page.test.tsx#event editor page"
        status: pass
    human_judgment: false
  - id: D2
    description: "Create and edit pages render the scenario label and search placeholder from keys in English and German"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/admin-event-page.test.tsx#labels the scenario picker from locale keys"
        status: pass
    human_judgment: false
  - id: D3
    description: "Events admin h1 equals the navigation label in both languages; shared chips, search and tone lexicon"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-event-page.test.tsx#event administration header and controls"
        status: pass
    human_judgment: false
  - id: D4
    description: "Role symbol picker expands through Disclosure and keeps Escape and focus return; slot badges use slotTones"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/events-member/event-role-symbol-picker.test.tsx; tests/event-slot-planner.test.tsx"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/event-authoring.spec.ts (chromium, 1 passed)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Leaving an edited event offers Keep editing and Leave without saving"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-event-page.test.tsx#offers Keep editing and Leave without saving"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual match of the converted tab strip, stepper, metric cards and role symbol toggle against the former look"
    verification: []
    human_judgment: true
    rationale: "The legacy rules in styles.css could not be edited; small overrides restate radius and type scale and only a human can judge pixel parity."

duration: ~3h
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 16: Events Admin Convergence Summary

**Event list, create page, editor, entity picker and slot planner now share PageHeader, state views, form fields, ActionButton and the slot tone lexicon; the role symbol picker expands through Disclosure and the ratchet keeps only four documented editor internals.**

## Performance

- **Tasks:** 3 (1 tracer, 2 auto)
- **Commits:** 3 task commits
- **Files:** 13 modified, 1 created

## Accomplishments

- The editor renders one h1 through `PageHeader` in every state (a shared `EditorStateFrame` puts the header above `LoadingState`, `ErrorState`, `EmptyState` and `ForbiddenState`), fields are `TextField`, `SelectField` and `TextAreaField` with react-hook-form registration kept, and the scenario picker label and placeholder come from `Pages_Events_Create_Step_Scenario_Label` and `React_EventAuthoring_ScenarioSearchPlaceholder` in both languages.
- Leaving an edited event, on the edit and the create page, uses `useLeaveGuard` and a `ConfirmDialog` with Keep editing and Leave without saving, initial focus on Keep editing, instead of `window.confirm`.
- The events list is titled `Events` / `Veranstaltungen`, uses `SearchField`, two `FilterChipGroup`s, `ErrorState`, `EmptyState`, `LoadingState`, `ForbiddenState` under a header, and `toneOf(eventTones)` (a hidden event is now neutral).
- `EventEntityPicker` uses `TextField` (with a Dismiss icon clear control in the trailing slot) and `ActionButton` options and keeps its combobox keyboard behavior.
- `EventRoleSymbolPicker` expands through `Disclosure`; Escape and selection close it and return focus to the toggle. The slot planner uses `ActionButton` and `toneOf(slotTones, ...)`.
- `family.events-admin.json` holds four entries, each with the reason "editor internal with grid or roving behavior (UI-SPEC allowance)".

## Task Commits

1. **Task 1 (tracer): event edit page with the shared header and localized scenario picker** - `4977f8ac` (refactor)
2. **Task 2: event admin list, create page and entity picker** - `b4677c06` (refactor)
3. **Task 3: editor internals, role symbol picker on Disclosure and slot tones** - `24b40611` (refactor)

Tracer gate (auto mode): the automated verify was re-run end to end (unit, ratchets, lint) and passed, so expansion continued. Commits were created with hooks and without extra signing flags.

## Decisions Made

See key-decisions. The plan prohibits editing `styles.css` and the locale files, so the legacy element selectors of the stepper, tab strip, metric cards and picker options still style the button element the shared component renders, and `eventAuthoring.css` restates only the properties `.button` sets and the legacy rule leaves open.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A missing event showed a query error instead of the not-found state**
- **Found during:** Task 1 (test for the not-found state)
- **Issue:** `eventAuthoringService.load` resolves `undefined` for a missing event, and a TanStack query cannot hold `undefined`, so `!query.data` was unreachable and the page showed a load error.
- **Fix:** The query function maps `undefined` to `null`.
- **Files modified:** `EventEditPage.tsx`
- **Commit:** `4977f8ac`

**2. [Rule 1 - Bug] exactOptionalPropertyTypes error in the editor tab strip**
- **Found during:** Task 2 (whole-project `tsc -b`)
- **Issue:** The Task 1 commit passed `className={... : undefined}` to `ActionButton`; the whole-project typecheck was only run after it.
- **Fix:** Conditional prop spread, in `EventEditPage.tsx` and in `EventAdminPage.tsx` for the same pattern.
- **Commit:** `b4677c06`

**3. [Rule 3 - Blocking] New family stylesheet and ratchet entry outside `files_modified`**
- **Found during:** Task 2
- **Issue:** Converting the stepper, tabs, metric cards and picker options to `ActionButton` lets `.button` leak radius, type scale and display; `styles.css` is closed to this plan.
- **Fix:** Added `src/features/admin/eventAuthoring.css` (token-only, no `white-space` override because `tests/styles/button-wrap.test.ts` forbids it) and its zero ceiling entry in `tests/styles/legacy-ratchet.json`.
- **Commit:** `b4677c06`, `24b40611`

**4. [Rule 3 - Blocking] `Save20Regular` is not in the unit-test icon mock**
- **Found during:** Task 1
- **Issue:** `tests/mocks/react-icons.tsx` lacks the icon, so the editor crashed under test (the real package has it).
- **Fix:** The decorative icon was removed from the Save button instead of editing the shared mock, which other parallel plans may also touch.
- **Commit:** `4977f8ac`

**5. [Process] An over-broad `prettier --write` reformatted about 160 unrelated files**
- All of them were restored file by file with `git checkout -- <file>` before the Task 3 commit; none reached a commit.

## Cross-plan findings

| Id | File | Problem | Proposed fix |
|----|------|---------|--------------|
| XPF-69-16-1 | `src/locales/*.json` | No generic search-clear key; `EventAdminPage` reuses `Server_Fleet_SearchClear` ("Clear search") | 69-33 adds a generic `Common_Search_Clear` and points all `SearchField` callers at it |
| XPF-69-16-2 | `src/locales/*.json` | No event editor leave keys; the edit and create pages reuse `Server_Alerts_Leave_Title`, `React_UnsavedChanges`, `Server_Change_KeepEditing` and `Pages_Command_Content_PageEditorLeave_Dismiss` | 69-33 adds `React_EventAuthoring_Leave_Title`, `_Body`, `_Stay`, `_Leave` and switches both pages to them |
| XPF-69-16-3 | `tests/mocks/react-icons.tsx` | Missing `Save20Regular` | 69-33 adds it if a Save icon is wanted back on the editor |

Keys that lost their last reader here and can be retired by 69-33: `Pages_Events_All_PageTitle` and `Pages_Events_All_NoEventsMatch`.

## Known limitations

- `Disclosure` has no disabled prop, so a disabled role symbol picker blocks opening but its toggle button is not announced as disabled (it carries a `data-disabled` style hook only).
- The metric card, tab and stepper looks depend on the legacy element selectors plus `eventAuthoring.css`; D6 in the coverage block needs a human visual check.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored), nothing was installed.
- `npm run test:e2e` regenerated `src/generated/api.ts` with line-ending changes; it was restored and is not committed.

## Verification

- `npx vitest run` over `admin-event-page`, `admin-events`, `admin-event-authoring`, `admin-event-entity-picker`, `event-slot-planner`, `admin-event-objective-icon-picker`, `events-member`, `ratchets`, `styles`, `locales`: 25 files, 348 passed, 1 skipped (opt-in ratchet writer).
- `npm run lint` and `npx tsc -b`: clean. `npm run locales:check`: 3980 keys, 0 failures.
- `npm run test:e2e -- tests/e2e/event-authoring.spec.ts --project=chromium`: 1 passed.
- Acceptance greps: `<h1` in `EventEditPage.tsx` 0; `Search scenarios` 0; raw controls in `EventEditPage.tsx` and `EventCreatePage.tsx` 0; `Pages_Events_All_PageTitle` in `EventAdminPage.tsx` 0; `<details` in `EventRoleSymbolPicker.tsx` 0; `function statusTone` 0; `className="button` in `EventSlotPlanner.tsx` 0.

## Known Stubs

None.

## Threat Flags

None. T-69-22 is mitigated: `admin-event-authoring.test.ts` payload assertions stay green and registration is passed through the `ref` prop of the shared fields.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/src/features/admin/eventAuthoring.css`.
- Commits `4977f8ac`, `b4677c06` and `24b40611` are ancestors of HEAD.

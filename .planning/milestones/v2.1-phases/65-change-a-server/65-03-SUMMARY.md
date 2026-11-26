---
phase: 65-change-a-server
plan: 03
subsystem: ui
tags: [react, react-router, leave-guard, dialog, form-fields, tokens, vitest]

requires:
  - phase: 64
    provides: workspaceSections registry and sectionTo, the form-fields chrome and the Phase 64 layout tokens
provides:
  - useLeaveGuard hook (useBlocker plus beforeunload with a caller predicate, no copy inside)
  - leavesWorkspaceSection predicate over resolved workspace sections
  - ConfirmDialog additive props extraAction, bodyLabel and confirmUnavailableReason
  - TextAreaField, the multi-line sibling of TextField
  - layout tokens --utf-change-bar-reserve and --utf-review-dialog-width
affects: [65-07, 65-08, 65-10, 65-11, 65-12, 65-13]

plan_head_before: cff478561be1b7870eedee215c80791d605cc038
plan_head_after: f88783892c6f19cdff766f934cab9bf03f3189ce

actuals:
  tokens: 7300
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Leave guard returns { blocked, stay, proceed }; the caller owns the dialog and its copy and wires Review as stay() plus its own review callback"
    - "Section guard predicate compares resolved sections (workspaceSectionFromSearch), never raw search strings"
    - "ConfirmDialog extensions are optional props that leave every existing caller byte-identical"

key-files:
  created:
    - sources/ReactApp/src/components/useLeaveGuard.ts
    - sources/ReactApp/tests/leave-guard.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/workspaceSections.ts
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/primitives.test.tsx
    - sources/ReactApp/tests/form-fields.test.tsx
    - sources/ReactApp/tests/hosting/workspace-sections.test.ts

key-decisions:
  - "useLeaveGuard returns { blocked, stay, proceed }; Review is stay() followed by the caller opening its review (no review method in the hook)"
  - "confirmUnavailableReason is added now so primitives.tsx has a single owner in this phase (plan 65-13 consumes it)"
  - "A blank reason (null, undefined, false, empty string) does not block the confirm, matching ActionButton's own hasReason rule"
  - "The 800px rule also covers the multi-line control, using the touch height times two"

patterns-established:
  - "Memory data router guard harness in tests/leave-guard.test.tsx: dirty toggle, section links and the three-action dialog, reusable by the Settings and Files plans"

requirements-completed: [CHG-05]

coverage:
  - id: D1
    description: "A dirty guarded view blocks router navigation (link, navigate(-1)) that leaves its section and offers Stay here, Discard and leave, Review changes in that DOM order with Stay focused"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#blocks a link that leaves the section and offers Stay, Discard and Review in that order"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#keeps the location, the dirty state and the history after Stay"
        status: pass
    human_judgment: false
  - id: D2
    description: "Discard proceeds to the target, Review closes the dialog, stays and calls the review callback once, a navigation to another server is blocked"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#proceeds to the target after Discard"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#closes the dialog, stays and calls the review callback once on Review"
        status: pass
    human_judgment: false
  - id: D3
    description: "Same-section navigations (replace of ?action=, folder change) and clean views are never blocked; the predicate resolves unknown and prototype tab values to overview"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#never blocks a replace that stays in the section"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-sections.test.ts#leavesWorkspaceSection"
        status: pass
    human_judgment: false
  - id: D4
    description: "beforeunload is registered only while dirty (preventDefault, returnValue empty), removed when clean, and window.confirm is never called"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/leave-guard.test.tsx#registers beforeunload only while dirty and never uses window.confirm"
        status: pass
      - kind: other
        ref: "grep -c window.confirm useLeaveGuard.ts returns 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "ConfirmDialog extraAction, bodyLabel and confirmUnavailableReason are additive; existing dialogs keep exactly two action buttons and a plain body"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/primitives.test.tsx#ConfirmDialog additive capabilities"
        status: pass
    human_judgment: false
  - id: D6
    description: "TextAreaField follows the TextField label, hint, error and aria-describedby rules; both layout tokens exist"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/form-fields.test.tsx#TextAreaField"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/form-fields.test.tsx#Phase 65 layout tokens"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/styles (server-style-guard over form-fields.css)"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 03: Leave guard, extended dialog and multi-line field Summary

**A section-aware leave guard (router blocker plus beforeunload, copy-free), three additive ConfirmDialog props, a multi-line TextAreaField and two layout tokens, so the Settings, Files and review plans start from tested primitives.**

## Performance

- **Duration:** 8 min (2026-11-20T19:42Z to 19:50Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 10 (2 created, 8 modified)

## Accomplishments

- `useLeaveGuard({ dirty, shouldBlock })` calls `useBlocker` with a predicate that is false while clean and otherwise the caller predicate (default: block all). The `beforeunload` listener exists only while dirty and is removed in the effect cleanup. It never uses `window.confirm` and holds no translation keys.
- `leavesWorkspaceSection(current, next, section)` compares the pathname and the resolved `?tab=` section, so a replaced `?action=` or a changed `path=` inside the section never blocks (RESEARCH Pitfall 7, T-65-07).
- `ConfirmDialog` gained `extraAction` (a third plain button after confirm, disabled while busy), `bodyLabel` (the body becomes a focusable named `region`) and `confirmUnavailableReason` (the confirm stays focusable with `aria-disabled`, the reason is linked, activation does nothing).
- `TextAreaField` shares the wiring, label, hint and error helpers with `TextField`; CSS adds a tokens-only `--multiline` modifier with a two-row minimum, vertical resize and the touch height at 800px.
- `--utf-change-bar-reserve: 12rem` and `--utf-review-dialog-width: 44rem` added next to the Phase 64 layout tokens.

## Task Commits

1. **Task 1 (tracer): section-aware leave guard with a three-action dialog** - `003ce135` (feat(webapp))
2. **Task 2: review dialog body, confirm reason, text area and tokens** - `f8878389` (feat(webapp))

## Verification Results

- Tracer gate (Task 1): leave-guard, workspace-sections and primitives tests green (75 tests), typecheck and lint clean before expansion.
- Task 2 set (primitives, form-fields, styles, leave-guard): 95 tests green; the server style guard accepts the new CSS.
- Full ReactApp suite: 138 files, 1593 tests passed.
- `typecheck`, `lint --max-warnings 0` exit 0; Prettier check passes on every file this plan touched.
- Acceptance greps: useLeaveGuard 1, leavesWorkspaceSection 1, extraAction 6, window.confirm 0, TextAreaField 1, bodyLabel 3, confirmUnavailableReason 7, both token greps 1.
- `sources/Common/Ui/routes.json` unchanged; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`; `useUnsavedChanges` is untouched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Commit subject exceeded the 80 character limit**
- **Found during:** Task 2 commit
- **Issue:** The plan's suggested subject `feat(webapp): add the review dialog body, blocked confirm reason and text area field` is 84 characters, while AGENTS.md requires under 80.
- **Fix:** Used `feat(webapp): add review dialog body, confirm reason and text area` (66 characters), amended before anything else was built on it.
- **Files modified:** none (commit message only)
- **Commit:** f8878389

The TDD cycle was relaxed as in plans 65-01 and 65-02: implementation and tests were written together and run green rather than committed as separate RED and GREEN steps.

## Issues Encountered

None. The tracer tests passed on the first run, including the Back (`router.navigate(-1)`) case on the memory router.

## Known Stubs

None.

## Threat Flags

None. T-65-07 (the tab value is resolved only through the exact `workspaceSectionFromSearch` match, unknown values resolve to overview, covered by a `__proto__` test) and T-65-08 (the guard blocks only while dirty and only navigations that leave the section, so a stale guard cannot trap the member) are mitigated and tested.

## Next Phase Readiness

Plans 65-08 (Files) and 65-10 (Settings) can wire `useLeaveGuard` with `leavesWorkspaceSection(current, next, section)` as the predicate and render their own leave dialog from `blocked`, `stay` and `proceed`, using `extraAction` for Review. Plan 65-13 can use `bodyLabel` and `confirmUnavailableReason` for the scrollable review body and the compare-in-progress block. Plan 65-09 onward can use `TextAreaField` for string-array settings.

## Self-Check: PASSED

- Created and modified files verified on disk; commits `003ce135` and `f8878389` verified as ancestors of HEAD.

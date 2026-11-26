---
phase: 71-server-access-and-cost-owner-ui
plan: 11
subsystem: testing
tags: [playwright, e2e, access-panel, strict-mocks, axe, keyboard, reflow, german-layout, layout-shift]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-09 mutable server record and the quiet cost-owner options in workspaceMocks.ts"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-10 Remove all access, the lost-access flow and the list filter in the Access panel"
provides:
  - "workspaceMocks.ts: AccessMockState, accessMockState, accessUserRecord, accessRoleRecord, WorkspaceMockOptions.accessState and .canManageAccess, and stateful access route handlers (list, both candidate searches, four grant and revoke routes)"
  - "hosting-access.spec.ts: 16 chromium tests (7 per culture plus 1 per narrow viewport pair), all with an empty session.unexpected"
  - "access.css: the Give access cell keeps the room of its unavailable reason, so choosing or clearing a candidate no longer moves the list"
affects: [71-12, e2e-mocks, access-panel]

actuals:
  tokens: 10500
  tasks: 2
  commits: 3
plan_head_before: 151b9b0b59bb1ae09e7c1c3cf9dde7eb3565f248
plan_head_after: a2b197be363782851ae7e4ed2932df7596e7e943

tech-stack:
  added: []
  patterns:
    - "Stateful access mock: a grant adds the candidate to the list, a revoke of the last permission removes the subject and offers it as candidate again, so every refetch returns server truth"
    - "canManageAccess of the mocked server is derived from the state (auditUserId holds sv or sv:manage-access), so the lost-access flow appears by itself after the revoke"
    - "Layout-shift readings use page coordinates (top plus scrollY) and are taken with the in-flow listbox closed"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-access.spec.ts
  modified:
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
    - sources/ReactApp/src/components/server/access.css

key-decisions:
  - "The access mock logs every grant and revoke with the status it answered, including refused ones, so tests assert exact request order and refused attempts"
  - "A mock subject that loses its last permission leaves the list and becomes a candidate again, as the API lists only subjects with permissions"
  - "The no-shift test compares readings with the listbox closed (Escape) because the in-flow listbox pushes the list down by design"

patterns-established:
  - "installAccess(page, culture, state, overrides) in the spec wraps installWorkspace with an access state"

requirements-completed: [D-04, D-08, D-09, D-15, D-19]

coverage:
  - id: E1
    description: "A manager gives a member access through the picker, opens the new row, switches Operate on and off and removes all access through the dialog; every step is one logged call, axe is clean after the grant, with the row open and with the dialog open"
    requirement: "D-04, D-08, D-09, D-19"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#gives access, switches a permission and removes all access (en, de)"
        status: pass
    human_judgment: false
  - id: E2
    description: "Remove all access on a role revokes in catalog order through the role endpoint; a refused switch rolls back with the alert under it"
    requirement: "D-08, D-09"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#revokes the permissions of a role in catalog order when all access is removed (en, de)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#rolls a refused switch back and shows the alert under it (en, de)"
        status: pass
    human_judgment: false
  - id: E3
    description: "Removing the own Manage access asks first with Keep permission focused and the switch unchanged; confirming removes the panel, shows the toast and focuses the Settings h2"
    requirement: "D-15"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#asks before the member removes their own Manage access and leaves the panel safely (en, de)"
        status: pass
    human_judgment: false
  - id: E4
    description: "The grant form works with the keyboard only, and the German panel at 320 and 390 px has no horizontal page scroll, no clipped label (including Zugangsdaten verwalten and its switch), and passes reflow and text spacing with the picker open and a long candidate chosen"
    requirement: "D-19, UI Considerations: overflow at 320, long text"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#gives access with the keyboard only (en, de)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#keeps the switches, the picker and a long role title inside the viewport (de, 320x568 and 390x844)"
        status: pass
    human_judgment: false
  - id: E5
    description: "Choosing and clearing a candidate keeps top and height of the picker slot, the Give access button and the first row header within 0.5 px, on desktop and in German at 320 and 390 px"
    requirement: "standing no-layout-shift preference"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#keeps the grant form and the row headers in place while a candidate is chosen and cleared (en, de, and de at 320 and 390)"
        status: pass
    human_judgment: false
  - id: E6
    description: "A visibilitychange on the document refetches the access list exactly once and shows another manager's change"
    requirement: "UI Considerations: backstop for another manager's change"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-access.spec.ts#fetches the access list again when the page becomes visible (en, de)"
        status: pass
    human_judgment: false

# Metrics
duration: 55min
completed: 2026-11-26
---

# Phase 71 Plan 11: Access panel in the browser Summary

**A stateful strict mock of the access routes and 16 Playwright tests that give access, toggle, remove all access, lock the member out safely, use the keyboard only, read the panel in German at 320 and 390 px, check for layout shift and prove the refetch on return; the shift check found and drove a one-line CSS fix.**

## Accomplishments

- **Mock (Task 1):** `AccessMockState` (users, roles, allowed and assignable permissions, two candidate lists, failing permissions, a `calls` log and a `reads` counter) with `accessMockState`, `accessUserRecord` and `accessRoleRecord`. `workspaceHandlers` answers `GET access`, both options routes (filtered by `search`, never offering a subject that holds a permission) and the four grant and revoke routes, which change the state. The server record's `canManageAccess` follows the state (the `auditUserId` row holds `sv` or `sv:manage-access`) or the forced option, and a member who may not manage access gets 403 on the access routes.
- **Tracer (Task 1):** the manager flow in English and German: grant "Charlie" through the picker (logged `sv:view` grant, announcement, closed row), open the row, switch Operate on and off (logged calls, announcements), Remove all access with the dialog (body text, Keep access focused, revoke, row gone, focus on the list `h3`), axe after the grant, with the row open and with the dialog open.
- **Task 2:** a role removal in catalog order, a refused switch (500) that rolls back with the alert, the self lock-out, a keyboard-only grant, German 320x568 and 390x844 layout tests, the no-shift test (also at both narrow widths) and the `visibilitychange` refetch.

## Task Commits

1. **Task 1 (tracer): manager flow and access mock** - `0cefeb22` (test)
2. **Product fix found by the no-shift check** - `86507f94` (fix)
3. **Task 2: lock-out, keyboard, German narrow layouts, no shift, refetch** - `a2b197be` (test)

## Verification

- `hosting-access.spec.ts` and `hosting-cost-owner.spec.ts` on `--project=chromium --project=mobile-390 --project=mobile-320 --retries=0 --workers=4` through the lock helper: 32 passed, 0 failed, 0 flaky. The final run after the CSS fix is the one above; before the fix the no-shift tests failed with a 25 px header shift in both cultures.
- `lint` (max-warnings 0) and `typecheck`: clean. `prettier --check --end-of-line auto` on the three changed files: clean. `npx vitest run tests/styles`: 5 files, 156 passed, 1 skipped.
- CI step substitution: `npm run ci` was not run because `format:check` fails on every file under CRLF checkouts (environmental). Steps run individually: prettier (`--end-of-line auto`), lint, typecheck and the full `npx vitest run --maxWorkers=4` (221 files, 3783 passed, 2 skipped, pre-existing skips). `locales:check`, `locales:unused`, `contracts:check` and `build:assets` are not affected: no locale key, API contract or route changed; the e2e web server builds the assets on every run and succeeded.
- Tracer gate: the Task 1 `<verify>` is automated only; the same command ran end to end before Task 2 and passed, so the tracer was verified before expansion.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Give access button reason made the list jump when a candidate was chosen or cleared**
- **Found during:** Task 2 (no-shift test)
- **Issue:** The unavailable Give access button prints its reason ("Choose a member first.") under the button. Choosing a candidate removed that line, so the whole list below moved up by 25 px, and clearing moved it down again. The plan's truth (and the standing preference) is that choosing and clearing keep the form controls and row headers in place.
- **Fix:** `.access-grant__action` now has a `min-height` equal to its label offset plus the button plus one reason line (touch button height at 800px and below, no label offset at 520px and below). Only `access.css` changed; the style guard test passes.
- **Files modified:** `sources/ReactApp/src/components/server/access.css`
- **Commit:** `86507f94`
- **Known limit:** a reason that wraps to two lines (not the case for the choose-first reasons in either language at 320 px) would still grow the cell and move the list when it disappears.

### Plan wording interpreted

- **The spec runs on chromium only.** `mobile-390` and `mobile-320` match only `audit`, `journeys` and `mobile` specs, so a root spec has no tests there (XPF-71-09-6). The verify command still names all three projects; the narrow widths are covered by `test.use({ viewport })` describes at 320x568 and 390x844 on chromium.
- **"Record the picker slot, Give access button and first row header, choose and clear"** is implemented with all readings taken while the in-flow listbox is closed (Escape), because the listbox is in normal flow by design and pushes the list down while it is open. The readings are page coordinates (top plus scroll), so a scroll between two readings cannot count as a shift.
- **The picker slot** is the first `.form-field` inside `.access-grant__picker` (label plus input, or label plus chosen token).
- **Three extra tests** beyond the plan's list (role removal order, refused switch rollback, the no-shift test at narrow widths) were added because the mock options and the XPF findings called for them; they add no risk to the plan's contracts.
- The commit trailer names Claude Sonnet 5.5 (the system attribution reminder), not the Opus 5.5 line of the environment notes, matching the model that wrote the commits (as 71-08 to 71-10 did).
- The plan's ledger file for commit counting could not be written inside the shared git directory of this isolated worktree; `commits` and `plan_head_before` were measured from the expected base commit instead.

## Cross-plan findings resolved (XPF)

- **XPF-71-09-1:** reused. The access mock follows the cost-owner pattern (record helpers with unique ids per index, a mutable record, a recorded call log).
- **XPF-71-09-5:** done. Setting a state makes `canManageAccess` true and answers the access reads, both candidate searches and the four change routes; no test fills `session.unexpected`.
- **XPF-71-07-2:** carried, not needed. The baseline `quietCostOwnerOptions` of `installApp` answers the cost-owner options for every access test (the Cost owner panel is open on the same Settings route); no strict router of its own was added.
- **XPF-71-08-6:** done. Grant and revoke resolve with 204 and change the state, so the refetch after the last toggle returns server truth. The failing-permission option answers 500 and leaves the state unchanged.
- **XPF-71-10-3:** done. The lost-access flow appears because the derived `canManageAccess` flips to false on the server read after the own Manage access revoke.
- **XPF-71-10-2:** done and asserted: focus is on the list `h3` after Remove all access, and on the Settings `h2` after losing Manage access.
- **XPF-71-10-4:** carried, not exercised. No test reaches eight subjects, so the filter result line is not mounted in these tests; the filter stays covered by the jsdom tests of 71-10.
- **XPF-71-09-2:** done. Option locators are scoped to the panel's `listbox` inside `#server-access`.
- **XPF-71-09-3:** carried, not needed. No test clicks an `aria-disabled` control.
- **XPF-71-09-6:** done as described above (chromium with explicit viewports).
- **XPF-71-08-1/10 deviation:** asserted. The role removal test checks the revoke order `sv:view`, `sv:operate`, `sv:modify` (catalog order; Manage access and Full control would be last).
- **Process hygiene:** no background processes of mine remain; the `node_modules` junction is removed before returning.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Test-only code with mocked responses plus one CSS rule. T-71-27 (false green) is mitigated: every test ends with an empty `session.unexpected`, and every state-changing test asserts the exact logged grant and revoke calls (kind, direction, subject, permission, order).

## Cross-plan findings

- **XPF-71-11-1:** `installWorkspace` now accepts `accessState` (built with `accessMockState`) and `canManageAccess`. Specs of 71-12 that need the Access panel (for example the accessibility audit and registry states) can pass a state and read `state.calls` and `state.reads`; without a state the Access panel must not mount, or `session.unexpected` fills.
- **XPF-71-11-2:** the Give access button cell now reserves one line for its unavailable reason. Any other panel that swaps a button with a visible reason for a plain button when its precondition is met (for example the Cost owner Transfer button) can show the same 25 px jump; 71-12 may check it.
- **XPF-71-11-3:** `tests/e2e/hosting-access.spec.ts` rows are addressed by role: the row toggle is a `button` named by the subject name and its content a `region` with the same name; use those instead of `.access-row` text matches.

## Self-Check: PASSED

- FOUND: `sources/ReactApp/tests/e2e/hosting-access.spec.ts`
- FOUND: `sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts` (accessState, accessMockState)
- FOUND: `sources/ReactApp/src/components/server/access.css` (min-height on `.access-grant__action`)
- FOUND commits `0cefeb22`, `86507f94`, `a2b197be`

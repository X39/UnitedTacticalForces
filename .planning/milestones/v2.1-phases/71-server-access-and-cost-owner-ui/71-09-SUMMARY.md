---
phase: 71-server-access-and-cost-owner-ui
plan: 09
subsystem: testing
tags: [playwright, e2e, cost-owner, strict-mocks, axe, keyboard, reflow, german-layout]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-07 ServerCostOwnerPanel (anchor server-cost-owner), the cost strip link and the quiet options baseline"
provides:
  - "workspaceMocks.ts: mutable server record behind the detail and fleet reads, a filtered cost-owner/options handler, a PUT cost-owner handler with costOwnerTransfer ok/409/400/403 and costOwnerTransferBodies recording, and the costOwnerOptionRecord builder"
  - "hosting-cost-owner.spec.ts: 14 browser tests (6 per culture for the transfer flows, 1 German layout test per narrow viewport)"
affects: [71-10, 71-11, 71-12, e2e-mocks]

actuals:
  tokens: 6500
  tasks: 2
  commits: 2
plan_head_before: 1738d16ac387869324e4f5f108abc1e1afaf3da5
plan_head_after: e8b3886a849646ceb4502f8ef5a7c2a5d9acf43e

tech-stack:
  added: []
  patterns:
    - "Strict workspace mock with one mutable server record, so a mutating request shows in every later read"
    - "Forced click on an aria-disabled control to prove it does nothing, because Playwright refuses to click it otherwise"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-cost-owner.spec.ts
  modified:
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts

key-decisions:
  - "The options handler never lists the current owner of the mocked server, as the API does, so after a transfer the new owner drops out of the picker and the mock stays truthful"
  - "A transfer request body recorder (costOwnerTransferBodies) replaces trust in the UI: each test asserts exactly the body sent, or no body at all while the transfer is unavailable"
  - "The strict mock records only unanswered requests, so the read-only viewer test listens to page requests directly to prove no options request was sent"

patterns-established:
  - "Scope option locators to the panel listbox: a page-wide getByRole option matches hundreds of unrelated options in the Settings section"

requirements-completed: [D-11, D-12, D-13, D-19]

coverage:
  - id: D1
    description: "A modifier transfers the cost owner in a real browser: picker, confirmation naming owner and server, toast, panel summary and cost strip show the new owner from the refetched server; axe is clean on the panel, the open picker and the dialog"
    requirement: "D-11, D-13, D-19"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#transfers the cost owner and shows the new owner in the panel and the cost strip (en, de)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Transfer is unavailable with its visible reason while a deployment exists and while jobs are pending; a mocked 409 shows the conflict message inside the open dialog"
    requirement: "D-12"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#keeps Transfer unavailable with its reason while a cloud machine exists (en, de)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#keeps Transfer unavailable with its reason while jobs are pending (en, de)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#shows the conflict message inside the open dialog when the API answers 409 (en, de)"
        status: pass
    human_judgment: false
  - id: D3
    description: "A viewer without canModify sees the read-only sentence, no picker and no transfer button, and no cost-owner request is sent while walking through the panel"
    requirement: "D-03"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#shows the read-only sentence, no picker and no owners request without canModify (en, de)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The transfer works with the keyboard only, and the German panel at 320 and 390 px has no horizontal page scroll, no clipped label, a long owner name wrapped inside its row, and passes reflow and text spacing with the picker and the dialog open"
    requirement: "D-19"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#transfers with the keyboard only (en, de)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-cost-owner.spec.ts#keeps the picker, the long owner name and the dialog inside the viewport (de, 320x568 and 390x844)"
        status: pass
    human_judgment: false

# Metrics
duration: 40min
completed: 2026-11-26
---

# Phase 71 Plan 09: Cost owner transfer in the browser Summary

**Strict-mock Playwright coverage of the cost owner transfer: the happy path with toast and refreshed owner, both unavailable reasons, the 409 dialog message, the read-only viewer, a keyboard-only transfer and the German 320 and 390 px layouts, all green with no retries.**

## Performance

- **Duration:** about 40 min
- **Completed:** 2026-11-26
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files changed:** 2 (1 created, 1 modified)
- **Commits:** 2 task commits (measured from the plan head ledger)

## Accomplishments

- **Mock (Task 1):** `workspaceHandlers` keeps one mutable `server` record read by `GET {base}` and `GET /api/game-servers`. New options `costOwnerOptions`, `costOwnerTransfer` (`"ok" | 409 | 400 | 403`) and `costOwnerTransferBodies`; a filtered `GET cost-owner/options` handler that never lists the current owner; a `PUT cost-owner` handler that replaces `costOwnerId` and `costOwnerName` on "ok" and answers a problem body otherwise; the exported `costOwnerOptionRecord` builder.
- **Tracer (Task 1):** the happy path in English and German. It opens `?tab=settings#server-cost-owner`, focuses the picker, checks the listed owners, chooses Charlie, asserts the dialog text and the focused dismiss button, scans the panel (open picker and chosen state) and `dialog[open]` with axe, confirms, then asserts the toast, the summary "Billed to Charlie", the cost strip owner value, exactly one PUT body and an empty `session.unexpected`.
- **Edge cases (Task 2):** unavailable reasons for a running deployment and for pending jobs (with an owner chosen, so the priority over "choose first" is proven and no dialog opens); the 409 path (dialog stays open with the alert text, no toast, owner unchanged, dismiss keeps the chosen owner); the read-only viewer (sentence, no combobox, no button, no `/cost-owner` request in the page request log); the keyboard-only transfer including focus on the facts value after the refetch; and one German layout test per viewport with a long, space-containing owner name.

## Task Commits

1. **Task 1: tracer, transfer the cost owner in the browser** - `ea038db1` (test)
2. **Task 2: unavailable reasons, 409, read-only, keyboard and German narrow layouts** - `e8b3886a` (test)

## Verification

- `hosting-cost-owner.spec.ts` on `--project=chromium --project=mobile-390 --project=mobile-320 --retries=0` through the lock helper: 14 passed, 0 failed, 0 flaky, under 12 workers.
- `lint` (max-warnings 0) and `typecheck` (`tsc -b`): clean. `prettier --check --end-of-line auto` on both files: clean.
- Tracer gate: the Task 1 `<verify>` is automated only; the same command ran end to end before Task 2 and passed, so the tracer was verified before expansion.

## Deviations from Plan

### Plan wording

**1. Tab, not Shift+Tab, from dismiss to confirm**
- The plan says "Shift+Tab to the confirm". The dialog renders the dismiss button first and the confirm button after it (`confirmPlacement` default), and the dismiss button holds the initial focus, so the confirm button is the next stop with plain Tab. The test presses Tab and asserts the confirm button is focused.

**2. The spec does not run on the mobile projects**
- `mobile-390` and `mobile-320` only match `audit`, `journeys` and `mobile` specs, so a spec in `tests/e2e/` root has no tests there; the verify command runs the 14 tests on chromium. The narrow layouts are covered by `test.use({ viewport })` describes at 320x568 and 390x844 on chromium, as `hosting-change.spec.ts` does.

### Auto-fixed Issues

**3. [Rule 3 - Blocking] Page-wide option locator matched 427 elements**
- **Found during:** Task 1 first run
- **Issue:** `page.getByRole("option")` matches the options of other controls in the Settings section.
- **Fix:** `optionsOf(page)` scopes to the listbox inside `#server-cost-owner`.
- **Committed in:** `ea038db1`

**4. [Rule 3 - Blocking] Playwright cannot click an aria-disabled button**
- **Found during:** Task 2 first run
- **Issue:** Actionability waits for "enabled", and `aria-disabled` makes it never enabled, so the test timed out.
- **Fix:** `click({ force: true })` with a comment, followed by asserting no `dialog[open]` and no recorded PUT body.
- **Committed in:** `e8b3886a`

**Total deviations:** 2 plan wording notes, 2 auto-fixed (both blocking, test-side). No app code changed.

## Issues Encountered

- The first Task 1 commit carried the attribution line from the environment notes ("Claude Opus 5.5"); it was amended before any further work to the line of the session attribution reminder ("Claude Sonnet 5.5"), which names the model that wrote it.
- The lint run took over three minutes while plan 71-08 ran its own suite; it finished clean.

## Cross-plan findings

- **XPF-71-09-1:** `installWorkspace` now accepts `costOwnerOptions`, `costOwnerTransfer` and `costOwnerTransferBodies`. After a transfer the mock answers the detail and fleet reads with the new owner and drops that owner from the options. Later specs that need a different owner flow can reuse `costOwnerOptionRecord(index, name, overrides)` (ids are unique per index).
- **XPF-71-09-2:** `getByRole("option")` is not safe page-wide on the Settings route (hundreds of unrelated options). Scope option locators to the panel's `listbox`.
- **XPF-71-09-3:** To prove an `aria-disabled` control does nothing, click it with `{ force: true }`; Playwright refuses the normal click.
- **XPF-71-09-4:** In the cost owner dialog the order is dismiss (initial focus), then confirm; Tab, not Shift+Tab, reaches the confirm button.
- **XPF-71-09-5:** After merge with 71-08, the Access panel only renders for `canManageAccess`; the default mocked server of `installWorkspace` has no such flag, so this spec sends no access requests. A later spec that sets `canManageAccess` on this mock must also answer the access reads, or `session.unexpected` fills.
- **XPF-71-09-6:** The workspace e2e specs of `tests/e2e/` root do not run on `mobile-390` and `mobile-320`; narrow-width coverage there needs an explicit `test.use({ viewport })` on chromium.

## Known Stubs

None.

## Threat Flags

None. Test-only code with mocked responses; T-71-23 (false green) is mitigated: every test ends with an empty `session.unexpected`, and the transfer tests also assert the exact PUT bodies.

## Self-Check: PASSED

- FOUND: `sources/ReactApp/tests/e2e/hosting-cost-owner.spec.ts`
- FOUND: `sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts` (costOwnerTransfer, cost-owner/options)
- FOUND commit `ea038db1`
- FOUND commit `e8b3886a`

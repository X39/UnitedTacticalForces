---
phase: 71-server-access-and-cost-owner-ui
fixed_at: 2026-11-26T00:00:00Z
review_path: A:/dev/git/UnitedTacticalForces/.planning/phases/71-server-access-and-cost-owner-ui/71-REVIEW.md
iteration: 3
findings_in_scope: 1
fixed: 1
skipped: 0
status: all_fixed
---

# Phase 71: Code Review Fix Report

**Fixed at:** 2026-11-26
**Source review:** A:/dev/git/UnitedTacticalForces/.planning/phases/71-server-access-and-cost-owner-ui/71-REVIEW.md
**Iteration:** 3

**Summary:**
- Findings in scope: 1
- Fixed: 1
- Skipped: 0

Verification ran in the main checkout (`workflow.use_worktrees` is true in config, but the orchestrator ran this pass in the main checkout, so no worktree was created).

## Fixed Issues

### WR-01: Clearing `toggledSubject` in `runToggle`'s `finally` reads the optimistic cache, so the focus move never fires against a real server

**Files modified:** `sources/ReactApp/src/features/hosting/useAccessMutations.ts`, `sources/ReactApp/tests/hosting/server-access-panel.test.tsx`
**Commit:** 726d7558
**Applied fix:** The reviewer's first option. `useToggleAccess`'s `onSettled` now returns `settleAccessChange(...)` when the settling toggle is the last pending one (and `undefined` otherwise), the same way `useRemoveAllAccess` does. `mutateAsync` of the last toggle therefore resolves only after the access list has been refetched, so the `finally` in `runToggle` reads the refetched list. If the subject is gone, the ref stays set and the focus effect (which already ran when the refetch landed) moves focus to the heading. If the subject is still listed, the `finally` clears the ref. `ServerAccessPanel.tsx` is unchanged. The serial-scope, optimistic-rollback and "refreshes once after the last toggle" tests all still pass, so the fallback (checking the query state) was not needed. A doc comment in the hook records the new resolve timing.

New tests:
- "moves focus to the list heading when the refetch lands well after the toggle resolved": the refetch is a deferred promise resolved manually after the toggle and a 50 ms wait, with focus left on the switch. It asserts Bob is still listed (optimistic) before the landing, then that the heading has focus after the row is gone. **Shown failing on the pre-fix code** (assertion `toHaveFocus` on the heading failed), passing after.
- "forgets the toggled subject once a late refetch keeps it listed": the refetch is deferred and lands with Bob still listed. A later unrelated removal (cache write for another manager) must not focus the heading. This passes before and after the fix, because the old `finally` also cleared the ref in this case. It is a guard for iteration-2 IN-01 against the fix: with the `finally` clear removed it fails, as does the existing "does not pull focus ... for another reason" test (checked by mutation, then restored).

Both behaviours hold: (a) the late-landing focus move, (b) the remembered subject is cleared when the subject stays listed.

Status: fixed: requires human verification (focus and timing logic; no syntax tier covers it, but the tests above exercise both directions).

Side effect to be aware of: while the last toggle's refetch is running, its switch stays marked busy a little longer and the "turned on/off" announcement follows the refetch. A toggle started during that window queues behind the settle step in the serial scope, as Remove all already does.

## Verification

- `tests/hosting/server-access-panel.test.tsx`, `tests/hosting/settings-section.test.tsx`, `tests/search-picker.test.tsx`: 134 passed.
- `tests/e2e/hosting-access.spec.ts` (chromium, 2 workers, through the lock helper): 19 passed.
- `npx prettier --check ... --end-of-line auto`, `npm run -s lint`, `npm run -s typecheck`: clean.
- `sources/ReactApp/src/generated/api.ts` restored after e2e and not committed.

---

_Fixed: 2026-11-26_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 3_

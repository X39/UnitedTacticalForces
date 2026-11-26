---
phase: 69-consistency-accessibility-verification
plan: 41
subsystem: ui
tags: [react, accessibility, focus-management, keyboard, playwright, vitest]
requires:
  - phase: 69-39
    provides: "useDrawerFocus hook, inert covered content, focus return on close"
provides:
  - "Document-level Tab cycle that holds focus in an open mobile navigation after pointer input"
  - "Skip links inert and scrims out of the tab order while a mobile navigation is open"
  - "Layered Escape: the profile popover closes before the shell navigation"
  - "Chromium 390 px proof of the pointer-mixed paths"
affects: [FND-06 keyboard operability, 69-VERIFICATION human list]
tech-stack:
  added: []
  patterns:
    - "Focus trap as a document keydown listener that acts only for focus inside, on the toggle or nowhere"
    - "Escape layering: inner layers preventDefault on the document, the shell navigation listens on the window and skips handled events"
key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/useDrawerFocus.ts
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/src/components/ProfileMenu.tsx
    - sources/ReactApp/src/components/useResponsiveNavigation.ts
    - sources/ReactApp/tests/shell-layouts.test.tsx
    - sources/ReactApp/tests/profile-menu.test.tsx
    - sources/ReactApp/tests/e2e/public-shell.spec.ts
key-decisions:
  - "The Tab cycle listens on document, not on an element, because a click on non-focusable surface leaves focus on body and no element handler receives that keydown"
  - "The initial focus move retries for up to 10 animation frames (Rule 1 fix) because under reduced motion the drawer is still visibility:hidden for its first frames"
  - "ProfileMenu marks its Escape handled with preventDefault; useResponsiveNavigation moved to window and skips handled events"
requirements-completed: [FND-06]
duration: 95min
completed: 2026-11-25
status: complete
actuals:
  tokens: 7600
  tasks: 3
  commits: 3
plan_head_before: 5efa7c7e31ed7c20693e4e764a9631d254b4e90f
plan_head_after: 5e33ac6b95384cb9e4bc0f78e9a3e3538dc92c27
---

# Phase 69 Plan 41: Pointer-mixed focus leaks in the mobile navigations Summary

Both mobile navigations now keep focus inside after pointer input followed by Tab or Shift+Tab, and Escape closes one layer at a time (profile popover before the menu), proven with user-event clicks in Vitest and mouse clicks in Chromium at 390 px.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: pointer then Tab never leaves an open mobile navigation | a39749fa |
| 2 | Escape closes one layer at a time | 9b08717a |
| 3 | Chromium proof of popover Tab order and layered Escape over the open community menu | 5e33ac6b |

- **WR-01** (Command Center drawer): `useDrawerFocus` now returns `void` and, while open, registers a `keydown` listener on `document`. It acts only when focus is inside the navigation, on the toggle (`includeOpener`) or on the body. The skip links carry `inert` while open and both scrims have `tabIndex={-1}`.
- **WR-02** (community menu): the header no longer carries a trap handler, so Tab from a middle popover item stays in the popover. `ProfileMenu` calls `preventDefault` on its Escape and `useResponsiveNavigation` listens on `window` and ignores handled events.
- **IN-07**: the cycle selector covers `a[href]`, `area[href]`, `button`, `input` (not hidden), `select`, `textarea`, `summary`, `iframe`, `[contenteditable="true"]` and `[tabindex]`, and skips disabled elements, negative tabindex and anything inside `[inert]`.

## Verification

- Vitest `shell-layouts`, `profile-menu`, `hosting/cost-confirmation-provider`: 76 passed. RED was confirmed first (6 failures in Task 1, 3 in Task 2).
- `typecheck` and `lint` clean.
- Playwright Chromium `public-shell.spec.ts` + `hosting-fleet.spec.ts`: 51 passed, retries 0. The Task 1 spec was shown to fail with the Tab listener disabled (Shift+Tab after a brand click left the drawer).
- Blank-surface click for the "below the last link" path worked at the default 390x844 viewport (gap above 8 px), so the viewport height was not raised.
- Acceptance greps of Tasks 1 and 2 all met their expected counts.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Initial drawer focus lost when the drawer is opened by mouse under reduced motion**
- **Found during:** Task 1 (Chromium test, first run)
- **Issue:** With `prefers-reduced-motion`, the global `transition-duration: 0.01ms !important` makes the drawer's `visibility` transition start at `hidden` for its first frame(s). The open effect's `focus()` call hit a hidden close button, focus stayed on the toggle, and Chrome's inert focus fix-up then dropped it to body. The existing keyboard test did not hit this because it does not emulate reduced motion.
- **Fix:** `useDrawerFocus` retries the initial focus on the following animation frames (at most 10), only while focus is on body, nothing or the opener, so focus the user moved elsewhere is not taken. The retry is cancelled when the effect re-runs or unmounts.
- **Files modified:** `sources/ReactApp/src/components/useDrawerFocus.ts`
- **Commit:** a39749fa

**2. [Rule 3 - Blocking] Worktree had no `node_modules`**
- **Issue:** The fresh worktree had no installed dependencies, so `vitest` was not found.
- **Fix:** `npm ci` from the existing lockfile (no package added or changed). `src/generated/api.ts`, rebuilt by the Playwright web server, was reverted before each commit.

**3. Plan ledger file not written**
- The per-plan HEAD ledger under the git dir could not be written from the sandboxed shell. `plan_head_before` is the worktree base commit named in the dispatch, and `commits` was measured with `git rev-list --count` against it (3).

## Known Stubs

None.

## Threat Flags

None. T-69-53, T-69-54 and T-69-55 are mitigated as planned: the cycle catches Tab from body, the skip link is inert and the scrims are out of the tab order while open; the listener ignores focus outside the navigation; Escape is only marked handled while the popover is open, which closes before it requests a cost confirmation.

## Out of scope (unchanged)

IN-09 (focus after a drawer link goes to the hamburger) was not touched, as the plan states.

## Self-Check: PASSED

- Commits a39749fa, 9b08717a and 5e33ac6b are ancestors of HEAD.
- All eight modified files exist and are committed.

---
phase: 69-consistency-accessibility-verification
plan: 39
subsystem: ui
tags: [react, accessibility, focus-management, inert, playwright, vitest]

requires:
  - phase: 69-consistency-accessibility-verification
    provides: "69-34 shell layouts and 69-37 toast live regions (context only; 69-37 ordering is for the shared Playwright ports)"
provides:
  - "useDrawerFocus hook: focus move on open, Tab cycle while open, focus return on close"
  - "Command Center drawer and community menu keep keyboard focus inside while open, with an inert background"
  - "Chromium 390 px proof of focus containment for both navigations"
affects: [69-verification, FND-06]

actuals:
  tokens: 9000
  tasks: 3
  commits: 3
plan_head_before: 1b02af481ff2b350c4da789edc2812c5b340a2f9
plan_head_after: 2b0593ec7c3f326541c270041fe268819edbde10

tech-stack:
  added: []
  patterns:
    - "Focus return runs in an effect after the commit that removes inert, instead of a synchronous Escape handler"
    - "The Tab cycle handler sits on an ancestor of the navigation and acts only while it is open"

key-files:
  created:
    - sources/ReactApp/src/components/useDrawerFocus.ts
  modified:
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/tests/shell-layouts.test.tsx
    - sources/ReactApp/tests/e2e/public-shell.spec.ts

key-decisions:
  - "Focus return happens in an effect keyed on the open state, so it runs once the covered column is no longer inert"
  - "Focus the user moved outside the navigation (for example the language select) is never taken back on close"
  - "The Tab handler skips events whose default is already prevented, so the profile menu popover keeps its own wrap"

patterns-established:
  - "useDrawerFocus: one hook for every overlay navigation; callers own inert and closing"

requirements-completed: [FND-06]

coverage:
  - id: D1
    description: "Opening the Command Center drawer moves focus to its close button, makes the main column inert, cycles Tab inside and returns focus to the toggle on close"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#moves focus to the close button when the drawer opens and makes the covered column inert"
        status: pass
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#cycles Tab and Shift+Tab inside the open drawer"
        status: pass
      - kind: e2e
        ref: "tests/e2e/public-shell.spec.ts#opens and closes the command drawer with the keyboard on mobile"
        status: pass
    human_judgment: false
  - id: D2
    description: "The community menu moves focus to its first link, makes main and footer inert, cycles Tab between the toggle and its links and returns focus to the toggle"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#cycles Tab between the toggle and the navigation links while the menu is open"
        status: pass
      - kind: e2e
        ref: "tests/e2e/public-shell.spec.ts#contains keyboard focus in the open community menu on mobile"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 39: Mobile Navigation Focus Containment Summary

**A shared useDrawerFocus hook makes both mobile navigations take focus when they open, trap Tab behind an inert page, and hand focus back to the toggle on close (WR-08).**

## Performance

- **Duration:** about 45 min
- **Tasks:** 3
- **Files modified:** 5 (1 created)

## Accomplishments
- Command Center drawer: focus lands on the close button, `.command-main` is inert while open, Tab and Shift+Tab wrap inside the drawer, and every close path (Escape, close button, link, scrim) returns focus to the toggle.
- Community menu: focus lands on the first link, `main#main-content` and `footer.site-footer` are inert, and the Tab cycle includes the toggle; focus the user moved elsewhere (language select) is not taken.
- Playwright at 390 px proves real focus containment, inert state and focus return for both navigations; the existing drawer and fleet specs stay green (49 passed).

## Task Commits

1. **Task 1: Command Center drawer focus (tracer)** - `8a87676c` (fix)
2. **Task 2: Community menu focus** - `5e649443` (fix)
3. **Task 3: Browser proof at 390 px** - `2b0593ec` (test)

## Files Created/Modified
- `sources/ReactApp/src/components/useDrawerFocus.ts` - shared focus move, Tab cycle and focus return
- `sources/ReactApp/src/components/CommandCenterLayout.tsx` - hook, refs, inert main column; synchronous Escape handler removed
- `sources/ReactApp/src/components/CommunityLayout.tsx` - hook, nav ref, inert main and footer; synchronous Escape handler removed
- `sources/ReactApp/tests/shell-layouts.test.tsx` - 7 new cases for focus move, inert, Tab cycle, focus return
- `sources/ReactApp/tests/e2e/public-shell.spec.ts` - extended drawer test and new community menu test

## Decisions Made
See key-decisions in the frontmatter. The Tab handler also ignores already-prevented events, a small addition so the profile menu popover (inside the community header) keeps its own Tab wrap.

## Deviations from Plan

None - plan executed exactly as written. One test detail: the community link-click test uses the "Home" link because the unit-test router has only the index route, and navigating to another path would unmount the layout.

## Issues Encountered
- `npm run build:assets` (run by the Playwright web server) rewrote `src/generated/api.ts`; it was reverted before committing, as instructed.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
WR-08 is closed. The real-phone check stays on the milestone-end human list (deferred follow-up).

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/components/useDrawerFocus.ts
- FOUND commits: 8a87676c, 5e649443, 2b0593ec
- Verified: vitest (shell-layouts, profile-menu: 45 passed), typecheck, lint, Playwright chromium (49 passed)

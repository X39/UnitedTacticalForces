---
phase: 62-fleet-quick-actions
plan: 10
subsystem: webapp
status: complete
tags: [react, hooks, accessibility, live-region, focus-management, tanstack-query, vitest]

# Dependency graph
requires:
  - phase: 62-01
    provides: shared fleet fixtures (fleetServer, fleetAction)
  - phase: 62-03
    provides: Server_Fleet_Changed and Server_Fleet_ChangedMore (_one/_other) localization keys
  - phase: 62-07
    provides: diffFleetStates and FleetStateChange ({ serverId, name, stateLabelKey })
  - phase: 62-09
    provides: data-server-card on the card article and data-server-id on the Open server link
provides:
  - useFleetSubmitted (submitted cards derived from the fleet query's dataUpdatedAt, stable markSubmitted, markedAt map)
  - useFleetFocusFallback (focus moves to the Open server link only when a focused card control unmounted)
  - useFleetAnnouncements (polite, localized, bounded live region message for refetched state changes)
affects: [62-11, 62-12]

# Actuals (#2632)
actuals:
  tokens: 6600
  tasks: 3
  commits: 3
plan_head_before: 62c523513dfe58f1295caccc36e88a28c9241dd3
plan_head_after: 42fe15b86cbd7a5083635683bb300c6dfddbd471

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Derived clearing: transient UI state keyed to a query is cleared by comparing against dataUpdatedAt during render, never by an effect or a timer"
    - "Latest-value ref written in an effect after every render and read only in event handlers (markSubmitted)"
    - "Guarded adjust-state-during-render for snapshot diffs (useFleetAnnouncements), matching CostConfirmationProvider"
    - "DOM lookups by untrusted id compare attribute strings over querySelectorAll results instead of interpolating the id into a selector"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/useFleetSubmitted.ts
    - sources/ReactApp/src/features/hosting/useFleetFocusFallback.ts
    - sources/ReactApp/src/features/hosting/useFleetAnnouncements.ts
    - sources/ReactApp/tests/hosting/fleet-submitted.test.tsx
    - sources/ReactApp/tests/hosting/fleet-focus.test.tsx
    - sources/ReactApp/tests/hosting/fleet-announcements.test.tsx
  modified: []

key-decisions:
  - "useFleetFocusFallback stores the card id only for focusin events inside the container (null when the target is inside the container but outside any card) and keeps it when focus leaves the container, so a dialog in a portal does not erase the card the viewer came from"
  - "useFleetAnnouncements treats a change as viewer-caused when markedAt for the server is greater than or equal to the previous snapshot's dataUpdatedAt; an older mark no longer suppresses announcements"
  - "An announcement lasts until the next successful fetch: any dataUpdatedAt advance without new data clears it, so the live region never repeats a stale sentence"

patterns-established:
  - "Fleet behavior hooks stay page-independent: they take the query's data, dataUpdatedAt and isError as plain arguments so tests drive them with renderHook props"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "A card is submitted from the accepted action until the next successful fleet fetch, never while the query is in error, independent per card, with a stable markSubmitted and no effect-driven state or timer (D-12)"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-submitted.test.tsx#useFleetSubmitted (6 tests, including StrictMode)"
        status: pass
      - kind: command
        ref: "grep -c \"setInterval\\|setTimeout\" sources/ReactApp/src/features/hosting/useFleetSubmitted.ts -> 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "When a focused card control unmounts because the card changed section, focus moves to the same server's Open server link; focus on any other connected element is never moved; missing cards and selector-special ids never throw; the focusin listener is removed on unmount"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-focus.test.tsx#useFleetFocusFallback (6 tests, including StrictMode and listener cleanup)"
        status: pass
      - kind: command
        ref: "grep -c \"querySelector(\" sources/ReactApp/src/features/hosting/useFleetFocusFallback.ts -> 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "Refetched state changes produce a polite message: one sentence per changed server up to three, then a pluralized summary; nothing for first load, new or removed servers, unchanged refetches or viewer-caused changes; language switches re-render the message"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-announcements.test.tsx#useFleetAnnouncements (6 tests, English and German, StrictMode wrapper)"
        status: pass
      - kind: command
        ref: "grep -c useEffect sources/ReactApp/src/features/hosting/useFleetAnnouncements.ts -> 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "Screen-reader experience of the composed fleet page (announcement timing in a real assistive technology, focus visibility after a card moves)"
    requirement: FLT-04
    human_judgment: true
    rationale: "The hooks are proven in isolation; the page composition lands in 62-11 and real assistive-technology behavior needs human UAT"

# Metrics
duration: 6 min
completed: 2026-11-19
---

# Phase 62 Plan 10: Fleet Behavior Hooks Summary

**Three page-independent fleet hooks: submitted-card tracking derived from TanStack's dataUpdatedAt (no effect, no timer), a document-level focusin focus fallback to the Open server link that compares attribute strings, and a guarded during-render diff that produces a bounded, localized polite announcement.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-11-19T09:58:41Z
- **Completed:** 2026-11-19T10:05:25Z
- **Tasks:** 3
- **Files modified:** 6 (all created)

## Accomplishments

- `useFleetSubmitted(dataUpdatedAt, isError)` returns `submittedIds`, `markedAt` and a stable `markSubmitted`. A server is submitted while `isError` is false and `dataUpdatedAt <= recorded`; a new mark prunes marks older than the current data. Clearing is purely derived.
- `useFleetFocusFallback(containerRef, signature)` remembers the last card that held focus through one `document` `focusin` listener and, in a layout effect keyed on the signature, focuses the remembered server's `a[data-server-id]` only when focus is on the body, null or a detached node.
- `useFleetAnnouncements(servers, dataUpdatedAt, markedAt)` diffs consecutive snapshots with `diffFleetStates`, drops viewer-caused changes, and builds `Server_Fleet_Changed` sentences (max three) plus `Server_Fleet_ChangedMore` with the remaining count, translated on every render.

## Task Commits

1. **Task 1: useFleetSubmitted** - `f6391d37` (feat)
2. **Task 2: useFleetFocusFallback** - `cb1cf9ab` (feat)
3. **Task 3: useFleetAnnouncements** - `42fe15b8` (feat)

Each task followed RED (test file written first, failing on the missing module) then GREEN; the plan prescribes one commit per task, so test and implementation share that commit.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/useFleetSubmitted.ts` - submitted-card tracker
- `sources/ReactApp/src/features/hosting/useFleetFocusFallback.ts` - focus fallback
- `sources/ReactApp/src/features/hosting/useFleetAnnouncements.ts` - announcement message
- `sources/ReactApp/tests/hosting/fleet-submitted.test.tsx` - 6 tests
- `sources/ReactApp/tests/hosting/fleet-focus.test.tsx` - 6 tests
- `sources/ReactApp/tests/hosting/fleet-announcements.test.tsx` - 6 tests

## Decisions Made

- The focus hook keeps the remembered card when focus moves outside the container (for example into the confirmation dialog portal) and resets it to null when focus moves to a non-card element inside the container.
- A change is viewer-caused when its server's mark is at or after the previous snapshot's `dataUpdatedAt`, which is exactly the value `useFleetSubmitted` records at mark time.
- Any successful fetch replaces the announcement, so a no-change refetch clears it.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- `npm run format:check` still reports `src/auth/claims.ts` (CRLF working copy only) and `tests/hosting/fleet-fixtures.test.ts`; both are pre-existing entries in `deferred-items.md` and not owned by this plan. Every file created here passes `prettier --check`.

## Verification

- `npm --prefix sources/ReactApp test`: 106 files, 890 tests passed
- `npm --prefix sources/ReactApp run typecheck`: clean
- `npm --prefix sources/ReactApp run lint` (`--max-warnings 0`): clean
- `npm --prefix sources/ReactApp run format:check`: only the two pre-existing deferred files
- Acceptance greps: one export per hook; `setInterval|setTimeout` = 0 in useFleetSubmitted; `querySelector(` = 0 in useFleetFocusFallback; `diffFleetStates` = 3 and `useEffect` = 0 in useFleetAnnouncements

## Threat Mitigations

- T-62-23: server titles are passed only as i18next interpolation values (the app sets `escapeValue: false`, so the page must render the string as text, which a live region text node does); the focus lookup compares `getAttribute("data-server-id")` with the stored id and never builds a selector or HTML from it (test 6 uses an id containing `"]` and a comma).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for 62-11: the page wires `onSubmitted` to `markSubmitted`, passes `submittedIds.has(id)` to each card, passes `markedAt` to `useFleetAnnouncements`, renders its result in a visually hidden `aria-live="polite"` region, and calls `useFleetFocusFallback` with the sections container and a per-section id signature.

## Self-Check: PASSED

- FOUND: all six created files
- FOUND: f6391d37, cb1cf9ab, 42fe15b8 (ancestors of HEAD)

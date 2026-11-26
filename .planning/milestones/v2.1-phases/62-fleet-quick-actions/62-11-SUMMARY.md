---
phase: 62-fleet-quick-actions
plan: 11
subsystem: webapp
status: complete
tags: [react, react-router, tanstack-query, url-state, search, accessibility, css-tokens, vitest]

# Dependency graph
requires:
  - phase: 62-02
    provides: costOwnerUserId on GameServer for the Mine filter
  - phase: 62-03
    provides: Server_Fleet_* strings, Servers vocabulary, onSubmitted on the shared confirmation
  - phase: 62-06
    provides: FleetLiveUpdatesProvider mounted in both layouts, so the fleet poll could be removed
  - phase: 62-07
    provides: fleetModel (URL state, search, filters, partition, cost count)
  - phase: 62-08
    provides: FleetToolbar, FleetLiveNotice, SearchField, FilterChipGroup
  - phase: 62-09
    provides: FleetServerCard and FleetSection
  - phase: 62-10
    provides: useFleetSubmitted, useFleetFocusFallback, useFleetAnnouncements
provides:
  - Rebuilt ServerFleetPage at /command/servers composed from the model, hooks, toolbar, sections and cards
  - Poll-free useFleetQuery (single fleet query key, no refetchInterval)
  - Token-only fleet-page.css (cost line, section stack)
  - tests/hosting/fleet-page.test.tsx (39 page behavior tests)
  - Legacy ServerCard, ServerFleetView, summary strip and their styles removed; three legacy suites migrated
affects: [62-12, 63]

# Actuals (#2632)
actuals:
  tokens: 18900
  tasks: 2
  commits: 2
plan_head_before: b6e33849c4910c788c65ab9ae2818902169cfd04
plan_head_after: c6fa8ff8434c4a8671f42321be902dd7cc73e56a

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Search draft { value, base } rebased during render once the URL catches up with the typed text, so typed whitespace survives the debounced URL write and an external navigation still resets the field without an effect"
    - "Debounced URL writes call setSearchParams through a latest-value ref, so a timer started before a filter change never writes stale parameters"

key-files:
  created:
    - sources/ReactApp/src/components/server/fleet-page.css
    - sources/ReactApp/tests/hosting/fleet-page.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/FleetPages.tsx
    - sources/ReactApp/src/features/hosting/useFleetQuery.ts
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/tests/hosting/fleet.test.tsx
    - sources/ReactApp/tests/hosting/member-access.test.tsx
    - sources/ReactApp/tests/hosting/server-state-surfaces.test.tsx

key-decisions:
  - "62-11: the search draft is rebased to the new URL q during render when the URL equals the normalized draft, so trailing spaces typed before a pause are kept while external navigation still resets the field"
  - "62-11: setSearchParams is called through a latest-value ref from the debounce timer; react-router's functional form uses the searchParams captured by the callback, which would be stale inside a timer"
  - "62-11: clearing the search (empty or whitespace-only text) writes q immediately instead of waiting for the 250 ms debounce"
  - "62-11: the live notice also renders above the empty-fleet panel; toolbar, cost line and sections are still omitted for an empty fleet"
  - "62-11: the orderFleetServers re-export was removed from FleetPages.tsx; fleet.test.tsx imports it from fleetModel"

patterns-established:
  - "Page tests drive refetches by resolving a deferred second servers response through Refresh servers or queryClient.invalidateQueries, and read history replace through router.state.historyAction"

requirements-completed: [FLT-01, FLT-02, FLT-03, FLT-04, FLT-05, ACC-02]

coverage:
  - id: D1
    description: "One-scan fleet: h1 Servers (Server in German), whole-fleet cost line, counted toolbar, Active then Off sections with h3 cards; a possible-cost stopped server is never in Off; Off has no disclosure control; Active renders an empty line in All and Mine"
    requirement: FLT-01
    verification:
      - kind: integration
        ref: "tests/hosting/fleet-page.test.tsx#structure, #cost line and counts, #filters"
        status: pass
    human_judgment: false
  - id: D2
    description: "Search and filter live in ?q= and ?filter= on the same route, written with history replace (q after 250 ms), other parameters preserved, restored from shared links, invalid filters fall back to All, external navigation resets the field; search matches title, game and real cost owner, ignores case and diacritics, requires every term"
    requirement: FLT-02
    verification:
      - kind: integration
        ref: "tests/hosting/fleet-page.test.tsx#search and URL state, #search semantics"
        status: pass
    human_judgment: false
  - id: D3
    description: "Off servers are grouped, never hidden; Running lists live or maybe cost only, Off lists off only, Mine lists created or paid servers; no-match and Mine-empty panels replace the sections and the clear action resets both parameters and focuses search; empty-fleet panels show no toolbar, cost line or sections"
    requirement: FLT-03
    verification:
      - kind: integration
        ref: "tests/hosting/fleet-page.test.tsx#filters, #empty states"
        status: pass
    human_judgment: false
  - id: D4
    description: "Quick start and stop through the shared confirmation, submitted In progress state with Request sent until the refetched fleet arrives, no optimistic badge flip; no polling (no refetchInterval, no extra call after one minute); delayed live notice with Reconnect now; polite announcements; focus kept on a card that changes section"
    requirement: FLT-04
    verification:
      - kind: integration
        ref: "tests/hosting/fleet-page.test.tsx#quick start, #no polling, #live notice, #announcements and focus, #states"
        status: pass
      - kind: other
        ref: "grep -c 'refetchInterval: 10_000' useFleetQuery.ts == 0; grep -c setInterval FleetPages.tsx == 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "Member access on the new card: bare sv sees enabled Start server and Shut down and stop billing with no terminate control; bare sv:view sees both aria-disabled with the operate reason in English and German; create control, empty, forbidden, loading and query-once assertions still pass; surfaces test reads identical state on the new card"
    requirement: ACC-02
    verification:
      - kind: integration
        ref: "tests/hosting/member-access.test.tsx; tests/hosting/server-state-surfaces.test.tsx"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp test (107 files, 918 tests); typecheck; lint; locales:check; contracts:check; build:assets"
        status: pass
    human_judgment: false
  - id: D6
    description: "fleet-page.css is token-only and covered by the server style guard"
    requirement: FLT-05
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps fleet-page.css free of raw colours and pixel literals"
        status: pass
    human_judgment: false
  - id: D7
    description: "Composed page at 320 and 390 px in English and German, real screen-reader announcement timing and visible focus after a card moves"
    requirement: FLT-05
    verification: []
    human_judgment: true
    rationale: "jsdom does not compute layout or run assistive technology; the Playwright spec of Plan 62-12 and the UAT verify widths, axe results and screen-reader behavior"

# Metrics
duration: 14 min
completed: 2026-11-19
---

# Phase 62 Plan 11: Fleet Page Composition Summary

**`/command/servers` is now one working fleet screen: URL-backed search and counted filters (history replace, 250 ms debounce for `q`), a whole-fleet cost line, Active and Off sections of `FleetServerCard`s, no-match and empty panels, quick start and stop through the shared confirmation with the submitted state, the delayed live notice, polite announcements and the focus fallback. The fleet query no longer polls, and the legacy card, summary strip and their styles are gone.**

## Performance

- **Duration:** about 14 min
- **Started:** 2026-11-19T10:08:22Z
- **Completed:** 2026-11-19T10:22:36Z
- **Tasks:** 2
- **Files modified:** 9 (2 created, 7 modified)

## Accomplishments

- `ServerFleetPage` keeps the old scaffold (document title, auth, permissions, `HostingAccessBoundary`, loading, error-without-data and stale-data error panels, `aria-busy`). It now composes:
  - the header with a ghost Refresh servers control that is disabled while fetching, and a Create control: a `primary` link for a creator, a disabled `primary` button with the permission reason for everyone else, and no header control for a creator whose fleet is empty;
  - the cost line (`role="note"`, plural, locale-formatted count over the whole fleet);
  - `FleetToolbar` with chip counts computed over the search result;
  - `FleetLiveNotice` fed by `useFleetLiveUpdates()`;
  - the Active and Off sections (or the no-match or Mine-empty panel) inside the container that feeds `useFleetFocusFallback`;
  - an always-present visually hidden polite region that holds `useFleetAnnouncements`.
- URL state is read with `parseFleetSearchParams` and written with `writeFleetSearchParams` in functional form with `{ replace: true }`. The typed text filters immediately through a `{ value, base }` draft. It reaches the URL after 250 ms through one debounce timer, and clearing writes immediately. The clear action resets both parameters and focuses the search field.
- `useFleetQuery` has no `refetchInterval`. The single `hostingQueryKeys.fleet` key is still shared with the overview and the profile menu.
- `fleet-page.css` styles the warning-token cost line and the `--utf-space-xl` section stack, using tokens only.
- 39 page tests cover structure (EN and DE), the cost line and counts, the filters, URL replace, restore and reset, search semantics, the empty panels, the header, quick start (confirm, dismiss, reject), no polling, the live notice, announcements and focus, the loading, error and busy states, and the absence of terminate on cards.
- The legacy `ServerCard`, `ServerFleetView`, the `Metric` strip use on this page and the `next-hosting-server-card*` and `next-hosting-card-grid` rules (including the narrow-width media block) were removed. `fleet.test.tsx` keeps the ordering and overview tests. `server-state-surfaces.test.tsx` renders `FleetServerCard`. `member-access.test.tsx` asserts start and stop controls instead of terminate.

## Task Commits

1. **Task 1: Rebuild ServerFleetPage, remove the fleet poll and add the page styles and tests** - `18d54651` (feat)
2. **Task 2: Remove the legacy card and styles and migrate the legacy fleet tests** - `c6fa8ff8` (refactor)

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/FleetPages.tsx` - rebuilt `ServerFleetPage`. `HostingOverviewView` and `HostingOverviewPage` are unchanged. The `orderFleetServers` re-export was dropped.
- `sources/ReactApp/src/features/hosting/useFleetQuery.ts` - poll removed, JSDoc updated
- `sources/ReactApp/src/components/server/fleet-page.css` - cost line and section stack
- `sources/ReactApp/tests/hosting/fleet-page.test.tsx` - page behavior tests
- `sources/ReactApp/src/features/hosting/components.tsx` - legacy `ServerCard` and its imports removed
- `sources/ReactApp/src/features/hosting/hosting.css` - legacy card and card-grid rules removed
- `sources/ReactApp/tests/hosting/fleet.test.tsx`, `member-access.test.tsx`, `server-state-surfaces.test.tsx` - migrated

## Decisions Made

- After the debounced write, the search draft is rebased during render (the guarded adjust-state pattern) once the URL `q` equals the normalized draft. This keeps a trailing space the viewer typed before pausing. An external navigation to a different `q` still resets the field, with no effect involved.
- The debounce timer calls `setSearchParams` through a latest-value ref. React Router's functional updater receives the parameters captured by the callback it was called on, so a timer started before a filter chip click would otherwise write the old filter back.
- An empty or whitespace-only search writes `q` removal immediately. Typed text still waits 250 ms.
- The live notice renders whenever the fleet has loaded, including above the empty-fleet panel. The UI-SPEC limits only the toolbar, the cost line and the sections for the empty case.

## Deviations from Plan

None. The plan was executed as written. The decisions above are implementation choices inside the plan's draft and URL rules.

## Issues Encountered

- A mutation check of the no-polling test (temporarily re-adding `refetchInterval`) used `git checkout` on `useFleetQuery.ts` and reverted the uncommitted Task 1 edit. I rewrote the file before committing. The committed content was verified by the acceptance grep (`refetchInterval: 10_000` count 0) and by the tests.
- `format:check` still reports the two known pre-existing files from `deferred-items.md` (`src/auth/claims.ts` CRLF working copy, `tests/hosting/fleet-fixtures.test.ts`). This plan did not touch either file, and every file it changed passes `prettier --check`.

## Verification

- `npm --prefix sources/ReactApp test`: 107 files, 918 tests passed
- `typecheck`, `lint` (`--max-warnings 0`), `locales:check`, `contracts:check` and `build:assets` all pass. `format:check` reports only the two pre-existing files.
- `tests/hosting/parity-matrix.test.ts` passes (74 tests, no row removed)
- `git diff --exit-code -- sources/Common/Ui/routes.json` exits 0

## User Setup Required

None. No external service configuration is required.

## Next Phase Readiness

- Plan 62-12 can run the Playwright fleet spec (320 and 390 px, EN and DE, axe) against the composed page.
- Known limitation carried from 62-07: attention and unconfirmed cards show the API's disabled stop with its reason (literal UI-SPEC table).

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

## Self-Check: PASSED

- FOUND: `sources/ReactApp/src/components/server/fleet-page.css`, `sources/ReactApp/tests/hosting/fleet-page.test.tsx`
- FOUND: `18d54651` and `c6fa8ff8` are ancestors of HEAD. `check evaluation-scope --plan 62-11` matches commits by plan id in the subject. The project commit rule uses `type(webapp)` subjects, so it returns no matches; ancestry is the evidence instead.
- Every acceptance criterion of both tasks re-ran green (greps, tests, parity matrix, `routes.json` unchanged).

---
phase: 62-fleet-quick-actions
plan: 12
subsystem: webapp
status: complete
tags: [playwright, axe, websocket, e2e, parity-matrix, documentation, phase-gate]
# Dependency graph
requires:
  - phase: 62-04
    provides: fleet change interceptor that emits the signal the e2e frame imitates
  - phase: 62-05
    provides: fleet subscription gate, keep-alive and the Fleet change signal documentation
  - phase: 62-06
    provides: useUpdateStream reconnect and FleetLiveUpdatesProvider refetch on open
  - phase: 62-11
    provides: rebuilt ServerFleetPage with URL state, quick actions and the delayed live notice
provides:
  - tests/e2e/support/fleetStream.ts (mockFleetStream, fleetChangedFrame) for WebSocket-mocked e2e specs
  - tests/e2e/hosting-fleet.spec.ts (20 browser tests for layout, axe, URL state, live update, quick start, notice)
  - Migrated German fleet deep-link assertions in public-shell.spec.ts
  - Every Phase 62 parity row closed with evidence, plus FLT-search-filter and FLT-live-updates rows
  - "Fleet and live updates" section in docs/features/react-ui-beta.md and a corrected reconnect paragraph
  - Phase 62 validation record with gate results, Nyquist compliance and the UAT items
affects: [62-verify, 63, 69]
# Actuals (#2632)
actuals:
  tokens: 16700
  tasks: 3
  commits: 4
plan_head_before: c3c129ef7172b413b1598f1e7e2edcf3923275c1
plan_head_after: e1f9aa21d662eb2a5ae33df4082e619827fd8e44
# Tech tracking
tech-stack:
  added: []
  patterns:
    - "page.routeWebSocket without connectToServer mocks the update stream; closing later connections inside the handler before it returns keeps the client in one disconnection episode"
    - "Hold the fleet GET behind a gate after a confirmed action so the submitted state is observable before the refetch lands"
key-files:
  created:
    - sources/ReactApp/tests/e2e/support/fleetStream.ts
    - sources/ReactApp/tests/e2e/hosting-fleet.spec.ts
  modified:
    - sources/ReactApp/tests/e2e/public-shell.spec.ts
    - sources/ReactApp/tests/hosting/fleet-fixtures.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - docs/features/react-ui-beta.md
    - .planning/phases/62-fleet-quick-actions/62-VALIDATION.md
    - .planning/phases/62-fleet-quick-actions/deferred-items.md
key-decisions:
  - "62-12: mockFleetStream.closeFromServer also refuses every later connection by closing it inside the route handler, before Playwright opens it, so the client never reopens and the 3 second notice can appear"
  - "62-12: e2e viewport checks allow 1 CSS pixel of subpixel rounding, and a whole card is checked on the horizontal axis only because a 320x568 viewport is shorter than a card"
  - "62-12: FLT-card is recorded as status relocated with the word Partial in New location, because the parity guard allows only pending, kept, relocated and removed"
requirements-completed: [FLT-04, FLT-05, ACC-02]
coverage:
  - id: D1
    description: "Populated fleet, no-match panel and disconnected notice at 320x568 and 390x844 in English and German: no horizontal page scroll, every card primary and Open server control fully in the viewport after scrolling, chips not clipped, axe WCAG 2.1 AA with zero violations and at least one rule evaluated"
    requirement: FLT-05
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts#fleet layout (populated, no-match, disconnected notice)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A 256-character title and an unbroken long cost owner fit at 320 and 390 px in both languages"
    requirement: FLT-05
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts#a 256-character title and a long cost owner do not overflow"
        status: pass
    human_judgment: false
  - id: D3
    description: "A frame pushed through the mocked update stream updates a card without a navigation; quick start shows In progress with Request sent while the badge stays Off, then Starting after the frame; the stream path is game-servers/fleet; no unmocked request in any scenario"
    requirement: FLT-04
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts#a pushed fleet frame updates the card without a reload, #quick start shows the submitted state, then the reported state"
        status: pass
    human_judgment: false
  - id: D4
    description: "Search and filter restored from a shared link and after a reload; typing writes q with history replace and does not grow history.length"
    requirement: FLT-04
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts#search and filter live in the URL without growing the history"
        status: pass
    human_judgment: false
  - id: D5
    description: "Servers vocabulary: fleet heading, document title and the Command Center drawer link read Servers; public-shell nav and German deep-link specs pass with Server, Läuft, Server öffnen and the German cost line, without the terminate link"
    requirement: ACC-02
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts#the Command Center nav and the fleet page read Servers; tests/e2e/public-shell.spec.ts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Parity matrix: no Phase 62 row pending, FLT-summary-metrics, FLT-card, FLT-card-cost-warning and FLT-card-cost-stop relocated with real locations, two new rows"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts"
        status: pass
    human_judgment: false
  - id: D7
    description: "Phase gate: npm run ci, the GameServerHosting dotnet tests, routes.json identical, sources/WebApp and sources/Database untouched since de6261e4"
    requirement: FLT-05
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run ci (918 tests); dotnet test --filter GameServerHosting (580 tests); git diff --exit-code -- sources/Common/Ui/routes.json"
        status: pass
    human_judgment: false
  - id: D8
    description: "Idle socket behind the production nginx, the disabled stop on attention and unconfirmed cards, the accepted id and timing disclosure, and a visual review of German text on a phone"
    requirement: FLT-04
    verification: []
    human_judgment: true
    rationale: "Proxy idle timeouts, product judgment and an accepted risk cannot be asserted by Playwright; they are listed as manual-only and UAT rows in 62-VALIDATION.md"
# Metrics
duration: 16 min
completed: 2026-11-19
---

# Phase 62 Plan 12: Fleet Browser Evidence and Phase Gate Summary

**A Playwright fleet spec with a `routeWebSocket` stream mock proves the fleet at 320 and 390 px in English and German (no horizontal scroll, controls in the viewport, zero axe violations), a live frame updating a card without a reload, quick start through the submitted state, URL state across reloads and the delayed disconnected notice. The Phase 62 parity rows are closed, the React docs describe the fleet and the live stream, and the phase gate is green.**

## Performance

- **Duration:** about 16 min
- **Started:** 2026-11-19T10:25:11Z
- **Completed:** 2026-11-19T10:42:01Z
- **Tasks:** 3
- **Files modified:** 8 (2 created, 6 modified)

## Accomplishments

- `tests/e2e/support/fleetStream.ts` exports `fleetChangedFrame(serverId)`, which builds the pinned frame from Plan
  62-01, and `mockFleetStream(page)`. The mock never connects to a server. It records every connection and its
  `path`, and exposes `send`, `closeFromServer`, `connectionCount` and `paths`.
- `tests/e2e/hosting-fleet.spec.ts` has 20 tests:
  - For each culture (en, de) and viewport (320x568, 390x844), the same four test bodies cover the populated fleet,
    the no-match panel, the disconnected notice and the long-text fixture: a 256-character title and an unbroken
    long cost owner.
  - English tests at 390x844 cover URL state with reload and history length, the live update, quick start and the
    Servers vocabulary.
  - Every test aborts and records unmocked API requests and asserts there were none. Every axe scan asserts that at
    least one rule was evaluated.
- In `public-shell.spec.ts`, the German fleet deep-link test now expects the badge `Läuft`, the German cost line for
  one costing server and `Server öffnen` linking to `/next/command/servers/{id}`. It also asserts that the old
  terminate link and the old per-card cost sentence are absent, and it keeps the reload check. The nav assertion
  already read `Servers` (62-03).
- In the parity matrix, 12 pending Phase 62 rows now have a status and evidence. FLT-card and FLT-card-cost-stop
  moved from `kept` to `relocated` with real new locations, with the D-11 terminate path named in the workspace and
  the `?action=terminate` deep link. ACT-start, ACT-stop and FLT-empty-state gained Phase 62 evidence. FLT-search-filter
  and FLT-live-updates were added. No row was deleted or removed.
- `docs/features/react-ui-beta.md` has a new "Fleet and live updates" section. It covers the URL parameters, the
  resource-agnostic `useUpdateStream`, the provider in both layouts, which pollers remain until Phase 63, and the
  `/api` WebSocket upgrade requirement for Vite and nginx. The stale "Client behavior" paragraph from 62-05 now
  describes the shipped behavior: backoff of 1, 2, 4, 8 and 16 seconds, then 30 seconds, with jitter; an immediate
  retry on online, visible or Reconnect now; and a refetch on every open.
- `62-VALIDATION.md` marks all 29 per-task rows green and ticks Wave 0 item by item. It sets `wave_0_complete` and
  `nyquist_compliant` to true, records the gate results and lists the nginx idle check plus three UAT rows: the
  disabled stop on attention and unconfirmed cards, the accepted id and timing disclosure, and German text by layout
  metrics only.

## Task Commits

1. **Task 1: Fleet e2e spec with the stream helper, and the public-shell label updates** - `d1356fb9` (test)
2. **Task 2: Parity matrix rows and the React documentation** - `df5d71d5` (docs)
3. **Task 3: Phase gate and validation record** - `033464cd` (style, gate fix) and `e1f9aa21` (docs)

## Files Created/Modified

- `sources/ReactApp/tests/e2e/support/fleetStream.ts` - WebSocket mock helper and pinned frame builder
- `sources/ReactApp/tests/e2e/hosting-fleet.spec.ts` - fleet browser spec
- `sources/ReactApp/tests/e2e/public-shell.spec.ts` - German fleet deep-link assertions for the rebuilt card
- `sources/ReactApp/tests/hosting/fleet-fixtures.test.ts` - Prettier formatting only (gate fix)
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - Phase 62 rows closed, two rows added
- `docs/features/react-ui-beta.md` - Fleet and live updates section, corrected client behavior paragraph
- `.planning/phases/62-fleet-quick-actions/62-VALIDATION.md` - gate results, Wave 0, sign-off, manual and UAT rows
- `.planning/phases/62-fleet-quick-actions/deferred-items.md` - resolution note for the formatting items

## Decisions Made

- After `closeFromServer`, the mock closes every new connection inside the route handler. Playwright then never opens
  the page socket, so the client gets a close without an open and stays in one disconnection episode. A plain close
  would reconnect after about one second and the notice would never appear.
- Viewport edge checks allow 1 CSS pixel, because `scrollIntoViewIfNeeded` leaves fractional offsets such as
  844.297. A whole card is checked horizontally only, because at 320x568 a card is taller than the viewport.
- The quick-start test holds the fleet GET after confirming, because the confirmation invalidates the fleet at once.
  Without the hold, the refetch would end the submitted state before it could be observed. The test then releases the
  hold with the Starting list and pushes a frame.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Phase gate `format:check` failed on the known deferred items**
- **Found during:** Task 3
- **Issue:** `npm run ci` reported `tests/hosting/fleet-fixtures.test.ts` (Plan 62-01, not Prettier formatted) and
  `src/auth/claims.ts` (CRLF in the working copy only, index LF).
- **Fix:** Ran Prettier on the fixture test and normalized the claims working copy to LF. The claims change has no
  content diff and nothing was committed for it.
- **Files modified:** `sources/ReactApp/tests/hosting/fleet-fixtures.test.ts`
- **Commit:** `033464cd`

**2. [Rule 1 - Test bug] Initial spec assumptions corrected before the first commit**
- **Found during:** Task 1
- **Issue:** The first run showed four problems. A search for `alpha ops` matches nothing, because the search uses
  substrings. The desktop nav link is not rendered at 390 px. The whole-card vertical check cannot pass at 320x568.
  Edges failed by subpixel amounts.
- **Fix:** Search for `alpha oper`, open the Command Center drawer before reading the nav link, check the card on the
  horizontal axis only, and allow 1 px of tolerance. No product code changed.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-fleet.spec.ts`
- **Commit:** `d1356fb9`

**Total deviations:** 2 auto-fixed (1 blocking gate fix, 1 test correction). **Impact:** the browser layer found no
product defect at 320 or 390 px, and no app code changed.

## Issues Encountered

None beyond the deviations above. The new spec passed 60 of 60 runs under `--repeat-each 3`.

## Known Stubs

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase complete, ready for `/gsd-verify-work 62`. The manual-only nginx idle check and the UAT rows in
  `62-VALIDATION.md` are for the end-of-phase review.
- Pixel baselines for German text at 320 and 390 px remain a Phase 69 item.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/e2e/support/fleetStream.ts
- FOUND: sources/ReactApp/tests/e2e/hosting-fleet.spec.ts
- FOUND: commits d1356fb9, df5d71d5, 033464cd, e1f9aa21 on HEAD
- Acceptance: grep 320 = 1, routeWebSocket = 2, `name: "Game servers"` = 0, Spielserver = 0, pending Phase 62 rows = 0,
  FLT-search-filter = 1, FLT-live-updates = 1, FLT-summary-metrics relocated = 1, "## Fleet and live updates" = 1,
  wave_0_complete true = 1, nyquist_compliant true = 1, routes.json identical, WebApp and Database diff empty

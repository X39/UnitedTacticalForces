---
phase: 62-fleet-quick-actions
plan: 06
subsystem: webapp
tags: [react, websocket, update-stream, tanstack-query, reconnect, backoff, vitest, fake-timers]

# Dependency graph
requires:
  - phase: 62-01
    provides: tracer useUpdateStream hook, FleetLiveUpdatesProvider, FakeWebSocket test double, pinned fleet frame
  - phase: 62-05
    provides: proven server delivery path and the 25 second WebSocket keep-alive
provides:
  - Reconnecting useUpdateStream (1-16 s backoff, 30 s cap, 20 percent jitter, online/visibility/reconnectNow triggers)
  - Pure helpers nextDelayMs and createCoalescer exported from src/app/updateStream.ts
  - FleetLiveUpdatesProvider with 500 ms leading-edge coalescing, the open-refetch rule and a refetching reconnectNow
  - Provider mounted inside CostConfirmationProvider in CommunityLayout and CommandCenterLayout
affects: [62-08, 62-11, phase 63 notification and workspace live updates]

# Actuals (#2632)
actuals:
  tokens: 18400
  tasks: 2
  commits: 2
plan_head_before: 79078bb22f3ed8e1cdc4afa495ae0813d164146e
plan_head_after: e1d1bc683d1f7c07d65bbeae239077be20329f6b

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-effect-run connection controller owning one socket and at most one retry timeout; hook-instance session ref for the ever-opened flag"
    - "Leading-edge coalescer with one trailing run per window, created once per mount with useMemo and cancelled in an effect cleanup (not terminal, so StrictMode remount keeps working)"
    - "Fake-timer hook tests with Math.random fixed at 0.5 so every retry delay is exact"

key-files:
  created:
    - sources/ReactApp/tests/app/update-stream.test.tsx
    - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
  modified:
    - sources/ReactApp/src/app/updateStream.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-layouts.test.tsx
    - sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx

key-decisions:
  - "Immediate retry triggers (online, visible, reconnectNow) act only while disconnected and between attempts; they never open a second socket while a retry is in flight"
  - "An error event is treated like a close (socket detached and closed), so the error-then-close sequence counts as one failed attempt"
  - "toSocketUrl now appends a missing trailing slash to the base so its last path segment is kept"
  - "The open rule refetches when the cached fleet dataUpdatedAt is strictly older than the open time; no cached data means no refetch"

patterns-established:
  - "Live data providers refetch from onOpen instead of relying on any polling fallback"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "The update stream client retries on the D-15 schedule, retries immediately on online, visibility and reconnectNow, reports connecting/open/reconnecting/unavailable and the epoch, and leaks no socket, timer or listener"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/app/update-stream.test.tsx (16 tests)"
        status: pass
      - kind: other
        ref: "grep -c setInterval sources/ReactApp/src/app/updateStream.ts == 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "Fleet signals invalidate only the fleet and notifications keys with exact true, coalesced to one immediate and one trailing refetch per 500 ms; every open after older data and every reconnectNow refetches; only viewers with server access subscribe"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-live-updates.test.tsx (11 tests)"
        status: pass
      - kind: unit
        ref: "tests/hosting/fleet-signal-tracer.test.tsx (5 tests, unchanged semantics)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Both route layouts mount the provider inside the cost confirmation provider and open exactly one fleet socket for a server member and none for an anonymous visitor"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirm-layouts.test.tsx (5 tests) and tests/profile-menu.test.tsx"
        status: pass
      - kind: other
        ref: "git diff -w de6261e4 -- CommunityLayout.tsx adds only the import and wrapper lines; routes.json unchanged"
        status: pass
    human_judgment: false
  - id: D4
    description: "A real browser recovers the socket after a network drop or sleep and the fleet refreshes without a reload"
    requirement: FLT-04
    verification: []
    human_judgment: true
    rationale: "Real browser online and visibility events and a real proxy drop are only observable in the deployed app; the visible indicator arrives in 62-08 and 62-11"

# Metrics
duration: 8min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 06: Fleet Live Client Reconnect and Layout Mount Summary

**`useUpdateStream` now recovers from every disconnect by itself. It retries after 1, 2, 4, 8 and 16 seconds, then every 30 seconds, each with 20 percent jitter. It also retries immediately on `online`, when the page becomes visible, and on `reconnectNow`, and it reports `unavailable` after five failed attempts that never opened. The fleet provider refetches exactly the fleet and notifications queries: a burst of signals triggers one refetch at once and at most one more per 500 ms. It also refetches on every open that follows older cached data and on every `reconnectNow`. Both route layouts now mount it, so removing the page poll in 62-11 cannot leave a stale fleet.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-11-19T09:15:19Z
- **Completed:** 2026-11-19T09:23:44Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- `updateStream.ts`:
  - New pure exports `nextDelayMs(attempt, random)` and `createCoalescer(run, windowMs)`, both with TSDoc.
  - The per-effect-run controller owns the socket, a single retry timeout, the failure counter and a disposed flag. The ever-opened flag lives in a hook-instance ref.
  - There is no interval timer.
- `FleetLiveUpdatesProvider`:
  - A memoized base URL and a coalescer that is created once per mount and cancelled on unmount.
  - Frames are filtered by contract type name plus a string `serverId`.
  - The `onOpen` rule uses `getQueryState(hostingQueryKeys.fleet)`, and `reconnectNow` reconnects and then refetches.
  - Every invalidation uses `exact: true`, so per-server workspace queries are never refetched by a signal.
- Both layouts nest `<FleetLiveUpdatesProvider configuration={configuration}>` directly inside `CostConfirmationProvider`. `CommandCenterLayout`'s early returns stay unwrapped, and no markup was added (D-01).
- 32 new or extended tests. The full ReactApp suite is green (97 files, 790 tests), and typecheck and lint are clean.

## Task Commits

1. **Task 1: Reconnecting update stream client with backoff, online and visibility triggers** - `9ec1df24` (feat)
2. **Task 2: Coalesced exact invalidation, the open-refetch rule, and the provider in both layouts** - `e1d1bc68` (feat)

**Plan metadata:** recorded in the following docs(62-06) commit

## Files Created/Modified
- `sources/ReactApp/src/app/updateStream.ts` - reconnect loop, retry triggers, status machine, `nextDelayMs`, `createCoalescer`
- `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx` - coalesced exact invalidation, open rule, refetching `reconnectNow`
- `sources/ReactApp/src/components/CommunityLayout.tsx` - provider mounted (import plus wrapper; inner shell re-indented)
- `sources/ReactApp/src/components/CommandCenterLayout.tsx` - provider mounted around the returned shell only
- `sources/ReactApp/tests/app/update-stream.test.tsx` - 16 helper, status-machine, trigger, callback and cleanup tests
- `sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx` - 11 provider tests
- `sources/ReactApp/tests/hosting/cost-confirm-layouts.test.tsx` - 3 layout socket tests
- `sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx` - Prettier formatting only; assertions unchanged

## Decisions Made
- Immediate triggers act only between attempts while disconnected. A retry already in flight is never duplicated, so the T-62-16 rule of at most one pending attempt holds.
- An `error` event detaches and closes the socket and counts as the failure. The `close` event that follows is ignored, so one attempt is counted once.
- `useFleetLiveUpdates.ts` is unchanged because the value type needed no new member.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `toSocketUrl` dropped the last path segment of a base without a trailing slash**
- **Found during:** Task 1 (behavior test 1)
- **Issue:** `new URL("update-stream/subscribe", "https://host/api")` resolves to `/update-stream/subscribe`, so the `api` segment was lost. The behavior spec requires that the segment be kept.
- **Fix:** Copy the base and append `/` when its pathname does not end with one.
- **Files modified:** sources/ReactApp/src/app/updateStream.ts
- **Commit:** 9ec1df24

**2. [Rule 3 - Blocking] Prettier formatting of plan-owned files**
- **Found during:** Task 2 verification (`format:check`)
- **Issue:** `fleet-signal-tracer.test.tsx` and `FleetLiveUpdatesProvider.tsx` were not formatted at HEAD (logged by 62-03 in deferred-items). Wrapping the layouts also needed a re-indent, and Prettier rewrapped one long JSX spread line in `CommandCenterLayout`.
- **Fix:** Ran `prettier --write` on the plan-owned files. `git diff -w` of the layouts shows only the import and wrapper lines (plus that single rewrap).
- **Files modified:** fleet-signal-tracer.test.tsx, both layouts, provider
- **Commit:** e1d1bc68

---

**Total deviations:** 2 auto-fixed (1 bug, 1 formatting)
**Impact on plan:** None on scope.

## Issues Encountered
- `format:check` still reports two files outside this plan. `tests/hosting/fleet-fixtures.test.ts` is unformatted at HEAD. `src/auth/claims.ts` is flagged only because its working copy has CRLF line endings, and its committed content is clean. Both are logged in `deferred-items.md`.
- The unrelated working-tree change in `sources/ReactApp/tests/hosting/arma-placement.test.tsx` was not touched.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- 62-08 can render `FleetLiveNotice` from `status` and `disconnectEpoch`, and wire "Reconnect now" to `reconnectNow`, which already refetches.
- 62-11 can remove the `useFleetQuery` `refetchInterval`, because the provider is mounted in both layouts and every recovery refetches.
- The docs paragraph in `react-ui-beta.md` (62-05) says the reconnect is specified but not yet implemented. A docs plan can now describe it as implemented.

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

## Self-Check: PASSED

- Created files exist: tests/app/update-stream.test.tsx, tests/hosting/fleet-live-updates.test.tsx.
- Commits 9ec1df24 and e1d1bc68 are ancestors of HEAD; `git rev-list --count 79078bb2..HEAD` = 2.
- Acceptance: setInterval 0, `export function nextDelayMs` 1, `export function createCoalescer` 1, FleetLiveUpdatesProvider
  3 in each layout, `exact: true` 3 in the provider, routes.json unchanged, CommunityLayout `diff -w` adds only import and wrapper.
- Plan verification: `npm test` 97 files / 790 tests passed; `typecheck` and `lint` clean; `format:check` clean for every
  file this plan touched (two out-of-scope files deferred).

---
phase: 63-workspace-shell-activity
plan: 03
subsystem: ui
tags: [react, websocket, tanstack-query, live-updates, freshness, intl]

requires:
  - phase: 62-fleet-quick-actions
    provides: FleetLiveUpdatesProvider, the single fleet socket, createCoalescer, hostingQueryKeys
provides:
  - additive subscribe(listener) on the fleet live updates context and provider
  - useWorkspaceLiveUpdates and invalidateWorkspaceQueries (exact server, jobs and operations invalidation)
  - deriveFreshness truth table and freshnessStaleAfterMs
  - active-job helpers and the 30 s fallback poll constant
  - formatRelativeTime and the useNow coarse clock hook
affects: [63-07, 63-09, 63-11]

requirements-completed: [ACT-02]

actuals:
  tokens: 20000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Additive listener registry on the one shared socket instead of a second WebSocket or per-server invalidation inside the fleet provider"
    - "exact: true on hostingQueryKeys.server(id) because it is a prefix of every per-server key"
    - "Pure freshness derivation with the clock injected; the anti-flicker delay stays in the badge"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/workspaceFreshness.ts
    - sources/ReactApp/src/features/hosting/jobProgress.ts
    - sources/ReactApp/src/app/useNow.ts
    - sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx
    - sources/ReactApp/tests/hosting/workspace-freshness.test.ts
    - sources/ReactApp/tests/hosting/job-progress.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/src/app/format.ts
    - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
    - sources/ReactApp/tests/hosting/fleet-page.test.tsx
    - sources/ReactApp/tests/i18n-format.test.ts

key-decisions:
  - "The workspace follows the Phase 62 socket through provider.subscribe; the provider's own fleet and notifications invalidation and its connected no-op default are unchanged"
  - "Recovery after a reconnect compares disconnectEpoch with a ref seeded at mount, so the first open never invalidates and each episode invalidates once"
  - "A connecting socket with disconnectEpoch above zero is reported stale (disconnected), not connecting"
  - "An active job with no data loaded yet (dataUpdatedAt 0) reports live, so the badge does not claim old data that never existed"

patterns-established:
  - "Listener errors are swallowed per listener so one failing subscriber cannot block others or the fleet invalidation"

coverage:
  - id: D1
    description: "A fleet frame for the open server invalidates exactly server, jobs and operations with exact filters; files, configuration, preservation, secrets and other servers stay valid"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "Bursts coalesce to a leading plus one trailing round, a reconnect recovers once, the first open does not, unmount stops invalidation and no second socket is opened"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Provider subscribe delivers the server id of valid frames only, unsubscribes cleanly and isolates a throwing listener; Phase 62 fleet behaviour is unchanged"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "deriveFreshness truth table including the 60000 versus 60001 ms boundary and connecting-before-first-open; active-job statuses, ordering and hasActiveJob; English and German relative time"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-freshness.test.ts"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/job-progress.test.ts"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/i18n-format.test.ts"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-11-19
status: complete
commits: 3
plan_head_before: 9a1a30636781cd5af71c274a6f75659f925b8557
plan_head_after: 8cab9660a0bf1bcc6f7ddd258be8326ddcc9dd4a
---

# Phase 63 Plan 03: Live Refresh Foundations Summary

**The open workspace now refetches exactly its own server, jobs and operations queries from the one shared fleet socket through an additive `subscribe` listener, backed by a pure freshness truth table, active-job helpers, relative time and a coarse clock.**

## Performance

- **Duration:** about 12 min (start time was not captured precisely; estimated from the first test run)
- **Tasks:** 2
- **Files:** 7 created, 6 modified

## Accomplishments

- `FleetLiveUpdatesProvider` keeps a ref-held listener `Set`; `subscribe` returns the unsubscribe function, and each valid fleet-changed frame calls every listener with the server id after the unchanged fleet coalescer trigger. A throwing listener is swallowed so other listeners and the fleet invalidation still run. The context default gained a no-op `subscribe`.
- `useWorkspaceLiveUpdates(serverId)` coalesces to 500 ms with `createCoalescer`, triggers only for frames carrying its own id, and invalidates once when the socket opens after a disconnection episode (the first open after mount does not). It opens no socket. `invalidateWorkspaceQueries` uses the exact filter on all three keys.
- `deriveFreshness`, `freshnessStaleAfterMs` (60 000), `isActiveJobStatus`, `activeJobs`, `hasActiveJob` (honours `pendingJobCount`), `workspaceFallbackPollMs` (30 000), `formatRelativeTime` and `useNow` are in place and tested at their boundaries. No translation key was added.
- Verification: `tests/hosting` plus `tests/i18n-format.test.ts` is 52 files and 660 tests green; `typecheck` and `lint --max-warnings 0` exit 0; the plan's commits touch no `src/locales` path.

## Task Commits

1. **Task 1: Tracer, one fleet frame invalidates exactly the open server's queries** - `4da7e2df` (feat)
2. **Task 2: Freshness truth table, active-job helpers, relative time and a coarse clock** - `017656b8` (feat)
3. **Formatting follow-up for a Task 1 test** - `8cab9660` (style)

**Plan metadata:** recorded in the docs commit that carries this summary.

## Decisions Made

See the key-decisions frontmatter. The tracer gate (`type="tracer"`) was satisfied by re-running the Task 1 verify end to end (workspace, fleet and Phase 62 tracer tests, typecheck, lint) before expansion; all passed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing fleet page test built a context value without the new member**
- **Found during:** Task 1 (typecheck)
- **Issue:** `tests/hosting/fleet-page.test.tsx` passes literal `FleetLiveUpdatesContext.Provider` values in two tests; the new required `subscribe` member broke `tsc -b`.
- **Fix:** Added `subscribe: () => () => undefined` to both literals. No behaviour change.
- **Files modified:** `sources/ReactApp/tests/hosting/fleet-page.test.tsx` (not in the plan's file list)
- **Verification:** `typecheck` clean, fleet page tests green
- **Commit:** `4da7e2df`

**2. [Style] Task 1 acceptance wording and formatting**
- The TSDoc of `useWorkspaceLiveUpdates.ts` originally contained the literal `exact: true` a fourth time, which broke the criterion `grep -c "exact: true"` equals 3; the wording was changed and the commit amended before moving on. A prettier line-wrapping fix to one Task 1 test was committed separately as `8cab9660`.

**Total deviations:** 1 auto-fixed (Rule 3) plus 1 style follow-up. **Impact:** none on behaviour or scope.

## Issues Encountered

- `npm run format:check` reports files such as `fleet-page.test.tsx`, `arma-placement.test.tsx` and `fleet-fixtures.test.ts`. These warnings come from line-ending differences in the CRLF working copy and are not caused by this plan. The working copy also shows `M` for a few files of this plan that differ from HEAD only by line endings; their content is committed. Left alone.
- One TSDoc line in `useWorkspaceLiveUpdates.ts` slightly exceeds 120 characters; neither lint nor prettier flags it.

## Known Stubs

None.

## Threat Flags

None. T-63-07 (frames are type-checked, id compared by string equality, only refetches) and T-63-08 (500 ms coalescer, exact filters, isolated throwing listener) are mitigated as planned.

## Next Phase Readiness

Ready for 63-04. Plans 63-07, 63-09 and 63-11 can consume `jobProgress.ts`, `deriveFreshness`, `useNow` and `useWorkspaceLiveUpdates`.

## Self-Check: PASSED

All created files exist on disk and the commits `4da7e2df`, `017656b8` and `8cab9660` are ancestors of HEAD.

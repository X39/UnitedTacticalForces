---
phase: 62-fleet-quick-actions
plan: 07
subsystem: webapp
tags: [react, pure-model, search, filters, url-state, deriveServerState, vitest, property-tests]

# Dependency graph
requires:
  - phase: 62-01
    provides: fleetServer and fleetAction fixtures
  - phase: 62-02
    provides: GameServer.costOwnerUserId in the regenerated client types
  - phase: 62-03
    provides: exported ownerFor and the Server_Fleet_QueuedChange key
provides:
  - Pure fleet model fleetModel.ts with no React import
  - URL state parseFleetSearchParams and writeFleetSearchParams (whitelist, 200 character cap, defaults omitted)
  - Locale-aware AND search searchServers and foldSearchText that leave out the cost owner placeholders
  - matchesFilter, countFilters, partitionFleet and orderFleetServers (moved here from FleetPages.tsx)
  - primaryActionFor and FleetPrimary, fleetStageKey, countCostlyServers, diffFleetStates and FleetStateChange
affects: [62-09, 62-10, 62-11, 62-12]

# Actuals (#2632)
actuals:
  tokens: 11000
  tasks: 2
  commits: 2
plan_head_before: 3a31ee803e684a63612e6e57c6c72fe91bdfe023
plan_head_after: c0fc5a69ab2a2b87303eb126223a9e8e28bce305

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Every fleet decision goes through deriveServerState. countFilters derives each server once and shares one membership function with matchesFilter, so counts and filtering cannot drift apart"
    - "Search haystack fields are joined with a line break so a term never matches across two fields"
    - "Exhaustive status x possibleCost x pendingJobCount loops, with the status list kept exhaustive at compile time by a Record<EDeploymentStatus, true>"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/fleetModel.ts
    - sources/ReactApp/tests/hosting/fleet-model.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/FleetPages.tsx

key-decisions:
  - "FleetStateChange uses the field names serverId, name and stateLabelKey that Plan 62-10 consumes. name is the server title from the newer snapshot"
  - "The query is trimmed and capped at 200 code units, then trailing whitespace and a dangling high surrogate are dropped, so a capped query is always valid text"
  - "The filter whitelist is case-sensitive. Values such as MINE fall back to all because the app only writes lowercase values"
  - "The UI-SPEC primary-action table is implemented literally: an attention or unconfirmed server shows a disabled stop when the API disables it (known limitation for the UAT)"

patterns-established:
  - "Fleet UI components consume fleetModel decisions and never branch on server state themselves"

requirements-completed: [FLT-01, FLT-02, FLT-03, FLT-04]

coverage:
  - id: D1
    description: "URL parameters are parsed with a filter whitelist and a 200 character query cap, and written back without default values while every other parameter keeps its value and position"
    requirement: FLT-03
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-model.test.ts#fleet URL parameters (4 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Search ignores case and diacritics, is locale-aware, requires every term (AND), and never indexes the Assigned owner or Unknown owner placeholders"
    requirement: FLT-02
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-model.test.ts#fleet search (5 tests)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The Mine, Running, Off and All filters, their counts, and the Active/Off partition come from deriveServerState alone. A server that may still cost never lands in Off"
    requirement: FLT-03
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-model.test.ts#fleet filters and #fleet sections (6 tests, exhaustive status loops)"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-state-single-owner.test.ts"
        status: pass
      - kind: unit
        ref: "tests/hosting/fleet.test.tsx (orderFleetServers still imported through the page re-export)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Primary action per derived state (start, stop, disabled stop, in progress, Open server only), never terminate. Also the stage line key, the fleet-wide cost count and the state diff"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-model.test.ts#fleet primary action, #fleet stage line, #fleet cost count, #fleet state diff (10 tests)"
        status: pass
    human_judgment: false
  - id: D5
    description: "A deterministic fleet of 150 servers is searched, filtered, partitioned and counted correctly in under one second"
    requirement: FLT-01
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-model.test.ts#fleet model at the expected fleet size"
        status: pass
    human_judgment: false

# Metrics
duration: 9min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 07: Pure Fleet Model Summary

**`fleetModel.ts` now makes every decision the fleet UI needs, using only `deriveServerState`, with no React. It covers URL state with a whitelist and a 200 character cap, and a search that ignores case and diacritics, requires every term, and leaves out the cost owner placeholders. It also provides the Mine, Running and Off filters with counts, the Active/Off partition with ordering, and the primary action from the UI-SPEC table (terminate is never offered). Finally it returns the stage line key, the fleet-wide cost count and the state diff used for announcements. 26 table-driven and exhaustive tests cover it, including the 150-server backstop.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-11-19T09:26:11Z
- **Completed:** 2026-11-19T09:35:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `fleetModel.ts` exports, each with TSDoc:
  - Types and constants: `FleetFilter`, `fleetFilters`, `FleetViewParams`, `fleetSearchMaxLength`, `FleetPrimary`, `FleetStateChange`.
  - URL state: `parseFleetSearchParams`, `writeFleetSearchParams`.
  - Search: `foldSearchText`, `searchServers`.
  - Filters and sections: `matchesFilter`, `countFilters`, `orderFleetServers`, `partitionFleet`.
  - Card and page decisions: `primaryActionFor`, `fleetStageKey`, `countCostlyServers`, `diffFleetStates`.
- The model never reads a deployment status. State, cost, section, stage and diff all come from `deriveServerState`. The only raw deployment field it reads is `deployment?.possibleCost`, in the ordering moved over unchanged.
- `FleetPages.tsx` now imports `orderFleetServers` from the model and re-exports it, so `tests/hosting/fleet.test.tsx` keeps working until Plan 62-11 rebuilds the page. The now unused `currentLocale` import was removed. Nothing else changed.
- 26 new tests. The full ReactApp suite is green (98 files, 816 tests), typecheck and lint are clean, and all three touched files pass `prettier --check`.

## Task Commits

1. **Task 1: URL params, search, filters, counts, partition and ordering** - `b0c8db5a` (feat)
2. **Task 2: Primary action decision, stage key, cost count and state diff** - `c0fc5a69` (feat)

**Plan metadata:** recorded in the following docs(62-07) commit

## Files Created/Modified
- `sources/ReactApp/src/features/hosting/fleetModel.ts` - the pure fleet model
- `sources/ReactApp/tests/hosting/fleet-model.test.ts` - parameter, search, filter, partition, primary action, stage, cost, diff and 150-server backstop tests
- `sources/ReactApp/src/features/hosting/FleetPages.tsx` - `orderFleetServers` is now a re-export from the model

## Decisions Made
- **`FleetStateChange` field names.** The fields are `{ serverId, name, stateLabelKey }`. The plan text says "id, title and the new state label key", and Plan 62-10's verified facts name the fields `serverId`, `name` and `stateLabelKey`. The consumer's names were used, and `name` carries the title from the newer snapshot.
- **Query capping.** The query is trimmed, cut to 200 code units, then cleaned up: a dangling high surrogate and trailing whitespace are dropped. The cap never produces broken text or a trailing space.
- **Filter whitelist is case-sensitive.** The app only writes lowercase values, and anything else falls back to `all`.
- **Primary action table implemented literally.** This follows the research choice recorded in the plan. Attention and unconfirmed cards whose stop the API disables get `{ kind: "action", variant: "danger", enabled: false }`, so the card will show a disabled stop with the API's reason next to Open server. This is the expected result of the API rules, not a bug, and should be surfaced in the UAT. The cheap alternative is to promote Open server and hide the disabled stop for those two states.
- **In-progress reason.** It is `pending` when `pendingJobCount > 0` or any server action is pending, and `state` otherwise. A `working` server always has a pending job, so it is always `pending`.

## Deviations from Plan

None in behavior. Two small details:
- In TDD terms, each task was run RED (tests failing on the missing exports) and then GREEN, but committed as one `feat(webapp)` commit per task. That is the commit message the plan prescribes for each task, so there are no separate `test(...)` commits.
- In `FleetPages.tsx`, the now unused `currentLocale` import had to go once the function moved. This was required by `tsc` and lint and does not change behavior.

**Total deviations:** 0 auto-fixed. **Impact:** none.

## Issues Encountered
- One docs line in the model contained the phrase "raw deployment status". It matched the `deployment.status` acceptance grep because `.` matches a space. The sentence was reworded before the Task 2 commit, and the grep prints 0. The single-owner guard test was never affected, since its regex requires a property access.

## Known Stubs

None. The model is complete and pure. It is not wired into the UI yet; Plans 62-09 to 62-11 consume it.

## Notes for Later Plans
- `docs/features/react-ui-beta.md`, paragraph "Fleet change signal" (written in 62-05), still says the client reconnect and backoff have to be built. 62-06 has built them. This plan does not own that file, so it was not edited; Plan 62-12 also edits that doc and should update the paragraph.
- `tests/hosting/fleet-fixtures.test.ts` is still not Prettier formatted (see `deferred-items.md`). It was not touched here.

## Next Phase Readiness
Ready for 62-08 to 62-11. The card (62-09) can call `primaryActionFor(server, derived)` and `fleetStageKey(derived)`. Announcements (62-10) can call `diffFleetStates`. The page (62-11) can call `parseFleetSearchParams`, `writeFleetSearchParams`, `searchServers`, `countFilters`, `matchesFilter`, `partitionFleet` and `countCostlyServers`.

## Self-Check: PASSED
- FOUND: sources/ReactApp/src/features/hosting/fleetModel.ts
- FOUND: sources/ReactApp/tests/hosting/fleet-model.test.ts
- FOUND: commit b0c8db5a (ancestor of HEAD)
- FOUND: commit c0fc5a69 (ancestor of HEAD)
- Acceptance: `export function partitionFleet` 1, `export function orderFleetServers` in FleetPages.tsx 0, `deriveServerState` in the model 9 (at least 3), `export function primaryActionFor` 1, `export function diffFleetStates` 1, `deployment.status|deployment?.status` 0
- Plan verification: `npm test` (98 files, 816 tests), `typecheck`, `lint` green; `git diff --exit-code -- sources/Common/Ui/routes.json` exits 0

---
phase: 61-foundations-localization-member-access
plan: 12
subsystem: webapp-fleet
status: complete
tags: [react, fleet, member-access, localization, disabled-with-reason]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-01 Server_Fleet_PendingJobs plural keys, 61-05 Server_Access_* and Server_Reason_CreatePermission keys, 61-07 ServerStateSummary and ActionUnavailableReason, 61-09 useFleetQuery and derived state"
provides:
  - "ServerCard header shows ServerStateSummary (state badge plus cost chip) from deriveServerState and the i18next plural queue count"
  - "Fleet card terminate control: ActionLink to /command/servers/{id}?action=terminate when usable, otherwise an aria-disabled ActionButton with a visible mapped reason (operate, pending or state)"
  - "HostingAccessBoundary and HostingError use the Server_Access_* JSON copy (title plus body, Try again ActionButton)"
  - "ServerFleetPage gated by useFleetQuery(!auth.isLoading && permissions.accessServers) inside HostingAccessBoundary; loading state while auth loads, never forbidden"
  - "Create control: ActionLink for creators, aria-disabled ActionButton with Server_Reason_CreatePermission for members"
  - "Access empty state (Server_Access_EmptyTitle/Body) for members; create-first copy kept for creators"
  - "Overview attention list renders ServerStateSummary derived from the joined server, not the raw statusLabel"
  - "member-access.test.tsx: React-side proof of ACC-01 for bare sv and sv:view"
affects: [61-13]

actuals:
  tokens: 9300
  tasks: 3
  commits: 3
plan_head_before: d3ccc4d74b76ae80d70669663e89c6d26b165949
plan_head_after: 7cbfc715488cdabb366acd2dcb0063150b8b22dd

tech-stack:
  added: []
  patterns:
    - "Fleet query enabled only for a finished auth state with a server claim; boundary wraps the content so a no-claim user never issues it"
    - "UI never recomputes action enabled: the API projection decides, the card only maps it to a link or a disabled-with-reason control"
    - "ids placed in card and overview URLs pass through encodeURIComponent"

key-files:
  created:
    - sources/ReactApp/tests/hosting/member-access.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/src/features/hosting/FleetPages.tsx
    - sources/ReactApp/tests/hosting/fleet.test.tsx
    - sources/ReactApp/tests/hosting/deep-link.test.tsx

key-decisions:
  - "HostingError always renders the localized Server_Access_LoadError as the panel title and shows the optional detail message as the body only when one is given, so the same sentence is never printed twice"
  - "The fleet page load error no longer exposes a raw error message; it uses the shared HostingError panel with Try again, and stale fleet data stays visible below it"
  - "Legacy keys Hosting_Servers_JobCount, Hosting_Servers_LoadError and Hosting_Servers_EmptyAccessDescription are left in the JSON (locales:check passes); removal is deferred to the Phase 69 cleanup"

requirements-completed: [FND-02, FND-04, ACC-01, ACC-03]

coverage:
  - id: D1
    description: "A fleet card shows the derived state badge and cost chip, uses the plural queue key, and the terminate control is a link to ?action=terminate when usable"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/hosting/fleet.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "An unavailable or pending terminate action renders an aria-disabled button with a visible, accessibly described mapped reason (no title attribute, no link)"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/hosting/fleet.test.tsx"
        status: pass
      - kind: test
        ref: "tests/hosting/member-access.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "A stopped deployment that may still cost never reads Off; a server without a deployment reads Off with no cost; icons are aria-hidden while state and cost are text"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/fleet.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "The fleet page is wrapped in HostingAccessBoundary; a no-claim user sees forbidden and the fleet query is never issued; auth loading shows loading, not forbidden"
    requirement: "ACC-01"
    verification:
      - kind: test
        ref: "tests/hosting/member-access.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Bare sv and sv:view members reach navigation and the fleet, see exactly the API list, get the Create control aria-disabled with a visible reason in English and German, and an empty list shows the access empty state"
    requirement: "ACC-01"
    verification:
      - kind: test
        ref: "tests/hosting/member-access.test.tsx"
        status: pass
    human_judgment: false
  - id: D6
    description: "The overview attention list joins notifications to servers by serverId and renders the derived state summary instead of the raw status label"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/hosting/fleet.test.tsx"
        status: pass
    human_judgment: false
  - id: D7
    description: "Visual layout of the state badge, cost chip and disabled-with-reason controls inside the existing card header and footer"
    requirement: "ACC-03"
    verification: []
    human_judgment: true
    rationale: "No test asserts rendered layout, wrapping or contrast; Plan 61-13 browser scans cover it. Fleet grid overflow is explicitly Phase 62."

duration: 17 min
completed: 2026-11-18
---

# Phase 61 Plan 12: Fleet Surfaces and Member Access Summary

**The fleet is now gated by server access and shows the shared derived state and cost on every card with a disabled-with-reason terminate control; bare `sv` and `sv:view` members get the read experience with an explained, disabled Create control and an access empty state, proven on the React side in a new member-access suite.**

## Performance

- **Duration:** about 17 min (2026-11-18T20:04:12Z to 20:20:59Z)
- **Tasks:** 3
- **Files:** 1 created, 4 modified

## Accomplishments

- Task 1: `ServerCard` computes `deriveServerState(server)` once and renders `ServerStateSummary` in the header, the queue fact through `t("Server_Fleet_PendingJobs", { count })`, the terminate control as `ActionLink variant="danger"` (encoded server and action ids) or `ActionButton variant="danger"` with `ActionUnavailableReason`, and the Open link as `ActionLink variant="ghost"`. `HostingAccessBoundary` renders the forbidden `StatePanel` with `Server_Access_ForbiddenTitle/Body`; `HostingError` uses `Server_Access_LoadError` and an `ActionButton` `Server_Access_Retry`.
- Task 2: `FleetPages.tsx` drops the local `useFleet` for `useFleetQuery`. The page header shows Refresh and Create only for users with server access; Create is an `ActionLink` for creators and an aria-disabled `ActionButton` with `Server_Reason_CreatePermission` otherwise. Auth loading shows the loading panel, everything else sits inside `HostingAccessBoundary`, so a no-claim user sees forbidden and no query runs. `ServerFleetView` shows the create-first empty state to creators and `Server_Access_EmptyTitle/Body` to members. The overview attention list builds a `Map` of servers and renders `ServerStateSummary` from `deriveServerState(server, { notification })`.
- Task 3: `member-access.test.tsx` (16 tests) covers navigation and permissions for both bare claims and a no-claim user, exactly-the-API-list rendering with one query, the enabled link for `sv` and the disabled-with-reason control for `sv:view`, the Create reason in English and German for both claims, the access empty state, forbidden with zero calls, loading without a query, and the creator link. A mutation check (query enabled without `accessServers`) turns the forbidden test red, confirming the suite bites.

## Task Commits

1. Task 1: fleet card state, cost, plural count and disabled terminate - `9fce10fd`
2. Task 2: gated fleet page, create reason, access empty state, derived attention list - `2c2bf4e0`
3. Task 3: member access proof for bare `sv` and `sv:view` - `7cbfc715`

## Verification

- `tests/hosting` (30 files, 384 tests) green after Task 1; `fleet.test.tsx` 14 tests and `member-access.test.tsx` 16 tests green
- `typecheck` exit 0; `lint` (`--max-warnings 0`) exit 0; `locales:check` 3108/3108 keys, 0 failures, 0 warnings
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` (CRLF, already in `deferred-items.md`)
- Full `npm test`: 90 files, 709 tests, all passed
- Acceptance: `ServerStateSummary` in components 2, `Server_Access_ForbiddenTitle` 1, `Server_Action_Terminate_Label` 2, `useFleetQuery` in FleetPages 3, `HostingAccessBoundary` in FleetPages 5, `Server_Reason_CreatePermission` 1, `toHaveBeenCalledTimes(0)` in member-access 2, API proof class named in a comment

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The deep-link forbidden assertion used the superseded boundary copy**
- **Found during:** Task 1 (running the hosting suite after moving the boundary copy to JSON)
- **Issue:** `deep-link.test.tsx` asserted "You do not have permission to access this hosting workspace.", the copy.ts text the plan replaces for the shared boundary.
- **Fix:** The assertion now expects the new forbidden title "You don't have access to game servers". Behavior asserted (forbidden panel only, no dialog, no query) is unchanged.
- **Files modified:** `sources/ReactApp/tests/hosting/deep-link.test.tsx`
- **Verification:** deep-link and workspace suites pass
- **Commit:** `9fce10fd`

**2. [Rule 2 - Missing critical] Added an overview attention test and German empty-state test**
- **Found during:** Task 2
- **Issue:** The plan listed no test for the derived attention list, and only "English and German" for the non-creator empty state; the attention change would have been unverified.
- **Fix:** Added a `HostingOverviewView` test (derived state shown, raw `statusLabel` absent) and German and creator empty-state tests in `fleet.test.tsx`.
- **Files modified:** `sources/ReactApp/tests/hosting/fleet.test.tsx`
- **Commit:** `2c2bf4e0`

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing coverage). **Impact:** test-only; no behavior beyond the plan.

## Issues Encountered

None. The plan's literal "title and message fallback" for `HostingError` would print the same sentence twice, so the body is rendered only when a detail message is supplied (see key-decisions).

## Known Stubs

None.

## Threat Flags

None. T-61-32 (no query for a no-claim user), T-61-33 (card and Create controls aria-disabled with API-derived reasons, enabled never recomputed) and T-61-34 (`encodeURIComponent` on server and action ids, fixed `/command/servers` prefix) are mitigated as planned and covered by tests.

## Next Phase Readiness

Ready for 61-13 (phase-level browser and parity checks). Fleet card grid overflow, search, filters and start or stop from the list remain Phase 62.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/hosting/member-access.test.tsx`
- Commits `9fce10fd`, `2c2bf4e0` and `7cbfc715` are ancestors of HEAD; `git rev-list --count` from `plan_head_before` is 3
- All task acceptance criteria and the plan verification pass (see above)

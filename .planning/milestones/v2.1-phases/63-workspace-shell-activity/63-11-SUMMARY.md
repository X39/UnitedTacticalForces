---
phase: 63-workspace-shell-activity
plan: 11
subsystem: ui
tags: [react, workspace-shell, live-updates, polling, announcements, vitest, accessibility]

requires:
  - phase: 63-workspace-shell-activity
    provides: workspaceSectionFromSearch, sectionTo and the section label keys (63-01), useWorkspaceLiveUpdates, hasActiveJob and workspaceFallbackPollMs (63-03), Breadcrumbs and SectionNavigation (63-05), WorkspaceHeader, CostStrip and ServerEndpoint (63-06), JobProgressStrip, JobFailurePanel and jobTransitionAnnouncement (63-07), activityOperationsTake (63-08), ActivitySection (63-09), OverviewSection, SettingsSection and MachineSection (63-10)
provides:
  - WorkspaceShell, the shell composition (breadcrumbs, sticky header, details block with Refresh server, notices, progress strip, failure panel, section navigation, routed section, polite announcement region)
  - ServerWorkspacePage rewritten as the route component with the fleet-signal listener, the active-job fallback poll, the exact-key Refresh server, the deep link and the one retry and skip mutation
  - useJobAnnouncement, one-time announcements of job transitions observed after mount
  - Shell styles (rhythm, flat details block) and the migrated workspace tests
affects: [63-12, 64, 65, 66, 67, 68, 69]

requirements-completed: [WSP-01, WSP-02, WSP-04, WSP-05, WSP-06, ACT-02]

plan_head_before: 13b39b3e8736bf42d71cc94d3581bff9c9210369
plan_head_after: 4f0a7795a7589c539785b035ab62bc13fb5aa881

actuals:
  tokens: 19700
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "The fallback poll is a refetchInterval function that reads the server and jobs from the query cache at each evaluation, so it never depends on a value declared after the queries"
    - "State derived from a changing data timestamp is adjusted during render (useJobAnnouncement), not in an effect"
    - "Refresh and mutation side effects handle their own failure (setRefreshFailed) so a failed refetch never turns a successful retry into a failed mutation"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/WorkspaceShell.tsx
    - sources/ReactApp/src/features/hosting/useJobAnnouncement.ts
    - sources/ReactApp/tests/hosting/workspace-shell.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/components/server/workspace-shell.css
    - sources/ReactApp/tests/hosting/workspace-header.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/deep-link.test.tsx
    - docs/features/react-ui-beta.md

key-decisions:
  - "The fallback poll reads hasActiveJob from the query cache inside the refetchInterval function instead of a render-time const, because the three queries are declared before the jobs data exists"
  - "The paused notice is the shared StatePanel kind info, which already is a polite role=status region; no second role=status wrapper was added, to avoid nested live regions"
  - "Refresh server handles its own failure and reports it through refreshFailed, so the retry and skip mutation (which refreshes on success) is never failed by a rejected refetch"
  - "The announcement hook starts at updatedAt 0 so an already cached first snapshot is stored silently and the next refetch has a base to compare"
  - "The not-found and load-error states use the Phase 63 panel copy; the raw Error.message is never rendered"

patterns-established:
  - "Page tests render ServerWorkspacePage on /command/servers/:serverId with hostingService spies and reach job, log and audit detail through the Activity timeline"

coverage:
  - id: D1
    description: "Shell order and routing: breadcrumbs, sticky header with the only h1, details block (five cost rows, endpoint with Copy address, Refresh server), section navigation and five routed sections; the shell stays in every section; retired tab values open Overview with the URL untouched"
    requirement: WSP-01
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-header.test.tsx#keeps the shell in every section and swaps only the section content"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#opens Overview for a retired tab value and leaves the URL unchanged"
        status: pass
    human_judgment: false
  - id: D2
    description: "Breadcrumbs Servers, server name, current section with aria-current (WSP-06)"
    requirement: WSP-06
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-header.test.tsx#renders breadcrumbs from the servers list to the current section"
        status: pass
    human_judgment: false
  - id: D3
    description: "Loading, load error without raw error text and Try again, and Server not found with Back to servers"
    requirement: WSP-01
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-header.test.tsx#shows the error panel with Try again and never the raw error text"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-header.test.tsx#shows Server not found with a way back for a missing server"
        status: pass
    human_judgment: false
  - id: D4
    description: "Live refresh: fleet signal refetches server, jobs and operations but not the files, and the fallback poll is 30000 only while a job is active and false otherwise (D-07)"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#fallback poll"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#fleet signal"
        status: pass
    human_judgment: false
  - id: D5
    description: "Refresh server invalidates exactly fleet, server, jobs, operations and notifications with exact filters, is disabled while a workspace query fetches and shows the request error when a refetch rejects"
    requirement: WSP-01
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#Refresh server"
        status: pass
    human_judgment: false
  - id: D6
    description: "Paused-by-operator and archived notices in every section; progress strip above the navigation while a job is active; failure panel with retry by job id and request error (WSP-04, WSP-05)"
    requirement: WSP-04
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#notices"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#progress strip and failure panel"
        status: pass
    human_judgment: false
  - id: D7
    description: "A job that finishes or fails while the page is open is announced once through the visually hidden polite region; nothing at load"
    requirement: WSP-05
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-shell.test.tsx#job announcements"
        status: pass
    human_judgment: false
  - id: D8
    description: "Deep link keeps its Phase 61 behaviour with the activity section carried, one dialog beside the header Start button; retry and skip through the Activity timeline"
    requirement: WSP-02
    verification:
      - kind: unit
        ref: "tests/hosting/deep-link.test.tsx#opens exactly one dialog although the sticky header offers the same Start button"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#requests the shared confirmation for Start and exposes jobs, audit, and diagnostic protocols"
        status: pass
    human_judgment: false
  - id: D9
    description: "Visual rhythm of the shell (breadcrumb, header, details block spacing, sticky header at 320 and 390 px, details block divider) at real viewport widths"
    verification: []
    human_judgment: true
    rationale: "Spacing and stickiness are asserted only by the token style guard; the Playwright viewport checks belong to plan 63-12"

duration: 18min
completed: 2026-11-19
status: complete
---

# Phase 63 Plan 11: Workspace Shell and Live Refresh Summary

**WorkspaceShell composes breadcrumbs, the sticky header, a cost and connection details block, notices, the progress strip, the failure panel and five routed sections, refreshed by the fleet signal with a 30 s poll only while a job is active, with one-time job announcements**

## Performance

- **Duration:** about 18 min
- **Started:** 2026-11-19T21:26:00Z
- **Completed:** 2026-11-19T21:44:00Z
- **Tasks:** 3
- **Files modified:** 9 (3 created, 6 modified)

## Accomplishments

- Opening a server now renders, top to bottom, breadcrumbs, the sticky header, the details block (cost strip, endpoint, Refresh server), the notices, the progress strip, the failure panel, the section navigation and the section content. The shell renders in every section; only the content below the navigation changes.
- `?tab=` selects Overview, Activity, Settings, Files or Machine; retired and unknown values render Overview and leave the URL untouched.
- The three 5-second pollers are gone. The workspace listens to its own server's fleet signal and to reconnects, and polls server, jobs and operations every 30 seconds only while a job is active.
- Refresh server invalidates exactly the fleet, server, jobs, operations and notifications queries with exact filters and shows the shared request error when a refetch fails. No raw `Error.message` is rendered anywhere in the page.
- Job outcomes observed after load are announced once through a visually hidden polite region; a failed job that is already there at load stays silent.
- The old job list, cloud panel, `ServerWorkspaceView`, page header usage, delete flow and legacy copy hook usage were removed from the page; the delete flow already lives in `SettingsSection`.
- `docs/features/react-ui-beta.md` describes the workspace live refresh and its active-job fallback poll.

## Task Commits

1. **Task 1: Shell tracer** - `1bc293a0` (feat)
2. **Task 2: Live refresh, notices, progress, failure, announcements** - `579e5743` (feat)
3. **Task 3: Deep-link test migration and suite sweep** - `4f0a7795` (test)

**Plan metadata:** committed with this SUMMARY (docs: complete plan)

The tracer's `<verify>` was automated only (page tests, typecheck, lint); it passed end to end before Task 2 expanded: tracer verified end to end, expanding.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/WorkspaceShell.tsx` - shell composition and closed section router
- `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx` - route component: queries, live updates, fallback poll, deep link, refresh, retry and skip
- `sources/ReactApp/src/features/hosting/useJobAnnouncement.ts` - job transition announcements adjusted during render
- `sources/ReactApp/src/components/server/workspace-shell.css` - shell rhythm and flat details block
- `sources/ReactApp/tests/hosting/workspace-shell.test.tsx` - 12 shell behaviour tests
- `sources/ReactApp/tests/hosting/workspace-header.test.tsx`, `workspace-page.test.tsx`, `deep-link.test.tsx` - migrated tests
- `docs/features/react-ui-beta.md` - workspace live refresh documented

## Decisions Made

See `key-decisions` above. No new dependencies and no new locale keys; every string comes from the Phase 63 keys of plan 63-01.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The fallback poll reads the query cache instead of a render-time `active` const**
- **Found during:** Task 2
- **Issue:** The plan computes `active` from `server.data` and `jobs.data` and uses it inside `refetchInterval`, but the three queries are declared before those values exist; referencing the const from the option closure risks a temporal dead zone at first evaluation.
- **Fix:** `refetchInterval` is a function that calls `hasActiveJob` on `client.getQueryData` of the server and jobs keys. Same semantics: 30000 while `pendingJobCount` is above 0 or an active job is loaded, `false` otherwise; the test reads the interval from the observer options and asserts both values.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx`
- **Commit:** `579e5743`

**2. [Judgement - accessibility] No extra `role="status"` wrapper around the paused notice**
- **Found during:** Task 2
- **Issue:** The plan asks for a `role="status"` wrapper around `StatePanel kind="info"`, but `StatePanel` already renders as a polite `role="status"` region; a wrapper would nest live regions and risk double announcements.
- **Fix:** The paused notice is the `StatePanel` itself (`role="status"`); the test asserts the notice sits in a `role="status"` element in three sections.
- **Commit:** `579e5743`

**3. [Plan criterion] `grep -c activityOperationsTake` prints 2 instead of 1**
- **Found during:** Task 1
- **Issue:** The identifier appears once in the import and once in the request, so the line count is 2; the criterion's intent (operations are requested with take 200) is met.
- **Commit:** `1bc293a0`

**4. [Test fixture] The migrated audit case uses `WorkerDegraded` instead of `LifecycleFailed`**
- **Found during:** Task 1
- **Issue:** The old fixture kind is an echo of a job event and would not show as its own expandable audit entry in the merged timeline.
- **Fix:** The operation keeps the same summary and trace id ("Preparation failed", `trace-1`) but uses a kind that renders as an audit entry; the technical evidence disclosure is opened before asserting the trace id.
- **Files modified:** `sources/ReactApp/tests/hosting/workspace-page.test.tsx`
- **Commit:** `1bc293a0`

**Total deviations:** 4 (1 blocking-resolution, 1 accessibility judgement, 1 plan-criterion count, 1 test fixture). **Impact:** none on behaviour or scope.

`server-state-surfaces.test.tsx` and `member-access.test.tsx` needed no change: the cross-surface test already locates the workspace surface through the `h1` inside its `header`, and the member-access test asserts no removed workspace label.

## Issues Encountered

- `npm --prefix sources/ReactApp test` is not fully green: `tests/hosting/activity-section.test.tsx` "shows 50 entries first and Show more activity adds 50 and focuses the first new one" times out at 5 s in the full-suite run and passes in isolation (36/36). It is logged as deferred in 63-10, lives outside this plan's file list, and was left untouched as the plan requires. Every other Vitest file passes (125 of 126 files, 1297 of 1298 tests).
- `format:check` lists `tests/hosting/arma-placement.test.tsx`, `tests/hosting/fleet-fixtures.test.ts` and `tests/hosting/fleet-page.test.tsx`. None was touched by this plan; the first two were already deferred in 63-01. `typecheck`, `lint` and `locales:check` exit 0.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. The `SlotPlaceholder` areas (accrued cost, metrics, automation) belong to earlier plans and are intentional.

## Threat Flags

None. T-63-28 (no raw error text), T-63-29 (30 s poll only while a job is active, on three exact keys), T-63-30 (the deep-link matching and handled-key guard kept verbatim) and T-63-31 (`workspaceSectionFromSearch` whitelist) are implemented as planned.

## Next Phase Readiness

- Plan 63-12 can run the Playwright viewport and axe checks against the composed shell (sticky header height at 320 by 568, section links in the viewport, announcement region).
- The old `.next-hosting-job-list` and related legacy styles may now be unused; check in 63-12 before deleting.

## Self-Check: PASSED

- Created files exist: WorkspaceShell.tsx, useJobAnnouncement.ts and workspace-shell.test.tsx.
- Commits `1bc293a0`, `579e5743` and `4f0a7795` are ancestors of HEAD and carry good signatures.
- Acceptance greps pass: `<WorkspaceHeader` 1, `function JobList` 0, `<progress` 0, `useHostingCopy` 0, `PageHeader` 0, `ServerWorkspaceView` in the page test 0, `refetchInterval: 5_000` 0, `workspaceFallbackPollMs` at least 1, `useWorkspaceLiveUpdates(` 1, `next-hosting-inline-error` 0, `pollers until Phase 63` 0, `<JobFailurePanel` 1, `tab=jobs` in deep-link 0 and `tab=activity` 6, `routes.json` unchanged.
- Plan verification: typecheck, lint and locales:check exit 0; the style guard and the single-owner guard pass; the only red items are the two deferred ones above.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

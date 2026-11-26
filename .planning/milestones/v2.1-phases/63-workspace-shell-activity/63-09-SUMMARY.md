---
phase: 63-workspace-shell-activity
plan: 09
subsystem: ui
tags: [react, activity-timeline, live-badge, deep-link, vitest, accessibility]

requires:
  - phase: 63-workspace-shell-activity
    provides: buildActivityEntries, filterEntries, filterCounts, auditWindowBoundary, groupEntriesByDay and ActivityEntry (63-08), deriveFreshness and useNow (63-03), the output max-height token and fixtures (63-04), CopyButton (63-06), the Server_Activity_* and Server_Live_* keys (63-01)
provides:
  - ActivitySection, the merged day-grouped timeline with toolbar, honest loading, error, empty and no-match states, paging and the activity deep link
  - ActivityTimeline, the day-grouped list with the audit window note and aria-busy
  - ActivityEntry expansion, so every capability of the old Jobs, Logs and Audit tabs is reachable in place
  - LiveBadge, the freshness badge with the 3 s anti-flicker and its recovery actions
  - HostingAuditEvidence, the technical evidence block exported from the audit feed
affects: [63-10, 63-11, 63-12, 69]

requirements-completed: [ACT-01, ACT-02]

plan_head_before: 273c586501f55873bcbc2de38e6cf50ed387ff0a
plan_head_after: 31b88fec948850688cb2e743a8f7238d44fcc773

actuals:
  tokens: 21800
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A deep link is applied by adjusting state during render when the location key changes (guarded, with the fragment matched through Array.find), and only scroll and focus run in an effect keyed by a focus request object"
    - "The anti-flicker timer for the disconnected badge stores the disconnect epoch in a timer callback, so a new episode waits again and a recovered socket never shows the stale state"
    - "Entry expansion is controlled by the section (a Set of ids) and additive on ActivityEntry; the compact variant ignores every expansion prop"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx
    - sources/ReactApp/src/components/server/ActivityTimeline.tsx
    - sources/ReactApp/src/components/server/LiveBadge.tsx
    - sources/ReactApp/tests/hosting/activity-section.test.tsx
    - sources/ReactApp/tests/hosting/live-badge.test.tsx
  modified:
    - sources/ReactApp/src/components/server/ActivityEntry.tsx
    - sources/ReactApp/src/features/hosting/HostingAuditFeed.tsx
    - sources/ReactApp/src/components/server/activity.css

key-decisions:
  - "The disclosure button renders only for the anchored variant when onToggle is given, so an entry without a handler never shows a dead button"
  - "The deep link is re-applied on every location key change that carries an activity fragment, so clicking Open activity details again after changing filters resets and refocuses again; a navigation without an activity fragment only clears the missing-entry line"
  - "A fragment that does not start with #activity- is ignored; one that does and matches no entry shows the missing-entry line and moves no focus"
  - "The toolbar stays visible while loading and after a failure (chip counts are omitted then), so Collect logs and the live badge stay usable"
  - "The audit window note is computed on the listed entries, so it also appears under the Jobs filter before the first job older than the oldest loaded audit record"
  - "Expanded output uses white-space: pre (scroll in both axes) instead of the old pre-wrap, as the UI-SPEC requires"

patterns-established:
  - "Job and command status, stage and failure tokens in expanded details are raw API tokens inside ApiText (lang en); ids are in code"
  - "Unavailable retry and skip reuse Server_Failure_Sending as the visible reason while a request is in flight"

coverage:
  - id: D1
    description: "The Activity section shows one day-grouped (Today, Yesterday, dated with the year only when not this year), newest-first timeline with loading, error with Try again, empty and refetch (aria-busy, no flash) states and the audit window note before the first entry older than the oldest loaded operation"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-section.test.tsx#ActivitySection timeline"
        status: pass
    human_judgment: false
  - id: D2
    description: "Toolbar: type filter chips with counts that equal the listed entries, Problems only, no-match and no-problems panels with Show all activity, Collect logs through the shared confirmation, Download all logs as a link or a disabled button with a reason"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-section.test.tsx#ActivitySection toolbar"
        status: pass
    human_judgment: false
  - id: D3
    description: "Expanding an entry keeps every capability of the old tabs: job facts, every command with output, Download log, Try again and Skip and continue (unavailable with a reason while busy), log output in a focusable pre with Copy output and Download log, audit summary and technical evidence; the compact variant has no disclosure"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "tests/hosting/activity-section.test.tsx#ActivitySection expanded entries"
        status: pass
      - kind: unit
        ref: "tests/hosting/audit-feed.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "LiveBadge shows Connecting, Live or Not up to date from deriveFreshness, shows the disconnected stale state only after 3 s of continuous disconnection per epoch, names the reason with Reconnect now or Refresh activity, and keeps the updated time outside the polite status region"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "tests/hosting/live-badge.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Paging shows 50 entries and Show more activity adds 50 and focuses the first new entry; the activity deep link resets the filters, pages in, expands, scrolls without motion and focuses the entry, shows the missing-entry line for an unknown anchor and treats the fragment only through Array.find"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-section.test.tsx#ActivitySection toolbar"
        status: pass
    human_judgment: false
  - id: D6
    description: "A 5,000-line output scrolls inside its own box within --utf-output-max-height at 320 px, never widens the page and renders in under 200 ms in the browser"
    requirement: ACT-02
    verification:
      - kind: unit
        ref: "tests/hosting/activity-section.test.tsx#ActivitySection very long output"
        status: pass
    human_judgment: true
    rationale: "Declared as a backstop in the plan. jsdom proves the rule text and a 200 ms expand budget, but real overflow at 320 px and render time are asserted in the plan 63-12 browser walk."
  - id: D7
    description: "Responsive toolbar layout (above 800 px, 521 to 800 px, 520 px and below), badge pill look and the reduced-motion spin at 320 to 1280 px; the style guard only proves token use"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "Visual layout and touch-target sizes are asserted in the plan 63-12 Playwright walk; this plan supplies the rules and the token guard."

duration: 20min
completed: 2026-11-19
status: complete
commits: 3
---

# Phase 63 Plan 09: Activity Section and Live Badge Summary

**One filterable, day-grouped activity timeline whose entries expand in place to every old Jobs, Logs and Audit control (raw output with copy and download, retry and skip, technical evidence), with a live badge that applies a 3 s anti-flicker per disconnect epoch, 50-entry paging, and an activity deep link that pages in, expands and focuses the target entry.**

## Performance

- **Duration:** about 20 min
- **Started:** 2026-11-19T16:34:25Z
- **Completed:** 2026-11-19T16:55Z
- **Tasks:** 3
- **Files modified:** 8 (5 created, 3 modified)

## Accomplishments

- `ActivityTimeline` groups entries by local day (Today, Yesterday, weekday and date with the year only for another year), sets `aria-busy` while refetching, and inserts the window note directly before the first listed entry older than the oldest loaded audit record.
- `ActivitySection` renders loading, error (Try again), empty, no-match and no-problems states, the toolbar (type chips with counts from `filterCounts`, Problems only, live badge, Collect logs through the shared confirmation, Download all logs), 50-entry paging with focus on the first new entry, and the deep link.
- `ActivityEntry` gained an additive disclosure. Expanded job entries show facts, every command with status and output, the protocol download and Try again or Skip and continue; log entries show facts, the output in a focusable `pre` named by the entry sentence, Copy output and Download log; audit entries show the API summary and the exported `HostingAuditEvidence`.
- `LiveBadge` derives Connecting, Live or Not up to date through `deriveFreshness`, keeps only the state word in the polite region, shows the updated time outside it, and offers Reconnect now or Refresh activity with the matching reason.

## Task Commits

1. **Task 1: Tracer - the Activity section lists merged entries by day with loading, error and empty states** - `a802c4bc` (feat)
2. **Task 2: Expandable entries keep raw output, copy and download, retry and skip, and audit evidence** - `9432ce44` (feat)
3. **Task 3: Toolbar with filters and counts, the live badge, paging and the activity deep link** - `31b88fec` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx` - the section: toolbar, states, paging, deep link
- `sources/ReactApp/src/components/server/ActivityTimeline.tsx` - day-grouped list with window note and aria-busy
- `sources/ReactApp/src/components/server/LiveBadge.tsx` - freshness badge with the anti-flicker timer
- `sources/ReactApp/src/components/server/ActivityEntry.tsx` - disclosure and expanded details (compact contract unchanged)
- `sources/ReactApp/src/features/hosting/HostingAuditFeed.tsx` - exported `HostingAuditEvidence`, feed output unchanged
- `sources/ReactApp/src/components/server/activity.css` - timeline, details, output box, toolbar and badge styles (tokens only)
- `sources/ReactApp/tests/hosting/activity-section.test.tsx` - 36 tests: timeline, expansion, toolbar, paging, deep link, long output
- `sources/ReactApp/tests/hosting/live-badge.test.tsx` - 12 tests over every badge state and the 3 s rule

## Decisions Made

See `key-decisions` above. The notable ones: the deep link re-applies on every location key change that carries an activity fragment; the disclosure needs an `onToggle`; the toolbar stays visible while loading or failed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Window-note test broke once paging arrived**
- **Found during:** Task 3 verification
- **Issue:** The Task 1 window-note test renders 200 operations plus two older jobs. With 50-entry paging the older job is beyond the first page, so the note was not rendered.
- **Fix:** The two window-note tests select the Jobs filter first, so the entries that matter are listed. No production change.
- **Files modified:** `sources/ReactApp/tests/hosting/activity-section.test.tsx`
- **Verification:** both tests pass; the full `tests/hosting` and `tests/styles` suites are green.
- **Committed in:** `31b88fec` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking test adjustment)
**Impact on plan:** None; the production behaviour is exactly the planned behaviour.

## TDD Notes

Tests and implementation were written in the same pass for each task and first run green, so a separate red run was not observed. Each task has one `feat` commit, as the plan specifies.

## Issues Encountered

- `npm run format:check` still reports the two files already recorded in `deferred-items.md` by plan 63-01; neither is touched here.
- The three ReactApp files with CRLF-only modifications (`FleetLiveUpdatesProvider.tsx`, `useWorkspaceLiveUpdates.ts`, `workspace-live-updates.test.tsx`) were left unstaged as instructed.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. `ActivitySection` and `LiveBadge` are wired to props and the shared socket context; plan 63-11 mounts the section and feeds it the queries.

## Threat Flags

None. The fragment is compared with entry ids through `Array.find` and never used as a key, selector or HTML (T-63-20); diagnostic output, summaries and evidence render only as React text in `pre`, `ApiText` and `code`, and downloads use the existing `textDownloadUrl` (T-63-21); retry and skip render only for `canRetry` and `canSkip` (T-63-23).

## Next Phase Readiness

- Plan 63-10 reuses `ActivityEntry compact` on the Overview; the compact contract is unchanged.
- Plan 63-11 mounts `ActivitySection` with `server`, `jobs`, `operations`, `loading`, `failed`, `fetching`, `dataUpdatedAt`, `hasActiveJob`, `jobBusy`, `jobRequestFailed`, `onReload`, `onRetryJob` and `onSkipJob`, inside the router and the cost confirmation provider.
- Plan 63-12 should confirm the 5,000-line output at 320 px, the toolbar layouts and the stale badge in the Playwright walk.

## Self-Check: PASSED

All eight files exist and the three task commits are ancestors of HEAD.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

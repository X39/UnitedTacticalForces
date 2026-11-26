---
phase: 69-consistency-accessibility-verification
plan: 35
subsystem: ui
tags: [react, tanstack-query, vitest, api-client, empty-state]

requires:
  - phase: 69-consistency-accessibility-verification
    provides: "69-34 gap closure; vereinService none-mapping pattern for statutes, minute and myMembershipRequest"
provides:
  - "eventService.detail, fieldReportService.detail and publicPageService.published resolve null (never undefined) for an expected 204 or 404"
  - "Service-level tests locking null for 204/404 and rejection for 403/500 across all six expected-none public reads"
  - "Page-level tests rendering EventDetailPage, FieldReportDetailPage and GdprPage through the real services and a stubbed fetch"
affects: [69 verification re-run, CON-02 empty-state reachability]

plan_head_before: b5a7800819842c3cc0488e9b06df7435121a0236
plan_head_after: bbc1f1c2744ebdc9b00141aa1aad2b58efa50c95

actuals:
  tokens: 9000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Reads that declare expected none statuses map the answer to null with `?? null` so TanStack Query sees data, not a failed load"
    - "Not-found state tests use the real services and a pathname-routed fetch stub, asserting an empty unexpected-request list"

key-files:
  created:
    - sources/ReactApp/tests/public-services.test.ts
    - sources/ReactApp/tests/public-not-found-states.test.tsx
  modified:
    - sources/ReactApp/src/api/services.ts
    - sources/ReactApp/tests/events-member/event-services.test.ts

key-decisions:
  - "Mapping stays per read (as vereinService does); ApiClient.request and expectedStatuses are unchanged, so 401, 403 and 5xx keep rejecting"
  - "The shared explanatory comment moved above eventService (first mapped read) with a one-line pointer above the Verein reads"

patterns-established:
  - "Page tests for empty states must not replace the services: a mocked service cannot expose a result-mapping defect"

requirements-completed: [CON-02]

coverage:
  - id: D1
    description: "An unknown or deleted event shows the designed not-found state with a back link, not the load-error panel"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/public-not-found-states.test.tsx#shows the operation not-found state for an unknown event"
        status: pass
    human_judgment: false
  - id: D2
    description: "An unknown field report slug shows the designed not-found state with a back link"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/public-not-found-states.test.tsx#shows the field report not-found state for an unknown slug"
        status: pass
    human_judgment: false
  - id: D3
    description: "An unpublished GDPR page (404 or 204) shows the unavailable empty state"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/public-not-found-states.test.tsx#shows the unavailable GDPR state when the API answers %i"
        status: pass
    human_judgment: false
  - id: D4
    description: "All six expected-none public reads resolve null for 204/404 and the three detail reads reject for 403 and 500"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/public-services.test.ts"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 35: Expected-none public reads resolve null Summary

**Event, field report and public page detail reads now map an expected 204 or 404 to null, so the pages reach their designed not-found or unavailable state instead of the load-error panel, proven by tests that use the real services.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 2
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments

- Closed VERIFICATION gap 1 (CR-01): `eventService.detail`, `fieldReportService.detail` and `publicPageService.published` return `(await request(...)) ?? null` with path, signal and `expectedStatuses: [204, 404]` unchanged.
- `tests/public-services.test.ts` proves null for 404 and 204, the parsed body for 200, a forbidden `ApiError` for 403 and a server `ApiError` for 500 on each of the three reads, plus a regression lock that `vereinService.statutes`, `minute` and `myMembershipRequest` resolve null for 404.
- `tests/public-not-found-states.test.tsx` renders the real `EventDetailPage`, `FieldReportDetailPage` and `GdprPage` against a stubbed `fetch` (no service mocked, no spy). Each test asserts the body text, the back link where designed, no `role="alert"`, no "Try again" button, exactly one h1 and an empty unexpected-request list.
- RED was observed first for every new case (the service resolved undefined and the page rendered the alert panel), then GREEN after the fix.

## Task Commits

1. **Task 1: Tracer - unknown event shows the operation not-found state** - `fd95b12f` (fix)
2. **Task 2: Unknown field report and unpublished GDPR page reach their empty states** - `bbc1f1c2` (fix)

**Plan metadata:** committed with this SUMMARY (docs commit).

## Files Created/Modified

- `sources/ReactApp/src/api/services.ts` - three reads mapped to null; shared comment moved above `eventService`
- `sources/ReactApp/tests/public-services.test.ts` - service-level none/forbidden/failure contract
- `sources/ReactApp/tests/public-not-found-states.test.tsx` - page-level not-found proof through the real services
- `sources/ReactApp/tests/events-member/event-services.test.ts` - the 204 detail now expects `toBeNull()`

## Decisions Made

None beyond the plan: the mapping stays per read, `ApiClient.request` is untouched, and `expectedStatuses` was not widened.

## Deviations from Plan

None - plan executed exactly as written.

Task 1's commit contained only the event cases of `public-services.test.ts`; the field report, public page and Verein cases were added with Task 2, as the plan's task split implies.

## Issues Encountered

- The fresh worktree had no `node_modules`. A directory junction to the main checkout's `sources/ReactApp/node_modules` was created so Vitest, tsc and eslint could run; no package was installed. The junction is untracked and was not committed.
- The `git rev-parse --git-dir` ledger step of the commit protocol was run as separate plain commands because the harness refuses compound git commands in an isolated worktree; the ledger file is `gsd-plan-head-before-69-35` in the worktree git dir.

## Verification

- `npm --prefix sources/ReactApp test -- tests/public-services.test.ts tests/public-not-found-states.test.tsx tests/public-form-pages.test.tsx tests/public-hero-pages.test.tsx tests/events-member`: 13 files, 125 tests passed.
- `npm --prefix sources/ReactApp run typecheck` and `npm --prefix sources/ReactApp run lint`: clean. Prettier check clean on the changed files.
- Acceptance greps: `?? null` count in `services.ts` is 6, `toBeUndefined` in `event-services.test.ts` is 0, `spyOn(` and `@/api/services` in the page test are 0.
- The full Vitest suite was not run (load timeouts of the WR-07 class are known on this machine); no unrelated file failed in the targeted runs.

## Known Stubs

None.

## Threat Flags

None. T-69-42 is mitigated: only the declared 204 and 404 become null, and `public-services.test.ts` asserts that 403 and 500 still reject.

## Cross-plan findings

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

VERIFICATION truth 6 (not-found and unavailable states reachable) is now provable by `tests/public-not-found-states.test.tsx`. No blockers for the remaining gap plans.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/public-services.test.ts
- FOUND: sources/ReactApp/tests/public-not-found-states.test.tsx
- FOUND: commit fd95b12f
- FOUND: commit bbc1f1c2

---
*Phase: 69-consistency-accessibility-verification*
*Completed: 2026-11-25*

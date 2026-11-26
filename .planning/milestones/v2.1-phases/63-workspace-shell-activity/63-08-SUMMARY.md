---
phase: 63-workspace-shell-activity
plan: 08
subsystem: ui
tags: [react, activity-timeline, audit, vitest, i18n, accessibility]

requires:
  - phase: 63-workspace-shell-activity
    provides: actorFor, jobKindLabelKey and activityEntryAnchor (63-01), requestedByName and actorName (63-02), isActiveJobStatus (63-03), workspaceJob, workspaceCommand and workspaceOperation fixtures (63-04), all Server_Activity_* keys (63-01)
provides:
  - buildActivityEntries, one pure merge of jobs, worker command logs and audit operations into plain-language entries with actor and time
  - filterEntries and filterCounts, pure filters whose chip counts equal their lists, with echo deduplication and Problems only
  - auditWindowBoundary and groupEntriesByDay for the audit window marker and the local-day groups
  - ActivityEntry, one timeline entry in an anchored and a compact variant
  - activity.css, token-only entry styles
affects: [63-09, 63-10, 63-11, 63-12, 69]

requirements-completed: [ACT-01]

plan_head_before: e42242420b3abae9eff90682b3a0c21c6a306954
plan_head_after: a3e3b099e38c2d5a9d68a69977b3814537ace4cb

actuals:
  tokens: 12800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Closed Map and switch lookups over untrusted status and kind strings with fixed fallbacks; unknown audit kinds render the Other sentence plus the API summary"
    - "A Record satisfies check over the generated command status union in the test, so a new API value breaks the typecheck"
    - "Filter counts are computed through filterEntries, so a chip count can never differ from its list"
    - "Entry ordering is newest first by parsed time with a code-unit id tie-break, independent of locale and input order"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/activityModel.ts
    - sources/ReactApp/src/components/server/ActivityEntry.tsx
    - sources/ReactApp/src/components/server/activity.css
    - sources/ReactApp/tests/hosting/activity-model.test.ts
    - sources/ReactApp/tests/hosting/activity-entry.test.tsx
  modified: []

key-decisions:
  - "An audit entry resolves its linked job for any operation with a lifecycleJobId, but only the eight echo kinds take their action from it and only they can be echo, and only when the job is loaded"
  - "A worker command status this client does not know falls back to the working log sentence, mirroring the job fallback"
  - "groupEntriesByDay accepts now as a number or a Date and groups entries with an unreadable time last under the key unknown instead of dropping them"
  - "The requester sentence is split around an invisible marker so the name sits in a bdi while the translation key stays one sentence; ActivityEntry owns a private copy because the strip's helper is not exported"

patterns-established:
  - "TimelineEntry is a discriminated union on type with shared base fields, so section and overview code switches on type only for the evidence it expands"
  - "The compact variant derives its sentence id from the entry id plus a compact suffix, so anchored and compact entries never share an id"

coverage:
  - id: D1
    description: "One job becomes a readable entry: closed sentence per status (WaitingForWorker and unknown statuses use the in-progress sentence), action label, requester actor, problem flags and active flag, newest first with a stable id tie-break"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#buildActivityEntries jobs"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every worker command becomes a log entry for the server actor, covering all eight generated command statuses with Failed as danger and CancelledByCostStop as warning"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#buildActivityEntries logs"
        status: pass
    human_judgment: false
  - id: D3
    description: "All 26 operational audit kinds map to their sentence; an unknown kind renders Platform event plus the API summary; echo kinds are hidden from All only when their job is loaded and keep full visibility in Audit"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#buildActivityEntries audit"
        status: pass
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#filterEntries and filterCounts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Each filter count equals exactly what the filter lists with and without Problems only; the audit window boundary appears only when the full take was returned; entries group by local day as today, yesterday or a dated day with the same-year flag"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#filterEntries and filterCounts"
        status: pass
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#auditWindowBoundary"
        status: pass
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#groupEntriesByDay"
        status: pass
    human_judgment: false
  - id: D5
    description: "ActivityEntry renders an article with the sentence, a decorative icon, the type tag, the actor (names in bdi) and the relative time in a time element, in English and German, anchored (id and tabindex) or compact (neither)"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-entry.test.tsx"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual layout of the entry grid, the middle-dot meta separators, the type pill and the problem tones at 320 to 1280 px; the style guard only proves the rules use tokens"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "Responsive appearance and tone of the entry are asserted in the plan 63-12 browser walk; this plan supplies the rules and the token guard."
  - id: D7
    description: "An unknown audit kind from a newer API renders Platform event: followed by the API summary in ApiText and is re-confirmed by the Phase 69 localization walk"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/hosting/activity-model.test.ts#gives a kind outside the map the Other sentence plus the API summary"
        status: pass
    human_judgment: true
    rationale: "Declared as a backstop in the plan: the Phase 69 localization walk re-confirms the rendered text in both languages."

duration: 10min
completed: 2026-11-19
status: complete
commits: 2
---

# Phase 63 Plan 08: Activity Model and Entry Summary

**One pure model merges jobs, worker command logs and audit operations into newest-first plain-language entries with actor and time, deduplicates audit echoes, filters with exact counts, marks the audit window and groups by day, plus the entry component that renders one entry anchored or compact.**

## Performance

- **Duration:** about 10 min (start inferred from the previous plan's last commit)
- **Completed:** 2026-11-19T16:32Z
- **Tasks:** 2
- **Files modified:** 5 (5 created)

## Accomplishments

- `buildActivityEntries(jobs, operations)` produces one `TimelineEntry` per job, per worker command and per operation. Sentences, action labels and actors come from closed `Map` and `switch` lookups; a job status, command status or audit kind this client does not know falls back to a fixed sentence (the Other sentence plus the API summary for audit kinds).
- Audit echo kinds (the eight job and command mirror kinds) are marked `echo` only when the job they name is loaded, so All hides them while Audit keeps them with full evidence. `filterCounts` is computed through `filterEntries`, and the test asserts equality for all four filters with and without Problems only.
- `auditWindowBoundary` returns the oldest audit time only when the full take of 200 came back; `groupEntriesByDay` groups by local calendar day (today, yesterday, dated with a same-year flag) and keeps entries with unreadable times in a last group instead of dropping them.
- `ActivityEntry` renders the sentence (with `ApiText` for API prose), a decorative type or problem icon, a type tag, the actor (names in `bdi`) and the relative time in a `time` element. The anchored variant carries `id` and `tabIndex={-1}`; the compact variant has neither and a different sentence id.

## Task Commits

1. **Task 1: Tracer - one job becomes a readable entry** - `ffe412fd` (feat)
2. **Task 2: Logs, audit, echo deduplication, filters, window and day groups** - `a3e3b099` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/activityModel.ts` - entry types, merge, filters, counts, window boundary, day groups
- `sources/ReactApp/src/components/server/ActivityEntry.tsx` - one timeline entry, anchored or compact
- `sources/ReactApp/src/components/server/activity.css` - token-only entry styles
- `sources/ReactApp/tests/hosting/activity-model.test.ts` - 89 model tests over every command status and all 26 audit kinds
- `sources/ReactApp/tests/hosting/activity-entry.test.tsx` - entry rendering in English and German

## Decisions Made

- A linked job is resolved for every operation that carries a `lifecycleJobId` (plan 63-09 will need it for the expanded evidence), but only the echo kinds take their action from it and can be `echo`.
- An unknown command status uses the working log sentence, mirroring the job fallback in the plan.
- `groupEntriesByDay` accepts `now` as a number or a `Date`.
- `ActivityEntry` keeps its own requester helper rather than editing `JobProgressStrip`, whose helper is private and outside this plan's file list.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Unused parameter in the tracer slice failed lint**
- **Found during:** Task 1 verification
- **Issue:** Task 1 builds job entries only, so the `operations` parameter was unused and `eslint --max-warnings 0` failed.
- **Fix:** Kept the parameter (the contract in the plan) with a scoped `eslint-disable-next-line` and a comment; Task 2 consumes it and the disable is gone in the final file.
- **Files modified:** `sources/ReactApp/src/features/hosting/activityModel.ts`
- **Verification:** lint passed for the Task 1 commit; the directive no longer exists after Task 2.
- **Committed in:** `ffe412fd` (Task 1 commit)

**2. [Test arithmetic] Problems only count of the All filter**
- **Found during:** Task 2 first green run
- **Issue:** My own expectation for All with Problems only was 3, but the failed-job echo is hidden from All while the degraded worker and three other problems remain, so the correct count is 4.
- **Fix:** Corrected the expectation and documented it in the test; no production change.
- **Files modified:** `sources/ReactApp/tests/hosting/activity-model.test.ts`
- **Committed in:** `a3e3b099` (Task 2 commit)

---

**Total deviations:** 2 (1 blocking lint fix, 1 test arithmetic fix)
**Impact on plan:** No scope change.

## TDD Notes

Task 2's tests were written first and ran red (72 failed, 17 passed) before the implementation. For Task 1 the tests and implementation were written in the same pass and were first run green, so a separate red run was not observed there. Each task has one `feat` commit, as the plan specifies, instead of separate test and feature commits.

## Issues Encountered

- `npm run format:check` still reports the files already recorded in `deferred-items.md` by plan 63-01; none of them is touched here.
- The three ReactApp files with CRLF-only modifications (`FleetLiveUpdatesProvider.tsx`, `useWorkspaceLiveUpdates.ts`, `workspace-live-updates.test.tsx`) were left unstaged as instructed.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. Both artifacts are pure model and presentation over props; plans 63-09 and 63-10 place them in the Activity section and the Overview.

## Threat Flags

None. No new network endpoint, auth path or trust boundary was introduced. API prose renders only as React text inside `ApiText` and names inside `bdi` (T-63-17), status and kind lookups use `Map` and `switch` with fixed fallbacks (T-63-18), and the actor is derived only from `requestedByName` and `actorName` (T-63-19).

## Next Phase Readiness

- Plan 63-09 builds the Activity section on `buildActivityEntries`, `filterEntries`, `filterCounts`, `auditWindowBoundary`, `groupEntriesByDay` and `ActivityEntry`, requesting operations with `activityOperationsTake`.
- Plan 63-10 reuses `ActivityEntry compact` on the Overview with the five newest All entries.

## Self-Check: PASSED

All five created files exist and both task commits are ancestors of HEAD.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

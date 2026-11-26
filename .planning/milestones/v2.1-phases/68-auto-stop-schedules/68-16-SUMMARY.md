---
phase: 68-auto-stop-schedules
plan: 16
subsystem: webapp
tags: [react, activity, overview, automation, i18n]
requires:
  - phase: 68-04
    provides: "Server_Activity_Trigger_*, Server_Activity_Audit_* and Server_Overview_Automation* keys in en.json and de.json"
  - phase: 68-12
    provides: "LifecycleJob.trigger and triggerLimit, GameServer.automation types and the formatWallTime helper"
provides:
  - "triggerFor and TriggerDisplay in activityLabels.ts (closed trigger map)"
  - "Trigger line in Activity entries and the job progress strip"
  - "Sentences for the seven automation audit kinds, two echo kinds, skipped-run reason"
  - "Optional Automation fact row on Overview with a link to the automation settings"
affects: [68-14, 68-17, 68-18, 68-19]
tech-stack:
  added: []
  patterns:
    - "Closed key map returns null for unknown values so the actor line stays"
key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/activityLabels.ts
    - sources/ReactApp/src/features/hosting/activityModel.ts
    - sources/ReactApp/src/components/server/ActivityEntry.tsx
    - sources/ReactApp/src/components/server/JobProgressStrip.tsx
    - sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx
    - sources/ReactApp/tests/hosting/activity-labels.test.ts
    - sources/ReactApp/tests/hosting/activity-model.test.ts
    - sources/ReactApp/tests/hosting/activity-entry.test.tsx
    - sources/ReactApp/tests/hosting/job-progress-strip.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
key-decisions:
  - "TriggerDisplay.key is a literal union of the three trigger keys, not TranslationKey, because the plural keys exist only as _one and _other in en.json"
  - "An Idle or MaxRuntime trigger without a numeric limit returns null so the actor line stays instead of rendering a broken plural"
  - "Overview next line falls back to the browser time zone when no zone was saved, and is omitted when the action or time cannot be formatted"
patterns-established:
  - "Automatic jobs reuse the manual job sentence and action label; only the meta line changes"
requirements-completed: [AUT-02, AUT-03, AUT-05]
duration: 55min
completed: 2026-11-22
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 3
plan_head_before: 8dd946cfff91d8396d4207a325384ad57bf436c1
plan_head_after: f324de4d945dae6d1aee9229ca3a482b032c8c60
commits: 3
---

# Phase 68 Plan 16: Automation in Activity and Overview Summary

**Automatic jobs read "Automatic: maximum runtime of 6 hours reached" (or idle or schedule) instead of "Requested by", the seven automation audit kinds read as sentences, and Overview shows an optional Automation row linking to `?tab=settings#server-automation`.**

## Performance

- Tasks: 2 of 2 complete
- Verification: `npm test -- tests/hosting tests/locales` (107 files, 2058 tests), `npm run typecheck` and `npm run lint` all pass

## Accomplishments

- `triggerFor(trigger, limit)` maps Idle, MaxRuntime and Schedule to their keys and count; absent, Manual and unknown values return null, so the actor line stays (T-68-39).
- Job timeline entries carry `trigger`; `ActivityEntry` and `JobProgressStrip` render the trigger line (plural via `count`) in place of the actor line. Sentence, action label, icon and facts are unchanged.
- The seven audit kinds are in `auditSentenceKeys`; `AutomaticStopRequested` and `ScheduledStartRequested` are echo kinds (hidden from All when their job is loaded, kept in Audit). `ScheduledRunSkipped` shows its reason through `ApiText`; the warning tone comes from the API status, so it is the only automation kind under Problems.
- `AutomationFact` on Overview: idle line, runtime line and next scheduled line (formatted through `formatWallTime` in the saved zone), each omitted when off, plus the compact ghost link for every viewer; nothing renders without an automation block or without anything configured.

## Task Commits

1. Task 1 (tracer): `11678907` feat(webapp): show automation triggers in activity
2. Task 2: `d6c936ea` feat(webapp): add the overview automation fact
3. Tests for Task 2: `f324de4d` test(webapp): cover automation activity and overview

The tracer gate re-ran `<verify>` for Task 1 (4 test files, typecheck, lint) before expanding; it passed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] TriggerDisplay.key typed as a literal union**
- **Found during:** Task 1
- **Issue:** The interface in the plan types `key` as `TranslationKey`, but `TranslationKey` is `keyof typeof en`, and the two plural trigger keys exist only with `_one` and `_other` suffixes, so the base keys would not type-check.
- **Fix:** `TriggerKey` literal union of the three base keys (the same approach as `MetricTileText.key`); `t(key, { count })` type-checks.
- **Files modified:** `sources/ReactApp/src/features/hosting/activityLabels.ts`
- **Commit:** 11678907

**2. [Rule 2 - Missing critical] Idle or MaxRuntime trigger without a limit returns null**
- **Found during:** Task 1
- **Issue:** A plural key rendered without a count resolves to nothing; an incomplete API payload would show an empty or broken line.
- **Fix:** `triggerFor` returns null for those cases so the actor line stays.
- **Commit:** 11678907

Tests for Task 1 were committed with its feature commit (tracer, verified together) rather than in the final test commit.

## Known Stubs

None.

## Threat Flags

None. No new endpoint or trust boundary; T-68-39 mitigated by the closed trigger map, unknown values keeping the actor line, and API prose only through `ApiText`.

## Self-Check: PASSED

- All ten modified files exist; no file under `sources/Api` or `sources/WebApp` changed.
- Commits 11678907, d6c936ea and f324de4d are ancestors of HEAD.

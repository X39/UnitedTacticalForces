---
phase: 68-auto-stop-schedules
plan: 11
subsystem: api
tags: [automation, schedules, scheduled-start, scheduled-stop, hosted-service, lifecycle, consent]

requires:
  - phase: 68-auto-stop-schedules
    provides: entities and audit kinds (68-01), ScheduleCalculator and options (68-02), EnqueueAutomatedActionAsync and CanReserveInitialSliceAsync (68-05), AutoStop alert writer (68-07), settings service and watermark (68-08), planner and evaluator (68-10)
provides:
  - ServerScheduleRunner that records each due schedule occurrence exactly once (start outcomes, stop pending rows, watermark)
  - Planner Replace decision and scheduled-stop candidates; evaluator integration (scan first, Replace, run row at execution)
  - Shared ServerRowLock, ScheduledRunLedger and PendingStopWithdrawal helpers
affects: [68-13, 68-14]

status: complete
actuals:
  tokens: 100000
  tasks: 2
  commits: 3
plan_head_before: 5485b357415ce1e5806dc2d768963a3bb3c5014b
plan_head_after: b456da7ce7ce3000f19627d6c6349588e40aeae9

tech-stack:
  added: []
  patterns:
    - "One transaction per schedule occurrence under the server-row lock, guarded by the unique (rule, occurrence) run row"
    - "A due stop is committed as a Pending row with WarnedAt null; the evaluator's reconcile executes it, so the occurrence cannot be lost"

key-files:
  created:
    - sources/Api/Services/ServerAutomation/IServerScheduleRunner.cs
    - sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs
    - sources/Api/Services/ServerAutomation/ScheduleScanResult.cs
    - sources/Api/Services/ServerAutomation/ServerRowLock.cs
    - sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs
    - sources/Api/Services/ServerAutomation/PendingStopWithdrawal.cs
    - tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs
  modified:
    - sources/Api/Services/ServerAutomation/EPendingStopDecision.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlanInput.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlan.cs
    - sources/Api/Services/ServerAutomation/PendingStopPlanner.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationEvaluator.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationTestData.cs
    - tests/Api.Tests/ServerAutomation/PendingStopPlannerTests.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationWorkerTests.cs

key-decisions:
  - "An open scheduled stop is valid only while its rule exists and rule.EffectiveFrom is not after the warning's creation time or its occurrence; a plain 'EffectiveFrom after occurrence' check cannot see a rule edited mid-window because saving resets EffectiveFrom to the save time, which lies before the occurrence"
  - "Superseded scheduled stops record their run (SkippedOff when the deployment is gone, SkippedBusy otherwise); cancelled ones record none"
  - "A scan never looks back more than eight days and the watermark is refreshed at most every five minutes when nothing was due, which bounds run-table growth and state writes"
  - "A schedule scan failure is logged and does not keep the idle and maximum runtime evaluation of the same server from running"

patterns-established:
  - "NextScheduledStop takes an optional handled set so occurrences with a pending stop or run row are never proposed twice"

requirements-completed: [AUT-03, AUT-04, AUT-05, AUT-06]

coverage:
  - id: D1
    description: "A weekly stop rule warns one lead before its time and queues one ordinary Stop job (trigger Schedule) at its time; rule deletion or change withdraws the warning"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs"
        status: pass
  - id: D2
    description: "Catch-up after downtime: due stop runs once with the downtime note, missed/off occurrences are recorded, the past is never fired"
    requirement: "AUT-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs"
        status: pass
  - id: D3
    description: "Scheduled starts need consent for the current cost owner and an affordable owner; every skip records one run row and one ScheduledRunSkipped audit entry in the order missed, consent, running, busy, cost"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs"
        status: pass
  - id: D4
    description: "Earlier stop wins between scheduled and maximum runtime/idle warnings (Replace); repeated passes and lost commits act once"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/PendingStopPlannerTests.cs, ScheduledRunTests.cs"
        status: pass
---

# Phase 68 Plan 11: Scheduled Starts and Stops Summary

**Weekly schedule rules now start and stop servers: a runner records every due occurrence exactly once, queues consented and affordable starts or records why it skipped them, turns stop occurrences into warned pending stops, and catches up safely after downtime.**

## Accomplishments

- `ServerScheduleRunner.ProcessDueAsync` scans `(max(watermark, EffectiveFrom), now]` per rule, each occurrence in its own transaction under the server-row lock, then advances `SchedulesEvaluatedThrough`. Start occurrences evaluate missed, consent, running, busy and cost in that order; a start inserts its run row and the ordinary Start job (trigger Schedule, no user) in one transaction. A `HostingConflictException` or `HostingValidationException` rolls back, clears the tracker and records `SkippedBusy` in a fresh transaction.
- Stop occurrences commit a Pending stop (`WarnedAt` null) when the deployment was running at the occurrence, otherwise record `SkippedMissed` (started later), `SkippedOff` (not running) or `SkippedBusy` (a stop is queued or an earlier stop applies). The evaluator's reconcile executes it and writes the `Stopped` run row in the execute transaction.
- `PendingStopPlanner` gained `Replace` (earlier scheduled stop supersedes an open warning), scheduled candidates in the new-warning race, and cancellation of an open scheduled stop whose rule is gone or changed. The evaluator runs the scan first, passes `NextScheduledStop` (excluding occurrences that already have a pending or run row), implements Replace, and selects servers that have schedule rules.
- 23 tests in `ScheduledRunTests` plus 7 new planner cases; `ServerAutomation|GameServerHosting` filter: 1461 passed, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.

## Task Commits

1. `42943812` feat(api): run scheduled server stops (tracer: stop rule warns, executes, catches up; verified end to end by `ScheduledRunTests` before the start path was enabled)
2. `a06738f2` feat(api): run consented scheduled server starts
3. `b456da7c` test(api): cover scheduled runs and catch-up

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan's rule-changed check cannot detect an edited rule**
- **Found during:** Task 1
- **Issue:** The plan cancels an open scheduled stop when `rule.EffectiveFrom` is after its occurrence. Saving an edited rule sets `EffectiveFrom` to the save time, which is before the occurrence, so an edited rule would keep the stale warning.
- **Fix:** The evaluator also compares `EffectiveFrom` with the warning's creation time; the planner receives the result as `OpenScheduleRuleValid`.
- **Files modified:** `ServerAutomationEvaluator.cs`, `PendingStopPlanInput.cs`, `PendingStopPlanner.cs`
- **Commit:** `42943812`

**2. [Rule 2 - Missing critical functionality] Re-warning an occurrence that was already handled**
- **Found during:** Task 1
- **Issue:** `NextScheduledStop(settings, now, lead)` alone would propose an occurrence again after its warning was superseded (for example by a manual stop and restart), creating a second pending row and a duplicate run row.
- **Fix:** Added the optional `handled` set parameter to `NextScheduledStop`; the evaluator fills it with pending and run keys inside the window.
- **Commit:** `42943812`

**3. [Rule 3 - Blocking] Shared helpers**
- Superseding or locking is needed by both the runner and the evaluator, so `ServerRowLock`, `PendingStopWithdrawal` and `ScheduledRunLedger` were extracted (one type per file); the evaluator's private lock and withdraw code now delegates to them. Constructors of the evaluator in `ServerAutomationWorkerTests` were updated for the new dependency.

### Other notes

- The competition tests use an open maximum runtime warning instead of an idle one: arming idle needs a full metrics seed and adds nothing, and `PendingStopPlannerTests` covers the idle replace and keep cases directly.
- The first commit contains the start code but with the start branch of the scan disabled, so each task commit matches its plan; the second commit enables it.

## Known Stubs

None.

## Threat Flags

None. No new endpoint or trust boundary; T-68-27..30 mitigations are implemented as planned (consent bound to the current cost owner, unique run rows, `CanReserveInitialSliceAsync` pre-check, one `ScheduledRunSkipped` audit entry per skip).

## Self-Check: PASSED

- Files exist: all created files listed above found.
- Commits `42943812`, `a06738f2`, `b456da7c` are ancestors of HEAD.
- Acceptance greps: `ProcessDueAsync` in evaluator 1, `Replace = 6` 1, `SchedulesEvaluatedThrough` in runner 4, `CanReserveInitialSliceAsync` 1, `[Fact]` count 23 (>= 12).
- No file under `sources/Database` or `sources/WebApp` changed.

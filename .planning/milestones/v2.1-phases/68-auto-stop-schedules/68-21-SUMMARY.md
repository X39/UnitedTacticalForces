---
phase: 68-auto-stop-schedules
plan: 21
subsystem: api
tags: [aspnetcore, efcore, schedules, lifecycle, openapi, gap-closure]

requires:
  - phase: 68-auto-stop-schedules
    provides: "68-19 schedule runner with unique (ScheduleFk, OccurrenceAt) run rows and the lifecycle service"
provides:
  - "WaitingForStop and SkippedStopUnfinished outcomes on the existing run row"
  - "Durable waiting state for a scheduled start that meets a stop in flight, re-collected on every pass"
  - "Closing rules for a waiting start with the real blocker (failed stop, next occurrence, rule change)"
  - "Live fleet frame for schedule run rows"
  - "Refreshed OpenAPI snapshot and generated React contract"
affects: [68-22 React sentences for the two new outcomes]

actuals:
  tokens: 19600
  tasks: 3
  commits: 4
plan_head_before: 8f99917f558e0305a76df0b615a5b7e3e65a1e9a
plan_head_after: 29e3959bb985e96b3cf02570756b0877948ca394

tech-stack:
  added: []
  patterns:
    - "The unique run row doubles as the durable open marker; only WaitingForStop is ever replaced, under the server-row lock"

key-files:
  created: []
  modified:
    - sources/HostingContracts/EServerScheduleRunOutcome.cs
    - sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs
    - sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs
    - sources/Database/Hosting/ServerScheduleRun.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
    - tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs
    - tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationProjectionTests.cs
    - tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "A due start waits without a time limit while a stop or terminate of the same server is in flight; StartGraceWindow only applies to an occurrence first seen late"
  - "A waiting start ends without a start when the stop failed, the rule's next occurrence came due, or the rule was changed; all record SkippedStopUnfinished"
  - "A failed stop is only detected on the unresolved deployment's own stop jobs, so an old failed stop of a resolved deployment cannot block a later start"
  - "No migration and no change to GameServerLifecycleService: Outcome is an integer column"

requirements-completed: [AUT-03, AUT-05, AUT-06]

coverage:
  - id: D1
    description: "A start that comes due while a stop of the same server is in flight is stored as WaitingForStop and queued exactly once after the stop finished, past the grace window"
    requirement: "AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs#StartRule_WhileAStopJobIsInFlight_WaitsAndStartsOnceAfterTheStopFinished"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every way a wait ends without a start is closed on the same row with its real blocker and one audit entry"
    requirement: "AUT-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs#StartRule_WhenTheStopFailed_ClosesTheWaitingStartAsSkippedStopUnfinished"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs#StartRule_AfterTheStopFinished_ClosesTheSameRowWithTheNormalChecks"
        status: pass
    human_judgment: false
  - id: D3
    description: "The waiting start survives an API restart and two instances exactly once"
    requirement: "AUT-06"
    verification:
      - kind: integration
        ref: "tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs#Restart_ScheduledStartWaitingForAStop_StartsExactlyOnceOnTheNextProcess"
        status: pass
    human_judgment: false
  - id: D4
    description: "Run-row decisions reach live viewers and the API contract carries both new outcomes"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs#ScheduleRunAddAndOutcomeChange_NotifyServer_RecordedAtAlone_StaysSilent"
        status: pass
      - kind: other
        ref: "OpenAPI export is byte-identical to sources/ReactApp/openapi/utf-api.json (cmp exit 0)"
        status: pass
    human_judgment: false

duration: 60min
completed: 2026-11-25
status: complete
---

# Phase 68 Plan 21: Scheduled Start Waits For A Stop Summary

**A scheduled start that comes due while a stop of the same server is still being processed is stored as WaitingForStop on its existing run row and queued exactly once after the stop finished, with consent and cost re-checked at that moment and every wait that ends without a start naming its real blocker.**

## Product decision and bound (for the user)

The orchestrator's reading of UAT gap G-68-3 was adopted as stated in the plan:

- Waiting has **no fixed time limit**. A due start waits as long as a stop or terminate of the same server is actively in flight (queued, running, retry-pending or waiting for its worker, or a due automatic stop that the evaluator executes in the same pass). The 15 minute `StartGraceWindow` keeps its meaning only for an occurrence first seen late (the platform was not running).
- Bound: a waiting start ends **without** a start when (a) the stop failed and waits for a person, (b) the same rule's next occurrence came due while the stop is still running (the newer occurrence takes over, so at most one start per rule waits and a stuck stop can never release a backlog of starts), or (c) the rule's action, days, time or time zone was changed after the occurrence. All three record `SkippedStopUnfinished` with the real blocker and a `ScheduledRunSkipped` audit entry. Removing the rule deletes its run rows (existing cascade).
- When the stop has finished the normal checks decide on the same row: consent for the current cost owner (`SkippedConsent`), a server running again (`SkippedRunning`), another active non-stop job (`SkippedBusy`), the owner's ability to pay (`SkippedCost`), and a refusal of the lifecycle service (`SkippedBusy` with its reason).
- D-AUTO-2 stays intact: a failed stop is never escalated; the waiting start is closed instead.

Final evaluation order: missed (first evaluation only), bound (waiting only), consent, stop in flight, stop failed, running, busy, cost.

## Accomplishments

- Added `WaitingForStop = 9` and `SkippedStopUnfinished = 10` (stored as integers; no schema change, `dotnet ef migrations has-pending-model-changes` reports none).
- `ScheduledRunLedger` gained `FindAsync`, `AddWaiting` and `RecordSkip`; `AddSkip` delegates to `RecordSkip` and `ExistsAsync` keeps its any-row meaning for the stop callers.
- `ServerScheduleRunner` re-collects open waiting rows each pass independent of the watermark, merges them without duplicates, always sends them down the start path, reads the rule again inside the locked transaction, and turns the waiting row into the started row in the same transaction that queues the Start job (audit note carries `WaitedForStop=true`). The watermark is still driven only by newly collected occurrences.
- `FleetChangeInterceptor` now signals on an added schedule run row or a changed `Outcome`, so the rule's last-run line refreshes live; a waiting row is not rewritten per pass, so it cannot flood the stream.
- OpenAPI snapshot and generated React contract refreshed; the diff is exactly the two enum values.

## Task Commits

1. Task 1 (tracer): `5f1c4968` feat(api): keep a scheduled start waiting while a stop is in flight
2. Task 2: `b07de669` feat(api): close a waiting scheduled start with its real blocker
3. Task 3: `e64669b2` feat(api): announce schedule run decisions to live viewers
4. Task 3 contract: `29e3959b` build(webapp): refresh the API contract for waiting scheduled starts

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj` (full run): 1726 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- `dotnet ef migrations has-pending-model-changes`: no changes.
- OpenAPI export compared with the committed snapshot with `cmp`: byte-identical (exit 0).
- `git diff 8f99917f -- sources/WebApp sources/Database/Migrations sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs`: empty.
- RED was observed first for Tasks 1 and 2 (10 and 4 failing tests respectively). For the interceptor change in Task 3 the implementation was written before the new test was run, so a separate RED run was not recorded; the silent `RecordedAt`-only branch is asserted by the test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Correctness] Failed-stop check scoped to the unresolved deployment**
- **Found during:** Task 2
- **Issue:** The plan says "latest Stop or Terminate job" of a server with an unresolved deployment. Taken literally, an old failed stop of an already resolved deployment would make every later start of a fresh deployment report `SkippedStopUnfinished` instead of `SkippedRunning`.
- **Fix:** The latest stop or terminate job is looked up only among jobs whose deployment is still unresolved.
- **Files modified:** sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs
- **Commit:** b07de669

**2. [Rule 3 - Blocking] `npm run typecheck` and `contracts:check` could not run in the worktree**
- **Issue:** The worktree has no `node_modules`, and installing packages is out of scope for this plan.
- **Fix:** The generator binary of the main checkout's existing `node_modules` was called by path against the worktree files (`openapi-typescript openapi/utf-api.json -o src/generated/api.ts`, same pinned version 7.13.0). Nothing was installed or modified in the main checkout.
- **Not run:** `npm --prefix sources/ReactApp run typecheck` and `contracts:check`. The change to `api.ts` is one line (two added members of a string-literal union), and the only consumer, `lastRunKey` in `automationModel.ts`, is keyed by plain strings and falls back to the "Other" sentence, so a type error is not expected. The orchestrator should run both after merging in a checkout that has `node_modules`.

## Known Stubs

None.

## Issues Encountered

- The commit-message attribution line named in the dispatch notes (Opus) differs from the one given by the harness (Sonnet 5.5); the harness line was used on all commits.

## Deferred Follow-up

- Deferred follow-up: real worker stop-then-start at milestone end. Seeded-stack Playwright coverage was not added: the React draft validation rejects two rules that share a weekday and a time, so a start comes due at least one minute after its stop, and the UAT emulated worker completes a stop within seconds, so the start never meets a stop in flight on the seeded stack. The API tests cover the window deterministically, including the restart.
- The React sentences for the two new outcomes follow in plan 68-22; until then the app shows the plain "Last run: {time}." sentence for them.

## Threat Flags

None. The plan's T-68-46 to T-68-50 are mitigated as planned: consent and cost are re-read in the locked transaction that queues the start; the waiting row is only turned into Started under the server-row lock (tests for repeated passes, a rewound watermark, two contexts and a restart); the bound closes every wait; reason texts are fixed English strings; frames only fire for an added row or a changed outcome.

## Self-Check: PASSED

- FOUND: sources/HostingContracts/EServerScheduleRunOutcome.cs (WaitingForStop = 9, SkippedStopUnfinished = 10)
- FOUND: commits 5f1c4968, b07de669, e64669b2, 29e3959b (ancestors of HEAD)
- FOUND: `StartRule_` tests: 22 (at least 21 required)

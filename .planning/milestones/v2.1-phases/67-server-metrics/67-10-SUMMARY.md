---
phase: 67-server-metrics
plan: 10
subsystem: worker
tags: [metrics, worker, arma3, players, sqf, headless-client]

requires:
  - phase: 67-05
    provides: IWorkerCommandExecutor.ObservePlayersAsync default and WorkerPlayerObservation
provides:
  - UTFPL player record written by the Arma 3 server addon (humans only, nonce-bound)
  - Arma3PlayerEvidence bounded parser and Arma3JoinMonitor.ReadLatestPlayersAsync (90 s freshness)
  - Arma3WorkerCommandExecutor.ObservePlayersAsync
  - Server addon installed and loaded for every Arma 3 primary launch
affects: [67-14, 67-20]

plan_head_before: 2d5728c1e0da9aa909e9a94ab3047992f975b65f
plan_head_after: 164db79196247e2533bef275f323a5a373199302

actuals:
  tokens: 14000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Second versioned record (UTFPL) beside UTFHC in one addon, each with its own uiNamespace sequence counter"
    - "Shared private tail-line reader so readiness and player readers cannot diverge on bounds"

key-files:
  created:
    - sources/CloudWorker/Arma3PlayerEvidence.cs
    - sources/CloudWorker/Arma3WorkerCommandExecutor.Players.cs
    - tests/CloudWorker.Tests/Arma3PlayerEvidenceTests.cs
  modified:
    - sources/CloudWorker/Arma3JoinAddonAssets/fn_init.sqf
    - sources/CloudWorker/Arma3JoinAddon.cs
    - sources/CloudWorker/Arma3JoinMonitor.cs
    - sources/CloudWorker/Arma3WorkerCommandExecutor.Admission.cs
    - sources/CloudWorker/Arma3WorkerCommandExecutor.cs
    - sources/CloudWorker/Arma3WorkerCommandExecutor.Join.cs
    - sources/CloudWorker/README.md
    - tests/CloudWorker.Tests/Arma3JoinAddonTests.cs
    - tests/CloudWorker.Tests/Arma3JoinMonitorTests.cs
    - tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs

key-decisions:
  - "Capacity stays null for Arma 3: the engine does not expose the configured slot count to SQF"
  - "ObservePlayersAsync also reports measuring while the primary process is not running, so a stopped server's last record is never shown as a fresh count"
  - "The first sighting of a record of a launch counts as an advance, so after a worker restart a stale record can show for at most 90 s before query-failed"
  - "A malformed saved session reports query-failed instead of throwing from the metrics path"

requirements-completed: [MET-02]

coverage:
  - id: D1
    description: "The addon counts connected users that are neither headless clients nor the server and writes a nonce-bound UTFPL record on change and at least every 30 seconds; UTFHC lines are unchanged"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3JoinAddonTests.cs#Install_WritesDeterministicLaunchBoundUncompressedAddon"
        status: pass
    human_judgment: true
    rationale: "The SQF runs only inside the Arma 3 engine; tests assert the installed script text, so engine behavior is confirmed by the end-of-phase UAT of plan 67-20"
  - id: D2
    description: "The parser accepts only the current nonce, sequence above 0 and counts 0 to 1024, and the monitor reports the highest sequence, measuring before a record, and query-failed after 90 s without a new sequence"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3PlayerEvidenceTests.cs#Parse_RejectsMalformedRecords"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3JoinMonitorTests.cs#ReadLatestPlayers_StaleAfterNinetySecondsWithoutAdvancingSequence"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3JoinMonitorTests.cs#ReadLatestPlayers_WithoutRecordReportsMeasuring"
        status: pass
    human_judgment: false
  - id: D3
    description: "The Arma 3 executor reports the count with null capacity for a primary, measuring without a session or process, and game-does-not-report on a headless-client host"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs#ObservePlayers_PrimaryWithFreshRecordReportsCountWithoutCapacity"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs#ObservePlayers_HeadlessClientHostReportsGameDoesNotReport"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every primary launch installs and loads the addon while headless-client readiness verification is unchanged"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs#Start_WithoutHeadlessAssignmentStillEnablesAddonForPlayers"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs#VerifyReadiness_WithoutHeadlessAssignmentCompletesWithoutWaiting"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/Arma3AdmissionLifecycleTests.cs#HeadlessRestart_UsesNewEndpointWithoutRestoringPrimaryStorage"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 10: Arma 3 Human Player Count Summary

**Arma 3 now reports a headless-free human player count read from a nonce-bound UTFPL record that the worker-owned server addon writes from inside the engine, and the addon loads for every primary launch.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-11-22T11:01Z
- **Tasks:** 2
- **Files modified:** 13 (3 created, 10 modified)

## Accomplishments
- `fn_init.sqf` counts `allUsers` entries whose `getUserInfo` headless flag (index 7) is not true and whose owner id (index 1) is above 2, and writes `UTFPL|1|<nonce>|<sequence>|<humans>` inside `isNil` with its own `uiNamespace` sequence counter, on change and at least every 30 s. Every UTFHC line is untouched.
- `Arma3PlayerEvidence.Parse` mirrors the UTFHC parser (256 character limit, balanced quotes, non-backtracking `GeneratedRegex`, optional RPT timestamp) and accepts counts 0 to 1024 only.
- `Arma3JoinMonitor.ReadLatestPlayersAsync` shares one private tail reader with `ReadLatestAsync` (same 64 KiB bound), keeps the highest sequence and the `TimeProvider` time it last advanced, and reports `Players(humans, null)`, query-failed after 90 s, or measuring before any record.
- `Arma3WorkerCommandExecutor.ObservePlayersAsync` reports game-does-not-report on a headless-client host, measuring without a running process or session, otherwise the monitor reading.
- `PrepareAdmissionAsync` and the primary `-serverMod=` argument now use `IsPrimaryService`; `RequiresJoinEvidence`, `VerifyJoinAsync` and `HasExpiredAsync` are unchanged.

## Task Commits

1. **Task 1: Tracer - engine human player count to worker observation** (tracer verified end-to-end: 78 targeted tests)
   - `e096b599` feat(worker): report Arma 3 human players from the server addon
   - `887a39b1` test(worker): cover the Arma 3 player record
2. **Task 2: Addon for every primary launch**
   - `e5e1e1c8` feat(worker): load the Arma 3 server addon for every primary launch
   - `164db791` test(worker): keep headless readiness while loading the addon always

**Plan metadata:** committed separately (docs: complete plan).

## Decisions Made
See `key-decisions` in the frontmatter. Capacity is intentionally null, so the tile shows the count without "of N slots".

## Deviations from Plan

**1. [Rule 2 - Missing critical] Process-running guard in ObservePlayersAsync**
- **Found during:** Task 1
- **Issue:** Without a liveness check a stopped server's last record would be reported as a fresh count for up to 90 s.
- **Fix:** `ObservePlayersAsync` reports measuring while the primary process is not running; a malformed session reports query-failed.
- **Files modified:** `sources/CloudWorker/Arma3WorkerCommandExecutor.Players.cs`
- **Commit:** `e096b599`

**2. [Scope] Test and doc files outside the listed set**
- The existing assertion that primaries without headless clients load no addon lived in `Arma3AdmissionLifecycleTests.cs` (with the only executor fixture), so the executor player tests and the updated assertions are there instead of `Arma3WorkerCommandExecutorTests.cs`. The XML doc of `RequiresJoinEvidence` (`Arma3WorkerCommandExecutor.Join.cs`) and `README.md` were updated to match the new behavior.

**TDD ordering:** each feat commit precedes its test commit and the tests were written alongside the implementation; no non-compiling RED commit was made (executor brief: every commit builds).

**Total deviations:** 1 auto-fixed (1 missing-critical), 1 scope note. **Impact:** none on the plan's goal.

## Known Stubs

None.

## Open Item

The SQF is verified by its installed text only; whether a live Arma 3 server writes UTFPL records and excludes the headless client is confirmed by the end-of-phase UAT of plan 67-20.

## Threat Flags

None. T-67-21 and T-67-22 are mitigated as planned (nonce, advancing sequence, bounded line, 0 to 1024 counts, emission on change and every 30 s, existing 64 KiB tail).

## Verification

`dotnet build X39.UnitedTacticalForces.slnx`: 0 errors. `dotnet test tests/CloudWorker.Tests`: 343 passed. No file under `sources/Api`, `sources/ReactApp`, `CloudWorkerRuntime.cs` or `Program.cs` was touched.

## Self-Check: PASSED

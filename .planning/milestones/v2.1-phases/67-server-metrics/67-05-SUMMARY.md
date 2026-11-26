---
phase: 67-server-metrics
plan: 05
subsystem: worker
tags: [metrics, worker, a2s, udp, cgroup, proc-stat, players]

requires:
  - phase: 67-02
    provides: WorkerMetricUnavailableReasons with the WorkerMachineReasons and WorkerPlayerReasons sets
provides:
  - IWorkerCommandExecutor.ObservePlayersAsync with a game-does-not-report default
  - WorkerPlayerObservation (count and capacity, or one reason)
  - IA2sInfoClient and A2sInfoClient (loopback A2S_INFO with challenge handling), A2sInfo with Humans
  - AskaWorkerCommandExecutor player observation on the fixed query port 27016
  - IWorkerMetricsSampler and WorkerMetricsSampler (container-aware CPU and memory) with WorkerMachineReading
affects: [67-09, 67-10, 67-14, 67-20]

plan_head_before: d1943d0749ba6fd60acd8b159811cff1736e6646
plan_head_after: 8231a87bd9b40d7c3f72e3355f36b0cbcae7c9d4

actuals:
  tokens: 11000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Every observation is a number or exactly one reason; factories reject anything else"
    - "File access injected as Func<string, string?> so /proc and cgroup fixtures drive the tests"
    - "In-process loopback UDP responder for protocol tests"

key-files:
  created:
    - sources/CloudWorker/WorkerPlayerObservation.cs
    - sources/CloudWorker/A2sInfo.cs
    - sources/CloudWorker/IA2sInfoClient.cs
    - sources/CloudWorker/A2sInfoClient.cs
    - sources/CloudWorker/WorkerMachineReading.cs
    - sources/CloudWorker/IWorkerMetricsSampler.cs
    - sources/CloudWorker/WorkerMetricsSampler.cs
    - tests/CloudWorker.Tests/A2sInfoClientTests.cs
    - tests/CloudWorker.Tests/WorkerMetricsSamplerTests.cs
  modified:
    - sources/CloudWorker/IWorkerCommandExecutor.cs
    - sources/CloudWorker/AskaWorkerCommandExecutor.cs
    - tests/CloudWorker.Tests/AskaImplementationTests.cs

key-decisions:
  - "A2S 'at most 3 rounds' means at most 3 requests per query (initial plus two challenge resends); the third challenge reply ends the query with null"
  - "ASKA reports measuring (not query-failed) whenever the process is not running; any running process is queried, readiness is not required"
  - "CPU counters that do not advance or go backwards report measuring and re-baseline, so a counter reset never shows a fake value"
  - "A numeric memory.max is trusted as the container total; only 'max' falls back to MemTotal"

patterns-established:
  - "Executors without a player source inherit the game-does-not-report default"

requirements-completed: [MET-02, MET-03, MET-05]

coverage:
  - id: D1
    description: "A running ASKA server's human player count (players minus bots) and capacity are read over loopback A2S_INFO; a silent server reports query-failed and a stopped process reports measuring"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/AskaImplementationTests.cs#ObservePlayers_RunningAndQueryAnswers_ReportsHumanCountAndCapacity"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/AskaImplementationTests.cs#ObservePlayers_RunningAndQueryFails_ReportsQueryFailed"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/AskaImplementationTests.cs#ObservePlayers_NotRunning_ReportsMeasuringWithoutQuerying"
        status: pass
    human_judgment: false
  - id: D2
    description: "The A2S client decodes direct and challenged replies and returns null on timeout, split packets, truncated replies and endless challenges"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/A2sInfoClientTests.cs#QueryAsync_ChallengeThenInfo_AppendsChallengeInLittleEndianOrder"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/A2sInfoClientTests.cs#QueryAsync_EndlessChallenges_StopsAfterThreeRoundsWithNull"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/A2sInfoClientTests.cs#QueryAsync_MalformedReply_ReturnsNull"
        status: pass
    human_judgment: false
  - id: D3
    description: "Games without their own source report game-does-not-report, never a number"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/AskaImplementationTests.cs#ObservePlayers_ExecutorWithoutPlayerSource_ReportsGameDoesNotReport"
        status: pass
    human_judgment: false
  - id: D4
    description: "CPU percent from /proc/stat deltas and cgroup v2 working-set memory with a meminfo fallback; first sample, unreadable files and non-Linux hosts report a reason, never zero"
    requirement: "MET-03"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/WorkerMetricsSamplerTests.cs#Sample_SecondCpuSample_ReportsPercentFromDeltas"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/WorkerMetricsSamplerTests.cs#Sample_CgroupV2_ReportsWorkingSetOfLimit"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/WorkerMetricsSamplerTests.cs#Sample_NothingReadable_ReportsQueryFailedForBoth"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/WorkerMetricsSamplerTests.cs#Sample_NonLinuxHost_ReportsUnsupportedPlatformWithoutReading"
        status: pass
    human_judgment: false
  - id: D5
    description: "Whether a real ASKA server answers A2S_INFO on port 27016 is confirmed against a live server"
    requirement: "MET-02"
    verification:
      - kind: other
        ref: "Deferred to the end-of-phase UAT of plan 67-20 (research A2)"
        status: deferred
    human_judgment: true

duration: 25min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 05: Worker Player and Machine Readings Summary

**The worker can now read ASKA's human player count over loopback A2S_INFO and sample container-aware CPU and memory, and every reading is a measured value or exactly one reason, never an invented zero.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-11-22T10:00Z
- **Tasks:** 2
- **Files modified:** 12 (9 created, 3 modified)

## Accomplishments
- `IWorkerCommandExecutor.ObservePlayersAsync` is a default interface method returning game-does-not-report, so Counter-Strike 2 and Arma 3 stay compatible until plans 67-09 and 67-10 override it.
- `A2sInfoClient` sends `FF FF FF FF 54 "Source Engine Query" NUL` from an ephemeral loopback socket to 127.0.0.1 only, answers a 0x41 challenge by appending it, stops after 3 requests and returns null on timeout, socket errors, split-packet headers and short replies. Only caller cancellation throws.
- `AskaWorkerCommandExecutor.ObservePlayersAsync` queries port 27016 with a 2 s timeout while the process runs and reports `Players(humans, max)`, query-failed, or measuring when stopped.
- `WorkerMetricsSampler` computes CPU as `100 * (1 - idleDelta / totalDelta)` (idle includes iowait, rounded to 0.1, clamped) and memory as cgroup v2 `memory.current` minus `inactive_file` of `memory.max`, falling back to `/proc/meminfo`.
- 31 tests for the A2S client and ASKA, 16 for the sampler; the whole CloudWorker suite passes (257) and `X39.UnitedTacticalForces.slnx` builds with 0 errors.

## Task Commits

1. **Task 1: Tracer - ASKA human player count through A2S_INFO** (tracer verified end-to-end: 31 tests)
   - `d1068497` test(worker): cover the A2S_INFO client and ASKA players (RED)
   - `00fe354c` feat(worker): observe players through A2S_INFO for ASKA (GREEN)
2. **Task 2: Machine CPU and memory readings**
   - `0b16ecdd` test(worker): cover machine metric sampling (RED)
   - `8231a87b` feat(worker): sample machine CPU and memory with reasons (GREEN)

**Plan metadata:** committed separately (docs: complete plan).

## Files Created/Modified
- `sources/CloudWorker/IWorkerCommandExecutor.cs` - `ObservePlayersAsync` default
- `sources/CloudWorker/WorkerPlayerObservation.cs` - value-or-reason player reading with validating factories
- `sources/CloudWorker/A2sInfo.cs`, `IA2sInfoClient.cs`, `A2sInfoClient.cs` - loopback A2S_INFO
- `sources/CloudWorker/AskaWorkerCommandExecutor.cs` - optional `a2s` constructor parameter and the observation
- `sources/CloudWorker/WorkerMachineReading.cs`, `IWorkerMetricsSampler.cs`, `WorkerMetricsSampler.cs` - machine sampler
- `tests/CloudWorker.Tests/A2sInfoClientTests.cs`, `WorkerMetricsSamplerTests.cs`, `AskaImplementationTests.cs` - tests

## Decisions Made
- "At most 3 challenge rounds" is implemented as at most 3 requests per query.
- Any running ASKA process is queried; readiness is not required (the plan said "running"), a stopped process reports measuring.
- Non-advancing or regressing CPU counters report measuring and re-baseline.
- Numeric `memory.max` is the total as-is; `max` uses `MemTotal`.

## Deviations from Plan

None - plan executed as written. One ordering note:

**TDD ordering:** the plan lists the `feat` commit before the `test` commit. Per the executor instruction the failing tests were committed first (RED), then the implementation (GREEN). Each RED commit does not compile because the types it exercises do not exist yet; the next commit restores a green build. The compile failure was confirmed before the first RED commit.

## Known Stubs

None.

## Open Item

Whether a live ASKA server answers A2S_INFO on its query port is unverified here (research A2); plan 67-20's end-of-phase UAT confirms it. Until then ASKA reports query-failed when it does not answer.

## Self-Check: PASSED

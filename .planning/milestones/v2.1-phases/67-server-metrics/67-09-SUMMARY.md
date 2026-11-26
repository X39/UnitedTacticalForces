---
phase: 67-server-metrics
plan: 09
subsystem: worker
tags: [metrics, worker, cs2, rcon, a2s, players]

requires:
  - phase: 67-05
    provides: ObservePlayersAsync default, WorkerPlayerObservation, IA2sInfoClient and A2sInfoClient
provides:
  - ICounterStrike2RconClient.RequestStatusAsync with bounded reads
  - CounterStrike2StatusParser (humans and capacity from the RCON status players line)
  - CounterStrike2WorkerCommandExecutor.ObservePlayersAsync with RCON first and A2S_INFO fallback
  - CounterStrike2ProcessIdentity.GamePort persisted by the process controller
affects: [67-14, 67-20]

plan_head_before: 4d8cef371959fcfe16cee09d8bf48514441bdeed
plan_head_after: 8f50ee6740f727d2a0f6592fd4db320c2bb70f1d

actuals:
  tokens: 14000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Secrets held in a nested class without a value-bearing ToString, replaced on start and cleared on stop"
    - "Loopback TCP server fixture for RCON protocol tests"

key-files:
  created:
    - sources/CloudWorker/CounterStrike2StatusParser.cs
    - tests/CloudWorker.Tests/CounterStrike2StatusParserTests.cs
  modified:
    - sources/CloudWorker/ICounterStrike2RconClient.cs
    - sources/CloudWorker/CounterStrike2RconClient.cs
    - sources/CloudWorker/CounterStrike2WorkerCommandExecutor.cs
    - sources/CloudWorker/CounterStrike2ProcessIdentity.cs
    - sources/CloudWorker/CounterStrike2ProcessController.cs
    - tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs

key-decisions:
  - "The RCON password is held in executor memory from the latest successful Start, Restart or already-running Start, and cleared when a stop or terminate succeeds"
  - "After a worker restart the password is gone, so CS2 reports players through A2S_INFO on the persisted game port until the next Start"
  - "A status reply whose humans or capacity exceeds 1024 is rejected as a whole, never clamped"

patterns-established:
  - "Player sources are ordered: authenticated in-game query first, A2S_INFO second, query-failed last, never a guessed number"

requirements-completed: [MET-02, MET-05]

coverage:
  - id: D1
    description: "A running CS2 server started by this worker reports its human players through the existing RCON client on the start port"
    requirement: "MET-02"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#ObservePlayers_AfterStart_ReadsHumansThroughRconStatus"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2StatusParserTests.cs#TryParse_PlayersLine_ReturnsHumansAndCapacity"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#RconClient_RequestStatus_JoinsResponsePackets"
        status: pass
    human_judgment: false
  - id: D2
    description: "Without a held password or with an unparseable reply CS2 falls back to A2S_INFO on its persisted game port; both failing report query-failed, a stopped process measuring"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#ObservePlayers_NoPasswordHeld_FallsBackToA2sOnThePersistedPort"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#ObservePlayers_RconAndA2sFail_ReportsQueryFailed"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#ObservePlayers_NoRunningProcess_ReportsMeasuringWithoutQuerying"
        status: pass
    human_judgment: false
  - id: D3
    description: "The RCON password and reply body are never persisted, logged or put into exceptions"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#Start_DoesNotPersistTheRconPasswordInWorkerState"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CounterStrike2ImplementationTests.cs#ObservePlayers_UnparseableReply_ReportsQueryFailedWithoutLeakingSecrets"
        status: pass
    human_judgment: false
  - id: D4
    description: "The live CS2 status text format (players line) and A2S_INFO on the CS2 game port match a real server"
    requirement: "MET-02"
    verification:
      - kind: other
        ref: "Deferred to the end-of-phase UAT of plan 67-20 (CS2 status format is not formally documented)"
        status: deferred
    human_judgment: true
    rationale: "The status format is undocumented; only a live server proves it"

duration: 30min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 09: Counter-Strike 2 Player Count Summary

**Counter-Strike 2 now reports its human player count from the existing RCON client's `status` response, with an A2S_INFO fallback on the persisted game port when no password is held or the reply cannot be parsed.**

## Accomplishments
- `CounterStrike2RconClient.RequestStatusAsync` authenticates like the quit path, sends `status`, and joins response packets until a 250 ms pause or a 64 KiB cap; it returns null on auth failure, timeout or socket error.
- `CounterStrike2StatusParser.TryParse` reads one source-generated, timeout-bounded players line (humans, optional bots, optional max), bounded to 0-1024; any other shape returns false.
- `CounterStrike2WorkerCommandExecutor.ObservePlayersAsync`: stopped process is measuring; with a held password RCON status is parsed; otherwise or on an unparseable reply A2S_INFO on `identity.GamePort ?? 27015` supplies humans and max players; both failing is query-failed.
- The password lives in a private nested session object (no value-bearing `ToString`), set on Start/Restart and cleared on successful stop/terminate/compensation, never written to disk.
- `CounterStrike2ProcessIdentity` gains `GamePort`, recorded by the controller from the `-port` start argument; older persisted identities read it as null.
- 34 CS2 tests (parser, executor, loopback RCON server, identity); whole CloudWorker suite 312 passing; solution builds with 0 errors.

## Task Commits
1. **Task 1 (tracer): RCON status players** - tracer verified end-to-end (28 tests)
   - `85d39246` feat(worker): observe Counter-Strike 2 players through RCON status
   - `727c2840` test(worker): cover CS2 status parsing and RCON players
2. **Task 2: A2S_INFO fallback**
   - `9dadd07f` feat(worker): fall back to A2S_INFO for Counter-Strike 2 players
   - `8f50ee67` test(worker): cover the CS2 player fallback

## Decisions Made
- Password is also held when a Start finds the server already running (redelivery after a worker restart); a wrong password only makes RCON return null and the A2S fallback takes over.
- Capacity above 1024 rejects the whole reading rather than being clamped.
- Per-chunk read pause (250 ms) is applied after the first packet; the first packet may take as long as the overall 2 s timeout.

## Deviations from Plan

**TDD ordering:** the production commit precedes the test commit in each task, as the plan lists them, because every commit must build and the tests need the production types. Tests were written together with the code and passed on first run (no separate RED commit).

**[Rule 2 - Missing critical] Port parsing helper tested directly:** `CounterStrike2ProcessController.ReadGamePort` is `internal static` so the `-port` parsing is unit-tested without launching a real process.

**Total deviations:** 1 minor. **Impact:** none on scope.

## Known Stubs

None.

## Open Item

The live CS2 status text format and the CS2 A2S_INFO behavior are unverified against a real server; plan 67-20's end-of-phase UAT confirms them. Until then an unrecognized format yields query-failed, never a guessed count.

## Self-Check: PASSED

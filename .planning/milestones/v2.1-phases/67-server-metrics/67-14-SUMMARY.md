---
phase: 67-server-metrics
plan: 14
subsystem: worker
tags: [worker, control-channel, websocket, protocol-3, metrics, heartbeat]

requires:
  - phase: 67-05
    provides: IWorkerMetricsSampler, WorkerMachineReading and IWorkerCommandExecutor.ObservePlayersAsync
  - phase: 67-08
    provides: IWorkerControlChannel, CloudWorkerChannelClient and the channel exceptions
  - phase: 67-11
    provides: API-side Wake pushes that replace the claim and probe timers
provides:
  - CloudWorkerRuntime on the control channel (protocol 3) with an independent heartbeat loop, wake-driven claims and probes, and every report over the socket
  - Registration that advertises every game the executor registry implements (ASKA registers again)
  - Metrics loop on the primary host (game-server, ordinal 0) with MetricsInterval (--metrics-seconds, UTF_WORKER_METRICS_SECONDS, default 15 s)
  - FakeControlChannel test double and a migrated plus extended runtime test suite
affects: [67-16, UAT of server metrics, worker image rollout]

plan_head_before: 776786788d86f6e46fb05b8fa5e0b764f3dc33b9
plan_head_after: 297732d4221f98069a8ec999573b8602fd4fc8d6
commits: 2

actuals:
  tokens: 25000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Three activities (heartbeat loop, command loop, metrics loop) plus channel.Completion run under one linked token; the first to finish decides the outcome and the others are cancelled and observed"
    - "Wake-or-completion wait during execution: WhenAny(executionTask, WaitForWakeAsync(linked)); the wake wait is cancelled when execution finishes first"
    - "Lossy metrics: a sample the channel cannot send is dropped, never queued or retried"

key-files:
  created:
    - tests/CloudWorker.Tests/FakeControlChannel.cs
  modified:
    - sources/CloudWorker/CloudWorkerRuntime.cs
    - sources/CloudWorker/CloudWorkerOptions.cs
    - sources/CloudWorker/Program.cs
    - tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs

key-decisions:
  - "Registration and credential rotation stay on HTTP (credential lifecycle); heartbeat, claim, report and metrics are channel-only, and the test FakeApiClient throws if the runtime ever uses HTTP for them"
  - "A due credential rotation runs before the channel starts, so the first connect never presents a credential about to be replaced; later rotations are saved first and then trigger ReconnectAsync"
  - "The first heartbeat runs inline (raced against channel.Completion) before the loops start, preserving heartbeat-before-claim ordering and bootstrap retirement after the first success"
  - "Heartbeat interval comes from the stored credential (whole seconds, minimum 1 s) as before; PollInterval is only the retry delay after a transient channel failure"
  - "The metrics loop primes the sampler once before the first tick so the first sent sample already carries a measured CPU value instead of measuring"
  - "Player capacity is sent only together with a count and only when positive and not below the count; otherwise it is reported as unknown so one inconsistent game reading cannot make the API reject the whole sample"

patterns-established:
  - "Command output size is bounded by the journal (16 KiB output, 2 KiB error message) before any terminal report is built, which keeps every channel frame far below the 256 KiB limit that makes the API close with 1009"

requirements-completed: [MET-02, MET-03, MET-05]

coverage:
  - id: D1
    description: "A new worker registers with protocol version 3 and advertises arma3, aska and cs2 from the executor registry"
    requirement: MET-02
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#NewRegistration_AdvertisesProtocolThreeAndEveryImplementedGame"
        status: pass
    human_judgment: false
  - id: D2
    description: "Heartbeats run on their own timer while a command executes, claims happen only on start, after a command and after a Wake, and an idle worker sends no claim without a Wake"
    requirement: MET-02
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#IdleWorker_SendsNoClaimUntilAWakeArrives"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#HeartbeatsContinueAtTheirOwnIntervalWhileACommandExecutes"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#RunningCommand_WithoutAWake_SendsNoProbe"
        status: pass
    human_judgment: false
  - id: D3
    description: "A Wake during execution sends a Running probe; a Superseded answer cancels, compensates and journals the supersession; journal, redelivery and lease handling are unchanged"
    requirement: MET-03
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#WakeDuringExecution_ProbesAndSupersessionCancelsAndPersistsBarrierOutcome"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#NewCommand_PersistsEveryStateBeforeReportingIt"
        status: pass
    human_judgment: false
  - id: D4
    description: "Credential rotation is saved before the channel reconnects with it, and a rejected credential ends the worker with the authentication fault"
    requirement: MET-03
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#RotationWhileRunning_IsSavedBeforeTheChannelReconnectsWithIt"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#RejectedCredential_EndsTheWorkerWithTheAuthenticationFault"
        status: pass
    human_judgment: false
  - id: D5
    description: "The primary host sends one validated sample per metrics interval with values or reasons, never zero for an unmeasured value; other hosts send none; undeliverable samples are dropped"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#UnmeasuredValues_AreSentAsReasonsAndNeverAsZero"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#NonPrimaryHostWorker_SendsNoSample"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs#SampleThatCannotBeSent_IsDroppedAndNeverResent"
        status: pass
    human_judgment: false
  - id: D6
    description: "A real worker image talking to a real API over nginx/Kestrel (research A1 end-to-end WebSocket upgrade) behaves as the fake channel tests assume"
    verification: []
    human_judgment: true
    rationale: "Only fake channels were used here; the real ClientWebSocket upgrade through the reverse proxy needs an end-to-end check in UAT"

duration: 11min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 14: Worker on the Control Channel Summary

**CloudWorkerRuntime now registers as protocol 3 for arma3, aska and cs2 and runs entirely on the control channel (independent heartbeat loop, wake-driven claims and Running probes, socket reports), and the primary host sends lossy CPU, memory and player samples every 15 s as values or reasons.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-11-22T12:23:02Z
- **Completed:** 2026-11-22T12:33:56Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Protocol 3 registration with `executorRegistry.GameIdentifiers`; the hard-coded `arma3, cs2` list is gone, so ASKA deployments can register again (Pitfall 2).
- No polling: the command loop claims at start, after a command and after a Wake only; an idle worker provably sends no further claim (D-01).
- Heartbeats on their own timer with the current stage, credential expiry check and rotation (save, then `ReconnectAsync`); a missed heartbeat never abandons a local execution (Pitfall 5).
- While a command runs, a Wake sends a Running probe; Superseded cancels, compensates and journals `command-superseded` exactly as before (Pitfall 4, T-67-23).
- Metrics loop for game-server ordinal 0: one sample per `MetricsInterval`, reasons instead of zeros, capacity only with a valid count, drop on `WorkerChannelUnavailableException`.
- Orchestrator note from 67-08 (1009 for frames over 256 KiB): handled. Every terminal report is built from the journal entry, and the journal already bounds Output to 16 KiB and ErrorMessage to 2 KiB, so no report can approach the limit. Regression tests (`LargeOutput_ReachesTheChannelBoundedByTheJournal`, `LargeFailure_ReachesTheChannelWithBoundedOutputAndErrorMessage`) lock it in. An extra runtime-side truncation was written first and removed as redundant.

## Task Commits

1. **Task 1: Tracer - protocol 3 runtime on the channel** - `cf39f2b5` (feat; includes the migrated and new tests, see deviations)
2. **Task 2: Metrics loop and MetricsInterval** - `297732d4` (feat; includes tests)

**Plan metadata:** recorded in the following docs commit.

_TDD note: no separate RED commit. The new constructor and `ProcessCommandAsync` signature mean the tests cannot compile before the production change, and AGENTS rules forbid committing a non-building RED state._

## Files Created/Modified

- `sources/CloudWorker/CloudWorkerRuntime.cs` - channel-based runtime, heartbeat, command and metrics loops
- `sources/CloudWorker/CloudWorkerOptions.cs` - `MetricsInterval` (`--metrics-seconds`, `UTF_WORKER_METRICS_SECONDS`)
- `sources/CloudWorker/Program.cs` - `CloudWorkerChannelClient` (disposed with `await using`), `WorkerMetricsSampler`, supported games from the registry
- `tests/CloudWorker.Tests/FakeControlChannel.cs` - recording channel double with wake, supersede, fault and failure injection
- `tests/CloudWorker.Tests/CloudWorkerRuntimeTests.cs` - every previous case migrated, plus channel, wake, rotation, bounding and metrics cases

## Decisions Made

See `key-decisions` in the frontmatter. In short: register/rotate stay HTTP; rotation due at start happens before the first connect; the first heartbeat is inline; heartbeat interval stays credential-driven; the sampler is primed once; capacity is sent only when consistent with the count.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Production change and tests committed together per task**
- **Found during:** Task 1 and Task 2
- **Issue:** The plan asks for separate `feat` and `test` commits, but `CloudWorker.Tests` is part of `X39.UnitedTacticalForces.slnx` and cannot compile against the new runtime constructor until its tests are migrated, so a feat-only commit would not build.
- **Fix:** One commit per task containing code and tests, with `feat(worker)` subjects.
- **Commit:** `cf39f2b5`, `297732d4`

**2. [Rule 3 - Blocking] `metricsSampler` constructor parameter unused between the two commits**
- **Found during:** Task 1
- **Issue:** The plan fixes the final constructor in Task 1, but the metrics loop belongs to Task 2, producing a CS9113 warning in the first commit only.
- **Fix:** Kept the final signature from the start to avoid rewriting every test call twice; the loop was added in Task 2.
- **Commit:** `cf39f2b5`

**3. [Rule 1 - Bug] Redundant report-text truncation removed**
- **Found during:** Task 1 (orchestrator 1009 note)
- **Issue:** A runtime-side `BoundText` was added to bound report output, but the test showed the journal already bounds Output and ErrorMessage before reporting.
- **Fix:** Removed `BoundText`; documented the existing bound in the class remarks and covered it with regression tests.

**4. [Rule 3 - Blocking] Option parsing test needed absolute ASKA paths**
- **Found during:** Task 2
- **Issue:** `CloudWorkerOptions.FromArgs` rejects the Linux default ASKA tool paths on Windows.
- **Fix:** The test supplies absolute paths through its environment resolver; production defaults are untouched.

---

**Total deviations:** 4 (3 blocking, 1 bug-in-progress)
**Impact on plan:** No scope creep; behavior matches the plan.

## Issues Encountered

None beyond the deviations above. Tests that need real heartbeat ticks use a 1 s credential interval and take a few seconds each; the whole `CloudWorker.Tests` suite runs in about 13 s (364 tests).

## Known Stubs

None.

## Threat Flags

None. No new network surface; the runtime now uses the existing channel endpoint from plan 67-08.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Worker side of the channel and metrics is complete; the real `ClientWebSocket` upgrade through nginx/Kestrel (research A1) still needs the end-to-end UAT check (coverage D6).
- Old worker images keep working over HTTP (protocol 1 and 2) because the API endpoints are unchanged.

## Self-Check: PASSED

---
*Phase: 67-server-metrics*
*Completed: 2026-11-22*

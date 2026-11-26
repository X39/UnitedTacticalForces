---
phase: 67-server-metrics
plan: 08
subsystem: worker
tags: [worker-channel, websocket, cloud-worker, reconnect, dotnet, xunit]

requires:
  - phase: 67-01
    provides: WorkerChannelFrame envelope, EWorkerChannelFrameType, EWorkerChannelOutcome
  - phase: 67-02
    provides: WorkerMetricsSample contract
  - phase: 67-03
    provides: WorkerChannelProtocol constants and the API channel endpoint the client targets
provides:
  - IWorkerControlChannel and CloudWorkerChannelClient, the self-healing outbound worker channel with correlated requests and a wake latch
  - IWorkerChannelConnector and ClientWebSocketChannelConnector (bearer header, keep-alive, 401 mapping)
  - CloudWorkerChannelOptions, WorkerChannelUnavailableException and WorkerChannelAuthenticationException
  - WorkerCommandReportMapper shared by the HTTP client and the channel client
  - Loopback test kit (LoopbackWebSocketPair, FakeWorkerChannelServer, GatedStream, ManualTimeProvider)
affects: [67-14 worker runtime switch to the channel, 67-09, 67-10]

actuals:
  tokens: 23900
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Connector abstraction (IWorkerChannelConnector) so tests hand the client a real loopback WebSocket without a web host"
    - "Session object per connection owning socket, single-writer send lock and the pending-ack table; replaced atomically on reconnect"
    - "Manual TimeProvider test double that fires timers only on Advance, used to assert backoff delays exactly"

key-files:
  created:
    - sources/CloudWorker/IWorkerControlChannel.cs
    - sources/CloudWorker/IWorkerChannelConnector.cs
    - sources/CloudWorker/ClientWebSocketChannelConnector.cs
    - sources/CloudWorker/CloudWorkerChannelClient.cs
    - sources/CloudWorker/CloudWorkerChannelOptions.cs
    - sources/CloudWorker/WorkerChannelUnavailableException.cs
    - sources/CloudWorker/WorkerChannelAuthenticationException.cs
    - sources/CloudWorker/WorkerCommandReportMapper.cs
    - tests/CloudWorker.Tests/LoopbackWebSocketPair.cs
    - tests/CloudWorker.Tests/FakeWorkerChannelServer.cs
    - tests/CloudWorker.Tests/GatedStream.cs
    - tests/CloudWorker.Tests/ManualTimeProvider.cs
    - tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs
  modified:
    - sources/CloudWorker/CloudWorkerApiClient.cs

key-decisions:
  - "Metrics requests never wait for a connection: with no open socket they throw at once, and a sample is never retried or queued; all other requests wait up to RequestTimeout"
  - "An authentication failure (401 or 1008) re-reads the credential at once; an unchanged credential faults Completion, a changed one reconnects immediately, and only one immediate retry is allowed before normal backoff applies"
  - "The connect timeout lives inside ClientWebSocketChannelConnector with a real clock, so the injected TimeProvider only governs backoff, request and send timeouts"
  - "ReconnectAsync closes the current socket normally, waits for it to end (aborting after SendTimeout) and skips the backoff; during a backoff delay it cuts the delay short instead"

patterns-established:
  - "Pending requests belong to the Session that carried them, so a drop fails exactly those requests as transient"
  - "Console diagnostics name only state and status code; exception texts from the connector are status-only"

requirements-completed: [MET-05]

duration: 25min
completed: 2026-11-22
status: complete
commits: 3
plan_head_before: 824a93a1f6eb10c511c97dd1895ed5ed585d0020
plan_head_after: 930fffd9f9634bd72a912a5f7a0719b44078900c

coverage:
  - id: D1
    description: "The worker keeps one outbound WebSocket to lifecycle-workers/{workerId}/channel?deploymentId={id} (wss for https, ws for http) with the bearer credential in the Authorization header and explicit keep-alive interval and timeout"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#BuildChannelUri_MapsSchemeAndKeepsCredentialOutOfTheUri"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Heartbeat_SendsCorrelatedFrameAndCompletesWithItsAck"
        status: pass
    human_judgment: false
  - id: D2
    description: "ClientWebSocketChannelConnector sets the Authorization header, KeepAliveInterval and KeepAliveTimeout and maps an HTTP 401 at upgrade to an authentication exception; only the loopback fake is exercised, the real ClientWebSocket upgrade against Kestrel is not covered here"
    requirement: MET-05
    verification:
      - kind: other
        ref: "grep SetRequestHeader and KeepAliveTimeout in sources/CloudWorker/ClientWebSocketChannelConnector.cs"
        status: pass
    human_judgment: true
    rationale: "Research assumption A1 (header survives nginx and Kestrel on the real upgrade) needs a real worker against a deployed API, which no unit test here proves"
  - id: D3
    description: "Heartbeat, claim, the four command reports and metrics are sent as frames with a fresh correlation id and complete only with their own ack; a Superseded report ack returns false like the HTTP 409, and acks arriving in reverse order complete the right request"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Claim_ReturnsCommandFromAckPayloadOrNull"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Report_SendsProcessBodyAndMapsSupersessionToFalse"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#ConcurrentRequests_CompleteWithTheirOwnAckWhenAcksArriveReversed"
        status: pass
    human_judgment: false
  - id: D4
    description: "A Wake push is latched so a wake before the wait is not lost, and the client never claims on its own"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Wake_IsLatchedAndConsumedOnce"
        status: pass
    human_judgment: false
  - id: D5
    description: "A dropped socket fails pending requests as unavailable, reconnects with bounded backoff (1 s doubling to 15 s) using the current credential and re-latches the wake; the backoff resets after a successful connect"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Drop_FailsPendingRequestReconnectsWithCurrentCredentialAndLatchesWake"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#ConnectFailures_BackOffUpToTheMaximum"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#SuccessfulConnection_ResetsBackoff"
        status: pass
    human_judgment: false
  - id: D6
    description: "A 401 or a 1008 close with an unchanged credential faults Completion as an authentication failure; a changed credential or ReconnectAsync after rotation reconnects with the new credential"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Unauthorized_WithUnchangedCredential_FaultsCompletion"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Unauthorized_FollowedByChangedCredential_Reconnects"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#PolicyViolationClose_WithUnchangedCredential_FaultsCompletion"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#ReconnectAsync_AfterRotationClosesNormallyAndUsesNewCredential"
        status: pass
    human_judgment: false
  - id: D7
    description: "Inbound messages above the cap close the socket, stalled sends abort the connection, metrics are sent once and never retried, and no console output contains the credential or a frame body"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#OversizedInboundMessage_ClosesSocketAndReconnects"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#StalledSend_AbortsConnectionAndFailsRequest"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#Metrics_OnDroppedConnectionThrowsAndIsNeverRetried"
        status: pass
      - kind: unit
        ref: "tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs#ConsoleOutput_NeverContainsCredentialOrFramePayload"
        status: pass
    human_judgment: false
---

# Phase 67 Plan 08: Worker Channel Client Summary

**Self-healing outbound WebSocket client for the worker with correlated acks, a latched wake, bounded 1-15 s reconnect backoff, credential-rotation reconnect and 4 MiB / 10 s / 30 s limits, proven by 21 loopback-socket tests**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-22T10:35:00Z
- **Tasks:** 2 (tracer plus resilience)
- **Files modified:** 14 (13 created, 1 modified)

## Accomplishments
- `CloudWorkerChannelClient` speaks the `WorkerChannelFrame` contract: every heartbeat, claim, report and metrics call is a request with a fresh correlation id that completes only with its own ack. A Superseded report ack returns false exactly like HTTP 409; Rejected and Unavailable acks become transient `WorkerChannelUnavailableException`.
- The connection loop reconnects with 1 s doubling to 15 s backoff (driven by `TimeProvider`), re-reads the credential before every attempt, fails pending requests on a drop, and sets the wake latch after each connect. A 401 or a 1008 close with an unchanged credential faults `Completion`; `ReconnectAsync` swaps to a rotated credential without backoff.
- Hostile or broken servers are bounded: inbound messages above `MaxInboundMessageBytes` close with `MessageTooBig`, a send that exceeds `SendTimeout` aborts the socket, and requests time out after `RequestTimeout`. Nothing writes frames or credentials to the console.
- `WorkerCommandReportMapper` is now the single report-state-to-contract mapping for both the HTTP client and the channel; existing `CloudWorkerApiClientTests` stay green.

## Task Commits

1. **Task 1 / 2 (shared prerequisite): report mapper extraction** - `397a9e8f` refactor(worker): share command report mapping
2. **Task 1: Tracer, channel client** - `039454aa` feat(worker): add the control-plane channel client
3. **Task 1 + 2: tests** - `930fffd9` test(worker): cover channel requests, acks, wake and resilience

**Plan metadata:** committed separately as `docs(67-08): complete worker channel client plan`.

_Note: no separate RED commit; a failing-test-first commit would not have compiled without stubs, which the brief forbids. The client and tests were written together and verified green, three consecutive runs of the channel suite._

## Files Created/Modified
- `sources/CloudWorker/CloudWorkerChannelClient.cs` - connection loop, session, correlated requests, wake latch, caps and timeouts
- `sources/CloudWorker/IWorkerControlChannel.cs`, `IWorkerChannelConnector.cs`, `ClientWebSocketChannelConnector.cs` - seam and production connector
- `sources/CloudWorker/CloudWorkerChannelOptions.cs`, `WorkerChannelUnavailableException.cs`, `WorkerChannelAuthenticationException.cs` - options and error types
- `sources/CloudWorker/WorkerCommandReportMapper.cs`, `CloudWorkerApiClient.cs` - shared mapping
- `tests/CloudWorker.Tests/LoopbackWebSocketPair.cs`, `FakeWorkerChannelServer.cs`, `GatedStream.cs`, `ManualTimeProvider.cs`, `CloudWorkerChannelClientTests.cs` - loopback socket test kit and 21 tests

## Decisions Made
See `key-decisions` above. The most consequential: metrics never wait for a connection and are never retried (a stale sample is worthless), while an unchanged-credential 401/1008 is fatal so a revoked worker stops hammering the API.

## Deviations from Plan

### Auto-fixed Issues

None - no bugs, blockers or missing-critical fixes arose.

### Plan-shape deviations (not rule-driven)

**1. Task 2 behavior shipped in the tracer commit** - the reconnect loop, caps and timeouts are inseparable from the connection loop that `ReconnectAsync` and the interface already require, so `feat(worker): reconnect the channel...` and `test(worker): cover channel resilience` were not separate commits; the resilience tests are in the single test commit. All Task 2 acceptance criteria are met (20 `[Fact]`/`[Theory]` lines, `MessageTooBig` present).

**2. Two extra test helpers** - `GatedStream.cs` (stalls client writes for the send-timeout test) and `ManualTimeProvider.cs` (exact backoff assertions without a new package) were added beyond the plan's file list; both are test-only.

**Total deviations:** 0 auto-fixed, 2 plan-shape notes. **Impact:** none on behavior or scope; no file outside the plan's area was touched (no `sources/Api`, `ReactApp`, `CloudWorkerRuntime.cs`, `Program.cs` or game executors).

## Known Stubs

None.

## Threat Flags

None - the client adds no new endpoint or trust boundary beyond the channel the plan's threat model covers (T-67-12, T-67-13, T-67-14 all mitigated and tested).

## Issues Encountered
None. `dotnet build X39.UnitedTacticalForces.slnx` succeeds with 0 errors and the full CloudWorker suite passes (278 tests).

## Next Phase Readiness
Plan 67-14 can swap the runtime's HTTP heartbeat, claim, report and metrics calls for `IWorkerControlChannel`: call `StartAsync` with a credential provider that reads the credential store, watch `Completion` for authentication faults, call `ReconnectAsync` after a durable rotation save, and wait on `WaitForWakeAsync` instead of the poll delay. Two points for that plan: outbound frames above 256 KiB (`MaxWorkerMessageBytes`) make the API close with 1009, so very large command `Output` should be bounded by the runtime; and the real `ClientWebSocket` upgrade through nginx/Kestrel (research A1) still needs an end-to-end check.

## Self-Check: PASSED

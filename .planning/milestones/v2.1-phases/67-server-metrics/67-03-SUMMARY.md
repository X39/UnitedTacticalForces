---
phase: 67-server-metrics
plan: 03
subsystem: api
tags: [worker-protocol, websocket-channel, hosting-contracts, dotnet, xunit]

requires:
  - phase: 67-01
    provides: WorkerChannelFrame envelope, AuthenticatedWorker, IWorkerChannelFrameRoute and the heartbeat, claim and report routes
provides:
  - GET lifecycle-workers/{workerId}/channel upgrade authenticated by bearer credential before the socket is accepted
  - IWorkerProtocolService.AuthenticateAsync (credential check and pending-credential promotion without liveness or audit side effects)
  - WorkerChannelHandler: Wake on connect, 256 KiB message cap, correlated acks, HTTP-equivalent failure outcomes
  - WorkerChannelProtocol constants (protocol version 3) and channel settings on LifecycleWorkerOptions
  - LoopbackWebSocketPair and a socket-capable WorkerChannelTestHost with a worker-end test client
affects: [67-05 worker channel client, 67-06 registry and hardening, 67-11, 67-12 metrics route, 67-13]

actuals:
  tokens: 18800
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Authenticate before AcceptWebSocketAsync; the handler re-verifies the held credential on every frame through the unchanged protocol service"
    - "Protocol exceptions map to ack outcomes: Conflict to Superseded, Validation and NotFound to Rejected, anything else to Unavailable with a fixed text, Authentication to a 1008 close"
    - "Channel tests run the real handler over a loopback TCP WebSocket pair with a test client that exposes SendAsync, ReceiveAsync, RequestAsync and ReceiveCloseAsync"

key-files:
  created:
    - sources/HostingContracts/WorkerProtocol/WorkerChannelProtocol.cs
    - sources/Api/Services/WorkerProtocol/IWorkerChannelHandler.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelHandler.cs
    - tests/Api.Tests/GameServerHosting/LoopbackWebSocketPair.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs
  modified:
    - sources/Api/Controllers/LifecycleWorkersController.cs
    - sources/Api/Services/GameServerLifecycle/LifecycleWorkerOptions.cs
    - sources/Api/Services/WorkerProtocol/IWorkerProtocolService.cs
    - sources/Api/Services/WorkerProtocol/WorkerProtocolService.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs
    - tests/Api.Tests/GameServerHosting/LifecycleWorkersControllerTests.cs
    - tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs

key-decisions:
  - "IWorkerChannelHandler is public (the plan said internal) because the public controller action takes it as a parameter; the implementation stays internal"
  - "A message that is not a frame but is a JSON object with a readable id is acked Rejected; a message without a readable id is ignored (non-JSON text cannot carry a readable id)"
  - "The action uses the literal route segment channel so the HttpGet acceptance grep holds; WorkerChannelProtocol.ChannelPathSegment stays the shared constant for the worker"
  - "Server-initiated closes use CloseOutputAsync (no wait for the peer); only the echo of a worker close uses CloseAsync"

patterns-established:
  - "Per-frame IServiceScope resolving IEnumerable<IWorkerChannelFrameRoute>, so scoped protocol services and the DbContext never outlive a frame"
  - "Frame bodies, payloads and credentials never reach a log line; entries carry worker id, frame type and outcome only"

requirements-completed: [MET-05]

duration: 25min
completed: 2026-11-22
status: complete
commits: 5
plan_head_before: 1429ff3c1f1628bc1f77d94dedda5917e7d65f3b
plan_head_after: 18797f4e42cca29386dbc47835bd111557f28a62

coverage:
  - id: D1
    description: "A registered worker upgrades with its bearer credential, receives a Wake, and heartbeats, claims and reports (received, running, succeeded) over one socket with Accepted acks echoing the request id"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#Connect_SendsAWakeFrameFirst"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#Heartbeat_IsAckedAcceptedWithTheRequestIdAndPersisted"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#Claim_AckCarriesTheClaimResponseAndNothingToDeliverIsANullPayload"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#Reports_AreAckedAcceptedAndTheCommandEndsSucceeded"
        status: pass
    human_judgment: false
  - id: D2
    description: "Unauthenticated upgrades are refused before accept: 401 for a missing or rejected credential, 400 for a non-WebSocket request; the accept uses the configured per-socket keep-alive"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/LifecycleWorkersControllerTests.cs#Channel_*"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs#Authenticate_*"
        status: pass
    human_judgment: false
  - id: D3
    description: "Failures map like HTTP: conflict to Superseded, validation and not-found to Rejected, unexpected exception to Unavailable with a fixed text, credential loss to a 1008 close without an ack"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#ReportRunningAfterCostStopSupersededTheCommand_IsAckedSuperseded"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#UnexpectedRouteFailure_IsAckedUnavailableWithoutLeakingTheExceptionText"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#RevokedCredential_ClosesTheChannelWithPolicyViolationAndNeverAcks"
        status: pass
    human_judgment: false
  - id: D4
    description: "Messages above 256 KiB close with 1009; unreadable, unknown-type and unrouted frames are acked Rejected with the connection kept open; registration accepts protocol versions 1 to 3 and rejects 0 and 4; the HTTP endpoints are unchanged"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#MessageAboveTheCap_ClosesTheChannelWithMessageTooBig"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs#Register_AcceptsProtocolVersionsOneToThree"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerProtocolServiceTests.cs#Register_RejectsProtocolVersionsOutsideOneToThree"
        status: pass
    human_judgment: false
---

# Phase 67 Plan 03: Authenticated Worker Channel over WebSocket Summary

**Worker-authenticated outbound WebSocket (`lifecycle-workers/{id}/channel`) carrying heartbeat, claim and command reports as correlated frames with Wake pushes, a 256 KiB cap and HTTP-equivalent failure acks, over the unchanged protocol service**

## Performance

- **Duration:** about 25 min (start time was not captured; estimated)
- **Completed:** 2026-11-22T09:44Z
- **Tasks:** 2 (tracer plus expansion)
- **Files:** 12 (5 created, 7 modified)

## Accomplishments
- `GET lifecycle-workers/{workerId}/channel?deploymentId=` authenticates the bearer credential through the new `AuthenticateAsync` before `AcceptWebSocketAsync`; missing bearer is 401, a non-WebSocket request 400, a rejected credential 401 without an accept (T-67-01).
- `WorkerChannelHandler` sends Wake on connect, assembles fragmented text messages up to `ChannelMaxMessageBytes` (1009 above it), routes each frame in its own DI scope and answers with an Ack echoing the request id; Wake follows an ack when the route asks for it.
- Protocol exceptions become the HTTP-equivalent outcomes: Superseded (409), Rejected (400 and 404), Unavailable with a fixed text (anything else, no exception text on the wire), and a 1008 close without an ack for credential loss (T-67-02, T-67-06).
- Protocol version 3 is accepted at registration (1 to 3); the HTTP heartbeat, claim and report endpoints are unchanged for protocol 1 and 2 images.
- Test infrastructure: `LoopbackWebSocketPair` (real TCP loopback WebSockets) and a socket-capable `WorkerChannelTestHost` with a nested `WorkerChannelTestClient`.

## Task Commits

1. **Tracer: upgrade, heartbeat, claim and reports over one socket**
   - `e1e1008a` feat(hosting): add worker channel protocol constants
   - `76f590f2` feat(api): carry worker heartbeat, claims and reports over a WebSocket
   - `45268816` test(api): cover the worker channel over a real socket
2. **Task 2: protocol version 3, message cap and failure acks**
   - `2ee93dc1` feat(api): map worker channel failures like the HTTP protocol
   - `18797f4e` test(api): prove worker channel failure parity

**Plan metadata:** committed separately as `docs(67-03): complete worker channel plan`.

## Decisions Made
- No channel code decides claim order, leases, supersession or credentials; every call re-verifies the held credential in `WorkerProtocolService`.
- The cap, the keep-alive options and the frame error mapping live on `LifecycleWorkerOptions` and the handler; rate limit, send timeout, registry and one-connection-per-worker are left to plan 67-06 as planned.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] IWorkerChannelHandler made public**
- **Found during:** Task 1 (controller action)
- **Issue:** The plan declared `IWorkerChannelHandler` internal, but the public controller action takes it as a `[FromServices]` parameter, which fails with CS0051 (inconsistent accessibility).
- **Fix:** Declared the interface public (its signature uses only public types); `WorkerChannelHandler` stays internal.
- **Files modified:** sources/Api/Services/WorkerProtocol/IWorkerChannelHandler.cs
- **Committed in:** 76f590f2

**2. [Ordering] Constants and channel options landed with the tracer**
- **Found during:** Task 1
- **Issue:** The tracer controller action and handler use `WorkerChannelProtocol` and the channel options (message cap, keep-alive); shipping the tracer with an uncapped receive buffer, even for one commit, was not acceptable.
- **Fix:** Committed `WorkerChannelProtocol` first (`e1e1008a`) and included the options and the cap in the tracer commit; Task 2 then added the failure mapping and protocol version 3 as planned.
- **Files modified:** sources/HostingContracts/WorkerProtocol/WorkerChannelProtocol.cs, sources/Api/Services/GameServerLifecycle/LifecycleWorkerOptions.cs
- **Committed in:** e1e1008a, 76f590f2

**3. [Interpretation] Unreadable frame test uses valid JSON with a bad member**
- **Found during:** Task 2
- **Issue:** The behavior line "not JSON but has a readable Id" is not satisfiable literally. The handler reads the id from raw JSON, so the covered case is a JSON object whose frame deserialization fails (bad `commandId`, unknown type string); truly malformed JSON has no readable id and is ignored, and both are tested.
- **Files modified:** tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs
- **Committed in:** 18797f4e

---

**Total deviations:** 3 (1 blocking fix, 2 ordering and interpretation notes)
**Impact on plan:** No scope change; no file outside the plan list besides the contracts constants file the plan lists for Task 2.

## TDD Notes
- Task 2 had observed RED: the new handler and registration tests were run against the Task 1 handler and failed (11 of 22 failing) before the failure mapping and version-3 check were implemented, then passed (79 of 79 in the channel, controller, protocol-service and route test classes). Commit order still follows the plan (production, then tests) because the tests depend on the types introduced by the production commits.
- Task 1 tests depend on new types (`AuthenticateAsync`, handler, controller action), so their RED state is a compile failure; no test-only RED commit was made, as a non-building commit would break bisection.

## Issues Encountered
- Commit hooks and signing worked normally; git reports LF-to-CRLF working-copy warnings only.
- The OpenAPI snapshot and Kiota client are unaffected: the controller is excluded from the API explorer.

## Known Stubs
None.

## Threat Flags
None beyond the plan's register: the new endpoint is the planned channel upgrade, authenticated before accept, rate limited by the existing worker policy and covered by the pause middleware path prefix.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 67-05 can speak the channel from the worker with `WorkerChannelProtocol.ProtocolVersion` and the `WorkerChannelFrame` contracts; plan 67-06 adds the connection registry, rate and send-timeout enforcement and targeted pushes on top of `IWorkerChannelHandler`.
- Verification: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` (1038 passed) and `dotnet build X39.UnitedTacticalForces.slnx` (0 errors) are green; no commit touches ReactApp, WebApp, CloudWorker, Database or `Program.cs`.

## Self-Check: PASSED

- Created files present (5 of 5); commits `e1e1008a`, `76f590f2`, `45268816`, `2ee93dc1`, `18797f4e` are ancestors of HEAD.
- Acceptance criteria re-run: `HttpGet("channel")` 1, `AuthenticateAsync` in the interface 1, HTTP `HttpPost("heartbeat")` 1, handler test count 14 (min 8), `is < 1 or > 3` 1, `ChannelMaxMessageBytes` in options 1, `WebSocketCloseStatus.MessageTooBig` 1, `WorkerChannelProtocol.cs` present with `ProtocolVersion = 3`.

---
*Phase: 67-server-metrics*
*Completed: 2026-11-22*

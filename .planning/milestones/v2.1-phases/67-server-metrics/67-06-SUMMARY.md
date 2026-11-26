---
phase: 67-server-metrics
plan: 06
subsystem: api
tags: [worker-protocol, websocket-channel, connection-registry, rate-limit, dotnet, xunit]

requires:
  - phase: 67-03
    provides: WorkerChannelHandler, channel options (burst, rate, send timeout) and the loopback channel test host
provides:
  - IWorkerChannelRegistry: process-local live connection per worker, push by worker id or deployment id, newest connection wins
  - WorkerChannelConnection: socket wrapper with one send lock and a send timeout that aborts a stuck socket
  - WorkerChannelHandler wired through the registry with a per-connection token bucket (1008 "frame rate exceeded")
  - WorkerChannelTestHost.Registry, configureOptions hook and AdoptSeededWorker for restart simulation
affects: [67-11 wake push, 67-12 metrics route, 67-13]

actuals:
  tokens: 21500
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "One SemaphoreSlim send lock per socket serializes acks and control-plane pushes; a send or lock wait beyond ChannelSendTimeout aborts the socket"
    - "Registration handle removes the registry entry only while it still points at its own connection, so a replaced connection can never evict its successor"
    - "Token bucket on an injected TimeProvider (optional constructor parameter defaulting to TimeProvider.System), deterministic in tests"

key-files:
  created:
    - sources/Api/Services/WorkerProtocol/IWorkerChannelConnection.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelConnection.cs
    - sources/Api/Services/WorkerProtocol/IWorkerChannelRegistry.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelRegistry.cs
  modified:
    - sources/Api/Services/WorkerProtocol/WorkerChannelHandler.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs

key-decisions:
  - "The registry is process-local and documented as a single-API-instance limitation (RESEARCH Pitfall 12); the worker's periodic claim remains the fallback"
  - "A newer connection wins and the older one is closed NormalClosure 'replaced by a newer connection' fire-and-forget, so Register stays synchronous"
  - "Registry sends never throw except for caller cancellation; a failed push is logged with ids and exception type only and drops the entry"
  - "Program.cs is untouched: TimeProvider is an optional handler constructor parameter"

patterns-established:
  - "Server-initiated closes go through the connection (under the send lock), never directly on the socket"

requirements-completed: [MET-05]

duration: 11min
completed: 2026-11-22
status: complete
commits: 5
plan_head_before: c24f4c694a226e73cc04c986e9d1b9bcc85fcd46
plan_head_after: 78e0474fd2c298382d42d0afb57e7801db8c4c4b

coverage:
  - id: D1
    description: "A control-plane push reaches the connected worker (by worker id or deployment id) without polling, and concurrent pushes never interleave with acks"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#Push_ThroughTheRegistryReachesTheConnectedWorker"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#PushToDeployment_ReachesEveryConnectionOfThatDeploymentAndNoOther"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#ConcurrentPushesAndAcks_ArriveAsCompleteFrames"
        status: pass
    human_judgment: false
  - id: D2
    description: "One connection per worker: a newer connection replaces and closes the older one, disposing the older registration keeps the newer, and a client close removes the entry"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#NewerConnection_ReplacesTheOlderOneAndDisposingTheOlderRegistrationKeepsTheNewerOne"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#ClosingTheSocket_RemovesTheRegistryEntry"
        status: pass
    human_judgment: false
  - id: D3
    description: "Frame floods close with 1008 frame rate exceeded and a stuck send aborts the connection; both remove the registry entry"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#FrameBurstAboveTheLimit_ClosesWithPolicyViolationBeforeEveryFrameIsAcked"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#SendThatDoesNotFinishWithinTheTimeout_AbortsTheConnectionAndRemovesTheRegistryEntry"
        status: pass
    human_judgment: false
  - id: D4
    description: "Credential rotation, API-restart redelivery with the same attempt, and log hygiene behave as over HTTP"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#CredentialRotation_KeepsTheOldSocketUntilTheReplacementIsPromotedThenRefusesTheOldCredential"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#CommandClaimedBeforeARestart_IsRedeliveredWithTheSameAttemptNumberAfterIt"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs#ChannelLogs_NeverContainACommandPayloadOrACredential"
        status: pass
    human_judgment: false
---

# Phase 67 Plan 06: Worker Channel Registry and Hardening Summary

**Single bounded, push-capable worker channel connection: a process-local registry with newest-wins replacement, a send-locked socket wrapper with a send timeout, and a per-connection token bucket (1008) over the unchanged protocol service**

## Performance

- **Duration:** about 11 min (2026-11-22T10:03:59Z to 10:14:41Z)
- **Tasks:** 2 (tracer plus hardening)
- **Files:** 7 (4 created, 3 modified)

## Accomplishments
- `IWorkerChannelRegistry` / `WorkerChannelRegistry`: `Register` (newest wins, older closed with NormalClosure "replaced by a newer connection"), `TrySendAsync` by worker id, `SendToDeploymentAsync` fan-out, `ConnectionCount`. Failed pushes are logged with ids and exception type only and never thrown (caller cancellation excepted).
- `WorkerChannelConnection`: one `SemaphoreSlim` send lock for acks, pushes and closes; each send (and the lock wait) is bounded by `ChannelSendTimeout`, and a timeout aborts the socket and rethrows as `OperationCanceledException`.
- `WorkerChannelHandler` registers its connection before the first Wake, sends everything through the connection, disposes the registration first in `finally`, and closes with `PolicyViolation` "frame rate exceeded" when a token bucket (burst `ChannelFrameBurst`, refill `ChannelMaxFramesPerSecond`, clock from an optional `TimeProvider`) runs dry. No credential, claim or lease logic was added to the channel code.
- Tests (29 facts and theories in `WorkerChannelHandlerTests`, 15 new): push, unknown worker, deployment fan-out, 50 concurrent pushes against 50 heartbeats, newest wins, client close, replaced-registration safety, failed-push logging, exact 40-of-60 rate cut-off with a manual clock, refill, stuck-send abort, rotation both ways, restart redelivery, and a recording-logger check.

## Task Commits

1. **Tracer: push through the registry, newest connection wins**
   - `fee5d653` feat(api): push frames to connected workers, newest connection wins
   - `6dd8e9ba` test(api): cover the worker channel registry
2. **Task 2: bound the channel, prove rotation, restart and log hygiene**
   - `401dac31` fix(api): bound worker channel frame rate and stuck sends
   - `843a3b59` test(api): prove worker channel rotation, restart and log hygiene
3. **Acceptance follow-up**
   - `78e0474f` docs(api): reference the channel send timeout setting

**Plan metadata:** committed separately as `docs(67-06): complete worker channel registry plan`.

## Decisions Made
- Process-local registry, newest connection wins, replaced connection closed fire-and-forget so `Register` stays synchronous.
- Rotation is proven as two scenarios because a replaced socket is already closed NormalClosure: (a) the old socket keeps working until the replacement is promoted by any first verification, then its next frame closes with 1008; (b) a new socket carrying the replacement credential replaces the old connection and the old credential can no longer connect.

## Deviations from Plan

### Auto-fixed Issues

**1. [Ordering] Test host change shipped in the production commit**
- **Found during:** Task 1
- **Issue:** The handler now requires `IWorkerChannelRegistry`; committing production code before the test host registers it would leave every existing channel test failing at runtime in that commit.
- **Fix:** `WorkerChannelTestHost.cs` (registry registration, `Registry` property) went into `fee5d653` with the production code so every commit is green.
- **Files modified:** tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs
- **Committed in:** fee5d653

**2. [Ordering] `TimeProvider` parameter added in Task 2**
- **Issue:** An unread primary-constructor parameter would warn (CS9113) in the Task 1 commit.
- **Fix:** The optional `TimeProvider? timeProvider = null` arrived with the token bucket in `401dac31`; the final constructor matches the plan's interface.

**3. [Rule 3 - Blocking] Handler catches every `OperationCanceledException`**
- **Issue:** A send-timeout abort surfaces as an `OperationCanceledException` that is not the request token; the old `when (cancellationToken.IsCancellationRequested)` filter would let it escape the handler.
- **Fix:** The handler catch is unfiltered; both causes end the channel.
- **Committed in:** fee5d653

**4. [Addition] Test host helpers**
- `configureOptions` hook on `CreateAsync`, `AdoptSeededWorker` for the restart test and an idempotent client `DisposeAsync` (a test disposes a client explicitly and again via `await using`).

**5. [Interpretation] Fan-out and replacement edge cases use a fake connection**
- Deployment fan-out and the failed-push logging test run against an in-memory `IWorkerChannelConnection` (one worker per deployment makes a real-socket multi-connection deployment impractical); the registry and rotation behaviors use real loopback sockets.

---

**Total deviations:** 5 (1 blocking fix, 4 ordering, addition or interpretation notes)
**Impact on plan:** None on scope; no file outside the plan list, and no edit to `Program.cs`, `ServerMetrics`, `CloudWorker`, `Database`, `WebApp` or `ReactApp`.

## TDD Notes
- Production code was written before its tests in each task (the plan's commit order is production then tests, and the new tests depend on new types, so a RED commit would not have built). The tests were not observed failing against a missing implementation; the rate, stuck-send, rotation and restart tests were instead run three times each for stability.

## Issues Encountered
- Git reports LF-to-CRLF working-copy warnings only; hooks and signing worked normally.
- The stuck-send test fills real loopback socket buffers with 2 MiB pushes (bounded to 400 attempts, 300 ms send timeout); it passed on every run here but is the one test whose runtime depends on OS socket buffer sizes.

## Known Stubs
None.

## Threat Flags
None beyond the plan's register: T-67-03 is mitigated (token bucket, send timeout abort, one connection per worker, removal on every close path) and T-67-33 is covered by the recording-logger test.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 67-11 can push Wake hints with `IWorkerChannelRegistry.TrySendAsync` / `SendToDeploymentAsync`; plans 67-12 and 67-13 can use `WorkerChannelTestHost.Registry`.
- Verification: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` (1112 passed) and `dotnet build X39.UnitedTacticalForces.slnx` (0 errors) are green.

## Self-Check: PASSED

- Created files present (4 of 4); commits `fee5d653`, `6dd8e9ba`, `401dac31`, `843a3b59`, `78e0474f` are ancestors of HEAD.
- Acceptance criteria re-run: `class WorkerChannelRegistry` 1, "replaced by a newer connection" 2, `SemaphoreSlim` in the connection 2, `frame rate exceeded` in the handler 1, `ChannelSendTimeout` in the connection 1, test count 29 (min 16).

---
*Phase: 67-server-metrics*
*Completed: 2026-11-22*

---
phase: 67-server-metrics
plan: 01
subsystem: api
tags: [worker-protocol, websocket-channel, frames, hosting-contracts, dotnet, xunit]

requires:
  - phase: 66
    provides: IWorkerProtocolService HTTP semantics (leases, redelivery, idempotent reports, cost-stop supersession)
provides:
  - WorkerChannelFrame envelope plus EWorkerChannelFrameType and EWorkerChannelOutcome in HostingContracts
  - AuthenticatedWorker identity record and WorkerChannelJson options
  - IWorkerChannelFrameRoute seam with Heartbeat, Claim and CommandReport routes over the unchanged protocol service
  - WorkerChannelTestHost shared test host and WorkerChannelRouteTests parity tests
affects: [67-03 channel handler, 67-06 registry and hardening, 67-11, 67-12 metrics route, 67-13]

actuals:
  tokens: 14300
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Thin per-frame-type route in front of IWorkerProtocolService: deserialize, validate annotations and IValidatableObject, check deployment binding, delegate with the held credential"
    - "Test host builds a ServiceProvider over HostingTestStore and routes frames through the registered IWorkerChannelFrameRoute set"

key-files:
  created:
    - sources/HostingContracts/WorkerProtocol/WorkerChannelFrame.cs
    - sources/HostingContracts/WorkerProtocol/EWorkerChannelFrameType.cs
    - sources/HostingContracts/WorkerProtocol/EWorkerChannelOutcome.cs
    - sources/Api/Services/WorkerProtocol/AuthenticatedWorker.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelJson.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelRequest.cs
    - sources/Api/Services/WorkerProtocol/WorkerChannelRouteResult.cs
    - sources/Api/Services/WorkerProtocol/IWorkerChannelFrameRoute.cs
    - sources/Api/Services/WorkerProtocol/HeartbeatFrameRoute.cs
    - sources/Api/Services/WorkerProtocol/ClaimFrameRoute.cs
    - sources/Api/Services/WorkerProtocol/CommandReportFrameRoute.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs
  modified: []

key-decisions:
  - "Routes call IWorkerProtocolService with the held credential and contain no claim, lease, supersession or credential logic; protocol exceptions propagate unchanged for the 67-03 handler to map to ack outcomes"
  - "WorkerChannelTestHost is internal (not public as the plan sketched) because it exposes the internal HostingTestStore"
  - "WorkerChannelRouteResult.Empty is built with new(Result: null) to avoid binding the null literal to the record copy constructor"

patterns-established:
  - "Frame route payload helper: object-kind check, JsonException mapped to a validation exception, Validator.TryValidateObject with validateAllProperties true, deployment equality check"
  - "Route tests assert on the shared tracked DbContext entities of the real protocol service"

requirements-completed: [MET-05]

duration: 14min
completed: 2026-11-22
status: complete
commits: 5
plan_head_before: 1188825a7b972a297ea299e37ffa6a899e0c19e3
plan_head_after: 76ec5acba2aa69576330c9980706697ad640a004

coverage:
  - id: D1
    description: "One shared frame envelope (type, correlation id, command id, outcome, bounded error, JSON payload) with stable enum values and string-name serialization"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs#Frame_SerializesTypeAndOutcomeAsNamesAndRoundTrips"
        status: pass
    human_judgment: false
  - id: D2
    description: "Heartbeat frames are validated and persisted through the unchanged HeartbeatAsync; missing, array, invalid-telemetry, foreign-deployment and wrong-credential frames are refused without changing the worker row"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs#Heartbeat_*"
        status: pass
    human_judgment: false
  - id: D3
    description: "Claim frames return the queued command, redeliver Received commands first, return null when nothing is deliverable and raise the cost-stop conflict"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs#Claim_*"
        status: pass
    human_judgment: false
  - id: D4
    description: "Received, running, succeeded and failed report frames move the command exactly as the HTTP endpoints do, reject missing command ids and foreign deployments, and surface supersession as a conflict"
    requirement: MET-05
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs#Report*"
        status: pass
    human_judgment: false
---

# Phase 67 Plan 01: Worker Channel Frame Contract and Routes Summary

**Shared worker channel frame envelope with heartbeat, claim and four command-report routes that execute through the unchanged IWorkerProtocolService, proven by 16 parity tests**

## Performance

- **Duration:** 14 min
- **Started:** 2026-11-22T09:04:29Z
- **Completed:** 2026-11-22T09:18:14Z
- **Tasks:** 2 (tracer plus expansion)
- **Files modified:** 13 (all created)

## Accomplishments
- `WorkerChannelFrame`, `EWorkerChannelFrameType` and `EWorkerChannelOutcome` define the one envelope the worker and API share, with stable integer values and string names on the wire.
- `HeartbeatFrameRoute`, `ClaimFrameRoute` and `CommandReportFrameRoute` validate payloads (annotations, `IValidatableObject`, deployment binding, T-67-04) and delegate to the protocol service with the held credential; no protocol rule is re-implemented (T-67-05).
- `WorkerChannelTestHost` and `WorkerChannelRouteTests` give plans 67-03, 67-06, 67-11, 67-12 and 67-13 a shared real-service host and 16 passing tests.

## Task Commits

1. **Task 1: Tracer - heartbeat frame through the protocol service**
   - `eef94c1f` feat(hosting): add worker channel frame contracts
   - `48ada87f` feat(api): route worker heartbeat frames through the protocol service
   - `1bb334eb` test(api): cover the worker channel heartbeat route
2. **Task 2: Claim and the four command reports as frames**
   - `4599d44b` feat(api): route worker claims and command reports as channel frames
   - `76ec5acb` test(api): prove claim and report routes match the HTTP protocol

**Plan metadata:** committed separately as `docs(67-01): complete worker channel frame routes plan`.

## Files Created/Modified
- `sources/HostingContracts/WorkerProtocol/WorkerChannelFrame.cs` - wire envelope shared by worker and API
- `sources/HostingContracts/WorkerProtocol/EWorkerChannelFrameType.cs` - frame types with stable values
- `sources/HostingContracts/WorkerProtocol/EWorkerChannelOutcome.cs` - ack outcomes
- `sources/Api/Services/WorkerProtocol/AuthenticatedWorker.cs` - authenticated attribution source
- `sources/Api/Services/WorkerProtocol/WorkerChannelJson.cs` - channel JSON options (string enums, depth 32)
- `sources/Api/Services/WorkerProtocol/WorkerChannelRequest.cs`, `WorkerChannelRouteResult.cs`, `IWorkerChannelFrameRoute.cs` - route seam
- `sources/Api/Services/WorkerProtocol/HeartbeatFrameRoute.cs`, `ClaimFrameRoute.cs`, `CommandReportFrameRoute.cs` - the three routes
- `tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs` - shared host with seeding and routing
- `tests/Api.Tests/GameServerHosting/WorkerChannelRouteTests.cs` - 16 parity tests

## Decisions Made
- Routes stay thin; every decision (ordering, lease, supersession, credential check) remains in `WorkerProtocolService`, so the socket plan is a transport swap.
- The payload read/validate/deployment-check sequence is copied per route as the plan prescribed; `CommandReportFrameRoute` uses one generic private helper for its three payload types.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test host made internal instead of public**
- **Found during:** Task 1 (WorkerChannelTestHost)
- **Issue:** The plan sketched a `public sealed class`, but its `Store` property and `CreateAsync` parameter expose the internal `HostingTestStore`, which would not compile (inconsistent accessibility).
- **Fix:** Declared `WorkerChannelTestHost` as `internal sealed`; tests live in the same assembly so later plans are unaffected.
- **Files modified:** tests/Api.Tests/GameServerHosting/WorkerChannelTestHost.cs
- **Committed in:** 1bb334eb

**2. [Rule 1 - Bug] Null literal bound to the record copy constructor**
- **Found during:** Task 1 (build of WorkerChannelRouteResult)
- **Issue:** `new(null)` in `WorkerChannelRouteResult.Empty` bound to the synthesized copy constructor and raised CS8625.
- **Fix:** Used `new(Result: null)`; amended into the route commit before any later work depended on it.
- **Files modified:** sources/Api/Services/WorkerProtocol/WorkerChannelRouteResult.cs
- **Committed in:** 48ada87f

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both necessary for compilation; no scope change.

## Issues Encountered
- Tests passed on first run, so no failing-RED evidence exists. The plan is `type: execute` (not `type: tdd`) and prescribed contracts, route and test commits in that order; the tests were written against known service behavior and each exercises a real persisted effect or a thrown protocol exception.
- Commit hooks and signing worked normally; git reports LF-to-CRLF working-copy warnings only.

## Known Stubs
None.

## Threat Flags
None. The routes add no network endpoint; the socket transport is plan 67-03.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 67-03 can build the socket handler on `IWorkerChannelFrameRoute`, mapping `WorkerProtocolValidationException`, `WorkerProtocolAuthenticationException` and `WorkerProtocolConflictException` to `EWorkerChannelOutcome` acks and honoring `WakeAfterAck`.
- Verification: `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` (983 passed) and `dotnet build X39.UnitedTacticalForces.slnx` (0 errors) both green; no commit touches ReactApp, WebApp, CloudWorker, Database, Program.cs or Controllers; the HTTP heartbeat endpoint is untouched.

## Self-Check: PASSED

- Created files all present (13 of 13); commits `eef94c1f`, `48ada87f`, `1bb334eb`, `4599d44b`, `76ec5acb` are ancestors of HEAD.
- Acceptance criteria re-run: HeartbeatAsync grep 2, validateAllProperties grep 1, ReportSucceededAsync 1, ClaimCommandAsync 2, CanExecuteCommand in ClaimFrameRoute 0, test count 16 (min 7), HTTP heartbeat action count 1.

---
*Phase: 67-server-metrics*
*Completed: 2026-11-22*

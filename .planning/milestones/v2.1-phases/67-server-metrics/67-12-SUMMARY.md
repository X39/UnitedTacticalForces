---
phase: 67-server-metrics
plan: 12
subsystem: api
tags: [metrics, worker-channel, websocket, rest, openapi]

requires:
  - phase: 67-03
    provides: authenticated worker channel, IWorkerChannelFrameRoute and AuthenticateAsync
  - phase: 67-04
    provides: IServerMetricsService ingest and current readings
  - phase: 67-07
    provides: GetHistoryAsync and ServerMetricResolutions.TryParse
provides:
  - MetricsFrameRoute, the only metrics entry point (Metrics frames on the worker channel)
  - IServerMetricsPublisher and GameServerMetricsMessage pushed to the server view audience per stored sample
  - GET game-servers/{id}/metrics and GET game-servers/{id}/metrics/history?range=1h|24h|7d
  - WorkerMetricsTransportGuardTests proving no HTTP action accepts a sample
  - Refreshed ReactApp OpenAPI snapshot and generated types
affects: [67-13, 67-16, server metrics browser UI]

plan_head_before: ef79ebd1cbf75eeee155a38736e205d8f7c723c3
plan_head_after: 4f6b5992289e0e4bd257eb49bd2f4b318aa9d331
commits: 5

actuals:
  tokens: 30000
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Channel route authenticates every frame, builds the sample source from the authenticated worker and delegates validation to the metrics service"
    - "Publisher resolves the audience per publication, fails closed with one warning and never throws"

key-files:
  created:
    - sources/Api/Services/WorkerProtocol/MetricsFrameRoute.cs
    - sources/Api/Services/ServerMetrics/IServerMetricsPublisher.cs
    - sources/Api/Services/ServerMetrics/ServerMetricsPublisher.cs
    - sources/WebSocketContracts/Hosting/GameServerMetricsMessage.cs
    - tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs
    - tests/Api.Tests/GameServerHosting/ServerMetricsEndpointTests.cs
    - tests/Api.Tests/GameServerHosting/WorkerMetricsTransportGuardTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "The route does not duplicate validation: IngestAsync already throws WorkerProtocolValidationException for invalid, foreign-deployment, future and stale samples, which the channel handler maps to Rejected"
  - "The publisher resolves the audience before evaluating the readings, so an unviewed server costs no readings query"
  - "The history route returns 400 for a bad range before any authorization lookup; viewers of other servers still get 404 for a valid range"

patterns-established:
  - "Transport guard by reflection: no controller action parameter is or wraps WorkerMetricsSample, and exactly two GET routes mention metrics"

requirements-completed: [MET-01, MET-04, MET-05]

coverage:
  - id: D1
    description: "A primary worker's Metrics frame is authenticated, stored (snapshot and three buckets) and pushed to the server's viewers only, with the control plane's readings and StaleAfterSeconds 45"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#PrimaryFrame_IsStoredAndPushedToTheViewersOnly"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#GameDoesNotReportPlayers_IsPushedAsAReasonNeverZero"
        status: pass
    human_judgment: false
  - id: D2
    description: "Headless-client frames are acknowledged and discarded; invalid, foreign-deployment, future-sampled and payload-less frames are rejected; a revoked credential closes the channel; an audience failure never fails the ack"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#HeadlessClientFrame_IsAcknowledgedAndDiscarded"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#FrameForAnotherDeployment_IsRejectedAndStoresNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#FrameAfterCredentialRevocation_ClosesTheChannelWithPolicyViolation"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerChannelMetricsTests.cs#FailingAudienceRead_SendsNothingAndStillAcknowledges"
        status: pass
    human_judgment: false
  - id: D3
    description: "Viewers read current readings and tier history over REST; others and unknown servers get 404; any range other than 1h, 24h, 7d gets 400"
    requirement: "MET-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsEndpointTests.cs#Viewer_ReceivesTheDecidedCurrentReadings"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsEndpointTests.cs#History_ReadsTheTierOfTheRange"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/ServerMetricsEndpointTests.cs#History_RejectsAnyOtherRange"
        status: pass
    human_judgment: false
  - id: D4
    description: "No HTTP action accepts a metrics sample, and the OpenAPI snapshot and generated types contain both read endpoints"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/WorkerMetricsTransportGuardTests.cs#NoControllerActionAcceptsAMetricsSample"
        status: pass
      - kind: command
        ref: "dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <tmp> && cmp <tmp> sources/ReactApp/openapi/utf-api.json; npm --prefix sources/ReactApp run contracts:check"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 12: Metrics Over the Worker Channel and REST Summary

**Primary-worker Metrics frames are authenticated, stored and pushed to the server's viewers, with two REST read endpoints for current readings and tier history, a reflection guard that no HTTP action accepts a sample, and a refreshed ReactApp contract.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-22T11:40Z
- **Tasks:** 2 (tracer plus endpoints)
- **Files:** 10 (7 created, 3 modified)

## Accomplishments
- `MetricsFrameRoute` (`FrameTypes = [Metrics]`) deserializes the sample, re-authenticates the held credential per frame, builds `ServerMetricSampleSource` only from the authenticated worker, ingests, and publishes when the sample was stored. Non-primary samples are acknowledged and discarded.
- `ServerMetricsPublisher` sends one flat `GameServerMetricsMessage` per stored sample to `FleetChangeStreamPaths.ForUser` of every viewer; a failed audience read logs one warning and sends nothing, and the publisher never throws.
- `GET game-servers/{id}/metrics` and `GET game-servers/{id}/metrics/history?range=` follow the accrued-cost endpoint style with `Server.View`, 404 mapping through the existing exception mapper and 400 for an unknown range.
- The OpenAPI snapshot and `src/generated/api.ts` were regenerated (665 insertions, no deletions); the legacy Kiota client was not regenerated because the change is additive.

## Task Commits

1. **Task 1: Tracer** (verified end-to-end: 68 tests across channel metrics, ingest and handler classes)
   - `1c01abfb` feat(api): ingest worker metrics frames and push them to viewers
   - `056b76d1` test(api): cover metrics over the worker channel
2. **Task 2: REST, guard and contract**
   - `9e282fa5` feat(api): expose server metrics and history endpoints
   - `9b045d23` test(api): cover metrics endpoints and the transport guard
   - `4f6b5992` build(webapp): refresh the API contract for server metrics

## Decisions Made
- Validation stays in `IServerMetricsService.IngestAsync`; the route only maps unreadable payloads to `WorkerProtocolValidationException`.
- The publisher resolves the audience before evaluating the readings.

## Deviations from Plan

None - plan executed exactly as written.

Note on TDD: the new route, publisher and endpoint types did not exist before the tests, so a compile-failing RED commit was not possible without breaking the build. Implementation and tests were committed in the plan's order (production, then tests); the tests passed on the first run, so no RED/GREEN iteration was needed.

## Issues Encountered
None. Plan 67-11 had already completed, so `WorkerChannelTestHost`, `Program.cs` and the heartbeat route were left untouched as required; the host was configured only through its hook.

## Known Stubs
None.

## Threat Flags
None beyond the plan register: T-67-17 (per-frame authentication, attribution from the worker, foreign deployment rejected, non-primary discarded), T-67-18 (audience resolved per publish, fail closed; REST `Server.View` plus service-level 404) and T-67-19 (reflection guard) are mitigated.

## Next Phase Readiness
Ready for the browser plan 67-16 (generated types contain both endpoints; frames match `GameServerMetricsMessage`) and the worker sender plan.

## Self-Check: PASSED

- All 7 created files exist; commits `1c01abfb`, `056b76d1`, `9e282fa5`, `9b045d23` and `4f6b5992` are ancestors of HEAD (`git rev-list --count` over the plan range is 5).
- Acceptance: `AuthenticateAsync` in the route 2, `FleetChangeStreamPaths.ForUser` in the publisher 2, channel metrics test cases 9, `metrics/history` in the controller 1, `"{gameServerId:guid}/metrics"` 1, OpenAPI metrics paths 2.
- `dotnet test --filter FullyQualifiedName~GameServerHosting`: 1167 passed, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors; OpenAPI export equals the snapshot and `contracts:check` is clean; no commit touches `sources/WebApp`, `sources/CloudWorker`, `Program.cs`, `HeartbeatFrameRoute.cs` or `WorkerProtocolService.cs`.

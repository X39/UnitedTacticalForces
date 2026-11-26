---
phase: 66-accrued-cost-cost-alerts
plan: 05
subsystem: api
tags: [cost-alerts, websocket, update-stream, authorization, audience, dotnet]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 GetCostOwnerViewAudienceAsync and the accrued cost calculation
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-03 CostAlert and CostAlertDismissal entities, evaluator result records and CostAlertWorker
provides:
  - "GET cost-alerts, POST cost-alerts/{id}/dismissal and POST cost-alerts/dismissals: per-viewer list (newest first, capped at 50) and idempotent per-viewer dismissal"
  - "IHostingAuthorizationService.GetViewableCostOwnerIdsAsync: the set form of CanViewCostOwnerAsync (null means every owner)"
  - "UpdateStreamService per-socket send lock: several producers can write to one user path without dropping the subscriber"
  - "GameServerAccruedCostMessage, CostAlertRaisedMessage, CostAlertDismissedMessage wire frames (flat scalars, DateTimeOffset)"
  - "ICostAlertPublisher/CostAlertPublisher: value, raised and dismissed frames to fail-closed audiences; CostAlertWorker publishes after every evaluation pass"
affects: [66-06, 66-07, 66-08, 66-09, cost-alert-ui, accrued-cost-strip]

plan_head_before: 8309b8e66d823a45329b6fb077f87080faca53fa
plan_head_after: 59df7cd2007741ae051d4323d56207fde15fb88b

actuals:
  tokens: 36000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Every cost frame is addressed to FleetChangeStreamPaths.ForUser(userId) of one recipient; there is no broadcast path"
    - "Audience read once per batch; a failed read drops the batch with one warning (fail closed, like the fleet notifier)"
    - "Alert existence and visibility are indistinguishable to the caller: invisible or unknown ids are HostingNotFoundException (404)"
    - "Per-socket SemaphoreSlim inside the singleton update stream; locks are deliberately not disposed on removal"

key-files:
  created:
    - sources/Api/DTO/CostAlertDto.cs
    - sources/Api/DTO/Payloads/CostAlertDismissAllPayload.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertService.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertService.cs
    - sources/Api/Controllers/CostAlertsController.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertPublisher.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs
    - sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs
    - sources/WebSocketContracts/Hosting/CostAlertRaisedMessage.cs
    - sources/WebSocketContracts/Hosting/CostAlertDismissedMessage.cs
    - tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs
    - tests/Api.Tests/GameServerHosting/UpdateStreamServiceSendTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs
  modified:
    - sources/Api/Constants.cs
    - sources/Api/Services/GameServerLifecycle/IHostingAuthorizationService.cs
    - sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs
    - sources/Api/Services/UpdateStreamService/UpdateStreamService.cs
    - sources/Api/HostedServices/CostAlertWorker.cs
    - sources/Api/Services/GameServerLifecycle/DeploymentAccrual.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertEvaluator.cs
    - sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs
    - tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs

key-decisions:
  - "Dismissal is per viewer (research open question 6); a unique-index DbUpdateException is treated as already dismissed only when every requested dismissal now exists, otherwise it is rethrown"
  - "Value frames are sent once per worker pass for costing sessions only; the final transition reaches the client through the existing id-only fleet signal"
  - "Dismissed frames are published only for dismissals this call stored, so a repeated dismissal pushes nothing"
  - "The REST list is capped at 50 and the dismiss-all endpoint returns 204 even when nothing was visible"
  - "Users without a server-section claim get no live push; the update stream subscribe gate is not widened"

patterns-established:
  - "GetViewableCostOwnerIdsAsync, CanViewCostOwnerAsync and GetCostOwnerViewAudienceAsync express one rule; a drift test compares the set form with the per-owner check for six claim classes"

requirements-completed: [CST-03, CST-04]

coverage:
  - id: D1
    description: "The viewer lists undismissed alerts of every owner they may view, newest first, capped at 50, with server name, threshold, accrued amount, currency and time; a server-only viewer and another owner's alerts are hidden, ended sessions stay listed"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#OwnersUser_ListsAlertsNewestFirstWithFullDetails"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#ListVisibility_FollowsCostOwnerViewRule"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#List_ReturnsAtMostFiftyNewestAlerts"
        status: pass
    human_judgment: false
  - id: D2
    description: "Dismissal is per viewer and idempotent; an alert the viewer may not see, or an unknown id, returns 404"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#Dismiss_IsPerViewerAndIdempotent"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#Dismiss_InvisibleOrUnknownAlert_ThrowsNotFound"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#DismissAllForServer_DismissesVisibleAlertsOfThatServerForCallerOnly"
        status: pass
    human_judgment: false
  - id: D3
    description: "GetViewableCostOwnerIdsAsync agrees with CanViewCostOwnerAsync for hosting admin, empty fin, empty fin:view-all, co:view, own owner and unrelated claims"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#ViewableOwnerIds_AgreeWithCanViewCostOwnerForEveryClaimClass"
        status: pass
    human_judgment: false
  - id: D4
    description: "The update stream serializes sends per socket: 20 concurrent sends all arrive and the subscriber stays registered, failing sockets are still removed, and a blocked socket does not delay another"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamServiceSendTests.cs#ConcurrentSends_ToOneSocket_AllArriveAndKeepSubscriber"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamServiceSendTests.cs#SocketWhoseSendFails_IsRemoved"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamServiceSendTests.cs#BlockedSocket_DoesNotDelayOtherSocket"
        status: pass
    human_judgment: false
  - id: D5
    description: "Accrued value frames reach only users in both the server and the cost-owner audience; raised frames reach the whole owner audience including finance viewers without server access; resolved sessions send nothing"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#Accruals_GoToServerAndOwnerViewersOnly"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#RaisedAlert_GoesToOwnerAudienceIncludingFinanceViewerWithoutServerAccess"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#ResolvedAccrual_SendsNothing"
        status: pass
    human_judgment: false
  - id: D6
    description: "A failed audience read sends nothing and logs one warning; a failing send does not stop other recipients; dismissed frames go only to the caller's path and only for newly stored dismissals"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#AudienceFailure_SendsNothing_AndLogsOneWarning"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#FailingSend_DoesNotStopOtherRecipients"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs#Dismiss_PublishesOneFrameToCallerOnly_AndNothingWhenAlreadyDismissed"
        status: pass
    human_judgment: false
  - id: D7
    description: "Through the real update stream the accrued frame carries an ISO-8601 asOf with offset, a numeric amount and the fully qualified wire type"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs#AccruedFrame_SerializesWithIsoTimeNumericAmountAndWireType"
        status: pass
    human_judgment: false
  - id: D8
    description: "The worker publishes in a live process against real PostgreSQL and a real browser socket (30 s cadence, audience resolution over a seeded database); no automated test drives CostAlertWorker itself"
    requirement: "CST-03"
    verification: []
    human_judgment: true

duration: about 40min
completed: 2026-11-21
---

# Phase 66 Plan 05: Cost alert API and live cost frames Summary

**Viewers of a cost owner can now list and dismiss its cost alerts per viewer through three endpoints, and accrued values, new alerts and dismissals are pushed over the update stream to exactly the users who may see the cost owner, behind a new per-socket send lock.**

## Performance

- **Duration:** about 40 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 13 created, 9 modified

## Accomplishments

- Tracer slice: `GetViewableCostOwnerIdsAsync` as the set form of the cost-owner view rule, `CostAlertService` (list capped at 50, idempotent per-viewer dismissal, 404 for invisible ids), `CostAlertsController`, 10 tests.
- `UpdateStreamService` serializes sends per socket with a `SemaphoreSlim`; the fleet notifier and the cost publisher can now share a user path.
- Three flat WebSocket frames, `CostAlertPublisher` (fail-closed intersection and owner audiences), and `CostAlertWorker` publishing after each pass; `CostAlertService` pushes dismissal frames to the dismissing user's own path.

## Task Commits

1. **Task 1 (tracer): list and dismiss cost alerts per viewer** - `a08ab907` (feat, api)
2. **Task 2: serialize update stream sends per socket** - `ab084d8e` (fix, api)
3. **Task 3: push accrued cost and cost alert frames** - `59df7cd2` (feat, api)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostAlertServiceTests|FullyQualifiedName~FleetAudienceSourceTests"`: 15 passed.
- `... UpdateStreamServiceSendTests|UpdateStreamControllerFleetTests|FleetChangeNotifierTests`: 55 passed.
- `... CostAlertPublisherTests|CostAlertServiceTests|CostAlertEvaluatorTests`: 36 passed.
- `dotnet test ... --filter "FullyQualifiedName~GameServerHosting"`: 864 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- No commit of this plan touches `sources/ReactApp`, `sources/WebApp` or `sources/Database`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `DeploymentAccrual` lacked a currency for the value frame**
- **Found during:** Task 3
- **Issue:** `GameServerAccruedCostMessage.Currency` is required, but the plan 66-03 record `DeploymentAccrual` carried no currency and the publisher has no database access by the planned constructor signature.
- **Fix:** Added a `Currency` member to `DeploymentAccrual` and had `CostAlertEvaluator` load `CostOwner` with the candidate deployments and pass `CostOwner.Currency` (fallback `EUR`, as the alert row already does).
- **Files modified:** `DeploymentAccrual.cs`, `CostAlertEvaluator.cs` (both outside this plan's file list, both from plan 66-03)
- **Commit:** `59df7cd2`

**2. [Rule 2 - Missing critical] Unique-index handling does not hide other save failures**
- **Found during:** Task 1
- **Issue:** The plan treats a unique-index `DbUpdateException` as "already dismissed". Swallowing every `DbUpdateException` would hide real failures such as a foreign key violation.
- **Fix:** The exception is swallowed only when every requested dismissal now exists for the user; otherwise it is rethrown. C# does not allow `await` inside a `catch` filter, so the check runs inside the catch block after the added entities are detached.
- **Files modified:** `CostAlertService.cs`
- **Commit:** `a08ab907`

**Total deviations:** 2 auto-fixed (1 Rule 3, 1 Rule 2). No architectural change beyond the update-stream send lock the plan itself justifies.

## Notes for Dependent Plans

- The OpenAPI snapshot and generated clients were not regenerated here; no OpenAPI snapshot or generated client for the React app is tracked under `sources/Api`, and the plan lists none. The new endpoints (`GET cost-alerts`, `POST cost-alerts/{costAlertId}/dismissal`, `POST cost-alerts/dismissals`) and the three `WebSocketContracts.Hosting` messages must be picked up by the plan that builds the client (66-08). Legacy `sources/WebApp` was not touched.
- Wire frame property names are camelCase (`serverId`, `deploymentId`, `amount`, `currency`, `asOf`, `isFinal`, `unavailableReason`; `alertId`, `serverName`, `costOwnerId`, `thresholdAmount`, `accruedAmount`, `createdAt`). The stream serializer preserves references, so objects also carry `$id`.
- `ICostAlertPublisher`, `CostAlertPublisher` and `CostAlertService` are `internal`; the test assembly sees them through the existing internals access.
- The first alert of a pass is pushed after the evaluation transaction has committed and is best effort. A client that was offline reads it from `GET cost-alerts`.

## Known Stubs

None.

## Threat Flags

None. The surfaces added are those of the plan's threat model: T-66-14 (intersection audiences, fail closed, server viewer without owner visibility tested), T-66-15 (visibility check plus 404 for unknown ids, tested), T-66-16 (per-socket lock, 20-way concurrency test), T-66-17 (owner filter from `GetViewableCostOwnerIdsAsync`, drift-tested).

## Issues Encountered

None beyond the deviations above.

## Self-Check: PASSED

- All 13 created files and 9 modified files exist (verified by the successful build and test run).
- Commits `a08ab907`, `ab084d8e`, `59df7cd2` are ancestors of HEAD.

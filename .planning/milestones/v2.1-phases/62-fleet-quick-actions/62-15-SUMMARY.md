---
phase: 62-fleet-quick-actions
plan: 15
subsystem: api
tags: [websocket, fleet, authorization, audience, gap-closure, fail-closed]
status: complete

requires:
  - phase: 62-fleet-quick-actions
    provides: per-user fleet registration via FleetChangeStreamPaths.ForUser (62-13) and IFleetAudienceSource / GetServerViewAudienceAsync (62-14)
provides:
  - FleetChangeNotifier per-recipient delivery from a publish-time audience, one audience read per coalesced batch, failing closed
  - FleetChangeStreamPaths with ForUser as the only accessible path builder; the shared prefix constant is private
  - End-to-end claim-scoped delivery proof (tracer, claim matrix, revoke, production DI graph)
affects: [62-16]

actuals:
  tokens: 18000
  tasks: 2
  commits: 2
plan_head_before: 158a72349b5dc89935785ceaa19203575dd350a2
plan_head_after: df0b307d2e297193e3aa6789df6e25e987073f1e

tech-stack:
  added: []
  patterns:
    - "Publish-time audience fan-out: resolve viewers once per coalesced batch, then send serially on one recipient path per user"
    - "Fail closed on audience failure: drop the batch with one warning, never broadcast"
    - "Real-layer test harness that breaks the store -> interceptor -> notifier -> audience -> connection ordering with a lazily read connection variable"

key-files:
  created: []
  modified:
    - sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs
    - sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs
    - tests/Api.Tests/GameServerHosting/FleetChangeNotifierTests.cs
    - tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs
    - tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs

key-decisions:
  - "62-15: the notifier reads IFleetAudienceSource once per drained batch with the stopping token and sends each GameServerFleetChangedMessage to FleetChangeStreamPaths.ForUser(userId); a missing audience entry means nobody"
  - "62-15: an audience failure drops the batch with exactly one warning (server count plus exception) and the loop continues; a forced-shutdown cancellation drops the batch silently; there is no broadcast fallback"
  - "62-15: the shared fleet prefix is a private constant of FleetChangeStreamPaths, so no code outside the class can target the bare game-servers/fleet prefix"

patterns-established:
  - "Notifier send-site guard: FleetChangeNotifier.cs holds exactly one update-stream send call and no comment spells its name"

requirements-completed: [FLT-04]

coverage:
  - deliverable: "A committed lifecycle write reaches only the sockets of users who may view the server, through the real interceptor, notifier, audience source, authorization service and update stream service"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#CommittedLifecycleJobInsert_ReachesOnlySocketOfUserWhoMayViewTheServer"
        status: pass
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#CommittedLifecycleJobInsert_ReachesExactlyTheUsersTheViewRuleAllows"
        status: pass
  - deliverable: "Revoked claims stop delivery with the next signal (publish-time audience)"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#RevokedViewClaim_StopsDeliveryWithTheNextSignal"
        status: pass
  - deliverable: "Production registration graph resolves without a cycle and delivers through a factory-created context"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#ProductionRegistrationGraph_ResolvesWithoutCycle_AndDeliversThroughFactoryContext"
        status: pass
  - deliverable: "Per-recipient fan-out, empty/missing audience, one read per batch, fail-closed audience, isolated recipient failure, and the migrated 62-05 guarantees"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetChangeNotifierTests.cs"
        status: pass
  - deliverable: "No send can target the bare fleet prefix; UpdateStreamService, WebSocketContracts, ReactApp and WebApp untouched"
    human_judgment: false
    verification:
      - kind: command
        ref: "grep -rnE 'FleetChangeStreamPaths\\.Fleet\\b' sources tests --include=*.cs | wc -l prints 0; grep -c SendUpdateAsync FleetChangeNotifier.cs prints 1; git diff --quiet 7bec477c -- sources/WebSocketContracts sources/Api/Services/UpdateStreamService sources/ReactApp sources/WebApp exits 0"
        status: pass

duration: 11 min
completed: 2026-11-19
---

# Phase 62 Plan 15: Claim-Scoped Fleet Delivery Summary

**The fleet notifier now looks up, once per coalesced batch and at publish time, which users may view each changed server. It then sends each `GameServerFleetChangedMessage` only on those users' own `game-servers/fleet/{userId}` paths. If the lookup fails, nothing is sent for that batch, and code outside the path class can no longer address the shared prefix. This closes gap G-62-2.**

## Performance

- **Duration:** about 11 min
- **Started:** 2026-11-19T12:23:31Z
- **Completed:** 2026-11-19T12:34Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- `FleetChangeNotifier` takes `IFleetAudienceSource` (constructor order: update stream, audience source, logger). The channel, single reader loop, coalesce window, `Notify`, `StartAsync` and `StopAsync` are unchanged. A new `DeliverBatchAsync` calls `GetViewersAsync` once per batch with the stopping token. For each server and each listed user it then sends to `ForUser(userId)` serially on the loop.
  - If the audience lookup throws, the batch is dropped and one warning is logged. A cancellation caused by a forced shutdown drops the batch silently.
  - If a send to one recipient throws, one warning names both the server and the user, and the loop moves on to the next recipient.
  - Every member of the file is now documented, including the private ones (WR-06 for this file).
- `FleetChangeStreamPaths.Fleet` is now the private `FleetRequestPath`. `ForUser` is the only member left. Its remarks explain why no shared publish target exists: the update stream matches paths by raw prefix.
- The placement remarks of `IFleetChangeNotifier` now list everything the signal touches. The update stream service and the WebSocket contracts are consumed unchanged.
- The notifier tests use a new `FakeAudienceSource` double. It supports configured entries, omitted entries, call counting, recorded requests and throwing on the first N calls. Five new tests bring the file to 12 facts.
- The tracer tests now run on a real-layer harness with a real `FleetAudienceSource` over a scoped `HostingAuthorizationService` on the store connection. It has 5 facts:
  - the tracer itself
  - a 9-user claim matrix
  - claim revocation
  - the production DI graph with `ValidateScopes` and `ValidateOnBuild`
  - the existing subscription gate test

## Task Commits

1. **Task 1: Tracer - a committed lifecycle write reaches only the socket of a user who may view the server** - `0be858f3` (fix)
2. **Task 2: Rule matrix end to end, revocation, fail-closed audience, fan-out and the production graph** - `df0b307d` (test)

## Files Created/Modified

- `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs` - per-recipient delivery from a publish-time audience, fail closed, fully documented
- `sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs` - prefix constant made private; remarks rewritten
- `sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs` - placement remarks rewritten
- `tests/Api.Tests/GameServerHosting/FleetChangeNotifierTests.cs` - audience double, migrated 62-05 tests, 5 new tests
- `tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs` - real-layer harness, migrated tracer, matrix, revoke and production-graph tests
- `tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs` - one assertion no longer names the removed public constant

## Decisions Made

- Each server's viewer list is passed through `Distinct()` before sending. The audience source already returns distinct sets; this guard keeps a duplicate entry from becoming a second frame.
- An empty batch, which cannot happen in practice, returns before the audience lookup, so no lookup is wasted.
- The tracer harness reads the SQLite connection through a variable that is assigned after the store exists, as the plan's verified facts prescribe. The coalesce window is 25 ms.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Migrated one assertion in UpdateStreamControllerFleetTests**
- **Found during:** Task 1
- **Issue:** `UpdateStreamControllerFleetTests.ForUser_AppendsLowerCaseDFormatUserIdToFleetRequestPath` named `FleetChangeStreamPaths.Fleet`. Once the constant became private, the test no longer compiled, and the plan's grep requires zero uses across `tests`.
- **Fix:** The assertion now compares against the literal `"game-servers/fleet/" + userId.ToString("D")`. The intent of the assertion is unchanged.
- **Files modified:** tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs
- **Commit:** 0be858f3

**2. [Process] TDD task committed as one test commit**
- Task 2 is `tdd="true"`, but it adds tests only, against behavior already built in Task 1, and the plan prescribes one `test(api): ...` commit. All new tests passed on the first green run, and no production defect showed up.

**Total deviations:** 1 auto-fixed (blocking compile fix) and 1 process note. **Impact:** none on behavior or scope.

## Issues Encountered

- `IHostedService` needed an explicit `using Microsoft.Extensions.Hosting;` in the tracer tests for the production-graph test. Fixed before commit.
- One line in `FleetSignalTracerTests.cs`, the pinned-frame literal, is still 122 characters long. It predates this plan and was left unchanged so the literal stays byte-identical to the React tracer.

## Known Stubs

None.

## Verification

- `dotnet build sources/Api/Api.csproj`: 0 errors, and no warnings in the changed files
- `dotnet test --filter "FullyQualifiedName~Fleet"`: 97 passed (after Task 1)
- `dotnet test --filter "FullyQualifiedName~FleetChangeNotifier|FullyQualifiedName~FleetSignalTracer"`: 17 passed, repeated 3 times with no flakes
- `dotnet test --filter "FullyQualifiedName~GameServerHosting"`: 641 passed
- Acceptance greps:
  - `FleetChangeStreamPaths.Fleet` uses = 0
  - `ForUser` in the notifier = 2
  - `IFleetAudienceSource` in the notifier = 3
  - `SendUpdateAsync` in the notifier = 1
  - "one message per distinct server" = 0
  - Fact count in the notifier tests = 12; in the tracer tests = 5
  - `ValidateOnBuild` in the tracer tests = 1
  - The guard over WebSocketContracts, UpdateStreamService, ReactApp and WebApp since 7bec477c exits 0

## Next Phase Readiness

Ready for 62-16 (docs): write D-17 into 62-CONTEXT.md, record the IN-10 decision that claim and role changes emit no signal, and update the T-62-14 disposition to T-62-33.

## Self-Check: PASSED

- All six modified files exist on disk
- Commits 0be858f3 and df0b307d are ancestors of HEAD

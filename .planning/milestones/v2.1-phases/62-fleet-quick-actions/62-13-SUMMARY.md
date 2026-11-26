---
phase: 62-fleet-quick-actions
plan: 13
subsystem: api
tags: [websocket, update-stream, authorization, fleet, gap-closure]

requires:
  - phase: 62-fleet-quick-actions
    provides: fleet subscription gate (62-01) and the fleet change notifier (62-04)
provides:
  - FleetChangeStreamPaths.ForUser, the only string a fleet socket is registered on
  - UpdateStreamController fleet branch that registers game-servers/fleet/{userId} from the authenticated principal
  - Gate tests for per-user registration, cross-user prefix isolation and path spoofing
affects: [62-14, 62-15, 62-16]

actuals:
  tokens: 5400
  tasks: 2
  commits: 2
plan_head_before: 7249e81caa23e2767f48f8049d8e673a76883347
plan_head_after: a644329968035cffe01269369c95420b1538c966

tech-stack:
  added: []
  patterns:
    - "Per-recipient update-stream addressing: one fixed-length path per user, so raw prefix matching cannot cross users"

key-files:
  created: []
  modified:
    - sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs
    - sources/Api/Controllers/UpdateStreamController.cs
    - tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs
    - tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs

key-decisions:
  - "62-13: fleet sockets register on game-servers/fleet/{userId} (D format, lower case, 55 characters) built only from the authenticated user id; clients still request game-servers/fleet and the wire frame is unchanged"
  - "62-13: the shared Fleet constant keeps its value and stays the publish target until plan 62-15; it is a prefix of every per-user path, so the existing publisher still reaches every fleet socket in the meantime"

patterns-established:
  - "Subscription gate returns a server-built path; no part of the client-supplied path reaches the registered string"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "ForUser path builder returns game-servers/fleet/ plus the lower-case D-format user id, fixed 55 characters"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#ForUser_AppendsLowerCaseDFormatUserIdToFleetRequestPath"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#ForUser_HasFixedLengthPrefixAndLowerCase"
        status: pass
    human_judgment: false
  - id: D2
    description: "Fleet subscriptions register only the authenticated user's path; the coarse claim gate and the event branch are unchanged"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#AllowedClaim_ResolvesFleetPathToAuthenticatedUserPath"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#DeniedClaim_DoesNotResolveFleetPath"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#Subscribe_RegistersOnlyAuthenticatedUserFleetPath"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#EventSubscription_BehavesAsBefore"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#FleetSubscription_IsGatedByServerSectionClaims"
        status: pass
    human_judgment: false
  - id: D3
    description: "Registered paths are isolated between users and cannot be spoofed or widened by the client (T-62-27, T-62-28)"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#Subscribe_TwoUsersRegisterDistinctNonPrefixPaths"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#SpoofedFleetPath_DoesNotResolveOrRegister"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#FleetPathMatrix_ResolvesOnlyExactTwoSegmentPath"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs#Subscribe_RegisteredPathIgnoresRequestSpelling"
        status: pass
    human_judgment: false

duration: 4min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 13: Per-user fleet stream registration Summary

**Fleet WebSockets now register on `game-servers/fleet/{userId}`, built only from the authenticated user id. A client cannot pick, widen or spoof that path. This is the subscribe half of gap G-62-2.**

## Performance

- **Duration:** about 4 min
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- `FleetChangeStreamPaths.ForUser(Guid)` returns the fleet request path, a slash and the D-format (lower-case) user id. The result is always 55 characters, so no user's path is a prefix of another's. The remarks of the `Fleet` constant now call it the path clients request, not a place to register.
- `UpdateStreamController.ResolveSubscriptionPathAsync` passes the `userId` argument, which `SubscribeAsync` takes from the authenticated principal, to `ForUser` and returns the result. The two-segment requirement still applies, so any request with a user id or other suffix resolves to null. `CanAccessServerSection` and the event branch did not change. The remarks now state that the section gate only decides who may open the socket, and that which servers' signals reach it is decided at publish time.
- Migrated the three gate assertions plus the one tracer-gate assertion. Two tests that had "Canonical" in their names were renamed.
- Added coverage: the builder's format, length, prefix and casing; two users getting distinct, non-prefix registrations; an 8-case spoof matrix covering the caller's own id, another user's id, `..` and `%2E`, each with and without a trailing slash; and identical registration across four request spellings.

## Task Commits

1. **Task 1: Per-user path builder and user-addressed fleet registration** - `282a4df0` (fix)
2. **Task 2: Prove the registered path is user-derived, isolated between users and unspoofable** - `a6443299` (test)

## Files Created/Modified

- `sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs` - `ForUser` builder; rewritten remarks for `Fleet`
- `sources/Api/Controllers/UpdateStreamController.cs` - fleet branch returns `ForUser(userId)`; remarks on gate scope
- `tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs` - migrated and extended gate tests (10 test methods)
- `tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs` - gate-test resolved-path assertion only

## Decisions Made

- The `Fleet` constant keeps its value. `FleetChangeNotifier` still publishes to it, and the tracer test still registers on it, as the plan requires. Because update-stream matching is a raw prefix match, publishing to `game-servers/fleet` still reaches every per-user socket. Until plan 62-15, the live signal therefore keeps the pre-existing audience: every holder of a server-section claim. Plans 62-14 and 62-15 narrow it.

## Deviations from Plan

**1. [Style] Rewrapped one Task 1 controller remark in the Task 2 commit**
- **Found during:** Task 2 line-length check
- **Issue:** One line of the new `CanAccessServerSection` remarks was 121 characters; the limit is 120 (`.editorconfig`)
- **Fix:** Rewrapped the remark; no wording changes
- **Files modified:** `sources/Api/Controllers/UpdateStreamController.cs`
- **Commit:** `a6443299`

**Total deviations:** 1 (formatting only). **Impact:** none on behavior.

## TDD Gate Compliance

- RED: after the assertions were migrated, the test project failed to compile with CS0117 (`ForUser` missing) in both test files.
- GREEN: after `ForUser` and the controller change, all 35 filtered tests passed. With the Task 2 additions, 49 passed.
- Each task was committed as one unit, as the plan's commit instructions specify.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateStreamControllerFleet|FullyQualifiedName~FleetSignalTracer|FullyQualifiedName~FleetChangeNotifier"`: 49 passed, 0 failed
- `dotnet build sources/Api/Api.csproj`: succeeded
- `grep -c ForUser FleetChangeStreamPaths.cs` = 2; `grep -c FleetChangeStreamPaths.ForUser UpdateStreamController.cs` = 2; `grep -c FleetChangeStreamPaths.Fleet UpdateStreamController.cs` = 0
- `git diff --quiet 7bec477c -- sources/WebSocketContracts sources/Api/Services/UpdateStreamService sources/ReactApp sources/WebApp`: exit 0
- Test attribute count = 10 (at least 9 required); `StartsWith` count = 3

## Issues Encountered

None.

## Known Stubs

None.

## Next Phase Readiness

Ready for 62-14 (audience rule). 62-15 then moves the publisher to `ForUser` targets and removes the shared `Fleet` constant.

## Self-Check: PASSED

- All four modified files exist; commits `282a4df0` and `a6443299` are ancestors of HEAD.

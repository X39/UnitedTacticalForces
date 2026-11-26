---
phase: 62-fleet-quick-actions
plan: 05
subsystem: api
tags: [websocket, update-stream, xunit, channels, authorization, nginx, keep-alive]

# Dependency graph
requires:
  - phase: 62-01
    provides: FleetChangeNotifier (serialized coalescing loop, internal CoalesceWindow), ResolveSubscriptionPathAsync fleet gate, FleetChangeStreamPaths
  - phase: 62-04
    provides: complete interceptor coverage and the explicit post-commit possible-cost signal (documented here)
provides:
  - FleetChangeNotifierTests proving serialization, coalescing, failure isolation, non-blocking writers and lifecycle
  - UpdateStreamControllerFleetTests proving the claim matrix, path matrix, canonical registration and events regression
  - Global 25 second WebSocket KeepAliveInterval in Program.cs
  - Operator documentation "Fleet change signal" in docs/features/react-ui-beta.md and a decision-log bullet
affects: [62-06, 62-07, 62-08, fleet client reconnect, multi-instance API deployment]

# Actuals (#2632)
actuals:
  tokens: 11500
  tasks: 3
  commits: 4
plan_head_before: 8adc79f53ba2854ff04f34e2502d7392b1d1d418
plan_head_after: 1dacc3383a6f26e1f28b3ba1ac1e8a6064bc5565

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Concurrency-counting IUpdateStreamService fake (in-flight counter plus CAS maximum) to prove serialized sends"
    - "IHttpWebSocketFeature stub on DefaultHttpContext.Features to drive SubscribeAsync to registration without a server"
    - "Condition polling with a 5 second bound instead of fixed sleeps in timing-sensitive tests"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/FleetChangeNotifierTests.cs
    - tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs
  modified:
    - sources/Api/Program.cs
    - docs/features/react-ui-beta.md
    - docs/features/cloud-based-game-server-hosting/decision-log.md

key-decisions:
  - "A global 25 second WebSocket KeepAliveInterval, which also applies to the event streams, was accepted over a per-location nginx proxy_read_timeout"
  - "Holders of any server-section claim learn changed server ids and timing; this is accepted and documented, and dropping the id from the message is the escape hatch"
  - "No defect was found in FleetChangeNotifier or UpdateStreamController; both production files are unchanged"

patterns-established:
  - "Fleet delivery guarantees are tested against a fake IUpdateStreamService, not the shared UpdateStreamService, which this phase does not edit"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "The notifier never overlaps sends, coalesces duplicates in its window, survives a failing send, never blocks writers, and honors start and stop"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~FleetChangeNotifier (7 passed, repeated 3 times)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The fleet subscription gate admits only the listed claims, resolves only the exact two-segment path, registers only the canonical string, and leaves events unchanged"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~UpdateStreamControllerFleet (25 passed)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The WebSocket keep-alive is 25 seconds, and the API builds"
    requirement: FLT-04
    verification:
      - kind: other
        ref: "dotnet build sources/Api/Api.csproj (0 errors); grep KeepAliveInterval = TimeSpan.FromSeconds(25) sources/Api/Program.cs == 1"
        status: pass
    human_judgment: false
  - id: D4
    description: "An idle fleet socket survives the deployed nginx reverse proxy for longer than 60 seconds"
    requirement: FLT-04
    verification: []
    human_judgment: true
    rationale: "The proxy timeout is an assumed nginx default and can only be observed against the deployed proxy; the plan marks this as manual-only"
  - id: D5
    description: "The operator documentation covers the signal, the gate, accepted disclosure, sources, delivery, process-local limitation and keep-alive"
    requirement: FLT-04
    verification:
      - kind: other
        ref: "grep '## Fleet change signal' and 'backplane' in docs/features/react-ui-beta.md; grep 'fleet-changed signal' in decision-log.md; git ls-files --error-unmatch on both"
        status: pass
    human_judgment: true
    rationale: "Whether the prose is accurate and readable for operators is a reviewer judgment"

# Metrics
duration: 7min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 05: Fleet Signal Delivery Hardening Summary

**Two xunit suites (32 cases) prove the fleet delivery path. The notifier never runs two sends at once, sends a duplicate id inside its 250 ms window only once, keeps running after a failing send, and never blocks writers. The `game-servers/fleet` gate admits only the server-section claims, registers only the canonical path, and leaves events unchanged. A 25 second WebSocket keep-alive keeps idle streams open behind nginx, and the process-local design and accepted disclosure are now documented for operators.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-11-19T09:04:51Z
- **Completed:** 2026-11-19T09:11:46Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- D-15 is proven on the server side:
  - 200 parallel `Notify` calls never run more than one send at a time.
  - A send that throws leaves the other server in the same batch delivered, keeps the loop running, and logs exactly one warning naming the failed id.
  - While a send is blocked, 1000 `Notify` calls return immediately, and every one of them is delivered after release.
  - Nothing is sent after `StopAsync`.
- T-62-11 and T-62-12 are proven:
  - Eight allowed claim variants (bare and server-bound `sv`/`sv:view`, `crt:sv`, `adm:sv`, `adm:hosting`, `adm`) pass, and seven denied variants fail.
  - `/Game-Servers/FLEET/` registers exactly `game-servers/fleet` through a stubbed WebSocket upgrade.
  - Paths with extra segments, partial paths and other near-miss paths are rejected.
- Events regression: a server-claim holder cannot open a hidden event, and an unknown event stays forbidden even for `adm`. A visible event resolves to the requested path unchanged.
- `Program.cs` now passes `WebSocketOptions { KeepAliveInterval = 25 s }` and includes the justification comment.
- `react-ui-beta.md` gains a "Fleet change signal" section. The decision log records the fleet-changed signal, where it lives, the global keep-alive and the accepted disclosure.

## Task Commits

Each task was committed atomically:

1. **Task 1: Notifier serialization, coalescing, failure isolation and lifecycle tests** - `333fa91e` (test)
2. **Task 2: Subscription gate claim matrix, path matrix and canonical registration** - `867559b6` (test)
3. **Task 3: Proxy keep-alive and the fleet change signal documentation** - `8fdb1b33` (feat, Program.cs) and `1dacc338` (docs)

**Plan metadata:** recorded in the following docs(62-05) commit

## Files Created/Modified
- `tests/Api.Tests/GameServerHosting/FleetChangeNotifierTests.cs` - 7 delivery-path tests with a concurrency-counting fake sender and a capturing logger
- `tests/Api.Tests/GameServerHosting/UpdateStreamControllerFleetTests.cs` - claim/path theories, a canonical registration test through an `IHttpWebSocketFeature` stub, and an events regression test
- `sources/Api/Program.cs` - `UseWebSockets` with a 25 second `KeepAliveInterval`
- `docs/features/react-ui-beta.md` - "Fleet change signal" section placed before Validation
- `docs/features/cloud-based-game-server-hosting/decision-log.md` - one Decisions bullet

## Decisions Made
- The global keep-alive is accepted, and the AGENTS.md justification is recorded in the plan, the code comment and the docs. A per-location nginx `proxy_read_timeout` is documented as the fallback.
- No implementation fix was needed. The tracer notifier and gate passed every new test unchanged.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The client paragraph would have described behavior that does not exist yet**
- **Found during:** Task 3
- **Issue:** The plan asked for one paragraph saying the client reconnects with backoff and refetches on every reconnect. `sources/ReactApp/src/app/updateStream.ts` currently only reports `reconnecting`. Retry scheduling and the reconnect refetch belong to later fleet client plans.
- **Fix:** The paragraph now says the client is *specified* to reconnect with backoff and refetch, and states that the current hook only reports the reconnecting state.
- **Files modified:** docs/features/react-ui-beta.md
- **Committed in:** 1dacc338

---

**Total deviations:** 1 auto-fixed (1 documentation accuracy)
**Impact on plan:** None on scope. The documentation now matches what the code does today.

## Issues Encountered
- `git add` refused `docs/features/cloud-based-game-server-hosting/decision-log.md` because its folder is git-ignored. The file is already tracked, so `git add -u` staged it without forcing.
- Plan 62-05 did not touch the unrelated working-tree change in `sources/ReactApp/tests/hosting/arma-placement.test.tsx` or the untracked `deploy/host/*.ps1` scripts.

## User Setup Required

None - no external service configuration required. Manual check for later: behind the deployed nginx, leave the fleet page idle for more than 60 seconds and confirm the socket does not reconnect.

## Next Phase Readiness
- The server half of the fleet signal is proven and documented. The fleet client plans can rely on the guarantees: delivery is serialized and failure-isolated, and only the canonical path is registered.
- The client reconnect backoff and the refetch on every reconnect are still to be built (documented as specified, not yet implemented).

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

## Self-Check: PASSED

- Both created test files exist; commits 333fa91e, 867559b6, 8fdb1b33 and 1dacc338 are ancestors of HEAD.
- Acceptance greps: SendUpdateAsync (1), Game-Servers/FLEET (3), KeepAliveInterval = TimeSpan.FromSeconds(25) (1),
  app.UseWebSockets(); (0), "## Fleet change signal" (1), backplane (1), "fleet-changed signal" (1), both docs tracked.
- Plan verification: `dotnet test --filter FullyQualifiedName~GameServerHosting` 580 passed; `dotnet build sources/Api/Api.csproj` 0 errors.

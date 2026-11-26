---
phase: 62-fleet-quick-actions
plan: 01
subsystem: api, ui
tags: [websocket, ef-core-interceptor, channels, react, tanstack-query, vitest, xunit]

requires:
  - phase: 61-foundations-localization-member-access
    provides: deriveServerState state model, canAccessGameServers claim rule, CostConfirmationProvider ref-sync style
provides:
  - GameServerFleetChangedMessage wire contract (server id only)
  - FleetChangeNotifier serialized coalescing publisher on game-servers/fleet
  - FleetChangeInterceptor SaveChanges interceptor (lifecycle job insert or status change)
  - Gated canonical fleet subscription in UpdateStreamController.ResolveSubscriptionPathAsync
  - useUpdateStream hook, toSocketUrl, parseUpdateFrame in src/app/updateStream.ts
  - FleetLiveUpdatesProvider and useFleetLiveUpdates context
  - Global FakeWebSocket double, fleetServer/fleetAction fixtures, two icon mock entries
affects: [62-04, 62-05, 62-06, 62-11, 63-workspace]

actuals:
  tokens: 17700
  tasks: 2
  commits: 3
plan_head_before: e928adc0e9790ec5782079fcfee3e4533f3f991a
plan_head_after: a82459aa52f2e6fb96048c9deffbf2073d8db296

tech-stack:
  added: []
  patterns:
    - "Single-reader Channel<Guid> loop in front of IUpdateStreamService serializes all sends to one path"
    - "Singleton EF interceptor with ConditionalWeakTable<DbContext, PendingChanges> per-context state"
    - "Per-effect-run connection controller with disposed flag for StrictMode-safe WebSocket hooks"
    - "Pinned wire-frame literal shared by xunit and Vitest tests as the cross-stack contract"

key-files:
  created:
    - sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs
    - sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs
    - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
    - tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs
    - sources/ReactApp/src/app/updateStream.ts
    - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/tests/helpers/fakeWebSocket.ts
    - sources/ReactApp/tests/helpers/fleetFixtures.ts
    - sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx
    - sources/ReactApp/tests/hosting/fleet-fixtures.test.ts
  modified:
    - sources/Api/Controllers/UpdateStreamController.cs
    - sources/Api/Program.cs
    - tests/Api.Tests/GameServerHosting/HostingTestStore.cs
    - sources/ReactApp/tests/setup.ts
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "Pinned frame confirmed by the real serializer: the predicted literal matched byte for byte, so both tests use it unchanged"
  - "FleetChangeInterceptor takes an ILogger in addition to IFleetChangeNotifier so swallowed collect/publish failures are logged"
  - "Fleet gate accepts adm, adm:sv, adm:hosting, crt:sv, sv, sv:view of any value and always registers the canonical game-servers/fleet string"
  - "useUpdateStream status stays connecting while disabled; reconnectNow only connects when the controller has no socket"

patterns-established:
  - "Fleet signal: lifecycle write -> FleetChangeInterceptor -> IFleetChangeNotifier.Notify -> single loop -> IUpdateStreamService.SendUpdateAsync(FleetChangeStreamPaths.Fleet)"
  - "Browser signal handling: invalidate hostingQueryKeys.fleet and notifications with exact: true only for the exact contract type name"
  - "Every Vitest file runs against FakeWebSocket via tests/setup.ts; tests drive open/message/serverClose/fail"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "A committed lifecycle job insert reaches a fleet subscriber as exactly one pinned text frame carrying only the server id"
    requirement: FLT-04
    verification:
      - kind: integration
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#CommittedLifecycleJobInsert_ReachesFleetSubscriberAsPinnedFrame"
        status: pass
    human_judgment: false
  - id: D2
    description: "Fleet subscription gate forbids principals without a server-section claim, passes a bare sv claim and resolves the canonical path"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs#FleetSubscription_IsGatedByServerSectionClaims"
        status: pass
    human_judgment: false
  - id: D3
    description: "FleetLiveUpdatesProvider turns the pinned frame into exact fleet and notifications invalidations, ignores foreign frames, opens no socket for anonymous or non-server viewers and leaves one socket under StrictMode"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "Shared fleet fixtures derive the documented states and icon mock carries Checkmark16Regular and PlugDisconnected20Regular"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/fleet-fixtures.test.ts"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 01: Live Fleet Signal Tracer Summary

**One committed lifecycle write becomes one `GameServerFleetChangedMessage` frame. It goes through an EF Core SaveChanges interceptor, a single-reader channel notifier and a gated `game-servers/fleet` subscription to a StrictMode-safe React `useUpdateStream` hook. There the frame turns into exact-key invalidations of the fleet and notifications queries. An xunit test and a Vitest test pin the same wire literal.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-11-19T08:18:02Z
- **Completed:** 2026-11-19T08:33:11Z
- **Tasks:** 2
- **Files modified:** 18

## Accomplishments
- Backend tracer: the wire contract, the serialized coalescing notifier (hosted service, 250 ms window, per-send try/catch with a structured warning), the thread-safe per-context interceptor and the fleet-path gate. The gate registers only the canonical string, never the raw query value.
- `Program.cs` registers the notifier once as a singleton, interface and hosted service. The interceptor is added to the `AddDbContextFactory` options through the `(serviceProvider, options)` overload, so the controller, executor and queue contexts are all covered.
- Browser tracer: the resource-agnostic `useUpdateStream` with an inner per-run connection controller (no interval timer), the `FleetLiveUpdatesProvider`, and a context whose default value is a connected no-op.
- Wave 0 infrastructure: the global `FakeWebSocket` installed in `tests/setup.ts`, the `fleetServer`/`fleetAction` fixtures and two icon mock entries. The full Vitest suite (93 files, 724 tests) stays green with the double active.

## Task Commits

1. **Task 1: Tracer, backend half**: `3afb5e00` (feat(api): signal fleet changes over the update stream)
2. **Task 1: Tracer, React half**: `52482d46` (feat(webapp): add the update stream client and fleet provider)
3. **Task 2: Wave 0 shared fleet fixtures and icon mock entries**: `a82459aa` (test(webapp): add shared fleet fixtures and icon mocks)

TDD evidence for Task 2: the RED run failed with `Failed to resolve import "../helpers/fleetFixtures"` before the helper existed. After that, GREEN passed 4 of 4.

## Files Created/Modified
- `sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs`: wire message, `required Guid ServerId` only
- `sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs`: the only definition of `game-servers/fleet`
- `sources/Api/Services/GameServerLifecycle/IFleetChangeNotifier.cs`: non-blocking `Notify`, with the placement justification in remarks
- `sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs`: single-reader loop, coalescing and serialized sends, notes the process-local limitation
- `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs`: collects on SavingChanges, publishes on SavedChanges, discards on SaveChangesFailed; `Deferred` is reserved for 62-04
- `sources/Api/Controllers/UpdateStreamController.cs`: `ResolveSubscriptionPathAsync` replaces `CanSubscribeAsync`; events behavior is unchanged and the fleet branch is new
- `sources/Api/Program.cs`: notifier and interceptor registration, `AddInterceptors`
- `tests/Api.Tests/GameServerHosting/HostingTestStore.cs`: optional `IInterceptor[]` parameter, XML docs
- `tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs`: pinned-frame and gate facts, with a nested `RecordingWebSocket`
- `sources/ReactApp/src/app/updateStream.ts`: `useUpdateStream`, `toSocketUrl`, `parseUpdateFrame`, status and frame types
- `sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts`, `FleetLiveUpdatesProvider.tsx`: the context, the hook and the provider
- `sources/ReactApp/tests/helpers/fakeWebSocket.ts`, `tests/setup.ts`: the global WebSocket double
- `sources/ReactApp/tests/helpers/fleetFixtures.ts`, `tests/hosting/fleet-fixtures.test.ts`: shared fixtures and their guard
- `sources/ReactApp/tests/hosting/fleet-signal-tracer.test.tsx`: the React half of the pinned-frame proof
- `sources/ReactApp/tests/mocks/react-icons.tsx`: `Checkmark16Regular`, `PlugDisconnected20Regular`

## Decisions Made
- The real serializer output matched the predicted pinned literal byte for byte (`$id` on the envelope and the value, but not on the Guid). No change was needed in either test.
- `useUpdateStream` keeps its initial `connecting` status while disabled and never sets state synchronously in an effect body. This satisfies the react-hooks 7 lint rules; 62-06 owns the full status machine.
- `reconnectNow` connects only when the controller currently has no socket, so it does nothing while connecting or open.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Logger added to FleetChangeInterceptor**
- **Found during:** Task 1
- **Issue:** The plan requires unexpected collect and publish failures to be "logged and swallowed", but the plan's constructor took only `IFleetChangeNotifier`, so nothing could log.
- **Fix:** Added `ILogger<FleetChangeInterceptor>` as a second constructor parameter. DI resolves it, and tests pass `NullLogger`.
- **Files modified:** sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
- **Committed in:** 3afb5e00

**2. [AGENTS.md - Documentation] XML docs added to HostingTestStore**
- **Found during:** Task 1
- **Issue:** The modified test store had undocumented members, and AGENTS.md requires XML docs on every C# type and member.
- **Fix:** Documented the class, its properties, `CreateAsync` (including the new parameter) and `DisposeAsync`.
- **Committed in:** 3afb5e00

**3. [Rule 1 - Race avoidance] Tracer socket registered synchronously**
- **Found during:** Task 1
- **Issue:** Starting `RegisterAsync` in `Task.Run` could race the save, because the socket might be added after the frame was sent.
- **Fix:** The test calls `RegisterAsync(...).AsTask()` directly. Its synchronous prefix adds the socket before the first await, and the returned task still runs in the background until cancellation.
- **Committed in:** 3afb5e00

Additive test assertions beyond the plan: the resolver returns the canonical path for a mixed-case, padded path and null for `game-servers/fleet/extra`; a status probe shows `connecting:0` -> `open:0` -> `reconnecting:1`; a loading session opens no socket.

**Total deviations:** 3 auto-fixed (1 missing critical, 1 AGENTS.md compliance, 1 test race)
**Impact on plan:** None changes the architecture or the public shape; all are within the files listed in the plan.

## Issues Encountered
None.

## Known Stubs
None. `PendingChanges.Deferred` is intentionally left empty; Plan 62-04 fills it for transaction deferral.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- 62-04 can extend `FleetChangeInterceptor` (more entities, transaction deferral through `Deferred`) without touching the transport.
- 62-05 can add notifier concurrency and failure tests and the full claim matrix against `ResolveSubscriptionPathAsync`.
- 62-06 can add the retry scheduler inside `createStreamConnection` without changing `useUpdateStream`'s result shape, and can mount the provider in both layouts.
- Known limitation (D-14): the signal is process-local; a multi-instance API needs a shared backplane.

## Self-Check: PASSED
- All 13 created files exist on disk.
- Commits 3afb5e00, 52482d46 and a82459aa are ancestors of HEAD.
- All Task 1 and Task 2 acceptance criteria were re-run and pass. The `sources/Api` diff contains only GameServerLifecycle files, UpdateStreamController.cs and Program.cs. `sources/Common/Ui/routes.json` is unchanged.

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

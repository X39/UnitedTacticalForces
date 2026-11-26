---
phase: 62-fleet-quick-actions
plan: 14
subsystem: api
tags: [authorization, fleet, audience, gap-closure, ef-core, dependency-injection]
status: complete

requires:
  - phase: 62-fleet-quick-actions
    provides: per-user fleet socket registration (62-13) and the fleet change notifier (62-04)
provides:
  - IHostingAuthorizationService.GetServerViewAudienceAsync, the batched, uncached "who may view server S" query that mirrors HasServerPermission(sv:view)
  - Three internal static provider-neutral query builders (global administrators, direct grants, role grants) beside HasServerPermission
  - ServerViewGrant, the grant row type of the two grant queries
  - IFleetAudienceSource and its attribute-registered singleton FleetAudienceSource, which opens one DI scope per call
affects: [62-15, 62-16]

actuals:
  tokens: 14200
  tasks: 2
  commits: 2
plan_head_before: 743cb8f0255c2bf8586eb77595c5afebdd5c1eab
plan_head_after: dfd72babc3cf90b8036411780fb19e043480c346

tech-stack:
  added: []
  patterns:
    - "Set-form authorization rule kept in the same class as its per-principal form, with a drift test comparing both on rebuilt principals"
    - "Offline Npgsql translation proof through ToQueryString on a metadata-only context"
    - "Singleton-to-scoped bridge that depends only on IServiceScopeFactory to avoid a DbContext-factory construction cycle"

key-files:
  created:
    - sources/Api/Services/GameServerLifecycle/ServerViewGrant.cs
    - sources/Api/Services/GameServerLifecycle/IFleetAudienceSource.cs
    - sources/Api/Services/GameServerLifecycle/FleetAudienceSource.cs
    - tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs
    - tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/IHostingAuthorizationService.cs
    - sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs

key-decisions:
  - "62-14: the server view audience is computed in HostingAuthorizationService next to HasServerPermission; identifiers compare as lower-cased stored text against lower-case constants, values ordinally, null values count as empty, ValueType and IsPrefix are ignored, archived servers are not excluded"
  - "62-14: FleetAudienceSource takes only IServiceScopeFactory and resolves IHostingAuthorizationService per call from a fresh async scope; exceptions propagate so the 62-15 notifier can fail closed"

patterns-established:
  - "Audience drift guard: every claim-matrix row compares audience membership with HasServerPermission on RebuildPrincipalAsync"

requirements-completed: [FLT-04]

coverage:
  - deliverable: "GetServerViewAudienceAsync equals the REST sv:view rule, excludes banned and deleted users, batches, de-duplicates and reads current claims"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs#Audience_MatchesHasServerPermissionForEveryMatrixRow"
        status: pass
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs#RemovedClaim_DropsUserFromNextResult"
        status: pass
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs#ManyServers_ResolveInOneCallWithoutDuplicates"
        status: pass
  - deliverable: "The three audience queries translate for Npgsql"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs#AudienceQueries_TranslateForNpgsql"
        status: pass
  - deliverable: "Singleton FleetAudienceSource bridge with per-call scope and no database construction dependency"
    human_judgment: false
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs"
        status: pass
      - kind: command
        ref: "grep -cE \"DatabaseContext|IDbContextFactory\" sources/Api/Services/GameServerLifecycle/FleetAudienceSource.cs prints 0"
        status: pass

duration: 6 min
completed: 2026-11-19
---

# Phase 62 Plan 14: Server View Audience Query and Singleton Bridge Summary

**Batched, uncached `GetServerViewAudienceAsync` that mirrors `HasServerPermission(sv:view)` over user and role claims with banned/deleted exclusion, plus an `IServiceScopeFactory`-only singleton `FleetAudienceSource` the notifier can read it through.**

## Performance

- **Duration:** about 6 min
- **Started:** 2026-11-19T12:15:23Z
- **Completed:** 2026-11-19T12:20:48Z
- **Tasks:** 2
- **Files modified:** 7 (5 created, 2 modified)

## Accomplishments

- `IHostingAuthorizationService.GetServerViewAudienceAsync(ids, token)` returns an entry for every requested id. It runs three sequential `AsNoTracking` queries (global administrators, direct grants, role grants), merges them into one distinct set per server, ignores returned values that are not exact requested keys (so it fails closed), and keeps no cache.
- The rule is documented on both members as identical. A drift test covers 30 claim-matrix rows, including mixed-case identifiers, null and empty administrative values, an upper-case value, crt:sv, bare claims, sv:operate, a claim bound to the other server, and banned and deleted users. It checks every row against `HasServerPermission` on the principal that `RebuildPrincipalAsync` builds.
- An offline Npgsql `ToQueryString` test runs all three builders. Each produces SQL that filters on `"IsBanned"` and `"IsDeleted"` and uses `lower(`.
- `FleetAudienceSource` (`[Singleton<FleetAudienceSource, IFleetAudienceSource>]`) opens and disposes one async scope per call, forwards ids and token unchanged, and lets exceptions propagate. No `Program.cs` edit was needed.

## Task Commits

1. **Task 1: Audience rule with drift, batch, freshness and translation tests** - `0f756431` (feat)
2. **Task 2: Singleton audience source bridging the notifier to the scoped authorization service** - `dfd72bab` (feat)

## Files Created/Modified

- `sources/Api/Services/GameServerLifecycle/IHostingAuthorizationService.cs` - new audience member; existing members documented
- `sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs` - audience method and the three `internal static` builders; class and members documented
- `sources/Api/Services/GameServerLifecycle/ServerViewGrant.cs` - internal sealed record (user id, server value)
- `sources/Api/Services/GameServerLifecycle/IFleetAudienceSource.cs` - internal seam with `GetViewersAsync`
- `sources/Api/Services/GameServerLifecycle/FleetAudienceSource.cs` - singleton bridge
- `tests/Api.Tests/GameServerHosting/HostingFleetAudienceTests.cs` - 33 test cases (rule matrix, drift, batch, empty input, freshness, Npgsql translation)
- `tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs` - 5 tests (per-call scope, forwarding, propagation, attribute registration, scope-factory-only construction)

## Decisions Made

- Identifiers are lower-cased in SQL with `ToLower()` and compared with the constants lower-cased once, held in static readonly fields. This mirrors the `OrdinalIgnoreCase` type comparison of `ClaimsPrincipal.HasClaim` for the ASCII identifiers in use.
- The grant queries project through the two-argument `SelectMany` result selector into the `ServerViewGrant` positional record. This translated on both SQLite and Npgsql on the first attempt.
- Beyond the plan's test list, test 5 also asserts that a call fails when no authorization service is registered. This shows the dependency is resolved per call, not at construction.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - AGENTS.md documentation] Documented the existing members of the changed interface and class**
- **Found during:** Task 1
- **Issue:** AGENTS.md requires XML documentation on every changed type and member. `IHostingAuthorizationService`, `HostingAuthorizationService` and their existing members had none, which produced CS1591 warnings.
- **Fix:** Added summaries and parameter/return docs to the interface members, and `<inheritdoc />` plus class and private-member docs to the implementation. No behavior changed.
- **Files modified:** IHostingAuthorizationService.cs, HostingAuthorizationService.cs
- **Commit:** 0f756431

**2. [Process] TDD tasks committed as one feat commit each**
- **Issue:** Both tasks are `tdd="true"`, but the plan prescribes a single `feat(api): ...` commit subject per task.
- **Fix:** Wrote the tests and the implementation together, ran them green, and made one commit per task as the plan's action text instructs. There are no separate RED commits.

**Total deviations:** 1 auto-fixed (documentation) and 1 process note. **Impact:** none on behavior or scope.

## Issues Encountered

None. The seeded claim catalog already holds valueless `crt:sv`, `sv` and `sv:view` rows. The tests' `ClaimRows` helper reuses stored rows, so the unique claim indexes are never violated.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetAudience"`: 38 passed (both suites discovered)
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 633 passed
- `dotnet build sources/Api/Api.csproj`: succeeded with 0 errors and no warnings in the changed files
- Every acceptance-criteria grep passes, and the guard over WebSocketContracts, UpdateStreamService, ReactApp and WebApp is unchanged since 7bec477c. `Program.cs` contains no `FleetAudienceSource`.

## Next Phase Readiness

Ready for 62-15. The notifier can take `IFleetAudienceSource`, resolve the viewers of each coalesced server, send to `FleetChangeStreamPaths.ForUser` per viewer, and log and drop the batch on failure.

## Self-Check: PASSED

- All 5 created files exist on disk
- Commits 0f756431 and dfd72bab are ancestors of HEAD

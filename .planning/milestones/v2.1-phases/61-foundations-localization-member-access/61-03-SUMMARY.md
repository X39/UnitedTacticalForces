---
phase: 61-foundations-localization-member-access
plan: 03
subsystem: api-hosting-access
tags: [xunit, sqlite, authorization, game-servers, acc-01]

requires:
  - phase: none
    provides: independent API-side proof, no dependency on plans 61-01 or 61-02
provides:
  - GameServerListAccessTests, the ACC-01 list endpoint access proof for sv and sv:view
  - GameServerNotificationAccessTests, the ACC-01 notification CanOperate and cost-stop proof
affects: [61-12, phase-61-verification]

actuals:
  tokens: 3300
  tasks: 2
  commits: 2
plan_head_before: 684aec106f9200f40656fdfbc305dfc5f9f3677c
plan_head_after: fc8f78356900ec811fad8c3320dcefc0b3b55142

tech-stack:
  added: []
  patterns:
    - "Access proofs drive the real GameServersController with a DefaultHttpContext principal over an in-memory SQLite store"
    - "Theory scenario names (strings) instead of an enum build the excluded principals"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs
  modified: []

key-decisions:
  - "No production fix was needed (D-11): the existing HasServerPermission boundary and the GetServersAsync filter behave correctly for sv, sv:view and every excluded principal"

patterns-established:
  - "Two seeds in one HostingTestStore do not collide; HostingTestData.SeedServerAsync can be called twice to prove exclusion of an unrelated server"

requirements-completed: [ACC-01]

coverage:
  - id: D1
    description: "GET game-servers returns exactly the bound server for sv and sv:view and an empty list for operate-only, other-server, no-claims and bare sv or sv:view principals"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs#GetAll_WithServerClaimBoundToOneServer_ReturnsOnlyThatServer"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs#GetAll_WithoutMatchingViewGrant_ReturnsEmptyList"
        status: pass
    human_judgment: false
  - id: D2
    description: "sv:view sees the start action disabled with the operate-permission reason; sv sees it enabled"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs#GetAll_WithViewClaim_DisablesStartActionWithOperateReason"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs#GetAll_WithAllPermissionsClaim_EnablesStartAction"
        status: pass
    human_judgment: false
  - id: D3
    description: "Notifications report CanOperate and an enabled or disabled terminate cost-stop action per sv or sv:view, omit unviewable servers, and carry PossibleCost"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs#GetNotifications_ReportsCanOperateAndCostStopAction"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs#GetNotifications_WithViewClaimForOtherServer_ReturnsEmptyList"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs#GetNotifications_WithViewClaim_ReportsPossibleCostOfActiveDeployment"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-11-18
status: complete
---

# Phase 61 Plan 03: Server list and notification access proof Summary

**The real `GameServersController`, `GameServerProjectionService` and `HostingAuthorizationService` are proven over in-memory SQLite to return exactly the servers an `sv` or `sv:view` holder may view, with operate and terminate actions disabled for `sv:view`.**

## Performance

- **Duration:** about 8 min (start time derived from the prior plan's completion)
- **Completed:** 2026-11-18T17:32Z
- **Tasks:** 2 (both auto)
- **Files changed:** 2 (both created)

## Accomplishments

- `GameServerListAccessTests` (9 cases): `sv` and `sv:view` bound to a server id yield that server only (a second seeded server is excluded); operate-only, a view claim bound to another Guid, no claims, bare `sv` and bare `sv:view` with an empty value all yield an empty list; the start action is disabled with "Server Operate permission is required." for `sv:view` and enabled for `sv`.
- `GameServerNotificationAccessTests` (4 cases): with an active deployment, `sv` gives `CanOperate` true and an enabled `terminate` action, `sv:view` gives `CanOperate` false and a disabled `terminate` action with the operate reason; a view claim for another server yields no notification; `PossibleCost` is true for the `sv:view` notification.
- Research was confirmed by execution: no defect surfaced, so no production change was made (D-11 fix path unused).

## Task Commits

1. **Task 1: List endpoint proof for sv and sv:view** - `51fa3a2e` (test)
2. **Task 2: Notification access proof (CanOperate and cost-stop)** - `fc8f7835` (test)

**Plan metadata:** committed separately (docs commit carrying this SUMMARY, STATE.md and ROADMAP.md).

## Verification Results

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerListAccessTests"`: 9 passed, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"` (plan verification): 531 passed, 0 failed.
- Acceptance: both files exist; `/// <summary>` count is 8 (list, needed 6) and 5 (notification, needed 4); each file declares exactly one top-level type.

## Decisions Made

- Excluded principals use string scenario names in `InlineData` rather than an enum, per the E-prefix rule and the plan.
- The small private helpers (controller construction and action lookup) are duplicated in each test class instead of a shared type, as the plan directs.

## Deviations from Plan

None - plan executed exactly as written.

## Deferred Issues

None.

## Known Stubs

None.

## Threat Flags

None. T-61-06, T-61-07 and T-61-08 are mitigated by the new tests; no new surface was introduced.

## Next Phase Readiness

The API half of ACC-01 is proven. Plan 61-12 owns the React-side check that joins the notification `CanOperate` and `CostStopAction` with the fleet UI.

## Self-Check: PASSED

- Both test files exist on disk; commits `51fa3a2e` and `fc8f7835` are ancestors of HEAD; all task acceptance criteria and the plan-level verification re-run green.

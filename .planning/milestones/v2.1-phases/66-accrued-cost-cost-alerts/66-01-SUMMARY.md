---
phase: 66-accrued-cost-cost-alerts
plan: 01
subsystem: api
tags: [hosting, cost, accrual, authorization, ledger, dotnet, efcore]

requires:
  - phase: 65-hosting-finance
    provides: ProviderCostSlice ledger entries, CostOwner visibility rule (CanViewCostOwnerAsync)
provides:
  - "CostAccrualCalculator: pure slice-prorating accrual formula and as-of rule"
  - "ICostAccrualService/CostAccrualService: latest-session selection, cost-owner visibility, batch accrual"
  - "GET game-servers/{id}/accrued-cost returning GameServerAccruedCostDto"
  - "IHostingAuthorizationService.GetCostOwnerViewAudienceAsync with drift test"
affects: [66-04, 66-05, 66-08, accrued-cost-frames, cost-alert-audience]

plan_head_before: 4ca6f8ac6c20f513ecef6b49065ed0218aa22109
plan_head_after: 7ecedd41fd061d1e3ac4c3d9709efdc564b130a0

actuals:
  tokens: 23500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Prorate every charged slice by elapsed fraction, frozen at TimeStampResolved"
    - "Principal rule and set-based audience rule kept in one class with a matrix drift test"

key-files:
  created:
    - sources/Api/Services/GameServerLifecycle/CostAccrualCalculator.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostSlice.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostResult.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostUnavailableReasons.cs
    - sources/Api/Services/GameServerLifecycle/ICostAccrualService.cs
    - sources/Api/Services/GameServerLifecycle/CostAccrualService.cs
    - sources/Api/Services/GameServerLifecycle/CostOwnerViewGrant.cs
    - sources/Api/DTO/AccruedCostDto.cs
    - sources/Api/DTO/GameServerAccruedCostDto.cs
    - tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs
    - tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs
    - tests/Api.Tests/GameServerHosting/CostOwnerViewAudienceTests.cs
  modified:
    - sources/Api/Controllers/GameServersController.cs
    - sources/Api/Services/GameServerLifecycle/IHostingAuthorizationService.cs
    - sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs
    - tests/Api.Tests/GameServerHosting/HostingTestData.cs
    - tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs

key-decisions:
  - "Accrued cost is the sum of charged ProviderCostSlice amounts, each prorated by elapsed fraction at the as-of instant; final total is clipped to TimeStampResolved (not the sum of prepaid slices)"
  - "Latest session = newest deployment that may still cost or has at least one slice, so a never-charged latest attempt does not hide the previous session total"
  - "A viewer who may view the server but not the cost owner gets amount null with reason hidden and no other cost value"
  - "Unavailable reasons are plain string codes (rate, not-yet-charged, hidden) shared by REST and the later update-stream frame"
  - "Accrual service is injected per action with [FromServices]; GameServersController constructor is unchanged"

patterns-established:
  - "Cost-owner audience mirrors CanViewCostOwnerAsync: hosting admin, empty fin/fin:view-all, co:view with owner id (direct or role), owner's own user; deleted/banned excluded; unrequested values ignored"

requirements-completed: [CST-03]

coverage:
  - id: D1
    description: "GET game-servers/{id}/accrued-cost returns the current or last session cost prorated from charged slices, frozen at resolution, null (never zero) when unknown"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "A viewer without cost-owner visibility receives amount null and reason hidden; an unviewable or unknown server returns 404"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs#ViewerWithoutCostOwnerVisibility_GetsHiddenWithoutAmount"
        status: pass
    human_judgment: false
  - id: D3
    description: "GetCostOwnerViewAudienceAsync equals CanViewCostOwnerAsync for every claim class"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostOwnerViewAudienceTests.cs#Audience_MatchesCanViewCostOwnerForEveryMatrixRow"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-11-21
status: complete
---

# Phase 66 Plan 01: Accrued Cost on the Control Plane Summary

**Control-plane accrued cost from durable ledger slices, prorated per slice and frozen at resolution, served by `GET game-servers/{id}/accrued-cost`, plus a cost-owner view audience pinned to the principal rule by a drift test.**

## Performance

- **Duration:** about 35 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 12 created, 5 modified

## Accomplishments

- Tracer slice end to end: slice rows, pure calculator, scoped service, DTOs, REST action, tests through the real controller.
- Edge-case matrix pins consecutive, catch-up, future, settled and rounding cases (4 decimals, AwayFromZero).
- `GetCostOwnerViewAudienceAsync` returns, per requested owner, exactly the active users for whom `CanViewCostOwnerAsync` holds. A 36-row matrix test compares both for every claim class, and an offline Npgsql translation test covers the new query builders.

## Task Commits

1. **Task 1: Tracer, accrued cost endpoint** - `9f73441b` (feat)
2. **Task 2: Proration edge-case matrix** - `2a747357` (test)
3. **Task 3: Cost-owner view audience** - `7ecedd41` (feat)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 775 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: succeeded, 0 errors.
- No file under `sources/ReactApp`, `sources/WebApp`, `sources/CloudWorker` or `sources/Database/Migrations` was touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `AccruedCostResult` and `AccruedCostUnavailableReasons` made public**
- **Found during:** Task 1
- **Issue:** The plan listed both types as `internal`, but `ICostAccrualService` is public (it is a `[FromServices]` parameter of a public controller action) and exposes `AccruedCostResult` in `CalculateForDeploymentsAsync`; an internal type in a public signature is a compile error (CS0050/CS0051).
- **Fix:** Declared both types `public`. `AccruedCostSlice` and `CostAccrualCalculator` stay internal as planned.
- **Files modified:** `AccruedCostResult.cs`, `AccruedCostUnavailableReasons.cs`
- **Commit:** `9f73441b`

**2. [Rule 3 - Blocking] Test helper `AddCostSliceAsync` also sets `ProviderIdentifier`**
- **Found during:** Task 1
- **Issue:** The ledger check constraint `CK_HostingCostLedgerEntries_Shape` requires a non-null `ProviderIdentifier` for provider-cost slices; the helper as specified failed to save.
- **Fix:** Helper sets `ProviderIdentifier = TestRuntimeProvider.Identifier`.
- **Files modified:** `tests/Api.Tests/GameServerHosting/HostingTestData.cs`
- **Commit:** `9f73441b`

**Total deviations:** 2 auto-fixed (both Rule 3). No scope change.

## Known Stubs

None.

## Threat Flags

None. The new endpoint and audience are the surfaces named in the plan's threat model (T-66-01, T-66-02, T-66-03), each with a covering test.

## Issues Encountered

None beyond the deviations above. The OpenAPI snapshot and React types are intentionally not regenerated here; plan 66-08 regenerates them once for all backend plans.

## Self-Check: PASSED

- All 12 created files and 5 modified files exist.
- Commits `9f73441b`, `2a747357`, `7ecedd41` are ancestors of HEAD.

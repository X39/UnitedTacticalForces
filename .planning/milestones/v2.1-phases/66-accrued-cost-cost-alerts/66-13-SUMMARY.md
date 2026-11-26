---
phase: 66-accrued-cost-cost-alerts
plan: 13
subsystem: api
tags: [billing-unit, accrued-cost, hetzner, cost-alerts, runtime-provider, uat-seed]
status: complete
gap_closure: true

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 accrual calculator and service, 66-04 UAT costing scenario, 66-02 cost alert evaluator
provides:
  - "RuntimeBillingUnit (StartedHour, PerStarted, BilledThrough) declared through RuntimeProviderCapabilities.BillingUnit"
  - "Hetzner and the emulated Hetzner provider declare the started-hour unit; the test provider declares per started second"
  - "CostAccrualCalculator rounds the session's billable time to the provider unit; CostAccrualService resolves the unit from the ledger provider identifier"
  - "Reason code billing-unit for slices whose provider declares no unit or is not registered"
  - "UAT costing seed that shows 1.20 EUR and then steps to 1.80 EUR five minutes later"
affects: [66-16-billing-facts-on-the-wire, uat, phase-66-verification]

plan_head_before: 4ff2e3e7a15deeba98d3b85dd853521058fd889b
plan_head_after: e6936eca96d6d07a420e1896e3b540f427469224

actuals:
  tokens: 18600
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Provider-declared capability (BillingUnit) resolved by ledger ProviderIdentifier instead of provider-specific rules in the accrual path"
    - "Integer nanosecond ceiling division (BigInteger) for unit rounding, no floating drift"

key-files:
  created:
    - sources/Api/Services/HostingRuntime/RuntimeBillingUnit.cs
    - tests/Api.Tests/GameServerHosting/RuntimeBillingUnitTests.cs
  modified:
    - sources/Api/Services/HostingRuntime/RuntimeProviderCapabilities.cs
    - sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs
    - sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostSlice.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostResult.cs
    - sources/Api/Services/GameServerLifecycle/AccruedCostUnavailableReasons.cs
    - sources/Api/Services/GameServerLifecycle/CostAccrualCalculator.cs
    - sources/Api/Services/GameServerLifecycle/CostAccrualService.cs
    - sources/Api/Data/UatFleetScenarioSeeder.cs
    - tests/Api.Tests/GameServerHosting/TestRuntimeProvider.cs
    - tests/Api.Tests/GameServerHosting/HostingTestData.cs
    - tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs
    - tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs
    - tests/Api.Tests/GameServerHosting/HetznerRuntimeProviderTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs
    - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs

key-decisions:
  - "The session's billable time, counted from its first charged slice, is rounded up as a whole to the provider unit (planner interpretation of D-15). Rounding every 15-minute reservation slice separately would bill four hours per hour. Charged slice amounts price the covered part, and the newest slice's hourly rate prices billed time beyond it"
  - "The unit lives on RuntimeProviderCapabilities as an optional trailing parameter (null = not declared), next to EstimatedHourlyCostEur, and an undeclared, mixed or unregistered provider identifier yields amount null with reason billing-unit instead of a lower prorated guess"
  - "RuntimeBillingUnit is public (not internal) because AccruedCostResult and RuntimeProviderCapabilities are public records that carry it"
  - "A settled deployment without slices still returns 0 at its resolution instant and needs no unit; only slices without a unit are unavailable"

patterns-established:
  - "Nothing in CostAccrualCalculator or CostAccrualService names Hetzner; the unit comes only from IRuntimeProvider.Capabilities.BillingUnit"

requirements-completed: [CST-03, CST-04]

duration: 25min
completed: 2026-11-21
---

# Phase 66 Plan 13: Billing unit for accrued cost Summary

**Accrued session cost and cost alert thresholds now follow the provider's declared billing unit: a Hetzner session that ran five minutes reports a full hour (0.1000 at 0.10 EUR/h), another provider keeps its own granularity, and an undeclared unit is reported as unavailable (reason `billing-unit`), proven through the accrued-cost endpoint, the calculator and the evaluator.**

## Performance

- **Duration:** about 25 minutes
- **Completed:** 2026-11-21
- **Tasks:** 3 (task 1 is the tracer)
- **Commits:** 5 (measured from the persisted ledger, base `4ff2e3e7`)

## Accomplishments

- Tracer (task 1): `RuntimeBillingUnit` with `StartedHour`, `PerStarted(Duration)` and `BilledThrough(anchor, asOf)` (BigInteger nanosecond ceiling division, at least one unit once billing started, the anchor itself before it). `RuntimeProviderCapabilities.BillingUnit` is declared by `HetznerRuntimeProvider.ProviderBillingUnit` (ready and not-ready path), by `EmulatedHetznerRuntimeProvider` (so UAT bills like production) and by `TestRuntimeProvider` (per started second). `CostAccrualService` takes `IEnumerable<IRuntimeProvider>`, maps `ProviderIdentifier` to the unit once per instance (first registration wins) and resolves a deployment's unit only when all its slices carry one identical registered identifier; REST (`GetForServerAsync`) and the evaluator (`CalculateForDeploymentsAsync`) share that resolution. The endpoint returns 0.1000 for a Hetzner slice at minute 5, and the existing per-second expectations (0.0100 at minute 6, 0.0150 frozen at minute 9) are unchanged.
- Calculator rule: with the anchor at the earliest slice start, `billedThrough = unit.BilledThrough(anchor, asOf)`, each slice contributes its amount times the fraction of its span before `billedThrough`, and billed time past the latest slice end is priced at that slice's `HourlyRate` (or amount divided by span hours when the ledger recorded no rate). A session that may still cost keeps advancing when charging stalls (minute 130 reports 0.3000); a resolved session freezes (resolved at minute 61 reports 0.2000, also a day later). Ledger rows, balances and reservation slices are untouched.
- Pinned cases (task 2): 5-minute session, hour boundary (0.1000 at 60 min, 0.2000 at 60 min + 1 s), five slices at minute 61, extension rate recorded vs derived (0.0137 / 0.0136), catch-up slice (0.1000), resolution freeze and zero before the first slice, stalled charging, a per-minute stub (0.0033 at 90 s, 0.0017 at 60 s), null unit versus unchanged no-slice reasons, overflow-free rounding over ten years, and both Hetzner providers declaring the started hour.
- Threshold proof (task 3): a Hetzner session with a 0.10 threshold crosses at minute 1, records one alert with accrued 0.1000 and two delivery rows, records nothing on a later pass, and the evaluator's accrual equals the endpoint amount; the same session on a per-second provider records no alert at minute 1 (accrual 0.0017).
- UAT seed: the costing session now starts 1 h 55 min before seeding with a 1.3000 EUR slice at 0.60 EUR/h, so it shows 1.20 EUR after a reseed and at +5 min and steps to 1.80 EUR one second later (three instants asserted).

## Task Commits

1. **Task 1: Tracer, a 5-minute Hetzner session reports a full hour through the endpoint** - `90f40a50` (feat)
2. **Task 2: Billing rule at boundaries, across slices, after resolution, other unit** - `1cbe3332` (test)
3. **Task 3a: Thresholds cross at minute 1 of a started hour** - `faff5ffb` (test), `e6936eca` (test, separates the later-pass case so each behavior line has its own test)
4. **Task 3b: UAT costing seed before its third hour** - `9ddfccfc` (chore)

Tracer gate (auto mode): the tracer's `<verify>` set was re-run on the committed tree by the task 2 and 3 runs (all green), so the proven slice held before expansion. Tracer verified end-to-end, expanded.

## Verification

| Command | Result |
|---------|--------|
| Task 1 filter (endpoint, calculator, evaluator, seeder, publisher tests) | 53 passed |
| Task 2 filter (billing unit, calculator, Hetzner provider) | 113 passed |
| Task 3 filter (evaluator, seeder, endpoint, calculator) | 59 passed, then 21 evaluator tests after the split |
| Phase filter (`CostAccrual`, `CostAlert`, `AccruedCost`, `RuntimeBillingUnit`, `HetznerRuntimeProvider`, `UatFleetScenarioSeeder`, `OpenApi`) | 242 passed |
| `dotnet build X39.UnitedTacticalForces.slnx` | 0 errors |
| Full `Api.Tests` | 1182 passed, 1 failed: the known `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` (pre-existing, plan 66-06 deferred item) |
| `git diff --name-only 4ff2e3e7 HEAD` on `HostingCostSliceService.cs`, `sources/ReactApp`, `sources/WebApp` | nothing changed |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] NodaTime 3.3.2 has no `Duration.BigIntegerNanoseconds`**
- **Found during:** Task 1 (first build)
- **Issue:** the property used for the nanosecond arithmetic does not exist in the referenced NodaTime version.
- **Fix:** `RuntimeBillingUnit` converts a `Duration` with `Days * 86_400_000_000_000 + NanosecondOfDay` as a `BigInteger`, which keeps the overflow-free integer arithmetic the plan asked for.
- **Files modified:** `sources/Api/Services/HostingRuntime/RuntimeBillingUnit.cs`
- **Commit:** `90f40a50`

**2. [Rule 3 - Blocking] `SeedCostingSessionAsync` cannot take an optional parameter in front of `params`**
- **Found during:** Task 3
- **Issue:** adding `string? providerIdentifier = null` before `params decimal[] amounts` would make existing positional callers bind a threshold to the string parameter.
- **Fix:** a new core overload takes `providerIdentifier` explicitly, and the existing overloads forward `null`, so no existing caller changed.
- **Files modified:** `tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs`
- **Commit:** `faff5ffb`

**3. [Plan-conformance] `RuntimeBillingUnit` is public**
- **Issue:** the interfaces block did not state the accessibility; `AccruedCostResult` and `RuntimeProviderCapabilities` are public records and expose the type, so an internal type would not compile.
- **Fix:** public sealed record with a private constructor and the validated factory.
- **Commit:** `90f40a50`

Task 2 exposed no defect in the Task 1 implementation, so no production fix was needed there. Task 3 needed no production change in the evaluator.

## Authentication Gates

None.

## Deferred Issues

- `ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` still fails as before (plan 66-06 deferred item, unrelated).
- The accrued-cost wire contract does not yet expose the billing facts; plan 66-16 adds them (`BilledThrough` and `BillingUnit` are already carried on `AccruedCostResult`).

## Known Stubs

None. No placeholder data flows to a UI; the changes are calculation, provider declaration, tests and a seed.

## Threat Flags

None. T-66-40 is mitigated: the unit comes only from a registered provider's declared capabilities chosen by the ledger identifier, and a missing, mixed or unregistered identifier yields `billing-unit` (tested with `UnregisteredProvider_IsUnavailableWithBillingUnitReason`). T-66-41 is mitigated: `PerStarted` rejects zero and negative lengths and the rounding is integer BigInteger arithmetic (`RuntimeBillingUnitTests`).

## Self-Check: PASSED

- `sources/Api/Services/HostingRuntime/RuntimeBillingUnit.cs` and `tests/Api.Tests/GameServerHosting/RuntimeBillingUnitTests.cs` exist.
- Commits `90f40a50`, `1cbe3332`, `faff5ffb`, `e6936eca` and `9ddfccfc` are ancestors of HEAD.
- Acceptance counts: `AccruedCostEndpointTests` 13 facts (needs 12), `CostAccrualCalculatorTests` 18 (needs 17), `CostAlertEvaluatorTests` 15 (needs 15), `HetznerRuntimeProviderTests` references `BillingUnit` at least twice, `1.3000m` and `FromMinutes(115)` appear once each in the seeder, `billing-unit` once in the reasons file.

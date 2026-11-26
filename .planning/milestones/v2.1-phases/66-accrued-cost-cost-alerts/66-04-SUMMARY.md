---
phase: 66-accrued-cost-cost-alerts
plan: 04
subsystem: api
tags: [uat, seeder, aspire, mailpit, cost-alerts, dotnet]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 CostAccrualCalculator (prorated accrued cost from charged slices)
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-02 EmailOptions (Email:Smtp settings) and the human-approved Mailpit tag v1.34.0
provides:
  - "UAT 9 Costing (accrued cost): a running seeded session with a backdated, charged ProviderCostSlice (about 1.20 EUR accrued at seeding time)"
  - "Mailpit SMTP sink (axllent/mailpit v1.34.0) under the AppHost Uat:Enabled block, API Email__Smtp__* pointed at it"
  - "UAT owner email fallback uat-owner@utf.test so the email channel has a recipient"
  - "UatFleetScenarioSeeder.SeedAsync optional now parameter for deterministic tests"
affects: [66-05, 66-06, 66-07, 66-08, end-of-phase UAT]

plan_head_before: 98ae56b17ed04a9f801c96858ec7c7ed91580cdd
plan_head_after: 471828a191f79aadac691bb39502a5c52f4faebd

actuals:
  tokens: 14000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "UAT scenario seeded through the same ledger rows the slice charging service writes, so reconciler and accrual calculator treat it as a real session"
    - "Development-only containers confined to the Uat:Enabled AppHost block"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs
  modified:
    - sources/Api/Data/UatFleetScenarioSeeder.cs
    - sources/AppHost/Program.cs
    - scripts/Start-Uat.ps1

key-decisions:
  - "Costing slice spans now-2h to now+15m at 0.60 EUR/h (1.3500 EUR charged, 1.2000 EUR accrued at seeding, +0.01 EUR per minute)"
  - "Owner email set only when blank; an existing address is kept"
  - "Mailpit uses fixed host ports 8025 (UI) and 1025 (SMTP) so UAT instructions can name them"
  - "No cost alert threshold is seeded; the owner sets an amount in the app"

patterns-established:
  - "Seeder takes the tracked CostOwner (not just its id) so charged-slice seeding adjusts the balance in the same unit of work"

requirements-completed: [CST-03, CST-04, CST-06]

duration: 12min
completed: 2026-11-21
---

# Phase 66 Plan 04: UAT costing scenario and Mailpit inbox Summary

**A fresh UAT start now has a running server that has already accrued about 1.20 EUR through a normal charged slice, and cost alert emails land in a pinned local Mailpit inbox at http://localhost:8025.**

## Performance

- **Duration:** about 12 min
- **Started:** 2026-11-21T14:36:00Z
- **Completed:** 2026-11-21T14:49:00Z
- **Tasks:** 2
- **Files modified:** 4 (1 created)

## Accomplishments

- `UAT 9 Costing (accrued cost)` scenario: Running, PossibleCost deployment, authorization end now+15m, rate 0.60, one `ProviderCostSlice` ledger entry (-1.3500, BalanceAfter matching the decreased owner balance), resource rate 0.60.
- `CostAccrualCalculator.Calculate` over the seeded slice returns exactly 1.2000 at the seeding instant (asserted in tests).
- Mailpit container (`axllent/mailpit`, tag `v1.34.0`, the tag the human approved at the 66-02 checkpoint) with UI endpoint 8025 and SMTP endpoint 1025, plus `Email__Smtp__Host/Port/SecureSocket/FromAddress` on the API, all inside the `Uat:Enabled` block.
- `Start-Uat.ps1` help documents the costing server and the Mailpit inbox; script behavior is unchanged.

## Task Commits

1. **Task 1: Tracer - seed a running session that has already cost money** - `8f3b38b9` (test)
2. **Task 2: Mailpit inbox in the UAT profile** - `471828a1` (chore)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UatFleetScenarioSeederTests"`: 5 passed, 0 failed.
- `dotnet build sources/AppHost/AppHost.csproj`: 0 errors.
- Acceptance greps: `axllent/mailpit` 1, `Email__Smtp__Host` 1, unpinned-tag guard 0, `Mailpit` in Start-Uat.ps1 1.
- `Program.cs` of the API untouched; no file under `sources/ReactApp`, `sources/WebApp` or `sources/Database` changed.
- Tracer gate: the `<verify>` test run was already the end-to-end check of the slice and passed before Task 2 expansion.

## Deviations from Plan

None - plan executed as written. The plan's `<precondition>` (an approved Mailpit tag recorded in 66-02-SUMMARY.md) was met: `approved mailpit:v1.34.0`.

## Issues Encountered

- Test first referenced a non-existent `seed.Owner`; the record member is `CostOwner`. Fixed in the same task before the commit.
- The Mailpit container itself was not started here (needs a container runtime); only the AppHost build and the grep gates were run. It is exercised at the end-of-phase UAT via `./scripts/Start-Uat.ps1 -Fresh`.

## Known Stubs

None.

## Threat Flags

None. Seeder remains behind `Testing:UatScenarios:Enabled` in Development; Mailpit and the SMTP settings exist only under `Uat:Enabled` (T-66-12); the image tag is pinned to the approved version (T-66-13).

## Self-Check: PASSED

- FOUND: sources/Api/Data/UatFleetScenarioSeeder.cs
- FOUND: tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs
- FOUND: 8f3b38b9 and 471828a1 (ancestors of HEAD)

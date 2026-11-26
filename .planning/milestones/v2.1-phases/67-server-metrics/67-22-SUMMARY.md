---
phase: 67-server-metrics
plan: 22
subsystem: api
tags: [uat, seed, cost-reconciler, hosting-ledger, audit, gap-closure, g-67-1, g-67-3]
status: complete
gap_closure: true

requires:
  - phase: 67-13
    provides: UAT fleet seeder and the UAT worker emulator that feeds UAT 10
  - phase: 67-21
    provides: Stopped-server metrics merge used by the UAT 1 and 3 re-check
provides:
  - One idempotent, audited 1000.0000 EUR seed credit for the UAT cost owner, booked before every seeded charge
  - A regression test that runs the real HostingCostReconciler over the seeded UAT fleet at seed time and 15 minutes later
  - UAT help text that names the funded UAT cost owner
affects: [phase-67-verification, verify-work]

plan_head_before: c879aa7c1f1809530e477a95e6aaaa3a35100f5a
plan_head_after: f3c068a346bcea4435ea4a35a3f17e89a3772d28
commits: 5

actuals:
  tokens: 5176
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Seed-only ledger credit built directly in the static seeder, shaped like HostingFinanceService.RecordBankTransferPaymentAsync, with a fixed record identifier as idempotency key"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/UatFleetScenarioCostReconciliationTests.cs
  modified:
    - sources/Api/Data/UatFleetScenarioSeeder.cs
    - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs
    - scripts/Start-Uat.ps1

key-decisions:
  - "The credit lives in UatFleetScenarioSeeder, not DebugDemoDataSeeder: only the UAT profile unpauses the control plane, and the credit stays behind Development plus Testing:UatScenarios:Enabled"
  - "The credit is a BankTransferPayment ledger entry with fixed record identifier 7f3c2a1e-5b8d-4c6f-9a0e-2d4b6c8e0f13, reference UAT-SEED-CREDIT and one BankTransferPaymentRecorded audit record; HostingFinanceService is not called because it opens its own transaction and rejects future effective times"
  - "The credit is booked one day before the seed instant so a ledger ordered by effective time shows it before the UAT 9 slice"
  - "The real reconciler, slice service, cost-stop service, Program.cs and the AppHost stay unchanged; no prepaid windows were added to other scenarios"

requirements-completed: [MET-05, MET-04]
---

# Phase 67 Plan 22: Funded UAT cost owner Summary

The UAT cost owner now receives one audited 1000.0000 EUR seed credit, so the real cost reconciler funds every seeded possible-cost session with a normal slice instead of cost-stopping it about 7 seconds after startup (closes G-67-1 and G-67-3, one root cause: a UAT seed defect, not a product bug).

## What changed

`UatFleetScenarioSeeder` billed every seeded server to a cost owner with balance 0, and the UAT 9 slice lowered it to -1.30 EUR. The UAT profile runs the real `HostingCostReconciliationWorker`, whose first pass could not keep the 5 EUR safety range, threw `HostingAffordabilityException` and queued an automatic cost-stop for each possible-cost server.

`SeedAsync` now calls a new private `EnsureCreditAsync` before the scenario loop:

- Returns at once when a ledger row with `CreditRecordIdentifier` exists (the unique filtered index `IX_HostingCostLedgerEntries_RecordIdentifier_Unique` also rejects a duplicate under a race).
- Otherwise raises the owner balance by `CreditAmountEur` (1000.0000), adds one `EHostingLedgerEntryKind.BankTransferPayment` entry (reference `UAT-SEED-CREDIT`, a reason, `RecordedByFk` = the UAT owner user, effective and created one day before the seed instant) and one `BankTransferPaymentRecorded` audit record via `new HostingAuditWriter().Add(...)`.

Final values after seeding with the fixed test instant: credit 1000.0000 EUR, costing slice -1.3000 EUR, owner balance 998.7000 EUR. Reseeding books no second credit and no second audit record.

Untouched, proven by the diff guard against `ba78453f` (0 changed lines): `sources/Api/Program.cs`, `sources/AppHost`, `sources/Api/Services`, `sources/Api/HostedServices`, `sources/Database`, `DebugDemoDataSeeder`, `sources/ReactApp`, `sources/WebApp`.

## Tasks

| Task | Name | Commits |
|------|------|---------|
| 1 (tracer) | Real cost reconciler over the seeded UAT fleet queues no cost-stop | `f162e0c3` test (RED), `7cb3d0aa` fix (GREEN) |
| 2 | Audited, never-repeated credit, 15-minute renewal guard, UAT help text | `d64dc8e7` test (RED), `6bc37f63` fix (GREEN), `f3c068a3` chore |

## TDD evidence

### RED 1 (Task 1, before the credit)

Command: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UatFleetScenarioCostReconciliation"`. Target test `FirstReconcilerPass_FundsEverySeededSessionWithoutCostStop(seededMinutesAgo: 0)` failed on the planned assertion (`Assert.Empty` over `LifecycleJobs`):

```
Assert.Empty() Failure: Collection was not empty
Collection: [LifecycleJob { ActionIdentifier = "terminate", ... }, LifecycleJob { ActionIdentifier = "terminate", ... }, ... ]
```

Semantic assessment: the target ran and failed because the unfunded owner made the real reconciler queue `terminate` cost-stop jobs, which is the reported defect. No compile, fixture or unrelated failure.

### RED 2 (Task 2, before the audit record)

`SeedCredit_IsNotBookedAgainOnReseed` failed with `Expected: 1, Actual: 0` (no `BankTransferPaymentRecorded` audit record), and `SeedCredit_IsOneAuditedPaymentBookedBeforeTheCostingSlice` failed with `InvalidOperationException: Sequence contains no elements` on the audit record query. The ledger-entry assertions of both tests were already satisfied by Task 1, so only the audit assertions drove RED. The new `InlineData(15)` case passed at once, as planned (recurrence guard: UAT 9 is renewed, not cost-stopped, with a seven-slice pass).

### GREEN

`UatFleetScenario` filter: 16 passed, 0 failed. `GameServerHosting` filter: 1187 passed, 0 failed, 0 skipped. `dotnet build X39.UnitedTacticalForces.slnx`: succeeded (54 pre-existing warnings).

## Acceptance criteria

All pass: BankTransferPayment kind in seeder = 1; `CreditRecordIdentifier` = 3 occurrences; old -1.3000 owner-balance asserts = 0; `998.7000m` = 2; `Assert.Equal(-1.3000m, entry.Amount)` = 1; `new HostingCostReconciler(` = 1; `BankTransferPaymentRecorded` in seeder = 1; `InlineData(15)` = 1; `SeedCredit_` = 2; `UAT-SEED-CREDIT` in `Start-Uat.ps1` = 1; prohibited-path diff guard (plan-level, includes ReactApp and WebApp) = 0.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None. The credit is reachable only through `UatFleetScenarioSeeder.SeedAsync`, whose gate in `Program.cs` (Development plus `Testing:UatScenarios:Enabled`) is unchanged (T-67-43, T-67-44, T-67-45, T-67-46 mitigated as planned).

## Human re-check (UAT tests 1 and 3)

1. Start from the repository root in PowerShell: `./scripts/Start-Uat.ps1 -Fresh`. Stop everything with Ctrl+C in that terminal.
2. Open http://localhost:5173/next/command/servers and wait about one minute (the cost reconciler runs every 30 s).
3. Expected for test 3: only "UAT 1 Off (no deployment)" and "UAT 8 Off (terminated)" show as off. "UAT 2 Running" is running, and its tiles show the worker sentence. "UAT 1 Off (no deployment)" shows the not-running sentence and "No readings in this time range."
4. Expected for test 1: open "UAT 10 Metrics", wait until the tiles show values, then choose "Shut down and stop billing" without reloading and confirm. Once the stop completes, every tile reads "Unavailable" with "The game is not running, so there is nothing to measure.", with no number and no running-since time.
5. Optional: after 15 minutes, "UAT 9 Costing (accrued cost)" is still running.

## Self-Check: PASSED

- Created file present: `tests/Api.Tests/GameServerHosting/UatFleetScenarioCostReconciliationTests.cs`.
- Commits present on the branch: `f162e0c3`, `7cb3d0aa`, `d64dc8e7`, `6bc37f63`, `f3c068a3`.
- `git rev-list --count c879aa7c..HEAD` = 5, matching `commits: 5`.

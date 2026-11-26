---
phase: 66-accrued-cost-cost-alerts
plan: 03
subsystem: api
tags: [hosting, cost-alerts, evaluator, outbox, efcore, migration, hosted-service, dotnet]

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 ICostAccrualService.CalculateForDeploymentsAsync (accrued amount per deployment)
provides:
  - "CostAlertSetting/CostAlertThreshold: per cost owner channel flags and absolute EUR thresholds"
  - "CostAlert: once-only crossing record, unique on (DeploymentFk, ThresholdAmount)"
  - "CostAlertDelivery: per channel and recipient outbox row with lease and retry columns"
  - "CostAlertDismissal: per-viewer dismissal (unique per alert and user)"
  - "User.Locale (en/de) for message rendering in plans 66-06 and 66-07"
  - "ICostAlertEvaluator/CostAlertEvaluator: records alert plus delivery intents in one transaction"
  - "ICostAlertRecipientResolver: personal owner -> own user, group owner -> active co:use holders"
  - "CostAlertWorker hosted loop (30 s) registered only while the control plane is unpaused"
  - "CostAlertOptions (Hosting:CostAlerts) with Validate()"
  - "AddCostAlerts migration (tool-generated), snapshot in sync"
affects: [66-04, 66-05, 66-06, 66-07, 66-08, cost-alert-dispatcher, cost-alert-publisher]

plan_head_before: 46054ada94e71b94707bab40c0bc7342dcfbcd8c
plan_head_after: a74eab2ad15c688bd2a8b252087e80c2cd161e04

actuals:
  tokens: 21650
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Persist intent first: crossing and per-channel delivery rows inserted in one transaction before any side effect"
    - "Unique index as the once-only guard; DbUpdateException re-checked against recorded crossings before it is treated as a concurrent pass"
    - "Decimal comparisons done in memory (SQLite stores decimals as text)"

key-files:
  created:
    - sources/Database/Hosting/CostAlertSetting.cs
    - sources/Database/Hosting/CostAlertThreshold.cs
    - sources/Database/Hosting/CostAlert.cs
    - sources/Database/Hosting/CostAlertDismissal.cs
    - sources/Database/Hosting/CostAlertDelivery.cs
    - sources/Database/Hosting/ECostAlertChannel.cs
    - sources/Database/Hosting/ECostAlertDeliveryStatus.cs
    - sources/Database/Migrations/20261121143229_AddCostAlerts.cs
    - sources/Database/Migrations/20261121143229_AddCostAlerts.Designer.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertOptions.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertDeliveryReasons.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertRecipient.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertRecipientResolver.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertRecipientResolver.cs
    - sources/Api/Services/GameServerLifecycle/DeploymentAccrual.cs
    - sources/Api/Services/GameServerLifecycle/RaisedCostAlert.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertEvaluationResult.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertEvaluator.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertEvaluator.cs
    - sources/Api/HostedServices/CostAlertWorker.cs
    - tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertRecipientResolverTests.cs
  modified:
    - sources/Database/Hosting/CostOwner.cs
    - sources/Database/Authority/User.cs
    - sources/Database/DatabaseContext.cs
    - sources/Database/Migrations/DatabaseContextModelSnapshot.cs
    - sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs
    - sources/Api/Program.cs
    - sources/Api/appsettings.json
    - tests/Api.Tests/GameServerHosting/HostingTestData.cs
    - tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs

key-decisions:
  - "One alert per (deployment, threshold): several thresholds crossed in one pass create one alert each"
  - "No settings row, an empty threshold list, or a non-positive threshold never raises an alert"
  - "A threshold equal to or below the current accrued cost counts as reached"
  - "Delivery rows: Skipped no-recipient with a null recipient per channel when nobody resolves (takes priority over channel flags); otherwise per recipient Skipped channel-disabled, not-linked or no-address, else Pending with TimeStampNextAttempt = now"
  - "Group owners resolve to active co:use holders only (direct or via role); co:view holders and finance staff are not messaged"
  - "On DbUpdateException the pass re-reads recorded crossings and swallows the error only when one of its own crossings now exists; any other failure is rethrown"
  - "Evaluator never reads channel configuration; the dispatcher checks SMTP and bot availability at send time"

patterns-established:
  - "Candidate set = unresolved deployments that may cost plus deployments resolved within ResolvedLookback; amount null (no slice yet) is ignored"

requirements-completed: [CST-04, CST-05, CST-06]

coverage:
  - id: D1
    description: "A costing session reaching an owner's thresholds records exactly one CostAlert per threshold with Pending delivery rows per channel for the owner user, in one transaction"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#CostingSession_ReachingThreshold_RecordsOneAlertWithDeliveries"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#SeveralThresholdsCrossedInOnePass_CreateOneAlertEach"
        status: pass
    human_judgment: false
  - id: D2
    description: "No settings row or an empty threshold list never alerts; Skipped delivery rows carry reason codes and never suppress the alert"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#OwnerWithoutSetting_NeverAlerts"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#DeliveryRows_FollowChannelFlagsAndRecipientReachability"
        status: pass
    human_judgment: false
  - id: D3
    description: "Crossings are recorded once across restart, duplicate passes and a concurrent pass; a new session starts with fresh crossings"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#EvaluatingAgainAfterRestart_RecordsNothingNew"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#CrossingCommittedByConcurrentPass_IsNotRecordedTwice"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#NewSessionOfSameServer_RecordsFreshCrossing"
        status: pass
    human_judgment: false
  - id: D4
    description: "Recipients follow the owner type: personal owner to its user, group owner to active co:use holders only (direct or role); view-only, banned and deleted users excluded; nobody resolves to Skipped no-recipient rows"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertRecipientResolverTests.cs"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs#OwnerWithoutRecipient_StoresOneSkippedRowPerChannel"
        status: pass
    human_judgment: false
  - id: D5
    description: "CostAlertWorker is registered only while the control plane is unpaused; the migration is tool-generated and the snapshot matches the model"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs#ExplicitSetting_ControlsBackgroundServiceRegistration"
        status: pass
      - kind: other
        ref: "dotnet ef migrations has-pending-model-changes (exit 0)"
        status: pass
    human_judgment: false
  - id: D6
    description: "The worker loop itself (30 s cadence, restart recompute against a real PostgreSQL database) is not exercised by an automated test; the migration was not applied to a PostgreSQL instance"
    requirement: "CST-04"
    verification: []
    human_judgment: true

duration: about 30min
completed: 2026-11-21
status: complete
---

# Phase 66 Plan 03: Durable Cost Alert Crossings Summary

**A threshold crossing is now a once-only durable fact: per-owner thresholds, an evaluator that stores each alert and its per-channel delivery intents in one transaction behind a unique (deployment, threshold) index, the tool-generated migration, and a hosted worker that evaluates every 30 s while the control plane is active.**

## Performance

- **Duration:** about 30 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 22 created, 9 modified

## Accomplishments

- Tracer slice end to end: five entities and two enums, `User.Locale`, evaluator, personal-owner resolver and tests; 13 evaluator tests green.
- Schema gate: `dotnet ef migrations add AddCostAlerts` produced exactly five `CreateTable` calls and one `AddColumn` (`Users.Locale`); `has-pending-model-changes` exits 0.
- Group-owner recipients (Verein, external organization) as active direct or role `co:use` holders; `CostAlertWorker` wired into `AddHostingControlPlane` with `Hosting:CostAlerts` options.

## Task Commits

1. **Task 1a: Entities** - `bb781b68` (feat, database)
2. **Task 1b: Evaluator, resolver, options, tests** - `1844cd7f` (feat, api)
3. **Task 2: Migration** - `a5ad70c8` (feat, database) - files `20261121143229_AddCostAlerts.cs` and `20261121143229_AddCostAlerts.Designer.cs`
4. **Task 3: Worker, group recipients, registration** - `a74eab2a` (feat, api)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 837 passed, 0 failed.
- Task 3 slice (`CostAlertEvaluatorTests`, `CostAlertRecipientResolverTests`, `HostingControlPlaneStateTests`, `HostingControlPlanePausedStartupTests`): 31 passed.
- `dotnet ef migrations has-pending-model-changes ...`: exit 0, "No changes have been made to the model since the last migration."
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- `git ls-files -- "sources/Database/Migrations/*_AddCostAlerts*"` lists exactly the migration and its designer; no file under `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker` was touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] `DbUpdateException` is rethrown unless it is a concurrent crossing**
- **Found during:** Task 1
- **Issue:** The plan said to treat any `DbUpdateException` as "already recorded". That would hide a genuine failure such as a foreign key violation as a Debug log line.
- **Fix:** After rollback the evaluator re-reads the recorded crossings and swallows the exception only when one of its own selected crossings now exists; otherwise it rethrows. A test commits the competing crossing between the evaluator's read and its insert (via a decorated resolver) and asserts exactly one alert and no duplicate deliveries.
- **Files modified:** `CostAlertEvaluator.cs`, `CostAlertEvaluatorTests.cs`
- **Commit:** `1844cd7f`, `a74eab2a`

**2. [Rule 2 - Missing critical] Non-positive thresholds are ignored**
- **Found during:** Task 1
- **Issue:** A stored threshold of 0 (or less) would alert immediately on any costing session. Input validation belongs to plan 66-06, but the evaluator must not depend on it.
- **Fix:** The evaluator skips thresholds `<= 0`.
- **Files modified:** `CostAlertEvaluator.cs`
- **Commit:** `1844cd7f`

**Total deviations:** 2 auto-fixed (both Rule 2). No scope change.

## Notes for Dependent Plans

- `ICostAlertRecipientResolver`, `ICostAlertEvaluator`, `CostAlertRecipient`, `DeploymentAccrual`, `RaisedCostAlert` and `CostAlertEvaluationResult` are `internal`; the test assembly sees them through the existing internals access.
- `CostAlertEvaluator` calls `ChangeTracker.Clear()` after a recorded pass, so it must run in its own scope (as the worker does).
- `Hosting:CostAlerts` in `appsettings.json` has only scalar keys on purpose (no array keys, so the configuration binder cannot append to the default `DeliveryRetryDelays`).
- The migration was not applied to a PostgreSQL instance here; it is generated by `dotnet-ef` 10.0.9 (global tool updated from 10.0.5 as decided by the orchestrator) and checked for model sync only.

## Known Stubs

None.

## Threat Flags

None. The surfaces added are those of the plan's threat model (T-66-08 unique index plus transaction plus restart test, T-66-09 one alert per threshold and session, T-66-10 co:use-only recipients with banned, deleted and view-only tests, T-66-11 own hosted worker and transaction).

## Issues Encountered

None beyond the deviations above.

## Self-Check: PASSED

- All 22 created files and 9 modified files exist (verified by the successful build and test run).
- Commits `bb781b68`, `1844cd7f`, `a5ad70c8`, `a74eab2a` are ancestors of HEAD.

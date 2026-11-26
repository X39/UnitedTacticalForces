---
phase: 68-auto-stop-schedules
plan: 03
subsystem: api
tags: [uat, worker-emulator, worker-channel, lifecycle, testing]
status: complete
requires:
  - phase: 67
    provides: worker channel, UAT worker emulator, emulated Hetzner provider
provides:
  - UAT worker emulator that claims commands and acknowledges stop and fault-stop over the real worker channel
  - EUatPlayerProfile and UatFleetScenarioSeeder.EmulatedWorkers map (one emulated session per listed server)
  - UatEmulatedCommandReports report builders with the fixed uat-emulated-unsupported-action code
affects: [68-19 automation UAT scenarios, seeded UAT check 67-7]
tech-stack:
  added: []
  patterns:
    - single-sender session loop with a separate receive loop correlating Ack frames by id
    - file-backed SQLite (WAL, one connection per context) for tests that run control plane and emulator concurrently
key-files:
  created:
    - sources/Api/HostedServices/UatEmulatedCommandReports.cs
    - sources/Api/Data/EUatPlayerProfile.cs
  modified:
    - sources/Api/HostedServices/UatWorkerChannelEmulator.cs
    - sources/Api/HostedServices/UatWorkerChannelEmulatorOptions.cs
    - sources/Api/Data/UatFleetScenarioSeeder.cs
    - tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs
key-decisions:
  - "The emulator acknowledges only stop and fault-stop; every other action is reported failed with uat-emulated-unsupported-action"
  - "A session ends after its stop succeeded so the deployment can resolve; other sessions are unaffected"
  - "The emulated-worker map is exposed to tests through an internal init property, not a constructor overload"
requirements-completed: [AUT-05]
duration: ~1h
completed: 2026-11-22
commits: 4
plan_head_before: a3d6871108e7d2338fa89fddef179e6898e5344d
plan_head_after: 97139ade7db0af50609205d25ff2c06f08ab3278
actuals:
  tokens: 40000
  tasks: 2
  commits: 4
---

# Phase 68 Plan 03: UAT worker emulator acknowledges stop commands Summary

**The Development-only UAT worker emulator now claims commands through the real worker channel and acknowledges stop and fault-stop, so a Stop job of an emulated server reaches the emulated provider's verified non-billable release; it also drives one session per seeded server with a per-scenario player profile.**

## What was built

- **Tracer (Task 1).** `UatWorkerChannelEmulator` was restructured so one task owns sending on the client socket. A `SessionChannel` receive loop assembles server messages, completes the pending request whose id an Ack echoes and signals Wake frames. The session loop sends heartbeats and metrics on their timers and a Claim frame (payload `WorkerCommandClaimRequest`) on Wake or every `ClaimPollInterval` (new option, 5 s default). A claimed command is reported `ReportReceived` and then `ReportSucceeded` (stop, fault-stop: every payload workload `Stopped`, `ProcessId` null) or `ReportFailed` with `uat-emulated-unsupported-action`. After an accepted stop success the session closes its client end and ends; no further heartbeat or metrics frame is sent. `UatEmulatedCommandReports` holds the report builders.
- **Several workers (Task 2).** `EUatPlayerProfile` (`Synthetic`, `NoPlayers`, `FewPlayers`) and `UatFleetScenarioSeeder.EmulatedWorkers` (only the metrics scenario, `Synthetic`; plan 68-19 adds the automation titles). `PrepareAsync` resolves one session per listed title (own credential, own loopback pair), sessions run with `Task.WhenAll`. Metrics keep the synthetic CPU and memory series; players come from the profile (0, 3 or the synthetic series). A listed title without a running deployment or primary worker is skipped with one information line while others run; with no session at all there is exactly one information line.
- **Guarantees kept.** No game process, every claim and report enters through `IWorkerChannelHandler` and `IWorkerProtocolService`, the emulator never throws out of `ExecuteAsync`, registration stays only in `AddTo` (Development plus `Testing:UatScenarios:Enabled`), credentials are never logged. No file under `sources/Api/Services/WorkerProtocol`, `LifecycleJobExecutor.cs`, `sources/Database` or `sources/CloudWorker` changed.

## Tests

`UatWorkerChannelEmulatorTests` grew from 5 to 12 test methods (16 cases with the theory): claimed stop succeeds through the channel and the session ends, unsupported action fails with the fixed code, a real `LifecycleJobExecutor` Stop job with `EmulatedHetznerRuntimeProvider` completes (deployment `Stopped`, `PossibleCost` false, resolved, `emu-server-` resource `Released`), several servers with their profiles, skipped titles, empty map, and one stopped session leaving the other running. The filter `UatWorkerChannelEmulatorTests|UatFleetScenarioSeederTests|UatFleetScenarioCostReconciliationTests` passes (32 tests); the emulator tests were run four times without a flake. `dotnet build X39.UnitedTacticalForces.slnx` succeeds with 0 errors.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Concurrent tests need separate connections**
- **Found during:** Task 1 (executor-level test)
- **Issue:** `WorkerChannelTestHost` registers one singleton `DatabaseContext` on one in-memory SQLite connection. Running the executor and the emulator concurrently on it would hit "second operation on this context" and nested-transaction errors (a status written inside an uncommitted protocol transaction is visible to the executor on a shared connection).
- **Fix:** The tests that run both sides concurrently use a small file-backed SQLite fixture (`SharedFileDatabase`, WAL, one connection per context) and replace the host's context with a scoped factory. Tests with a single actor keep the in-memory store.
- **Files modified:** tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs
- **Commit:** 2b05bf55, 97139ade

**2. [Rule 2 - Missing critical] Observable report log line**
- **Found during:** Task 1
- **Issue:** Tests (and UAT operators) had no way to see that a command was fully reported without polling a context the handler is using.
- **Fix:** One information line per fully reported command (`UAT worker emulation reported command ... as ...`), carrying only ids, the action and an outcome code.
- **Files modified:** sources/Api/HostedServices/UatWorkerChannelEmulator.cs
- **Commit:** 69d35abf

### Plan wording adapted

- The executor test builds the Stop `LifecycleJob` directly (Running, `IsProvisioningBlocked` set as enqueueing a Stop does) instead of calling `EnqueueActionAsync`, because the lifecycle service helper is wired to the test game catalog while the UAT servers are Arma 3 servers. `LifecycleJobExecutor.ExecuteAsync` does not set the job to Succeeded (the job queue does), so the test asserts that the executor returns and the persisted deployment, resource and command outcomes instead of a job status.
- The test seam for the emulated-worker map is an internal `init` property (`EmulatedWorkers`) defaulting to `UatFleetScenarioSeeder.EmulatedWorkers`, one of the two options the plan allowed.
- The "NoPlayers has no players reason" check asserts `PlayersUnavailableReason` is null on the stored snapshot.

## Known Stubs

None.

## Threat Flags

None. The plan added no endpoint, auth path or trust boundary; threat T-68-05 (diff gate on WorkerProtocol and the executor, registration only through `AddTo`) and T-68-06 (no credential in logs) are honoured.

## Seeded-stack follow-up

Per the plan, confirming seeded check 67-7 (`npm --prefix sources/ReactApp run test:uat`) against the full stack is done by plan 68-19; it was not run here because the full stack must not be started from this worktree.

## Self-Check: PASSED

- Created files present: UatEmulatedCommandReports.cs, EUatPlayerProfile.cs, this SUMMARY.
- Commits present on the branch: 69d35abf, 2b05bf55, 36da6568, 97139ade.
- Acceptance greps: `EWorkerChannelFrameType.Claim` and `ReportSucceeded` present in the emulator, `uat-emulated-unsupported-action` once in the report builders, `MetricsScenarioTitle` absent from the emulator, `enum EUatPlayerProfile` once, `EmulatedWorkers` in the seeder, test method count 12, no diff against the base in `Services/WorkerProtocol`, `LifecycleJobExecutor.cs`, `sources/Database` or `sources/CloudWorker`.

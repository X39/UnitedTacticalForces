---
phase: 67-server-metrics
plan: 13
subsystem: api
tags: [metrics, uat, seeder, worker-channel, emulation]

requires:
  - phase: 67-07
    provides: GetHistoryAsync and the tier resolutions
  - phase: 67-11
    provides: worker channel signal and Program wiring
  - phase: 67-12
    provides: MetricsFrameRoute, the only metrics entry point
provides:
  - "UAT 10 Metrics" scenario with an online primary worker and seven days of tiered history (stopped period, outage gap, CPU spike)
  - UatWorkerChannelEmulator, a Development-only emulated worker that drives the real channel handler over a loopback WebSocket
  - UatWorkerChannelEmulator.AddTo, the single registration gate (Development plus Testing:UatScenarios:Enabled)
affects: [67-14, 67-16, UAT of server metrics]

plan_head_before: b3873ee6346e3b78bdc49266610e1aaa02779648
plan_head_after: 99be01d81cf11a89b4ee27bdbfba0a8560d4005a
commits: 4

actuals:
  tokens: 40000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "History is seeded directly like the Phase 66 cost slice; live values enter only through Metrics frames handled by the worker channel handler"
    - "A pure synthetic series (UatMetricHistorySeeder.SampleAt) is shared by the seeded history and the live emulation, so they continue each other"

key-files:
  created:
    - sources/Api/Data/UatMetricHistorySeeder.cs
    - sources/Api/HostedServices/UatWorkerChannelEmulator.cs
    - sources/Api/HostedServices/UatWorkerChannelEmulatorOptions.cs
    - sources/Api/HostedServices/UatLoopbackWebSocketPair.cs
    - tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs
  modified:
    - sources/Api/Data/UatFleetScenarioSeeder.cs
    - sources/Api/Program.cs
    - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs

key-decisions:
  - "The outage gap (now - 40 min to now - 30 min) is left out of all three tiers, not only the 15 s and 60 s tiers, because a real outage produces no samples for any tier"
  - "The emulator catches unexpected failures, logs one sanitized warning and ends, so a development aid can never stop the API host"
  - "Registration lives in UatWorkerChannelEmulator.AddTo so the Development-plus-flag condition is unit-testable without building the whole host"

patterns-established:
  - "Gate-based test of a channel handler: a double IUpdateStreamService parks the first publication, leaving the shared test DbContext idle while it is inspected"

requirements-completed: [MET-04]

coverage:
  - id: D1
    description: "A UAT reseed seeds 'UAT 10 Metrics' as a running possible-cost server with an online primary worker and seven days of tiered history with a stopped period, an outage gap and a preserved CPU spike"
    requirement: "MET-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs#MetricsScenario_WeekHistoryHasNoPointInTheStoppedPeriod"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs#MetricsScenario_DayHistoryShowsTheShortOutageGap"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs#MetricsScenario_SpikeSurvivesDownsamplingWhileAveragesStayPlain"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs#MetricsScenario_SeedingTwiceCreatesNoDuplicates"
        status: pass
    human_judgment: false
  - id: D2
    description: "The emulated worker feeds heartbeats and metrics through the real channel handler, ends by itself when the channel refuses it, and never runs without the scenario, while paused, or outside Development with the flag"
    requirement: "MET-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs#Emulation_FeedsTheServerThroughTheChannelHandler"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs#Emulation_EndsByItselfWhenTheDeploymentIsResolved"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs#PausedControlPlane_CompletesWithoutConnecting"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs#Registration_RequiresDevelopmentAndTheUatFlag"
        status: pass
    human_judgment: false
  - id: D3
    description: "Whether the live tiles and the growing 1 hour chart look right to a person is judged in the end-of-phase UAT"
    requirement: "MET-04"
    verification: []
    human_judgment: true
    rationale: "Visual adequacy of the charts is not asserted by any test; the data and channel path behind them are"

duration: about 40min (resumed once after an orchestrator crash)
completed: 2026-11-22
status: complete
---

# Phase 67 Plan 13: UAT Metrics Seed and Emulated Worker Summary

**A UAT reseed now shows "UAT 10 Metrics" with a week of tiered history (stopped period, outage gap, CPU spike), and a Development-only emulated worker feeds it live heartbeats and metrics through the real worker channel handler.**

## Performance

- **Duration:** about 40 min including one resume after an orchestrator crash (Task 1 commits and two untracked Task 2 files survived and were reused)
- **Completed:** 2026-11-22
- **Tasks:** 2 (tracer plus emulator)
- **Files:** 8 (5 created, 3 modified)

## Accomplishments
- `UatFleetScenarioSeeder` gained the `UAT 10 Metrics` scenario: running possible-cost deployment, resource, and an online `game-server` ordinal 0 worker (process 4242, started 90 min before seeding, heartbeat at seeding).
- `UatMetricHistorySeeder` writes about 4 000 deterministic rows: 300 s for 7 d, 60 s for 26 h, 15 s for 2 h, a 3 h stopped period two days ago, a 10 min outage 40 minutes ago, and a CPU spike (maximum 95, average below 60) in the 60 s and 300 s windows five hours ago.
- `UatWorkerChannelEmulator` issues a fresh pending credential per API start, authenticates through the protocol service, runs `IWorkerChannelHandler.RunAsync` on one end of a loopback WebSocket pair and sends Heartbeat and Metrics frames from the other end (10 s and 15 s by default). It ends when the channel closes (for example after the server was shut down and the deployment resolved).
- `Program.cs` registers it only through `UatWorkerChannelEmulator.AddTo` (Development plus `Testing:UatScenarios:Enabled`); a paused control plane skips it.

## Task Commits

1. **Task 1: Tracer** (verified end-to-end: 12 seeder tests pass)
   - `6169302e` feat(api): seed a UAT server with seven days of metric history
   - `f8fe9746` test(api): cover the UAT metrics scenario
2. **Task 2: Emulated worker**
   - `ab86d8bf` feat(api): emulate a UAT worker on the real metrics channel
   - `99be01d8` test(api): cover the UAT worker emulation

## UAT start instructions

```powershell
./scripts/Start-Uat.ps1 -Fresh   # rebuild and reseed; Ctrl+C stops
./scripts/Start-Uat.ps1          # restart without reseeding (a new emulator credential is issued each start)
```
Open the fleet at http://localhost:5173/next/command/servers and select "UAT 10 Metrics".

## Decisions Made
See `key-decisions` above.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Seeded worker violated a database check constraint**
- **Found during:** Task 1
- **Issue:** `BootstrapCredentialConsumedAt` without `BootstrapCredentialExpiresAt` fails `CK_LifecycleWorkers_BootstrapConsumption`.
- **Fix:** The seeded worker also gets `BootstrapCredentialExpiresAt = now`.
- **Files modified:** `sources/Api/Data/UatFleetScenarioSeeder.cs`
- **Commit:** `6169302e`

**2. [Rule 1 - Bug] Planned 50 ms test intervals would trip the channel rate limit**
- **Found during:** Task 2 design
- **Issue:** Heartbeat plus metrics every 50 ms is about 40 frames per second, above `ChannelMaxFramesPerSecond` (20), so the handler would close the channel.
- **Fix:** The emulator tests use 200 ms intervals.
- **Files modified:** `tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs`
- **Commit:** `99be01d8`

**3. [Plan refinement] Outage gap applied to all three tiers**
- The plan named only the 15 s and 60 s tiers; the 300 s tier also omits the two windows, because a real outage yields no samples for any tier.

**Total deviations:** 2 auto-fixed bugs, 1 refinement. **Impact:** none on scope or behavior contract.

Note on TDD: the new seeder, emulator and registration helper did not exist before the tests, so a compile-failing RED commit was not possible without breaking the build. Production and tests were committed in the plan's order; tests passed on first run after the check-constraint fix.

## Issues Encountered
The orchestrator session crashed mid-plan; the repository state (two Task 1 commits, two untracked Task 2 files) was verified and the work continued without redoing anything.

## Known Stubs
None. The emulator is intentionally synthetic and UAT-only; it executes no game process.

## Threat Flags
None beyond the plan register. T-67-28 is mitigated by `AddTo` (Development plus flag), the paused-control-plane skip and the registration theory test; T-67-29 by a per-start in-memory credential that is never logged and authenticates through the normal service.

## Next Phase Readiness
Ready for the remaining Phase 67 plans; the metrics UAT can run from a reseed without a real cloud machine.

## Self-Check: PASSED

- All 5 created files exist; commits `6169302e`, `f8fe9746`, `ab86d8bf` and `99be01d8` are ancestors of HEAD (`git rev-list --count` over the plan range is 4).
- Acceptance: "UAT 10 Metrics" in the seeder 1; test cases 12 (seeder) and 5 (emulator); `UatWorkerChannelEmulator` in Program 1; `IWorkerChannelHandler` in the emulator 2.
- `dotnet test --filter FullyQualifiedName~Uat|WorkerChannelMetricsTests`: 51 passed twice, 0 failed; `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors; no commit touches `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker`.

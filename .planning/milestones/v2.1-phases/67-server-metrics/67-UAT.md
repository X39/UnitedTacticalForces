---
status: complete
phase: 67-server-metrics
source: [67-VERIFICATION.md]
started: 2026-11-22T17:10:00Z
updated: 2026-11-24T21:01:27.801Z
---

## How to run

Start (fresh seed, from the repository root in PowerShell):

```powershell
./scripts/Start-Uat.ps1 -Fresh
```

Open http://localhost:5173/next/command/servers. Stop the stack with Ctrl+C in the terminal that runs the script.

Tests 5 and 6 need real game servers or a deployed API and real worker. Skip them when that setup is not at hand; a skip does not block the phase.

## Current Test

[testing complete]

## Tests

### 1. Stop and not-running state (UAT step 7), re-run after the 67-21 fix
expected: Open 'UAT 10 Metrics', wait for values, then choose 'Shut down and stop billing' without reloading. Once the stop completes, every tile reads Unavailable with 'The game is not running, so there is nothing to measure.', with no number and no running-since time.
result: issue
reported: "UAT 10 is not shown as on"
severity: major

### 2. Live tiles and history on 'UAT 10 Metrics'
expected: Running since, Players, CPU and Memory change within about 15 s. The 1 hour, 24 hours and 7 days charts grow at the right edge, gaps show as breaks (never zero), the tables view works, and the same history shows after a reload and after an API restart.
result: pass
note: "User: pass, tho UAT10 is shown, again, as off, and we hence have a gap in the data here (same root cause as G-67-1)"

### 3. No-worker and off servers
expected: 'UAT 2 Running' shows the worker sentence on every tile. 'UAT 1 Off (no deployment)' shows the not-running sentence and 'No readings in this time range.'
result: issue
reported: "all UAT servers but 7, 9 and 5 are shown as off"
severity: major

### 4. Design review at full width and 390 px, English and German
expected: Layout acceptable (the UI review was code-only, 18/24).
result: pass

### 5. Real game player counts (optional)
expected: Arma 3, Counter-Strike 2 (RCON status text and A2S on the game port) and ASKA (A2S on port 27016). A recognised reply shows the count; an unrecognised reply reads 'The reading failed. It will be tried again.' and never a guessed count.
result: skipped
reason: "Deferred follow-up: pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"

### 6. Real worker through nginx (optional)
expected: Running a real worker against a deployed API, the ClientWebSocket upgrade with the worker header reaches the tiles.
result: skipped
reason: "Deferred follow-up: pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"

### 7. UAT test 1 re-check after 67-21 and 67-22 (G-67-1): seeded servers keep their state, then the stop path
expected: Start with ./scripts/Start-Uat.ps1 -Fresh, open http://localhost:5173/next/command/servers and wait about one minute (the cost reconciler runs every 30 s). 'UAT 10 Metrics' is shown as running and live samples arrive. Choose 'Shut down and stop billing' without reloading and confirm. Once the stop completes, every tile reads Unavailable with 'The game is not running, so there is nothing to measure.', with no number and no running-since time.
result: skipped
reason: "Deferred follow-up: automated run of sources/ReactApp/tests/uat/metrics.spec.ts (2026-11-22) confirmed UAT 10 Metrics is shown running with live samples, but the stop never completes on the UAT stack: UatWorkerChannelEmulator does not claim worker commands, so the Stop job waits at worker-stop-queued. This is a UAT harness gap, not a metrics defect; the emulator command handling is planned in phase 68. Re-run the spec at milestone end."
reason: "Deferred follow-up: user deferred the seeded-stack re-check to the end of the milestone"

### 8. UAT test 3 re-check after 67-22 (G-67-3): only the intentionally off servers show as off
expected: Same start as test 7. Only 'UAT 1 Off (no deployment)' and 'UAT 8 Off (terminated)' are shown as off. 'UAT 2 Running' is running and every tile shows the worker sentence. 'UAT 1 Off (no deployment)' shows the not-running sentence and 'No readings in this time range.'. Optional: after 15 minutes 'UAT 9 Costing (accrued cost)' is still running.
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/metrics.spec.ts on the real seeded stack (2026-11-22): only UAT 1 Off (no deployment) and UAT 8 Off (terminated) are in the Off section; UAT 2 Running tiles show the worker sentence; UAT 1 tiles show the not-running sentence and all three charts read No readings in this time range."
reason: "Deferred follow-up: user deferred the seeded-stack re-check to the end of the milestone"

### 9. UAT test 7 re-run after the emulator rework (36da6568, 69d35abf): the stop path on the seeded stack
expected: Same start as test 7. After about one minute 'UAT 10 Metrics' is shown as running with live samples. Choose 'Shut down and stop billing' without reloading and confirm. Once the stop completes, every tile reads Unavailable with 'The game is not running, so there is nothing to measure.', with no number and no running-since time. The other seeded servers stay running (the emulator sessions are isolated).
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/metrics.spec.ts on a fresh seeded stack (2026-11-24): UAT 10 Metrics Running, tiles changed live (CPU 32% -> 33%); after 'Shut down and stop billing' every tile reads Unavailable with the not-running sentence, no number, no running-since time, state badge OFF; Off section afterwards is exactly UAT 1, UAT 8 and UAT 10, UAT 2 Running stays Active."


### 10. UAT test 2 repeat after the emulator rework: live tiles and history on 'UAT 10 Metrics'
expected: Running since, Players, CPU and Memory change within about 15 s. The 1 hour, 24 hours and 7 days charts grow at the right edge, gaps show as breaks (never zero), the tables view works, and the same history shows after a reload and after an API restart.
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/metrics.spec.ts (2026-11-24): live tiles changed within 90 s; 1 hour, 24 hours and 7 days each draw 3 charts with line runs (6/6/9 runs, so gaps are breaks); tables view shows 3 tables (2376 value cells); history still drawn after a reload and, with UAT_PHASE=restarted after restarting the stack without -Fresh, in every range and in the tables. Screenshot after restart: CPU and Memory lines break around 9:15-9:25 PM with no drop to zero and reach the right edge."


## Summary

total: 10
passed: 5
issues: 2
pending: 0
skipped: 3
blocked: 0

## Gaps

- gap_id: G-67-1
  truth: "'UAT 10 Metrics' is seeded as a running server, so the stop flow can be exercised and every tile then reads Unavailable with the not-running sentence"
  status: resolved
  resolved_by: 67-22-PLAN.md
  reason: "User reported: UAT 10 is not shown as on"
  severity: major
  test: 1
  root_cause: "UAT profile runs the real HostingCostReconciliationWorker (Hosting__ControlPlane__Paused=false), but UatFleetScenarioSeeder bills every seeded server to a debug cost owner with balance 0, no allowance (UAT 9 seed drives it to -1.30 EUR). First reconciler pass (~7s after start) cannot authorize the next slice and queues an automatic cost-stop (cost-owner-credit-exhausted); emulated provider confirms release, deployment ends TerminatedDueToFault and the fleet shows off. UAT harness/seed bug, not a product bug."
  artifacts:
    - path: "sources/Api/Data/UatFleetScenarioSeeder.cs"
      issue: "Seeded servers billed to an owner with no credit; only UAT 9 has a prepaid window, which pushes balance negative"
    - path: "sources/Api/Data/DebugDemoDataSeeder.cs"
      issue: "EnsureDebugCostOwnersAsync creates owners with balance 0 and no allowance"
    - path: "sources/AppHost/Program.cs"
      issue: "UAT block unpauses control plane, starting the cost reconciler"
    - path: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs"
      issue: "Asserts owner.Balance == -1.3000m, locking in the insolvent seed"
  missing:
    - "Seed an idempotent, auditable credit for the UAT cost owner well above the 5 EUR floor plus several hours of slices for all seeded servers (no re-credit on reseed)"
    - "Optionally give seeded running/transitional servers a current prepaid window like UAT 9"
    - "Update CostingScenario_ChargesOneBackdatedSliceAgainstTheOwnerBalance for the new balance"
    - "Regression test: seed UAT scenarios, run HostingCostReconciler.ReconcileAsync once, assert no automatic cost-stop and UAT 2,3,4,6,7,10 keep seeded status"
    - "Keep the real cost reconciler active in the UAT profile (no no-op swap)"
  debug_session: ".planning/debug/uat-seeded-servers-shown-off.md"

- gap_id: G-67-3
  truth: "Seeded servers keep their seeded state: 'UAT 2 Running' shows as running (with the worker sentence on every tile) and only the intentionally off servers show as off"
  status: resolved
  resolved_by: 67-22-PLAN.md
  reason: "User reported: all UAT servers but 7, 9 and 5 are shown as off"
  severity: major
  test: 3
  root_cause: "UAT profile runs the real HostingCostReconciliationWorker (Hosting__ControlPlane__Paused=false), but UatFleetScenarioSeeder bills every seeded server to a debug cost owner with balance 0, no allowance (UAT 9 seed drives it to -1.30 EUR). First reconciler pass (~7s after start) cannot authorize the next slice and queues an automatic cost-stop (cost-owner-credit-exhausted); emulated provider confirms release, deployment ends TerminatedDueToFault and the fleet shows off. UAT harness/seed bug, not a product bug."
  artifacts:
    - path: "sources/Api/Data/UatFleetScenarioSeeder.cs"
      issue: "Seeded servers billed to an owner with no credit; only UAT 9 has a prepaid window, which pushes balance negative"
    - path: "sources/Api/Data/DebugDemoDataSeeder.cs"
      issue: "EnsureDebugCostOwnersAsync creates owners with balance 0 and no allowance"
    - path: "sources/AppHost/Program.cs"
      issue: "UAT block unpauses control plane, starting the cost reconciler"
    - path: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs"
      issue: "Asserts owner.Balance == -1.3000m, locking in the insolvent seed"
  missing:
    - "Seed an idempotent, auditable credit for the UAT cost owner well above the 5 EUR floor plus several hours of slices for all seeded servers (no re-credit on reseed)"
    - "Optionally give seeded running/transitional servers a current prepaid window like UAT 9"
    - "Update CostingScenario_ChargesOneBackdatedSliceAgainstTheOwnerBalance for the new balance"
    - "Regression test: seed UAT scenarios, run HostingCostReconciler.ReconcileAsync once, assert no automatic cost-stop and UAT 2,3,4,6,7,10 keep seeded status"
    - "Keep the real cost reconciler active in the UAT profile (no no-op swap)"
  debug_session: ".planning/debug/uat-seeded-servers-shown-off.md"

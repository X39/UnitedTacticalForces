---
phase: 67-server-metrics
verified: 2026-11-24T20:15:00Z
status: passed
score: 7/7 must-haves verified
covered_files:
  - ".planning/phases/67-server-metrics/67-01-PLAN.md"
  - ".planning/phases/67-server-metrics/67-01-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-02-PLAN.md"
  - ".planning/phases/67-server-metrics/67-02-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-03-PLAN.md"
  - ".planning/phases/67-server-metrics/67-03-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-04-PLAN.md"
  - ".planning/phases/67-server-metrics/67-04-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-05-PLAN.md"
  - ".planning/phases/67-server-metrics/67-05-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-06-PLAN.md"
  - ".planning/phases/67-server-metrics/67-06-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-07-PLAN.md"
  - ".planning/phases/67-server-metrics/67-07-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-08-PLAN.md"
  - ".planning/phases/67-server-metrics/67-08-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-09-PLAN.md"
  - ".planning/phases/67-server-metrics/67-09-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-10-PLAN.md"
  - ".planning/phases/67-server-metrics/67-10-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-11-PLAN.md"
  - ".planning/phases/67-server-metrics/67-11-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-12-PLAN.md"
  - ".planning/phases/67-server-metrics/67-12-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-13-PLAN.md"
  - ".planning/phases/67-server-metrics/67-13-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-14-PLAN.md"
  - ".planning/phases/67-server-metrics/67-14-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-15-PLAN.md"
  - ".planning/phases/67-server-metrics/67-15-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-16-PLAN.md"
  - ".planning/phases/67-server-metrics/67-16-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-17-PLAN.md"
  - ".planning/phases/67-server-metrics/67-17-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-18-PLAN.md"
  - ".planning/phases/67-server-metrics/67-18-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-19-PLAN.md"
  - ".planning/phases/67-server-metrics/67-19-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-20-PLAN.md"
  - ".planning/phases/67-server-metrics/67-20-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-21-PLAN.md"
  - ".planning/phases/67-server-metrics/67-21-SUMMARY.md"
  - ".planning/phases/67-server-metrics/67-22-PLAN.md"
  - ".planning/phases/67-server-metrics/67-22-SUMMARY.md"
  - "scripts/Start-Uat.ps1"
  - "sources/Api/Data/EUatPlayerProfile.cs"
  - "sources/Api/Data/UatFleetScenarioSeeder.cs"
  - "sources/Api/HostedServices/UatWorkerChannelEmulator.cs"
  - "sources/Api/Services/ServerMetrics/ServerMetricsService.cs"
  - "sources/Api/Services/WorkerProtocol/MetricsFrameRoute.cs"
  - "sources/CloudWorker/CloudWorkerRuntime.cs"
  - "sources/CloudWorker/WorkerMetricsSampler.cs"
  - "sources/ReactApp/src/components/server/ServerMetricsPanel.tsx"
  - "sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx"
  - "sources/ReactApp/src/features/hosting/metricsFrames.ts"
  - "sources/ReactApp/src/features/hosting/metricsModel.ts"
  - "sources/ReactApp/src/features/hosting/useServerMetrics.ts"
  - "sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx"
  - "sources/ReactApp/tests/hosting/metrics-frames.test.ts"
  - "sources/ReactApp/tests/hosting/metrics-panel.test.tsx"
  - "tests/Api.Tests/GameServerHosting/UatFleetScenarioCostReconciliationTests.cs"
  - "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs"
  - "tests/Api.Tests/GameServerHosting/UatWorkerChannelEmulatorTests.cs"
covered_digest: "v4:sha256:6e8ccd30f219a19ef69fc3527cd432040e91a617d4c04d230d2b2fd698ecd414"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 7/7
  gaps_closed: []
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "UAT test 7 (G-67-1 stop path) on the real seeded stack. Start: ./scripts/Start-Uat.ps1 -Fresh. Open http://localhost:5173/next/command/servers, wait about one minute (the cost reconciler runs every 30 s), open 'UAT 10 Metrics', wait for values, then choose 'Shut down and stop billing' without reloading and confirm."
    expected: "UAT 10 Metrics is shown as running with live samples. Once the stop completes, every tile reads Unavailable with 'The game is not running, so there is nothing to measure.', with no number and no running-since time. The other seeded servers stay running (the emulator sessions are isolated)."
    why_human: "Live check against the real seeded stack and fleet socket. The earlier automated run could not complete the stop because the emulator did not acknowledge worker commands; commit 69d35abf (emulator acknowledges stop commands) and the later emulator command-report work address that, and the emulator tests pass, but the end-to-end re-run (sources/ReactApp/tests/uat/metrics.spec.ts) has not been repeated. Standing preference: real-environment checks are deferred to milestone end and do not block."
  - test: "Live tiles and history on 'UAT 10 Metrics' (UAT test 2 passed once; repeat after the emulator rework): Running since, Players, CPU, Memory change within about 15 s; 1 hour, 24 hours and 7 days charts, gap breaks, tables view, reload and API restart."
    expected: "Values live, charts grow at the right edge, gaps shown as breaks (never zero), same history after reload and after an API restart."
    why_human: "Needs the running app and visual judgment."
  - test: "Real game player counts (optional, deferred): Arma 3, Counter-Strike 2 (RCON status text and A2S on the game port), ASKA (A2S on port 27016)."
    expected: "A recognised reply shows the count; an unrecognised reply reads 'The reading failed. It will be tried again.' and never a guessed count."
    why_human: "Needs real game servers; deferred to pre-release real-environment testing (UAT test 5)."
  - test: "Real worker through nginx (optional, deferred): run a real worker against a deployed API."
    expected: "The ClientWebSocket upgrade with the worker header reaches the tiles."
    why_human: "External service integration; deferred to pre-release real-environment testing (UAT test 6)."
---

# Phase 67: Server Metrics Verification Report

**Phase Goal:** A user sees how a running server is doing (uptime, players, machine load, and a short history) and sees "unavailable" instead of an invented number when a metric cannot be measured. The worker reports metrics through the worker protocol; the API never samples the game process.
**Verified:** 2026-11-24T20:15:00Z
**Status:** human_needed
**Re-verification:** Yes. The previous report (2026-11-22, 7/7, human_needed) was stale because commit `36da6568` changed `UatFleetScenarioSeeder.cs`, `EUatPlayerProfile.cs` and `UatWorkerChannelEmulator.cs`. All truths were re-checked against the current tree.

## Goal Achievement

### What changed since the last report

I diffed `db55187d` (the commit that completed the phase) against `HEAD`:

- **Phase 67 product files are unchanged.** `git diff --stat db55187d..HEAD` over `sources/Api/Services/ServerMetrics`, `MetricsFrameRoute.cs`, `sources/CloudWorker`, `ServerMetricsPanel.tsx`, `metricsFrames.ts`, `metricsModel.ts`, `useServerMetrics.ts` and `FleetLiveUpdatesProvider.tsx` lists none of them. Phase 68 changed only shared hosting files in the same folders (automation, cost alerts, `OverviewSection.tsx`, `services.ts`, `types.ts`) and the database. `OverviewSection.tsx` still imports and renders `<ServerMetricsPanel server={server} />` (line 189).
- **UAT-only code changed.** `36da6568` adds `EUatPlayerProfile` and an `EmulatedWorkers` map to `UatFleetScenarioSeeder`, and turns the emulator into one session per listed server. Phase 68 work and commit `69d35abf` extend the emulator with command acknowledgement (`UatEmulatedCommandReports.cs`, +507 lines in the emulator). None of this is product code.
- **The emulator is Development/UAT-only.** `UatWorkerChannelEmulator.AddTo` (line 97) returns false unless `IsDevelopment()` and `Testing:UatScenarios:Enabled` are both set. Its only call site is `Program.cs:227`. It speaks through the real worker channel (loopback pair, real authentication), so it does not weaken "metrics reach the control plane only through the worker protocol", and it adds no API-side sampling.
- **Prior seed credit is intact.** `UatFleetScenarioCostReconciliationTests` and `UatFleetScenarioSeederTests` still pass after the seeder change (see spot-checks).

### Observable Truths

| #   | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1 | SC1: A running server shows its uptime (running since) in the workspace | VERIFIED | `ServerMetricsService.EvaluateCurrentAsync`, `metricTiles` in `metricsModel.ts`, `ServerMetricsPanel` rendered from `OverviewSection.tsx:189`. Files unchanged since the previous report; 130 frontend tests pass. |
| 2 | SC2: Current player count where the game exposes it, explicit "unavailable" where it does not | VERIFIED (live game output unconfirmed) | Unchanged code; API and worker metric tests pass. Real game reply formats stay a deferred human item. |
| 3 | SC3: CPU and memory usage of the server machine | VERIFIED | `WorkerMetricsSampler` returns null plus a reason (`measuring`, `query-failed`, `unsupported-platform`), never zero (lines 40-64, 100-101). 20 CloudWorker metric tests pass. |
| 4 | SC4: Short retained history as charts, still present after a page reload | VERIFIED | `ServerMetricBuckets` persistence and REST history fetched on mount; API metric tests (134) pass. Live reload and API restart check remains in the human list. |
| 5a | SC5 (display): A stopped server, an offline worker, or a game without player data shows metrics as unavailable, never zero or a fake value | VERIFIED (with warnings W-1 to W-3, carried over) | `replacesCachedReading` is the single merge rule, called from `useServerMetrics.ts:32` and `FleetLiveUpdatesProvider.tsx:219`; no second as-of comparison exists. Panel tests for the stop and restart paths pass. |
| 5b | SC5 (transport): Metrics reach the control plane only through the worker protocol; the API never samples the game process | VERIFIED | Samples come in through `MetricsFrameRoute` (worker protocol) into `ServerMetricsService`. A grep of `sources/Api` for process, performance-counter or `/proc/` sampling returns no match. Worker sampling lives only in `sources/CloudWorker`. The UAT emulator feeds frames through the worker channel and is gated to Development plus UAT. |
| 6 | 67-21 truth: REST readings and live frames merge by one exported rule | VERIFIED | `replacesCachedReading` defined once (`metricsFrames.ts:189`), two callers. |
| 7 | 67-22 truth: after a fresh UAT seed the real cost reconciler funds every seeded possible-cost session and queues no cost-stop | VERIFIED (live stack unconfirmed) | `UatFleetScenarioCostReconciliationTests` (real `HostingCostReconciler`, seeded now and 15 minutes ago) passes after commit `36da6568`. |

**Score:** 7/7 truths verified (0 present, behavior-unverified)

### Incremental review of commit 36da6568 (67-REVIEW.md)

The review reports 0 critical, 2 new warnings (WR-02, WR-03) and 5 info items (IN-05 to IN-09). I read the cited code paths and agree with the severity: all seven findings sit in `UatWorkerChannelEmulator` and the seeder map, which are Development/UAT-only and not reachable in production. They concern emulator robustness and logging (one failed preparation strands earlier prepared sessions; a failed session is logged late; a stopped session is not re-established; duplicated `"game-server"` constant; missing XML docs on two seeder members). None changes what a user of the product sees, and none touches the worker protocol, the API sampling boundary or the unavailable-instead-of-invented rule. They do not block the phase goal. WR-02 and WR-03 are `open` in `67-REVIEW-DISPOSITION.md` and should be triaged. IN-07 is a handwritten-code documentation rule from AGENTS.md (missing `<summary>` on `Scenarios` and `AddServer`) and is cheap to fix.

### Residual risk (warnings, not gaps; carried over unchanged)

- **W-1 (review WR-01 / earlier CR-01):** a late pre-stop frame can replace the cached stop reading, showing a real earlier sample for up to about 45 s, then "Unavailable (last reading too old)". Narrow race, never zero or invented.
- **W-2 (review WR-12 / WR-01):** REST and frames serialize the same instant differently (`Z` versus `+00:00`), so a cross-path refetch re-stamps an unchanged reading.
- **W-3 (review WR-04, pre-existing):** `staleTime: Infinity` with frame-created cache entries can show "too old" instead of "not running" on opening the workspace of a stopped server.
- Earlier warnings still open: ingest rejects samples on the worker clock (`ServerMetricsService.cs`), and the worker metrics loop is not fault-isolated (`CloudWorkerRuntime.cs`).

These are triaged as warnings: each can only show a real, age-bounded value, never an invented one.

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `sources/Api/Services/ServerMetrics/ServerMetricsService.cs` | Ingest, current evaluation, history | VERIFIED | 438 lines, unchanged |
| `sources/Api/Services/WorkerProtocol/MetricsFrameRoute.cs` | Worker protocol route | VERIFIED | 85 lines, unchanged |
| `sources/CloudWorker/WorkerMetricsSampler.cs`, `CloudWorkerRuntime.cs` | Worker-side sampling | VERIFIED | null plus reason, never zero |
| `sources/ReactApp/src/components/server/ServerMetricsPanel.tsx` | Tiles, charts, tables | VERIFIED | 178 lines, wired from `OverviewSection.tsx` |
| `metricsFrames.ts`, `metricsModel.ts`, `useServerMetrics.ts`, `FleetLiveUpdatesProvider.tsx` | Single merge rule, model, live frames | VERIFIED | `replacesCachedReading` exported and used by both writers |
| `sources/Api/Data/UatFleetScenarioSeeder.cs`, `EUatPlayerProfile.cs` | UAT seed with credit and emulated-worker map | VERIFIED | Credit tests pass; enum follows the `E` prefix rule and has its own file |
| `sources/Api/HostedServices/UatWorkerChannelEmulator.cs` | Dev-only per-server worker emulation | VERIFIED | Gated by `AddTo`; 134 metric-related tests including emulator tests pass |
| Earlier artifacts (migration, REST controller endpoints, history, other panel files) | As in the first report | VERIFIED | No change since `db55187d` |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| Worker metrics frame | `MetricsFrameRoute` | Worker protocol | WIRED | Unchanged |
| `MetricsFrameRoute` | `ServerMetricsService` | Ingest | WIRED | Unchanged |
| `ServerMetricsService` | fleet socket and REST | publisher and controller | WIRED | Unchanged |
| `useServerMetrics.ts` and `FleetLiveUpdatesProvider.tsx` | `metricsFrames.ts` | `replacesCachedReading` | WIRED | lines 32 and 219 |
| `OverviewSection.tsx` | `ServerMetricsPanel` | render | WIRED | line 189 |
| `Program.cs` | `UatWorkerChannelEmulator.AddTo` | Development plus `Testing:UatScenarios:Enabled` | WIRED (gated) | line 227 |
| Seeder `EmulatedWorkers` | emulator sessions | title lookup, one session per entry | WIRED | covered by `UatWorkerChannelEmulatorTests` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| Metrics tiles | `ServerMetricsReading` | REST `/metrics` and fleet `metricsFrame`, merged by `replacesCachedReading` | Yes; unavailable readings carry the control plane's reason | FLOWING |
| Charts and tables | `GameServerMetricsHistory` | REST `/metrics/history` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Metrics merge, panel, model, fleet, source-scan tests | `npx vitest run tests/hosting/metrics-panel.test.tsx metrics-frames.test.ts metrics-model.test.ts fleet-live-updates.test.tsx metrics-source-scan.test.ts` (in `sources/ReactApp`) | 5 files, 130 passed, 0 failed | PASS |
| UAT seed, cost reconciliation, emulator, metrics service and frame tests | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~UatFleetScenario\|UatWorkerChannelEmulator\|ServerMetrics\|MetricsFrame"` | 118 passed, 0 failed | PASS |
| All API metric tests | `dotnet test tests/Api.Tests --no-build --filter "FullyQualifiedName~Metric"` | 134 passed, 0 failed | PASS |
| Worker metric tests | `dotnet test tests/CloudWorker.Tests --filter "FullyQualifiedName~Metric"` | 20 passed, 0 failed | PASS |
| API never samples the game process | grep of `sources/Api` for `Process.GetProcess`, `Process.Start`, `PerformanceCounter`, `/proc/` | no match | PASS |
| Emulator registration gate | read `UatWorkerChannelEmulator.AddTo` and its call sites | gated, one call site | PASS |
| Full solution suite after `36da6568` (not re-run) | `dotnet test X39.UnitedTacticalForces.slnx` | Api.Tests 1697/1697, CloudWorker.Tests 364/364, Ui.Tests 2/2, exit 0, as reported by the orchestrator | Accepted as reported |

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts.

### Requirements Coverage

All five IDs are declared across the 22 plans and match `REQUIREMENTS.md`, which now marks MET-01 to MET-05 as `[x]` and Complete. No orphaned IDs.

| Requirement | Source Plans | Description | Status | Evidence |
| ----------- | ------------ | ----------- | ------ | -------- |
| MET-01 | 04, 12, 15, 16, 17, 20, 21 | Uptime for a running server | SATISFIED | Truth 1 |
| MET-02 | 02, 04, 05, 09, 10, 14, 15, 16, 17, 20 | Player count where exposed | SATISFIED (live game formats unconfirmed) | Truth 2 |
| MET-03 | 02, 04, 05, 14, 15, 16, 17, 20 | CPU and memory of the server machine | SATISFIED | Truth 3 |
| MET-04 | 02, 04, 07, 12, 13, 17, 18, 19, 20, 22 | Short retained history as charts | SATISFIED | Truth 4 |
| MET-05 | 01 to 12, 14 to 22 | Worker protocol transport; unavailable never fake | SATISFIED (see W-1 to W-3) | Truths 5a and 5b |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| `UatWorkerChannelEmulator.cs` | 145-173 | One failing preparation aborts all sessions; failures logged late (review WR-02, WR-03) | Warning | Dev-only; a UAT server could get no live feed without a clear log |
| `UatWorkerChannelEmulator.cs`, `UatFleetScenarioSeeder.cs` | various | Non-unique title lookup, duplicated `"game-server"` constant, restart needs API restart (IN-05, IN-08, IN-09) | Info | Dev-only |
| `UatFleetScenarioSeeder.cs` | 126, 247 | `Scenarios` and `AddServer` lack XML documentation (IN-07) | Info | AGENTS.md rule, cosmetic |
| `metricsFrames.ts`, `useServerMetrics.ts` | see W-1 to W-3 | Merge-rule edge cases | Warning | Carried over; bounded real values only |
| `ServerMetricsService.cs`, `CloudWorkerRuntime.cs` | see earlier | Worker-clock rejection; metrics loop not fault-isolated | Warning | Carried over |

A `TBD`/`FIXME`/`XXX` grep over the four UAT-side files changed since the last report found no debt markers. 67-SECURITY.md reports SECURED; no new security surface is added by the emulator changes (Development-only, loopback, existing credential issue).

### Human Verification Required

These four items carry the earlier UAT evidence forward (`67-UAT.md`: tests 2, 4 and 8 passed, 5 and 6 skipped as deferred, 7 skipped as deferred). They need a running app, a browser or real game servers, and the standing preference defers real-environment checks to milestone end, so they do not block the phase. The first item replaces the earlier blocker reason: the emulator now acknowledges stop commands (`69d35abf`), so the stop path can be exercised, but that live run has not been repeated.

1. **UAT test 7, stop path on the seeded stack.** Start `./scripts/Start-Uat.ps1 -Fresh`, open http://localhost:5173/next/command/servers, wait about one minute, open "UAT 10 Metrics", wait for values, choose "Shut down and stop billing". Expect every tile to read Unavailable with "The game is not running, so there is nothing to measure.", no number, no running-since time.
2. **Live tiles and history** on "UAT 10 Metrics", after reload and after an API restart.
3. **Real game player counts** (deferred).
4. **Real worker through nginx** (deferred).

### Gaps Summary

No blocking gaps. The phase goal is achieved in code: worker-reported metrics (uptime, players, CPU, memory) flow through the worker protocol into the API and to the workspace; history is persisted and served over REST; unavailable readings carry a reason and are never rendered as zero or an invented value; the API contains no game-process sampling. The commit that made the previous report stale touches only the Development/UAT emulator and seeder, passes its tests, and has an advisory review (0 critical) whose warnings are dev-only robustness issues. Status stays `human_needed` because live-stack and real-environment checks remain open, not because of a code gap. Recommended follow-ups: triage review WR-02, WR-03 and the carried-over W-1 to W-3 in `67-REVIEW-DISPOSITION.md`, and re-run `sources/ReactApp/tests/uat/metrics.spec.ts` at milestone end.

---

_Verified: 2026-11-24T20:15:00Z_
_Verifier: Claude (gsd-verifier)_

## Acknowledged Gaps

- 2026-11-22 (verify-work): The blocking `ai-integration` API-coverage gate (`api-coverage.verify-pre`) asked for a COVERAGE.md. The user overrode it as a false positive. The detector matched only the phrase "The one wire envelope shared by worker and API" in 67-01-PLAN.md, which names the worker protocol envelope, not an external API integration. No COVERAGE.md was produced.

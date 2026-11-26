---
phase: "67"
slug: "server-metrics"
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-21"
---

# Phase 67 — Validation Strategy

> Per-phase validation contract. Source of truth: `67-RESEARCH.md` § Validation Architecture.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (Api, CloudWorker), Vitest 4.1.10 + Testing Library (ReactApp) |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj`, `tests/CloudWorker.Tests/CloudWorker.Tests.csproj`, `sources/ReactApp/vitest.config.ts` |
| **Quick run command** | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServerMetrics|FullyQualifiedName~WorkerChannel"` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |

## Sampling Rate

- **After every task commit:** filtered quick command for the touched area
- **After every plan wave:** full suite command
- **Before `/gsd-verify-work`:** full suite green, OpenAPI snapshot in sync
- **Max feedback latency:** 120 seconds

## Per-Task Verification Map

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| MET-01 | Running since from the primary worker's observed process start; tile and panel render it | integration + Vitest + e2e | `dotnet test tests/Api.Tests --filter "ServerMetricsReadModelTests|ServerMetricsEndpointTests"`; `npm --prefix sources/ReactApp test -- tests/hosting/metrics-model.test.ts tests/hosting/metrics-panel.test.tsx`; `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-metrics.spec.ts` | ✅ | ✅ green |
| MET-02 | A2S_INFO, CS2 `status` and Arma 3 addon player parsing; game-does-not-report | unit | `dotnet test tests/CloudWorker.Tests --filter "A2sInfoClientTests|Arma3PlayerEvidenceTests|CounterStrike2StatusParserTests|CounterStrike2ImplementationTests|AskaImplementationTests"` | ✅ | ✅ green |
| MET-03 | CPU and memory sampling math and fallbacks; tiles show them | unit + Vitest | `dotnet test tests/CloudWorker.Tests --filter WorkerMetricsSamplerTests`; `npm --prefix sources/ReactApp test -- tests/hosting/metrics-model.test.ts` | ✅ | ✅ green |
| MET-04 | Tiered rollup, durable prune, history endpoint, chart gaps, table view, reload history | integration + Vitest + e2e | `dotnet test tests/Api.Tests --filter "ServerMetricIngestTests|ServerMetricsPruneTests|ServerMetricsHistoryTests"`; `npm --prefix sources/ReactApp test -- tests/hosting/metric-line-chart.test.tsx tests/hosting/metric-table.test.tsx tests/hosting/metrics-performance.test.tsx`; `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-metrics.spec.ts` | ✅ | ✅ green |
| MET-05 | Worker-channel-only ingestion, one frame per authenticated worker, live frames, unavailable never zero | integration + property + Vitest + e2e | `dotnet test tests/Api.Tests --filter "WorkerChannelHandlerTests|WorkerChannelRouteTests|WorkerChannelMetricsTests|WorkerChannelSignalTests|WorkerChannelSupersessionTests|WorkerMetricsTransportGuardTests|ServerMetricsAvailabilityTests"`; `dotnet test tests/CloudWorker.Tests --filter CloudWorkerChannelClientTests`; `npm --prefix sources/ReactApp test -- tests/hosting/metrics-frames.test.ts tests/hosting/metrics-source-scan.test.ts` | ✅ | ✅ green |
| FND-07 | Parity rows MET-tiles, MET-history, SVC-serverMetrics, SVC-serverMetricsHistory kept with evidence | Vitest | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` | ✅ | ✅ green |
| Cross-phase | No axe violation at WCAG 2.1 AA and no horizontal page scroll at 320 and 390 px in English and German | Playwright | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-metrics.spec.ts tests/e2e/hosting-workspace.spec.ts tests/e2e/hosting-a11y.spec.ts` | ✅ | ✅ green |

## Wave 0 Requirements

- [x] `tests/Api.Tests/GameServerHosting/WorkerChannelHandlerTests.cs`
- [x] `ServerMetricIngestTests.cs`, `ServerMetricsPruneTests.cs`, `ServerMetricsReadModelTests.cs`, `ServerMetricsAvailabilityTests.cs`
- [x] `tests/CloudWorker.Tests/CloudWorkerChannelClientTests.cs`, `A2sInfoClientTests.cs`, `WorkerMetricsSamplerTests.cs`
- [x] `sources/ReactApp/tests/hosting/metrics-*.test.ts(x)`

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live CS2/Arma 3/ASKA player count | MET-02 | needs a live game server | Start a real deployment of the game from the running app and read the Players tile (UAT step 9 of plan 67-20); optional |
| Real WebSocket upgrade through the reverse proxy | MET-05 | needs a deployed API and a real worker | Run a real worker against a deployed API and confirm its metrics reach the tiles |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Wave 0 covers all MISSING references
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** automated gates green on 2026-11-22 (plan 67-20 task 3); the live UAT steps are recorded in `67-20-SUMMARY.md` as human-verification items.

## Validation Audit 2026-11-22

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

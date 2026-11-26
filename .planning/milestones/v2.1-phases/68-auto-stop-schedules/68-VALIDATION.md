---
phase: "68"
slug: "auto-stop-schedules"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-22"
---

# Phase 68 — Validation Strategy

> Per-phase validation contract. Source of truth: `68-RESEARCH.md` § Validation Architecture.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 with SQLite in-memory (`HostingTestStore`, `NodaTimeSqliteApiDbContext`) in `tests/Api.Tests`; Vitest + Testing Library (ReactApp); Playwright mocked e2e (`tests/e2e`); Playwright seeded-stack UAT (`tests/uat`, tags `@fresh` and `@restarted`, selected by `UAT_PHASE`) |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj`, `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `sources/ReactApp/playwright.uat.config.ts` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ServerAutomation"` and `npm --prefix sources/ReactApp test -- tests/hosting/automation` (single file: `npx vitest run <file>` in `sources/ReactApp`) |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |
| **Mocked e2e command** | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts` |
| **Seeded-stack UAT command** | `npm --prefix sources/ReactApp run test:uat` (`UAT_PHASE=restarted` for the restart check) |
| **Estimated runtime** | ~120 seconds (quick/filtered), longer for the full suite |

## Sampling Rate

- **After every task commit:** run the filtered quick command for the touched area
- **After every plan wave:** run the full suite command (`dotnet test X39.UnitedTacticalForces.slnx` plus `npm run ci` for React waves)
- **Before `/gsd-verify-work`:** full suites green, OpenAPI snapshot and generated types in sync, seeded UAT run recorded
- **Max feedback latency:** 120 seconds

## Per-Task Verification Map

AUT-04 server rows are covered by `AutoStopAlertWriterTests`, `CostAlertServiceTests`, `CostAlertEmailDeliveryTests`, `CostAlertMessageRendererTests`, `CostAlertPublisherTests` and `ServerAutomationWorkerTests` (filter `CostAlert|AutoStopAlertWriter|ServerAutomation`) rather than a single `AutoStopWarningTests` file. Mocked e2e and seeded-stack UAT specs exist; they are not re-run by this audit (see Manual-Only).

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| AUT-01 | Idle clock: zero starts, count resets, unknown holds and shifts, active job counts as activity, disabled or not running clears, structural unavailable never arms | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~IdleClockTests` | ✅ | ✅ green |
| AUT-01 | Settings PUT validates range from options, zone, rules; 403/404/409 mapping; non-manager sees `canManageAutomation=false`; `idleShutdown.available` follows `ReportsPlayerCount` | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ServerAutomationSettingsTests` | ✅ | ✅ green |
| AUT-01 | Panel read-only with notice for a non-manager; unavailable block; structural reason; draft/dirty/invalid/save/409/403; leave guard shared with staged changes | Vitest + Playwright e2e | `npm --prefix sources/ReactApp test -- tests/hosting/automation-model.test.ts tests/hosting/automation-panel.test.tsx`; `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts` | ✅ | ✅ green |
| AUT-02 | Max runtime from `TimeStampStarted`: warns at limit, stops after lead, cancels when raised, not armed before `Running` | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~MaxRuntimeTests` | ✅ | ✅ green |
| AUT-03 | `ScheduleCalculator`: weekdays, zone, DST gap and overlap, `EffectiveFrom`, watermark windows | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ScheduleCalculatorTests` | ✅ | ✅ green |
| AUT-03 | Start outcomes: Started, SkippedMissed, SkippedConsent, SkippedRunning, SkippedBusy, SkippedCost; Stop outcomes: Stopped, SkippedOff, SkippedMissed | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ScheduledRunTests` | ✅ | ✅ green |
| AUT-03 | Consent dialog on new or changed Start rule, consent-needed notice, Remove/Add focus, time zone select, duplicate detection | Vitest + Playwright e2e | as AUT-01 UI row | ✅ | ✅ green |
| AUT-04 | Pending row + alert + deliveries + audit in one transaction; Discord and email via recording senders; cancel marks deliveries `stop-resolved` and sends dismissal; per-viewer dismissal; resolved alerts excluded; EN and DE rendering | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AutoStopWarningTests` | ✅ | ✅ green |
| AUT-04 | Shell notice for every viewer (link only for managers), toast once per new alert id, cancellation toast, menu row cap and badge | Vitest | `npm --prefix sources/ReactApp test -- tests/hosting/auto-stop-notice.test.tsx tests/hosting/cost-alert-menu.test.tsx` | ✅ | ✅ green |
| AUT-05 | Automatic stop creates a normal `Stop` job (Priority 50, system actor, trigger snapshot), audit `AutomaticStopRequested`, Activity trigger line; completes only via verified release | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AutomaticStopJobTests` | ✅ | ✅ green |
| AUT-05 | `GetOperationsAsync` returns the seven kinds with correct status; interceptor emits a frame for settings and pending-stop changes | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutomationOperationsTests\|FullyQualifiedName~FleetChangeInterceptorTests"` | ✅ | ✅ green |
| AUT-06 | Restart simulation: new scope and evaluator at T0 + due yields exactly one job; double evaluation and concurrent evaluators yield one job, one alert, one run row; idle clock survives a new evaluator | integration | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AutomationRestartTests` | ✅ | ✅ green |
| AUT-06 | Seeded stack: after `Start-Uat.ps1` restart without `-Fresh`, the Activity entry is single (`@restarted`) | Playwright UAT | `UAT_PHASE=restarted npm --prefix sources/ReactApp run test:uat` (spec `tests/uat/automation.spec.ts`) | ✅ | ⬜ spec exists; run deferred (see Manual-Only) |
| AUT-01..06 | Seeded stack: idle, max-runtime and schedule scenarios warn, mail arrives (Mailpit), notice, toast, menu row, Activity trigger line, single audit entry | Playwright UAT | `UAT_PHASE=fresh npm --prefix sources/ReactApp run test:uat` | ✅ | ⬜ spec exists; run deferred (see Manual-Only) |
| All | Parity rows `AUT-settings-panel`, `AUT-warning-notice`, `AUT-activity-trigger`, `AUT-overview-fact`; copy guards; EN/DE locales; no `setInterval` or `refetchInterval` in automation files; contract check | Vitest | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts`; `npm --prefix sources/ReactApp run locales:check`; `npm --prefix sources/ReactApp run contracts:check` | ✅ | ✅ green |
| All | axe (WCAG 2.1 AA) on panel states, notice and consent dialog; no horizontal scroll at 320 and 390 px; EN and DE | Playwright e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-a11y.spec.ts` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

- [x] `tests/Api.Tests/GameServerHosting/` (or matching folder): `IdleClockTests.cs`, `ScheduleCalculatorTests.cs`, `ServerAutomationSettingsTests.cs`, `ScheduledRunTests.cs`, `MaxRuntimeTests.cs`, `AutoStopWarningTests.cs`, `AutomaticStopJobTests.cs`, `AutomationRestartTests.cs`, `AutomationOperationsTests.cs`
- [x] `ServerAutomationTestData` helper (settings, rules, running deployment with a primary worker and metrics snapshot; built on `HostingTestData.SeedServerAsync` and `ServerMetricsTestData`)
- [x] `sources/ReactApp/tests/hosting/automation-*.test.ts(x)`, `tests/hosting/auto-stop-notice.test.tsx`
- [x] `sources/ReactApp/tests/e2e/hosting-automation.spec.ts`, `tests/e2e/support/automationMocks.ts`
- [x] `sources/ReactApp/tests/uat/automation.spec.ts` plus UAT seed scenarios (`UAT 11 ...` upward in `UatFleetScenarioSeeder`) and generalized `UatWorkerChannelEmulator`
- [x] Framework install: none (all frameworks present)

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real Hetzner stop completes after an automatic stop on a real deployment | AUT-05 | needs a real provider and real billing resource | Deferred follow-up: run at milestone end against a real environment |
| Post-gap-closure seeded-stack re-check | AUT-06 | only at milestone end per project policy | Deferred follow-up: re-run `npm --prefix sources/ReactApp run test:uat` |
| Mocked e2e and seeded-stack UAT re-run after this audit | AUT-01..06 | background e2e run and no live stack at audit time | Deferred follow-up: run `npm run test:e2e` and `npm run test:uat` at milestone end |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated

## Validation Audit 2026-11-24

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

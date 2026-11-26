---
phase: "66"
slug: accrued-cost-cost-alerts
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-21"
---

# Phase 66 — Validation Strategy

> Per-phase validation contract. Source of truth: the Validation Architecture section of `66-RESEARCH.md`.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (API, EF SQLite in-memory via `HostingTestStore`); Vitest 4.1.10 + Testing Library + msw (React); Playwright + axe (e2e) |
| **Config file** | `tests/Api.Tests/Api.Tests.csproj`; `sources/ReactApp/vitest.config.ts` |
| **Quick run command** | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostAccrual\|FullyQualifiedName~CostAlert"` ; `cd sources/ReactApp && npx vitest run <file>` |
| **Full suite command** | `dotnet test X39.UnitedTacticalForces.slnx` and `cd sources/ReactApp && npm run ci` |
| **Estimated runtime** | ~30 s quick, several minutes full |

## Sampling Rate

- **After every task commit:** the relevant `--filter` slice or single `npx vitest run <file>`
- **After every plan wave:** `dotnet test tests/Api.Tests/Api.Tests.csproj` plus `npm run test` in `sources/ReactApp`
- **Before `/gsd-verify-work`:** full suite green, axe scan of settings page, banner and alert menu
- **Max feedback latency:** 30 s

## Per-Task Verification Map

API commands run as `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "<filter>"`; React commands as `npm --prefix sources/ReactApp test -- <files>`. "Created by task" means the test file is written by that TDD task itself (no separate Wave 0 plan).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 66-01-01 | 01 | 1 | CST-03 | T-66-01, T-66-03 | Hidden cost owner gets amount null, reason hidden; unviewable server 404 | integration | `FullyQualifiedName~AccruedCostEndpointTests` | existing | ✅ green |
| 66-01-02 | 01 | 1 | CST-03 | - | Proration frozen at resolution, never a fake zero | unit | `FullyQualifiedName~CostAccrualCalculatorTests` | existing | ✅ green |
| 66-01-03 | 01 | 1 | CST-03 | T-66-02 | Cost-owner view audience equals CanViewCostOwnerAsync (drift test) | integration | `FullyQualifiedName~CostOwnerViewAudienceTests` | existing | ✅ green |
| 66-02-01 | 02 | 1 | CST-05 | T-66-05, T-66-06 | EN/DE render from resx, control chars stripped, disconnected bot never Sent | unit | `FullyQualifiedName~CostAlertMessageRendererTests\|FullyQualifiedName~CostAlertResourceTests\|FullyQualifiedName~DiscordDirectMessageSenderTests` | existing | ✅ green |
| 66-02-02 | 02 | 1 | CST-06 | T-66-SC | Human approves MailKit 5.0.0 and the Mailpit tag before install | checkpoint | n/a (blocking human checkpoint) | n/a | ✅ green |
| 66-02-03 | 02 | 1 | CST-06 | T-66-04, T-66-07, T-66-SC | Unconfigured SMTP is NotConfigured, no credential in messages, deterministic Message-Id | unit | `FullyQualifiedName~SmtpEmailSenderTests` | existing | ✅ green |
| 66-03-01 | 03 | 2 | CST-04 | T-66-08 | One alert per (deployment, threshold); D-09 Skipped rows keep the in-app alert | integration | `FullyQualifiedName~CostAlertEvaluatorTests` | existing | ✅ green |
| 66-03-02 | 03 | 2 | CST-04 | - | Tool-generated migration in sync | integration | `dotnet ef migrations has-pending-model-changes ...` and `dotnet build X39.UnitedTacticalForces.slnx` | n/a | ✅ green |
| 66-03-03 | 03 | 2 | CST-04 | T-66-10 | Restart records nothing new; group recipients are co:use holders only | integration | `FullyQualifiedName~CostAlertEvaluatorTests\|FullyQualifiedName~CostAlertRecipientResolverTests\|FullyQualifiedName~HostingControlPlaneStateTests` | existing | ✅ green |
| 66-04-01 | 04 | 2 | CST-03 | T-66-12 | Dev-only seeded costing session with a real slice | integration | `FullyQualifiedName~UatFleetScenarioSeederTests` | existing | ✅ green |
| 66-04-02 | 04 | 2 | CST-06 | T-66-12, T-66-13 | Mailpit pinned and only under Uat:Enabled | build | `dotnet build sources/AppHost/AppHost.csproj` plus grep gates | n/a | ✅ green |
| 66-05-01 | 05 | 3 | CST-04 | T-66-15, T-66-17 | Per-viewer list and dismissal; invisible alert id returns 404 | integration | `FullyQualifiedName~CostAlertServiceTests` | existing | ✅ green |
| 66-05-02 | 05 | 3 | CST-03 | T-66-16 | Concurrent producers cannot drop a socket | unit | `FullyQualifiedName~UpdateStreamServiceSendTests` | existing | ✅ green |
| 66-05-03 | 05 | 3 | CST-03, CST-04 | T-66-14 | Value frames only to server-view and owner-view intersection; fail closed; DateTimeOffset on the wire | integration | `FullyQualifiedName~CostAlertPublisherTests` | existing | ✅ green |
| 66-06-01 | 06 | 3 | CST-04 | T-66-18, T-66-19, T-66-20 | canManage enforced, validation 400, stale revision 409, locale recorded | integration | `FullyQualifiedName~CostAlertSettingsTests` | existing | ✅ green |
| 66-06-02 | 06 | 3 | CST-04 | T-66-21 | Server alert summary null for viewers without owner visibility | integration | `FullyQualifiedName~CostAlertServerSummaryTests` | existing | ✅ green |
| 66-06-03 | 06 | 3 | CST-06 | T-66-22 | Only en and de stored for the caller | integration | `FullyQualifiedName~UserLocaleEndpointTests` | existing | ✅ green |
| 66-07-01 | 07 | 3 | CST-05 | T-66-23 | Claim before send; Sent is terminal | integration | `FullyQualifiedName~CostAlertDispatcherTests` | existing | ✅ green |
| 66-07-02 | 07 | 3 | CST-05 | T-66-25, T-66-26 | Bounded retries, permanent failure not retried, lease recovery | integration | `FullyQualifiedName~CostAlertDispatcherTests` | existing | ✅ green |
| 66-07-03 | 07 | 3 | CST-06 | T-66-24 | Skipped when unconfigured, one email per recipient, no address in logs | integration | `FullyQualifiedName~CostAlertEmailDeliveryTests` | existing | ✅ green |
| 66-08-01 | 08 | 4 | CST-03 | T-66-27, T-66-28 | Contract regenerated by tooling; frames validated and ordered by asOf | unit + contract | export + `cmp`, `contracts:check`, `tests/hosting/accrued-cost.test.tsx` | existing | ✅ green |
| 66-08-02 | 08 | 4 | CST-03 | - | Accrued states, tiny rule, no interval timer or refetch interval | unit | `tests/hosting/accrued-model.test.ts tests/hosting/accrued-cost.test.tsx tests/hosting/cost-alerts-source-scan.test.ts` | existing | ✅ green |
| 66-08-03 | 08 | 4 | CST-04 | - | EN/DE parity, no banned button label, typed client calls | unit | `tests/hosting/cost-alert-services.test.ts tests/locales tests/hosting/parity-matrix.test.ts` | existing | ✅ green |
| 66-09-01 | 09 | 5 | CST-04 | T-66-31 | Pushed alert appears in the right server's banner; dismissal after API accept | unit | `tests/hosting/cost-frames.test.ts tests/hosting/cost-alert-banner.test.tsx` | existing | ✅ green |
| 66-09-02 | 09 | 5 | CST-04 | - | Menu rows, badge, one toast per new id, no alert polling | unit | `tests/hosting/cost-alert-menu.test.tsx tests/profile-menu.test.tsx` | existing | ✅ green |
| 66-09-03 | 09 | 5 | CST-04 | T-66-30 | Cost-stop only through the shared confirmation; focus rules | unit | `tests/hosting/cost-alert-banner.test.tsx tests/hosting/cost-alert-menu.test.tsx tests/hosting/cost-confirm-dialog.test.tsx` | existing | ✅ green |
| 66-10-01 | 10 | 5 | CST-04 | T-66-35 | Route certified; owner param validated before requests | unit | `tests/hosting/cost-alerts-page.test.tsx tests/routes.test.ts` | existing | ✅ green |
| 66-10-02 | 10 | 5 | CST-04 | T-66-34 | Client validation mirrors the API | unit | `tests/hosting/alert-settings-model.test.ts tests/hosting/cost-alert-settings.test.tsx tests/form-fields.test.tsx` | existing | ✅ green |
| 66-10-03 | 10 | 5 | CST-04 | T-66-33 | Draft never persisted; conflict recoverable; leave guard | unit | `tests/hosting/cost-alert-settings.test.tsx tests/hosting/change-source-scan.test.ts` | existing | ✅ green |
| 66-11-01 | 11 | 6 | CST-04 | - | Strip summary only for owner viewers; link only for managers | unit | `tests/hosting/cost-strip.test.tsx` | existing, extended | ✅ green |
| 66-11-02 | 11 | 6 | CST-05, CST-06 | - | Channel availability hints; in-app always on | unit | `tests/hosting/cost-alert-settings.test.tsx` | existing, extended | ✅ green |
| 66-11-03 | 11 | 6 | CST-04 | T-66-36, T-66-37 | Finance panel gated; locale recorded only when signed in | unit | `tests/hosting/finance-pages.test.tsx tests/hosting/fleet-page.test.tsx tests/locale-recording.test.tsx` | existing | ✅ green |
| 66-12-01 | 12 | 7 | CST-03, CST-04 | T-66-38 | Unmocked requests empty; zero axe violations; no horizontal scroll at 320/390 | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` | existing | ✅ green |
| 66-12-02 | 12 | 7 | CST-03, CST-04 | - | Parity rows guarded | unit | `tests/hosting/parity-matrix.test.ts` | existing, extended | ✅ green |
| 66-12-03 | 12 | 7 | CST-03..CST-06 | T-66-39 | Full gate plus human UAT | full + manual | `dotnet test X39.UnitedTacticalForces.slnx`, `npm --prefix sources/ReactApp run ci`, migration sync, OpenAPI `cmp`, e2e spec | n/a | ✅ green |
| 66-13-01 | 13 | 8 | CST-03, CST-04 | T-66-40 | A 5-minute Hetzner session reports a full started hour through the accrued-cost endpoint | integration (tracer) | `FullyQualifiedName~AccruedCostEndpointTests\|FullyQualifiedName~CostAccrualCalculatorTests\|FullyQualifiedName~CostAlertEvaluatorTests\|FullyQualifiedName~UatFleetScenarioSeederTests\|FullyQualifiedName~CostAlertPublisherTests` | existing | ✅ green |
| 66-13-02 | 13 | 8 | CST-03 | T-66-41 | Billing rule holds at hour boundaries, across slices, after resolution and for another unit; integer arithmetic | unit | `FullyQualifiedName~RuntimeBillingUnitTests\|FullyQualifiedName~CostAccrualCalculatorTests\|FullyQualifiedName~HetznerRuntimeProviderTests\|FullyQualifiedName~EmulatedHetznerRuntimeProvider` | existing | ✅ green |
| 66-13-03 | 13 | 8 | CST-04 | T-66-40 | A threshold is crossed at minute 1 of a Hetzner session; the UAT server shows the hour step | integration + build | `FullyQualifiedName~CostAlertEvaluatorTests\|FullyQualifiedName~UatFleetScenarioSeederTests\|FullyQualifiedName~AccruedCostEndpointTests\|FullyQualifiedName~CostAccrualCalculatorTests` and `dotnet build X39.UnitedTacticalForces.slnx` | existing | ✅ green |
| 66-14-01 | 14 | 8 | CST-04 | T-66-42, T-66-43, T-66-44 | Dismiss all sends the shown ids; the API dismisses only the caller's own visible ones and answers 204 | integration + contract | `FullyQualifiedName~CostAlertServiceTests`, OpenAPI export + `cmp`, `contracts:check`, `tests/hosting/cost-alert-services.test.ts tests/hosting/cost-alert-banner.test.tsx` | existing | ✅ green |
| 66-14-02 | 14 | 8 | CST-04 | - | Alerts never vanish without a dismissal: failed refetches, redelivered and stale frames | unit | `tests/hosting/cost-alert-persistence.test.tsx tests/hosting/fleet-live-updates.test.tsx tests/hosting/cost-alert-banner.test.tsx tests/hosting/cost-alert-menu.test.tsx tests/hosting/cost-frames.test.ts` | existing | ✅ green |
| 66-15-01 | 15 | 8 | CST-04 | T-66-45 | Settings sections render as raised cards in a capped column on the page and the finance panel | unit | `tests/hosting/cost-alert-settings.test.tsx tests/hosting/cost-alerts-page.test.tsx tests/hosting/finance-pages.test.tsx tests/styles` | existing | ✅ green |
| 66-15-02 | 15 | 8 | CST-05, CST-06 | T-66-45 | Channel rows show a status badge; amount, language and save read as one form | unit | `tests/hosting/alert-settings-model.test.ts tests/hosting/cost-alert-settings.test.tsx tests/locales` and `locales:check` | existing | ✅ green |
| 66-15-03 | 15 | 8 | CST-04 | - | UI-SPEC amended, page reviewed in screenshots, e2e journey green | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` and a scoped Prettier check | existing | ✅ green |
| 66-16-01 | 16 | 9 | CST-03 | T-66-46, T-66-47 | REST accrued value carries the billing unit and the strip says when the next hour starts; null for hidden owners | integration + contract | `FullyQualifiedName~AccruedCostEndpointTests`, OpenAPI export + `cmp`, `contracts:check`, `tests/hosting/accrued-model.test.ts tests/hosting/accrued-cost.test.tsx` | existing | ✅ green |
| 66-16-02 | 16 | 9 | CST-03 | T-66-46, T-66-47 | Pushed accrued frame carries the same billing facts to the same audience; malformed frames dropped | integration + unit | `FullyQualifiedName~CostAlertPublisherTests\|FullyQualifiedName~UpdateStreamServiceSendTests`, `tests/hosting/cost-frames.test.ts tests/hosting/accrued-cost.test.tsx` | existing | ✅ green |
| 66-16-03 | 16 | 9 | CST-03 | - | Accrued row amended in the UI-SPEC and the billing line proven in the e2e journey | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` and `tests/hosting/cost-strip.test.tsx` | existing | ✅ green |
| 66-17-01 | 17 | 10 | CST-04 | T-66-48 | Every accepted dismissal shows a toast naming the server and amount, also when the banner disappears | unit | `tests/hosting/cost-alert-banner.test.tsx tests/hosting/cost-alert-menu.test.tsx tests/hosting/cost-alert-persistence.test.tsx tests/profile-menu.test.tsx tests/locales` | existing | ✅ green |
| 66-17-02 | 17 | 10 | CST-04 | T-66-49 | Banner is one quiet panel with threshold rows and a single footer; cost-stop only requests the shared confirmation | unit | `tests/hosting/cost-alert-banner.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/cost-alerts-source-scan.test.ts tests/styles` | existing | ✅ green |
| 66-17-03 | 17 | 10 | CST-04 | T-66-49 | Menu rows match the server rows; UI-SPEC, screenshots and e2e record the redesign | unit + e2e | `tests/hosting/cost-alert-menu.test.tsx tests/profile-menu.test.tsx tests/hosting/cost-alert-banner.test.tsx tests/styles` and the Phase 66 e2e spec | existing | ✅ green |
| 66-18-01 | 18 | 11 | CST-04 | T-66-50 | In a real browser alerts leave only through the viewer's dismissal; unmocked requests empty | e2e (tracer) | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts` | existing | ✅ green |
| 66-18-02 | 18 | 11 | CST-03..CST-06 | - | Parity matrix and validation map describe the gap closure with test evidence | unit | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` | existing | ✅ green |
| 66-18-03 | 18 | 11 | CST-03..CST-06 | T-66-51 | Full API and React gates, migration sync, OpenAPI `cmp`, then the end-of-phase human UAT | full + manual | `dotnet test X39.UnitedTacticalForces.slnx` (without the known pre-existing failure), migration sync, OpenAPI `cmp`, React gates, scoped Prettier, e2e spec | existing | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

No separate Wave 0 plan: every test file below is created by the TDD task that needs it (see the per-task map).

- [x] `tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs`, `AccruedCostEndpointTests.cs` — CST-03 (66-01)
- [x] `CostOwnerViewAudienceTests.cs` (drift test; replaces the planned `CostAlertAudienceTests.cs`) — CST-03 (66-01)
- [x] `CostAlertMessageRendererTests.cs`, `CostAlertResourceTests.cs`, `DiscordDirectMessageSenderTests.cs`, `SmtpEmailSenderTests.cs` — CST-05, CST-06 (66-02)
- [x] `CostAlertEvaluatorTests.cs`, `CostAlertRecipientResolverTests.cs` — CST-04, no-deliverable-address, restart idempotency (66-03)
- [x] `UatFleetScenarioSeederTests.cs` — UAT harness (66-04)
- [x] `CostAlertServiceTests.cs`, `UpdateStreamServiceSendTests.cs`, `CostAlertPublisherTests.cs` — CST-03, CST-04 (66-05)
- [x] `CostAlertSettingsTests.cs`, `CostAlertServerSummaryTests.cs`, `UserLocaleEndpointTests.cs` — CST-04 (66-06)
- [x] `CostAlertDispatcherTests.cs`, `CostAlertEmailDeliveryTests.cs` + fakes `RecordingDiscordDirectMessageSender.cs`, `RecordingEmailSender.cs` — CST-05, CST-06 (66-07; the email filter is `FullyQualifiedName~CostAlertEmail`)
- [x] React: `tests/hosting/accrued-cost.test.tsx`, `accrued-model.test.ts`, `cost-alerts-source-scan.test.ts`, `cost-alert-services.test.ts` (66-08); `cost-frames.test.ts`, `cost-alert-banner.test.tsx`, `cost-alert-menu.test.tsx` (66-09); `cost-alerts-page.test.tsx`, `alert-settings-model.test.ts`, `cost-alert-settings.test.tsx` (66-10); `tests/locale-recording.test.tsx` (66-11); `tests/e2e/hosting-cost-alerts.spec.ts` (66-12)

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live WebSocket alert push into banner/menu | CST-04 | Mocks cannot prove the live push | 66-12 task 3 human-check: `./scripts/Start-Uat.ps1 -Fresh`, open "UAT 9 Costing (accrued cost)", set amount 1, watch banner, menu and toast |
| Email arrives once, also after a restart | CST-06 | Needs a running SMTP sink | 66-12 task 3 human-check: Mailpit inbox at http://localhost:8025 |
| Real Discord DM to a linked account | CST-05 | Needs real bot; optional, skippable | 66-12 task 3 human-check step 7 (optional); fake sender covers logic |
| Gap closure (2026-11-21): started-hour accrued value and its step, an alert at the started hour, the dismissal toast, alerts surviving an API restart until dismissed, and the look of the banner, menu rows and settings page | CST-03, CST-04, CST-05, CST-06 | Live billing steps, socket pushes across a real restart and visual quality can only be judged by the user who reported the gaps | 66-18 task 3 human-check: `./scripts/Start-Uat.ps1 -Fresh` (restart without reseeding: `./scripts/Start-Uat.ps1`), steps 1 to 5 required, step 6 (Mailpit) optional; automated gates green, human answers recorded in 66-18-SUMMARY.md |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (only 66-02-02 is a blocking human checkpoint)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (each test file is created by its TDD task)
- [x] No watch-mode flags (`vitest run`, `dotnet test`)
- [ ] Feedback latency < 30s (per-task filters; the first API build in a fresh worktree can exceed it)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-11-22 (Nyquist audit: API slice 263/263, React slice green after clock-pinning fix ea759b42)

## Validation Audit 2026-11-22

| Metric | Count |
|---|---|
| Gaps found | 7 |
| Resolved | 7 |
| Escalated | 0 |

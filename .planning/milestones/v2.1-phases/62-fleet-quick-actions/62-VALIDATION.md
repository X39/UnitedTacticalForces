---
phase: "62"
slug: fleet-quick-actions
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-18"
---

# Phase 62 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Source: `62-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 + jsdom + Testing Library (React); xunit 2.9.3 net10.0 (API); Playwright 1.62.1 + axe (e2e) |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-model.test.ts tests/hosting/fleet-page.test.tsx` |
| **Full suite command** | `npm --prefix sources/ReactApp run ci` ; `dotnet test tests/Api.Tests` ; `npx playwright test tests/e2e/hosting-fleet.spec.ts` |
| **Estimated runtime** | ~120 seconds (React + API); e2e separate |

---

## Sampling Rate

- **After every task commit:** touched test file only, plus typecheck when types changed; API tasks run the new fixture filter
- **After every plan wave:** whole React suite, lint, locales:check; `dotnet test tests/Api.Tests`
- **Before `/gsd-verify-work`:** `npm run ci`, full dotnet test, Playwright fleet spec, `routes.json` diff clean, `contracts:check` clean
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

Populated by the planner per task (all commands run from the repository root; each task also runs `typecheck` and `lint`
unless noted):

| Plan | Task | Requirement | Test Type | Automated Command | File Exists | Status |
|------|------|-------------|-----------|-------------------|-------------|--------|
| 62-01 | 1 tracer | FLT-04 | xunit + component | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetSignalTracer"` and `npm --prefix sources/ReactApp test -- tests/hosting/fleet-signal-tracer.test.tsx` | ✅ | ✅ green |
| 62-01 | 2 fixtures | FLT-01 | unit | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-fixtures.test.ts` | ✅ | ✅ green |
| 62-02 | 1 projection | FLT-02 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostOwnerUserId"` | ✅ | ✅ green |
| 62-02 | 2 contract | FLT-02 | contract | `git add sources/ReactApp/openapi/utf-api.json sources/ReactApp/src/generated/api.ts && npm --prefix sources/ReactApp run contracts:check` | ✅ | ✅ green |
| 62-03 | 1 locales | ACC-02 | unit + gate | `npm --prefix sources/ReactApp test -- tests/locales` and `npm --prefix sources/ReactApp run locales:check` | ✅ | ✅ green |
| 62-03 | 2 entry | ACC-02 | component | `npm --prefix sources/ReactApp test -- tests/hosting/server-section-entry.test.tsx` | ✅ | ✅ green |
| 62-03 | 3 onSubmitted | FLT-04 | component | `npm --prefix sources/ReactApp test -- tests/hosting/cost-confirmation-provider.test.tsx tests/hosting/cost-confirm-model.test.ts` | ✅ | ✅ green |
| 62-04 | 1 interceptor | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetChangeInterceptor"` | ✅ | ✅ green |
| 62-04 | 2 executor | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LifecycleJobExecutorCloudTests"` | ✅ | ✅ green |
| 62-05 | 1 notifier | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetChangeNotifier"` | ✅ | ✅ green |
| 62-05 | 2 gate | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateStreamControllerFleet"` | ✅ | ✅ green |
| 62-05 | 3 keep-alive and docs | FLT-04 | build + grep | `dotnet build sources/Api/Api.csproj` | ✅ | ✅ green |
| 62-06 | 1 client | FLT-04 | unit (fake timers) | `npm --prefix sources/ReactApp test -- tests/app/update-stream.test.tsx` | ✅ | ✅ green |
| 62-06 | 2 provider and layouts | FLT-04 | component | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-live-updates.test.tsx tests/hosting/cost-confirm-layouts.test.tsx` | ✅ | ✅ green |
| 62-07 | 1 search and partition | FLT-02, FLT-03 | unit | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-model.test.ts` | ✅ | ✅ green |
| 62-07 | 2 primary action and diff | FLT-01, FLT-04 | unit | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-model.test.ts tests/hosting/server-state-single-owner.test.ts` | ✅ | ✅ green |
| 62-08 | 1 search and chips | FLT-02 | component | `npm --prefix sources/ReactApp test -- tests/search-field.test.tsx tests/filter-chip-group.test.tsx tests/styles` | ✅ | ✅ green |
| 62-08 | 2 toolbar | FLT-02 | component | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-toolbar.test.tsx` | ✅ | ✅ green |
| 62-08 | 3 notice | FLT-04 | component | `npm --prefix sources/ReactApp test -- tests/primitives.test.tsx tests/hosting/fleet-live-notice.test.tsx` | ✅ | ✅ green |
| 62-09 | 1 card and section | FLT-01, FLT-04, FLT-05 | component + guard | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-card.test.tsx tests/styles` | ✅ | ✅ green |
| 62-09 | 2 suite run | FLT-05 | full suite | `npm --prefix sources/ReactApp test` | ✅ | ✅ green |
| 62-10 | 1 submitted | FLT-04 | hook | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-submitted.test.tsx` | ✅ | ✅ green |
| 62-10 | 2 focus | FLT-04 | hook | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-focus.test.tsx` | ✅ | ✅ green |
| 62-10 | 3 announcements | FLT-04 | hook | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-announcements.test.tsx` | ✅ | ✅ green |
| 62-11 | 1 page | FLT-01, FLT-02, FLT-03, FLT-04, ACC-02 | component | `npm --prefix sources/ReactApp test -- tests/hosting/fleet-page.test.tsx tests/styles` | ✅ | ✅ green |
| 62-11 | 2 cleanup and migration | FLT-01, ACC-02 | full suite | `npm --prefix sources/ReactApp test` | ✅ | ✅ green |
| 62-12 | 1 e2e | FLT-04, FLT-05, ACC-02 | e2e + axe | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-fleet.spec.ts tests/e2e/public-shell.spec.ts tests/e2e/hosting-a11y.spec.ts tests/e2e/hosting-placement.spec.ts` | ✅ | ✅ green |
| 62-12 | 2 parity and docs | FND-07 | unit | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` | ✅ | ✅ green |
| 62-12 | 3 gate | all | gate | `npm --prefix sources/ReactApp run ci` and `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"` and `git diff --exit-code -- sources/Common/Ui/routes.json` | ✅ | ✅ green |
| 62-13 | 1 per-user path and registration | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateStreamControllerFleet\|FullyQualifiedName~FleetSignalTracer"` | ✅ | ✅ green |
| 62-13 | 2 user-derived, isolated path | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateStreamControllerFleet"` | ✅ | ✅ green |
| 62-14 | 1 audience rule | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HostingFleetAudience"` | ✅ | ✅ green |
| 62-14 | 2 audience source | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetAudienceSource"` | ✅ | ✅ green |
| 62-15 | 1 claim-scoped tracer | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetChangeNotifier\|FullyQualifiedName~FleetSignalTracer"` | ✅ | ✅ green |
| 62-15 | 2 matrix, revoke, fail closed | FLT-04 | xunit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FleetChangeNotifier\|FullyQualifiedName~FleetSignalTracer"` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/setup.ts` — `FakeWebSocket` stub (present; the whole Vitest suite passes, 107 files, 918 tests)
- [x] `tests/mocks/react-icons.tsx` — add `Checkmark16Regular`, `PlugDisconnected20Regular` (both present)
- [x] `fleet-model`, `fleet-card`, `fleet-page`, `fleet-live-updates`, `update-stream` tests and `hosting-fleet.spec.ts`
  (all present and green; `hosting-fleet.spec.ts` 20 tests, also stable under `--repeat-each 3`)
- [x] API fixtures: `FleetChangeInterceptorTests`, `FleetChangeNotifierTests`, `UpdateStreamControllerFleetTests`,
  `CostOwnerUserId` cases in `GameServerProjectionServiceTests` (all under `tests/Api.Tests/GameServerHosting`, 99
  tests across the per-plan filters, all green)
- [x] Update `fleet.test.tsx`, two `public-shell.spec.ts` assertions, e2e mock helpers (`fleet.test.tsx` migrated in
  62-11; the nav label and the German fleet deep-link assertions migrated in 62-03 and 62-12;
  `tests/e2e/support/fleetStream.ts` added in 62-12)

---

## Phase Gate Results (62-12, 2026-11-19)

| Gate | Command | Result |
|------|---------|--------|
| React CI | `npm --prefix sources/ReactApp run ci` | ✅ exit 0: locales, contracts drift, Prettier, ESLint, TypeScript, Vitest 107 files and 918 tests, production build |
| Hosting API tests | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"` | ✅ 580 passed, 0 failed |
| Browser evidence | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-fleet.spec.ts tests/e2e/public-shell.spec.ts tests/e2e/hosting-a11y.spec.ts tests/e2e/hosting-placement.spec.ts` | ✅ 61 passed |
| Route manifest | `git diff --exit-code -- sources/Common/Ui/routes.json` | ✅ byte-identical |
| Classic WebApp and database untouched | `git diff --name-only de6261e4 -- sources/WebApp sources/Database` | ✅ empty |
| Parity matrix (FND-07) | `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts` | ✅ 74 passed; no Phase 62 row pending |

Gate fix: `npm run ci` first failed `format:check` on the two pre-existing offenders from `deferred-items.md`.
`tests/hosting/fleet-fixtures.test.ts` (Plan 62-01) was reformatted with Prettier. `src/auth/claims.ts` only had CRLF
line endings in the working copy; normalizing them produced no content change.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live signal through nginx proxy (keepalive) | FLT-04 | Proxy timeouts not reproducible in tests | Open fleet behind nginx, idle >60s, change a server elsewhere, confirm card updates |
| UAT: primary action of attention and unconfirmed cards | FLT-04, FLT-01 | Product judgment about a dead-end disabled control on cards that may still cost money | Open the fleet with a failed deployment (attention) and an unconfirmed stop. The card shows a disabled Shut down and stop billing with the deployment-state reason, because the API enables stop only while the deployment is Running, plus an Open server link. The product owner confirms this literal UI-SPEC behavior or asks for Open server to become the primary action in those two states (UI-SPEC, research open question 1) |
| UAT: claim-scoped fleet delivery (G-62-2 re-test) | FLT-04 | The xunit suites prove the audience rule and the per-recipient delivery path, but not the deployed behavior with a real browser socket, two sessions and real claims | Sign in as a user whose only server claim is `sv:view` bound to one server. Open the fleet with the browser developer tools on the update stream WebSocket. From another session, change a different server and confirm no frame arrives. Then change the permitted server and confirm a frame arrives and its card updates without a page reload (re-run of UAT test 2, D-17) |
| German text at 320 and 390 px | FLT-05 | Pixel baselines arrive with the Phase 69 Playwright mobile projects | Verified here by layout metrics (no horizontal scroll, controls in the viewport, no clipped chips, titles or owners) and axe only; a visual review of the German fleet on a phone is part of the end-of-phase UAT |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s (per-task commands; the full gate runs once before verification)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-11-19 (automated gate green; the manual-only and UAT rows above remain for the end-of-phase
review)

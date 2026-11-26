---
phase: "65"
slug: "change-a-server"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-11-20"
---

# Phase 65 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Frontend: vitest 4.1.10 + @testing-library/react 16.3.2 (jsdom 28); e2e: Playwright 1.62.1 + @axe-core/playwright 4.13.0; API: xUnit (SQLite in-memory `HostingTestStore`) |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/tests/setup.ts`, `sources/ReactApp/playwright.config.ts`, `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `npm --prefix sources/ReactApp test -- <touched test files>` or `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~<TestClass>"` |
| **Full suite command** | `npm --prefix sources/ReactApp run ci` and `dotnet test X39.UnitedTacticalForces.slnx`; phase gate adds the hosting e2e specs, the OpenAPI export comparison and `dotnet ef migrations has-pending-model-changes` (plan 65-15 Task 3) |
| **Estimated runtime** | ~65 seconds per quick run (e2e specs ~2-4 minutes each, run only where listed) |

---

## Sampling Rate

- **After every task commit:** Run the task's `<automated>` command (touched test files plus `typecheck` and `lint` for the React app, the filtered test class for the API)
- **After every plan wave:** Run `npm --prefix sources/ReactApp test`, `npm --prefix sources/ReactApp run locales:check` and `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 65 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 65-01-01 | 01 | 1 | CHG-04 | T-65-01, T-65-02 | Pending flags are written only inside authorized, non-archived change paths | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PendingStartChangeTracking"` | ❌ W0 (created in task) | ⬜ pending |
| 65-01-02 | 01 | 1 | CHG-04 | — | N/A (schema gate) | schema | `dotnet ef migrations has-pending-model-changes --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext && dotnet build X39.UnitedTacticalForces.slnx` | ✅ | ⬜ pending |
| 65-01-03 | 01 | 1 | CHG-04 | T-65-03 | Start consumes only the mod pack area; interceptor signals other viewers | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PendingStartChangeTracking\|FullyQualifiedName~FleetChangeInterceptor\|FullyQualifiedName~GameServerLifecycleServiceTests"` | ✅ (extends) | ⬜ pending |
| 65-02-01 | 02 | 1 | CHG-03 | T-65-04, T-65-05 | Draft model has no storage or console; failures map from ApiError.code only | unit | `npm --prefix sources/ReactApp test -- tests/hosting/server-change-draft.test.ts && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-02-02 | 02 | 1 | CHG-03 | T-65-04, T-65-06 | Provider never writes storage; review descriptors carry no secret value | unit + hook | `npm --prefix sources/ReactApp test -- tests/hosting/server-change-draft.test.ts tests/hosting/use-server-change-draft.test.tsx && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-02-03 | 02 | 1 | CHG-03 (copy for CHG-01..07) | — | N/A | locale | `npm --prefix sources/ReactApp test -- tests/locales && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ✅ (extends) | ⬜ pending |
| 65-03-01 | 03 | 1 | CHG-05 | T-65-07, T-65-08 | ?tab= resolved by exact match; guard blocks only while dirty | component | `npm --prefix sources/ReactApp test -- tests/leave-guard.test.tsx tests/hosting/workspace-sections.test.ts tests/primitives.test.tsx && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-03-02 | 03 | 1 | CHG-03 | — | N/A | component + style | `npm --prefix sources/ReactApp test -- tests/primitives.test.tsx tests/form-fields.test.tsx tests/styles && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ✅ (extends) | ⬜ pending |
| 65-04-01 | 04 | 2 | CHG-07, CHG-04 | T-65-09, T-65-10, T-65-11 | Archived servers reject credential writes with 409; no value in responses or audit details | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerSecret"` | ❌ W0 (controller test created in task) | ⬜ pending |
| 65-04-02 | 04 | 2 | CHG-04 | T-65-12 | Consumption under the server-mutation lock, primary command only | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LifecycleJobExecutorCloudTests\|FullyQualifiedName~PendingStartChangeTracking"` | ✅ (extends) | ⬜ pending |
| 65-05-01 | 05 | 2 | CHG-01 | T-65-13 | Contents need Server.View; archived, unknown or cross-game packs 404 | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerModPackContent\|FullyQualifiedName~OpenApiRouteCompatibility"` | ❌ W0 (created in task) | ⬜ pending |
| 65-05-02 | 05 | 2 | CHG-01 | T-65-14, T-65-15 | Pack validated (active, game, revision, resolvable) before assignment; pin cleared | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerModPackContent\|FullyQualifiedName~GameServerLifecycleServiceTests\|FullyQualifiedName~GameServerProjectionServiceTests\|FullyQualifiedName~GameServerCreationCostOwnerOptions"` | ✅ (extends) | ⬜ pending |
| 65-06-01 | 06 | 2 | CHG-02 | T-65-16 | No sensitive definition is Common | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameConfigurationTier\|FullyQualifiedName~GameServerConfigurationService"` | ❌ W0 (created in task) | ⬜ pending |
| 65-06-02 | 06 | 2 | CHG-02 | T-65-17 | Pinned revision must belong to the profile (existing check) | API unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameConfigurationTier\|FullyQualifiedName~ConfigurationProfilesControllerTests\|FullyQualifiedName~OpenApiRouteCompatibility"` | ✅ (extends) | ⬜ pending |
| 65-07-01 | 07 | 2 | CHG-03, CHG-04 | T-65-18, T-65-20 | Apply mutation has no variables and gcTime 0; API re-authorizes | component | `npm --prefix sources/ReactApp test -- tests/hosting/change-flow.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/arma-placement.test.tsx tests/hosting/workspace-section-panels.test.tsx tests/hosting/server-state-single-owner.test.ts && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (change-flow created in task) | ⬜ pending |
| 65-07-02 | 07 | 2 | CHG-03 | T-65-19 | No secret value or Error.message in the review dialog | component + scan + e2e | `npm --prefix sources/ReactApp test -- tests/hosting/review-changes-dialog.test.tsx tests/hosting/change-source-scan.test.ts tests/hosting/change-flow.test.tsx tests/styles && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-placement.spec.ts` | ❌ W0 (created in task) | ⬜ pending |
| 65-08-01 | 08 | 2 | CHG-06 | T-65-21, T-65-23 | Strict ?path= parser before any request; links built from normalized paths | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/files-location.test.ts tests/hosting/files-panel-location.test.tsx tests/hosting/workspace-page.test.tsx tests/styles && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-08-02 | 08 | 2 | CHG-06, CHG-05 | T-65-22 | Fallback only for non-root paths, one replace, no loop | component | `npm --prefix sources/ReactApp test -- tests/hosting/files-panel-location.test.tsx tests/hosting/files-location.test.ts tests/hosting/workspace-page.test.tsx tests/leave-guard.test.tsx && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ✅ (extends) | ⬜ pending |
| 65-09-01 | 09 | 3 | CHG-04 | T-65-24, T-65-25 | Snapshot and types regenerated by tooling; banner reads only the API signal | contract + component | `dotnet build sources/Api/Api.csproj && D=$(mktemp -d) && OUT="$(cygpath -m "$D" 2>/dev/null \|\| echo "$D")/utf-api.json" && dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi "$OUT" && cmp "$OUT" sources/ReactApp/openapi/utf-api.json && npm --prefix sources/ReactApp run contracts:check && npm --prefix sources/ReactApp test -- tests/hosting/pending-start-notice.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/server-state-single-owner.test.ts tests/hosting/create-server-page.test.tsx && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-09-02 | 09 | 3 | CHG-01, CHG-02 | T-65-26 | N/A | contract unit | `npm --prefix sources/ReactApp test -- tests/hosting/contract-shape.test.ts tests/hosting/workspace-services.test.ts tests/hosting/parity-matrix.test.ts tests/hosting/workspace-model.test.ts tests/hosting/arma-placement.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/change-flow.test.tsx tests/hosting/server-change-draft.test.ts tests/hosting/configuration-profiles.test.tsx && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ❌ W0 (contract-shape created in task) | ⬜ pending |
| 65-10-01 | 10 | 3 | CHG-05 | T-65-27 | Leave dialog uses fixed copy with counts only | component | `npm --prefix sources/ReactApp test -- tests/hosting/settings-leave-guard.test.tsx tests/hosting/change-flow.test.tsx tests/leave-guard.test.tsx tests/hosting/deep-link.test.tsx && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-10-02 | 10 | 3 | CHG-03, CHG-05 | T-65-27, T-65-28 | Announcements carry no values; read-only mirrors API authority | component | `npm --prefix sources/ReactApp test -- tests/hosting/change-flow.test.tsx tests/hosting/settings-leave-guard.test.tsx tests/hosting/arma-placement.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/change-source-scan.test.ts tests/styles && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ✅ (extends) | ⬜ pending |
| 65-11-01 | 11 | 3 | CHG-07, CHG-03 | T-65-30 | Typed secret absent from DOM after apply and from the review | component | `npm --prefix sources/ReactApp test -- tests/hosting/credentials-panel.test.tsx tests/hosting/workspace-section-panels.test.tsx tests/hosting/change-flow.test.tsx tests/styles && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-11-02 | 11 | 3 | CHG-07 | T-65-29, T-65-31, T-65-32 | Reveal is transient (30 s, visibility, unmount); mutation cache, storage and console stay clean | component + scan | `npm --prefix sources/ReactApp test -- tests/hosting/credentials-panel.test.tsx tests/hosting/change-source-scan.test.ts && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ✅ (extends) | ⬜ pending |
| 65-12-01 | 12 | 4 | CHG-02, CHG-03 | T-65-33, T-65-34 | Sensitive entries read-only and never staged; API text as React text | component | `npm --prefix sources/ReactApp test -- tests/hosting/configuration-panel.test.tsx tests/hosting/arma-placement.test.tsx tests/hosting/change-flow.test.tsx tests/styles && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-12-02 | 12 | 4 | CHG-02, CHG-03 | T-65-35 | Client proposes only a valid latest revision id | component + scan + e2e | `npm --prefix sources/ReactApp test -- tests/hosting/configuration-panel.test.tsx tests/hosting/arma-placement.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/change-flow.test.tsx tests/hosting/review-changes-dialog.test.tsx && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check && npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-placement.spec.ts` | ✅ (extends) | ⬜ pending |
| 65-13-01 | 13 | 4 | CHG-01, CHG-03 | T-65-36 | Titles rendered as React text only | component | `npm --prefix sources/ReactApp test -- tests/hosting/mod-pack-panel.test.tsx tests/hosting/workspace-section-panels.test.tsx tests/hosting/change-flow.test.tsx tests/styles && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint` | ❌ W0 (created in task) | ⬜ pending |
| 65-13-02 | 13 | 4 | CHG-01 | T-65-36, T-65-37 | Contents load only for the saved and staged pack of a viewable server | unit + component + scan | `npm --prefix sources/ReactApp test -- tests/hosting/mod-pack-diff.test.ts tests/hosting/mod-pack-panel.test.tsx tests/hosting/review-changes-dialog.test.tsx tests/styles && npm --prefix sources/ReactApp run locales:check && npm --prefix sources/ReactApp run typecheck && npm --prefix sources/ReactApp run lint && npm --prefix sources/ReactApp run format:check` | ❌ W0 (mod-pack-diff created in task) | ⬜ pending |
| 65-14-01 | 14 | 5 | CHG-01, CHG-02, CHG-03, CHG-04 | T-65-38, T-65-39 | Typed credential absent from the review dialog in a real browser; no unmocked request | e2e + axe | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-change.spec.ts` | ❌ W0 (created in task) | ⬜ pending |
| 65-14-02 | 14 | 5 | CHG-03, CHG-05 | T-65-39 | N/A | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-change.spec.ts` | ✅ (extends) | ⬜ pending |
| 65-15-01 | 15 | 6 | CHG-06 | T-65-41 | Traversal and missing links fall back without loops in a real browser | e2e + axe | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-files-secrets.spec.ts` | ❌ W0 (created in task) | ⬜ pending |
| 65-15-02 | 15 | 6 | CHG-07 | T-65-40 | Revealed value absent from DOM, storage, URL and console after auto-hide | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-files-secrets.spec.ts` | ✅ (extends) | ⬜ pending |
| 65-15-03 | 15 | 6 | CHG-01..07 | T-65-42 | Contract, schema and route drift gates | gate | plan 65-15 Task 3 verify block (parity test, `ci`, hosting e2e specs, routes.json diff, API tests, has-pending-model-changes, OpenAPI cmp) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No framework install is needed (vitest, Playwright, axe and xUnit are in place). Every missing test file is created by the task that first needs it, before or with its implementation:

- [ ] `tests/Api.Tests/GameServerHosting/PendingStartChangeTrackingTests.cs` — CHG-04 (65-01-01)
- [ ] `tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs` — CHG-07 (65-04-01)
- [ ] `tests/Api.Tests/GameServerHosting/GameServerModPackContentTests.cs` — CHG-01 (65-05-01)
- [ ] `tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs` — CHG-02 (65-06-01)
- [ ] `sources/ReactApp/tests/helpers/configurationFixtures.ts` — shared entry factory (65-02-01)
- [ ] `sources/ReactApp/tests/hosting/server-change-draft.test.ts`, `use-server-change-draft.test.tsx` — CHG-03 (65-02-01, 65-02-02)
- [ ] `sources/ReactApp/tests/leave-guard.test.tsx` — CHG-05 (65-03-01)
- [ ] `sources/ReactApp/tests/hosting/change-flow.test.tsx`, `review-changes-dialog.test.tsx`, `change-source-scan.test.ts` — CHG-03 (65-07-01, 65-07-02)
- [ ] `sources/ReactApp/tests/hosting/files-location.test.ts`, `files-panel-location.test.tsx` — CHG-06 (65-08-01)
- [ ] `sources/ReactApp/tests/hosting/pending-start-notice.test.tsx`, `contract-shape.test.ts` — CHG-04, CHG-02 (65-09-01, 65-09-02)
- [ ] `sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx` — CHG-05 (65-10-01)
- [ ] `sources/ReactApp/tests/hosting/credentials-panel.test.tsx` — CHG-07 (65-11-01)
- [ ] `sources/ReactApp/tests/hosting/configuration-panel.test.tsx` — CHG-02 (65-12-01)
- [ ] `sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx`, `mod-pack-diff.test.ts` — CHG-01 (65-13-01, 65-13-02)
- [ ] `sources/ReactApp/tests/e2e/hosting-change.spec.ts` — CHG-01..05 (65-14-01)
- [ ] `sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts` — CHG-06, CHG-07 (65-15-01)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Native unload prompt wording after tab close | CHG-05 | Browsers show their own non-customizable prompt; automation proves only that `beforeunload` is prevented while dirty | Optional during UAT: stage a change, close the tab, confirm the browser asks; skipping it does not block the phase |

All other phase behaviors have automated verification. Visual baselines (sticky bar over focus, German at 390 with the dialog open, long Advanced lists) are backstops owned by Phase 69 (VER-03).

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [ ] Feedback latency < 65s (unit and API runs yes; the e2e tasks 65-07-02, 65-12-02, 65-14-*, 65-15-* take longer by design)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** {pending / approved YYYY-MM-DD}

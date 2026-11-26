---
phase: "69"
slug: "consistency-accessibility-verification"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-22"
---

# Phase 69 — Validation Strategy

> Per-phase validation contract. Source of truth: `69-RESEARCH.md` § Validation Architecture and the auto-decisions in `69-CONTEXT.md`.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 (jsdom, `tests/setup.ts`) and Testing Library; Playwright 1.62.1 (Chromium) mocked e2e (`tests/e2e`, projects chromium, mobile-320, mobile-390); Playwright seeded-stack UAT (`tests/uat`); ESLint (`eslint.config.js`); `scripts/check-locales.mjs` |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `sources/ReactApp/playwright.uat.config.ts`, `sources/ReactApp/eslint.config.js` |
| **Quick run command** | `npx vitest run <file>` (run in `sources/ReactApp`), plus `npx eslint <touched files>` |
| **Lint command** | `npm run lint` (in `sources/ReactApp`) |
| **Locale command** | `npm run locales:check` (and `node scripts/check-locales.mjs --unused`) |
| **Mocked e2e command** | `npm run test:e2e` (`playwright.config.ts`); audit subset `npx playwright test tests/e2e/audit --project=chromium` |
| **Seeded-stack UAT command** | `npm run test:uat` (`playwright.uat.config.ts`, after `scripts/Start-Uat.ps1 -Fresh -NoBrowser`) |
| **Full suite command** | `npm run ci`, then `npm run test:e2e` |
| **dotnet tests** | Not relevant: Phase 69 is ReactApp-only and changes no API, contract or database code. Run `dotnet test X39.UnitedTacticalForces.slnx` only if an unplanned API change appears. |
| **Estimated runtime** | ~30 seconds (quick, per touched file), several minutes for the full suite |

## Sampling Rate

- **After every task commit:** run `npx vitest run <touched test files>` plus `npx eslint <touched files>` (each under 30 s)
- **After every plan wave:** run `npm run lint && npm run typecheck && npm test`, plus `npx playwright test tests/e2e/audit --project=chromium` once registry entries exist
- **Before `/gsd-verify-work`:** `npm run ci`, the full `npm run test:e2e`, and `npm run test:uat` against the seeded stack, with `npm run locales:check` green
- **Max feedback latency:** 30 seconds (per task), 300 seconds (per wave)

## Per-Task Verification Map

Rows were seeded before planning and are now mapped to the tests the 34 plans delivered. Audited 2026-11-25 on the merged tree (HEAD 19b49338): `npm run locales:check`, `npm run locales:unused`, `npm run typecheck`, `npm run lint` and `npx vitest run` green (207 files, 3266 tests); `npx playwright test tests/e2e/audit tests/e2e/journeys` green on chromium, mobile-390 and mobile-320 (621 passed, 263 opt-in/conditional skips, 0 failed, 0 flaky); 69-34 ran the full mocked e2e suite at 841 passed, 0 failed.

| Requirement | Behavior | Plans | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-------|-----------|-------------------|-------------|--------|
| CON-01 | `PageHeader` Display 28/700, token rhythm, no `clamp`, exactly one h1 per routed page | 69-01..29 | unit + e2e | `npx vitest run tests/primitives.test.tsx tests/styles/server-style-guard.test.ts`; `npx playwright test tests/e2e/audit/route-walk.spec.ts` | ✅ | ✅ green |
| CON-02 | Loading/Empty/Error/Forbidden anatomy and roles; `copy.ts` removed; Forbidden above-header rule (D-AUTO-3) | 69-02, 69-09 + slices | unit | `npx vitest run tests/state-views.test.tsx tests/locales/ownership.test.ts` | ✅ | ✅ green |
| CON-03 | Nav labels closed list, h1 equals label, `statusTones` closed lexicon, unknown maps to neutral | 69-02, 69-33 + slices | unit + e2e | `npx vitest run tests/status-tones.test.ts tests/locales/nav-labels.test.ts`; route walk | ✅ | ✅ green |
| CON-01..03 | No raw controls outside the ratchet; ratchet ceilings equal counts and never grow; closed `allowedRemainders`; Tier C hero allowlist (D-AUTO-4) | 69-03, 69-34 | static | `npm run lint`; `npx vitest run tests/ratchets tests/styles/legacy-ratchet.test.ts` | ✅ | ✅ green |
| FND-06 | axe `wcag2a/2aa/21a/21aa/22aa` zero violations, keyboard walk, reflow, text spacing, zoom, reduced motion over every registry entry | 69-04, 69-26..30 | e2e | `npx playwright test tests/e2e/audit/a11y-audit.spec.ts` (all three projects) | ✅ | ✅ green (S-07 de mobile-390 target-size intermittent in 69-34 runs; green in this audit, tracked in deferred-items-69-34.md) |
| FND-06 | Focus ring, contrast pairs, `--quiet` AA, no outline removal without ring | 69-01, 69-34 | static | `npx vitest run tests/styles` | ✅ | ✅ green |
| L10N-03 | No literal user text in Tier A; German walk; identical EN/DE values fail; unused keys fail CI | 69-05, 69-33 | static + e2e | `npm run locales:check && npm run locales:unused`; `npx vitest run tests/locales`; `npx playwright test tests/e2e/audit/german-walk.spec.ts` | ✅ | ✅ green |
| VER-01 | Route manifest equals Blazor `@page` set, URL parameter contract, link resolution, route walk | 69-06, 69-28 | unit + e2e | `npx vitest run tests/routes.test.ts tests/hosting/routes.test.ts tests/app`; route walk | ✅ | ✅ green |
| VER-02 | J-1 create→start, J-2 change mods, J-3 German, J-4 automation (mocked API) | 69-30 | e2e | `npx playwright test tests/e2e/journeys` | ✅ | ✅ green |
| VER-02 | Seeded create→waiting for worker (D-AUTO-6), change mod, automation, German, `@restarted` persistence | 69-32 | Playwright UAT | `npm run test:uat` (after `scripts/Start-Uat.ps1 -Fresh -NoBrowser`) | ✅ `tests/uat/journeys.spec.ts` | ⚠️ environment-gated — Deferred follow-up: fresh seeded run at milestone end |
| VER-03 | 320 and 390 px, EN and DE, layout-metric assertions on every registry entry; `@visual` baselines opt-in (D-AUTO-1) | 69-07, 69-31 | e2e | `npx playwright test --project=mobile-320 --project=mobile-390`; baselines `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` | ✅ `tests/e2e/mobile` | ✅ green (baselines manual-only) |
| FND-07 (closure) | No parity row `pending`; Phase 69 evidence on 26 rows | 69-34 | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | ✅ | ✅ green |
| All | Vendored font SHA-256 manifest, no `tests/` import from `src`, no pending registry ids, UAT hostname guard | 69-07, 69-34 | unit | `npx vitest run tests/e2e/fonts tests/hosting/screen-registry.test.ts` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

- [x] `sources/ReactApp/tests/e2e/support/mockApi.ts`, `serverScreens.ts`, `a11y.ts`, `germanLeak.ts`, `visual.ts`, fixtures adapter, `tests/hosting/screen-registry.test.ts` (FND-06, VER-02, VER-03)
- [x] `sources/ReactApp/tests/ratchets/*.json` plus ratchet and hard-coded-text tests, ESLint config updates (CON-01..03, L10N-03)
- [x] `sources/ReactApp/tests/e2e/fonts/` vendored fonts plus manifest test (VER-03)
- [x] `sources/ReactApp/src/app/urlParameters.ts`, `tests/app/url-parameters.test.ts`, `tests/app/route-links.test.ts` (VER-01)
- [x] `sources/ReactApp/tests/state-views.test.tsx`, `tests/status-tones.test.ts` (CON-02, CON-03)
- [x] Framework install: none (all pinned and installed)

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| First pixel baseline acceptance (`@visual` baselines) | VER-03 | a human must judge and accept the first generated baselines (D-AUTO-1) | Deferred follow-up: generate with `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` in `sources/ReactApp`, review the images, then commit them |
| Screen-reader pass | FND-06 | automated axe cannot judge announced reading order or wording | Deferred follow-up: walk the key server screens with NVDA or VoiceOver and record findings |
| Real device check at 320 and 390 px | VER-03 | emulated viewports do not cover real touch, fonts or browser chrome | Deferred follow-up: open the main server screens on a physical phone and record layout issues |
| Seeded-stack UAT run (fresh + restart) | VER-02 | needs the seeded Aspire stack; kept out of the automated gate per milestone-end policy | Deferred follow-up: `./scripts/Start-Uat.ps1 -Fresh -NoBrowser` then `npm run test:uat` in `sources/ReactApp` |
| Real-environment create→Running journey | VER-02 | needs a real provider and worker; the seeded journey stops at waiting-for-worker (D-AUTO-6) | Deferred follow-up: run at milestone end against a real environment |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s per task
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-25 (execute-phase verify:post hook)

## Validation Audit 2026-11-25

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

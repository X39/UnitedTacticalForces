---
phase: "63"
slug: workspace-shell-activity
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-19"
---

# Phase 63 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 + jsdom + Testing Library; Playwright 1.62.1 + axe 4.13.0 |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| **Quick run command** | `npm --prefix sources/ReactApp test -- tests/hosting/<file>` |
| **Full suite command** | `npm --prefix sources/ReactApp run ci` |
| **Estimated runtime** | ~120 seconds |

---

## Sampling Rate

- **After every task commit:** touched test files plus `npm --prefix sources/ReactApp run typecheck` and `lint`
- **After every plan wave:** `npm --prefix sources/ReactApp test`, `locales:check`, style and single-owner guards
- **Before `/gsd-verify-work`:** `run ci`, the four Playwright specs, `routes.json` diff clean, `contracts:check` clean
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Req | Behavior | Test Type | Automated Command | Status |
|-----|----------|-----------|-------------------|-------------|
| WSP-01 | Compact bar + details block in every section | component | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-header.test.tsx tests/copy-button.test.tsx` | ✅ green |
| WSP-02 | Five sections from `?tab=`, retired ids fall back to Overview | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-sections.test.ts tests/hosting/workspace-page.test.tsx tests/hosting/deep-link.test.tsx` | ✅ green |
| WSP-03 | No clipping at 320x568 / 390x844, keyboard reaches all sections | e2e + axe | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-workspace.spec.ts` | ✅ green |
| WSP-04 | `deriveJobProgress` exhaustive, never a percentage | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/job-progress.test.ts tests/hosting/job-progress-strip.test.tsx` | ✅ green |
| WSP-05 | Failed job reason, cost sentence, retry/skip gating | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/job-failure.test.tsx` | ✅ green |
| WSP-06 | Breadcrumb Servers > name > section | component | `npm --prefix sources/ReactApp test -- tests/breadcrumbs.test.tsx tests/hosting/workspace-page.test.tsx` | ✅ green |
| CST-01 | Cost strip uses dialog rule; null estimates "not available" | component | `npm --prefix sources/ReactApp test -- tests/hosting/cost-strip.test.tsx` | ✅ green |
| CST-02 | "Shut down and stop billing" vocabulary, EN/DE | unit + component | `npm --prefix sources/ReactApp test -- tests/locales/server-keys.test.ts tests/hosting/workspace-vocabulary.test.ts` | ✅ green |
| ACT-01 | `buildActivityEntries` merge, order, dedup, filters | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/activity-model.test.ts tests/hosting/activity-section.test.tsx` | ✅ green |
| ACT-02 | `deriveFreshness` truth table, live-update invalidation | unit + component | `npm --prefix sources/ReactApp test -- tests/hosting/workspace-freshness.test.ts tests/hosting/workspace-live-updates.test.tsx tests/hosting/fleet-live-updates.test.tsx` | ✅ green |

*Status: ✅ green · audited 2026-11-26: 17 Vitest files (444 tests) and 13 Playwright tests all pass*

---

## Wave 0 Requirements

- [x] `tests/hosting/workspace-sections.test.ts` — WSP-02
- [x] `tests/hosting/job-progress.test.ts`, `tests/hosting/job-progress-strip.test.tsx` — WSP-04
- [x] `tests/hosting/job-failure.test.tsx` — WSP-05
- [x] `tests/breadcrumbs.test.tsx`, `tests/copy-button.test.tsx` — WSP-06, WSP-01
- [x] `tests/hosting/cost-strip.test.tsx` — CST-01
- [x] `tests/hosting/workspace-vocabulary.test.ts` — CST-02
- [x] `tests/hosting/activity-model.test.ts`, `tests/hosting/activity-section.test.tsx` — ACT-01
- [x] `tests/hosting/workspace-freshness.test.ts`, `tests/hosting/workspace-live-updates.test.tsx` — ACT-02
- [x] `tests/e2e/hosting-workspace.spec.ts` — WSP-03
- [x] Shared workspace fixture builders (`tests/helpers/workspaceFixtures.ts`)

---

## Manual-Only Verifications

All phase behaviors have automated verification. No manual-only items.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-11-26

## Validation Audit 2026-11-26

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

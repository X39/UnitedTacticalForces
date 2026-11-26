---
phase: "70"
slug: "ui-polishing"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-26"
---

# Phase 70 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Source: `70-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 (jsdom) + Playwright 1.62.1 with `@axe-core/playwright` |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| **Quick run command** | `npm --prefix sources/ReactApp test -- <touched test files> tests/styles` |
| **Full suite command** | `npm --prefix sources/ReactApp run ci` (then `npm --prefix sources/ReactApp run test:e2e` on chromium, mobile-390, mobile-320) |
| **Estimated runtime** | quick ~10 s; full vitest ~100 s; e2e several minutes |

---

## Sampling Rate

- **After every task commit:** quick run command, plus `run typecheck` and `run lint` for TS changes
- **After every plan wave:** full vitest, `locales:check`, `locales:unused`, `lint`, `typecheck`, `format:check`; CSS-removal waves also lower `tests/styles/legacy-ratchet.json` via `UTF_LEGACY_RATCHET_UPDATE=1`
- **Before `/gsd-verify-work`:** `run ci` green and e2e (audit + hosting specs) green on chromium, mobile-390, mobile-320
- **Max feedback latency:** 120 seconds (vitest); pixel baselines are not part of the phase gate (milestone end)

---

## Per-Task Verification Map

Filled by the planner per task; decision-level map:

| Decision | Behavior | Test Type | Automated Command | File Exists | Status |
|----------|----------|-----------|-------------------|-------------|--------|
| D-01..D-05 | drawn checkbox/radio/switch/chip/card marks over native inputs | unit | `npm --prefix sources/ReactApp test -- tests/form-fields.test.tsx` | ✅ | ✅ green |
| D-01, D-06, D-08 | style guards: no `accent-color`, no gold hover, tokens only | style guard | `npm --prefix sources/ReactApp test -- tests/styles` | ✅ | ✅ green |
| D-01, D-08 | control and button contrast pairs | unit | `npm --prefix sources/ReactApp test -- tests/styles/tokens-contrast.test.ts` | ✅ | ✅ green |
| D-07 | Disclosure heading/id/badge/variant | unit | `npm --prefix sources/ReactApp test -- tests/disclosure.test.tsx` | ✅ | ✅ green |
| D-09 | collapsible Settings panels, summaries, hash/staging/invalid open, persistence | component | `npm --prefix sources/ReactApp test -- tests/settings-panel.test.tsx tests/hosting/settings-section.test.tsx` | ✅ | ✅ green |
| D-09 | existing hosting flows with panels seeded open | component | `npm --prefix sources/ReactApp test -- tests/hosting` | ✅ | ✅ green |
| D-10 | configuration search and filter | unit | `npm --prefix sources/ReactApp test -- tests/hosting/configuration-filter.test.ts tests/hosting/configuration-panel.test.tsx` | ✅ | ✅ green |
| D-11 | no layout shift on staging | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-change.spec.ts` | ✅ | ✅ green |
| D-12 | compact review dialog | component + e2e | `npm --prefix sources/ReactApp test -- tests/hosting/review-changes-dialog.test.tsx` | ✅ | ✅ green |
| D-13 | leave dialog one row, backdrop = Stay | unit + e2e | `npm --prefix sources/ReactApp test -- tests/primitives.test.tsx tests/hosting/settings-leave-guard.test.tsx` | ✅ | ✅ green |
| D-14 | Files panel layout | unit + e2e | `npm --prefix sources/ReactApp test -- tests/hosting/files-panel-controls.test.tsx tests/hosting/files-panel-location.test.tsx` | ✅ | ✅ green |
| D-15 | credentials alignment and focus after rotate | component + e2e | `npm --prefix sources/ReactApp test -- tests/hosting/credentials-panel.test.tsx` | ✅ | ✅ green |
| D-05 | weekday chips 4 per row at ≤520 px | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-automation.spec.ts` | ✅ | ✅ green |
| D-16 | WR-01, WR-02, IN-06, IN-08, refresh target | unit + e2e | `npm --prefix sources/ReactApp test -- tests/shell-layouts.test.tsx tests/locales tests/admin-site-images.test.ts` | ✅ | ✅ green |
| D-18 | axe, keyboard walk, reflow, German walk | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/audit tests/e2e/hosting-a11y.spec.ts` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/settings-panel.test.tsx`, `tests/hosting/settings-section.test.tsx` — D-09
- [x] `tests/hosting/configuration-filter.test.ts` — D-10
- [x] `tests/locales/de-glossary.test.ts` + `de-glossary-allowed.json` — IN-06
- [x] shared helper seeding `utf.settings.open` (vitest + Playwright); `sessionStorage.clear()` in `tests/setup.ts`
- [x] `Checkmark16Filled` in `tests/mocks/react-icons.tsx`
- [x] `action-control.css` added to style-guard and legacy-ratchet guarded lists
- [x] axe fixture check of the new check-mark structure before screens depend on it

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Overall look of controls and panels | Phase goal | Visual design judgment | Deferred follow-up: milestone-end visual review with regenerated baselines |
| Screen-reader output for switches and panels | D-18 | Spoken output | Deferred follow-up: milestone-end screen-reader pass |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-26 (vitest 215 files / 3,541 tests green; e2e audit + hosting specs 776 passed on chromium, mobile-390, mobile-320, retries 0)

## Validation Audit 2026-11-26

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

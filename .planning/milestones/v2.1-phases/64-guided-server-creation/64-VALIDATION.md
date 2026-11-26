---
phase: "64"
slug: "guided-server-creation"
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-20"
---

# Phase 64 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Source: `64-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 (jsdom, Testing Library, user-event) + Playwright 1.62.1 with @axe-core/playwright 4.13.0 |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| **Quick run command** | `npm --prefix sources/ReactApp test -- tests/hosting/create-server.test.ts` |
| **Full suite command** | `npm --prefix sources/ReactApp run ci` (+ Playwright create and a11y specs) |
| **Estimated runtime** | ~120 seconds |

---

## Sampling Rate

- **After every task commit:** touched test files plus `npm --prefix sources/ReactApp run typecheck` and `lint`
- **After every plan wave:** `npm --prefix sources/ReactApp test`, `locales:check`, `tests/styles`, `tests/hosting/server-state-single-owner.test.ts`
- **Before `/gsd-verify-work`:** `npm --prefix sources/ReactApp run ci`, Playwright specs, `git diff --exit-code -- sources/Common/Ui/routes.json`, `contracts:check` clean
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 64-XX-XX | TBD | TBD | CRT-01 | — | N/A | component | `npm --prefix sources/ReactApp test -- tests/hosting/create-server-page.test.tsx tests/hosting/create-server.test.ts tests/form-fields.test.tsx tests/disclosure.test.tsx` | ✅ | ✅ green |
| 64-XX-XX | TBD | TBD | CRT-02 | — | Unknown price never shown as 0 | unit+component | `npm --prefix sources/ReactApp test -- tests/hosting/create-server.test.ts tests/hosting/create-summary.test.tsx` | ✅ | ✅ green |
| 64-XX-XX | TBD | TBD | CRT-03 | — | Frozen owner blocked before any request | unit+component | `npm --prefix sources/ReactApp test -- tests/hosting/create-server.test.ts tests/hosting/create-server-page.test.tsx` | ✅ | ✅ green |
| 64-XX-XX | TBD | TBD | CRT-04 | — | Cost dialog opens after create, reload-safe | integration | `npm --prefix sources/ReactApp test -- tests/hosting/create-start-flow.test.tsx` | ✅ | ✅ green |
| 64-XX-XX | TBD | TBD | CRT-05 | — | N/A | unit+component | `npm --prefix sources/ReactApp test -- tests/hosting/create-presets.test.ts tests/hosting/create-server-page.test.tsx` | ✅ | ✅ green |
| 64-XX-XX | TBD | TBD | CRT-06 | — | N/A | unit scan+component | `npm --prefix sources/ReactApp test -- tests/hosting/create-capability-driven.test.tsx` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/form-fields.test.tsx`, `tests/disclosure.test.tsx` — shared controls (CRT-01)
- [x] `tests/hosting/create-summary.test.tsx` — estimate, reasons, bar structure (CRT-02, CRT-03)
- [x] `tests/hosting/create-presets.test.ts` — preset projection (CRT-05)
- [x] `tests/hosting/create-capability-driven.test.tsx` — capability fixtures + source-scan guard (CRT-06)
- [x] `tests/hosting/create-start-flow.test.tsx` — two-route integration with real `CostConfirmationProvider`, StrictMode (CRT-04)
- [x] `tests/e2e/hosting-create.spec.ts` — 320x568 / 390x844, EN and DE, axe

---

## Manual-Only Verifications

All phase behaviors have automated verification. No real-device or screen-reader item is part of this phase's requirements.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-26 (220 Vitest tests in 8 files and 20 Playwright tests green)

## Validation Audit 2026-11-26

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

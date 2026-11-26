---
phase: "61"
slug: foundations-localization-member-access
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-18"
---

# Phase 61 — Validation Strategy

> Per-phase validation contract. Source: the Validation Architecture section of 61-RESEARCH.md.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 (jsdom), Playwright 1.62.1, xUnit 2.9.3 (`tests/Api.Tests`) |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| **Quick run command** | `cd sources/ReactApp && npx vitest run <files>` |
| **Full suite command** | `cd sources/ReactApp && npm run ci`; `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` |
| **Estimated runtime** | ~20 s quick, several minutes full |

## Sampling Rate

- **After every task commit:** `npx vitest run` on touched files plus `npx tsc -b`
- **After every plan wave:** `npm test` and `npm run lint`
- **Before `/gsd-verify-work`:** `npm run ci`, the API test filter and the axe e2e spec all green

## Per-Task Verification Map

| Requirement | Behavior | Test Type | Automated Command | File | File Exists | Status |
|---|---|---|---|---|---|---|
| FND-01 | token contrast pairs; no raw hex/px in server CSS | unit | `npx vitest run tests/styles` | tests/styles/{tokens-contrast,server-style-guard}.test.ts | yes | COVERED (green) |
| FND-02 | same state text on card, header, notification | component | `npx vitest run tests/hosting/server-state-surfaces.test.tsx tests/profile-menu.test.tsx` | tests/hosting/server-state-surfaces.test.tsx | yes | COVERED (green) |
| FND-03 | status x cost x pending x deployment invariant | unit | `npx vitest run tests/hosting/server-state.test.ts` | tests/hosting/server-state.test.ts | yes | COVERED (green) |
| FND-04 | one confirmation from every entry point | component | `npx vitest run tests/hosting/cost-confirm-dialog.test.tsx tests/hosting/actions.test.tsx tests/hosting/fleet.test.tsx tests/hosting/workspace-page.test.tsx` | tests/hosting/cost-confirm-dialog.test.tsx | yes | COVERED (green) |
| FND-05 | `?action=` deep link behavior | component | `npx vitest run tests/hosting/deep-link.test.tsx` | tests/hosting/deep-link.test.tsx | yes | COVERED (green) |
| FND-07 | parity matrix completeness | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | tests/hosting/parity-matrix.test.ts | yes | COVERED (green) |
| L10N-01 | React-owned locales, no Blazor/generated dependency | unit | `npx vitest run tests/locales/ownership.test.ts` | tests/locales/ownership.test.ts | yes | COVERED (green) |
| L10N-02 | build fails on missing German key | unit/CLI | `npx vitest run tests/locales/check-locales.test.ts`; `npm run locales:check` | tests/locales/check-locales.test.ts | yes | COVERED (green) |
| ACC-01 | list endpoint returns exactly the viewable servers | xUnit | `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerListAccessTests` | tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs | yes | COVERED (green) |
| ACC-01 | nav, fleet and query gating for `sv` and `sv:view` | component | `npx vitest run tests/hosting/member-access.test.tsx` | tests/hosting/member-access.test.tsx | yes | COVERED (green) |
| ACC-03 | disabled actions show their reason | component | `npx vitest run tests/shared-controls.test.tsx tests/hosting/unavailable-reason.test.tsx` | tests/hosting/unavailable-reason.test.tsx | yes | COVERED (green) |
| a11y | zero axe violations on dialog and notice, EN and DE | e2e | `npm run test:e2e -- tests/e2e/hosting-a11y.spec.ts` | tests/e2e/hosting-a11y.spec.ts | yes | COVERED (green) |

Audit run: 25 vitest files, 576 passed and 1 skipped (the opt-in legacy-ratchet update mode); `locales:check` 4037/4037 keys; `GameServerListAccessTests` 9/9; `hosting-a11y.spec.ts` 22/22.

## Wave 0 Requirements

- [x] `tests/styles/{tokens-contrast,server-style-guard}.test.ts` and shared `tests/helpers/`
- [x] `tests/hosting/{server-state,server-state-surfaces,cost-confirm-dialog,deep-link,unavailable-reason,member-access,parity-matrix}.test.ts(x)`
- [x] `tests/locales/{check-locales,ownership}.test.ts` with fixture JSON
- [x] `tests/e2e/hosting-a11y.spec.ts`
- [x] `tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs`
- [x] New icons in `tests/mocks/react-icons.tsx`; export `workspaceTabIds` from `ServerWorkspacePage.tsx`
- [x] Shared test render helper (QueryClient, router, i18n, toast, CostConfirmationProvider)
- [x] Spike: one plural key through typed `t()`
- [x] `npm install --save-dev --save-exact @axe-core/playwright@4.13.0`

## Manual-Only Verifications

All phase behaviors have automated verification.

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Screen-reader announcement of dialog and notice | a11y | Deferred follow-up: needs a real screen reader and device; axe covers the static rules only | Open a start dialog and a deep-link notice with NVDA and VoiceOver and confirm the announced text |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] No 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-26

## Validation Audit 2026-11-26

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

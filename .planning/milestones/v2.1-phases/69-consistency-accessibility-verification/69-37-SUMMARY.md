---
phase: 69-consistency-accessibility-verification
plan: 37
subsystem: webapp-toasts
tags: [accessibility, live-regions, toast, WR-04, FND-06]
requires: ["69-34"]
provides:
  - persistent polite and assertive toast live regions
  - findToast and toastPoliteness test helpers
  - dated O-09 amendment in 69-UI-SPEC.md
affects: [AppToasts, O-09 audit, toast tests]
tech-stack:
  added: []
  patterns: ["persistent aria-live regions without a role; toast carries no role"]
key-files:
  created:
    - sources/ReactApp/tests/helpers/toastQueries.ts
  modified:
    - sources/ReactApp/src/components/AppToasts.tsx
    - sources/ReactApp/src/components/shared-primitives.css
    - sources/ReactApp/tests/app-toasts.test.tsx
    - sources/ReactApp/tests/e2e/support/screens/fleetScreens.ts
    - sources/ReactApp/tests/admin-kassenbuch.test.tsx
    - sources/ReactApp/tests/admin-membership-requests.test.tsx
    - sources/ReactApp/tests/admin-public-page-editor-page.test.tsx
    - sources/ReactApp/tests/admin-verein-documents.test.tsx
    - sources/ReactApp/tests/admin-verein-settings.test.tsx
    - sources/ReactApp/tests/hosting/configuration-profiles.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx
    - sources/ReactApp/tests/hosting/create-summary.test.tsx
    - .planning/phases/69-consistency-accessibility-verification/69-UI-SPEC.md
decisions:
  - "Regions use aria-live (polite, assertive) with aria-atomic false and aria-relevant additions text and no role, as designed in the plan"
status: complete
gap_ids: [WR-04]
requirements: [FND-06]
actuals:
  tokens: 9000
  tasks: 3
  commits: 5
plan_head_before: b5a7800819842c3cc0488e9b06df7435121a0236
plan_head_after: 47a78cb285fe7301206a252c62466bb90a8db45e
---

# Phase 69 Plan 37: Persistent toast live regions Summary

The toast viewport now always renders one polite and one assertive live region (aria-live, aria-atomic false, aria-relevant additions text, no role), and each toast is inserted into the region that already exists; success, info and warning toasts go to the polite region and error toasts to the assertive one (WR-04).

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 | Tracer: persistent polite and assertive regions | 1952665d |
| 2 | Admin toast assertions follow the region contract | 5a94dc77 |
| 3 | Hosting toast assertions, summary live-region check, UI-SPEC amendment | d147f85d, 58a0a80e (lint fix), 47a78cb2 (UI-SPEC) |

## What changed

- `AppToasts.tsx`: two `div.app-toast-region` children rendered unconditionally; per-toast `role` removed; toast markup extracted into a local `renderToast`; timing, dismiss control, pause handlers and the `useAppToast` API untouched. JSDoc on `AppToastProvider` explains persistence and the aria-live choice.
- `shared-primitives.css`: `.app-toast-viewport > .app-toast-region` as grid with `--utf-space-sm` gap; `:empty` regions get `position: absolute` so they add no gap. `styles.css` untouched.
- `tests/helpers/toastQueries.ts`: `findToast` and `toastPoliteness`.
- Tests: toast unit tests rewritten (initial regions, identical region nodes before and after, no status or alert role inside the viewport, region stylesheet assertions); eight admin and hosting tests now find the toast by text and assert politeness; the CreateSummary "no live region" check is scoped to `aside.create-summary`.
- O-09 audit locates the toast in `.app-toast-region[aria-live="polite"] .app-toast`.
- `69-UI-SPEC.md`: O-09 cell amended and a dated paragraph ("Amended 2026-11-25 (69-37, WR-04)") added below the Overlays table.

## Verification

- Vitest: toast, styles, admin and hosting files pass; full suite 3267 passed, 1 failed. The single failure (`admin-content-pages.test.tsx`, GDPR editor, 5 s timeout) passes when its file runs alone (18/18): known load flake, not a regression. No failure names a toast role.
- `tsc -b` typecheck and `eslint . --max-warnings 0` pass.
- Playwright (chromium, CI=1): `O-09 (en)`, `O-09 (de)` and the toast dismiss target-size audit pass (3/3). Ports 4173 and 4174 are free afterwards.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree had no node_modules**
- **Found during:** Task 1
- **Fix:** Created an untracked directory junction `sources/ReactApp/node_modules` pointing at the main checkout's installed packages (git ignores it; nothing committed).

**2. [Rule 1 - Bug] Unused import in rewritten toast test**
- **Found during:** Task 3 lint run
- **Issue:** `userEvent` became unused in `app-toasts.test.tsx` after the rewrite.
- **Fix:** Removed the import.
- **Commit:** 58a0a80e

**3. [Housekeeping] Generated file touched by the e2e build**
- `build:assets` rewrote line endings of `src/generated/api.ts`; reverted with `git checkout -- <file>` before committing, nothing committed for it.

Otherwise: plan executed as written.

## Known Stubs

None.

## Threat Flags

None. T-69-47 mitigated as planned (only error toasts use the assertive region; additions-only relevance).

## Deferred follow-up

Screen-reader pass (NVDA or VoiceOver) at milestone end to confirm the spoken result (backstop, already on the 69-VERIFICATION human list).

## Self-Check: PASSED

Created and modified files exist; commits 1952665d, 5a94dc77, d147f85d, 58a0a80e, 47a78cb2 are on the branch; all acceptance greps meet their expectations.

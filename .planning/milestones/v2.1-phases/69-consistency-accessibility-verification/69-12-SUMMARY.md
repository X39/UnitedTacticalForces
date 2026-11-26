---
phase: 69-consistency-accessibility-verification
plan: 12
subsystem: ui
tags: [react, confirm-dialog, state-views, form-fields, status-tones, focus-ring, ratchet, hosting-finance]

requires:
  - phase: 69-03
    provides: "StateViews, financeTones and the HostingFinance_*_Dismiss keys"
  - phase: 69-04
    provides: "family.hosting-finance.json ratchet and its writer"
  - phase: 69-06
    provides: "discovery findings for the finance and cost alert screens"
provides:
  - "FinanceDialog on the shared ConfirmDialog shell with an outcome-specific dismiss label"
  - "Finance owners, account and settings pages and the cost alerts page on PageHeader, PageStack and shared state views"
  - "Finance and cost alert files free of raw controls; hosting-finance ratchet family at zero"
  - "Finance table rows with a focus ring token instead of removed outline"
affects: [69-33]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: b2fb1163caf4446cd82c2fe52fb2b1582ccf7f1c
commits: 3

actuals:
  tokens: 30000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Form dialog reads its FormData from the shell's own form through a ref, so native validation and Enter keep working"
    - "Page early returns render the same PageHeader above a shared state view, so the h1 element survives the loading to loaded switch"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/FinanceDialog.tsx
    - sources/ReactApp/src/features/hosting/FinanceProtocol.tsx
    - sources/ReactApp/src/features/hosting/FinanceAccountPicker.tsx
    - sources/ReactApp/src/features/hosting/HostingFinancePage.tsx
    - sources/ReactApp/src/features/hosting/HostingFinanceAccountPage.tsx
    - sources/ReactApp/src/features/hosting/HostingFinanceSettingsPage.tsx
    - sources/ReactApp/src/features/hosting/CostAlertsPage.tsx
    - sources/ReactApp/src/features/hosting/finance.css
    - sources/ReactApp/tests/hosting/finance-pages.test.tsx
    - sources/ReactApp/tests/hosting/cost-alerts-page.test.tsx
    - sources/ReactApp/tests/ratchets/family.hosting-finance.json

key-decisions:
  - "FinanceDialog.onSubmit now receives FormData read from the shell form; callers no longer handle a form event."
  - "Dialog descriptions reuse existing text: the freeze and unfreeze sentences for those two actions, the account display name for the other account actions, and the owners overview description for Add account. No locale key was added."
  - "The ledger detail drawer keeps its hand-rolled aside dialog (it is a detail view, not a confirmation); only its raw scrim button became an aria-hidden div, and the aside now closes on Escape."
  - "Not-found on the cost alerts page uses EmptyState with the existing App_NotFound_Title and Server_Alerts_Page_NotFound."

patterns-established:
  - "Finance table row focus: :focus-visible with var(--utf-focus-ring) and a negative offset so the scroll container does not clip it"

requirements-completed: [CON-01, CON-02, CON-03, FND-06]

coverage:
  - id: D1
    description: "Finance corrections open on the shared dialog shell with kicker, title, fields and an outcome-specific dismiss label; Escape (cancel event) closes it"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx#opens a correction on the shared dialog shell with a named dismiss label"
        status: pass
    human_judgment: false
  - id: D2
    description: "Payment and correction payloads equal the pre-conversion payloads after the field conversion (T-69-17)"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx#submits the payment payload unchanged and #submits the correction payload unchanged"
        status: pass
    human_judgment: false
  - id: D3
    description: "Owners, account, settings and cost alerts pages render exactly one h1 above loading, forbidden, error, empty and not-found states"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx and tests/hosting/cost-alerts-page.test.tsx (one h1 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Owners list filters keep their URL driven query parameters through SearchField, SelectField and FilterChipGroup; the cost alerts load error uses the generic title"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx#filters the owners list ...; tests/hosting/cost-alerts-page.test.tsx#shows the generic load error title ..."
        status: pass
    human_judgment: false
  - id: D5
    description: "Finance row focus ring restored; no outline removal remains in finance.css"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx#keeps the focus ring on a focused finance table row (CSS source contract, not a rendered check)"
        status: pass
    human_judgment: true
    rationale: "The ring is asserted against the stylesheet source only; whether it is visible against the row background in a browser was not rendered or screenshotted."

duration: ~70min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 12: Hosting Finance and Cost Alert Convergence Summary

**The finance dialog now sits on the shared ConfirmDialog shell with outcome-specific dismiss labels, all eight finance and cost alert screens use the shared header, states, form fields and tone lexicon, and the finance ratchet family is at zero.**

## Performance

- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 11 modified, none created
- **Commits:** 3 task commits plus this summary

## Accomplishments

- `FinanceDialog` renders `ConfirmDialog` (kicker, title, description, error slot, busy state) and reads `FormData` from the shell form, so the account page mutation and its payload code are unchanged.
- `HostingFinanceAccountPage`: shared header above `LoadingState`, `ErrorState` (real refetch) and `ForbiddenState`; `TextField`, `TextAreaField` and `SelectField` fields; `toneOf(financeTones)` badges; per-action dismiss keys (`HostingFinance_Payment_Dismiss` and the other six); the cost alerts section uses `LoadingState` and `ErrorState`.
- `HostingFinancePage`: `SearchField`, `SelectField` (kind) and `FilterChipGroup` (status) keep the `search`, `kind`, `status` and `page` URL parameters; empty state uses the two new title and body keys; the add-account dialog uses `HostingFinance_AddAccount_Dismiss`.
- `FinanceProtocol`, `FinanceAccountPicker`, `HostingFinanceSettingsPage`: shared fields and states; the raw scrim button became an aria-hidden div.
- `CostAlertsPage`: one `PageHeader` always rendered (description and owner line once loaded), `PageStack` body, `LoadingState`, `ErrorState` with the generic title, `ForbiddenState`, `EmptyState` for not found.
- `finance.css`: removed the `outline: none` on focused rows and added a `:focus-visible` ring; added `.finance-back`; removed the now unused `.finance-page` and `.finance-picker` rules.
- `family.hosting-finance.json` lowered to an empty `files` map with the ratchet writer.

## Task Commits

1. **Task 1 (tracer): finance dialog on the shared shell, opened from the account page** - `7fa00413`
2. **Task 2: owners list, settings, protocol and account picker** - `4afc7db4`
3. **Task 3: cost alerts page on the shared header and states** - `b2fb1163`

Commits were created with signing available; no unsigned fallback was needed. The tracer's automated-only verify was re-run end to end before expansion and passed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] HostingFinancePage edited in Task 1**
- **Found during:** Task 1
- **Issue:** Changing the `FinanceDialog` contract (required `description` and `dismissLabel`, `onSubmit(FormData)`) breaks its second caller, the add-account dialog in `HostingFinancePage.tsx`.
- **Fix:** Minimal prop update of that dialog in Task 1 (the file is in the plan's `files_modified`); the full page conversion followed in Task 2.
- **Commit:** `7fa00413`

**2. [Rule 1 - Bug] Existing and new tests that raced the header**
- **Found during:** Tasks 2 and 3
- **Issue:** The h1 is now present during loading, so `findByRole("heading")` resolved at the loading state and then the element was replaced (finance settings) or the panels were not yet rendered (cost alerts).
- **Fix:** Those tests now await the loaded content (protected reserve field, "Alert amounts" heading) before asserting the header.
- **Files modified:** `tests/hosting/finance-pages.test.tsx`, `tests/hosting/cost-alerts-page.test.tsx`
- **Commits:** `4afc7db4`, `b2fb1163`

### Cross-plan findings (no change made outside this plan)

- No locale key exists for a dialog description of payment, refund, allowance, correction, rename or reversal. The dialogs describe the affected account by its display name instead; a dedicated sentence per action would need keys from a closing plan.
- `HostingFinance_LoadError`, `HostingFinance_CloseDetails`, `HostingFinance_NoOwners`, `HostingFinance_NoLedgerEntries` and (for these screens) `Server_Settings_Retry` have no reader in this plan's files any more; 69-33's unused-key check should retire those that nothing else reads.
- `CostAlertSettingsPanel.tsx` needed no change: it has no raw control, no hand-rolled heading and its dialog already names its dismiss.

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug). **Impact:** none on the planned outcome.

## Issues Encountered

- `node_modules` was absent in the worktree; a directory junction to the main checkout's install was created (git-ignored, nothing installed, lockfile untouched).
- `npm run test:e2e` rewrote `src/generated/api.ts` with line-ending changes; it was restored with `git checkout --` and not committed.
- The machine was loaded by sibling executors: one full-suite run hit the 5 s ceiling in `ratchet.test.ts`; it passes alone and under `--testTimeout=30000`.

## Verification

- `npx vitest run tests/hosting tests/ratchets tests/styles --testTimeout=30000`: 115 files, 2220 passed, 1 skipped (opt-in writer).
- `npm run lint` and `npm run typecheck`: exit 0.
- `npm run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts --project=chromium`: 14 passed, 24 skipped by the spec's own project filter.
- Acceptance criteria: `Common_Cancel` in FinanceDialog 0, `ConfirmDialog` in FinanceDialog 4, raw controls 0 in all four converted pages and both helpers, `outline: none|0` in finance.css 0, `StateViews` in CostAlertsPage 1, ratchet file keys 0.

## Not verified

- Focus return to the invoker after Escape is not asserted in jsdom (its dialog polyfill does not restore focus); it relies on the native dialog behavior that `ConfirmDialog` already carries.
- The 69-06 discovery finding about 1px input focus outlines (`styles.css .field input:focus`) should be gone because the raw inputs became `TextField`, but no discovery or audit run was done against these screens.
- No visual check of the new filter row layout (SearchField, SelectField and FilterChipGroup share the existing `finance-filters` grid) or of the restored row focus ring in a browser.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or file access; T-69-17 is covered by the two payload tests.

## Self-Check: PASSED

- Commits `7fa00413`, `4afc7db4` and `b2fb1163` are ancestors of HEAD.
- All eleven modified files exist on disk.

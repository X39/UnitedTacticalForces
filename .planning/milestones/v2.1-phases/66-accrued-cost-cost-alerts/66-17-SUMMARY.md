---
phase: 66-accrued-cost-cost-alerts
plan: 17
subsystem: webapp
tags: [cost-alerts, toast, banner, profile-menu, react, ui-spec, d-16, d-17, d-08, d-07]
status: complete
gap_closure: true

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-14 dismiss audit (markCostAlertsDismissed, dismissAll with shown ids), 66-09 banner and menu rows, 66-15 gap closure amendments section
provides:
  - "useDismissCostAlerts: dismiss(alert) and dismissAll(serverId) show a localized success toast after the API accepted, naming server and amount or the alert count"
  - "CostAlertBanner as one quiet panel: threshold rows without the server name, divider rows, Dismiss16Regular on the buttons, one footer holding the cost-stop button and Dismiss all, no StatePanel action column"
  - "CostAlertMenuItem with an Alert16Regular icon column and one text column (server name as the only bold line, reached line, amount line, actions)"
  - "Four new EN and DE strings and the UI-SPEC amendment 'Banner, menu rows and dismissal feedback (D-16, D-17)'"
  - "e2e proof of the dismissal toast after the banner is gone, plus env-gated banner and menu screen captures"
affects: [66-18, phase-66-verification, uat, phase-69-locale-cleanup]

plan_head_before: d7386f874e2a3b3aaf761000b381d6187af9def9
plan_head_after: d685481ba6964e73351ae30df729c6a084d37b3f

actuals:
  tokens: 8100
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "A dismissal's confirmation is a toast from the app-wide AppToastProvider, which outlives the banner that unmounts with the last alert"
    - "Banner buttons override the shared nowrap so a long German label wraps instead of widening the grid column of StatePanel"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/useCostAlerts.ts
    - sources/ReactApp/src/components/server/CostAlertBanner.tsx
    - sources/ReactApp/src/components/server/CostAlertMenuItem.tsx
    - sources/ReactApp/src/components/server/cost-alerts.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-menu.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-persistence.test.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - .planning/phases/66-accrued-cost-cost-alerts/66-UI-SPEC.md

key-decisions:
  - "The success toast replaces the banner's screen-reader-only announcement: it is visible, announced once, and survives the banner unmounting (D-17)"
  - "Server_Alerts_DismissedAnnounce and Server_Alerts_Row_Reached stay in the locale files and the Phase 66 key list until the Phase 69 cleanup (precedent 61-12)"
  - "Rows after the first carry an explicit cost-alert-banner__row--divided class instead of a sibling selector, so the contract is assertable in jsdom"
  - "Dismiss all in the footer uses the default button size to match the cost-stop button beside it; row Dismiss alert stays compact"

requirements-completed: [CST-04]

duration: 15min
completed: 2026-11-21
---

# Phase 66 Plan 17: Banner, Menu Rows and Dismissal Feedback Summary

**Every accepted cost alert dismissal now shows a localized toast naming the server and amount (or the alert count), and the banner and menu rows are redesigned into one quiet panel with a single footer and icon-column rows that match the server rows.**

## Performance

- **Duration:** about 15 min
- **Tasks:** 3 of 3 (tracer, then two auto tasks)
- **Commits:** 5 code and docs commits, then the SUMMARY commit

## Accomplishments

- Tracer (D-17): `useDismissCostAlerts` takes the `CostAlert`, and after the API accepted (after `markCostAlertsDismissed` and the cache filter) calls `showToast(..., "success")`: `Server_Alerts_Dismissed_Toast` for one alert, plural `Server_Alerts_DismissedAll_Toast` with the dismissed count for Dismiss all. A failure shows no toast, keeps the alert and the inline `role="alert"` line. The banner's sr-only announcement state and element are gone. Unit tests prove the toast stays visible after the last alert removes the banner, focus still goes to the workspace heading, the next row's button gets focus when one of two is dismissed, Dismiss all with three alerts, the German singular and plural, the menu path, the failure path, and that a toast never removes another alert (persistence suite).
- Banner (D-16): no `action` on `StatePanel`; rows read "{{threshold}} reached" over the amount line without the server name, with `Dismiss16Regular` on the row button; rows after the first are divided; the footer (`cost-alert-banner__footer`) holds the cost-stop button first, then Dismiss all, and does not render when empty. Cost-stop still only calls `useCostConfirmation().request` with source `alert-banner`.
- Menu rows (D-16): `Alert16Regular` icon column in `--utf-state-warning-fg`, text column with the server name as the only bold line, the reached line in normal weight, the muted amount line and the actions row; the failure line sits in the text column.
- UI-SPEC amendment recorded with the banner and menu anatomy, the toast contract, the four strings in EN and DE, the retired strings and the overridden earlier rows.
- e2e: the journey asserts the threshold sentence without the server name and the dismissal toast after the banner is gone; axe and no-horizontal-scroll suites unchanged and green at 320 and 390 px in EN and DE. The env-gated capture gained `banner-{1|3}-{en|de}-{1280|390}.png` and `menu-{en|de}-{1280|390}.png`.

## Task Commits

1. Task 1 (tracer): `60eaa1d2` feat(webapp): confirm every cost alert dismissal with a toast
2. Task 2: `81617c87` feat(webapp): redesign the cost alert banner
3. Task 3 code: `3a4b9aa1` feat(webapp): align cost alert menu rows with server rows
4. Task 3 spec: `0bb5014a` test(webapp): cover dismissal feedback and capture alert screens
5. Task 3 amendment: `d685481b` docs(planning): amend the cost alert banner and menu contract

## Screenshot review (Task 3)

Captured with `UTF_CAPTURE_SCREENS=1` and each PNG opened with the Read tool. Reviewed: `banner-3-en-1280`, `banner-3-de-1280`, `banner-3-en-390`, `banner-3-de-390`, `banner-1-en-1280`, `banner-1-de-1280`, `banner-1-en-390`, `menu-en-1280`, `menu-en-390`, `menu-de-1280`, `menu-de-390`. `banner-1-de-390` was written but not opened separately (its layout is the one-row case of `banner-3-de-390`, which was reviewed).

Findings: row text and the Dismiss button share one row with the button at the row end; dividers appear only between rows (none orphaned above the first or below the last); footer buttons align at 1280 and stack full width at 390; the menu icon sits level with the server name and the actions sit under the text column; German text wraps without overflow in the menu.

CSS change made after the review (tokens only, one rule): at 390 px in German the cost-stop label "Herunterfahren und Abrechnung beenden" did not wrap (shared `.button` is `white-space: nowrap`) and pushed the button past the panel's right padding. Added `.cost-alert-banner .button { white-space: normal; }` in `cost-alerts.css`; the re-capture shows the label wrapping inside the button and the full e2e spec (axe, no horizontal scroll) stays green.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Icon test mock lacked the new glyphs**
- **Found during:** Task 2 (first banner test run: "Element type is invalid ... Check the render method of AlertRow")
- **Issue:** `vitest.config.ts` aliases `@fluentui/react-icons` to `tests/mocks/react-icons.tsx`, which exported neither `Dismiss16Regular` nor `Alert16Regular`, so any component importing them rendered `undefined`.
- **Fix:** Added both exports to the mock (the real package has both, verified in `sizedIcons`). The file is not in the plan's `files_modified`.
- **Files modified:** `sources/ReactApp/tests/mocks/react-icons.tsx`
- **Commit:** `81617c87`

**2. [Rule 1 - Bug] Banner buttons could not wrap a long German label**
- **Found during:** Task 3 screenshot review (see above)
- **Fix:** `white-space: normal` for `.cost-alert-banner .button`
- **Files modified:** `sources/ReactApp/src/components/server/cost-alerts.css`
- **Commit:** `3a4b9aa1`

### Plan-faithful adjustments (not defects)

- The e2e `copy.reached` expectation became a threshold regex per culture (`/1\.00.* reached/`, `/1,00.* erreicht/`), as the plan directs for the threshold sentence.
- Dismiss all moved into the footer at the default button size (it was compact beside the list); the row buttons stay compact.
- `profile-menu.test.tsx` needed no change: its assertions do not depend on the alert row markup.
- The mock icon ignores `className`, so the menu layout test asserts the icon by position, tag and `aria-hidden` rather than by its class.

## Authentication Gates

None.

## Known Stubs

None. No placeholder values flow to the UI.

## Threat Flags

None. The only new surface is the toast text (server name and amount the viewer just dismissed, rendered as React text); T-66-48 is accepted and T-66-49 is mitigated: the relocated cost-stop buttons still only call `request`, and the banner and menu tests assert the action API is never called.

## Verification

- `npm test -- tests/hosting tests/profile-menu.test.tsx tests/locales tests/styles`: 101 files, 1808 tests pass
- `locales:check` (3647 EN and 3647 DE keys, 0 failures), `typecheck`, `lint --max-warnings 0`: exit 0
- Prettier `--check` on the listed files: clean
- `npm run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts`: 13 passed, 24 capture tests skipped without the env var (37 passed with `UTF_CAPTURE_SCREENS=1`)
- `git status --porcelain -- sources/WebApp sources/Api`: nothing changed

## Self-Check: PASSED

PASSED: all modified files exist and all five commits (`60eaa1d2`, `81617c87`, `3a4b9aa1`, `0bb5014a`, `d685481b`) are ancestors of HEAD.

---
phase: 66-accrued-cost-cost-alerts
plan: 10
subsystem: ui
tags: [react, tanstack-query, forms, i18n, cost-alerts, accessibility, leave-guard]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-08 costAlertSettings, personalCostAlertSettings, updateCostAlertSettings with CostAlertSettingsConflictError, hostingQueryKeys.costAlertSettings, CostAlertSettings type and every Server_Alerts_ string
provides:
  - "Certified member route /command/servers/cost-alerts (manifest, React route, App route, navigation context title at priority 115)"
  - "CostAlertsPage: personal or ?owner= settings, loading, load-error with Try again, forbidden, not-found (malformed owner without a request), no-owner panels"
  - "CostAlertSettingsPanel: amounts, basic channels, save row, conflict handling, read-only mode, pristine-follow, leave guard; reusable by the finance account page through settings, queryKey and headingLevel props"
  - "alertSettingsModel: parseAlertAmount, validateAlertDraft, normalizeAlertAmounts, isAlertDraftDirty, formatAlertAmount"
  - "TextField additive trailing prop and two layout tokens (--utf-alert-amount-width, --utf-alerts-status-min-height)"
affects: [66-11, 66-12, finance-account-page, uat]

plan_head_before: 73d0e16d7af48da323de3ddd6ac2e6f563872690
plan_head_after: 89eba6f95d6770270e505699ef2eb15b1e2af6d2

actuals:
  tokens: 17900
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Draft in React state with a baseline: the draft follows a changed settings prop only while pristine (state adjusted during render, no effect)"
    - "Status line is one always-mounted role=status paragraph plus a role=alert paragraph that mounts only for invalid, save-error and conflict states; the container reserves --utf-alerts-status-min-height"
    - "Stable row ids from a module-level sequence; focus requests travel in a ref consumed by an effect on the rows"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/CostAlertsPage.tsx
    - sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx
    - sources/ReactApp/src/features/hosting/alertSettingsModel.ts
    - sources/ReactApp/src/features/hosting/cost-alert-settings.css
    - sources/ReactApp/tests/hosting/cost-alerts-page.test.tsx
    - sources/ReactApp/tests/hosting/alert-settings-model.test.ts
    - sources/ReactApp/tests/hosting/cost-alert-settings.test.tsx
  modified:
    - sources/Common/Ui/routes.json
    - sources/ReactApp/src/features/hosting/routes.tsx
    - sources/ReactApp/src/components/commandNavigation.tsx
    - sources/ReactApp/src/components/FormFields.tsx
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/src/app/App.tsx
    - sources/ReactApp/src/features/hosting/index.ts
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/tests/routes.test.ts
    - sources/ReactApp/tests/hosting/routes.test.ts
    - sources/ReactApp/tests/form-fields.test.tsx
    - sources/ReactApp/tests/hosting/change-source-scan.test.ts

key-decisions:
  - "The page shows the load-error panel only when there is no data at all: a failed background refetch (for example a failed Reload settings) keeps the form and the draft mounted"
  - "A row that holds text which is not an amount is dirty; a blank row is not. Duplicates compare on the parsed value and mark the later row"
  - "-1 is reported as Min (above 0), not Number, because the text is a number and the kinder message names the rule it breaks"
  - "Read-only uses native disabled controls with one notice; saving uses aria-disabled plus readOnly inputs so focus stays and the draft cannot change mid-save"
  - "Discard changes is aria-disabled while there is nothing to discard (pristine and no extra blank row)"
  - "The counter renders only while there is at least one row (plan behavior); the empty-state panel carries the zero-row message"

requirements-completed: [CST-04]

coverage:
  - id: D1
    description: "A member with server access opens /command/servers/cost-alerts, loads the personal or ?owner= settings and saves amounts with revision, both flags and the active UI language"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alerts-page.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/routes.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "Malformed owner shows not found without a request; 404 not found, 403 forbidden, 204 no-owner and load error with Try again are distinct panels; no control renders while loading"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alerts-page.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Locale decimals, precision, range, duplicate and limit rules mirror the API; validation timing after blur or save attempt; add and remove focus rules; limit state; empty state states that no alert fires"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/alert-settings-model.test.ts"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-alert-settings.test.tsx#Cost alert amounts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Status line states, invalid path without a request, aria-disabled pending state, save failure, conflict with Reload (draft kept until chosen, kept when the reload fails), read-only viewer, pristine-follow, leave dialog, Enter never saves"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-settings.test.tsx#Cost alert settings form"
        status: pass
    human_judgment: false
  - id: D5
    description: "TextField trailing renders in one row with the control and leaves existing callers unchanged; both layout tokens exist; the new files hold no persistence, console output or printed error text"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/form-fields.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/hosting/change-source-scan.test.ts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual alignment of the trailing Remove button, 44px targets at 800px, stacked full-width Save and Discard at 520px, the German 320px wrapping of the status text and no horizontal scroll are asserted by CSS review and unit text only, not in a browser"
    requirement: "CST-04"
    verification: []
    human_judgment: true

duration: about 1h 15m
completed: 2026-11-21
---

# Phase 66 Plan 10: Cost Alerts Settings Page Summary

**A cost owner now sets, edits and removes up to ten absolute alert amounts from a certified member page at `/command/servers/cost-alerts`, with locale decimal input, kind validation, one stable status line, recoverable conflicts, a read-only mode and a leave guard, all held in React state only.**

## Performance

- **Duration:** about 1 h 15 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 7 created, 12 modified

## Accomplishments

- Tracer: manifest entry with classic fallback `/command/servers`, React and App route placed before `:serverId`, navigation context title, page with every load state, and a save that sends revision, amounts, both flags and `"en"` or `"de"`.
- Amounts: pure `alertSettingsModel` (parse, validate, normalize, dirty, format), `TextField` `trailing` slot, two tokens, rows with stable ids, focus after add and remove, limit state described by the counter, announcements.
- Save row: always-mounted status line with reserved height, plural invalid status and focus on the first invalid field, aria-disabled pending state, failure and 409 paths with Reload, read-only mode, pristine-follow and the leave dialog.
- 24 new component tests, 11 model tests (plus parameterized cases), 10 page tests; the wider hosting, routes, form-fields, styles, app and locales suites pass (1731 tests).

## Task Commits

1. **Task 1: Tracer, cost alerts page** - `8222eb88` (feat, webapp)
2. **Task 2: Amount editing and validation** - `db4d8526` (feat, webapp)
3. **Task 3: Save row states, conflict, leave guard** - `89eba6f9` (feat, webapp)

## Verification

- `vitest run tests/hosting tests/routes.test.ts tests/form-fields.test.tsx tests/styles tests/leave-guard.test.tsx tests/app tests/locales`: 104 files, 1731 tests passed.
- `typecheck`, `lint --max-warnings 0` and `locales:check` clean. `prettier --check` is clean on every file this plan changed (the 32-file pre-existing drift is untouched).
- `git status --porcelain -- sources/WebApp` is empty; `countRoutePatterns()` is 81.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The route was not reachable through the app router**
- **Found during:** Task 1 (`tests/routes.test.ts` "implements every certified manifest pattern")
- **Issue:** `implementedReactRoutePatterns` comes from `src/app/App.tsx`, which the plan did not list; the certified pattern was unimplemented there. `src/features/hosting/index.ts` had to export the page for the lazy import.
- **Fix:** Added the lazy route before `servers/:serverId` and the barrel export.
- **Files modified:** `src/app/App.tsx`, `src/features/hosting/index.ts`
- **Commit:** `8222eb88`

**2. [Rule 3 - Blocking] Existing route list assertion**
- **Issue:** `tests/hosting/routes.test.ts` lists every hosting manifest pattern and is part of the plan's own verify command.
- **Fix:** Inserted the new pattern in order.
- **Commit:** `8222eb88`

**3. [Rule 3 - Blocking] The trailing row styles live in `components/server/form-fields.css`**
- **Issue:** The form field styles are in that shared stylesheet (covered by the server style guard), which the plan did not list.
- **Fix:** Added `.form-field__row` and `.form-field__trailing` there, tokens only.
- **Commit:** `db4d8526`

**4. [Rule 1 - Bug] A failed refetch would have replaced the form with the load error**
- **Found during:** Task 3 design of Reload settings
- **Issue:** TanStack Query keeps `status: "error"` after a failed refetch even with data, so the page would have shown the load-error panel and unmounted the panel, losing the draft and the conflict.
- **Fix:** The page shows the load error only when `data === undefined`; `Reload` ignores a failed refetch and keeps draft and conflict. Both paths are tested.
- **Commit:** `89eba6f9`

**5. [Rule 3 - Process] Tests spy on `hostingService` instead of msw**
- msw is not a dependency of this repo; the existing hosting tests spy on the service, and the plan's wording "with msw" was followed in spirit with the same request and response shapes. The service calls themselves are covered by `cost-alert-services.test.ts` from plan 66-08.

**6. [Rule 3 - Process] Navigation context assertion lives in the page test**
- `tests/command-overview.test.ts` is not a plan file, so the context title and active Servers item are asserted in `tests/hosting/cost-alerts-page.test.tsx` through the exported helpers.

**Total deviations:** 6 (one bug, five blocking or process). No scope change.

## Plan Notes

- `CostAlertSettingsPanel` also takes an optional `headingLevel` (2 or 3) so plan 66-11 can place it under the finance account page's own h2 section without a second panel rewrite.
- The channels section is deliberately basic (two checkboxes without hints, no in-app row, no language line); plan 66-11 completes it.
- The page keys the panel by cost owner id so changing `?owner=` resets the draft.

## Known Stubs

None. The channels panel is intentionally minimal until plan 66-11 and is recorded above, not a stub of this plan's goal.

## Threat Flags

None. T-66-33 (draft persistence) is covered by the extended source scan, T-66-35 (owner parameter) by GUID validation before any request; T-66-34 and T-66-SC were accepted in the plan.

## Self-Check: PASSED

- All created files exist; commits `8222eb88`, `db4d8526`, `89eba6f9` are ancestors of HEAD (`git rev-list --count` from the ledger base is 3).

---
phase: 66-accrued-cost-cost-alerts
plan: 09
subsystem: ui
tags: [react, tanstack-query, websocket, cost-alerts, accessibility, profile-menu]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-05 CostAlertRaisedMessage and CostAlertDismissedMessage frames plus the alert REST endpoints
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-08 CostAlert type, hostingQueryKeys.costAlerts, dismissal service calls, useAccruedLive, formatAccruedAmount and every Server_Alerts_ string
provides:
  - "costFrames parsers for raised and dismissed alert frames; the provider writes them into the one alerts cache by id and exposes subscribeCostAlerts"
  - "useCostAlerts, useDismissCostAlerts and useCostAlertArrivalToasts"
  - "CostAlertBanner in the workspace notices slot (first notice) and CostAlertMenuItem rows first in the profile menu Server attention list"
  - "Avatar badge over distinct servers with a hidden description; empty copy only when notifications and alerts are both empty"
  - "Shared cost-stop confirmation sources alert-banner and alert-notification; accrued session fact (This session so far) in the confirmation dialog for cost-stopping actions"
affects: [66-10, 66-11, 66-12, cost-alert-ui, uat]

plan_head_before: a02c8710ac2149c704e09e81c6c795a0d8952245
plan_head_after: eb03383e53d9d3460938dd303242f2df4678af91

actuals:
  tokens: 22500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Pushed frames are validated and written into the same TanStack Query entry the REST list lives in, deduplicated by id; a frame that arrives before the first load only invalidates"
    - "Focus after a removal is a state request processed by an effect once the removed ids have left the rendered list, so it never depends on render ordering"
    - "Server names inside translated sentences are placed in bdi by splitting around a private-use sentinel"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/costAlertFormat.ts
    - sources/ReactApp/src/features/hosting/useCostAlerts.ts
    - sources/ReactApp/src/components/server/CostAlertBanner.tsx
    - sources/ReactApp/src/components/server/CostAlertMenuItem.tsx
    - sources/ReactApp/src/components/server/cost-alerts.css
    - sources/ReactApp/tests/hosting/cost-frames.test.ts
    - sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-menu.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/costFrames.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/components/ProfileMenu.tsx
    - sources/ReactApp/src/components/server/useCostConfirmation.ts
    - sources/ReactApp/src/components/server/CostConfirmDialog.tsx
    - sources/ReactApp/src/components/server/WorkspaceHeader.tsx
    - sources/ReactApp/src/components/server/AccruedCost.tsx
    - sources/ReactApp/tests/hosting/cost-alerts-source-scan.test.ts
    - sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx
    - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
    - sources/ReactApp/tests/hosting/accrued-cost.test.tsx
    - sources/ReactApp/tests/hosting/fleet-page.test.tsx
    - sources/ReactApp/tests/hosting/live-badge.test.tsx
    - sources/ReactApp/tests/profile-menu.test.tsx

key-decisions:
  - "The alerts query is enabled for every signed-in user (staleTime Infinity, no focus refetch, no interval, retry false); the provider refetches it on a socket open when the cache is older than the open and in reconnectNow"
  - "A raised or dismissed frame that arrives while the alerts have not loaded only invalidates the alerts query; writing a one-entry list would mark the query fresh (staleTime Infinity) and hide the alerts the first load is about to return"
  - "subscribeCostAlerts passes wasCached computed before the cache write, so only a frame-delivered alert the cache did not hold toasts; a ref of announced ids stops a redelivered frame from toasting twice"
  - "The cost-stop button is hidden when the derived cost state is none (the ended line explains why), matching JobProgressStrip and the Machine section; a disabled one shows its mapped reason"
  - "Dismissal is per viewer (UI-SPEC A2): the dismissal frame only reaches the dismissing viewer's tabs and the API stores it per viewer"
  - "The dialog's accrued fact reads useAccruedLive, so it also fetches the accrued value when a cost-stopping dialog opens without a cached value"

requirements-completed: [CST-04]

coverage:
  - id: D1
    description: "A pushed alert appears in its own server's workspace banner without a reload, newest first, once per id; another server's alert never shows; nothing renders before the first load or with no alert"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-banner.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-frames.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "Alert rows come first in the profile menu (at most three plus the more line), the badge counts distinct servers with a hidden description, the empty copy shows only when both lists are empty, and the fleet loads when either list is non-empty"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-menu.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/profile-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "One warning toast per newly pushed alert id; none for the first load, a cached id or a reconnect refetch; a dismissed frame removes the row without a toast; no refetch interval on the alerts query"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-menu.test.tsx#toasts once for a newly pushed alert and never for loaded, cached or refetched alerts"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-alerts-source-scan.test.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Banner and menu cost-stop buttons only ask the shared confirmation (sources alert-banner and alert-notification) and never call the action API; none without a cost-stop action or for a server missing from the fleet result; disabled with the mapped reason when unavailable"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-banner.test.tsx#cost alert banner actions"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-alert-menu.test.tsx#offers the cost-stop button only for a server in the fleet result that has a cost-stop action"
        status: pass
    human_judgment: false
  - id: D5
    description: "Dismissal: one request per alert, aria-disabled while pending, row removed only after the API accepted, failure keeps the row with the role alert line, Dismiss all in one request, ended line only when cost is none, predictable focus (next row, cost-stop button, workspace heading) and the Alert dismissed announcement"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-banner.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-alert-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D6
    description: "The cost confirmation dialog shows This session so far with the amount and as-of time for a cost-stopping action when the amount is known, and nothing for a start action, an unknown amount or no session"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirm-dialog.test.tsx#session so far"
        status: pass
    human_judgment: false
  - id: D7
    description: "The first real WebSocket push of an alert into a running workspace and the profile menu, the live badge update, and the German 320x568 one-screen layout of the banner are asserted with fixtures and the stylesheet text only; jsdom has no layout"
    requirement: "CST-04"
    verification: []
    human_judgment: true

duration: about 30 minutes
completed: 2026-11-21
---

# Phase 66 Plan 09: Cost Alert Banner and Profile Menu Summary

**A cost alert now arrives as a socket frame and shows up, without a reload or polling, in the server workspace banner and the first rows of the profile menu; the owner can open the shared "Shut down and stop billing" confirmation from either place, dismiss one or all alerts with predictable focus, and the confirmation states what the session has cost so far.**

## Performance

- **Duration:** about 30 minutes
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 8 created, 16 modified

## Accomplishments

- Tracer: frame parsers validate every field; the provider adds a raised alert to `hostingQueryKeys.costAlerts` once by id (newest first) and removes a dismissed one; `CostAlertBanner` is the first notice and lists the server's alerts with per-alert dismissal that removes the row only after the API accepted.
- Profile menu: up to three alert rows before the existing rows, the more-cost-alerts line, the distinct-server badge with a hidden description, the empty copy only when both lists are empty, and one warning toast per frame-delivered new alert through `subscribeCostAlerts`. The 15 s notifications poll is untouched (`profileNotificationPollInterval` still appears exactly twice).
- Full banner behavior: ended line only when cost is none, one cost-stop button through the shared confirmation, Dismiss all (counts hidden alerts), the role alert error line, the polite "Alert dismissed." announcement, focus rules, and the workspace h1 made focusable. The confirmation dialog gained the accrued fact.

## Task Commits

1. **Task 1: Tracer, pushed alerts in the workspace banner** - `b15c6b0e` (feat, webapp)
2. **Task 2: Profile menu rows, badge and arrival toast** - `f494bd17` (feat, webapp)
3. **Task 3: Cost-stop, dismiss all, focus and the accrued fact** - `eb03383e` (feat, webapp)

## Verification

- `vitest run tests/hosting tests/profile-menu.test.tsx tests/styles tests/locales`: 97 files, 1619 tests, all passing. `typecheck`, `lint --max-warnings 0` and `locales:check` clean.
- `format:check` reports 30 files with pre-existing drift, none touched by this plan.
- `git status --porcelain -- sources/WebApp` empty; `src/locales` unchanged (every string already existed from plan 66-08).
- Acceptance greps: `CostAlertRaisedMessage` once in `costFrames.ts`; `profileNotificationPollInterval` twice in `ProfileMenu.tsx`; `alert-banner`, `alert-notification`, `Server_Confirm_Fact_Accrued` (1), `tabIndex={-1}` and `CostAlertBanner` in the source scan are all present.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] A shared formatter and helper module that the plan did not list**
- **Found during:** Task 1
- **Issue:** The banner, menu row, toast and dialog all need the same absolute as-of time and money formatting, and the time formatter existed only as a private function in `AccruedCost.tsx`.
- **Fix:** Created `src/features/hosting/costAlertFormat.ts` (`formatInstant`, `formatAlertThreshold`, `formatAlertAmount`, `alertsOfServer`, `insertAlert`) and made `AccruedCost.tsx` import `formatInstant` from it, so the strip, the alerts and the dialog cannot disagree. Only the import and the removed private copy changed in `AccruedCost.tsx`.
- **Commit:** `b15c6b0e`

**2. [Rule 3 - Blocking] Existing tests encoded the old context shape and invalidation set**
- **Issue:** `FleetLiveUpdatesValue` gained the required `subscribeCostAlerts`, so three test files that build the context value failed to typecheck; the `reconnectNow` test counted exactly two invalidations; the cost confirmation dialog now calls `useQuery` for cost-stopping actions, so its tests need a `QueryClientProvider` and an `accruedCost` mock; the profile menu tests need a `costAlerts` mock.
- **Fix:** Added `subscribeCostAlerts: () => () => undefined` to the literals in `accrued-cost`, `fleet-page` and `live-badge` tests; updated the `reconnectNow` assertions to the fleet pair plus the alerts key; wrapped the dialog test renderer and mocked `accruedCost`; mocked `costAlerts` in the profile menu setup.
- **Files modified:** `tests/hosting/accrued-cost.test.tsx`, `fleet-page.test.tsx`, `live-badge.test.tsx`, `fleet-live-updates.test.tsx`, `cost-confirm-dialog.test.tsx`, `tests/profile-menu.test.tsx`
- **Commits:** `b15c6b0e`, `f494bd17`, `eb03383e`

**3. [Rule 2 - Missing critical] A frame before the first load must not become the whole list**
- **Found during:** Task 1 design
- **Issue:** Writing `[alert]` into an unloaded alerts cache would mark the query fresh (staleTime Infinity) and the real list would never load, hiding existing alerts.
- **Fix:** When the cache is empty the provider only invalidates the alerts query; the toast listener still fires with `wasCached` false.
- **Commit:** `b15c6b0e`, `f494bd17`

**4. [Rule 1 - Bug] Style guard rejected the 3px warning stripe**
- **Issue:** `border-left: 3px solid ...` in `cost-alerts.css` is a pixel literal.
- **Fix:** The alert row keeps the shared 1px card border and only recolors its left edge with the warning border token.
- **Commit:** `f494bd17`

**Total deviations:** 4 (one hardening, three blocking or process). No scope change and no new locale key.

## Plan Notes

- `useDismissCostAlerts().dismiss` and `dismissAll` resolve to `boolean` (accepted or not) instead of `void`, because the focus rules need to know whether the row really left. `useCostAlerts()` additionally returns `loading` so the menu can wait for the first alert load before it shows the empty copy.
- `CostAlertMenuItem` takes `onNavigate`, `returnFocus` and `onDismissed` in addition to the interface block's props; the menu closes before the confirmation opens exactly like the existing notification button, and focus after a dismissal goes to the next row's link or the close button.
- The cost-stop button is not rendered when the derived cost is `none`; the ended line already tells the viewer why, and it matches the progress strip and the Machine section.
- The dialog's accrued fact uses `useAccruedLive`, so a cost-stopping dialog opened from the fleet card or the menu fetches the accrued value once if it is not cached.
- The 320x568 German one-screen rule is asserted structurally (DOM order of title, first row and action; wrapping and 520px rules in the stylesheet text) because jsdom has no layout; the Phase 69 baselines remain the real check.

## Known Stubs

None.

## Threat Flags

None. Surfaces match the plan's threat model: T-66-30 (only `useCostConfirmation().request` is called and tests assert no action call), T-66-31 (frame parsers validate every field, duplicates are dropped by id), T-66-32 (names render as React text inside `bdi`).

## Self-Check: PASSED

- All created files exist; commits `b15c6b0e`, `f494bd17`, `eb03383e` are ancestors of HEAD (`git rev-list --count` from the ledger base is 3).

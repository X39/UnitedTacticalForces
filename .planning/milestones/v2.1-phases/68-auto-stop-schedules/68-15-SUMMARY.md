---
phase: 68-auto-stop-schedules
plan: 15
subsystem: webapp
tags: [react, automation, auto-stop, notice, cost-alerts, toasts, profile-menu]

requires:
  - phase: 68-auto-stop-schedules
    provides: Server_Automation_* copy keys (68-04), client types for pendingStop, canManageAutomation and AutoStop alert members, autoStopAlert fixture (68-12)
provides:
  - AutoStopNotice, the first workspace notice for every viewer while an automatic stop is pending
  - useAutoStopNotice (pending stop plus the cancellation toast transition) and useAutoStopReason (shared reason sentence)
  - AutoStop alert rows in the profile menu, the AutoStop arrival toast, and the cost banner filter
affects: [68-16, 68-17, 68-18, 68-19]

status: complete
commits: 3
plan_head_before: 8dd946cfff91d8396d4207a325384ad57bf436c1
plan_head_after: 0f4dae69
actuals:
  tokens: 11000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A hook returning a reason-wording function (useAutoStopReason) keeps typed t() calls inline and shares one closed reason map between two surfaces"
    - "AutoStop rows carry data-auto-stop-alert-id so threshold selectors keep counting threshold rows only"

key-files:
  created:
    - sources/ReactApp/src/components/server/AutoStopNotice.tsx
    - sources/ReactApp/src/features/hosting/useAutoStopNotice.ts
    - sources/ReactApp/tests/hosting/auto-stop-notice.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/components/server/CostAlertMenuItem.tsx
    - sources/ReactApp/src/components/server/CostAlertBanner.tsx
    - sources/ReactApp/src/components/server/cost-alerts.css
    - sources/ReactApp/src/features/hosting/useCostAlerts.ts
    - sources/ReactApp/src/components/ProfileMenu.tsx
    - sources/ReactApp/tests/hosting/cost-alert-menu.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx

key-decisions:
  - "The cancellation toast is suppressed when the server reports a pending job or an active Stop job exists, so a refetch that lands before the jobs query is not mistaken for a cancellation"
  - "An idle or maximum runtime stop without its limit snapshot shows no reason sentence rather than an invented count; unknown reasons use the schedule-neutral sentence (T-68-37)"
  - "Dismissing an AutoStop row shows no success toast, because the existing dismissed toast words a cost threshold and no automation copy key exists for it"
  - "Dismiss all in the cost banner sends only the threshold alert ids, so AutoStop records are never dismissed from the banner"

requirements-completed: [AUT-04]

coverage:
  - id: D1
    description: "A pending automatic stop shows the warning notice to every viewer first in the workspace notices; no notice without a pending stop; the link only for managers"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/auto-stop-notice.test.tsx"
        status: pass
  - id: D2
    description: "Cancellation toast on present-to-absent without a Stop job, none on first render, server change or a started Stop job"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/auto-stop-notice.test.tsx"
        status: pass
  - id: D3
    description: "AutoStop rows, shared cap and distinct-server badge, arrival toast once per new id, banner never lists AutoStop, per-viewer dismissal"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-menu.test.tsx, tests/hosting/cost-alert-banner.test.tsx"
        status: pass
  - id: D4
    description: "First real arrival of a warning through the socket, delivered Discord and email, live menu row and badge update"
    requirement: "AUT-04"
    verification:
      - kind: other
        ref: "Phase 68 UAT (backstop; not covered by a mock alone)"
        status: pending
---

# Phase 68 Plan 15: Automatic shutdown warnings in the browser Summary

**Every viewer of a server sees a pending automatic shutdown first in the workspace notices, and the owner gets an AutoStop row and warning toast through the existing alert surfaces.**

## Performance

- **Tasks:** 2 of 2 complete
- **Commits:** 3 (feat notice, feat rows and toasts, test)
- **Files:** 3 created, 8 modified, all under `sources/ReactApp`

## Accomplishments

- `AutoStopNotice` renders a single `StatePanel kind="warning"` from `automation.status.pendingStop`: title with the API `stopsAt`, the reason sentence with the limit snapshot, the consequence sentence, and a relative line in `aria-live="off"` that ticks on `useNow(30_000)`. The settings link appears only with `canManageAutomation`. It is mounted first in the `ServerWorkspacePage` notices, above `CostAlertBanner`.
- `useAutoStopNotice` shows one info toast (idle or settings copy) when a pending stop disappears with no active Stop job and no pending job, never on first render or on a server change.
- `CostAlertMenuItem` branches on `kind === "AutoStop"`: alert icon, server link, "Automatic shutdown at {time}", muted reason sentence, a Dismiss button with `Server_Automation_Menu_DismissLabel`, no cost-stop button, `data-auto-stop-alert-id`. Shared cap of three rows and the distinct-server badge are unchanged.
- `useCostAlertArrivalToasts` picks `Server_Automation_Toast_Warning` for AutoStop arrivals from the same announced-id ref; `CostAlertBanner` filters AutoStop alerts out.

## Task Commits

1. Task 1 (tracer): `bad7c348` feat(webapp): show the automatic shutdown notice
2. Task 2: `0928ddba` feat(webapp): add automatic shutdown alert rows and toasts
3. Task 2 tests: `0f4dae69` test(webapp): cover automatic shutdown warnings

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Profile menu focus lookup ignored AutoStop rows**
- **Found during:** Task 2
- **Issue:** After a dismissal, `ProfileMenu` looks up the next row by `[data-cost-alert-id]`. AutoStop rows carry `data-auto-stop-alert-id`, so focus would fall back to the close button.
- **Fix:** The selector and id match accept both attributes.
- **Files modified:** `sources/ReactApp/src/components/ProfileMenu.tsx` (not in the plan's file list; two-line change)
- **Commit:** 0928ddba

**2. [Rule 2 - Missing critical functionality] Dismiss all and the dismissed toast assumed threshold alerts**
- **Found during:** Task 2
- **Issue:** `dismissAll` would have sent AutoStop record ids from the cost banner, and the single-dismiss success toast would have worded an AutoStop record as "Cost alert dismissed: name, 0.00".
- **Fix:** `dismissAll` filters out AutoStop records; the single dismissal shows no toast for AutoStop (no automation copy key exists for it).
- **Files modified:** `sources/ReactApp/src/features/hosting/useCostAlerts.ts`
- **Commit:** 0928ddba

**3. [Design addition] `useAutoStopReason` shared hook**
- The reason wording is needed by both the notice and the menu row, so it lives once in `useAutoStopNotice.ts` instead of being duplicated.

### Environment note

The first `prettier --write` on `src/components/server` rewrote line endings of unrelated files (CRLF to LF, no content change). They were restored with `git checkout` before the final state; no unrelated file is part of any commit.

## Verification

- `tests/hosting/auto-stop-notice.test.tsx` (14), `cost-alert-menu.test.tsx`, `cost-alert-banner.test.tsx`: 60 tests pass.
- `tsc -b` and `eslint . --max-warnings 0` pass with no output.
- Wider `tests/hosting`, `tests/styles`, `tests/components` run: 1986 pass; 3 files timed out at the 5 s default under the parallel load of the full run (workspace-page and workspace-header tests). `workspace-page.test.tsx` alone passes 11 of 11; the timeouts are load flakes, not related to this plan.
- No file under `sources/Api` or `sources/WebApp` changed.

## Known Stubs

None.

## Threat Flags

None. T-68-37 and T-68-38 are mitigated: only API `stopsAt` is rendered (no client arithmetic, closed reason map), and the settings link requires `canManageAutomation`.

## Deferred (backstop, per plan)

- First real arrival of a warning through the socket, delivered Discord and email, and the live menu row and badge update are confirmed by the phase UAT.
- The 320x568 German one-screen assertion is covered here by a no-truncation unit test and re-confirmed by the Phase 69 Playwright mobile baselines.

## Self-Check: PASSED

All created files exist and the three commits are in the branch history.

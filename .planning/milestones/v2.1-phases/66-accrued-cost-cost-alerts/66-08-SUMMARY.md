---
phase: 66-accrued-cost-cost-alerts
plan: 08
subsystem: ui
tags: [react, tanstack-query, websocket, openapi, i18n, accrued-cost, cost-alerts]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 accrued cost calculation and GET game-servers/{id}/accrued-cost
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-05 cost alert endpoints and the GameServerAccruedCostMessage frame
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-06 alert settings endpoints, GameServerDto alert summary and users/me/locale
provides:
  - "Regenerated OpenAPI snapshot and src/generated/api.ts for every Phase 66 backend change (cmp and contracts:check clean)"
  - "Workspace cost strip accrued row: REST first value, value frames written into the query cache, no timer, no extrapolation, no polling"
  - "accruedModel (accruedPresentation, formatAccruedAmount, isNewerAccrued), costFrames (parseAccruedCostFrame), useAccruedLive, useDelayedDisconnect, useAccruedStoppedAnnouncement"
  - "All Phase 66 EN/DE strings (77 Server_Alerts_ keys plus the strip, dialog fact and nav keys)"
  - "hostingService.accruedCost, costAlerts, dismissCostAlert, dismissServerCostAlerts, costAlertSettings, personalCostAlertSettings, updateCostAlertSettings and identityService.recordLocale, each with a parity row"
affects: [66-09, 66-10, 66-11, 66-12, cost-alert-ui, finance-account-page]

plan_head_before: 2559c3659636fe96d2b6cbe361bdde35b0069bb9
plan_head_after: 6b7a6cba457df7caaf7b6d6144799aa8111348a0

actuals:
  tokens: 24500
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Pushed frames are validated and written into the same TanStack Query entry the REST value lives in; isNewerAccrued guards both the frame path and a reordered refetch"
    - "State adjusted during render (not a ref read, not an effect setState) for the stopped announcement, as in useJobAnnouncement"
    - "Epoch-keyed 3 s disconnect delay extracted into one hook shared by LiveBadge and the accrued row"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/costFrames.ts
    - sources/ReactApp/src/features/hosting/useAccruedLive.ts
    - sources/ReactApp/src/features/hosting/accruedModel.ts
    - sources/ReactApp/src/features/hosting/useAccruedStoppedAnnouncement.ts
    - sources/ReactApp/src/components/server/AccruedCost.tsx
    - sources/ReactApp/src/components/server/useDelayedDisconnect.ts
    - sources/ReactApp/tests/helpers/costAlertFixtures.ts
    - sources/ReactApp/tests/hosting/accrued-cost.test.tsx
    - sources/ReactApp/tests/hosting/accrued-model.test.ts
    - sources/ReactApp/tests/hosting/cost-alerts-source-scan.test.ts
    - sources/ReactApp/tests/hosting/cost-alert-services.test.ts
  modified:
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
    - sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/components/server/CostStrip.tsx
    - sources/ReactApp/src/components/server/LiveBadge.tsx
    - sources/ReactApp/src/components/server/workspace-shell.css
    - sources/ReactApp/src/api/services.ts
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The accrued query is useQuery with staleTime Infinity, no refetch on window focus and no refetch interval; it refetches only through the workspace exact invalidation (fleet signal, socket reopen, Refresh server)"
  - "A refetch that returns an older as-of than the cached value keeps the cached value (queryFn compares through isNewerAccrued), so reordered REST responses cannot move the number backwards either"
  - "Only the rate reason code maps to a sentence; not-yet-charged, hidden and unknown codes show the bare unknown sentence"
  - "A value that cannot be loaded (request error, no cached value) renders the unknown sentence, never a zero or the dash"
  - "The stopped announcement waits until the accrued value is final with a known amount, so the announced total is the final one"
  - "costAlertSettings lets 403 and 404 reject as ApiError (status and code) instead of using expectedStatuses, because expectedStatuses would make the two indistinguishable; personalCostAlertSettings uses expectedStatuses [204] for null"

requirements-completed: [CST-03]

coverage:
  - id: D1
    description: "OpenAPI snapshot equals the API export and api.ts equals the generator output; accrued-cost, cost-alerts, alert-settings and users/me/locale routes and their DTOs are present; the Kiota client is untouched"
    requirement: "CST-03"
    verification:
      - kind: other
        ref: "dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <tmp> && cmp <tmp> sources/ReactApp/openapi/utf-api.json; npm run contracts:check"
        status: pass
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "The strip shows the REST accrued amount with its as-of time, follows newer pushed frames without a REST call, ignores older, foreign and malformed frames"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/hosting/accrued-cost.test.tsx#follows a newer frame without a REST call and ignores older and foreign frames"
        status: pass
      - kind: unit
        ref: "tests/hosting/accrued-cost.test.tsx#ignores malformed frames without an error"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every accrued state: loading, no session, unknown with the mapped rate reason, final, API-final with active state model, still counting and live; Less than for tiny or zero while costing; paused sentence after 3 s; one stopped announcement"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/hosting/accrued-model.test.ts"
        status: pass
      - kind: unit
        ref: "tests/hosting/accrued-cost.test.tsx#accrued cost states in the row"
        status: pass
      - kind: unit
        ref: "tests/hosting/accrued-cost.test.tsx#accrued stopped announcement"
        status: pass
    human_judgment: false
  - id: D4
    description: "No interval timer and no refetch interval in the accrued files, proven live with an inline bad sample"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alerts-source-scan.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Every Phase 66 string exists in EN and DE with equal placeholders and plural forms; no alert button label is a banned generic label; one SVC parity row per new hostingService method"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/locales/server-keys.test.ts#Phase 66 locale vocabulary"
        status: pass
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
  - id: D6
    description: "Each new endpoint is called through hostingService or identityService with the specified verb, path and body; 204 and 409 are mapped"
    requirement: "CST-03"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-services.test.ts"
        status: pass
    human_judgment: false
  - id: D7
    description: "The first real socket push of an accrued value into a running workspace, and the German 320px wrapping of the accrued lines, are not exercised against the real API or a browser"
    requirement: "CST-03"
    verification: []
    human_judgment: true

duration: about 2h (interrupted once by a rate limit)
completed: 2026-11-21
---

# Phase 66 Plan 08: Accrued Cost in the Workspace Summary

**The workspace cost strip shows the control plane's accrued session cost with the time it was true, follows pushed frames in order without any client timer or calculation, and the whole Phase 66 client contract (regenerated API types, EN/DE strings, alert and settings service calls) now exists once for the parallel UI plans.**

## Performance

- **Duration:** about 2 h, interrupted once by a rate limit and resumed from the committed contract refresh
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 11 created, 15 modified

## Accomplishments

- Contract refresh: the OpenAPI snapshot and `api.ts` carry the accrued-cost, cost-alerts, alert-settings and `users/me/locale` routes plus the `GameServerDto` alert summary; `sources/WebApp` and the Kiota client are untouched (all changes additive).
- Tracer path end to end: REST value, `GameServerAccruedCostMessage` frame, query cache, accrued row. Frames are validated field by field, extras ignored, older values never replace newer ones.
- The accrued row implements the full UI-SPEC state table; the term switches to "Last session total" when final and cost is none; `LiveBadge` and the row share `useDelayedDisconnect`.
- 95 locale keys (77 `Server_Alerts_`) in English and German, the new service methods with typed conflict error, `identityService.recordLocale`, parity rows and fixtures.

## Task Commits

1. **Task 1a: Contract refresh** - `b774b6e4` (build, api)
2. **Task 1b: Tracer, accrued session cost in the strip** - `71bb29e7` (feat, webapp)
3. **Task 2: Every accrued state without polling** - `00f2294e` (feat, webapp)
4. **Task 3: Strings and client calls** - `6b7a6cba` (feat, webapp)

## Verification

- OpenAPI export `cmp` against the snapshot: identical. `npm run contracts:check`, `locales:check` (0 failures, 0 warnings), `typecheck` and `lint --max-warnings 0`: clean.
- `vitest run tests/hosting tests/locales tests/styles tests/app tests/routes.test.ts`: 1583 tests; the only failure in a full parallel run was a 5 s timeout of one heavy existing workspace-page test under load, which passes alone (11/11) and after the accrued mock was added to its setup.
- `git status --porcelain -- sources/WebApp` empty; `sources/Common/Ui/routes.json` unchanged against 383e68b0.
- `format:check` reports 32 files with pre-existing formatting drift, none of them touched by this plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests assumed the old invalidation set and copy**
- **Found during:** Task 1 and Task 2 test runs
- **Issue:** `workspace-live-updates.test.tsx` (three exact invalidations), `workspace-shell.test.tsx` (five Refresh keys) and `cost-strip.test.tsx` (needs a QueryClient, old "Not available yet" row) encoded the pre-Phase-66 behavior; `workspace-vocabulary.test.ts` rejected the spec-mandated "Cost stopped:" sentence because the key sits under a Phase 63 prefix.
- **Fix:** Updated the counts and key lists to include the accrued key; mocked `accruedCost` in the workspace shell and page test setups; the vocabulary guard now skips the `Server_Workspace_Cost_Accrued` keys (they describe the state of a session cost, not the name of the cost-stop action).
- **Files modified:** `tests/hosting/workspace-live-updates.test.tsx`, `workspace-shell.test.tsx`, `workspace-page.test.tsx`, `cost-strip.test.tsx`, `workspace-vocabulary.test.ts`
- **Commits:** `71bb29e7`, `6b7a6cba`

**2. [Rule 2 - Missing critical] A reordered refetch could move the amount backwards**
- **Found during:** Task 2 design
- **Issue:** Only the frame path compared as-of times; a slow REST refetch landing after a newer frame would overwrite it with an older amount, contradicting the must-have that the number never moves backwards.
- **Fix:** `useAccruedLive` keeps the cached value when the loaded one is not newer (`isNewerAccrued`).
- **Commit:** `00f2294e`

**3. [Rule 3 - Blocking] `identical-allowed.json` entry**
- `Server_Workspace_Cost_AlertsList` is the interpolation-only string `{{amounts}}` in both languages, which produced an identical-value warning; added an allow-list entry (file not listed in the plan).

**4. [Rule 3 - Process] Two tiny helpers outside the listed files**
- `useAccruedPresentation` lives in `useAccruedLive.ts` (a listed file) so `CostStrip` can take the row term from the same presentation as `AccruedCost` (one source of truth). The stopped-announcement hook also accepts `null` for the server because the page passes the query result.

**5. [Rule 3 - Process] Over-broad formatter run reverted**
- Two `prettier --write` runs over whole directories reformatted unrelated, pre-existing drifted files. Those files were restored with `git checkout -- <file>` before any commit; no unrelated file is in the plan commits.

**Total deviations:** 5 (one hardening, four blocking or process). No scope change.

## Plan Notes

- `costAlertSettings` lets 403 and 404 reject as `ApiError` carrying `status` and `code` (the interface block's "throws on 403/404 with status") instead of `expectedStatuses`, which would hand the page an indistinguishable `undefined` for both.
- The parity rows follow the Phase 62 convention (status `kept`, Today "New in Phase 66: ...") as the plan's amendment states.
- The e2e specs needed no edit: their literal server objects are cast and `tsc -b` reports no new required field.

## Known Stubs

None. The `Server_Workspace_Cost_AccruedUnavailable` key is retired from the strip but kept in the locale files until the Phase 69 cleanup, as planned.

## Threat Flags

None. Surfaces match the plan's threat model: T-66-27 (snapshot and types produced only by tooling, `cmp` and `contracts:check` clean), T-66-28 (frame validation and `isNewerAccrued`), T-66-29 accepted.

## Self-Check: PASSED

- All created files exist; commits `b774b6e4`, `71bb29e7`, `00f2294e`, `6b7a6cba` are ancestors of HEAD (`git rev-list --count` from the ledger base is 4).

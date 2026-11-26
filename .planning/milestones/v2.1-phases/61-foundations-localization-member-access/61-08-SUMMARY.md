---
phase: 61-foundations-localization-member-access
plan: 08
subsystem: webapp-cost-confirmation
status: complete
tags: [react, confirm-dialog, react-query, accessibility, focus-management, design-tokens]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-05 Server_Confirm_* locale keys and actionVocabulary, 61-07 extended ConfirmDialog, UnavailableReason, ApiText and server.css"
provides:
  - "buildCostConfirmationFacts: pure derivation of cost row, owner display, minutes and how-to-stop kind"
  - "CostConfirmDialog: the one confirmation dialog for cost-bearing and other lifecycle actions"
  - "useCostConfirmation().request({ serverId, actionId, source, returnFocus? }) and CostConfirmationProvider"
  - "CostConfirmationProvider mounted in CommunityLayout and CommandCenterLayout"
  - "renderWithProviders shared test helper"
affects: [61-09, 61-10, 61-11, 61-12, 61-13]

actuals:
  tokens: 21400
  tasks: 3
  commits: 3
plan_head_before: 2e24309199286b8aa2987fd5166dab149bb0b857
plan_head_after: b4d0530a1271fbbba0660f120c53484f37322dc0

tech-stack:
  added: []
  patterns:
    - "Provider-in-layout request hook: entry points call request(), only the dialog confirm handler executes"
    - "Live-while-open server query with a fresh cache seed so opening needs no network call"
    - "Derived dialog visibility (path, live data, action found) with a guarded set-state-during-render reset instead of effect state"
    - "Doubled selectors (.dialog.dialog--cost) to beat legacy .dialog rules loaded after component CSS"

key-files:
  created:
    - sources/ReactApp/src/components/server/costConfirmationModel.ts
    - sources/ReactApp/src/components/server/CostConfirmDialog.tsx
    - sources/ReactApp/src/components/server/useCostConfirmation.ts
    - sources/ReactApp/src/components/server/CostConfirmationProvider.tsx
    - sources/ReactApp/tests/helpers/renderWithProviders.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts
    - sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx
    - sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-layouts.test.tsx
  modified:
    - sources/ReactApp/src/components/server/server.css
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "request() resolves an uncached server with hostingService.server directly and seeds the query cache, instead of queryClient.fetchQuery: TanStack Query v5 treats an undefined queryFn result (the API returns undefined for 404) as an error and logs it"
  - "The server found for a request is written to hostingQueryKeys.server(id) with a 5 s staleTime on the live query, so a dialog opened from the fleet cache makes no network call and the live re-check starts 5 s later"
  - "Dialog confirm passes action.confirmationChallenge as the confirmation, which equals the typed text because Confirm stays blocked until it matches exactly"
  - "Pending and failure are derived from the mutation variables matching the active server and action, so a leftover failure never shows on another dialog; request() resets a non-pending mutation"

requirements-completed: [FND-04, FND-05]

coverage:
  - id: D1
    description: "buildCostConfirmationFacts derives hourly and monthly estimates, the five cost row kinds, owner display, rounded minutes and the how-to-stop kind"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/cost-confirm-model.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "CostConfirmDialog renders every facts row with its fallback, outcome-specific dismiss labels with initial focus on dismiss, variant per action, challenge gating, pending, request-error alert, stale reason, unknown-action API text in lang=en, and German copy"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/cost-confirm-dialog.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Provider: request never executes; confirm executes once; success toasts, invalidates five keys and returns focus; failure hides raw error text; pending ignores cancel, close and dismiss; repeated requests (also StrictMode) give one dialog; live unavailability blocks Confirm; deep-link strips only action; challenge sent; focus fallbacks; navigation closes; hook throws outside provider"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/cost-confirmation-provider.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "Both route layouts provide the confirmation context"
    requirement: "FND-05"
    verification:
      - kind: test
        ref: "tests/hosting/cost-confirm-layouts.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual result of the dialog (gold and danger confirm, amount at heading size, two-column facts, stacked actions at 520px, fade-in) and its contrast and layout in a real browser"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "The token-only guard passes, but no test asserts the rendered layout, spacing or fade; the Plan 61-13 browser scans cover contrast and layout"

duration: 23 min
completed: 2026-11-18
---

# Phase 61 Plan 08: Cost Confirmation Dialog, Request Hook and Layout Providers Summary

**One `CostConfirmDialog` and one `useCostConfirmation().request()` entry point, mounted in both route layouts, with a live stale-availability guard, localized error copy and focus return.**

## Performance

- **Duration:** 23 min
- **Started:** 2026-11-18T18:40:31Z
- **Completed:** 2026-11-18T19:04:19Z
- **Tasks:** 3
- **Files:** 13 (9 created, 4 modified)

## Accomplishments

- Task 1: `buildCostConfirmationFacts` (pure, no i18n) and `CostConfirmDialog` built on the shared `ConfirmDialog` with `className="dialog--cost"`, `initialFocus="dismiss"`, `confirmVariantFor`, `dismissKeyFor`, the challenge and helper, `busy`, the localized request-error, and `confirmDisabled` for a stale action. The facts `dl` always renders cost owner and how-to-stop; the cost row is omitted only for `NoCostImpact`; missing estimates, hidden or unknown owners and unknown cost impact show the documented fallbacks. Unknown action ids use the unknown title, an `ApiText` confirm label and a collapsed Server note. Dialog CSS was appended to `server.css` with doubled selectors, token-only values, 520px and 800px breakpoints, a fade-in and a reduced-motion override.
- Task 2: `useCostConfirmation` (types, context, throwing hook) and `CostConfirmationProvider`. `request()` resolves the server from the server cache, the fleet cache, or a single `hostingService.server` call, dedupes by server and action id, and opens a dialog whose visibility is derived from the path, the live server data and the found action. A live `useQuery` (5 s interval) feeds `stale`; confirm re-validates `enabled` and `pending` before one `executeAction`. Success closes, toasts, invalidates fleet, server, jobs, operations and notifications, strips `action` for the deep-link source and restores focus (returnFocus, connected invoker, then `main h1`). `renderWithProviders` was added for later plans.
- Task 3: Both `CommunityLayout` and `CommandCenterLayout` wrap their returned shell (not the early-return panels) in `CostConfirmationProvider`; a probe test proves the hook resolves under each, and the shared icon mock gained the 13 icons the layouts import.

## Task Commits

1. Task 1: facts model, dialog and CSS - `2885db67`
2. Task 2: provider, hook and test helper - `53e6b228`
3. Task 3: layout mounts - `b4d0530a`

## Verification

- `npm --prefix sources/ReactApp test`: 87 files, 647 tests pass
- `npm --prefix sources/ReactApp run typecheck`: exit 0
- `npm --prefix sources/ReactApp run lint` (`--max-warnings 0`, react-hooks 7): exit 0
- `tests/styles` style guard: pass (server.css token-only)
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` is reported (untouched by this plan, already in `deferred-items.md`); every file this plan touched is clean
- Acceptance: `dialog--cost` in server.css 28 (>=3), `ConfirmDialog` in CostConfirmDialog.tsx 7 (>=1), `executeAction` in the provider 1 (>=1), `renderWithProviders.tsx` exists, `CostConfirmationProvider` in CommandCenterLayout.tsx 3 and CommunityLayout.tsx 3 (>=2 each), `tests/profile-menu.test.tsx` still passes

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Uncached server is loaded without queryClient.fetchQuery**
- **Found during:** Task 2
- **Issue:** `hostingService.server` resolves `undefined` for a 404. TanStack Query v5 rejects an `undefined` query result with an error and logs it, so `fetchQuery` could not express "server missing" cleanly.
- **Fix:** `request()` calls `hostingService.server` directly, shows the `Server_Access_LoadError` toast on `undefined` or rejection, and seeds the cache with `setQueryData` on success. The live `useQuery` throws on `undefined` for the same reason.
- **Files modified:** `src/components/server/CostConfirmationProvider.tsx`
- **Verification:** provider tests for missing server and unknown action
- **Commit:** `53e6b228`

**2. [Rule 2 - Missing critical] Cache seed with staleTime so opening needs no network call**
- **Found during:** Task 2
- **Issue:** Plan test 1 requires opening from the fleet cache with no network call, but a live query with `placeholderData` alone refetches on mount.
- **Fix:** The found server is written to `hostingQueryKeys.server(id)` when the dialog opens and the live query uses `staleTime` of 5 s; `placeholderData` from the fleet cache is kept as a fallback. The API still revalidates every action, and confirm re-checks the live data.
- **Files modified:** `src/components/server/CostConfirmationProvider.tsx`
- **Verification:** provider test "opens from the fleet cache without a request call"
- **Commit:** `53e6b228`

**3. [Rule 3 - Blocking] Shared icon mock extended for the layouts**
- **Found during:** Task 3
- **Issue:** The layouts import 13 icons the shared mock did not export, so rendering them threw "Element type is invalid".
- **Fix:** Added `Apps20Regular`, `ArrowExit20Regular`, `Book20Regular`, `Calendar20Regular`, `Dismiss24Regular`, `Globe20Regular`, `Home20Regular`, `Image20Regular`, `Key20Regular`, `Money20Regular`, `People20Regular`, `Settings20Regular` and `Shield20Regular`. This was anticipated by the plan.
- **Files modified:** `tests/mocks/react-icons.tsx`
- **Commit:** `b4d0530a`

**Total deviations:** 3 auto-fixed (2 blocking, 1 missing critical). **Impact:** none for the public API of the plan; `request()` behaves as specified. The carry-over from 61-07 (import reason constants from `@/components/server/unavailableReasonMapping`) was followed.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. T-61-19 (confirmation still sent to the API, which revalidates; challenge-argument test), T-61-20 (live query plus a confirm-time re-check; test proves no `executeAction` and a visible mapped reason), T-61-21 (owner placeholders localized, owner rendered as a text node), T-61-22 (only localized request-error copy; test asserts the raw error text is absent) and T-61-23 (cancel, close and dismiss ignored while pending; repeated requests are a no-op) are mitigated and tested.

## Next Phase Readiness

Plans 61-09 to 61-12 can call `useCostConfirmation().request(...)` from any page under either layout and use `renderWithProviders` in tests. The provider reads `?action=` only through the `deep-link` source: the workspace deep-link effect (Plan 61-09) must call `request({ source: "deep-link", ... })` once the server query has data. Fleet-card, notification and workspace entry points still execute directly until their plans convert them.

## Self-Check: PASSED

- FOUND files: costConfirmationModel.ts, CostConfirmDialog.tsx, useCostConfirmation.ts, CostConfirmationProvider.tsx, renderWithProviders.tsx, cost-confirm-model.test.ts, cost-confirm-dialog.test.tsx, cost-confirmation-provider.test.tsx, cost-confirm-layouts.test.tsx
- FOUND commits: 2885db67, 53e6b228, b4d0530a

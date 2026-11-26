---
phase: 61-foundations-localization-member-access
plan: 10
subsystem: webapp-notifications
status: complete
tags: [react, profile-menu, command-overview, cost-confirmation, derived-state]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-05 locale keys, 61-07 UnavailableReason and ActionButton reason slot, 61-08 CostConfirmationProvider and useCostConfirmation, deriveServerState and useFleetQuery"
provides:
  - "Profile-menu notifications join the fleet query by serverId and render ServerStateSummary from deriveServerState"
  - "Profile-menu cost-stop closes the popover and requests the shared confirmation in place (source notification, focus returns to the profile trigger)"
  - "CommandAttentionItem.derived (DerivedServerState) replaces the raw detail string; exported OperationalSnapshot"
  - "commandOperationalSummary counts active deployments with isDeploymentActive"
affects: [61-11, 61-12, 61-13]

actuals:
  tokens: 30000
  tasks: 2
  commits: 2
plan_head_before: 42004a4e25f07a3c2afebfe0957f7851746b1c83
plan_head_after: 8a051fad0367dfa977d147654253ef26512673ff

tech-stack:
  added: []
  patterns:
    - "Notification surfaces are consumers of the one derived-state model and the one confirmation dialog"
    - "Unavailable cost-stop: ActionButton unavailableReason with UnavailableReason (aria-disabled, visible mapped reason)"
    - "Fleet query enabled only when the notification list is non-empty"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/ProfileMenu.tsx
    - sources/ReactApp/src/components/ProfileMenu.css
    - sources/ReactApp/src/pages/CommandOverviewPage.tsx
    - sources/ReactApp/tests/profile-menu.test.tsx
    - sources/ReactApp/tests/command-overview.test.ts

key-decisions:
  - "A disabled cost-stop without a known reason (not pending, canOperate true, enabled false, no API text) shows the generic mapped reason rather than inventing a deployment-state sentence, because the notification may have no fleet server"
  - "Test 3 of the overview renders the exported OperationalSnapshot with createElement from the existing .ts test file instead of adding a new .tsx file"
  - "Raw popover controls (trigger, scrim, close, sign out) outside the cost-stop were left as they are; the plan scoped the control conversion to the cost-stop"

requirements-completed: [FND-02, FND-04, ACC-03]

coverage:
  - id: D1
    description: "Profile notifications show the fleet-derived state and cost; the raw statusLabel is never rendered; a server missing from the fleet shows unknown with the cost chip decided by possibleCost and never Off or Stopped"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/profile-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "The fleet query is issued once for a non-empty notification list and not at all for an empty one"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/profile-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "The profile-menu cost-stop opens the shared dialog in place, executes only after the typed title with the exact challenge, never navigates, and returns focus to the profile trigger on dismiss"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/profile-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "A cost-stop the member cannot perform (no permission, disabled, pending) is aria-disabled with a visible mapped reason and opens nothing; unknown action labels sit in lang=en"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/profile-menu.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Command overview attention items carry derived state and cost, render through ServerStateSummary, never print raw status text, and the active count uses isDeploymentActive"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/command-overview.test.ts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual layout of the state summary under the notification title and the full-row reason inside the popover card"
    requirement: "FND-02"
    verification: []
    human_judgment: true
    rationale: "No test asserts rendered layout or contrast; Plan 61-13 browser scans cover it"

duration: 17 min
completed: 2026-11-18
---

# Phase 61 Plan 10: Notification Surfaces on the Shared State Model Summary

**The profile menu and the command-center overview now render the same derived server state and cost as the fleet and workspace, and the profile-menu cost-stop opens the shared confirmation in place instead of executing or navigating.**

## Performance

- **Duration:** about 17 min
- **Completed:** 2026-11-18
- **Tasks:** 2
- **Files:** 5 modified

## Accomplishments

- Task 1: `ProfileMenu` lost its `executeAction` mutation, pending-action state, local `ConfirmDialog`, `useNavigate` and the action-error branch. Notifications are joined to `useFleetQuery(Boolean(user) && notifications exist)` through a memoized `Map` by `primaryKey`; each row renders `ServerStateSummary` from `deriveServerState(server, { notification })`. The cost-stop is an `ActionButton` (ghost, compact) with `ActionLabel` (vocabulary label or `lang="en"` API label) or the in-progress text; when unavailable it carries `UnavailableReason` (operate-permission reason first, then the API reason, then the pending reason). Click calls `close()` then `request({ serverId, actionId, source: "notification", returnFocus: triggerRef })`. The CSS places the summary under the title and lets a disabled control's reason span the card; the rules for the removed status span, possible-cost text and plain button are gone.
- Task 2: `CommandAttentionItem.derived` replaces `detail`; notification items use `deriveServerState(matchingServer, { notification })`, pending-only items use `deriveServerState(server)`; merge rules and sort order are unchanged. The local raw-status helper was deleted in favour of `isDeploymentActive`. `OperationalSnapshot` is exported and renders `ServerStateSummary` plus the unchanged pending-work badge.
- Tests: `profile-menu.test.tsx` renders through `renderWithProviders` with the fleet and notifications spied; fixtures use the real API shape (`terminate`, typed-title challenge). Menu 1 to 7 are covered, including three disabled cases and the lang=en label. `command-overview.test.ts` keeps the summary expectation and asserts `derived` for joined, unmatched and pending-only items plus a rendered snapshot that never shows the raw label or stage.

## Task Commits

1. Task 1: profile notifications with derived state and in-place confirmation - `a4b259be`
2. Task 2 (TDD, RED confirmed before the change): command overview attention state - `8a051fad`

## Verification

- `tests/profile-menu.test.tsx` and `tests/hosting/cost-confirmation-provider.test.tsx`: 28 tests pass; `tests/command-overview.test.ts`: 10 tests pass
- `typecheck` exit 0; `lint` (`--max-warnings 0`) exit 0; `locales:check` 0 failures
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` (already in `deferred-items.md`)
- Full `npm test` run twice under load: 4 then 2 unrelated tests timed out (`workspace-page`, `event-slot-planner`, `wiki/page`, others); every flagged file passes in isolation. Treated as load-induced, not caused by this plan.
- Acceptance: `useFleetQuery` in ProfileMenu 2, `source: "notification"` 1, `useMutation` 0, `derived` in the overview page 4, `isDeploymentActive` 2, `statusLabel` in the overview page 0.

## Deviations from Plan

None - plan executed exactly as written. (Test 3 uses `createElement` in the existing `.ts` file; the plan allowed rendering the exported snapshot.)

## Known Stubs

None.

## Threat Flags

None. T-61-26 (misleading Stopped label), T-61-27 (execute without confirmation) and T-61-28 (fleet query for users with no notifications) are mitigated as planned and covered by tests.

## Notes for later plans

- `Hosting_Notification_ActionError` and `Hosting_Notification_ActionPending` locale keys are now unused by the profile menu; remove them when Plan 61-13 audits locale usage.
- The `?action=` deep link effect remains for Plan 61-11; `FleetPages.tsx` attention list and `ServerCard` for Plan 61-12.

## Self-Check: PASSED

- Modified files exist; commits `a4b259be` and `8a051fad` are on `gsd/v2.1-milestone`; `commits: 2` measured from the ledger

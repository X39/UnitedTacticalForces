---
phase: 63-workspace-shell-activity
plan: 04
subsystem: ui
tags: [react, cost-stop, cost-confirmation, vitest, fixtures, design-tokens]

requires:
  - phase: 62-fleet-quick-actions
    provides: actionCostCategory, buildCostConfirmationFacts, CostConfirmationProvider, fleetServer and fleetAction fixtures
  - phase: 63-workspace-shell-activity
    provides: requestedByName and actorName on the job and operation types (plan 63-02)
provides:
  - costStopAction, the single cost-stop selection for header, failure panel, progress strip retry mode and Machine section
  - serverCostFacts, the single owner/hourly/monthly rule shared by the confirmation dialog and the cost strip
  - CostConfirmationSource members workspace-header, workspace-failure and workspace-machine
  - workspaceJob, workspaceCommand and workspaceOperation test fixtures
  - ten icon entries in the Vitest react-icons mock
  - tokens --utf-workspace-fact-min, --utf-indicator-width and --utf-output-max-height
affects: [63-05, 63-06, 63-07, 63-08, 63-09, 63-10, 63-11, 63-12]

requirements-completed: [CST-01, CST-02]

actuals:
  tokens: 7000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "One pure selection function per UI decision (costStopAction), with the older caller delegating to it so behaviour cannot drift"
    - "Shared fact derivation (serverCostFacts) consumed by both the dialog model and, later, the cost strip"
    - "Fixture builders return fresh objects, use a module counter for UUID-formatted ids, and end in one type assertion so they compile across schema additions"

key-files:
  created:
    - sources/ReactApp/tests/helpers/workspaceFixtures.ts
    - sources/ReactApp/tests/hosting/workspace-fixtures.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/workspaceModel.ts
    - sources/ReactApp/src/components/server/costConfirmationModel.ts
    - sources/ReactApp/src/components/server/useCostConfirmation.ts
    - sources/ReactApp/tests/hosting/workspace-model.test.ts
    - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/src/styles/tokens.css

key-decisions:
  - "costStopAction order is usable graceful, usable destructive, disabled graceful, disabled destructive, so callers can render a disabled button with its reason instead of nothing"
  - "primaryServerAction calls costStopAction on its already-usable candidates, so its result is unchanged for every existing case"
  - "The three sizing tokens live in this foundation plan so plans 63-05, 63-06 and 63-09 never edit tokens.css in parallel"

patterns-established:
  - "CostConfirmationProvider does not switch exhaustively over the source, so new sources need no provider change"

coverage:
  - id: D1
    description: "costStopAction picks the cost-stop in the order usable graceful, usable destructive, disabled graceful, disabled destructive, ignores collect-logs and non-stopping actions, and primaryServerAction behaves as before"
    requirement: "CST-02"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-model.test.ts#cost-stop action"
        status: pass
    human_judgment: false
  - id: D2
    description: "serverCostFacts is the one owner, hourly and monthly rule, and buildCostConfirmationFacts returns the same values as before"
    requirement: "CST-01"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirm-model.test.ts#serverCostFacts"
        status: pass
    human_judgment: false
  - id: D3
    description: "The confirmation source union accepts workspace-header, workspace-failure and workspace-machine and typechecks end to end"
    requirement: "CST-01"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirm-model.test.ts#CostConfirmationSource"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run typecheck"
        status: pass
    human_judgment: false
  - id: D4
    description: "Shared fresh, complete job, command and operation fixtures with deterministic ids and null actor defaults"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-fixtures.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Ten icon mock entries (each verified to exist in the real @fluentui/react-icons package) and the three Phase 63 sizing tokens"
    verification:
      - kind: unit
        ref: "tests/styles (tokens-contrast and server-style-guard)"
        status: pass
      - kind: other
        ref: "grep of chunk-*.d.ts for each of the ten icon names, one match each"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-11-19
status: complete
---

# Phase 63 Plan 04: Cost Foundations and Test Fixtures Summary

**One cost-stop selection (`costStopAction`) and one cost-facts rule (`serverCostFacts`) reach the shared confirmation for every workspace caller, plus shared job/command/operation fixtures, ten icon mocks and three sizing tokens for the component plans.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-11-19T15:40:00Z
- **Completed:** 2026-11-19T15:48:18Z
- **Tasks:** 2
- **Files modified:** 9 (2 created, 7 modified)

## Accomplishments
- `costStopAction` selects the cost-stop in the order usable graceful, usable destructive, disabled graceful, disabled destructive; `primaryServerAction` now delegates to it with unchanged results.
- `serverCostFacts` returns owner, hourly (deployment before server) and monthly; `buildCostConfirmationFacts` derives its three values from it, so the dialog and the future cost strip cannot disagree.
- `CostConfirmationSource` accepts `workspace-header`, `workspace-failure` and `workspace-machine`; the existing sources stay valid.
- `workspaceJob`, `workspaceCommand` and `workspaceOperation` fixtures return fresh complete objects with UUID-formatted ids and null actor defaults.
- Ten icons added to the Vitest icon mock, and `--utf-workspace-fact-min`, `--utf-indicator-width` and `--utf-output-max-height` added to `tokens.css`.

## Task Commits

1. **Task 1: Tracer - one cost-stop choice and one cost-facts rule** - `3be638ad` (refactor)
2. **Task 2: Shared workspace fixtures, icon mocks and design tokens** - `36f56e18` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified
- `sources/ReactApp/src/features/hosting/workspaceModel.ts` - adds `costStopAction`; `primaryServerAction` delegates to it
- `sources/ReactApp/src/components/server/costConfirmationModel.ts` - adds `ServerCostFacts` and `serverCostFacts`
- `sources/ReactApp/src/components/server/useCostConfirmation.ts` - three new source members
- `sources/ReactApp/tests/hosting/workspace-model.test.ts` - cost-stop order, usable/pending, disabled fallbacks and primary-action parity cases
- `sources/ReactApp/tests/hosting/cost-confirm-model.test.ts` - `serverCostFacts` cases and a typed list of all confirmation sources
- `sources/ReactApp/tests/helpers/workspaceFixtures.ts` - the three fixture builders
- `sources/ReactApp/tests/hosting/workspace-fixtures.test.ts` - freshness, defaults and override tests
- `sources/ReactApp/tests/mocks/react-icons.tsx` - ten icon entries
- `sources/ReactApp/src/styles/tokens.css` - three sizing tokens

## Decisions Made
- Disabled cost-stops are returned as a last resort so header and failure UI can show a disabled button with its reason rather than nothing.
- The sizing tokens are owned by this plan so parallel component plans never touch `tokens.css`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. The three CRLF-only modified ReactApp files in the working tree (`FleetLiveUpdatesProvider.tsx`, `useWorkspaceLiveUpdates.ts`, `workspace-live-updates.test.tsx`) were left unstaged as instructed.

## Verification

- `npm test -- tests/hosting tests/styles`: 54 files, 699 tests passed.
- `npm run typecheck` and `npm run lint` (`--max-warnings 0`) exit 0; Prettier reports the changed files clean.
- The two task commits touch no path under `src/locales`.
- Tracer gate (auto mode): verify re-ran green, so expansion proceeded.

## Known Stubs

None.

## Threat Flags

None. T-63-09 is mitigated: both rules are pure functions over API-projected data and the selection order is pinned by tests.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plans 63-05, 63-06 and 63-09 can style with the three tokens; the component plans can import `costStopAction`, `serverCostFacts`, the fixtures and the icon mocks without touching shared files.

## Self-Check: PASSED

- Created files exist: `workspaceFixtures.ts`, `workspace-fixtures.test.ts`.
- Commits `3be638ad` and `36f56e18` are ancestors of HEAD.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

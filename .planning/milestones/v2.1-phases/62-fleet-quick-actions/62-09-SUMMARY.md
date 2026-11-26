---
phase: 62-fleet-quick-actions
plan: 09
subsystem: webapp
status: complete
tags: [react, fleet-card, cost-confirmation, accessibility, css-tokens, vitest]

# Dependency graph
requires:
  - phase: 62-01
    provides: shared fleet fixtures (fleetServer, fleetAction)
  - phase: 62-03
    provides: Server_Fleet_Open, Server_Fleet_Submitted, Server_Fleet_QueuedChange and owner placeholder keys
  - phase: 62-07
    provides: primaryActionFor, FleetPrimary and fleetStageKey from fleetModel.ts
provides:
  - FleetServerCard (kicker, h3 title, state summary, stage line, cost owner, one primary action, Open server link)
  - FleetSection (labelled section with an h2, a role list grid of cards, or an empty line)
  - Token-only fleet-card.css with an auto-fill grid, wrapping text and 44 px touch controls at or below 800 px
  - Executable coverage test that both fleet stylesheets stay under the server style guard
affects: [62-10, 62-11, 62-12, 63]

# Actuals (#2632)
actuals:
  tokens: 6800
  tasks: 2
  commits: 2
plan_head_before: 9b0ede50c4f73c747f3aac0f89573ecd60f07229
plan_head_after: ce58e00f9eac203c8fd4030fc475fed4bdf345ce

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fleet card is a thin rendering of fleetModel decisions; the card never reads a raw deployment status"
    - "Card-scoped override of the legacy .button nowrap and fit-content rules through .fleet-card .button"

key-files:
  created:
    - sources/ReactApp/src/components/server/FleetServerCard.tsx
    - sources/ReactApp/src/components/server/FleetSection.tsx
    - sources/ReactApp/src/components/server/fleet-card.css
    - sources/ReactApp/tests/hosting/fleet-card.test.tsx
  modified: []

key-decisions:
  - "The submitted state wins over every non-open-only primary: a disabled ghost In progress control with the Request sent reason; an open-only card stays open-only when submitted"
  - "A disabled lifecycle action keeps its own variant (gold or danger) and label, and switches the label to In progress only when the action itself is pending"
  - "The card article and the h3 keep the existing server-{id}-title id scheme so later focus logic and the old ServerCard selectors stay compatible"
  - "Fleet card buttons drop the legacy uppercase transform and wrap with overflow-wrap anywhere, per the UI-SPEC typography table"

patterns-established:
  - "FleetSection wraps Children.toArray output in li elements keyed by the React-assigned keys"

requirements-completed: [FLT-01, FLT-04, FLT-05]

coverage:
  - id: D1
    description: "FleetServerCard renders title h3, raw game identifier, state badge and cost, localized cost owner, the stage line only while starting, stopping or working, one primary action and the Open server link; no runtime provider, queue count, updated time, cost paragraph or teardown control"
    requirement: FLT-01
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-card.test.tsx#FleetServerCard"
        status: pass
    human_judgment: false
  - id: D2
    description: "The primary action follows primaryActionFor for every state, only calls useCostConfirmation().request with source fleet-card and onSubmitted, and disabled controls stay focusable with a visible reason and the card title as description"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-card.test.tsx#FleetServerCard"
        status: pass
    human_judgment: false
  - id: D3
    description: "FleetSection renders a labelled section with a role list of cards, or the empty line without a list"
    requirement: FLT-01
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-card.test.tsx#FleetSection"
        status: pass
    human_judgment: false
  - id: D4
    description: "fleet-card.css is token-only, uses an auto-fill grid with min(100%, the fleet card token), never truncates, and both fleet stylesheets are covered by the style guard"
    requirement: FLT-05
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-card.test.tsx#fleet card stylesheet; tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "The card wraps cleanly at 320 and 390 px with full-width wrapping buttons and 44 px controls at or below 800 px"
    requirement: FLT-05
    human_judgment: true
    rationale: "Visual layout at real viewport widths is not asserted by jsdom tests; verify in the browser at UAT"

duration: 7 min
completed: 2026-11-19
---

# Phase 62 Plan 09: Fleet Card and Section Summary

**One-scan fleet card that starts or stops a server only through the shared cost confirmation, a section list with its heading and empty line, and a token-only auto-fill card grid.**

## Performance

- **Duration:** about 7 min
- **Started:** 2026-11-19T09:48:57Z
- **Completed:** 2026-11-19T09:55:42Z
- **Tasks:** 2
- **Files created:** 4

## Accomplishments

- `FleetServerCard` renders the kicker, the h3 title, `ServerStateSummary`, the model's stage line, the cost owner row (hidden and unknown placeholders localized, never blank) and the actions block: one primary control decided by `primaryActionFor`, followed by the Open server link with `data-server-id`.
- Activation of an enabled primary calls `request({ serverId, actionId, source: "fleet-card", onSubmitted })` and nothing else; in-progress, disabled and submitted controls use `ActionButton` `unavailableReason` (focusable, `aria-disabled`, visible reason, described by the card title and the reason).
- `FleetSection` renders a section labelled by its h2 with a `role="list"` grid of list items, or the empty line and no list.
- `fleet-card.css` provides the auto-fill grid, the card surface, wrapping titles and values, full-width wrapping buttons and touch heights at or below 800 px using tokens only.
- 21 card, section and stylesheet tests cover every state, English and German, owner placeholders, a 256-character title, an id needing URL encoding, the unmapped status backstop and the style guard coverage claim.

## Task Commits

1. **Task 1: FleetServerCard and FleetSection with the card stylesheet** - `ae8f126e` (feat)
2. **Task 2: Card-level regression run and style guard coverage** - `ce58e00f` (test)

## Files Created/Modified

- `sources/ReactApp/src/components/server/FleetServerCard.tsx` - the fleet card
- `sources/ReactApp/src/components/server/FleetSection.tsx` - section heading and card list
- `sources/ReactApp/src/components/server/fleet-card.css` - token-only card, grid and section styles
- `sources/ReactApp/tests/hosting/fleet-card.test.tsx` - card, section and stylesheet coverage tests

## Decisions Made

- Submitted wins over every non-open-only primary; open-only cards stay open-only.
- A disabled lifecycle action keeps its variant and label; the label becomes In progress only when the action is pending.
- The title id keeps the existing `server-{id}-title` scheme.
- Card buttons are not uppercase and wrap anywhere, per the UI-SPEC typography table.

## Deviations from Plan

### Verification exceptions

**1. [Scope boundary] `format:check` reports one pre-existing unformatted file**
- **Found during:** Task 2 verification
- **Issue:** `npm run format:check` exits 1 on `tests/hosting/fleet-fixtures.test.ts` (committed content not Prettier formatted, already logged in `deferred-items.md` from 62-03 and 62-06) and `src/auth/claims.ts` (working-copy CRLF only; the committed content passes).
- **Fix:** None; neither file is owned by this plan and the orchestrator instructed not to fix deferred items outside this plan's files. Every file this plan created passes `prettier --check`.
- **Commit:** none

**Total deviations:** 0 auto-fixed; 1 verification exception for pre-existing format issues. **Impact:** none on this plan's deliverables.

## Issues Encountered

None.

## Known Stubs

None.

## Known Limitations

- Attention and unconfirmed cards show a disabled stop when the API disables it, which is always the case because the API enables stop only while the deployment is Running (research choice 1). Surface at UAT.

## Verification

- `npm --prefix sources/ReactApp test` - 103 files, 872 tests passed
- `npm --prefix sources/ReactApp run typecheck` - passed
- `npm --prefix sources/ReactApp run lint` - passed with zero warnings
- `npm --prefix sources/ReactApp run format:check` - only the two pre-existing deferred files reported
- `git diff --exit-code -- sources/Common/Ui/routes.json` - exit 0
- Acceptance greps: `export function FleetServerCard` 1, `source: "fleet-card"` 1, `auto-fill` 1, `auto-fit` 0, `text-overflow` 0, `<button` 0, `terminate` (case-insensitive) 0, `fleet-card.css` in the test file 3

## Next Phase Readiness

Ready for 62-10. The page integration (62-11) can render `FleetSection` with `FleetServerCard` children and own the submitted set keyed by server id.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/components/server/FleetServerCard.tsx
- FOUND: sources/ReactApp/src/components/server/FleetSection.tsx
- FOUND: sources/ReactApp/src/components/server/fleet-card.css
- FOUND: sources/ReactApp/tests/hosting/fleet-card.test.tsx
- FOUND: ae8f126e
- FOUND: ce58e00f

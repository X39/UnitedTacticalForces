---
phase: 63-workspace-shell-activity
plan: 06
subsystem: ui
tags: [react, workspace-header, cost-strip, clipboard, css, vitest]

requires:
  - phase: 63-workspace-shell-activity
    provides: workspace-header confirmation source, serverCostFacts and --utf-workspace-fact-min (plan 63-04); locale keys Server_Workspace_Cost_*, Server_Workspace_Endpoint_* and Shared_Copy_* (plan 63-01)
  - phase: 62-fleet-quick-actions
    provides: CostConfirmationProvider, ActionButton, primaryServerAction, fleetServer and fleetAction fixtures
provides:
  - WorkspaceHeader, the sticky compact header with the only h1, the state summary and the primary action
  - CostStrip, the always-five-row cost strip with the reserved accrued-cost slot
  - SlotPlaceholder, a reserved panel or inline slot with a data-slot hook
  - ServerEndpoint, the connection endpoint with copy or the running-only line
  - CopyButton, the shared clipboard button with an announced result and a visible failure text
  - workspace-shell.css, token-only styles for all five components
affects: [63-07, 63-08, 63-09, 63-10, 63-11, 63-12, 66]

requirements-completed: [WSP-01, CST-01, CST-02]

plan_head_before: 12c184f042b10632f3bbbaea0acafb89de312066
plan_head_after: 3f9aacd661042283a3360fb0340b0c2f242ee440

actuals:
  tokens: 8500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "The header action only calls request({ source: workspace-header }); the shared provider owns confirmation and execution"
    - "A live status region is always mounted next to the copy control; success text is sr-only, failure text is visible"
    - "SlotPlaceholder is a discriminated union (panel needs a title, inline does not) addressed by data-slot"

key-files:
  created:
    - sources/ReactApp/src/components/server/WorkspaceHeader.tsx
    - sources/ReactApp/src/components/server/CostStrip.tsx
    - sources/ReactApp/src/components/server/SlotPlaceholder.tsx
    - sources/ReactApp/src/components/server/ServerEndpoint.tsx
    - sources/ReactApp/src/components/CopyButton.tsx
    - sources/ReactApp/src/components/server/workspace-shell.css
    - sources/ReactApp/tests/hosting/workspace-header-bar.test.tsx
    - sources/ReactApp/tests/hosting/cost-strip.test.tsx
    - sources/ReactApp/tests/copy-button.test.tsx
  modified: []

key-decisions:
  - "The header title clamp, the cost strip grid and the copy status text are pure CSS in one token-only file; no IntersectionObserver and no JavaScript sizing"
  - "CopyButton tracks mount state and clears its single timer on unmount and on the next attempt, so a late clipboard promise never schedules a timer after unmount"
  - "ServerEndpoint reads only deployment.connectionEndpoint; it never compares a deployment status"

patterns-established:
  - "Generic components in src/components import their stylesheet from src/components/server so the style guard covers them (CopyButton)"

coverage:
  - id: D1
    description: "WorkspaceHeader holds the only h1 with the full title, the state and cost words and exactly one primary action button; a running may-cost server shows Shut down and stop billing, an off server Start server, a pending action In progress with aria-disabled and the pending reason, and an action-less server no button"
    requirement: "WSP-01"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-header-bar.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "The header action requests the shared confirmation only with source workspace-header and opens the stop dialog without executing the action"
    requirement: "CST-02"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-header-bar.test.tsx#requests the shared confirmation with the workspace-header source and the stop action id"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-header-bar.test.tsx#opens the stop confirmation dialog without executing the action"
        status: pass
    human_judgment: false
  - id: D3
    description: "CostStrip renders five rows with the owner mapped through ownerFor, hourly and monthly values equal to serverCostFacts, No estimate instead of a zero amount, Costing now for a running server and German labels"
    requirement: "CST-01"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#CostStrip"
        status: pass
    human_judgment: false
  - id: D4
    description: "The accrued row shows Not available yet inside data-slot=accrued-cost with no digit"
    requirement: "CST-01"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#keeps the accrued slot honest: Not available yet in data-slot, no digit"
        status: pass
    human_judgment: false
  - id: D5
    description: "ServerEndpoint shows the endpoint in code with a Copy address button only when connectionEndpoint is present, otherwise the running-only line without a button"
    requirement: "WSP-01"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#ServerEndpoint"
        status: pass
    human_judgment: false
  - id: D6
    description: "CopyButton copies only on click, shows Copied for 2 seconds with a polite announcement, and shows the visible failure text without throwing for NotAllowedError or a missing clipboard"
    verification:
      - kind: unit
        ref: "tests/copy-button.test.tsx"
        status: pass
    human_judgment: false
  - id: D7
    description: "Sticky positioning, two-line title clamp (one line at 520px and below), 44px actions at 800px and below and the two-column cost list at 520px and below pass the token-only style guard"
    requirement: "WSP-01"
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "The sticky behaviour under the command topbar and the clamped layout at 320 and 390 px need a real browser; plan 63-12 asserts them. This plan supplies the rules and the guard."

duration: 7min
completed: 2026-11-19
status: complete
---

# Phase 63 Plan 06: Sticky Header, Cost Strip, Endpoint and Copy Button Summary

**A sticky workspace header whose single primary action only requests the shared cost confirmation from `workspace-header`, a five-row cost strip that matches the dialog's cost rule and keeps the accrued slot honest, and an endpoint with a shared copy button that reports clipboard failure in visible text.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-11-19T16:00:02Z
- **Completed:** 2026-11-19T16:06:22Z
- **Tasks:** 2
- **Files modified:** 9 (all created)

## Accomplishments
- `WorkspaceHeader` renders the only `h1` with the full server name, `ServerStateSummary` for the derived state and one `ActionButton` chosen by `primaryServerAction`. The variant is `confirmVariantFor` (ghost while pending), and the pending and disabled reasons come from `ActionUnavailableReason`. Activation calls `request` with the header source and does nothing else.
- `CostStrip` always renders five `dt`/`dd` rows (owner, per hour, per month if left running, cost now, this session so far). Owner and estimates come from `serverCostFacts`, so the strip and the confirmation dialog cannot differ. A missing estimate reads "No estimate".
- `SlotPlaceholder` reserves the accrued-cost place with `data-slot="accrued-cost"` and the muted line "Not available yet", ready for Phase 66.
- `ServerEndpoint` shows the endpoint in `code` with "Copy address" when `deployment.connectionEndpoint` is set, and otherwise "Available while the server is running."; it never reads a deployment status.
- `CopyButton` copies only inside the click handler, swaps icon and label to "Copied" for 2 seconds, announces the caller message through an always-mounted `role="status"` region and shows `Shared_Copy_Failed` as visible text when the clipboard is missing or denied.
- `workspace-shell.css` holds all styles with `--utf-*` tokens (plus `--command-header-height` for the sticky offset), 1px borders and the 520px and 800px breakpoints only.

## Task Commits

1. **Task 1: Tracer - the sticky header shows name, state and the primary action that opens the shared confirmation** - `6cc41cc3` (feat)
2. **Task 2: Cost strip, reserved slot, endpoint and the shared copy button** - `3f9aacd6` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified
- `sources/ReactApp/src/components/server/WorkspaceHeader.tsx` - sticky header with h1, state and primary action
- `sources/ReactApp/src/components/server/CostStrip.tsx` - five-row cost strip
- `sources/ReactApp/src/components/server/SlotPlaceholder.tsx` - reserved panel or inline slot
- `sources/ReactApp/src/components/server/ServerEndpoint.tsx` - endpoint with copy or the running-only line
- `sources/ReactApp/src/components/CopyButton.tsx` - shared clipboard button
- `sources/ReactApp/src/components/server/workspace-shell.css` - token-only styles for the five components
- `sources/ReactApp/tests/hosting/workspace-header-bar.test.tsx` - seven header cases including the spy and real-provider dialog cases
- `sources/ReactApp/tests/hosting/cost-strip.test.tsx` - seven CostStrip and four ServerEndpoint cases
- `sources/ReactApp/tests/copy-button.test.tsx` - six CopyButton cases with a mocked clipboard and fake timers

## Decisions Made
- The sticky mechanism is pure CSS with one sticky element; the title clamp is a declared exception justified by the sticky bar, and the full text stays in the DOM.
- The copy success announcement is `sr-only` text inside the live region (the button label already confirms visually); only the failure text is visible.
- The failure text stays until the next attempt, as the UI-SPEC requires, and the status region is never unmounted so announcements are reliable.

## Deviations from Plan

None of Rules 1-4 applied. One note on the plan's own checks:

- Task 1 acceptance criterion `grep -c '"workspace-header"' WorkspaceHeader.tsx` expects 1, but it prints 2, because the plan also requires the class name `workspace-header` and that literal matches the same pattern. The criterion's intent (the only request source is `workspace-header`) is proven by the spy test, and the class name is required by the plan's own CSS description. No code was changed to game the count.
- Tasks 1 and 2 were `tdd="true"`, but each component and its test landed together in the one commit per task the plan prescribes; there is no separate RED commit.

**Total deviations:** 0 auto-fixed. **Impact:** none.

## Issues Encountered

- The user-event library installs its own clipboard stub, so the CopyButton tests use `fireEvent` with a controlled `navigator.clipboard` replaced through `Object.defineProperty` and restored afterwards.
- `beforeEach` is not an ESLint global in this project; the copy test imports it from `vitest`. The three CRLF-only modified ReactApp files in the working tree were left unstaged as instructed.

## Verification

- `npm test -- tests/hosting tests/styles tests/copy-button.test.tsx`: 57 files, 724 tests passed.
- `npm run typecheck` and `npm run lint` (`--max-warnings 0`) exit 0; Prettier clean on all changed files.
- Acceptance greps: `data-slot` in `SlotPlaceholder.tsx` 5, `serverCostFacts` in `CostStrip.tsx` 3, `utf-workspace-fact-min` in `workspace-shell.css` 1, `navigator.clipboard` in `CopyButton.tsx` 1, `position: sticky` 1.
- Single-owner guard (`tests/hosting/server-state-single-owner.test.ts`) green: no component reads `deployment.status` or `.statusLabel`.
- Tracer gate (auto mode): verify re-ran green, so expansion proceeded.

## Known Stubs

- `sources/ReactApp/src/components/server/CostStrip.tsx` accrued row: renders "Not available yet" by design (D-06). It is the reserved slot that Phase 66 fills with the real figure; it never shows 0 or an invented amount. Intentional, not a defect.

## Threat Flags

None. T-63-10 is mitigated: the clipboard is written only inside the click handler and failures are caught and shown. T-63-11 is mitigated: the owner goes through `ownerFor`. T-63-12 is mitigated: title, owner and endpoint render as React text children (`bdi`, `code`) with no `dangerouslySetInnerHTML`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 63-11 can compose `WorkspaceHeader`, `CostStrip` and `ServerEndpoint` into the shell (header after the breadcrumbs, details block below it). Activity and section plans can reuse `CopyButton` and `SlotPlaceholder`.
- The sticky offset and the clamped title at 320 and 390 px still need browser proof in plan 63-12.

## Self-Check: PASSED

- Created files exist: `WorkspaceHeader.tsx`, `CostStrip.tsx`, `SlotPlaceholder.tsx`, `ServerEndpoint.tsx`, `CopyButton.tsx`, `workspace-shell.css` and the three test files.
- Commits `6cc41cc3` and `3f9aacd6` are ancestors of HEAD; `git rev-list --count` from the plan base measures 2.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

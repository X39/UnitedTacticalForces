---
phase: 64-guided-server-creation
plan: 04
subsystem: ui
tags: [react, hosting, create-server, cost-summary, accessibility, css-tokens]
status: complete

requires:
  - phase: 64-guided-server-creation
    provides: createServerModel (CreateBlockReason, CreateCostEstimate, effectiveCostOwner), createFixtures, RadioGroupField, create.css
provides:
  - CreateSummary sticky cost summary and create bar with createBlockReasonKeys
  - CostOwnerLine read-only Billed to line with frozen and failed recovery, and costOwnerDisplayName
  - AfterCreatingChoice keep off or start radio group and the AfterCreating type
affects: [64-07]

commits: 2
plan_head_before: a1f0535226903fad254c3816110f20f2c1a898b0
plan_head_after: c410d88b97d499b4b1dadcd5492d3615056cff16

actuals:
  tokens: 11000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Exhaustive reason-to-copy map via as const satisfies, so a new CreateBlockReason fails typecheck until it has text"
    - "Blocked submit built on ActionButton unavailableReason: aria-disabled plus preventDefault also cancels Enter implicit submission"

key-files:
  created:
    - sources/ReactApp/src/components/server/CreateSummary.tsx
    - sources/ReactApp/src/components/server/CostOwnerLine.tsx
    - sources/ReactApp/src/components/server/AfterCreatingChoice.tsx
    - sources/ReactApp/tests/hosting/create-summary.test.tsx
    - sources/ReactApp/tests/hosting/create-choices.test.tsx
  modified:
    - sources/ReactApp/src/components/server/create.css

key-decisions:
  - "The summary owns no live region and does not use StatePanel; CostOwnerLine uses StatePanel because it sits in the main column"
  - "costOwnerDisplayName returns undefined for an unknown explicit id, even though effectiveCostOwner falls back to the caller's own owner, so the line never shows a different owner than the one selected"
  - "While pending the submit is natively disabled and aria-busy; the blocked state (reason) keeps it focusable"

requirements-completed: [CRT-02, CRT-03, CRT-04]

coverage:
  - id: D1
    description: "Summary shows the honest estimate (hourly, monthly only when known, calculating, not available, never 0) in en and de"
    requirement: "CRT-02"
    verification:
      - kind: unit
        ref: "tests/hosting/create-summary.test.tsx#CreateSummary cost"
        status: pass
    human_judgment: false
  - id: D2
    description: "Gold submit names the outcome and, for each of nine reasons, stays focusable with one visible linked reason; click and Enter never submit while blocked; no live region; invalid and request-error alerts"
    requirement: "CRT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/create-summary.test.tsx#CreateSummary submit"
        status: pass
      - kind: unit
        ref: "tests/hosting/create-summary.test.tsx#CreateSummary alerts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Billed to line with frozen warning and single Change cost owner recovery, viewer nickname fallback, loading and failed states"
    requirement: "CRT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/create-choices.test.tsx#CostOwnerLine"
        status: pass
    human_judgment: false
  - id: D4
    description: "After creating radio group defaults to keep off, explains both options, disables start with the launch-disabled reason"
    requirement: "CRT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/create-choices.test.tsx#AfterCreatingChoice"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-11-20
---

# Phase 64 Plan 04: Create Summary, Billed-to Line and After-creating Choice

Three tested components for the create page: a sticky cost summary whose gold submit is blocked with exactly one visible reason, a read-only Billed to line with the frozen recovery, and the Keep it off or Start it now choice.

## Accomplishments

- **Task 1 (tracer)**: `CreateSummary` with `createBlockReasonKeys` (all nine reasons), estimate headline (calculating, unavailable, known with optional monthly), size/storage/owner rows, while-off note, invalid-fields and request-error alerts, and the gold `ActionButton type="submit"` with `unavailableReason`. Summary and bottom-bar styles in `create.css`. Verified end to end (tests, styles guard, typecheck, lint) before expanding. Commit `e3300f79`.
- **Task 2**: `CostOwnerLine` (`costOwnerDisplayName`, loading, failed with retry, frozen warning, plain Billed to line, exactly one change button) and `AfterCreatingChoice` (heading inside the radio legend, start disabled with the `Server_Reason_LaunchDisabled` reason). `CreateSummary` now imports the `AfterCreating` type from `AfterCreatingChoice`. Commit `c410d88b`.

## Verification

- `vitest run tests/hosting/create-choices.test.tsx tests/hosting/create-summary.test.tsx tests/styles tests/locales`: 8 files, 119 tests passed.
- `npm run typecheck` and `npm run lint` (max-warnings 0): clean.
- `prettier --check --end-of-line auto` clean for all six touched files.
- `git diff -- sources/Common/Ui/routes.json`: no change.
- Acceptance greps hold: one `export function CreateSummary`, one `Server_Create_Blocked_EstimateFailed`, `unavailableReason` present, zero `aria-live` in `CreateSummary.tsx`, `create-summary__rows` at least twice in `create.css`, one `Server_Reason_LaunchDisabled` in `AfterCreatingChoice.tsx`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] node_modules missing in the fresh worktree**
- Ran `npm ci` from the lockfile (no package changes).

**2. Task ordering of the AfterCreating type**
- Task 1's commit declares `afterCreating` as the inline union `"keep-off" | "start"` because `AfterCreatingChoice.tsx` is created in Task 2; Task 2 switches the prop to the exported `AfterCreating` type. Behavior is identical and every commit typechecks.

**3. Pending submit is natively disabled**
- The plan does not say what the submit does while `pending`; it is `disabled` and `aria-busy` while the request runs so it cannot be activated twice. A blocking reason still keeps the button focusable (aria-disabled) when not pending.

### Deferred Issues

- `npm run format:check` over the whole project reports many pre-existing files because of CRLF checkout line endings; unrelated to this plan (same as plans 64-02 and 64-03).

## Known Stubs

None.

## Threat Flags

None. T-64-07 (only scoped owner names or the viewer's own nickname are shown, rendered as React text; a markup-named owner is tested as literal text), T-64-08 (aria-disabled plus preventDefault; click and Enter tested not to submit) and T-64-09 (fixed `Server_Create_Error` sentence only) are mitigated.

## Self-Check: PASSED

- FOUND: CreateSummary.tsx, CostOwnerLine.tsx, AfterCreatingChoice.tsx, create-summary.test.tsx, create-choices.test.tsx, create.css
- FOUND commits: e3300f79, c410d88b

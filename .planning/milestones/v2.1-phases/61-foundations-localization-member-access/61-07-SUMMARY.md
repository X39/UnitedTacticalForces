---
phase: 61-foundations-localization-member-access
plan: 07
subsystem: webapp-shared-components
status: complete
tags: [react, accessibility, aria-disabled, design-tokens, css-guard, confirm-dialog]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-02 --utf design tokens, 61-05 Server_* locale keys and serverState derivation, 61-01 ServerStateBadge tracer"
provides:
  - "ActionButton unavailableReason prop: aria-disabled, focusable, visible aria-describedby reason, no click and no form submit"
  - "UnavailableReason component and resolveUnavailableReason mapping of the six exact API reason strings (EN and DE)"
  - "CostIndicator, ServerStateSummary and ApiText shared server presentation components"
  - "components/server/server.css token-only stylesheet and the findStyleViolations style guard"
  - "ConfirmDialog additive props (kicker, confirmVariant, children, initialFocus, busy, error, className, challengeHelper, confirmDisabled) and PageHeader meta slot with focusable heading"
affects: [61-08, 61-09, 61-10, 61-11, 61-12, 61-13]

actuals:
  tokens: 11400
  tasks: 3
  commits: 3
plan_head_before: e18b277c36b85f7a7c3724bb92f5920f3dc93c5c
plan_head_after: 4d69e6ab17042152560f5b28e123426c0c335995

tech-stack:
  added: []
  patterns:
    - "Disabled-with-reason: aria-disabled plus a sibling reason span linked by aria-describedby, wrapper only when a reason exists"
    - "Keyed form remount (key by open state) resets ConfirmDialog challenge state without setState in an effect"
    - "Native dialog close event re-opens a busy dialog through a ref kept current in an effect"
    - "Token-only component CSS enforced by a fixture-backed guard test"

key-files:
  created:
    - sources/ReactApp/src/components/server/unavailableReasonMapping.ts
    - sources/ReactApp/src/components/server/UnavailableReason.tsx
    - sources/ReactApp/src/components/server/CostIndicator.tsx
    - sources/ReactApp/src/components/server/ServerStateSummary.tsx
    - sources/ReactApp/src/components/server/ApiText.tsx
    - sources/ReactApp/src/components/server/server.css
    - sources/ReactApp/tests/helpers/cssGuard.ts
    - sources/ReactApp/tests/styles/server-style-guard.test.ts
    - sources/ReactApp/tests/hosting/unavailable-reason.test.tsx
    - sources/ReactApp/tests/hosting/cost-indicator.test.tsx
  modified:
    - sources/ReactApp/src/components/ActionControl.tsx
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/src/components/server/ServerStateBadge.tsx
    - sources/ReactApp/src/styles.css
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/shared-controls.test.tsx
    - sources/ReactApp/tests/primitives.test.tsx

key-decisions:
  - "Renamed the pure reason mapping module from the planned unavailableReason.ts to unavailableReasonMapping.ts: unavailableReason.ts and UnavailableReason.tsx differ only in casing and collide on case-insensitive filesystems (Windows, macOS), where the extensionless component import resolved to the .ts module"
  - "ConfirmDialog is split into a shell (dialog element, open/close effects) and an inner form keyed by open state, so typed challenge state resets per opening with no setState in an effect"
  - "Hover rules of .button--gold, --ghost and --glass now exclude [aria-disabled=true], since the legacy rules had no :disabled exclusion to mirror"

requirements-completed: [FND-01, FND-02, FND-04, ACC-03]

coverage:
  - id: D1
    description: "ActionButton with unavailableReason is aria-disabled, focusable, described by a visible reason, never fires onClick and never submits a form; without a reason the DOM is unchanged"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/shared-controls.test.tsx#ActionButton with an unavailable reason"
        status: pass
    human_judgment: false
  - id: D2
    description: "UnavailableReason maps the six exact API reasons to EN and DE copy and quotes unknown text in a lang=en q element"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/hosting/unavailable-reason.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "ServerStateSummary and CostIndicator render state and cost from one DerivedServerState with decorative glyphs, visible text and a hidden chip when costVisible is false"
    requirement: "FND-01"
    verification:
      - kind: test
        ref: "tests/hosting/cost-indicator.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "components/server CSS uses tokens only and the guard rejects raw hex, colour functions and stray pixel literals"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "The visual result of the badge and chip colours, dashed unconfirmed border and wrap behaviour at 520px is not asserted by a test; the Plan 61-13 browser scans cover contrast and layout"
  - id: D5
    description: "ConfirmDialog and PageHeader extensions work additively: busy blocking, close re-open, error alert, challenge matching, initial focus, labelling, meta slot; all 83 existing test files stay green"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/primitives.test.tsx#ConfirmDialog additive capabilities"
        status: pass
      - kind: command
        ref: "npm --prefix sources/ReactApp test (83 files, 604 tests)"
        status: pass
    human_judgment: false

duration: 14 min
completed: 2026-11-18
---

# Phase 61 Plan 07: Shared Disabled-Reason, State Presentation and Dialog Extensions Summary

**Disabled-with-reason `ActionButton` (aria-disabled, focusable, visible described reason, no click or submit), a token-guarded server state and cost presentation, and additive `ConfirmDialog` and `PageHeader` extensions that keep all 16 existing dialog callers working.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-11-18T18:24:24Z
- **Completed:** 2026-11-18T18:38:43Z
- **Tasks:** 3
- **Files:** 17 (10 created, 7 modified)

## Accomplishments

- Task 1: `ActionButton` takes `unavailableReason`. With a reason it renders `aria-disabled="true"` and no `disabled`, stays in the tab order, appends the reason id to any caller `aria-describedby`, and swaps `onClick` for a handler that only calls `preventDefault` (blocking a `type="submit"` form post). Without a reason the output is the same single `<button>` as before. `UnavailableReason` and the pure `resolveUnavailableReason` map the six exact API strings, a `launchDisabled` hint, empty text, and unknown text (quoted in `<q lang="en">`, as a React text child).
- Task 2: `ServerStateBadge` now uses `server.css` through the new `StatusBadge` `className`; `CostIndicator` (live, maybe, none glyphs, hidden when `costVisible` is false), `ServerStateSummary` and `ApiText` added. `server.css` has 37 `var(--utf-` references, no hex and no pixel literal other than the 1px borders and the 520px breakpoint. `findStyleViolations` plus fixture-backed tests make the guard provably live and require at least one real stylesheet.
- Task 3: `ConfirmDialog` gained `kicker`, `confirmVariant`, `children`, `initialFocus`, `busy`, `error`, `className`, `challengeHelper`, `confirmDisabled`, with `title` and `confirmLabel` widened to `ReactNode`. It is labelled by its title, described by the description and body, sets `aria-busy`, ignores cancel while busy, re-opens on a native `close` while the `open` prop is still true, uses `aria-disabled` on confirm with click and submit guards, and focuses the error alert. `PageHeader` has a `meta` slot and a `tabIndex={-1}` heading.

## Task Commits

1. Task 1: disabled-with-reason and UnavailableReason - `aabe0d83`
2. Task 2: state and cost presentation, token CSS, style guard - `fda7f4e9`
3. Task 3: ConfirmDialog and PageHeader extensions - `4d69e6ab`

## Verification

- `npm --prefix sources/ReactApp test`: 83 files, 604 tests pass (includes every existing ConfirmDialog caller's tests)
- `npm --prefix sources/ReactApp run typecheck`: exit 0
- `npm --prefix sources/ReactApp run lint` (`--max-warnings 0`): exit 0
- `npm --prefix sources/ReactApp run locales:check`: 3108 keys per language, 0 failures
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` is reported (untouched by this phase, logged in `deferred-items.md`); every file this plan touched is clean
- Acceptance greps: `action-with-reason` in styles.css 2 (>=2), `button[aria-disabled="true"]` 1 (>=1), `unavailableReason` in ActionControl.tsx 5 (>=1), `var(--utf-` in server.css 37 (>=20), `initialFocus` in primitives.tsx 5 (>=1), `data-dialog-dismiss` 2 (>=1); no existing ConfirmDialog caller was edited

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Mapping module renamed to avoid a case-only filename collision**
- **Found during:** Task 1
- **Issue:** The plan names both `components/server/unavailableReason.ts` and `components/server/UnavailableReason.tsx`. On a case-insensitive filesystem the extensionless import `@/components/server/UnavailableReason` resolves to the `.ts` file first: the component was `undefined` at runtime and `tsc` reported TS1149 (file names differ only in casing).
- **Fix:** Named the pure module `unavailableReasonMapping.ts`. It exports exactly what the plan specified for `unavailableReason.ts` (`resolveUnavailableReason`, `operatePermissionReason`, `operationPendingReason`, `launchDisabledReason`, `deploymentStateReason`, `terminationPendingReason`, `pausedReason`, plus the context and result types).
- **Files modified:** `src/components/server/unavailableReasonMapping.ts`, `UnavailableReason.tsx`, `tests/hosting/unavailable-reason.test.tsx`
- **Verification:** typecheck, lint and the 20 Task 1 tests pass
- **Commit:** `aabe0d83`
- **Impact for later plans:** Plans 61-08 to 61-12 import the constants and resolver; they must use `@/components/server/unavailableReasonMapping`, not `.../unavailableReason`. The component import path `@/components/server/UnavailableReason` is unchanged.

**2. [Rule 2 - Missing critical] Hover rules also exclude aria-disabled**
- **Found during:** Task 1
- **Issue:** The plan said to exclude `[aria-disabled="true"]` from hover rules "the way `:disabled` is excluded", but no `.button` hover rule excludes `:disabled`. An aria-disabled control would otherwise react to hover as if actionable.
- **Fix:** Added `:not([aria-disabled="true"])` to `.button--gold:hover`, `.button--ghost:hover`, `.button--glass:hover`.
- **Files modified:** `src/styles.css`
- **Commit:** `aabe0d83`

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing critical). **Impact:** the module rename is the only change visible to later plans; it keeps the planned API and only changes one import path.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. T-61-16 (aria-disabled never activates, proven for click and form submit), T-61-17 (unknown API text only as React text inside `q lang="en"`, test asserts a `<b>` in the text is not parsed) and T-61-18 (cancel ignored while busy, native close re-opens, test fires the close event) are mitigated and tested.

## Next Phase Readiness

Plan 61-08 can build `CostConfirmDialog` on `ConfirmDialog` (`kicker`, `confirmVariant`, `initialFocus="dismiss"`, `busy`, `error`, `challengeHelper`, `confirmDisabled`, `className="dialog--cost"`) and append dialog rules to `server.css`; remember the `.dialog.dialog--cost` doubled selectors. Plan 61-09 to 61-12 can pass `unavailableReason` to `ActionButton` and render `ServerStateSummary`.

## Self-Check: PASSED

- FOUND files: unavailableReasonMapping.ts, UnavailableReason.tsx, CostIndicator.tsx, ServerStateSummary.tsx, ApiText.tsx, server.css, cssGuard.ts, server-style-guard.test.ts, unavailable-reason.test.tsx, cost-indicator.test.tsx
- FOUND commits: aabe0d83, fda7f4e9, 4d69e6ab

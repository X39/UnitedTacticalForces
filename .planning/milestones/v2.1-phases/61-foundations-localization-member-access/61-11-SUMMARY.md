---
phase: 61-foundations-localization-member-access
plan: 11
subsystem: webapp-server-workspace
status: complete
tags: [react, deep-link, cost-confirmation, security, notice]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-05 Server_DeepLink_* locale keys, 61-07 UnavailableReason and ActionUnavailableReason, 61-08/61-09 CostConfirmationProvider request({ serverId, actionId, source }) and the workspace page without ?action= handling"
provides:
  - "DeepLinkActionNotice: warning notice for a disabled or pending linked action, info notice for an unknown id (truncated to 40 code points, text only), Dismiss control, no focus steal"
  - "ServerWorkspacePage effect requesting the shared confirmation with source deep-link for any found, enabled, non-pending action (including start), idempotent per server and action"
  - "Notice derived in render inside the success branch only; dismiss strips action with replace"
  - "Missing server resolves to null so the existing not-found panel renders instead of a query error"
affects: [61-12, 61-13]

actuals:
  tokens: 40000
  tasks: 2
  commits: 2
plan_head_before: f643f435249ce23a18ec3d6d3e667c33f60324b0
plan_head_after: d9e64abf1b309e8158b1907dfd525a5486c1e041

tech-stack:
  added: []
  patterns:
    - "Untrusted query parameter: Array.find over the API array, vocabulary via Map, id as a React text child only"
    - "Deep-link effect with a ref handled-key; no setState in the effect (react-hooks 7); notice derived during render"

key-files:
  created:
    - sources/ReactApp/src/components/server/DeepLinkActionNotice.tsx
    - sources/ReactApp/tests/hosting/deep-link.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "An empty ?action= value is treated as no parameter (no notice, nothing requested)"
  - "The unavailable notice uses the vocabulary label (for example Restart game) when the action has one, otherwise the raw action id"
  - "The workspace server query maps a missing server (undefined from the service) to null, because react-query rejects undefined data"

requirements-completed: [FND-05, ACC-03]

coverage:
  - id: D1
    description: "?action=start (and any enabled non-pending action, case-insensitive) opens the shared cost confirmation once the server data exists and never executes by itself; terminate shows the typed challenge field"
    requirement: "FND-05"
    verification:
      - kind: test
        ref: "tests/hosting/deep-link.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "A disabled or pending linked action shows a warning notice with the mapped reason in English and German, opens no dialog and does not take focus; an unknown id shows an info notice with the id as text truncated to 40 characters"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/hosting/deep-link.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Forbidden, missing and loading servers show no notice, no dialog and no action hint"
    requirement: "FND-05"
    verification:
      - kind: test
        ref: "tests/hosting/deep-link.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "The action parameter is removed with replace, keeping other parameters, when the dialog is dismissed or confirmed and when the notice is dismissed; StrictMode opens a single dialog and a repeated link opens it again"
    requirement: "FND-05"
    verification:
      - kind: test
        ref: "tests/hosting/deep-link.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Prototype-style ids (constructor, __proto__, toString) and markup in the id are unknown ids rendered as plain text"
    requirement: "FND-05"
    verification:
      - kind: test
        ref: "tests/hosting/deep-link.test.tsx"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual placement and styling of the notice between the page header and the workspace tabs"
    requirement: "ACC-03"
    verification: []
    human_judgment: true
    rationale: "No test asserts rendered layout or contrast; Plan 61-13 browser scans cover it"

duration: 25 min
completed: 2026-11-18
---

# Phase 61 Plan 11: Action Deep Link Summary

**The `?action=` link on the server workspace now opens the shared cost confirmation for every found, enabled, non-pending action (including `start`) or explains why not through a dismissible notice, treating the parameter as untrusted text and never executing anything by itself.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-18
- **Tasks:** 2
- **Files:** 2 created, 2 modified

## Accomplishments

- Task 1: `DeepLinkActionNotice` takes a discriminated props union. `unavailable` renders `StatePanel kind="warning"` with `Server_DeepLink_Unavailable` (vocabulary label or the action id as the name) followed by `ActionUnavailableReason`. `unknown` renders `StatePanel kind="info"` with `Server_DeepLink_Unknown` and the id truncated to 40 code points (`Array.from`, ellipsis only when longer). Both carry an icon-only ghost `ActionButton` Dismiss; the notice never takes focus and never builds a link from the id. `Dismiss20Regular` was added to the icon mock.
- Task 2: `ServerWorkspacePage` reads `action`, finds the action with `Array.find` (case-insensitive), and an effect calls `request({ serverId, actionId, source: "deep-link" })` once the server data exists and the action is enabled and not pending. A ref handled-key makes it idempotent under StrictMode and re-arms when the parameter is removed; the effect calls no setState. The notice is derived in render inside the success branch, after the forbidden, loading, error and not-found branches. Dismiss removes only `action` with `replace`; the provider already strips it when the dialog closes.
- `tests/hosting/deep-link.test.tsx` (17 tests) covers start, upper-case STOP, terminate with challenge, loading, disabled (English and German) and pending notices, unknown/100-character/prototype/markup ids, forbidden and not-found, parameter stripping on dismiss and confirm and on notice dismiss, and StrictMode with a repeated link. RED was confirmed first (14 failing) before the page change.

## Task Commits

1. Task 1: deep link action notice - `6659e726`
2. Task 2 (TDD): effect, notice wiring and behavior suite - `d9e64abf`

## Verification

- `tests/hosting/deep-link.test.tsx`, `workspace-page`, `workspace-header`, `cost-confirmation-provider`: 45 tests pass
- `typecheck` exit 0; `lint` (`--max-warnings 0`) exit 0
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` (CRLF, already in `deferred-items.md`)
- Full `npm test`: 681 of 685 passed; the 4 failures (`wiki/page`, `event-slot-planner`, `admin-public-page-editor-page`) are load-induced timeouts in unrelated files and all pass in isolation
- Acceptance: `source: "deep-link"` in the page 1, `DeepLinkActionNotice` in the page 3, `Dismiss20Regular` in the mock 1

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A missing server showed a raw query error instead of the not-found panel**
- **Found during:** Task 2 (Test 6, not-found case)
- **Issue:** `hostingService.server` returns `undefined` for a 404, but react-query v5 rejects `undefined` query data, so the workspace rendered an alert with "data is undefined" and the existing not-found panel was unreachable. The plan requires a missing server to show the not-found panel.
- **Fix:** The workspace server query function returns `(await hostingService.server(...)) ?? null`; the existing `!server.data` branch then renders the not-found panel.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx`
- **Verification:** the not-found test mocks the real service shape (`undefined`) and passes; the other workspace tests still pass
- **Commit:** `d9e64abf`

**Total deviations:** 1 auto-fixed (1 bug). **Impact:** small, and needed for the must-have that a missing server shows only the not-found panel.

## Known Stubs

None.

## Threat Flags

None. T-61-29 (prototype keys and markup), T-61-30 (hints for forbidden or missing servers) and T-61-31 (link executing an action) are mitigated as planned and covered by tests; `executeAction` is asserted not to be called by a link.

## Self-Check: PASSED

- Created files exist: `DeepLinkActionNotice.tsx`, `deep-link.test.tsx`
- Commits `6659e726` and `d9e64abf` are ancestors of HEAD
- All task acceptance criteria and the plan verification pass (see above)

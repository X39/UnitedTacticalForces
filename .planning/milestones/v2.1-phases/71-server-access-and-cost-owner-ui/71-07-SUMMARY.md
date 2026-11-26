---
phase: 71-server-access-and-cost-owner-ui
plan: 07
subsystem: ui
tags: [react, settings-panel, cost-owner, search-picker, confirm-dialog, cost-strip, e2e-mocks]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-02 SearchPicker (with onFocus) and useDebouncedValue"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-05 Picker_* locale keys and the status-from-immediate-text picker shape"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-06 useCostOwnerTransfer, costOwnerTransferFailure, transferBlocker, serverCostOwnerOptions, costOwnerOptions key, GameServer.costOwnerIsFrozen and costOwnerAssignedAt"
provides:
  - "ServerCostOwnerPanel: collapsible Cost owner Settings panel (anchor server-cost-owner) with facts, frozen warning and badge, read-only sentence, owner picker, guarded Transfer button, gold confirmation and failure handling"
  - "cost-owner.css: token-only facts grid and transfer layout"
  - "CostStrip owner row link 'Change cost owner' to ?tab=settings#server-cost-owner for canModify on a non-archived server"
  - "Quiet GET cost-owner/options answers in both strict e2e routers"
  - "Parity row ADJ-server-cost-owner-panel (kept, phase 71)"
affects: [71-08, 71-09, 71-10, 71-11, 71-12, settings-section, cost-strip, e2e-mocks]

actuals:
  tokens: 12700
  tasks: 3
  commits: 3
plan_head_before: 04d6a7c06797080b33e65545e3c8e28e10825c5b
plan_head_after: 89a687a9f79166e121830eb0225c740db16b8d75

tech-stack:
  added: []
  patterns:
    - "Lazy options query: the picker's onFocus flips an engaged flag, the query is enabled only after it, so a panel that is only read costs no request"
    - "Focus after an async success through a ref plus an effect keyed on the arriving value, no setState in an effect"
    - "Literal key maps typed 'as const' for dynamic translation keys, so the typed t() and locales:unused both see them"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx
    - sources/ReactApp/src/components/server/cost-owner.css
    - sources/ReactApp/tests/hosting/server-cost-owner-panel.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/app/urlParameters.ts
    - sources/ReactApp/src/components/server/CostStrip.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/app/url-parameters.test.ts
    - sources/ReactApp/tests/helpers/settingsPanels.ts
    - sources/ReactApp/tests/e2e/support/settingsPanels.ts
    - sources/ReactApp/tests/e2e/support/mockApi.ts
    - sources/ReactApp/tests/e2e/support/automationMocks.ts
    - sources/ReactApp/tests/hosting/settings-section.test.tsx
    - sources/ReactApp/tests/hosting/cost-strip.test.tsx
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The options request starts on the first focus of the picker, not on opening the panel; this keeps read-only viewers, collapsed panels and the strict e2e mocks quiet"
  - "The no-match text depends on the query: an empty query reads 'There is no other cost owner you may use.', a typed query reads 'No matches' (the previously unused Picker_NoMatches key)"
  - "A frozen option is disabled with its reason, and a selected owner is not auto-cleared when a 400 makes it unavailable; the member clears it by hand after the options refetch"
  - "The strip link reuses the existing cost-strip__alerts paragraph class for the owner name line, so no stylesheet outside the plan changed"

patterns-established:
  - "Settings panel with a confirmation dialog inside its body, driven by a mutation hook that owns the cache refreshes"

requirements-completed: [D-01, D-03, D-11, D-12, D-13]

coverage:
  - id: D1
    description: "Every viewer of a non-archived server sees the Cost owner panel after Access and before Server record with the billed-to summary; an archived server has none"
    requirement: "D-01, D-03"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#renders no panel for an archived server"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#renders Access and Cost owner after Automation and before the Server record"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#keeps the Access panel and drops the Cost owner panel on an archived server"
        status: pass
    human_judgment: false
  - id: D2
    description: "Facts show the owner as the cost strip resolves it, Since with the locale date only when present, and a frozen owner shows a warning and a Frozen badge"
    requirement: "D-03"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#shows Since with the locale date when the assignment date is present"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#shows the frozen warning with the owner name and a Frozen badge in the header"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#says Assigned owner for an owner the viewer may not see"
        status: pass
    human_judgment: false
  - id: D3
    description: "Without canModify the read-only sentence shows and no picker or options request exists; with canModify the options start on first focus, list owners with Your account or kind, mark a frozen owner not selectable and distinguish empty from error"
    requirement: "D-03, D-11"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#shows the read-only sentence without canModify, with no picker and no options request"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#requests no owners until the picker takes focus, then lists the first owners"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#tells a failed load apart from an empty one and retries it"
        status: pass
    human_judgment: false
  - id: D4
    description: "Transfer stays visible and unavailable with a visible reason by priority running, jobs, nothing chosen"
    requirement: "D-12"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#explains an unavailable transfer by priority: running, then jobs, then nothing chosen"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#keeps the button unavailable for a server that still has a stopped deployment"
        status: pass
    human_judgment: false
  - id: D5
    description: "The confirmation names the new owner, the server and the old owner, lists the incurred-cost and alert consequences, gives the dismiss button initial focus and has the only gold button"
    requirement: "D-11"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#opens a confirmation that names the new owner, the server and the old owner"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#blocks dismissal while the transfer is pending and shows the busy label"
        status: pass
    human_judgment: false
  - id: D6
    description: "Success closes the dialog, clears the picker, toasts and focuses the owner value; 409, 400, 403 and other failures keep the dialog open with their mapped message"
    requirement: "D-11"
    verification:
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#transfers once, closes, clears the picker, toasts and focuses the new owner"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#keeps the dialog open with the message of HTTP %i"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-cost-owner-panel.test.tsx#keeps the dialog open with the generic message for a network failure"
        status: pass
    human_judgment: false
  - id: D7
    description: "The cost strip owner row links Change cost owner to ?tab=settings#server-cost-owner only for canModify on a non-archived server"
    requirement: "D-13"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#ends the cost owner row with Change cost owner for a modifier of a live server"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#offers no Change cost owner link without canModify"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#offers no Change cost owner link for an archived server"
        status: pass
    human_judgment: false
  - id: D8
    description: "The anchor, the open-panel lists, the parity row and the existing keyboard and axe audits stay green with the new panel open"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "tests/app/url-parameters.test.ts#declares each Settings panel anchor as the id of its panel in the code"
        status: pass
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#keeps the Phase 71 rows kept with Phase 71 test evidence (FND-07)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts S-06 and S-14 (en, de) on chromium; tests/e2e/hosting-automation.spec.ts and tests/e2e/hosting-a11y.spec.ts on chromium"
        status: pass
    human_judgment: false

# Metrics
duration: 55min
completed: 2026-11-26
---

# Phase 71 Plan 07: Cost owner panel and transfer Summary

**The Cost owner Settings panel for every viewer of a live server: who pays and since when, a frozen warning, a lazily loaded owner picker, a guarded Transfer button, a gold confirmation with mapped failures, and a "Change cost owner" link in the cost strip.**

## Performance

- **Duration:** about 55 min
- **Completed:** 2026-11-26
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files changed:** 18 (3 created, 15 modified)
- **Commits:** 3 task commits (measured from the plan head ledger)

## Accomplishments

- **Facts and gate (D-01, D-03):** `ServerCostOwnerPanel` renders `SettingsPanel id="server-cost-owner"` for every non-archived server, between Access and Server record. The summary is "Billed to {owner}", "Assigned owner" or "Cost owner unknown" through `serverCostFacts`, the same rule as the cost strip. The owner value is a `dd` with `tabIndex={-1}`; "Since" shows `formatDate` only when `costOwnerAssignedAt` is set; a frozen owner adds a warning panel and a "Frozen" header badge.
- **Picker (D-11):** `SearchPicker` with `minQueryLength={0}`. The first focus flips `engaged`, which enables the options query (250ms debounce, `keepPreviousData`, abort signal). Options carry a decorative wallet icon, the name in `bdi`, "Your account" or the kind label through `normalizeOwnerKind`, and a frozen owner is disabled with "Frozen, cannot be chosen". Status follows the immediate query text. Empty and failed loads are separate states (no-match text versus the "could not be loaded" error row with retry).
- **Transfer (D-11, D-12):** the primary button is always visible with a visible reason by priority (running via `server.deployment`, jobs, nothing chosen). It opens a `ConfirmDialog` with the new owner, the server and the old owner in the description, the incurred-cost and alert sentences as a list, a gold confirm, a primary dismiss with initial focus, no backdrop close, and busy blocking dismissal. A success closes the dialog, clears the picker, shows the toast and moves focus to the owner value once the refetched server carries the new owner. 409, 400, 403 and everything else keep the dialog open with their own message. The panel adds no invalidations; `useCostOwnerTransfer` owns them.
- **Strip link (D-13):** the cost strip owner row ends with a ghost compact `ActionLink` to `?tab=settings#server-cost-owner`, only for `canModify && !isArchived`.
- **E2E quiet defaults:** `GET /api/game-servers/{id}/cost-owner/options` answers `[]` in `baselineHandlers` and in the strict router of `automationMocks.ts`.

## Task Commits

1. **Task 1: tracer, the Cost owner panel shows who pays, wired into Settings** - `5b05a6bb` (feat)
2. **Task 2: the transfer flow, picker, guarded button, confirmation, success and failures** - `9131138b` (feat)
3. **Task 3: cost strip link and quiet e2e defaults for the options request** - `89a687a9` (feat)

## Verification

- Whole Vitest run (`npx vitest run --maxWorkers=4`): 221 files, 3728 passed, 2 skipped, 0 failed.
- `tests/hosting` (including cost-strip, route-links scan): 121 files, 2455 passed. The new panel test has 27 cases.
- `locales:check`: 4004 English and 4004 German keys, 0 failures. `locales:unused`: no `Server_CostOwner_*` or `Picker_*` warning (`Picker_NoMatches` is now used).
- `lint` (max-warnings 0), `typecheck` (`tsc -b`) and `prettier --check --end-of-line auto` over the touched files: clean. `contracts:check` and `build:assets`: ran, no content change (see finding XPF-71-07-5).
- Playwright on chromium through the lock helper: `a11y-audit.spec.ts` S-06 (en, de) and S-14 (en, de) pass; `hosting-automation.spec.ts` and `hosting-a11y.spec.ts` 49 passed, 12 skipped (screenshot cases that skip by design), 0 failed, no unexpected request.
- Tracer gate: the Task 1 `<verify>` is automated only, so the same command ran end to end after the commit's changes (vitest on the four files, locales checks, typecheck) and passed before Task 2.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Dynamic failure keys broke the typed translation function**
- **Found during:** Task 2 typecheck
- **Issue:** `failureKeys` was typed `Record<CostOwnerTransferFailure, string>`, so `t(failureKeys[...])` did not satisfy the typed key union.
- **Fix:** `as const satisfies Record<CostOwnerTransferFailure, string>`, which keeps the literal keys visible to `tsc` and to `locales:unused`.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx`
- **Committed in:** `9131138b`

### Plan wording

**2. The no-match text splits by query**
- The plan names `Server_CostOwner_NoOptions` as the no-match text. With a typed query that sentence ("There is no other cost owner you may use.") would be wrong, so a typed query reads `Picker_NoMatches` and the empty query reads `Server_CostOwner_NoOptions`, as the UI-SPEC state table lists both. This also uses the key that nothing referenced before.

Otherwise the plan was executed as written. No file outside the plan's `files_modified` list changed.

## Cross-plan findings

- **XPF-71-07-1:** After a transfer the focus moves to `.cost-owner-facts dd` only when the `server` prop arrives with the new `costOwnerId`. The panel must be rendered with a server that follows the exact server query (the Settings section does); a static prop never moves focus. E2E specs of 71-08 and later can assert focus on that element after the refetch.
- **XPF-71-07-2:** Any strict e2e router added in later plans must answer `GET /api/game-servers/{id}/cost-owner/options` with `[]`, or a keyboard walk that tabs into the picker records an unexpected request. `baselineHandlers` and the automation router already do.
- **XPF-71-07-3:** Dynamic translation keys need `as const` maps, because `t()` is typed over the key union and `locales:unused` only sees literal keys. The kind labels and failure messages in the panel follow this.
- **XPF-71-07-4:** The cost strip owner row wraps the name in `p.cost-strip__alerts` only when the link shows, so the DOM of read-only viewers and archived servers is unchanged and the existing strip tests did not need edits.
- **XPF-71-07-5:** `contracts:check`, `build:assets` and the e2e web server leave `src/generated/api.ts` with line-ending-only changes in a CRLF worktree. `git checkout -- sources/ReactApp/src/generated/api.ts` restores it; nothing of it was committed.
- **XPF-71-07-6:** The toast owner name is the picker option label of the chosen owner, so it speaks even when the refetch is slow; the facts row and the summary follow from the refetch.

## Known Stubs

None. No placeholder data or unwired value was introduced.

## Threat Flags

None. The panel adds no endpoint; it uses the already-contracted PUT and options routes covered by T-71-17 to T-71-19 (hidden owner via `serverCostFacts`, two-step confirmation with dismiss-first focus and no backdrop close, status-mapped 403).

## Self-Check: PASSED

- FOUND: `sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx`
- FOUND: `sources/ReactApp/src/components/server/cost-owner.css`
- FOUND: `sources/ReactApp/tests/hosting/server-cost-owner-panel.test.tsx`
- FOUND commit `5b05a6bb`
- FOUND commit `9131138b`
- FOUND commit `89a687a9`

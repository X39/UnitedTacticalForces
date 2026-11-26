---
phase: 61-foundations-localization-member-access
plan: 09
subsystem: webapp-workspace
status: complete
tags: [react, server-workspace, cost-confirmation, accessibility, derived-state]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-05 locale keys and actionVocabulary, 61-07 UnavailableReason/ApiText/ActionButton reason slot, 61-08 CostConfirmationProvider and useCostConfirmation"
provides:
  - "Workspace header, overview record, overview deployment and cloud panels render state and cost only through deriveServerState and ServerStateSummary"
  - "ActionUnavailableReason and actionIsUnavailable: mapped reason content for a projected action given its server"
  - "ServerActions({ server, actions }), primary panel and logs refresh all call request({ serverId, actionId, source })"
  - "primaryServerAction prefers the enabled non-dangerous cost-stop over the destructive one"
  - "ActionLabel: localized vocabulary label or lang=en API label"
affects: [61-10, 61-11, 61-12, 61-13]

actuals:
  tokens: 36000
  tasks: 2
  commits: 2
plan_head_before: 647f615b3571fdbc7884ac7c82fc97f94dffb90e
plan_head_after: ac4c78f77adabc36101824916a27c918736ddf3b

tech-stack:
  added: []
  patterns:
    - "Entry points only call request(); the one provider dialog executes"
    - "Unavailable control: ActionButton unavailableReason with ActionUnavailableReason content (aria-disabled, visible mapped reason)"
    - "API prose rendered as React text inside ApiText (lang=en); unknown action labels fall back to ApiText"

key-files:
  created:
    - sources/ReactApp/src/components/server/ActionUnavailableReason.tsx
    - sources/ReactApp/tests/hosting/workspace-header.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/workspaceModel.ts
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/src/features/hosting/ServerLogsPanel.tsx
    - sources/ReactApp/tests/hosting/actions.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/workspace-model.test.ts

key-decisions:
  - "ServerLogsPanel requests the confirmation itself through useCostConfirmation (props: server, jobs, collectLogsAction) instead of an onCollect callback, so no action plumbing remains in ServerWorkspaceView"
  - "ActionLabel is exported from features/hosting/components.tsx and shared by ServerActions and the primary panel"
  - "Stale 'Hosting lifecycle execution is paused' literal in the workspace view replaced by the pausedReason constant from unavailableReasonMapping"

requirements-completed: [FND-02, FND-04, ACC-03]

coverage:
  - id: D1
    description: "Workspace header, overview record panel and cloud panel render the same derived state badge and cost chip; technical status appears only as a labelled secondary detail"
    requirement: "FND-02"
    verification:
      - kind: test
        ref: "tests/hosting/workspace-header.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "primaryServerAction prefers the graceful non-dangerous StopsCost action, falls back to the dangerous one, ignores collect-logs, and does not substring-match cost impact"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/workspace-model.test.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Workspace primary, secondary and logs actions request the shared dialog and never execute; the typed challenge gates Confirm and the exact challenge is sent"
    requirement: "FND-04"
    verification:
      - kind: test
        ref: "tests/hosting/actions.test.tsx"
        status: pass
      - kind: test
        ref: "tests/hosting/workspace-page.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "Unavailable and pending actions are aria-disabled with a visible mapped reason in English and German and open nothing; API prose and unknown labels sit in lang=en"
    requirement: "ACC-03"
    verification:
      - kind: test
        ref: "tests/hosting/actions.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual placement of the header state summary, the reason text beside action buttons and the archive-dismiss dialog in a real browser"
    requirement: "FND-02"
    verification: []
    human_judgment: true
    rationale: "No test asserts rendered layout or contrast; Plan 61-13 browser scans cover it"

duration: 22 min
completed: 2026-11-18
---

# Phase 61 Plan 09: Workspace on the Shared State Model and Confirmation Summary

**The server workspace now shows one derived state and cost in the header, overview and cloud panels, and every lifecycle action (primary, list, logs refresh) requests the single shared confirmation with disabled-with-reason for unavailable actions.**

## Performance

- **Duration:** about 22 min
- **Completed:** 2026-11-18
- **Tasks:** 2
- **Files:** 9 (2 created, 7 modified)

## Accomplishments

- Task 1: `primaryServerAction` classifies candidates through `actionCostCategory` and, while provider cost may remain, picks the first enabled non-dangerous stop, then the first dangerous stop, then falls through to start, stop and lowest order. The page header gets `meta={<ServerStateSummary .../>}`; the overview record panel and the cloud panel heading use `ServerStateSummary`; the overview deployment panel heading is `t(derived.labelKey)` with a `small` detail line; the cloud panel gains a first "Technical status" fact row. No label is built from `deployment.status` any more.
- Task 2: New `ActionUnavailableReason` and `actionIsUnavailable`. `ServerActions({ server, actions })` renders `ActionLabel` (vocabulary label or `ApiText` fallback), the description in `ApiText`, and an `ActionButton` with `confirmVariantFor` and `request({ source: "workspace-action" })`. The primary panel uses `source: "workspace-primary"`; the logs panel drops `busy`, takes `server` and requests with `workspace-action`. `ServerWorkspaceView` lost `actionBusy`, `onExecuteAction`, the selected-action state and the bottom dialog; the page lost the action mutation, the requested-action memo and the second confirmation (no leftover reference). Refresh, Back, Delete, job Retry/Skip, "show all", download anchors and the HostingError retry now use `ActionButton`, `ActionLink` and `ActionAnchor`; Delete shows the existing delete-unavailable text as a visible reason; the archive dialog dismiss label is `Server_Action_ArchiveRecord_Dismiss`. `ActionConfirmation`, `ServerStatus` and `serverTone` were deleted in one cut.
- Tests: `actions.test.tsx` rewritten through `renderWithProviders` (dialog opens without executing, challenge gating with exact `executeAction(serverId, "terminate", "TERMINATE")`, lang=en prose, unknown label, mapped reason, English and German pause reason, pending); `workspace-page.test.tsx` wrapped in the shared provider helper with the Start fixture corrected to `dangerous: false` / `confirmationChallenge: null` and `costImpact: "NoCostImpact"` on collect-logs, plus a terminate dialog test; `workspace-header.test.tsx` is new; `workspace-model.test.ts` covers the new rule.

## Task Commits

1. Task 1: derived state in header and panels, graceful primary action - `580c5ae7`
2. Task 2: workspace actions through the cost confirmation - `ac4c78f7`

## Verification

- `npm --prefix sources/ReactApp test -- tests/hosting`: 29 files, 362 tests pass
- `typecheck`: exit 0; `lint` (`--max-warnings 0`): exit 0
- `format:check`: only the pre-existing `tests/hosting/arma-placement.test.tsx` (already in `deferred-items.md`)
- Full `npm test` run twice under load: 2 to 3 unrelated tests failed each time and a different set each run (`tests/wiki/page.test.tsx`, `tests/admin-public-page-editor-page.test.tsx`, a wiki Radio test); the same files pass in isolation (4 files, 11 tests). Treated as load-induced timeouts, not caused by this plan (no shared code touched).
- Acceptance: `export function ActionConfirmation` 0, `export function ServerStatus` 0, `useCostConfirmation` in the page 2, `source: "workspace-primary"` 1, no `className="button` left in the page or logs panel, no `requestedAction`/`actionBusy`/`onExecuteAction` references remain.

## Deviations from Plan

**1. [Rule 2 - Missing critical] ActionLabel helper added to components.tsx**
- **Found during:** Task 2
- **Issue:** The plan required the localized label (vocabulary or `ApiText` fallback) in both `ServerActions` and the primary panel but named no shared helper; duplicating the lookup risked drift and an unmarked API label in the German view.
- **Fix:** Exported `ActionLabel` from `components.tsx` and used it in both places.
- **Files modified:** `sources/ReactApp/src/features/hosting/components.tsx`, `ServerWorkspacePage.tsx`
- **Commit:** ac4c78f7

**2. [Rule 2 - AGENTS.md UI reuse] Further raw controls converted in touched files**
- **Found during:** Task 2
- **Issue:** AGENTS.md requires `ActionButton`/`ActionLink` in code that is touched; JobList retry/skip, the "show all" button, download anchors (page and logs panel) and the HostingError retry still used raw `className="button ..."`.
- **Fix:** Converted them (`ActionAnchor` for the downloads). Behavior is unchanged.
- **Commit:** ac4c78f7

**3. [Plan interpretation] Logs panel requests the confirmation itself**
- The plan said the logs tab calls `request` with source workspace-action and keeps the "panel receives the action" shape; the panel now owns the `request` call rather than an `onCollect` callback, which removes the last action plumbing from the view.

**Total deviations:** 3 (2 Rule 2, 1 interpretation). **Impact:** none on behavior or the plan's truths.

## Carry-over for Plan 61-11

The `?action=` deep link is intentionally not handled by the page now. Plan 61-11 must add the effect that calls `request({ serverId, actionId, source: "deep-link" })` once `server.data` exists (as noted in the 61-08 hand-off). `deploymentStatusKey` in `model.ts` and the old `serverMessageKey` use in `ServerCard` remain until Plan 61-12.

## Known Stubs

None.

## Threat Flags

None. No new network endpoints, auth paths or trust-boundary schema changes; T-61-24 and T-61-25 are mitigated as planned (graceful primary action, `confirmVariantFor` variant, API prose as React text in `ApiText`).

## Self-Check: PASSED

- Created files exist: `ActionUnavailableReason.tsx`, `workspace-header.test.tsx`
- Commits `580c5ae7` and `ac4c78f7` are on `gsd/v2.1-milestone`; `commits: 2` measured from the ledger

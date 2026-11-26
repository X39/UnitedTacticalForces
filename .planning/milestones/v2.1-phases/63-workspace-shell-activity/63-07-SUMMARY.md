---
phase: 63-workspace-shell-activity
plan: 07
subsystem: ui
tags: [react, job-progress, failure-panel, cost-stop, vitest, i18n, accessibility]

requires:
  - phase: 63-workspace-shell-activity
    provides: activeJobs and isActiveJobStatus (63-03), costStopAction and the workspace-failure confirmation source (63-04), jobKindLabelKey, actorFor and activityEntryTo (63-01), all Server_Progress_* and Server_Failure_* keys (63-01)
  - phase: 62-fleet-quick-actions
    provides: ActionButton, ActionLink, CostIndicator, fleetServer and fleetAction fixtures
provides:
  - deriveJobProgress, an exhaustive stage model over job kind, status, stage and worker commands that never returns a percentage
  - JobProgressStrip with steps, retry and generic modes, queue line and activity link
  - failedStepLabelKey, deriveFailure and costSentenceKey
  - JobFailurePanel with cost risk, next step, retry, skip, the shared cost-stop and the activity link
  - jobTransitionAnnouncement for the shell live region
  - job-status.css, token-only styles for both components
affects: [63-11, 63-12, 69]

requirements-completed: [WSP-04, WSP-05, CST-02]

plan_head_before: ea5d92b3112b739c56d1717b707a395f996bb4ab
plan_head_after: 7584efda8c06ee3edf8db4cfdbae1eb7153715c6

actuals:
  tokens: 17500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Pure total derivation (deriveJobProgress) tested over the full product of generated union values, with Record-keyed lists that fail the typecheck when the API adds a value"
    - "Untrusted stage and kind strings are only compared with startsWith, equality or switch/Map defaults, never used as object keys"
    - "A retry or failure surface delegates the cost-stop to the shared confirmation with its own source and never executes anything itself"
    - "An always-mounted empty role=alert slot cancels its grid gap with a negative margin instead of display none, so the alert is still announced"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/jobFailure.ts
    - sources/ReactApp/src/components/server/JobProgressStrip.tsx
    - sources/ReactApp/src/components/server/JobFailurePanel.tsx
    - sources/ReactApp/src/components/server/job-status.css
    - sources/ReactApp/tests/hosting/job-progress-strip.test.tsx
    - sources/ReactApp/tests/hosting/job-failure.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/jobProgress.ts
    - sources/ReactApp/tests/hosting/job-progress.test.ts

key-decisions:
  - "A derived step that is not in the kind's step list (for example Stop with WaitingForWorker) falls back to generic mode instead of inventing a step"
  - "The progress strip's retry mode shows the shut-down button only for a job that is not itself a Stop or Terminate and while cost is live or maybe; a cost-stop job shows the automatic-retry line instead"
  - "failedStepLabelKey reuses the stage and command rows of the derivation and ignores the status rows, falling back to Working"
  - "The actor name is placed inside a bdi element by splitting the translated sentence around an invisible marker, so the translation key stays a single sentence"
  - "The visually hidden state words reuse the global sr-only utility instead of a second copy in job-status.css"

patterns-established:
  - "Step items render a literal aria-current=step element for the current step and a plain item otherwise"
  - "deriveFailure owns the next-step order (stop billing, retry, ask owner, details) so the panel only renders it"

coverage:
  - id: D1
    description: "A running job shows its named stage with done, current and upcoming steps (aria-current=step plus a hidden state word), start or request time, requester, lag note and a details link, derived only from API data and without any percent sign or progressbar"
    requirement: WSP-04
    verification:
      - kind: unit
        ref: "tests/hosting/job-progress-strip.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/hosting/job-progress.test.ts#deriveJobProgress"
        status: pass
    human_judgment: false
  - id: D2
    description: "deriveJobProgress is total over 8 kinds x 8 statuses x 6 stages x 3 command sets; RetryPending always gives retry mode, Queued always the queued step, unknown stages and kinds give generic mode with the raw stage"
    requirement: WSP-04
    verification:
      - kind: unit
        ref: "tests/hosting/job-progress.test.ts#deriveJobProgress exhaustiveness"
        status: pass
    human_judgment: false
  - id: D3
    description: "Retry mode shows the warning tone, Waiting to retry, the retry body, the failure reason in ApiText, the cost sentence and either the workspace-failure shut-down button or the wait line; more than one active job adds the singular or plural queue line"
    requirement: WSP-04
    verification:
      - kind: unit
        ref: "tests/hosting/job-progress-strip.test.tsx#retry mode"
        status: pass
      - kind: unit
        ref: "tests/hosting/job-progress-strip.test.tsx#queue line"
        status: pass
    human_judgment: false
  - id: D4
    description: "The failure panel appears only when no job is active and the newest terminal job failed, states what failed, the cost risk and the next step in the specified order, and never renders for an active or RetryPending job"
    requirement: WSP-05
    verification:
      - kind: unit
        ref: "tests/hosting/job-failure.test.tsx#deriveFailure"
        status: pass
      - kind: unit
        ref: "tests/hosting/job-failure.test.tsx#JobFailurePanel"
        status: pass
    human_judgment: false
  - id: D5
    description: "Retry and skip call the page callbacks with the job id, are aria-disabled with Sending your request while busy, a failed request shows in a role=alert element, and the viewer without the operate permission is told to ask the owner with no retry or skip button"
    requirement: WSP-05
    verification:
      - kind: unit
        ref: "tests/hosting/job-failure.test.tsx#JobFailurePanel"
        status: pass
    human_judgment: false
  - id: D6
    description: "The failure panel and the retry-mode strip request the shared cost confirmation only with source workspace-failure; a terminate-only server keeps Shut down without saving"
    requirement: CST-02
    verification:
      - kind: unit
        ref: "tests/hosting/job-failure.test.tsx#requests the shared confirmation for the stop action with the workspace-failure source"
        status: pass
      - kind: unit
        ref: "tests/hosting/job-failure.test.tsx#keeps the honest label when the only cost-stop is terminate"
        status: pass
    human_judgment: false
  - id: D7
    description: "jobTransitionAnnouncement reports an active-to-Succeeded or Failed transition (newest wins) and never the first snapshot"
    requirement: WSP-05
    verification:
      - kind: unit
        ref: "tests/hosting/job-progress.test.ts#jobTransitionAnnouncement"
        status: pass
    human_judgment: false
  - id: D8
    description: "Layout of the strip steps (row above 800px, vertical below), the stacked failure facts and full-width actions at 520px and below, and the tone colours need a real browser; the style guard only proves the rules use tokens"
    requirement: WSP-04
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
    human_judgment: true
    rationale: "Responsive layout and visual tone of the two components at 320 to 1280 px are asserted in the plan 63-12 browser walk; this plan supplies the rules and the token guard."

duration: 14min
completed: 2026-11-19
status: complete
commits: 3
---

# Phase 63 Plan 07: Job Progress Strip and Failure Panel Summary

**An exhaustive percent-free stage model with a progress strip (steps, retry, generic and queue modes) and a failure panel that states what failed, whether the server may still cost money and one next step, with gated retry and skip and the shared cost-stop.**

## Performance

- **Duration:** about 14 min (start time inferred from the previous plan's last commit)
- **Completed:** 2026-11-19T16:21:30Z
- **Tasks:** 3
- **Files modified:** 8 (6 created, 2 modified)

## Accomplishments

- `deriveJobProgress` turns job kind, status, stage and worker command statuses into a step list with a current index, retry mode or generic mode. It never reads `progressPercent`, never throws for unknown kinds or stages, and a product test covers 8 kinds x 8 statuses x 6 stages x 3 command sets (1152 combinations).
- `JobProgressStrip` renders the newest active job with a decorative kicker, the action label and current step as two elements, an ordered step list with hidden state words and `aria-current="step"`, a relative start or request time refreshed every 15 s, the requester in a `bdi`, the honest 30-second lag note, a zero-one-many queue line and the activity link. Retry mode switches to the warning tone and shows the reason, the cost sentence and either the `workspace-failure` shut-down button or the automatic-retry line.
- `deriveFailure` shows a failure only when no job is active and the newest terminal job is `Failed`, and picks the next step in the specified order (stop billing, retry, ask the owner, details). `JobFailurePanel` renders what failed, the cost chip and sentence, the next step and the action row (cost-stop first, retry, skip, activity link) with a `role="alert"` slot for a failed request.
- `jobTransitionAnnouncement` reports the newest active-to-final transition between two snapshots and stays silent at load, ready for the shell live region in plan 63-11.

## Task Commits

1. **Task 1: Tracer - a running Start job shows its named stage** - `bacb14a8` (feat)
2. **Task 2: Failure panel with cost risk and next step** - `9aed9fa3` (feat)
3. **Task 3: Exhaustive stage model, retry mode, queue line and transition announcements** - `7584efda` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/jobProgress.ts` - step model, `deriveJobProgress`, `progressStepLabelKey`, `failedStepLabelKey`, `jobTransitionAnnouncement`
- `sources/ReactApp/src/features/hosting/jobFailure.ts` - `deriveFailure`, `costSentenceKey`, next-step order
- `sources/ReactApp/src/components/server/JobProgressStrip.tsx` - progress strip with steps, retry and generic modes and queue line
- `sources/ReactApp/src/components/server/JobFailurePanel.tsx` - failure panel with facts and actions
- `sources/ReactApp/src/components/server/job-status.css` - token-only styles for both components
- `sources/ReactApp/tests/hosting/job-progress.test.ts`, `job-progress-strip.test.tsx`, `job-failure.test.tsx` - model, strip and panel tests

## Decisions Made

- A derived step outside the kind's list falls back to generic mode (Stop with WaitingForWorker), so no step is ever invented.
- Retry mode offers the shut-down button only for a job that is not itself a Stop or Terminate and only while cost is live or maybe; cost-stop jobs show the automatic-retry line.
- The requester name sits in a `bdi` by splitting the translated sentence around an invisible marker built with `String.fromCodePoint`, keeping the single-sentence translation key.
- Hidden state words reuse the global `sr-only` utility rather than a second copy in `job-status.css`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Invisible separator was written as a literal character**
- **Found during:** Task 3 review of `JobProgressStrip.tsx`
- **Issue:** The marker constant created in Task 1 was stored as a raw invisible U+2063 character in the source, which is unreadable and trips injection scanners.
- **Fix:** Replaced it with `String.fromCodePoint(0x2063)`.
- **Files modified:** `sources/ReactApp/src/components/server/JobProgressStrip.tsx`
- **Verification:** strip tests and lint pass.
- **Committed in:** `7584efda` (Task 3 commit)

**2. [Plan wording] Visually hidden utility not added to job-status.css**
- **Found during:** Task 1
- **Issue:** The plan asked for a visually hidden utility in `job-status.css`; the repository already ships the global `.sr-only` that `CopyButton` uses.
- **Fix:** Reused `.sr-only` to avoid a duplicate utility.
- **Files modified:** none beyond the component
- **Committed in:** `bacb14a8` (Task 1 commit)

---

**Total deviations:** 2 (1 auto-fixed bug, 1 deliberate reuse)
**Impact on plan:** No scope change.

## Issues Encountered

- `npm run format:check` still reports `tests/hosting/arma-placement.test.tsx`, `fleet-fixtures.test.ts` and `fleet-page.test.tsx`. These are untouched by this plan and are already recorded in `deferred-items.md` (63-01); not fixed here.
- `rerender` from Testing Library drops the router and confirmation providers set up by `renderWithProviders`, so the request-error cases render separately instead of rerendering.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. Both components are pure presentation over props; plan 63-11 places them in the shell and wires the retry and skip mutation, which is why `onRetry` and `onSkip` are callbacks here.

## Threat Flags

None. No new network endpoint, auth path or trust boundary was introduced; API prose renders only as React text inside `ApiText` (T-63-14), stages are never object keys (T-63-15), and cost wording comes only from `deriveServerState` (T-63-16).

## Next Phase Readiness

- Plan 63-11 can place `JobProgressStrip` and `JobFailurePanel` in the shell, wire `hostingService.retryJob` and `skipJob` to `onRetry` and `onSkip` (`busy`, `requestFailed`), and use `jobTransitionAnnouncement` with the `Server_Workspace_Announce_*` keys.
- Plan 63-12 owns the browser check of layout and tone at the 320 to 1280 px widths.

## Self-Check: PASSED

All created files exist and all three task commits are ancestors of HEAD.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*

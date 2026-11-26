---
phase: 63-workspace-shell-activity
verified: 2026-11-22T20:30:00Z
status: passed
score: 5/5 must-haves verified
covered_files:
  - ".planning/phases/63-workspace-shell-activity/63-01-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-01-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-02-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-02-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-03-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-03-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-04-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-04-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-05-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-05-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-06-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-06-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-07-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-07-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-08-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-08-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-09-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-09-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-10-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-10-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-11-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-11-SUMMARY.md"
  - ".planning/phases/63-workspace-shell-activity/63-12-PLAN.md"
  - ".planning/phases/63-workspace-shell-activity/63-12-SUMMARY.md"
  - "sources/Api/DTO/HostingOperationDto.cs"
  - "sources/Api/DTO/LifecycleJobDto.cs"
  - "sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs"
  - "sources/ReactApp/src/components/server/ActivityEntry.tsx"
  - "sources/ReactApp/src/components/server/ActivityTimeline.tsx"
  - "sources/ReactApp/src/components/server/CostStrip.tsx"
  - "sources/ReactApp/src/components/server/JobFailurePanel.tsx"
  - "sources/ReactApp/src/components/server/JobProgressStrip.tsx"
  - "sources/ReactApp/src/components/server/LiveBadge.tsx"
  - "sources/ReactApp/src/components/server/ServerEndpoint.tsx"
  - "sources/ReactApp/src/components/server/WorkspaceHeader.tsx"
  - "sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx"
  - "sources/ReactApp/src/features/hosting/WorkspaceShell.tsx"
  - "sources/ReactApp/src/features/hosting/activityModel.ts"
  - "sources/ReactApp/src/features/hosting/jobFailure.ts"
  - "sources/ReactApp/src/features/hosting/jobProgress.ts"
  - "sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx"
  - "sources/ReactApp/src/features/hosting/sections/MachineSection.tsx"
  - "sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx"
  - "sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx"
  - "sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts"
  - "sources/ReactApp/src/features/hosting/workspaceFreshness.ts"
  - "sources/ReactApp/src/features/hosting/workspaceSections.ts"
  - "sources/ReactApp/src/locales/de.json"
  - "sources/ReactApp/src/locales/en.json"
  - "sources/ReactApp/tests/e2e/hosting-workspace.spec.ts"
covered_digest: "v4:sha256:14844ca209b6e836e087b37fcb02ca470f4eba111876a85ce945503a060857f8"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: passed
  previous_score: 8/8
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 63: Workspace Shell & Activity Verification Report

**Phase Goal:** Opening a server gives a persistent header with state, owner, cost, endpoint, and next action, connected section navigation, honest progress and failure feedback, and one readable activity timeline. The shell reserves named slots (the accrued figure in the cost strip, a metrics area on Overview, an automation settings area) that Phases 66-68 fill.
**Verified:** 2026-11-22T20:30:00Z
**Status:** passed
**Re-verification:** Yes. The previous report went stale because Phases 64-67 changed covered files. Every must-have was re-checked against the current code.

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Persistent header with status, owner, cost strip (owner, hourly and monthly rate, costing now), endpoint with copy and the primary action, visible in every section | VERIFIED | `WorkspaceShell.tsx` renders `WorkspaceHeader`, `CostStrip` and `ServerEndpoint` outside the section switch. The header is sticky (`workspace-shell.css`), holds the only h1, `ServerStateSummary` and the primary action through `primaryServerAction` and the shared cost confirmation. `CostStrip` rows come from `serverCostFacts` and `CostIndicator`; a missing estimate reads "No estimate". `ServerEndpoint` uses `CopyButton`. |
| 2 | Deep-linkable section navigation, breadcrumbs, retired `?tab=` opens Overview, no clipping at 320/390 px | VERIFIED | `workspaceSections.ts` is a closed registry with an exact-match resolver that falls back to Overview and never indexes by the raw value. `SectionNavigation` is a `nav` with router `Link`s and `aria-current=page`, with no tab role. `navigation.css` wraps links and has no overflow rule. The shell renders `Breadcrumbs` for Servers > server > section. The 320/390 geometry is covered by `tests/e2e/hosting-workspace.spec.ts`. |
| 3 | Progress strip shows the current stage during a job; a failed job shows what failed, cost risk and next step | VERIFIED | `JobProgressStrip` and `jobProgress.ts` (stages, `hasActiveJob`, fallback poll). `deriveFailure` in `jobFailure.ts` uses `deriveServerState(...).cost`, so a server that may still cost is never described as not costing. Next step order is stopBilling, retry, askOwner, details. `ServerWorkspacePage` passes retry and skip handlers and mounts `useWorkspaceLiveUpdates`. |
| 4 | Cost-stop in plain words ("Shut down and stop billing") with one vocabulary | VERIFIED | `Server_Action_Stop_Label` is "Shut down and stop billing". `costStopAction` is shared by the header, failure panel, progress strip and `MachineSection` (source `workspace-machine`). `workspace-vocabulary.test.ts` passes. |
| 5 | One activity timeline merging jobs, logs and audit in readable language, with a visible freshness indicator | VERIFIED | `activityModel.ts` merges jobs, worker commands and operations. The API projects `RequestedByName` (`LifecycleJobDto`) and `ActorName` (`HostingOperationDto`) in `GameServerProjectionService` (`ProjectActorName`, hidden members masked). `ActivitySection` renders `ActivityTimeline` and `LiveBadge`, which uses `deriveFreshness`. |

**Score:** 5/5 truths verified (0 present, behavior-unverified)

### Reserved slots (goal sentence, Phases 66-68)

| Slot | Status | Evidence |
|------|--------|----------|
| Accrued figure in the cost strip | VERIFIED, filled by Phase 66 | `CostStrip` keeps the `data-slot="accrued-cost"` element wrapping `AccruedCost`. The shell structure is intact. |
| Metrics area on Overview | VERIFIED, filled by Phase 67 | `OverviewSection` renders `ServerMetricsPanel` beside the details. |
| Automation settings area | VERIFIED, still reserved | `SettingsSection` renders `SlotPlaceholder slot="automation"` with honest unavailable text. Phase 68 is not yet complete, so a placeholder is correct. |

### Required Artifacts

All 30 implementation files in `covered_files` exist and are substantive (29 to 3697 lines). Each was last touched by Phase 63 to 67 feature commits. None is a stub.

### Key Link Verification

| From | To | Status |
|------|----|--------|
| `ServerWorkspacePage` | `WorkspaceShell` with section from `workspaceSectionFromSearch(search.get("tab"))`, jobs, operations, retry and skip, `useWorkspaceLiveUpdates` | WIRED |
| `WorkspaceShell` | Header, CostStrip, ServerEndpoint, JobProgressStrip, JobFailurePanel, SectionNavigation, five sections | WIRED |
| Header and Machine cost-stop buttons | `useCostConfirmation().request` with sources `workspace-header` and `workspace-machine` | WIRED |
| `ActivitySection` | `buildActivityEntries`, `LiveBadge` and freshness | WIRED |
| API projection | `LifecycleJobDto.RequestedByName` and `HostingOperationDto.ActorName` | WIRED |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Phase 63 unit and component tests | `npx vitest run` on 10 phase 63 files (shell, sections, cost-strip, job-failure, job-progress-strip, vocabulary, activity-model, freshness, header, breadcrumbs) | 10 files, 229 tests passed | PASS |
| Full regression (gathered 2026-11-22, not re-run here) | `dotnet test` (Api 1413, CloudWorker 364, Ui 2, parser) and ReactApp lint, typecheck, test (169 files, 2313 tests) | green | PASS |

Probe execution: none declared for this phase.

### Requirements Coverage

| Requirement | Source Plan(s) | Status | Evidence |
|-------------|----------------|--------|----------|
| WSP-01 | 63-06, 63-11 | SATISFIED | Header, cost strip, endpoint with copy, primary action |
| WSP-02 | 63-01, 63-05, 63-10, 63-11, 63-12 | SATISFIED | Section registry, deep links, `?tab=` fallback to Overview |
| WSP-03 | 63-05, 63-12 | SATISFIED | Wrapping nav with no overflow rule; e2e geometry |
| WSP-04 | 63-07, 63-11 | SATISFIED | `JobProgressStrip` |
| WSP-05 | 63-07, 63-11 | SATISFIED | `JobFailurePanel` and `deriveFailure` |
| WSP-06 | 63-05, 63-11 | SATISFIED | `Breadcrumbs` in the shell |
| CST-01 | 63-04, 63-06 | SATISFIED | `CostStrip` with `serverCostFacts` |
| CST-02 | 63-01, 63-04, 63-06, 63-07, 63-10 | SATISFIED | Shared `costStopAction` and one vocabulary |
| ACT-01 | 63-02, 63-08, 63-09 | SATISFIED | Merged model, actor names, timeline |
| ACT-02 | 63-03, 63-09, 63-11 | SATISFIED | `LiveBadge` and `deriveFreshness` |

All 10 requirement IDs declared across the 12 plans are accounted for and marked Complete in REQUIREMENTS.md. There are no orphans: REQUIREMENTS.md maps no other ID to Phase 63. CST-03 to CST-06 belong to Phase 66.

### Anti-Patterns Found

No blocking debt markers (TBD, FIXME, XXX) found in the covered implementation files. `deferred-items.md` records minor cleanup (unused `.next-hosting-job*` CSS, unused locale key `React_ServerWorkspace_ExternalStorageDeleted`, hard-coded `/h` in `ServerRuntimePanel`). These are warnings only; none affects a requirement.

### Human Verification

`63-UAT.md` has status complete. Tests 1 and 2 (real-browser shell review, German at 320/390 px) passed. Tests 3 (German at 320 px without Google Fonts) and 4 (about 5,000-line output in the timeline) were skipped by the user ("skip all of these tests"); they are not marked as "Deferred follow-up". Both are design-acceptability and large-output edge checks that do not block a requirement, and the user explicitly waived them. They are recorded as accepted residual risk, not a failure.

### Gaps Summary

None. The phase goal holds against the current codebase. Later phases filled the accrued-cost and metrics slots without removing the shell structure. The automation slot remains a reserved, honest placeholder for Phase 68.

---

_Verified: 2026-11-22T20:30:00Z_
_Verifier: Claude (gsd-verifier)_

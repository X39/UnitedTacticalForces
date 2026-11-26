---
phase: 68-auto-stop-schedules
verified: 2026-11-25T11:45:00Z
status: passed
score: 7/7 must-haves verified
covered_files: [
    ".planning/phases/68-auto-stop-schedules/68-01-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-01-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-02-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-02-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-03-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-03-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-04-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-04-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-05-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-05-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-06-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-06-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-07-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-07-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-08-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-08-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-09-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-09-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-10-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-10-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-11-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-11-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-12-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-12-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-13-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-13-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-14-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-14-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-15-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-15-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-16-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-16-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-17-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-17-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-18-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-18-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-19-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-19-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-20-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-20-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-21-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-21-SUMMARY.md",
    ".planning/phases/68-auto-stop-schedules/68-22-PLAN.md",
    ".planning/phases/68-auto-stop-schedules/68-22-SUMMARY.md",
    "sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs",
    "sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs",
    "sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs",
    "sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs",
    "sources/Database/Hosting/ServerScheduleRun.cs",
    "sources/HostingContracts/EServerScheduleRunOutcome.cs",
    "sources/ReactApp/src/components/server/ScheduleRuleFields.tsx",
    "sources/ReactApp/src/features/hosting/automationModel.ts",
    "sources/ReactApp/src/locales/de.json",
    "sources/ReactApp/src/locales/en.json",
    "sources/ReactApp/src/styles.css",
    "sources/ReactApp/tests/e2e/hosting-workspace.spec.ts",
    "sources/ReactApp/tests/styles/button-wrap.test.ts",
    "tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs",
    "tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs"
]
covered_digest: "v4:sha256:f8fa37729e4efed2e88538ecf3000758a4ebddd2cf19b43bc17d31ac74caa497"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 5/5
  gaps_closed:
    - "G-68-1: the workspace header button 'Herunterfahren und Abrechnung beenden' wraps its label at 320 px"
    - "G-68-3: a scheduled start that comes due while a stop of the same server is still processed is executed after the stop is fully processed"
  gaps_remaining: []
  regressions: []
---

# Phase 68: Auto-Stop and Schedules Verification Report

**Phase Goal:** A server's owner can make it stop itself (when idle, after a maximum runtime, or on a schedule) and start on a schedule, with a warning beforehand and the same verified cost-stop as a manual stop. The control plane owns schedules and timers.
**Verified:** 2026-11-25T11:45:00Z
**Status:** passed
**Re-verification:** Yes, after gap closure plans 68-20, 68-21 and 68-22. The previous report was `human_needed` with 5/5 truths.

## Goal Achievement

The goal is achieved in the codebase, and both recorded UAT gaps are closed. I re-checked the five roadmap success criteria with a quick regression pass, since the earlier report verified them and the gap plans only touch the schedule runner, the shared button and the last-run copy. I gave the two gap truths the full three-level check. I read the code and ran the focused tests and browser specs myself.

The previous `human_needed` items are all resolved:

| Previous item | Resolution |
|---|---|
| Visual review of the Automation panel | Done in UAT test 1. The only defect named as a gap of this phase was the non-wrapping shutdown button (G-68-1). It is fixed and proven below. The remaining polish is scoped to Phase 70 (see Deferred Items). |
| CR-01 (consent tied to the cost owner) | The user answered "pass" in UAT test 2. |
| WR-01 (start skipped while the server is busy) | The user rejected it in UAT test 3. It is G-68-3, now fixed. |

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The owner can set an idle shutdown and a maximum runtime. A non-owner sees the controls disabled with the reason. (SC1) | ✓ VERIFIED (regression check) | No file of this feature was touched by the gap plans. The `automation-*` Vitest files are inside the 215 tests that passed in my run. The previous report's evidence stands. |
| 2 | The owner can schedule start and stop times, and the server starts and stops at those times. (SC2) | ✓ VERIFIED | `ServerScheduleRunner` now also covers a start that meets a stop in flight (truth 7). `ScheduledRunTests` and `AutomationRestartTests` passed in my run. |
| 3 | The owner is warned shortly before an automatic stop through the Phase 66 alert channels. (SC3) | ✓ VERIFIED (regression check) | Alert writer and notice files are untouched since the previous report. |
| 4 | An automatic stop is a lifecycle job with an audit entry and completes only after verified non-billable teardown. (SC4) | ✓ VERIFIED (regression check) | Untouched. The new waiting path never queues, escalates or terminates anything for a failed stop (`HasFailedStopAsync` only closes the row, ServerScheduleRunner.cs:597-601). |
| 5 | Schedules and idle timers survive an API restart, and a stop that came due while the API was down still happens exactly once. (SC5) | ✓ VERIFIED | The waiting state is a PostgreSQL run row. `LoadWaitingAsync` (ServerScheduleRunner.cs:204-215) re-collects it independent of the watermark. `SelectServerIdsAsync` (ServerAutomationEvaluator.cs:165-171) selects every server with rules, including one whose deployment has already stopped. `Restart_ScheduledStartWaitingForAStop_StartsExactlyOnceOnTheNextProcess` passed. |
| 6 | G-68-1: In German at 320 px the header button "Herunterfahren und Abrechnung beenden" wraps its label inside its box. The cost confirmation button does the same. The shared `.button` wraps, the page-local overrides are gone, and the layout gates stay green. | ✓ VERIFIED | `styles.css` `.button` rule: `white-space: inherit` (line 1983) and `overflow-wrap: break-word`. The count of `white-space: nowrap` in that rule is 0. The topbar one-line rule (4404-4405) is kept. The hero rule keeps only `width: 100%`. `cost-alerts.css` and `cost-alert-settings.css` no longer carry the `.button` overrides. The `.fleet-card .button`, `.button.filter-chip` and `.fleet-live-notice` rules lost their wrap declarations but keep their other declarations. No css file contains the "never wraps" legacy comments. I ran the Playwright test "the German cost-stop label wraps inside the header button and the confirmation button" at 320x568 and it passed. The spec file also passed (13/13). The e2e gates for fleet, cost alerts, public shell and automation passed (81 passed, 36 skipped, 0 failed). `button-wrap.test.ts` passed. `ActionControl.tsx`, `sources/WebApp` and `sources/Database/Migrations` have no diff against `dbe8d919`. The summary records RED (scrollWidth 266 vs clientWidth 250) and GREEN. |
| 7 | G-68-3: A scheduled start that comes due while a Stop or Terminate of the same server is queued, running, retry-pending or waiting for its worker is stored as `WaitingForStop` and re-evaluated every pass. After the stop is fully processed it queues exactly one ordinary Start with the schedule trigger, even beyond the 15 minute grace window. Consent and cost are re-checked at that moment. A wait that ends without a start closes as `SkippedStopUnfinished`, never `SkippedMissed`, and a failed stop is never escalated. A running server still records a final `SkippedRunning`. | ✓ VERIFIED | `EServerScheduleRunOutcome` carries `WaitingForStop = 9` and `SkippedStopUnfinished = 10`. `ScheduledRunLedger` has `FindAsync`, `AddWaiting` and `RecordSkip`. In `EvaluateStartSkipAsync` (ServerScheduleRunner.cs:548-640) the missed check applies only to a first evaluation (`context.Open is null`). The bound checks apply only to a waiting row (rule `EffectiveFrom` later than the occurrence, or a next occurrence of the same rule in the zone). Then come consent, `IsStopInFlightAsync`, `HasFailedStopAsync`, running, busy and cost. `TryStartAsync` (362-470) runs under `ServerRowLock`, adds a waiting row once, leaves an existing one untouched, and turns the waiting row into `Started` in the same transaction as `EnqueueAutomatedActionAsync`, with `WaitedForStop=true` in the audit note. `LoadStartContextAsync` returns null for any non-waiting row, so a second pass or instance does nothing. `IsStopInFlightAsync` matches the plan, including the short window after the deployment resolves and the due pending automatic stop. No migration was added and `GameServerLifecycleService.cs` has no diff. Tests passed in my run: 119/119 focused API tests (`ScheduledRunTests`, `AutomationRestartTests`, `FleetChangeInterceptorTests`, `ServerAutomationProjectionTests`, `ServerAutomationOptionsTests`). They include `StartRule_WhileAStopJobIsInFlight_WaitsAndStartsOnceAfterTheStopFinished`, `StartRule_WhileAStopOrTerminateIsActive_Waits`, `StartRule_WhileTheResolvedStopJobIsStillCompleting_WaitsThenStartsOnce`, `StartRule_WaitingAndRewoundWatermark_RepeatedPassesStartOnce` and the restart test. 22 `StartRule_*` tests exist (the plan required at least 21). |

**Score:** 7/7 truths verified (0 present, behavior-unverified)

### Last-run copy and live frame (plans 68-21 Task 3 and 68-22)

| Item | Status | Evidence |
|---|---|---|
| The OpenAPI snapshot and generated contract carry both new outcomes | ✓ VERIFIED | `WaitingForStop` appears 2 times in `utf-api.json` and `SkippedStopUnfinished` once in `api.ts`. The generated file's content matches the committed one (only line endings differ after a regeneration). |
| The fleet interceptor signals a schedule run row that was added or whose outcome changed | ✓ VERIFIED | `Entries<ServerScheduleRun>()` loop at FleetChangeInterceptor.cs:290. `ScheduleRunAddAndOutcomeChange_NotifyServer_RecordedAtAlone_StaysSilent` passed. |
| The projection reports a waiting start as `WaitingForStop` | ✓ VERIFIED | `LastRun_ShowsAWaitingStartAsWaitingForStop` passed. |
| The UI shows the waiting and unfinished-stop sentences in EN and DE, mapped to fixed keys | ✓ VERIFIED | `automationModel.ts:521-537` and `ScheduleRuleFields.tsx:32-33` map both outcomes. The `en.json` and `de.json` texts match the plan word for word. Vitest: `tests/styles`, `automation-schedules`, `automation-model` and `server-keys` all passed (215 tests). Unknown codes still fall back to the plain sentence. |

### Deferred Items

No later-phase success criterion covers a failed truth, so there is no `deferred:` frontmatter. These follow-ups are recorded and are not gaps:

- Deferred follow-up: real worker stop-then-start at the milestone end (68-21 summary). The seeded stack cannot produce a start that meets a stop in flight, and the API tests cover the window deterministically, including the restart.
- Deferred follow-up: a seeded-stack re-check of the German header button at 320 px at the milestone end (68-20 plan, user preference). Real-browser Playwright coverage of the button already passes, so this is a re-confirmation and not the only evidence.
- Deferred follow-up: an automatic stop of a real worker on a real Hetzner machine, and completion of a scheduled start with a real worker (UAT tests 4 and 5, skipped by the user).
- Phase 70 (UI Polishing, roadmap): the plain radio buttons and checkboxes, the amount of scrolling to reach the Automation section, and the weekday grid showing two days per row at 320 px (UI spec says four). The Phase 70 goal text names these items and cites this UAT.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `sources/ReactApp/src/styles.css` | Shared `.button` wraps | ✓ VERIFIED | `white-space: inherit` and `overflow-wrap: break-word` in the rule. |
| `sources/ReactApp/tests/styles/button-wrap.test.ts` | Guard against single-line labels and local overrides | ✓ VERIFIED | Passed, with negative in-memory samples. |
| `sources/ReactApp/tests/e2e/hosting-workspace.spec.ts` | 320 px German check | ✓ VERIFIED | Contains "label wraps inside". Passed. |
| `sources/HostingContracts/EServerScheduleRunOutcome.cs` | Two new values | ✓ VERIFIED | Values 9 and 10 with documentation. |
| `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs`, `ScheduledRunLedger.cs` | Waiting state, re-collection, closing | ✓ VERIFIED | Real transactional logic, read in full above. |
| `sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs` | Run-row frames | ✓ VERIFIED | |
| `sources/ReactApp/openapi/utf-api.json`, `src/generated/api.ts` | Regenerated contract | ✓ VERIFIED | Both contain the new values. |
| `automationModel.ts`, `ScheduleRuleFields.tsx`, `en.json`, `de.json` | Mapping and copy | ✓ VERIFIED | |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `ServerScheduleRunner` | `GameServerLifecycleService.EnqueueAutomatedActionAsync` | The late Start is queued in the same transaction that turns the waiting row into `Started` | WIRED (ServerScheduleRunner.cs:447-462) |
| `ServerScheduleRunner` | `ServerScheduleRun` unique `(ScheduleFk, OccurrenceAt)` row | `WaitingForStop` is the only open outcome | WIRED |
| `FleetChangeInterceptor` | `ServerScheduleRun` | `Entries<ServerScheduleRun>` | WIRED |
| `ActionControl.tsx` | `styles.css` | The shared `button` class | WIRED (no prop change) |
| `ScheduleRuleFields.tsx` | `automationModel.ts` | `Record<LastRunKey, TranslationKey>`, so a missing key fails the type check | WIRED |
| `ServerAutomationEvaluator` | `ServerScheduleRunner.ProcessDueAsync` | Every server with rules is selected, so a waiting row is re-evaluated after the stop resolves | WIRED (lines 119 and 165-171) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Rule last-run line | `ServerScheduleLastRunDto.outcome` | `ServerAutomationProjector.ProjectLastRunAsync` from the latest `ServerScheduleRun` row, including waiting rows | Yes | FLOWING |
| Waiting-row re-collection | `LoadWaitingAsync` | A PostgreSQL query of run rows with outcome `WaitingForStop` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Schedule runner, restart, interceptor, projection and options tests | `dotnet test tests/Api.Tests/Api.Tests.csproj --artifacts-path .gsd-verify-artifacts --filter "...ScheduledRunTests\|AutomationRestartTests\|FleetChangeInterceptorTests\|ServerAutomationProjectionTests\|ServerAutomationOptionsTests"` | 119 passed, 0 failed | ✓ PASS |
| Button guard, schedules rendering, model and key list | `npx vitest run tests/styles tests/hosting/automation-schedules.test.tsx tests/hosting/automation-model.test.ts tests/locales/server-keys.test.ts` | 215 passed, 0 failed | ✓ PASS |
| German cost-stop label at 320 px, header and dialog | `npm run test:e2e -- tests/e2e/hosting-workspace.spec.ts` | 13 passed | ✓ PASS |
| Layout gates after the override removal | `npm run test:e2e -- hosting-fleet, hosting-cost-alerts, public-shell, hosting-automation specs` | 81 passed, 36 skipped, 0 failed | ✓ PASS |
| Full-suite results | The orchestrator reports a solution build with 0 errors, `Api.Tests` 1726 passed, Vitest 2601 passed, and typecheck, lint and locales:check clean | Cited, not re-run | ✓ PASS (cited) |

The 36 skipped e2e tests are the spec files' own conditional skips, not tests I disabled. I deleted the temporary `.gsd-verify-artifacts` folder afterwards. My e2e build regenerated `api.ts` with different line endings only, and I restored it with `git checkout`, so the working tree matches its starting state. The Aspire stack was not touched.

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts.

### Requirements Coverage

Every ID appears in at least one PLAN frontmatter, and REQUIREMENTS.md maps no other ID to Phase 68, so there are no orphans. The traceability table shows AUT-01 to AUT-06 as Complete.

| Requirement | Source Plans (gap plans) | Status | Evidence |
|-------------|--------------------------|--------|----------|
| AUT-01 | earlier plans, 68-20 | ✓ SATISFIED | Unchanged from the previous report. |
| AUT-02 | earlier plans, 68-20 | ✓ SATISFIED | Unchanged. |
| AUT-03 | earlier plans, 68-20, 68-21, 68-22 | ✓ SATISFIED | Truth 7: a scheduled start runs after a stop in flight. The last-run copy exists in EN and DE. |
| AUT-04 | earlier plans | ✓ SATISFIED | Unchanged. |
| AUT-05 | earlier plans, 68-21 | ✓ SATISFIED | A failed stop is never escalated or restarted. A late start goes through the ordinary lifecycle path. Real-infrastructure teardown stays a deferred follow-up. |
| AUT-06 | earlier plans, 68-21 | ✓ SATISFIED | The restart test covers the waiting state. |

### Anti-Patterns Found

No unreferenced `TBD`, `FIXME` or `XXX` markers in the files I read. The incremental review (`68-REVIEW.md`: 0 critical, 3 warnings, 4 info) is open and advisory. None of it breaks a must-have.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ServerScheduleRunner.cs`, `ServerAutomationOptions.cs` | 556-579 | WR-01: a waiting start has no time bound. It is closed only by a failed stop, the rule's next occurrence, or a rule edit. For a weekly rule a long-stuck stop could release a start days after its instant. A waiting row of an archived server or an invalid zone is not re-scanned and could be released after unarchiving. | Warning | This is the adopted product decision in the plan: the user asked that the start still run after the stop. It is a robustness question, not a plan violation. I recommend a follow-up that bounds the wait by time (for example one hour). It is not blocking, since a stop normally finishes in minutes. |
| `en.json`, `de.json`, `ServerScheduleRunner.cs` | 3310; 566-578, 597-601 | WR-02: `SkippedStopUnfinished` covers four causes (stop failed, next occurrence took over, rule changed, a failed stop holds the deployment). The sentence "the shutdown before it did not finish" is wrong for "the rule was changed". The reason text never reaches the DTO. | Warning | A copy accuracy issue. The plan specified this sentence, so the code matches the plan. |
| `ServerScheduleRunner.cs`, `ScheduledRunLedger.cs` | 66, 165, 938; 212 | WR-03: four private members have no XML documentation, which AGENTS.md requires with no visibility carve-out | Warning | A repository standards violation. It has no effect on behavior. |
| various | - | IN-01 to IN-04 (a `DbUpdateException` is logged at Information for the waiting conversion, the active-status predicate is repeated in several places, no concurrent-runner test, no audit of nowrap-dependent `.button` callers) | Info | None blocks the goal. For IN-04 the e2e layout gates I ran pass, and the plan's audit table covers the white-space rules. A visual pass over every `.button` caller belongs to Phase 70 or the milestone-end sweep. |

### Human Verification Required

None. The two items that were the only way to verify a must-have (the visual review and the two product decisions) were resolved in UAT. The real-environment re-checks are Deferred follow-ups under the standing user decision.

### Gaps Summary

There are no gaps. Both UAT gaps are closed in code and proven by tests I ran. G-68-1 is proven by a real-browser measurement at 320 px in German. G-68-3 is proven by 119 focused API tests that include the end-to-end evaluator path, the grace window, the failed-stop closure and an API restart. All five roadmap success criteria and all six requirement IDs (AUT-01 to AUT-06) still hold. Open review warnings WR-01 to WR-03 are advisory. WR-01 (an unbounded wait) is the one worth a follow-up decision, and I list it here and not as a blocker because it implements the behavior the user asked for.

---

_Verified: 2026-11-25T11:45:00Z_
_Verifier: Claude (gsd-verifier)_

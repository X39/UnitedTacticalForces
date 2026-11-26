---
phase: 68-auto-stop-schedules
reviewed: 2026-11-25T00:00:00Z
depth: standard
files_reviewed: 33
files_reviewed_list:
  - sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs
  - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
  - sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs
  - sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs
  - sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs
  - sources/Database/Hosting/ServerScheduleRun.cs
  - sources/HostingContracts/EServerScheduleRunOutcome.cs
  - sources/ReactApp/src/components/server/ScheduleRuleFields.tsx
  - sources/ReactApp/src/components/server/cost-alerts.css
  - sources/ReactApp/src/components/server/fleet-card.css
  - sources/ReactApp/src/components/server/fleet-toolbar.css
  - sources/ReactApp/src/features/hosting/automationModel.ts
  - sources/ReactApp/src/features/hosting/cost-alert-settings.css
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/styles.css
  - sources/ReactApp/tests/e2e/event-authoring.spec.ts
  - sources/ReactApp/tests/e2e/event-detail.spec.ts
  - sources/ReactApp/tests/e2e/hosting-fleet.spec.ts
  - sources/ReactApp/tests/e2e/hosting-placement.spec.ts
  - sources/ReactApp/tests/e2e/hosting-workspace.spec.ts
  - sources/ReactApp/tests/e2e/public-shell.spec.ts
  - sources/ReactApp/tests/e2e/support/costMocks.ts
  - sources/ReactApp/tests/e2e/verein-kassenbuch.spec.ts
  - sources/ReactApp/tests/hosting/automation-model.test.ts
  - sources/ReactApp/tests/hosting/automation-schedules.test.tsx
  - sources/ReactApp/tests/locales/server-keys.test.ts
  - sources/ReactApp/tests/styles/button-wrap.test.ts
  - tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs
  - tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs
  - tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs
  - tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs
  - tests/Api.Tests/ServerAutomation/ServerAutomationProjectionTests.cs
findings:
  critical: 0
  warning: 3
  info: 4
  total: 7
status: issues_found
---

# Phase 68: Code Review Report

**Reviewed:** 2026-11-25
**Depth:** standard (incremental re-review of `ddb4f5a3..HEAD`, gap-closure work)
**Files Reviewed:** 33
**Status:** issues_found

## Summary

The waiting-start path is sound on the points you asked about. I traced exactly-once queueing, restart recovery, the
failed-stop scoping and the grace window, and found no way for the waiting path to queue two starts or lose the row.
The three warnings below concern its behavior and housekeeping, not its idempotency.

- **Exactly-once queueing.** The waiting row is the unique `(ScheduleFk, OccurrenceAt)` row. It is converted to
  `Started` and the start job is enqueued in one transaction under `ServerRowLock`. `LoadStartContextAsync` re-reads the
  row inside the lock and returns `null` for any non-waiting row. A second pass or a second instance therefore finds a
  final row and does nothing.
- **Restart recovery.** `LoadWaitingAsync` is independent of the watermark. The evaluator selects every server that has
  rules, so a waiting row is picked up after a restart or a lost watermark commit.
- **Failed-stop scoping.** `HasFailedStopAsync` is correctly limited to stop and terminate jobs whose deployment is
  still unresolved. A failed stop of an already-resolved deployment cannot block a later start.
- **Grace window.** The window applies only to a first evaluation (`context.Open is null`). A waiting row is bounded
  only by the rule's own next occurrence and by `EffectiveFrom`. That is by design, but it is the source of WR-01.
- **Earlier findings.** CR-01 and WR-05 (consent re-check at execution) are fixed by `23bdf118`. The re-check at
  `LifecycleJobExecutor.cs:3004-3022` is correct, and `RejectStartAsync` always throws, so the trailing `return` is
  harmless. WR-04 (undocumented private members) is still open, now as WR-03.
- **Necessity.** I did not read `necessity-review.md` for this pass, so no `necessity:` findings are emitted.

## Warnings

### WR-01: A waiting scheduled start has no upper bound and can fire hours or days after its instant

**File:** `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:556-579` (also `ServerAutomationOptions.cs:24-30`)
**Issue:** The grace window is skipped for any row that is already waiting (`context.Open is null && ...`). A waiting
row is closed only when the rule's next occurrence comes due, when the rule is edited, or when the stop fails. For a
weekly rule on one day, the "next occurrence" is seven days away. A stop that sits in `WaitingForWorker` or
`RetryPending` for a day therefore releases an unattended, cost-generating start at an arbitrary time. That start is
nowhere near the scheduled instant (for example Wednesday 15:00 for a Saturday 19:00 rule). The consent was given for
a start at the scheduled time, not for any time up to a week later.

Rows can also go stale because nothing scans them:

- `SelectServerIdsAsync` skips archived servers.
- `ProcessDueAsync` returns early on an invalid time zone.
- `LoadStartContextAsync` returns `null` for an archived server.

Such a row keeps showing "the start waits..." in the UI. If the server is unarchived within the same rule period, the
stale row is released as a start, because no stop is in flight and no later occurrence has come due.
**Fix:** Bound the wait by time as well. For example, close the row as `SkippedStopUnfinished` when
`now - occurrence > MaxStartWait` (a new option, such as the grace window multiplied by a factor, or one hour). Check
the row's age before the consent and stop-in-flight evaluation. Alternatively, apply the grace window to the instant
the stop resolved instead of to the occurrence. Document the chosen bound on `StartGraceWindow`, replacing the
"not limited by this window" sentence.

### WR-02: `SkippedStopUnfinished` conflates four reasons and its copy is wrong for one of them

**File:** `sources/ReactApp/src/locales/en.json:3310`, `de.json:3310`; `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:566-578,597-601`
**Issue:** The outcome is stored for four causes:

- The stop failed.
- The next occurrence came due while the stop was still running.
- The rule was changed while the start waited.
- A first-evaluation start finds a failed stop holding the deployment.

The UI sentence is "skipped, the shutdown before it did not finish." That text is false for "the rule was changed". It
is also misleading for the first-evaluation case, where the shutdown did finish unsuccessfully. The distinguishing
`Details` string is stored but never reaches the DTO (`ServerScheduleLastRunDto` carries only the outcome), so a user
cannot tell why the start was closed.
**Fix:** Either give "rule changed" its own outcome, or reuse `SkippedMissed` or a new `SupersededByEdit` value. Or
word the copy so it is true for every cause, for example "skipped, the server was still shutting down or the shutdown
failed". If the `Details` text is meant to be user-visible, expose a localized reason code in the DTO instead of the
English `Details` string.

### WR-03: Mandatory AGENTS.md rule violated: private members have no XML documentation (carried over from the earlier WR-04)

**File:** `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:66,165,938`; `sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs:212`
**Issue:** `_options`, `Validated(...)` and `BeginAsync(...)` in the runner, and `Truncate(...)` in the ledger, have no
XML documentation. AGENTS.md requires meaningful XML docs on every C# member. The new gap-closure members such as
`IsStopInFlightAsync` and `HasFailedStopAsync` are documented, which leaves these four inconsistent.
**Fix:** Add `<summary>`, `<param>` and `<returns>` elements. For example, for `Truncate`: "Cuts a text to at most
`maxLength` characters; returns `null` for `null`."

## Info

### IN-01: `DbUpdateException` handling in `ProcessStartAsync` hides real failures of the waiting-row conversion

**File:** `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:332-341`
**Issue:** Every `DbUpdateException` is treated as "recorded concurrently" and logged at Information. That reasoning
holds only for the insert of a new row against the unique index. For the waiting-to-started conversion, the whole
transaction runs under the server-row lock and the change is an UPDATE of a row the same transaction just read, so a
concurrent writer cannot cause the exception. A genuine failure (constraint violation on `LifecycleJobFk`, a
connectivity fault surfaced as `DbUpdateException`, a `DbUpdateConcurrencyException`) is hidden at Information level.
The row stays waiting and retries each pass, so nothing is lost, but the failure goes unnoticed.
**Fix:** Log at Warning, or add `when (open is null)` semantics. Alternatively, pass `due.Waiting` into the handler and
log at Warning when it is true.

### IN-02: The same "active lifecycle job" status predicate is repeated in five places

**File:** `sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:617-618,757-761,831-834` (and the failed variant)
**Issue:** The `Queued || Running || RetryPending || WaitingForWorker` list is copied into `EvaluateStartSkipAsync`,
`ApplyStopAsync` and `IsStopInFlightAsync`. A new active status would have to be added in every copy, and a missed copy
silently changes the busy or in-flight decision.
**Fix:** Extract a static `Expression<Func<LifecycleJob, bool>>`, or a shared `ELifecycleJobStatus[]` used with
`Contains`.

### IN-03: No test exercises two concurrent runners against the waiting row

**File:** `tests/Api.Tests/ServerAutomation/ScheduledRunTests.cs` (the `StartRule_Waiting*` tests)
**Issue:** The exactly-once claim across two instances rests on `ServerRowLock` plus the re-read in
`LoadStartContextAsync`. The tests cover repeated sequential passes, a rewound watermark and a lost watermark commit.
None runs two runners at the same time, or interleaves the read of the waiting row with a competing conversion.
**Fix:** Add one test with two scopes started through `Task.WhenAll`, or a deterministic interleaving using a barrier
inside a stub `IGameServerLifecycleService`. It should assert exactly one start job. Run it against PostgreSQL if the
suite has such a fixture; Serializable on the test provider does not prove the ReadCommitted plus lock path.

### IN-04: Shared `.button { white-space: inherit }` is a global change with no audit of nowrap-dependent callers

**File:** `sources/ReactApp/src/styles.css:1982-1984`
**Issue:** Every `.button` in the app, including `.button--small`, `.button--icon-only`, table-row actions and public
page buttons, now wraps multi-word labels wherever its container allows. The change also makes a button inherit
`pre`-like values from any ancestor. The tests verify the one rule and the 320 px German workspace header. They do not
check other buttons that relied on `nowrap` in flex rows or table cells. The local overrides that were removed (cost
alerts, fleet card, filter chips, hero actions) were the only ones audited.
**Fix:** Run a visual or e2e pass over the other `.button` call sites (tables, toolbars, dialogs) at 320 px and at
desktop width. Where a button must stay on one line, set `white-space: nowrap` on the containing component, as the
comment already says.

---

_Reviewed: 2026-11-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

---
phase: 68-auto-stop-schedules
plan: 19
subsystem: testing
tags: [playwright, uat, aspire, podman, mailpit, hosting-automation, emulated-hetzner]

requires:
  - phase: 68-auto-stop-schedules
    provides: automation evaluator, schedule runner, notice, menu rows, toast, emulated worker profiles (plans 03, 11, 14-17)
provides:
  - seeded UAT scenarios UAT 11 to 16 with fast automation timers (salvaged commits)
  - automated seeded-stack Playwright suite for automatic shutdown and schedules (fresh and restarted phase)
  - fix for scheduled starts that failed in the lifecycle executor (found by the suite)
  - emulated UAT profile and AppHost values that let a started UAT server pass its preflight
affects: [phase-68 verification, milestone-end real-environment UAT]

# Actuals (#2632): chars/4 over the realized diff of this run (5 commits) plus the 2 salvaged commits.
actuals:
  tokens: 14000
  tasks: 3
  commits: 5
plan_head_before: 68cad819a265bd1b3546dd3737767f47c0df1dbd
plan_head_after: 9d3c98a6499cb698c42090db2d49f23276a33896

tech-stack:
  added: []
  patterns:
    - "UAT specs hand observations between the fresh and the restarted phase through a JSON file in test-results"
    - "A UAT spec waits for a live warning, then reloads the page between attempts to wait for a job entry"

key-files:
  created:
    - sources/ReactApp/tests/uat/automation.spec.ts
  modified:
    - sources/ReactApp/tests/uat/support/uat.ts
    - sources/ReactApp/tests/uat/metrics.spec.ts
    - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
    - sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs
    - sources/AppHost/Program.cs
    - tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs

key-decisions:
  - "A start queued by a schedule has no requesting user and skips the executor actor check; user-initiated starts without a requester are still rejected"
  - "The emulated Hetzner profile reports 160 GiB of disk so the default Arma 3 peak demand (about 102 GiB) fits"
  - "The UAT profile sets placeholder Steam credentials, which also keeps a developer's real credentials out of the UAT stack"
  - "The restarted check asserts no automatic shutdown email after the restart and no duplicate subject, not equality with the fresh phase inbox, because Mailpit is in memory and starts empty after a restart"

patterns-established:
  - "UAT stack runs use a worktree-local Aspire data volume; a new checkout path starts with an empty database without -Fresh"

requirements-completed: [AUT-01, AUT-02, AUT-03, AUT-04, AUT-05, AUT-06]

coverage:
  - id: D1
    description: "Seeded scenarios UAT 11 to 16 and fast UAT automation timers exist and are tested"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs (dotnet test, 37 passed with the cost reconciliation and emulator tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Idle shutdown warns (notice, menu row, toast, Mailpit email) and one lead later an automatic stop completes to Off with one warned and one requested audit entry"
    requirement: "AUT-01"
    verification:
      - kind: automated_ui
        ref: "playwright: sources/ReactApp/tests/uat/automation.spec.ts 68-2 (UAT_PHASE=fresh, seeded stack)"
        status: pass
    human_judgment: false
  - id: D3
    description: "A maximum runtime below the elapsed runtime stops although players are online"
    requirement: "AUT-02"
    verification:
      - kind: automated_ui
        ref: "playwright: sources/ReactApp/tests/uat/automation.spec.ts 68-3 (UAT_PHASE=fresh, seeded stack)"
        status: pass
    human_judgment: false
  - id: D4
    description: "A stop rule warns one lead before and stops at its time; a start rule with consent queues a Start server job by the schedule that passes its preflight"
    requirement: "AUT-03"
    verification:
      - kind: automated_ui
        ref: "playwright: sources/ReactApp/tests/uat/automation.spec.ts 68-4 and 68-5 (UAT_PHASE=fresh, seeded stack)"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#Start_ByScheduleWithoutRequester_PassesActorPreflightAndProvisions"
        status: pass
    human_judgment: false
  - id: D5
    description: "The cost-skipped start and the paused-starts consent notice show on UAT 15 and UAT 16"
    requirement: "AUT-03"
    verification:
      - kind: automated_ui
        ref: "playwright: sources/ReactApp/tests/uat/automation.spec.ts 68-6 (UAT_PHASE=fresh, seeded stack)"
        status: pass
    human_judgment: false
  - id: D4b
    description: "After a stack restart every automatic stop appears once in Activity and no automatic shutdown email is sent again"
    requirement: "AUT-06"
    verification:
      - kind: automated_ui
        ref: "playwright: sources/ReactApp/tests/uat/automation.spec.ts 68-7 (UAT_PHASE=restarted, seeded stack)"
        status: pass
    human_judgment: false
  - id: D6
    description: "A real worker on a real Hetzner machine performs the automatic stop"
    requirement: "AUT-05"
    verification: []
    human_judgment: true
    rationale: "Real-environment checks are deferred to the milestone end (project feedback); the suite carries a skipped test that names the deferral"

duration: 150min
completed: 2026-11-25
status: complete
---

# Phase 68 Plan 19: Seeded-stack UAT for automatic shutdown and schedules Summary

**An automated Playwright suite proves idle shutdown, maximum runtime, scheduled stop, scheduled start, the skipped and paused start states and exactly-once across a stack restart on the real seeded local UAT stack, and it found and fixed a defect that made every scheduled start fail in the lifecycle executor.**

## Performance

- **Duration:** about 150 min (salvage continuation, most of it waiting on four seeded-stack runs)
- **Completed:** 2026-11-25
- **Tasks:** 3 (tasks 1 and 2 of the plan were salvaged from the earlier run; the plan's third task is the spec and its runs)
- **Files modified:** 7 in this run (plus 8 in the salvaged commits)

## Accomplishments

- Salvaged work verified, not redone: the two merged commits `dab19244` (seeded scenarios UAT 11 to 16, unfunded group cost owner, emulated worker entries, tests) and `455f8279` (AppHost automation timers, Start-Uat description) still build and their targeted API tests pass against the current tree (37 passed).
- The work-in-progress spec was ported onto the current helper and metrics spec files (the patch applied cleanly with `--3way`; everything `b5d2894b` added to `metrics.spec.ts` is kept) and reviewed against the real React strings and selectors before the first run.
- `automation.spec.ts` drives the panel the way a user does: it turns on the email channel through the cost alerts page, sets idle shutdown and a maximum runtime, adds a stop and a start rule in UTC, allows scheduled starts, and observes the live toast, the notice with its reason, the menu row, the Mailpit email, the finished job with its trigger line, the server turning Off, and exactly one warned and one requested audit sentence with no Terminate entry.
- The restarted test (68-7) proves after a real stop and start of the stack (same database, new containers) that UAT 11, 12 and 13 are Off with exactly one automatic stop job, one `AutomaticStopRequested` and one `AutomaticStopWarned`, that UAT 13's rule still lists its last run, and that no automatic shutdown email arrived after the fresh phase finished.
- Metrics 67-8 compares its off and active sections on the Phase 66 and 67 titles only, so the automation scenarios do not disturb it.

## Task Commits

Salvaged from the earlier run (already in the base):

1. **Task 1 (tracer): seeded automation scenarios** - `dab19244` (feat)
2. **Task 1: fast automation timers in the UAT profile** - `455f8279` (chore)

New in this run:

3. **UAT helpers and metrics 67-8 on its own titles** - `9e3fb5f0` (test)
4. **Emulated UAT profile fits a default Arma 3 start** - `c2a31186` (fix, deviation 1)
5. **Scheduled starts without a requesting user run** - `b9430844` (fix, deviation 2, with two executor tests)
6. **Placeholder Steam credentials in the UAT profile** - `f01c845f` (chore, deviation 3)
7. **Seeded-stack automation spec (fresh and restarted tests)** - `9d3c98a6` (test)

The restarted test was committed inside the spec commit, not as the separate `test(webapp): check automation exactly once after a UAT restart` commit the plan names, because the spec file was reviewed and run as one unit.

## Files Created/Modified

- `sources/ReactApp/tests/uat/automation.spec.ts` - tests 68-1 to 68-6 and 68-8 (`@fresh`), 68-7 (`@restarted`)
- `sources/ReactApp/tests/uat/support/uat.ts` - `uatAutomationTitles` and the automation hand-over helpers
- `sources/ReactApp/tests/uat/metrics.spec.ts` - 67-8 ignores the automation titles
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs` - scheduled start skips the actor check
- `sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs` - 160 GiB emulated profile
- `sources/AppHost/Program.cs` - placeholder Steam credentials inside the `Uat:Enabled` block
- `tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs` - two executor regression tests

## Decisions Made

See the frontmatter `key-decisions`. The UAT timings (5 s evaluation, 1 minute lead, 1 minute idle minimum) are the plan's.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The emulated Hetzner profile could never fit a start**
- **Found during:** run 1 of the seeded suite, test 68-5
- **Issue:** the schedule fired for UAT 14, but the start could not be queued: "The included server disk is insufficient for the peak deployment requirement." The default Arma 3 peak demand is about 102 GiB (40 GiB runtime, 5 GiB workspace, 20 % margin) and the emulated profile had 80 GiB, so every start of an included-disk server in the UAT stack was rejected, manual ones too.
- **Fix:** the emulated profile reports 160 GiB (4 vCPU, 16 GiB RAM).
- **Files modified:** `sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs`
- **Verification:** targeted API tests; the start is queued in runs 2 to 4.
- **Committed in:** `c2a31186`

**2. [Rule 1 - Bug] Every scheduled start failed as "actor no longer authorized"**
- **Found during:** run 2 and the unit-level reading that followed (the job showed `permanent-actor-no-longer-authorized` through the jobs API)
- **Issue:** a start queued by a schedule has no requesting user by design (`EnqueueAutomatedActionAsync` passes none), but the executor's start preflight rebuilt the principal of `RequestedByFk` and rejected a job without one. A scheduled start could therefore never begin. This is a production defect of an earlier Phase 68 plan, not a harness problem.
- **Fix:** a start with no requester and an automatic trigger skips the actor check (the schedule runner proved the consent when it queued the job, and the cost owner is re-checked just before). A user-initiated start without a requester is still rejected.
- **Files modified:** `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs`, `tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs`
- **Verification:** `Start_ByScheduleWithoutRequester_PassesActorPreflightAndProvisions` and `Start_ByManualRequestWithoutRequester_IsRejectedBeforeMutation`; 173 targeted tests and the full solution test run pass; the live suite (run 4) shows the job "waiting to begin" instead of failed.
- **Committed in:** `b9430844`

**3. [Rule 3 - Blocking] The UAT stack had no Steam credentials**
- **Found during:** run 3 (aborted by me after 68-5 failed), through the jobs API: `permanent-worker-bootstrap-invalid`, "Steam:Username and Steam:Password must be configured before a 'arma3' worker can be provisioned."
- **Issue:** after fixes 1 and 2 the start still failed at the worker bootstrap render, so no start could proceed in the UAT stack.
- **Fix:** the `Uat:Enabled` block of the AppHost sets placeholder values (`uat-emulated`); the emulated provider never uses them, and it also keeps real credentials from a developer's secret files out of the UAT stack. This is a development harness value only, no production behavior change.
- **Files modified:** `sources/AppHost/Program.cs`
- **Verification:** 68-5 passes with the new assertion that the start entry does not say "did not finish".
- **Committed in:** `f01c845f`

**4. [Rule 1 - Bug] The saved 68-7 compared the restarted inbox with the fresh inbox**
- **Found during:** review of the work in progress
- **Issue:** the spec asserted the sorted subjects after the restart equal the fresh phase subjects, but Mailpit keeps messages in memory, so after a real restart the inbox is empty and the equality could never hold.
- **Fix:** assert that no automatic shutdown email arrived after the fresh phase finished, that no subject is in the inbox twice, and that every present subject was already seen in the fresh phase.
- **Files modified:** `sources/ReactApp/tests/uat/automation.spec.ts`
- **Committed in:** `9d3c98a6`

---

**Total deviations:** 4 auto-fixed (2 blocking harness issues, 2 bugs, one of them in production code)
**Impact on plan:** fixes 1 to 3 were needed for the plan's own truth "a start rule with consent queues a Start server job" to be observable; fix 2 is a real product defect the suite was meant to find. No scope creep beyond the files the fixes belong to.

## Verification That Ran

| Check | Result |
|-------|--------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter UatFleetScenarioSeederTests, UatFleetScenarioCostReconciliationTests, UatWorkerChannelEmulatorTests` (salvaged tasks, current tree) | pass, 37 of 37 |
| Targeted API run after the fixes (executor, seeder, schedule, emulator, Hetzner provider tests) | pass, 173 of 173 |
| `dotnet test X39.UnitedTacticalForces.slnx` (stack stopped) | pass: Api.Tests 1704, CloudWorker.Tests 364, X39.BI.Config.Parsing.Tests 93, Ui.Tests 2, 0 failed |
| `npm --prefix sources/ReactApp run typecheck` | pass |
| `eslint tests/uat --max-warnings 0`; `prettier --end-of-line auto --check tests/uat` | pass (the checkout uses CRLF, so plain `prettier --check` flags every file including untouched ones) |
| `npm --prefix sources/ReactApp run test:uat -- --list` | `UAT_PHASE=fresh`: 7 automation tests (68-1 to 68-6, 68-8) listed; `UAT_PHASE=restarted`: 68-7 listed |
| **Live seeded stack, `UAT_PHASE=fresh npm --prefix sources/ReactApp run test:uat`, final code (run 4)** | **15 passed, 1 skipped (68-8, the deferred real-environment test), 13.5 min; includes 66-1 to 66-4, 67-8 and 67-7** |
| **Live seeded stack after a stack restart without a fresh database, `UAT_PHASE=restarted npm --prefix sources/ReactApp run test:uat`** | **3 passed (68-7, 66-3 part B, 67-10), 2.4 min** |

How the live runs went, honestly: Podman 6.0.2 was available (Docker Desktop was not running). Run 1 (before fixes) ended 7 passed, 6 failed: 68-5 for deviation 1, and 66-2 failed because the cost alert toast was not seen within 90 s although the banner showed the alert; its four design-shot tests and 66-3 failed or were skipped as a consequence of the missing hand-over file. The 66-2 failure did not reproduce in runs 2 and 4 and was not investigated further; the likely cause is a timing race (an alert that is already in the client cache when its frame arrives raises no toast, by design). Run 2 (disk fix only) passed 15; the job then showed `permanent-actor-no-longer-authorized`, found by reading the jobs API, which led to fix 2. Run 3 was stopped after 68-5 caught the Steam bootstrap failure (fix 3). Run 4 and the restart are the recorded results above. The stack was started with `scripts/Start-Uat.ps1 -NoBrowser` without `-Fresh`: this worktree has its own Aspire data volume (named from the checkout path), so its database started empty; `-Fresh` deletes every volume named `*pg-database*`, including those of other checkouts, and was deliberately not used in this shared Podman environment. Every stack started here was stopped, and its containers and its volume were removed.

## Known Stubs

None.

## Issues Encountered

- `npm ci` was needed in the new worktree (586 packages from the lockfile, no new package); the environment has Node 25.5 while `engines` asks for Node 24 (warning only).
- A zero-byte-named `NUL` file appeared in `sources/AppHost` while the stack ran (written by the AppHost process); it was deleted and never staged.

## Human re-check

Copy-paste commands (PowerShell, repository root; Podman or Docker must be running):

```powershell
# 1. Start fresh (removes the PostgreSQL data volumes so the database is reseeded) and leave it running in this console.
./scripts/Start-Uat.ps1 -Fresh -NoBrowser

# 2. In a second console, once http://localhost:5173/next/ answers: run the fresh phase (about 14 minutes).
$env:UAT_PHASE = 'fresh'; npm --prefix sources/ReactApp run test:uat

# 3. Stop the stack: press Ctrl+C in the console of step 1.

# 4. Start again WITHOUT -Fresh (same database) and wait for the React app.
./scripts/Start-Uat.ps1 -NoBrowser

# 5. In the second console: run the restarted phase (about 3 minutes).
$env:UAT_PHASE = 'restarted'; npm --prefix sources/ReactApp run test:uat

# 6. Stop the stack with Ctrl+C.
```

Where to look while it runs or afterwards: the fleet at http://localhost:5173/next/command/servers (servers UAT 11 to 16), Mailpit at http://localhost:8025 (the automatic shutdown warnings, subject "Automatic shutdown of <server> at ..."), the Activity tab of UAT 11, 12 and 13 (one "Shut down and stop billing" job each with its "Automatic: ..." trigger line), UAT 14 (a "Start server" job by the schedule), UAT 15 (last run "skipped, the cost owner could not pay for a start") and UAT 16 ("Scheduled starts are paused"). Design screenshots of the notice, panel and audit are written to `sources/ReactApp/test-results/uat-shots`.

To seed and try by hand instead: run step 1, open a server, set idle shutdown at 1 minute (UAT 11), a maximum runtime of 1 hour (UAT 12), or a UTC stop rule two to three minutes ahead (UAT 13); the timers are fast (check every 5 seconds, warning one minute before).

Deferred follow-up: automatic stop of a real dedicated worker on a real Hetzner machine (provider teardown verification, billing stop and real player counts), to be run at the milestone end. The skipped test 68-8 in `automation.spec.ts` carries this wording.

Deferred follow-up: completion of a scheduled start. The suite asserts the queued job, its trigger line and its audit entry; the job then waits for a worker because the UAT worker emulation registers no worker for a freshly provisioned deployment, so a started UAT server never reaches Running. Verify end to end with a real worker at the milestone end (or extend the worker channel emulator to register a worker for new deployments).

## Next Phase Readiness

- The seeded-stack proof of the Phase 68 success criteria is automated and repeatable with the commands above.
- Two Phase 68 production-adjacent facts for the verifier: scheduled starts could not run before `b9430844`, and the emulated UAT profile could not fit a default Arma 3 start before `c2a31186`.
- No blockers.

## Self-Check: PASSED

All commits listed above exist in the branch history (`9e3fb5f0`, `c2a31186`, `b9430844`, `f01c845f`, `9d3c98a6`, and the salvaged `dab19244`, `455f8279`), `sources/ReactApp/tests/uat/automation.spec.ts` exists, and the working tree was clean apart from this summary.

---
*Phase: 68-auto-stop-schedules*
*Completed: 2026-11-25*

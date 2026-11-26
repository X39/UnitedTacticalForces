---
status: diagnosed
phase: 68-auto-stop-schedules
source: [68-VERIFICATION.md]
started: 2026-11-24T23:47:49Z
updated: 2026-11-25T09:04:31Z
audit_acknowledged:
  milestone: v2.1
  at: 2026-11-26
  gap_snapshot: "diagnosed::scenarios=0"
---

## How to run

Start (fresh seed, from the repository root in PowerShell; Podman or Docker must be running):

```powershell
./scripts/Start-Uat.ps1 -Fresh -NoBrowser
```

Open http://localhost:5173/next/command/servers (servers UAT 11 to 16 carry the automation scenarios; Mailpit is at
http://localhost:8025). Stop the stack with Ctrl+C in the terminal that runs the script.

Only test 1 needs the running stack. Tests 2 and 3 are decisions and need nothing started. Tests 4 and 5 need real
infrastructure and are already skipped; a skip does not block the phase.

The functional behaviour was already run automatically on the seeded stack by plan 68-19 (fresh phase: 15 passed,
1 skipped; restarted phase: 3 passed), so it is not repeated here.

## Current Test

[testing complete]

## Tests

### 1. Visual review of the automation settings
expected: Open a server (for example UAT 13), go to Settings, Automation. The time zone select looks like the other styled selects, closed and open. Adding a Start rule and saving opens the scheduled-start consent dialog, and its facts and buttons are aligned. In German at 320 px width the automatic shutdown notice fits without overflow. Known from the code-only UI review (19/24): the weekday grid shows three days per row at 320 px where the UI spec says four, and a rule without a next run shows "Next run: -".
automated_evidence: |
  Seeded stack, 2026-11-25, Playwright screenshots in sources/ReactApp/test-results/uat-shots/68-verify (en and de,
  1280 and 320 px). Measured: no horizontal page scroll and no clipped element in the automatic shutdown notice
  (de 320, de 1280, en 320); the consent dialog does not overflow at 320 px. The time zone select is the shared
  native SelectField (same class and computed style as the other selects); its open list is the browser's native
  popup. Observed: the weekday grid shows two days per row at 320 px (UI spec: four), in both languages. Outside
  this phase: the German header button "Herunterfahren und Abrechnung beenden" overflows its box at 320 px.
result: issue
reported: "Test 1 is semi-pass. Shit works but looks like shit still. Radio buttons and simplistic checkboxes eg. make me wonder where time was spend for actual UI work. The general amount of scrolling required to get to theese section is also massive, making the actual UX hard. We hence need to add a UI-Polishing phase. On the pure technical level, things work. However: The shutdown button \"Herunterfahren und Abrechnung beenden\" is not wrapping, causing overflow."
severity: cosmetic

### 2. Decision: scheduled-start consent is tied to the cost owner, not to the person (code review CR-01)
expected: Accept as is, or ask for a follow-up. Today a scheduled start runs when a stored consent exists for the server's current cost owner; the consent is re-read each time the start executes (23bdf118). The user who gave the consent is not re-authorized later, and there is no way to withdraw consent other than changing or removing the Start rule. Answer "pass" to accept, or describe what should change (for example: re-check the consenting user at fire time, add an explicit revoke).
result: pass
note: "User: If something is set up as scheduled then indeed it must start and stop on said schedule"

### 3. Decision: a scheduled start that comes due while the server is busy is skipped for good (code review WR-01)
expected: Accept as is, or ask for a follow-up. Example: stop at 04:00 and start at 04:05; if the stop is still tearing down at 04:05 the start is recorded as skipped and not retried, although a 15 minute grace window exists for starts missed during downtime. The skip is shown as the rule's last run. Answer "pass" to accept, or ask for a retry inside the grace window.
result: issue
reported: "Test 3 no. If someone is that stupid (or we do not have a proper restart mechanic for the server, as this is what that would look like), the start must still be executed AFTER the stop is fully processed"
severity: major

### 4. Automatic stop on a real Hetzner machine with a real worker (optional)
expected: An idle, maximum-runtime or scheduled stop tears the machine down and the provider reports it as non-billable.
result: skipped
reason: "Deferred follow-up: pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"

### 5. A scheduled start completes with a real worker (optional)
expected: A scheduled start provisions the machine and the server reaches Running. The UAT emulation registers no worker for a freshly provisioned deployment, so the automated suite asserts only the queued job, its trigger line and its audit entry.
result: skipped
reason: "Deferred follow-up: pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"

## Summary

total: 5
passed: 1
issues: 2
pending: 0
skipped: 2
blocked: 0

## Gaps

- gap_id: G-68-1
  truth: "The workspace header button \"Herunterfahren und Abrechnung beenden\" wraps its label and stays inside its box at 320 px width; the technical behaviour of the automation settings works"
  status: failed
  reason: "User reported: Test 1 is semi-pass. Shit works but looks like shit still. Radio buttons and simplistic checkboxes eg. make me wonder where time was spend for actual UI work. The general amount of scrolling required to get to theese section is also massive, making the actual UX hard. We hence need to add a UI-Polishing phase. On the pure technical level, things work. However: The shutdown button \"Herunterfahren und Abrechnung beenden\" is not wrapping, causing overflow."
  severity: cosmetic
  test: 1
  scope_note: "Only the non-wrapping shutdown button is a gap of this phase. The general visual quality (radio buttons, checkboxes, amount of scrolling) is a deferred follow-up for a new UI polishing phase, see Deferred Follow-Ups."
  root_cause: "The shared .button class forces white-space: nowrap (sources/ReactApp/src/styles.css:1982). At 520 px and below the header button is pinned to width 100% (workspace-shell.css:85-88), so the long German label cannot wrap and spills over both edges. The same rule makes the confirm button of the cost confirmation dialog overflow; six local CSS overrides already work around it."
  artifacts:
    - path: "sources/ReactApp/src/styles.css"
      issue: "Base .button (1963-1988) forces white-space: nowrap and has no wrapping variant"
    - path: "sources/ReactApp/src/components/ActionControl.tsx"
      issue: "Shared ActionButton, ActionLink and ActionAnchor offer no wrap control, so consumers patch with page-local CSS"
    - path: "sources/ReactApp/src/components/server/workspace-shell.css"
      issue: "Pins the header button to 100% width at 520 px and below (85-88), which turns nowrap into visible overflow; not wrong by itself"
  missing:
    - "Let the shared button wrap its label (fix in the shared layer, not a seventh local override)"
    - "Remove the local white-space overrides that become redundant (cost-alerts.css, fleet-card.css, fleet-toolbar.css, cost-alert-settings.css, styles.css public hero)"
    - "Check header and cost confirmation dialog at 320 px in German, plus other .button consumers (tabs, pagination, chips) for reliance on nowrap"
  debug_session: ".planning/debug/shutdown-button-no-wrap-overflow.md"
- gap_id: G-68-3
  truth: "A scheduled start that comes due while a stop of the same server is still being processed is executed after the stop is fully processed, not skipped"
  status: failed
  reason: "User reported: Test 3 no. If someone is that stupid (or we do not have a proper restart mechanic for the server, as this is what that would look like), the start must still be executed AFTER the stop is fully processed"
  severity: major
  test: 3
  root_cause: "ServerScheduleRunner treats a blocked start as final. EvaluateStartSkipAsync (ServerScheduleRunner.cs:435-501) returns SkippedRunning for any unresolved deployment and SkippedBusy for any active lifecycle job, without telling a stop in flight from a server that is really running. TryStartAsync (312-323) and RecordBusyAsync (375-425) then commit the unique ServerScheduleRun row, which is the exactly-once marker, and the watermark SchedulesEvaluatedThrough moves past the occurrence (103-104, 175), so it is never evaluated again. StartGraceWindow is only read at 441 to skip starts first seen too late; it never causes a re-evaluation."
  artifacts:
    - path: "sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs"
      issue: "Skip is final (243-279, 312-323, 375-425, 435-501) and the watermark passes the occurrence (103-104, 175)"
    - path: "sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs"
      issue: "The run row is both the skip record and the exactly-once marker; there is no pending state"
    - path: "sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs"
      issue: "StartGraceWindow is used only for the missed check"
    - path: "tests/Api.Tests/GameServerHosting/ScheduledRunTests.cs"
      issue: "StartRule_WithAPendingAction_IsRecordedSkippedBusy (553-572) asserts the final skip for exactly the stop-in-flight window"
    - path: "sources/ReactApp/src/locales/en.json"
      issue: "SkippedRunning copy 'skipped, the server was already running' (3309) is misleading for a stop in flight; same in de.json"
  missing:
    - "Keep a due start open (no run row) while a stop or terminate of the same server is in flight, and re-evaluate it on later ticks"
    - "Re-collect open start occurrences although the watermark has passed them, keeping exactly-once across API restarts and all state in PostgreSQL"
    - "Queue the start through the existing path once the stop is fully processed; consent and cost owner are re-read at that moment"
    - "Keep a genuinely running server as a final SkippedRunning"
    - "When the wait ends without a start (stop failed or bound reached), record the real blocker, not SkippedMissed"
    - "Queueing the start behind the stop job is not available: one unresolved deployment per server (DatabaseContext.cs:400-403), EnqueueCore conflict (GameServerLifecycleService.cs:455-463)"
  debug_session: ".planning/debug/scheduled-start-skipped-while-stop-in-progress.md"

## Deferred Follow-Ups

- test: 1
  idea: "Add a UI polishing phase: the automation settings work but look poor (plain radio buttons and checkboxes), and reaching the section needs a massive amount of scrolling, which makes the UX hard. Also seen in the automated pass: weekday grid shows two days per row at 320 px (UI spec: four)."
  deferred_at: 2026-11-25

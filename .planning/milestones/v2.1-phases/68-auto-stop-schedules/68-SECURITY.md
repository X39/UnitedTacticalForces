---
phase: "68"
slug: "auto-stop-schedules"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-25"
---

# Phase 68 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Control plane to PostgreSQL | Exactly-once timer decisions depend on DB constraints (unique and filtered indexes), not process memory (68-01) | Pending stops, schedule run rows, alerts, job triggers |
| Owner-supplied time zone id to schedule evaluation | A zone id from the settings payload reaches the calculator (68-02) | IANA zone id string |
| Development harness to control plane | Emulator authenticates as seeded workers through the real protocol; UAT timings and seeds must never reach production (68-03, 68-19) | Worker credentials (per session), seeded data, UAT timing config |
| API values to rendered text | Outcome codes, idle states, reasons, triggers and pending-stop data are rendered by the React client through closed maps (68-04, 68-15, 68-16) | Enum-like API strings, stop instants |
| Control plane (no user) to lifecycle queue | System-initiated jobs bypass user authorization by design: no synthetic principal (68-05) | Automated Start/Stop jobs |
| API to browser (automation block, manage flag, activity, frames) | The automation block, manage flag, activity entries and server frames reach viewers of the server (68-06, 68-09) | Automation settings, consent state (no owner identity), audit entries |
| Control plane to Discord and SMTP / API to sockets | Warning text containing the user-controlled server title leaves the platform; raised and dismissed frames reach the cost owner audience (68-07) | Server title, stop time, alert frames |
| Browser to settings endpoint | Untrusted payload changes cost-bearing behavior (scheduled starts, consent, rules) (68-08, 68-12, 68-14, 68-17) | Settings, rules, time zone, consent flag, version |
| Control plane timers to billable infrastructure | An automatic decision stops a running, billed server; a saved rule creates cloud cost with no user present (68-10, 68-11) | Stop/Start jobs |
| Worker-reported player count to stop decision | A worker reading influences an automatic stop (68-10) | Player count, staleness |
| Socket frames to client cache | Raised alert frames enter the browser alerts cache (68-12) | Alert frame payloads |
| Control plane restart | Timer decisions must survive process loss (68-13) | Persisted pending stops, watermark, idle clock |
| Test harness to app | Mocked routes and frames drive the UI (68-18) | Test fixtures |

---

## Threat Register

Columns follow the template (Threat ID, Category, Component, Severity, Disposition, Mitigation, Status) plus Evidence (file:line, paths relative to the repo root A:\dev\git\UnitedTacticalForces).

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status | Evidence |
|-----------|----------|-----------|----------|-------------|------------|--------|----------|
| T-68-01 | Tampering | duplicate automatic stop or start after restart or with two instances | high | mitigate | Filtered unique index one Pending stop per deployment; unique (ScheduleFk, OccurrenceAt); unique CostAlert.PendingStopFk; tests prove each rejection | closed | sources/Database/DatabaseContext.cs:548-557 (PendingStopFk unique; pending index filtered); sources/Database/Hosting/AutomationPendingStop.cs:24,29 (Status = 0; EAutoStopStatus.Pending = 0 at sources/HostingContracts/EAutoStopStatus.cs:11); sources/Database/Hosting/ServerScheduleRun.cs:18 (Index ScheduleFk, OccurrenceAt, IsUnique); sources/Database/Migrations/20261122205650_AddServerAutomation.cs:245-249, 251-256, 315-319 (indexes in the real migration); tests/Api.Tests/GameServerHosting/ServerAutomationModelTests.cs:86,128,232 |
| T-68-02 | Repudiation | automatic job without attribution | medium | mitigate | LifecycleJob.Trigger and TriggerLimit persisted; seven audit kinds for every automation decision | closed | sources/Database/Hosting/LifecycleJob.cs:50,56; sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs:658-659,665-682; seven kinds at sources/HostingContracts/EHostingAuditEventKind.cs:96-126, written at ServerAutomationEvaluator.cs:563, PendingStopWithdrawal.cs:44, ScheduledRunLedger.cs:107, ServerAutomationSettingsService.cs:472,490, GameServerLifecycleService.cs:670-672 |
| T-68-03 | Tampering | time zone id lookup | medium | mitigate | FindZone accepts only DateTimeZoneProviders.Tzdb ids, null otherwise; no OS or BCL lookup | closed | sources/Api/Services/ServerAutomation/ScheduleCalculator.cs:78-83; ServerAutomationSettingsService.cs:159-170 (length cap 64, FindZone, canonical zone.Id); TimeZoneInfo absent from Services/ServerAutomation, Controllers, DTO (grep empty) |
| T-68-04 | Denial of service | unbounded occurrence scans | low | mitigate | NextAfter scans at most eight days; OccurrencesBetween bounded by its explicit window | closed | ScheduleCalculator.cs:126-144 (afterExclusive + Duration.FromDays(8)), 96-113, 146-175; ServerScheduleRunner.cs:50,175 (MaxCatchUp 8 days) |
| T-68-05 | Elevation of privilege | emulator active outside UAT | high | mitigate | Registration in AddTo gated by Development and Testing:UatScenarios:Enabled; no production code path change (diff gate on WorkerProtocol and the executor) | closed | sources/Api/HostedServices/UatWorkerChannelEmulator.cs:97-103 (returns before registering unless Development and the config key); sources/Api/Program.cs:172 (seeder gated), 217-227 (emulated provider gated, emulator registered via AddTo); sources/AppHost/Program.cs:12-31 (only inside Uat:Enabled). Diff gate: git log 230029d4..HEAD -- sources/Api/Services/WorkerProtocol is empty; the executor diff gate is breached only by b9430844, assessed as T-68-19-X1 (the emulator-gating threat itself is intact) |
| T-68-06 | Information disclosure | worker credential in logs | medium | mitigate | Credentials per session, never logged; ids and outcome codes only; failures through HostingFailureSanitizer | closed | UatWorkerChannelEmulator.cs:122-126 (Sanitize), 158-170, 407-419, 450-455, 490-495 (all log statements carry titles, ids, codes only; issue.Credential goes to AuthenticateAsync and the session at 222-230, never to a logger) |
| T-68-07 | Spoofing | misleading UI text from unknown API values | low | mitigate | Unknown idle states and outcome codes map to fixed fallback keys; API text never rendered from these fields | closed | sources/ReactApp/src/features/hosting/automationModel.ts:489-507 (fallback Unknown), 537-548 (fallback Other); sources/ReactApp/tests/hosting/automation-model.test.ts:405-436 (unknown code maps to Other) |
| T-68-08 | Elevation of privilege | automated enqueue misused for destructive actions | high | mitigate | Only Start and Stop accepted; Manual trigger refused; no synthetic principal; Terminate impossible with a test | closed | GameServerLifecycleService.cs:339-343 (Start/Stop only), 344-345 (Manual refused), 350-361 (principal null), 403-404 (no-user path rejects other kinds); only two callers: ServerAutomationEvaluator.cs:607 (Stop) and ServerScheduleRunner.cs:343 (Start); tests/Api.Tests/GameServerHosting/AutomatedLifecycleEnqueueTests.cs:233 |
| T-68-09 | Tampering | double job from claim and enqueue split across transactions | high | mitigate | Core joins the caller's transaction; rollback test proves job and audit disappear together | closed | GameServerLifecycleService.cs:406-411 (own transaction only when none is open), 691-692 (commit only if owned); AutomatedLifecycleEnqueueTests.cs:158 (rollback removes job and audit), 195 |
| T-68-10 | Repudiation | automatic job without trace | medium | mitigate | One automation audit entry per job with null actor and trigger details; Trigger persisted | closed | GameServerLifecycleService.cs:648 (RequestedByFk = actorId, null for automated), 658-659, 665-682 (AutomaticStopRequested / ScheduledStartRequested with Trigger and Limit details); AutomatedLifecycleEnqueueTests.cs:23,278 |
| T-68-11 | Elevation of privilege | canManageAutomation granted too widely | high | mitigate | Modify permission AND may use the current cost owner, computed server-side; role tests | closed | sources/Api/Services/ServerAutomation/ServerAutomationProjector.cs:41-59; GameServerProjectionService.cs:865-871 (flag computed server-side, detail only); tests/Api.Tests/ServerAutomation/ServerAutomationProjectionTests.cs:88,104,125; ServerAutomationSettingsTests.cs:140,159,178 |
| T-68-12 | Information disclosure | cost owner identity in the automation block | medium | mitigate | Block carries no owner names or ids; consent state only; detail requires View (existing 404 rule) | closed | sources/Api/DTO/ServerStartConsentDto.cs (only State), ServerAutomationDto/Settings/Status/Limits DTOs (no owner member; only schedule ids); ServerAutomationProjectionTests.cs:632 (serialized block has neither owner id nor name); sources/Api/Controllers/GameServersController.cs:62-74 (Authorize Server.View) |
| T-68-13 | Denial of service | per-request schedule and metrics work in list views | low | mitigate | Block computed only for the detail; metrics read only when idle is armed | closed | GameServerProjectionService.cs:196,552 (list and notification paths pass false), 216 (detail only), 865; ServerAutomationProjector.cs:228-241 (metrics only when armed); ServerAutomationProjectionTests.cs:140 |
| T-68-14 | Tampering | email header and Discord injection through the server title | high | mitigate | SanitizeTitle on subject and body; deterministic Message-Id path unchanged; CR/LF title test | closed | sources/Api/Services/Notifications/CostAlertMessageRenderer.cs:84 (RenderAutoStop sanitizes the title used in subject and body), 142-149 (control chars to space, whitespace collapsed); tests/Api.Tests/GameServerHosting/CostAlertMessageRendererTests.cs:196 |
| T-68-15 | Denial of service | warning spam | medium | mitigate | One alert per pending stop (unique PendingStopFk); resolution skips undelivered rows | closed | DatabaseContext.cs:548-549; sources/Api/Services/GameServerLifecycle/AutoStopAlertWriter.cs:95-112 (pending deliveries set to Skipped with StopResolved); ServerAutomationModelTests.cs:232 |
| T-68-16 | Information disclosure | warning reaches people outside the cost owner audience | medium | mitigate | Recipients from ICostAlertRecipientResolver; frames to GetCostOwnerViewAudienceAsync only; list filtered by cost owner visibility | closed | AutoStopAlertWriter.cs:45-46; sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs:117 (raised) and 186 (resolved) use the audience dictionary; CostAlertService.cs:46-57 (GetViewableCostOwnerIdsAsync filter and pending-only AutoStop alerts) |
| T-68-17 | Elevation of privilege | member without the right changes automation or gives consent | high | mitigate | Route id authorization: View else 404, CanManageAsync else 403; tests for each role | closed | sources/Api/Services/ServerAutomation/ServerAutomationSettingsService.cs:74-75 (signed-in user), 78-79 (View else 404), 87-88 (CanManage else 403); sources/Api/Controllers/GameServerAutomationController.cs:15 (Authorize), 67-79 (404/403/400/409 mapping); single production write path to settings, rules and consent columns (grep: only this service plus the dev-gated UatFleetScenarioSeeder.cs:422-435); tests ServerAutomationSettingsTests.cs:122,140,159,178 |
| T-68-18 | Tampering | cost-bearing start the owner never confirmed | high | mitigate | Consent bound to the current cost owner id, cleared when a Start rule is added or changed without consent; ScheduledStartsConfirmed audit with who and estimate | closed | ServerAutomationSettingsService.cs:337,347,384-414 (consent owner = server.CurrentCostOwnerFk; clearConsent when a Start rule changed), 548-549,571 (startRuleChanged), 484-497 (ScheduledStartsConfirmed with user, owner, estimate), 626-632; ServerScheduleRunner.cs:447-458 (fire-time check); tests ServerAutomationSettingsTests.cs:513,554,571,617 |
| T-68-19 | Tampering | malformed input (zone, days, times, counts) | medium | mitigate | Server-side validation: TZDB ids only, option ranges, MaxSchedules, duplicate detection; field-addressable 400 | closed | ServerAutomationSettingsService.cs:124-185 (ranges from options), 159-170 (TZDB), 202 (MaxSchedules), 233 (days 1-7, distinct), 239-244 and 280-295 (strict HH:mm), 256-270 (duplicates), 176-177 (HostingFieldValidationException); controller maps to 400 with fields; tests ServerAutomationSettingsTests.cs:208-490 |
| T-68-20 | Tampering | lost update between two editors | medium | mitigate | Integer Version claimed with conditional ExecuteUpdateAsync; 409 on mismatch | closed | ServerAutomationSettingsService.cs:343,370-371 (version compare), 420-439 and 446-457 (Where Version == update.Version, zero rows means conflict), 503-508 (unique-index race means conflict); GameServerAutomationController.cs ConflictObjectResult; test ServerAutomationSettingsTests.cs:104 |
| T-68-21 | Repudiation | automatic decisions invisible to members | medium | mitigate | Seven kinds whitelisted in the operations feed; job trigger exposed; tests through GetOperationsAsync | closed | GameServerProjectionService.cs:644-650 (all seven kinds), 679 (Trigger = lifecycleJob.Trigger), 793-795 (status mapping); tests/Api.Tests/ServerAutomation/AutomationOperationsTests.cs:50,83,108 |
| T-68-22 | Denial of service | frame storm from evaluator state writes | low | mitigate | ServerAutomationState excluded from the interceptor; test asserts no frame | closed | sources/Api/Services/GameServerLifecycle/FleetChangeInterceptor.cs:30 (documented exclusion), 267-290 (only Settings, Schedule, PendingStop entries collected; no ServerAutomationState); tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs:396-402 |
| T-68-23 | Tampering | duplicate stop from restart, retries or two instances | high | mitigate | Server-row lock, conditional claim Pending to Executed, filtered unique pending index, enqueue in same transaction; repeat-pass tests | closed | sources/Api/Services/ServerAutomation/ServerAutomationEvaluator.cs:200 (row lock), 595-604 (conditional claim, affected != 1 returns false), 606-616 (enqueue in the same transaction); ServerRowLock.cs (SELECT FOR UPDATE); DB index (T-68-01); tests AutomationRestartTests.cs:162,194,239 |
| T-68-24 | Denial of service | data loss through escalation to Terminate | high | mitigate | Only Stop queued; no Terminate path (D-AUTO-2) | closed | ServerAutomationEvaluator.cs:606-611 (GameServerActions.Stop only); Terminate does not occur in Services/ServerAutomation (grep empty); GameServerLifecycleService.cs:339-343; evaluator HasStopJob counts a Failed stop (ServerAutomationEvaluator.cs:704-709) so a failed stop is never re-queued or escalated |
| T-68-25 | Tampering | stop on a missing or spoofed signal | medium | mitigate | Unknown readings hold; readings only from the authenticated worker channel (Phase 67); active jobs count as activity | closed | sources/Api/Services/ServerAutomation/IdleClock.cs:46-55 (unavailable reading holds, never starts or advances a clock), 58 (playerCount > 0 or HasActiveJob resets); ServerAutomationEvaluator.cs:396-415; sources/Api/Services/ServerMetrics/ServerMetricsService.cs:111-123 (stale and non-heartbeat snapshots read as unavailable); tests IdleShutdownTests.cs:134,163,198 |
| T-68-26 | Denial of service | one failing server blocks automation for all | medium | mitigate | Per-server try/catch with tracker reset; worker loop survives exceptions | closed | ServerAutomationEvaluator.cs:73-98 (per-server catch, ChangeTracker.Clear); sources/Api/HostedServices/ServerAutomationWorker.cs:30-54 (loop catches and retries); tests ServerAutomationWorkerTests.cs:102,124 |
| T-68-27 | Tampering | scheduled start without the payer's consent | high | mitigate | Consent must match the current cost owner; mismatch records SkippedConsent; consent cleared on rule change | closed | sources/Api/Services/ServerAutomation/ServerScheduleRunner.cs:447-458 (consent present and StartConsentCostOwnerFk == CurrentCostOwnerFk, else SkippedConsent), ordering at 428-441; ServerAutomationSettingsService.cs:408-414; tests ScheduledRunTests.cs:508,523,654 |
| T-68-28 | Tampering | duplicate start or stop after restart or with two instances | high | mitigate | Unique run row per occurrence, per-occurrence transaction for starts, committed pending row for due stops, server-row lock; crash and repeat tests | closed | ServerScheduleRunner.cs:297-358 (transaction, row lock, run row and start job commit together), 262-271 (DbUpdateException means recorded concurrently), 654-667 (committed pending stop), 524; unique index (T-68-01); tests ScheduledRunTests.cs:371,688; AutomationRestartTests.cs:194,335,382 |
| T-68-29 | Elevation of privilege | unaffordable start draining a cost owner | medium | mitigate | CanReserveInitialSliceAsync pre-check and the executor's authoritative reservation; frozen owners skipped | closed | ServerScheduleRunner.cs:482-498 (owner null, frozen or unaffordable gives SkippedCost); sources/Api/Services/GameServerLifecycle/HostingCostSliceService.cs:97-127; LifecycleJobExecutor.cs:328-333 and the HostingAffordabilityException handling at 334-363; tests ScheduledRunTests.cs:577,592 |
| T-68-30 | Repudiation | silent skips | low | mitigate | One ScheduledRunSkipped audit per skip with reason, rule id and occurrence | closed | sources/Api/Services/ServerAutomation/ScheduledRunLedger.cs:93-115 (run row plus ScheduledRunSkipped audit with ScheduleId, Occurrence, Outcome); every skip path goes through AddSkip: ServerScheduleRunner.cs:312,389,549 |
| T-68-31 | Tampering | malformed raised frame | low | mitigate | parseCostAlertRaisedFrame validates every new field and drops malformed frames | closed | sources/ReactApp/src/features/hosting/costFrames.ts:131-170; sources/ReactApp/tests/hosting/cost-frames.test.ts (frame cases incl. :84,106,115,213) |
| T-68-32 | Tampering | contract drift between API and client | medium | mitigate | Byte-identical snapshot check, contracts:check, compile-time assignability test | closed | .github/workflows/dotnet.yml:37 (contracts:check), 75-76 (--export-openapi then cmp --silent against sources/ReactApp/openapi/utf-api.json); sources/ReactApp/package.json:18; sources/ReactApp/tests/hosting/contract-shape.test.ts:99,190; snapshot contains UpdateAutomationAsync (utf-api.json:5222). git diff of the working-tree-modified src/generated/api.ts is empty apart from CRLF warnings, so no content drift |
| T-68-33 | Tampering | duplicate or lost stop across restarts | high | mitigate | Restart, double-pass, second-instance and direct-claim tests over the shared database | closed | tests/Api.Tests/ServerAutomation/AutomationRestartTests.cs:129,162,194,239,335 |
| T-68-34 | Denial of service | data loss by escalation | high | mitigate | Test proves no Terminate job after a failed automatic stop (D-AUTO-2) | closed | tests/Api.Tests/ServerAutomation/AutomaticStopJobTests.cs:371-408 (Assert.DoesNotContain Terminate kind; two later passes queue nothing) |
| T-68-35 | Elevation of privilege | UI shows editable controls to non-managers | medium | mitigate | Controls disabled from canManageAutomation only; API re-checks; tests for read-only and archived | closed | sources/ReactApp/src/features/hosting/AutomationPanel.tsx:244 (editable = canManageAutomation === true and not archived), 541-547, 595-719 (every control disabled from editable), 734 (save row only if editable); tests/hosting/automation-panel.test.tsx:315,336; API re-check T-68-17 |
| T-68-36 | Information disclosure | draft persisted to storage or URL | low | mitigate | React state only; source-scan asserts no storage, URL or log writes | closed | Independent grep of AutomationPanel.tsx, ScheduleRuleFields.tsx, useAutomationSettings.ts for localStorage, sessionStorage, indexedDB, history push/replaceState, URLSearchParams, setSearchParams, location.state, navigate(), console returns no match; sources/ReactApp/tests/hosting/automation-source-scan.test.ts:29-60 |
| T-68-37 | Spoofing | misleading stop time | low | mitigate | Only the API stopsAt rendered; closed reason map with schedule-neutral fallback | closed | sources/ReactApp/src/components/server/AutoStopNotice.tsx (renders formatInstant(pendingStop.stopsAt)); sources/ReactApp/src/features/hosting/useAutoStopNotice.ts:43-55 (closed map, default is the schedule-neutral sentence) |
| T-68-38 | Information disclosure | settings link to viewers without the right | low | mitigate | Link only with canManageAutomation; server name in bdi; no owner data in notice | closed | AutoStopNotice.tsx (action rendered only when server.canManageAutomation === true; no owner data referenced; grep for costOwner in the notice and hook is empty); sources/ReactApp/src/components/server/CostAlertMenuItem.tsx:98,168 (bdi around alert.serverName) |
| T-68-39 | Spoofing | misleading attribution of an automatic job | low | mitigate | Closed trigger map; unknown triggers keep the actor line; API prose only through ApiText | closed | sources/ReactApp/src/features/hosting/activityLabels.ts:80-83,104-115 (switch over a closed list, default null so the actor line stays); sources/ReactApp/src/components/server/ActivityEntry.tsx:125-161 (ApiText) |
| T-68-40 | Repudiation | consent given without seeing the cost | medium | mitigate | Dialog shows cost owner, hourly and monthly estimate and rule count before the only consent path; API records who, when and estimate | closed | sources/ReactApp/src/components/server/ScheduledStartConsentDialog.tsx:75-93 (owner, hourly, monthly, start rule count); AutomationPanel.tsx:426 (normal save sends consent false), 466-471 (true only inside confirmConsent); API record: ServerAutomationSettingsService.cs:484-497 |
| T-68-41 | Tampering | invalid rules reaching the API | low | mitigate | Client validation mirrors the API; API stays authoritative with field errors | closed | sources/ReactApp/src/features/hosting/automationModel.ts:279 (validateAutomationDraft); API authoritative per T-68-19 |
| T-68-42 | Information disclosure | automation draft leaked to storage, URL or logs | low | mitigate | Source-scan test over the panel, rule block and save hook with a live-pattern proof | closed | sources/ReactApp/tests/hosting/automation-source-scan.test.ts:20-60 (draftFiles, forbiddenDraftSinks, inline-sample liveness proofs); independent grep clean (see T-68-36) |
| T-68-43 | Elevation of privilege | UAT timings or seeds active in production | medium | mitigate | Values only inside the AppHost Uat:Enabled block; seeder and emulator gated by Development plus Testing:UatScenarios:Enabled | closed | sources/AppHost/Program.cs:12-31 (all timings, placeholder Steam credentials, emulator flags only inside the Uat:Enabled block); sources/Api/Program.cs:172 (seeder), 217-222 (emulated provider, which also bounds the 160 GiB emulated profile from c2a31186), 227; UatWorkerChannelEmulator.cs:97-103; production defaults at sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs:16-33 (15 s, 10 min, 5 min) and sources/Api/appsettings.json carries no UAT automation values (Testing:AuthorizationBypass:Enabled is false) |
| T-68-44 | Information disclosure | UAT mail leaving the machine | low | accept | Mailpit is local and never part of a deployment (existing Phase 66 setup) | closed (accepted) | Accepted in 68-19-PLAN.md threat_model. Confirmed the Mailpit container is created only inside the Uat:Enabled block (sources/AppHost/Program.cs:33-43). Entered in the Accepted Risks Log below |
| T-68-SC | Tampering | npm/pip/cargo installs (one register row, repeated in all 19 plans) | high | accept | No package is installed by the phase's plans | closed (accepted) | Accepted in every plan. Claim verified: git diff 230029d4..HEAD over all csproj files, Directory.Packages.props and package-lock.json shows no change; sources/ReactApp/package.json gained only the script test:uat (b42af1bd, 2026-11-22 20:54, a Phase 66/67 UAT commit that predates the phase-68 plans created at 21:47; a script, not a dependency). NodaTime/TZDB (68-02) was already a dependency. Entered in the Accepted Risks Log below |
| T-68-19-X1 | Elevation of privilege | LifecycleJobExecutor.RevalidateStartAsync: actor re-check skipped for system-initiated starts (commit b9430844, unregistered) | medium | mitigate (by design; covered by T-68-08, T-68-27, T-68-29) | See the Unregistered change section below | closed, with one non-blocking advisory (consent-author authority is not re-validated at fire time) | sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs:2975-3001 (launchability and cost owner checks precede the early return), 3001-3025; tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs (Start_ByScheduleWithoutRequester_PassesActorPreflightAndProvisions, Start_ByManualRequestWithoutRequester_IsRejectedBeforeMutation) |

---

## Unregistered change: T-68-19-X1 (commit b9430844)

Code (LifecycleJobExecutor.cs:2999-3002):

    // A system-initiated start (a schedule) has no requesting user: ...
    if (job.RequestedByFk is null && job.Trigger is not ELifecycleJobTrigger.Manual)
        return;

It sits at the end of `RevalidateStartAsync` (called from `ExecuteStartAsync`, LifecycleJobExecutor.cs:157), after every other revalidation check, and skips only the final block: rebuild the requester principal, require server Operate and `CanUseCostOwnerAsync`, else reject `actor-no-longer-authorized`.

### (1) Can an HTTP-reachable path create a job with a non-Manual Trigger and null RequestedByFk, or choose the Trigger?

No. Every LifecycleJob creation site was enumerated (`new LifecycleJob`, `LifecycleJobs.Add`):

- `GameServerLifecycleService.EnqueueActionAsync` (the HTTP start/stop path, GameServersController.cs:287): hard-codes `ELifecycleJobTrigger.Manual` and `RequestedByFk = actorId` from the caller's principal after an Operate check (GameServerLifecycleService.cs:310-329).
- `EnqueueAutomatedActionAsync` (the only way to set a non-Manual trigger): refuses Manual (line 344), accepts only Start/Stop (339), and has exactly two callers, both in the background ServerAutomationWorker pass: ServerAutomationEvaluator.cs:607 (Stop; trigger derived from the persisted pending-stop reason by ToTrigger) and ServerScheduleRunner.cs:343 (Start; trigger hard-coded Schedule). `AutomatedLifecycleRequest` is a plain record used nowhere else. No controller, payload or update DTO carries a trigger; the only trigger member in a DTO is the read-only output `LifecycleJobDto.Trigger`. The two lifecycle controllers call only Create, ChangeContentSelection, Archive, Retry and Skip.
- `RetryAsync` (HTTP, Operate-checked): creates the new job with `RequestedByFk = actorId` and no Trigger assignment, so it defaults to Manual. A retried scheduled start becomes a Manual job with the human as requester and gets the full actor re-check.
- `AutomaticCostStopService` (internal fault handling): creates a Terminate job with `RequestedByFk = null` and the default Manual trigger. It is a Terminate, not a Start, and Manual, so the new guard does not apply.
- `LifecycleJob.RequestedByFk` is `DeleteBehavior.Restrict` (DatabaseContext.cs:493-495), so deleting a user cannot turn a Manual job into a null-requester job.

Residual note (defence in depth, not an exploitable gap): nothing at the database level ties "null requester" to "non-Manual trigger or Terminate"; the invariant is enforced only by the single service entry points above.

### (2) What does the schedule runner check before queueing a scheduled start?

Re-evaluated at fire time inside the per-occurrence transaction under the server-row lock (ServerScheduleRunner.cs:297-358, 435-501), in this order:
1. Rule still exists, occurrence has no run row yet, server not archived (LoadServerIfOpenAsync, 678-696).
2. Within StartGraceWindow (default 15 min), else SkippedMissed.
3. Consent present and `StartConsentCostOwnerFk == server.CurrentCostOwnerFk` (447-458). A cost owner change after the schedule was saved invalidates the consent (SkippedConsent); editing a Start rule clears the consent unless re-confirmed (SettingsService.cs:384-414).
4. No unresolved deployment, no pending lifecycle action.
5. Cost owner exists, is not frozen, and `CanReserveInitialSliceAsync` passes (482-498). A frozen owner after save is skipped (SkippedCost).

Then `EnqueueCoreAsync` (principal null) re-checks: server not archived, game exposes the action, secrets resolvable, IsLaunchDisabled, CurrentCostOwner non-null and not frozen, no unresolved deployment or provider resources, profile and storage plan (GameServerLifecycleService.cs:418-529). Only the `CanUseCostOwnerAsync(principal)` and confirmation-text checks are skipped when no principal exists (lines 430, 448).

Not re-validated at fire time: the schedule author's or consenting user's current permission. `StartConsentByFk` and `UpdatedByFk` are written for audit only and are never read outside the settings service (grep). If the consenting manager later loses server Modify permission or loses the right to use the cost owner, the consent (bound to the cost owner id, not to the person) stays valid until a Start rule is changed, the cost owner changes, or the owner is frozen.

### (3) Do the remaining preflight checks still run for scheduled starts?

Yes. All of these execute before the early return for every start including scheduled ones: provisioning blocked (LifecycleJobExecutor.cs:2927-2928), configuration complete (2929-2941), configuration target conflicts (2942-2974), IsLaunchDisabled or IsArchived (2975-2985), `CurrentCostOwnerFk == deployment.CostOwnerFk`, owner present and not frozen (2986-2998). The authoritative initial cost-slice reservation (`ReserveInitialSliceAsync`, LifecycleJobExecutor.cs:328-333) and its failure handling are unconditional.

### Severity and status

Severity: medium (a cost-bearing action with no present authorizing user). Not reachable by an unauthenticated or under-privileged HTTP caller; the guard is gated on two internally written fields, and the user-initiated variant (null requester, Manual) is still rejected, with a test.

Mitigated: yes for the change itself. It matches the registered design (68-05 trust boundary "system-initiated jobs bypass user authorization by design", T-68-08 "no synthetic principal"), with T-68-27 and T-68-29 plus the retained preflight checks as the compensating controls. Before b9430844 every scheduled start failed `actor-no-longer-authorized`, so the feature could not work in production (68-19-SUMMARY deviation 2).

Advisory (non-blocking, below the high threshold, not counted in threats_open): consent is bound to the cost owner but not re-tied to a person who still holds the right to spend that owner's money. Suggested follow-up for a later phase or a risk decision by the user: at fire time, re-check that `StartConsentByFk` still has server Modify and `CanUseCostOwnerAsync` (IHostingAuthorizationService.RebuildPrincipalAsync already exists), skipping with a new outcome when not; or clear the consent when the consent author's access changes. The auditor cannot accept this risk on the user's behalf, so it is not entered in the Accepted Risks Log.

---

## Unregistered Flags (SUMMARY.md Threat Flags)

All 18 summaries 68-01..68-18 declare "None" and every flag text maps to registered threat IDs (informational only). 68-19-SUMMARY.md has no `## Threat Flags` section, and its deviation 2 (b9430844, a production change) has no threat mapping: logged above as the WARNING `unregistered_flag` T-68-19-X1. Deviation 1 (the 160 GiB emulated Hetzner profile, c2a31186) touches only the Development-gated emulated provider (Program.cs:217-222) and is covered by T-68-43. Deviations 3 and 4 are an AppHost `Uat:Enabled` block value and a test-only change (T-68-43).

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-68-01 | T-68-44 | Mailpit is a local-only development mail sink created only inside the AppHost Uat:Enabled block and never part of a deployment (Phase 66 setup); UAT mail cannot leave the developer machine | Plan 68-19 threat_model (plan-time disposition accept) | 2026-11-22 |
| AR-68-02 | T-68-SC | The phase's plans install no packages. Verified: no csproj, Directory.Packages.props or package-lock.json change in 230029d4..HEAD; only a test:uat npm script was added | Plans 68-01..68-19 threat_model (plan-time disposition accept) | 2026-11-22 |

Note: the Accepted By column records the plan-time disposition, not a named human sign-off. If the project wants a named approver, the user must supply it.

---

## Open Threats

None. 0 open at severity high or above; 0 open below the threshold. One non-blocking advisory is attached to T-68-19-X1 (see above).

---

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-25 | 46 | 46 | 0 | gsd-security-auditor (ASVS level 1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-25

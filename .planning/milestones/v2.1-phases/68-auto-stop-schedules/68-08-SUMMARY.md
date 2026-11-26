---
phase: 68-auto-stop-schedules
plan: 08
subsystem: api
tags: [automation, schedules, consent, optimistic-concurrency, audit, validation]

requires:
  - phase: 68-auto-stop-schedules
    provides: ServerAutomationSettings, ServerSchedule, ServerAutomationState entities (plan 68-01)
  - phase: 68-auto-stop-schedules
    provides: ScheduleCalculator.ToDaysMask and FindZone, ServerAutomationOptions (plan 68-02)
  - phase: 68-auto-stop-schedules
    provides: IServerAutomationProjector.CanManageAsync and ProjectAsync, ServerAutomationTestData (plan 68-06)
provides:
  - PUT game-servers/{id}/automation replacing idle shutdown, maximum runtime, time zone and weekly rules in one call
  - HostingFieldValidationException and a 400 body { error, fields } with field-addressable paths
  - Scheduled-start consent bound to the current cost owner, recorded without a version bump and cleared on Start rule changes
  - AutomationSettingsChanged and ScheduledStartsConfirmed audit entries written in the save transaction
affects: [68-09, 68-10, 68-11, 68-12, 68-13, 68-14, 68-15]

status: complete
actuals:
  tokens: 22000
  tasks: 2
  commits: 3
plan_head_before: 40fdf0f41c721d924123f7585d11234da9870868
plan_head_after: d0d1d06c9937eba7168d3aab1ff0bffc15e800f8

tech-stack:
  added: []
  patterns:
    - "Version claimed with a conditional ExecuteUpdateAsync, mirroring CostAlertSettingsService"
    - "Validation collects every failing field into one HostingFieldValidationException instead of failing on the first"
    - "Private nested records carry the validated, normalized content from Validate to SaveAsync"

key-files:
  created:
    - sources/Api/DTO/Updates/ServerAutomationUpdate.cs
    - sources/Api/DTO/Updates/ServerScheduleRuleUpdate.cs
    - sources/Api/Services/GameServerLifecycle/HostingFieldValidationException.cs
    - sources/Api/Services/ServerAutomation/IServerAutomationSettingsService.cs
    - sources/Api/Services/ServerAutomation/ServerAutomationSettingsService.cs
    - sources/Api/Controllers/GameServerAutomationController.cs
    - tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs
  modified: []

key-decisions:
  - "A save without content change bumps nothing and writes no audit row; consent-only saves update the consent columns under the version condition without incrementing it"
  - "StartConsent=false never revokes consent; it is cleared only when a Start rule is added or a rule becomes a Start rule or changes as a Start rule"
  - "An already valid consent (same cost owner) is not re-recorded unless a Start rule changed, so repeated confirmations do not spam the audit"
  - "Changing the time zone resets EffectiveFrom of every rule (not only changed ones) so a zone change cannot fire a past occurrence"
  - "The evaluator state row is created on the first write that finds it missing, with the schedule watermark at the save instant"

requirements-completed: []

coverage:
  - id: D1
    description: "Owner saves idle shutdown, maximum runtime, zone and weekly rules with one versioned PUT that returns the projected automation block"
    requirement: "AUT-01, AUT-02, AUT-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs#FirstSave_ReturnsVersionOne_AndCreatesSettingsStateAndAudit"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs#Rules_AreSavedInPayloadOrder_WithIdsAndEffectiveFromAtTheSave"
        status: pass
    human_judgment: false
  - id: D2
    description: "Only a viewer with the manage right saves; non-viewers get 404, archived 409, stale version 409"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs#ViewerWithoutManageRight_IsForbidden"
        status: pass
    human_judgment: false
  - id: D3
    description: "Saving never makes a past occurrence fire: new or changed rules become effective at the save time and the first save sets the schedule watermark"
    requirement: "AUT-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs#ChangingTheTimeZone_ResetsEveryRulesEffectiveFrom"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-11-22
---

# Phase 68 Plan 08: Save Server Automation Settings Summary

One authorized, validated, optimistically versioned `PUT game-servers/{id}/automation` that replaces idle shutdown, maximum runtime, the IANA time zone and the weekly rules, records scheduled-start consent bound to the cost owner, audits every content change and returns the projected automation block.

## Accomplishments

- Tracer (Task 1): payload records, `HostingFieldValidationException`, settings service and controller. First save inserts at version 1 and creates the evaluator state row with `SchedulesEvaluatedThrough` at the save instant; later saves claim `Version + 1` with a conditional `ExecuteUpdateAsync`; an unchanged save is a no-op.
- Authorization by route id only: no View permission gives 404, View/Operate without `CanManageAsync` gives 403, archived gives 409 "The server is archived.", stale or premature version gives 409, `DbUpdateException` maps to 409.
- Validation (Task 2) collects every failing field: idle and runtime ranges only while enabled (a disabled out-of-range number keeps the stored or default value), idle only for games that report players, zone known to TZDB (max 64 chars) and required with any rule, rule cap, days 1..7 distinct and non-empty, strict `HH:mm`, action defined, rule ids that belong to this server, duplicate day plus minute reported on both rules' time fields, `startConsent` without a Start rule.
- Rule diff by id: new rules get API ids and `EffectiveFrom = save time`; changed action, days or time reset only that rule; unchanged rules keep `EffectiveFrom`; rules missing from the payload are deleted with their runs; `Position` follows payload order.
- Consent: records cost owner, user, instant and hourly estimate and writes `ScheduledStartsConfirmed` (StartsCost impact) without bumping the version; clears the stored consent when a Start rule is added or changed without consent; Stop-only changes keep it.
- 24 test attributes (33 test cases) in `ServerAutomationSettingsTests`; the whole `ServerAutomation` and `GameServerHosting` filter (1375 tests) passes.

## Task Commits

1. Task 1 (tracer): `b374d378` feat(api): save server automation settings
2. Task 2: `6673561e` feat(api): validate schedules and record start consent
3. Task 2 tests: `d0d1d06c` test(api): cover server automation settings

## Deviations from Plan

### Auto-fixed / judgment calls

**1. [Rule 2 - Missing critical functionality] Time zone change resets every rule's EffectiveFrom**
- **Found during:** Task 2 design (AUT-06)
- **Issue:** The plan resets `EffectiveFrom` only for new or changed rules. Another zone moves every occurrence, so an unchanged rule could produce an occurrence between the evaluator watermark and the save instant.
- **Fix:** When the stored zone differs from the saved zone, all rules become effective at the save time. Covered by `ChangingTheTimeZone_ResetsEveryRulesEffectiveFrom`.
- **Files modified:** `sources/Api/Services/ServerAutomation/ServerAutomationSettingsService.cs`
- **Commit:** 6673561e

**2. [Rule 3 - Blocking] Zone validation and storage moved into Task 1**
- **Found during:** Task 1 test run (the tracer test expects the zone `UTC` back)
- **Fix:** Zone parsing via `ScheduleCalculator.FindZone` and storage of the canonical zone id landed in the tracer commit; Task 2 added the "rules need a zone" rule.
- **Commit:** b374d378

None of the above changes behavior outside the plan's stated intent.

## Authentication gates

None.

## Known Stubs

None.

## Threat Flags

None. The new endpoint is the one the plan's threat model (T-68-17 to T-68-20) covers: route-id authorization, consent bound to the cost owner id, server-side validation, version claim.

## Notes for downstream plans

- `sources/ReactApp/openapi/utf-api.json` and the generated Kiota clients are not regenerated here (the plan forbids changes under `sources/ReactApp` and `sources/WebApp`); the client-facing plans must regenerate the OpenAPI snapshot to pick up `PUT game-servers/{id}/automation`.
- Requirements AUT-01, AUT-02, AUT-03 and AUT-06 are not marked complete in REQUIREMENTS.md: this plan delivers the write path only; the workspace UI plans complete the user-visible parts.
- Idle shutdown availability and the schedule limits are read from the existing `ServerAutomationOptions`; no new configuration was added.

## Self-Check: PASSED

- FOUND: sources/Api/DTO/Updates/ServerAutomationUpdate.cs
- FOUND: sources/Api/DTO/Updates/ServerScheduleRuleUpdate.cs
- FOUND: sources/Api/Services/GameServerLifecycle/HostingFieldValidationException.cs
- FOUND: sources/Api/Services/ServerAutomation/IServerAutomationSettingsService.cs
- FOUND: sources/Api/Services/ServerAutomation/ServerAutomationSettingsService.cs
- FOUND: sources/Api/Controllers/GameServerAutomationController.cs
- FOUND: tests/Api.Tests/ServerAutomation/ServerAutomationSettingsTests.cs
- FOUND commits: b374d378, 6673561e, d0d1d06c

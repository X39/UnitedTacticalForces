---
phase: 68-auto-stop-schedules
plan: 07
subsystem: api
tags: [cost-alerts, notifications, discord, email, localization, websockets, auto-stop]

requires:
  - phase: 68-auto-stop-schedules
    provides: CostAlert.Kind, PendingStopFk, AutomationPendingStop, ECostAlertKind, EAutoStopReason (plan 68-01)
  - phase: 66-cost-alerts
    provides: alert outbox, recipient resolver, deterministic Message-Id, per-viewer dismissal
provides:
  - AutoStop as a second kind of the Phase 66 cost alert record, written by IAutoStopAlertWriter
  - Resolution that skips undelivered rows as stop-resolved and sends dismissal frames to the owner audience
  - Kind and stop facts on CostAlertDto and the raised socket frame
  - English and German AutoStop Discord and email text through the unchanged delivery processor
affects: [68-10, 68-12, 68-13, 68-14, 68-15]

status: complete
actuals:
  tokens: 70000
  tasks: 3
  commits: 4
plan_head_before: c00145f8efa1c73a01bede1aba6d1e40781e0bbe
plan_head_after: acc7ceae1dc89da5bcb5b443178414cbbaef1df4

tech-stack:
  added: []
  patterns:
    - "Delivery creation extracted into CostAlertDeliveryFactory and shared by threshold and AutoStop alerts"
    - "Writer methods add to the context and never save, so the caller commits alert, pending stop and audit together"
    - "Socket contract carries string-typed Kind and StopReason because WebSocketContracts does not reference HostingContracts"

key-files:
  created:
    - sources/Api/Services/GameServerLifecycle/CostAlertDeliveryFactory.cs
    - sources/Api/Services/GameServerLifecycle/IAutoStopAlertWriter.cs
    - sources/Api/Services/GameServerLifecycle/AutoStopAlertWriter.cs
    - sources/Api/Services/GameServerLifecycle/ResolvedCostAlert.cs
    - sources/Api/Services/Notifications/AutoStopMessageModel.cs
    - sources/Api/Services/Notifications/AutoStopMessageKeys.cs
    - tests/Api.Tests/GameServerHosting/AutoStopAlertWriterTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/CostAlertEvaluator.cs
    - sources/Api/Services/GameServerLifecycle/RaisedCostAlert.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertDeliveryReasons.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertPublisher.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertService.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertDeliveryProcessor.cs
    - sources/Api/Services/Notifications/CostAlertMessageRenderer.cs
    - sources/Api/DTO/CostAlertDto.cs
    - sources/WebSocketContracts/Hosting/CostAlertRaisedMessage.cs
    - sources/Api/Properties/Language.resx
    - sources/Api/Properties/Language.de.resx
    - tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertMessageRendererTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertResourceTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertEmailDeliveryTests.cs

key-decisions:
  - "AutoStop alerts carry ThresholdAmount 0 and AccruedAmount 0; the evaluator counts only Kind = CostThreshold rows as recorded crossings"
  - "A cost owner without a CostAlertSetting gets both channels skipped as channel-disabled while the in-app record is always created"
  - "ListAsync hides an AutoStop alert once its pending stop is no longer Pending; dismissal stays per viewer, so another holder of a group cost owner still sees it"
  - "A delivery already claimed for sending may still arrive after resolution (at-least-once); the text is neutral about whether the stop still happens"
  - "The warning link appends ?tab=settings#server-automation to the existing server link template"

patterns-established:
  - "AutoStop text lives under AutoStop_* keys in Language.resx and Language.de.resx; Language.Designer.cs is untouched and the renderer reads through ResourceManager"

requirements-completed: []

coverage:
  - id: D1
    description: "A pending stop becomes an AutoStop alert with Phase 66 deliveries and shows in the alert list with its stop facts"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/AutoStopAlertWriterTests.cs"
        status: pass
  - id: D2
    description: "A resolved warning is withdrawn from undelivered channels, the list and open sockets; dismissal is per viewer and never cancels the stop"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "AutoStopAlertWriterTests, CostAlertServiceTests, CostAlertPublisherTests"
        status: pass
  - id: D3
    description: "English and German sanitized AutoStop Discord and email text through the unchanged outbox with a deterministic Message-Id"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "CostAlertMessageRendererTests, CostAlertResourceTests, CostAlertEmailDeliveryTests (full Api.Tests: 1497 pass)"
        status: pass
---

# Phase 68 Plan 07: AutoStop Alert Kind Summary

**A pending automatic stop is now a second kind of the Phase 66 alert record, delivered through the existing Discord and email outbox in English and German, withdrawn when the stop is cancelled, and dismissible per viewer.**

## Accomplishments

- Extracted `CreateDeliveries` and `CreateDelivery` from `CostAlertEvaluator` into `CostAlertDeliveryFactory` (null setting means both channels disabled). The evaluator now counts only `CostThreshold` rows as recorded crossings.
- `AutoStopAlertWriter.AddWarningAsync` adds one alert (Kind AutoStop, `PendingStopFk`, amounts 0) with its deliveries to the caller's context and never saves. `ResolveAsync` marks still-Pending deliveries Skipped with the new `stop-resolved` reason and returns a `ResolvedCostAlert`.
- `RaisedCostAlert`, `CostAlertDto` and `CostAlertRaisedMessage` gained the kind and stop facts. `ICostAlertPublisher.PublishResolvedAsync` sends one `CostAlertDismissedMessage` per resolved alert to the cost owner view audience, best effort.
- `CostAlertDeliveryProcessor` loads the stop facts and renders AutoStop deliveries with `RenderAutoStop`. Seven `AutoStop_*` keys exist in both resource files.

## Task Commits

1. Delivery factory extraction: `c3b0ec47` refactor(api): share cost alert delivery creation
2. Tracer and resolution (writer, publisher, DTO, list filter, tests): `4910ad93` feat(api): record and withdraw automatic shutdown warnings
3. Rendering: `91973841` feat(api): render automatic shutdown warnings in English and German
4. Tests for rendering, resource parity and email delivery: `acc7ceae` test(api): cover automatic shutdown warning alerts

## Deviations from Plan

- **Task 1 and Task 2 commits merged.** The writer's `ResolveAsync`, `ResolvedCostAlert`, `StopResolved` and the publisher interface member were needed for Task 1 to compile (the test double implements the publisher interface), so both tasks landed in one commit. Planned subject `feat(api): record automatic shutdown warnings as alerts` was folded into `record and withdraw`.
- **Extra test coverage.** An AutoStop delivery test was added to `CostAlertEmailDeliveryTests` (not in the plan's file list) because it owns the processor test helpers; resource parity for the new keys was added to `CostAlertResourceTests`.

No other deviations. No Rule 1-3 fixes were needed.

## Notes for Dependent Plans

- **OpenAPI snapshot and generated client not regenerated.** The plan forbids changes under `sources/ReactApp`, which holds `openapi/utf-api.json` and `src/generated/api.ts`. `CostAlertDto` gained `Kind`, `StopsAt`, `StopReason` and `StopLimit`, so the snapshot needs regenerating by the plan that owns the frontend contract (or a final regeneration step). All existing Api tests pass.
- **Plan 68-10 call contract.** Call `AddWarningAsync` and `ResolveAsync` inside its transaction, save, then publish: `PublishRaisedAsync` with the returned record and `PublishResolvedAsync` with the resolved record.
- Requirement AUT-04 is not marked complete: the in-app notice and menu rows (frontend plans) are still outstanding. REQUIREMENTS.md was not changed.

## Known Stubs

None.

## Threat Flags

None. All new surface (frames, message text) is covered by T-68-14 (title sanitized with CR/LF test), T-68-15 (one alert per pending stop, undelivered rows skipped on resolution) and T-68-16 (recipients from the resolver, frames to the cost owner audience only).

## Self-Check: PASSED

- Created files exist: CostAlertDeliveryFactory.cs, IAutoStopAlertWriter.cs, AutoStopAlertWriter.cs, ResolvedCostAlert.cs, AutoStopMessageModel.cs, AutoStopMessageKeys.cs, AutoStopAlertWriterTests.cs.
- Commits c3b0ec47, 4910ad93, 91973841, acc7ceae are on the branch; full `Api.Tests` run: 1497 passed, 0 failed.
- `Language.Designer.cs`, `sources/Database`, `sources/ReactApp`, `sources/WebApp` unchanged versus the base commit.

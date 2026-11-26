---
phase: 66-accrued-cost-cost-alerts
plan: 07
subsystem: api
tags: [cost-alerts, outbox, dispatcher, discord, email, retry, lease, hosted-service, dotnet]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-02 Discord DM and SMTP adapters with typed outcomes, localized message renderer
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-03 CostAlertDelivery outbox rows, CostAlertOptions, CostAlertDeliveryReasons
provides:
  - "ICostAlertDeliveryProcessor/CostAlertDeliveryProcessor: leased claim, render, send and outcome write for one batch of outbox rows"
  - "CostAlertDispatcher hosted loop (5 s interval, immediate next pass on a full batch), registered only while the control plane is unpaused"
  - "RecordingDiscordDirectMessageSender and RecordingEmailSender test fakes"
affects: [66-08, 66-09, 66-10, 66-11, 66-12, uat-profile]

plan_head_before: 1a2b3b4d56b60bc445c905a557adf6f86f07d463
plan_head_after: 5258c31f61dbc4e14af85798369929db49269e35

actuals:
  tokens: 18500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Claim before send: a conditional ExecuteUpdateAsync persists Sending, the lease and the attempt count; the outcome write is conditional on Status == Sending and the current lease owner"
    - "Only ids and fixed reason codes are logged or stored; exceptions raised while sending become the fixed reason dispatch-error"

key-files:
  created:
    - sources/Api/Services/GameServerLifecycle/ICostAlertDeliveryProcessor.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertDeliveryProcessor.cs
    - sources/Api/HostedServices/CostAlertDispatcher.cs
    - tests/Api.Tests/GameServerHosting/RecordingDiscordDirectMessageSender.cs
    - tests/Api.Tests/GameServerHosting/RecordingEmailSender.cs
    - tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertEmailDeliveryTests.cs
  modified:
    - sources/Api/Services/GameServerLifecycle/HostingControlPlaneServiceCollectionExtensions.cs
    - tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs

key-decisions:
  - "Retry delay for attempt n is DeliveryRetryDelays[clamp(n - 1)]; the fifth transient result (MaxDeliveryAttempts) fails the row with retries-exhausted instead of scheduling a sixth attempt"
  - "The outcome write uses CancellationToken.None so a shutdown cannot lose a Sent write after a successful send; a cancellation during the send itself propagates and the lease expiry recovers the row"
  - "Email checks channel configuration (IsConfigured) before the recipient address, so an unconfigured installation skips with not-configured regardless of the address"
  - "Recipient and alert facts are read with two small projections (no Include), so the avatar blob of the user is never loaded"
  - "Server link is built only when BaseUrl.ClientUrl is an absolute URI; otherwise the message uses the no-link sentence"

patterns-established:
  - "Stale-process safety: every outcome write requires Status == Sending and this process's lease owner, proven by a test in which the lease is taken over mid-send"

requirements-completed: [CST-05, CST-06]

coverage:
  - id: D1
    description: "A pending Discord row is claimed with a lease, delivered as one DM in the recipient's language, recorded Sent with attempt count 1 and no lease, and never claimed again"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#PendingDiscordRow_IsSentOnceInRecipientLanguage_AndNeverClaimedAgain"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#TerminalRows_AreNeverClaimed"
        status: pass
    human_judgment: false
  - id: D2
    description: "Transient failures retry after 30 s, 2 min, 10 min and 1 h and fail after the fifth attempt; permanent failures fail at once; unconfigured, unlinked, addressless and inactive recipients are skipped with reason codes"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#TransientResults_RetryWithBackoff_AndFailAfterFifthAttempt"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#PermanentResult_FailsAfterOneAttempt"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#InactiveRecipient_IsSkippedAsRecipientInactive"
        status: pass
    human_judgment: false
  - id: D3
    description: "An expired foreign lease is reclaimed and sent, an unexpired lease is untouched, a stale outcome write changes nothing, and a cancellation mid-send leaves the lease for recovery"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#SendingRow_WithExpiredForeignLease_IsReclaimedAndSent"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#OutcomeWrite_WithoutTheLease_ChangesNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertDispatcherTests.cs#CancellationDuringSend_PropagatesAndLeavesRowLeased"
        status: pass
    human_judgment: false
  - id: D4
    description: "Email is skipped while unconfigured, one email per recipient and alert with a deterministic Message-Id in the recipient's language, server link only for an absolute client URL, and logs contain no address, subject, body or exception text"
    requirement: "CST-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertEmailDeliveryTests.cs"
        status: pass
    human_judgment: false
  - id: D5
    description: "CostAlertDispatcher is registered only while the control plane is unpaused"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/HostingControlPlaneStateTests.cs#ExplicitSetting_ControlsBackgroundServiceRegistration"
        status: pass
    human_judgment: false
  - id: D6
    description: "The dispatcher loop itself, concurrent dispatcher instances against PostgreSQL, and real Discord and SMTP sends are not exercised by an automated test (claim race is covered by the conditional update only on SQLite)"
    requirement: "CST-05"
    verification: []
    human_judgment: true

duration: about 25min
completed: 2026-11-21
---

# Phase 66 Plan 07: Cost Alert Delivery Dispatcher Summary

**A hosted dispatcher turns the cost alert outbox into Discord DMs and emails: rows are claimed with a leased conditional update before sending, outcomes are written only by the lease holder, Sent is terminal, and retries are bounded at five attempts with 30 s, 2 min, 10 min and 1 h backoff.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 7 created, 2 modified

## Accomplishments

- Tracer slice end to end: processor, hosted dispatcher, registration inside the `!state.IsPaused` block, two recording fakes, and tests for Sent-terminal, future next attempt and terminal rows.
- Full outcome matrix: bounded backoff, retries-exhausted, permanent failure, the four skip reasons, lease recovery, stale-lease protection, unexpected exception to `dispatch-error`, cancellation mid-send.
- Email guarantees: skipped while unconfigured, one localized email per recipient with a deterministic Message-Id, link only with an absolute client URL, and a capturing-logger test showing no address, subject, body, title or exception text in logs.

## Task Commits

1. **Task 1: Tracer, dispatch cost alert deliveries from the outbox** - `84016da7` (feat)
2. **Task 2: Bounded retries, permanent failures, lease recovery** - `2044705a` (feat)
3. **Task 3: One email per recipient, no personal data in logs** - `5258c31f` (test)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostAlertEmailDeliveryTests|FullyQualifiedName~CostAlertDispatcherTests"`: 23 passed, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 923 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- Acceptance greps: `ExecuteUpdateAsync` 2, `AddHostedService<CostAlertDispatcher>` 1, `WithDiscordAsync` 0, `RetriesExhausted` 1, `DeliveryRetryDelays` 1, dispatcher tests 13 (at least 9), email tests 5 (at least 5).
- No file under `sources/ReactApp`, `sources/WebApp` or `sources/Database` was touched; `HostingTestData.cs` was not modified.

## Deviations from Plan

None - plan executed as written. Small choices within the plan's discretion are listed under key-decisions (outcome write not cancellable, projections instead of Include, `IsConfigured` checked before the address). `RecordingEmailSender` gained a `ThrowOnSend` switch in Task 3 so the log test could exercise the exception path; it is a test fake owned by this plan.

## Known Stubs

None.

## Threat Flags

None. All surfaces are those of the plan's threat model: T-66-23 (claim predicate excludes terminal rows, Sent written under the lease, deterministic Message-Id), T-66-24 (ids and fixed codes only, capturing-logger test), T-66-25 (five attempts, bounded delays), T-66-26 (outcome write requires `Status == Sending` and the lease owner, test with a mid-send lease takeover).

## Issues Encountered

- The pre-existing failure `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` (see `deferred-items.md`) was not touched; it is outside the `GameServerHosting` filter used for verification.
- Concurrent claiming by two real dispatcher instances on PostgreSQL is not exercised; the atomic conditional update is verified on SQLite only.

## Self-Check: PASSED

- All 7 created files and 2 modified files exist (verified by the successful build and test run).
- Commits `84016da7`, `2044705a`, `5258c31f` are ancestors of HEAD.

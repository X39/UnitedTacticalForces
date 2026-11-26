---
phase: 66-accrued-cost-cost-alerts
plan: 02
subsystem: api
tags: [notifications, discord, email, smtp, mailkit, localization, dotnet]

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-01 accrued cost and cost-owner audience (consumed later by the dispatcher in 66-07)
provides:
  - "CostAlertMessageRenderer: localized EN/DE subject and plain-text body from API resource files"
  - "DiscordBot.TrySendDirectMessageAsync and IDiscordDirectMessageSender: outcome-typed Discord DM"
  - "IEmailSender/SmtpEmailSender on MailKit with EmailOptions (Email:Smtp), deterministic Message-Id"
  - "EDeliveryAttemptOutcome/DeliveryAttemptResult: Sent, Transient, Permanent, NotConfigured with fixed reason codes"
affects: [66-04, 66-07, cost-alert-dispatcher, uat-profile]

plan_head_before: 6becf5a5414763b889a0706d88a70014c7821a5f
plan_head_after: 049cb43ce7e465593feb893b28a13f3f0303058b

actuals:
  tokens: 13400
  tasks: 3
  commits: 3

tech-stack:
  added: [MailKit 5.0.0 (brings MimeKit.Core 5.0.0)]
  patterns:
    - "Channel adapters return a typed outcome with a fixed reason code, never an exception message"
    - "Localized end-user text lives in Language.resx/Language.de.resx, read with constant keys through Language.ResourceManager"

key-files:
  created:
    - sources/Api/Services/Notifications/EDeliveryAttemptOutcome.cs
    - sources/Api/Services/Notifications/DeliveryAttemptResult.cs
    - sources/Api/Services/Notifications/MessageLocales.cs
    - sources/Api/Services/Notifications/CostAlertMessageKeys.cs
    - sources/Api/Services/Notifications/CostAlertMessageModel.cs
    - sources/Api/Services/Notifications/CostAlertMessage.cs
    - sources/Api/Services/Notifications/CostAlertMessageRenderer.cs
    - sources/Api/Services/Notifications/IDiscordDirectMessageSender.cs
    - sources/Api/Services/Notifications/DiscordDirectMessageSender.cs
    - sources/Api/Services/Notifications/EmailOptions.cs
    - sources/Api/Services/Notifications/EmailMessage.cs
    - sources/Api/Services/Notifications/IEmailSender.cs
    - sources/Api/Services/Notifications/SmtpEmailSender.cs
    - sources/Api/Services/Notifications/SmtpFailureClassifier.cs
    - tests/Api.Tests/GameServerHosting/CostAlertMessageRendererTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertResourceTests.cs
    - tests/Api.Tests/GameServerHosting/DiscordDirectMessageSenderTests.cs
    - tests/Api.Tests/GameServerHosting/SmtpEmailSenderTests.cs
  modified:
    - sources/Api/HostedServices/DiscordBot.cs
    - sources/Api/Properties/Language.resx
    - sources/Api/Properties/Language.de.resx
    - sources/Api/Api.csproj
    - sources/Api/Program.cs
    - sources/Api/appsettings.json
    - sources/Api/appsettings.secret.json_template

key-decisions:
  - "Discord DMs never use WithDiscordAsync; a disconnected bot reports Transient, a disabled bot NotConfigured, Discord codes 50007 and 50278 Permanent, mentions are always disabled"
  - "Email is at-least-once with a deterministic Message-Id {alertId:N}.{recipientId:N}@{domain}"
  - "Amounts render as N2 in the recipient culture plus the ISO code; times as the culture's short date/time in UTC"
  - "A 5xx SMTP reply is Permanent and a 4xx reply Transient; authentication, network, protocol and timeout failures are Transient with fixed reasons"
  - "An unparsable recipient address is Permanent (invalid-recipient) and an invalid Email:Smtp section skips delivery as NotConfigured (invalid-configuration)"

patterns-established:
  - "Fixed reason codes (discord-not-connected, discord-dm-blocked, smtp-rejected-NNN, ...) are the only failure detail that is persisted or logged"

requirements-completed: [CST-05, CST-06]

coverage:
  - id: D1
    description: "A cost alert renders to English or German subject and body from API resource files by the recipient locale, with English fallback and culture-correct amounts"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertMessageRendererTests.cs"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertResourceTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "Discord DM adapter reports NotConfigured while disabled and Transient while disconnected, never Sent"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/DiscordDirectMessageSenderTests.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "SMTP sender is cleanly unavailable while unconfigured, builds a deterministic injection-safe message, classifies failures to fixed reasons and leaks no configuration values"
    requirement: "CST-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/SmtpEmailSenderTests.cs"
        status: pass
    human_judgment: false
  - id: D4
    description: "Permanent Discord rejection (codes 50007, 50278) and a real Discord or SMTP send are not exercised by an automated test"
    requirement: "CST-05"
    verification: []
    human_judgment: true

duration: 45min
completed: 2026-11-21
status: complete
---

# Phase 66 Plan 02: Cost Alert Delivery Channels Summary

**A localized EN/DE cost alert renderer plus two outcome-typed delivery adapters: a Discord DM through the existing bot that never claims success while disconnected, and a MailKit SMTP sender that is cleanly skipped while unconfigured.**

## Performance

- **Duration:** about 45 min (including the blocking checkpoint wait)
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 1 checkpoint, 1 auto/tdd)
- **Files:** 18 created, 7 modified

## Accomplishments

- Tracer slice end to end: resource text, renderer, `TrySendDirectMessageAsync`, adapter and tests.
- `SmtpEmailSender` with a deterministic Message-Id, header-injection-safe subject and sender name, default STARTTLS and no certificate-validation override.
- `Email:Smtp` defaults in `appsettings.json` and empty `Username`/`Password` placeholders in the secret template; no secret value committed.

## Task Commits

1. **Task 1: Tracer, render cost alerts and send them as Discord DMs** - `f053b990` (feat)
2. **Task 2: Human legitimacy checkpoint (MailKit 5.0.0, Mailpit)** - no commit; approved by the human, see below
3. **Task 3a: Add the MailKit package** - `1e5108f8` (build)
4. **Task 3b: Send cost alert emails over SMTP** - `049cb43c` (feat)

## Checkpoint Approval (record for plan 66-04)

The human user approved the Task 2 checkpoint themselves (answer "Approve both" to a direct question), relayed by the orchestrator on 2026-11-21. A first relay that was only the orchestrator's own registry check was not treated as approval; nothing was installed until the human approval arrived.

Resume signal, verbatim: **"approved mailpit:v1.34.0"** (human approved MailKit 5.0.0 on 2026-11-21).

For plan 66-04: use the Docker image `axllent/mailpit` with the exact tag `v1.34.0`, never `latest` or `edge`; development only.

Orchestrator-reported registry facts: MailKit 5.0.0 is owned by jstedfast (verified prefix), repository github.com/jstedfast/MailKit; declared dependencies MimeKit.Core 5.0.0, System.Formats.Asn1 10.0.0, Microsoft.Bcl.AsyncInterfaces 10.0.0, System.Threading.Tasks.Extensions 4.6.3.

## Package Graph Guard (T-66-SC)

`dotnet list sources/Api/Api.csproj package --include-transitive` was saved before and after the install. The only new entries were `MailKit 5.0.0` (top level) and `MimeKit.Core 5.0.0` (transitive). The other declared dependencies were already present in the graph. Nothing else was added.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SmtpEmailSenderTests|FullyQualifiedName~CostAlertMessageRendererTests|FullyQualifiedName~CostAlertResourceTests|FullyQualifiedName~DiscordDirectMessageSenderTests"`: 41 passed, 0 failed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 816 passed, 0 failed.
- `dotnet build X39.UnitedTacticalForces.slnx`: succeeded, 0 errors.
- Guard greps: `ServerCertificateValidationCallback` 0, `System.Net.Mail` 0, `Language.Designer.cs` untouched.
- No file under `sources/ReactApp`, `sources/WebApp` or `sources/Database` was touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] MailKit 5 send API**
- **Found during:** Task 3
- **Issue:** `SmtpClient.SendAsync(MimeMessage, ...)` does not exist in MailKit 5; only `SendAsync(ISendRequest, ...)` does (CS1503).
- **Fix:** Send `new SendRequest(mimeMessage)`.
- **Files modified:** `SmtpEmailSender.cs`
- **Commit:** `049cb43c`

**2. [Rule 1 - Bug] CRLF in a subject produced two spaces**
- **Found during:** Task 3 test run
- **Issue:** Replacing `\r` and `\n` separately turned `\r\n` into two spaces.
- **Fix:** A generated regex collapses each line-break run into one space.
- **Files modified:** `SmtpEmailSender.cs`
- **Commit:** `049cb43c`

**3. [Rule 3 - Blocking] `dotnet add package` rewrote unrelated csproj lines**
- **Found during:** Task 3
- **Issue:** The tool changed line endings and reformatted `/>` on unrelated lines.
- **Fix:** Restored `Api.csproj` and added only the single `PackageReference` line.
- **Files modified:** `Api.csproj`
- **Commit:** `1e5108f8`

**4. [Rule 2 - Missing critical] Small hardening beyond the plan text**
- `SanitizeTitle` replaces control characters with a space before collapsing whitespace (so `Ops\r\nNight` reads "Ops Night" instead of "OpsNight").
- An unparsable recipient returns Permanent `invalid-recipient` instead of retrying forever.
- `EmailOptions.Validate` also rejects a `MessageIdDomain` containing `@`, whitespace or control characters.
- The `HttpException` type needed the `Discord.Net` namespace import; the internal outcome enum cannot be a public xUnit theory parameter, so the classifier test passes the outcome name as a string.

**Total deviations:** 4 groups, all auto-fixed. No scope change.

## Known Stubs

None.

## Threat Flags

None. The surfaces added (SMTP egress, Discord DM) are the ones named in the plan's threat model (T-66-04 to T-66-07, T-66-SC), each with a covering test or guard.

## Issues Encountered

None beyond the deviations above. A real Discord DM and a real SMTP send are not exercised by automated tests; plan 66-04 provides the Mailpit development sink for manual verification.

## Self-Check: PASSED

- All 18 created files and 7 modified files exist.
- Commits `f053b990`, `1e5108f8`, `049cb43c` are ancestors of HEAD.

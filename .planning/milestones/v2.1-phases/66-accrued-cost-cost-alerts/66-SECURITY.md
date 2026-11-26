---
phase: "66"
slug: "accrued-cost-cost-alerts"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-22"
---

# Phase 66 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| API to Discord | The bot sends a DM with user-supplied server titles |
| API to SMTP server | Credentials and recipient addresses leave the process over TLS |
| Accrued value to cost alerts | The same value decides when an owner is warned about cost |
| Alert UI to cost-stop actions | A click on an alert may lead to a destructive, cost-relevant action |
| Alert data to the toast region | Server names (user data) appear in dismissal toasts |
| Alert settings DTO to the page | The API's channel availability decides which badge the owner sees |
| Browser form to settings API | The draft is sent to an owner-authorized endpoint |
| Browser to accrued-cost endpoint | Authenticated members request a server's accrued cost by route id |
| Browser to cost-alert endpoints | Authenticated users list and dismiss alerts by id |
| Browser to locale endpoint | A signed-in user records a preference on their own record |
| Browser to settings endpoints | Authenticated users read and write an owner's alert configuration |
| Client to dismiss-all endpoint | Untrusted alert ids decide which alerts are dismissed for the caller |
| Container registry to developer machine | The Mailpit image is pulled during UAT |
| Control plane to REST and socket viewers | Billing facts reveal session timing next to the amount |
| Control plane to later push audiences | The cost-owner view audience decides who receives cost values over the update stream |
| Control plane to sockets | Cost values and alert records leave the API on per-user paths |
| Development harness to production | Seed data and the SMTP sink must never reach a deployed environment |
| Dispatcher to logs | Delivery outcomes are logged for operators |
| Finance page to settings API | Finance viewers load another owner's alert settings |
| Generated contract to repository | Tool output is the source of the client types |
| Layout to locale endpoint | A signed-in user's language choice is sent to the API |
| Ledger rows to accrued value | The durable ledger provider identifier decides which billing unit prices a session |
| Ledger to alert records | Durable cost data drives the creation of alert and outbox rows |
| NuGet registry to repository | A new runtime package and its dependencies enter the API |
| Outbox to external channels | Persisted delivery rows become Discord DMs and SMTP emails |
| Owner type to recipients | Claim data decides who will receive Discord and email messages |
| Socket frames to client cache | Untrusted JSON frames are written into the query cache |
| Socket frames to the accrued cache | Untrusted frame fields reach the strip |
| Socket frames to the alerts cache | Untrusted alert frames create visible rows and toasts |
| Test harness to the app | Mocked API responses and frames stand in for the control plane |
| UAT profile to developer machine | Dev-only seeding and Mailpit run locally |
| URL parameter to page | `owner` is user-controlled |

---

## Threat Register

Rows are taken verbatim from the `<threat_model>` blocks of plans 66-01 to 66-18. T-66-SC is `mitigate` in plan 66-02 (MailKit 5.0.0 and the Mailpit tag, human-approved) and `accept` in every other plan (no package installed).

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-66-01 | Information disclosure | GET game-servers/{id}/accrued-cost | high | mitigate | `CanViewCostOwnerAsync` on the session's cost owner; a viewer without owner visibility gets amount null and reason hidden; covered by AccruedCostEndpointTests | closed |
| T-66-02 | Information disclosure | GetCostOwnerViewAudienceAsync | high | mitigate | Drift test proves equality with `CanViewCostOwnerAsync` for every claim class; deleted and banned users excluded; unrequested values ignored (fail closed) | closed |
| T-66-03 | Elevation of privilege | accrued-cost route id | medium | mitigate | `[Authorize(Claims.ResourceBased.Server.View)]` plus a service-level `HasServerPermission` check; unknown or unviewable servers return 404 | closed |
| T-66-04 | Information disclosure | SmtpEmailSender, EmailOptions, DiscordBot logs | high | mitigate | Logs carry outcome and fixed reason codes only; `Validate` messages contain no values; credentials live only in `appsettings.secret.json` (template has empty placeholders); classifier reasons never include exception text | closed |
| T-66-05 | Tampering | email subject (header injection via server title) | medium | mitigate | `SanitizeTitle` strips control characters; MimeKit encodes headers; renderer test with CR/LF/tab title | closed |
| T-66-06 | Spoofing | Discord DM mass mention via server title | medium | mitigate | `AllowedMentions.None` on every DM; acceptance grep gate | closed |
| T-66-07 | Information disclosure | SMTP transport | medium | mitigate | Default `SecureSocketOptions.StartTls`; no certificate-validation override (guard grep); None and Auto documented as development-only | closed |
| T-66-08 | Repudiation | duplicate alerts after restart or concurrent passes | medium | mitigate | Unique index (DeploymentFk, ThresholdAmount) plus a transaction for alert and delivery rows; `DbUpdateException` handled as already recorded; restart test | closed |
| T-66-09 | Denial of service | alert flooding | low | mitigate | One alert per threshold and session; the threshold count is capped by `MaxThresholds` (enforced on save in plan 66-06); unique delivery rows | closed |
| T-66-10 | Information disclosure | group-owner recipients | medium | mitigate | Only active co:use holders (direct or role) receive messages; co:view and finance staff excluded; resolver tests include banned and view-only users | closed |
| T-66-11 | Denial of service | evaluation blocking lifecycle work | low | mitigate | Evaluation runs in its own hosted worker and transaction, never inside the cost reconciler or the lifecycle executor's locks | closed |
| T-66-12 | Elevation of privilege | UAT seeding and Mailpit in production | medium | mitigate | Seeder runs only in Development with `Testing:UatScenarios:Enabled`; Mailpit exists only inside the AppHost `Uat:Enabled` block; no production configuration file is changed | closed |
| T-66-13 | Tampering | axllent/mailpit image | medium | mitigate | Tag pinned to the version a human approved at the plan 66-02 checkpoint; guard gate rejects latest and edge; development-only | closed |
| T-66-14 | Information disclosure | accrued value frames | high | mitigate | Recipients are the intersection of the server view and cost-owner view audiences; fail closed on audience errors; publisher tests include a server viewer without owner visibility | closed |
| T-66-15 | Elevation of privilege | POST cost-alerts/{id}/dismissal | high | mitigate | `CanViewCostOwnerAsync` on the alert's owner; invisible or unknown ids return 404 (no probing); tests | closed |
| T-66-16 | Denial of service | concurrent sends dropping sockets | medium | mitigate | Per-socket `SemaphoreSlim` in `UpdateStreamService`; concurrency test | closed |
| T-66-17 | Information disclosure | GET cost-alerts | high | mitigate | Owner filter from `GetViewableCostOwnerIdsAsync`, drift-tested against `CanViewCostOwnerAsync` for every claim class | closed |
| T-66-18 | Elevation of privilege | PUT cost-owners/{id}/alert-settings | high | mitigate | `CanUseCostOwnerAsync` required (403 otherwise); read requires `CanViewCostOwnerAsync`; authorization tests per claim class | closed |
| T-66-19 | Tampering | concurrent or replayed saves | medium | mitigate | Integer revision compared inside the save transaction; stale saves return 409; first-save race mapped to 409 | closed |
| T-66-20 | Tampering | threshold input | medium | mitigate | Server-side validation of count, range, precision and duplicates; 400 with no partial write; locale whitelist | closed |
| T-66-21 | Information disclosure | GameServerDto alert summary | medium | mitigate | Fields set only when `canSeeOwner`; null otherwise; projection test with a server-only viewer | closed |
| T-66-22 | Tampering | POST users/me/locale | low | mitigate | Writes only the caller's own record; normalized whitelist (`en`, `de`); 400 otherwise | closed |
| T-66-23 | Repudiation | resending a delivered alert | medium | mitigate | Claim predicate excludes Sent, Failed and Skipped; Sent is written under the lease; deterministic Message-Id for the at-least-once window; tests | closed |
| T-66-24 | Information disclosure | dispatcher logs and StatusReason | high | mitigate | Logs and reasons use ids and fixed codes only; capturing-logger test asserts no address, subject or body appears | closed |
| T-66-25 | Denial of service | endless retries | low | mitigate | `MaxDeliveryAttempts` (5) and bounded delays; permanent failures fail at once | closed |
| T-66-26 | Tampering | stale process overwriting a reclaimed row | low | mitigate | Outcome writes require `Status == Sending` and the current lease owner | closed |
| T-66-27 | Tampering | utf-api.json and api.ts | medium | mitigate | Regenerated by tooling only; CI-equivalent `cmp` and `contracts:check` gates in the verify block | closed |
| T-66-28 | Spoofing | malformed or reordered frames | low | mitigate | `parseAccruedCostFrame` validates every field and ignores extras; `isNewerAccrued` blocks older values; the socket is the authenticated same-origin stream | closed |
| T-66-29 | Information disclosure | accrued value in the browser | low | accept | The API sends values only to authorized viewers (plans 66-01, 66-05); the client shows only what it received | closed |
| T-66-30 | Elevation of privilege | banner and menu cost-stop buttons | medium | mitigate | Only `useCostConfirmation().request` is called; the shared dialog confirms and the API revalidates the action; tests assert no action call on click | closed |
| T-66-31 | Tampering | forged or malformed alert frames | low | mitigate | Parsers validate every field; frames arrive only on the authenticated per-user socket; duplicates are dropped by id | closed |
| T-66-32 | Information disclosure | server names in alert rows | low | mitigate | React escapes text; names render inside `bdi`; no HTML injection path | closed |
| T-66-33 | Information disclosure | draft persistence | low | mitigate | Draft lives in React state only; the change source scan covers the three settings files | closed |
| T-66-34 | Tampering | client-side validation bypass | low | accept | The API validates every save and enforces canManage and the revision (plan 66-06); the client rules mirror it for UX only | closed |
| T-66-35 | Spoofing | owner query parameter | low | mitigate | Validated as a GUID before any request; the API answers 403 or 404 for owners the viewer may not view | closed |
| T-66-36 | Information disclosure | finance account cost alert section | low | mitigate | Rendered only for `permissions.view`; the API independently requires `CanViewCostOwnerAsync` and `CanUseCostOwnerAsync` for saving | closed |
| T-66-37 | Tampering | locale recording request | low | mitigate | Sent only for a signed-in user, to the caller's own record; the API whitelists `en` and `de` | closed |
| T-66-38 | Spoofing | unmocked requests in e2e | low | mitigate | Every test records and aborts unmocked API requests and asserts the list is empty, so the spec cannot pass against a real backend | closed |
| T-66-39 | Information disclosure | UAT instructions | low | mitigate | No secret is required for the mandatory steps; the optional Discord step points to the git-ignored secret file and never prints a token | closed |
| T-66-40 | Tampering | billing unit resolution in CostAccrualService | medium | mitigate | The unit comes only from a registered provider's declared capabilities, chosen by the ledger provider identifier; a missing, mixed or unregistered identifier yields an unavailable amount (reason billing-unit), never a silently prorated lower amount that would delay an alert; tests cover the unregistered case | closed |
| T-66-41 | Denial of service | RuntimeBillingUnit rounding | low | mitigate | Integer nanosecond arithmetic with a validated positive length (PerStarted rejects zero and negative lengths), so no division by zero or floating drift; covered by RuntimeBillingUnitTests | closed |
| T-66-42 | Elevation of privilege | CostAlertService.DismissAllForServerAsync | medium | mitigate | Submitted ids are intersected with the server, the caller's viewable cost owners and the caller's undismissed alerts; dismissals are written for the caller only; tests cover another server's id and an unviewable owner's id | closed |
| T-66-43 | Information disclosure | dismiss-all response | low | mitigate | The endpoint answers 204 whether or not a submitted id exists or is visible, so ids cannot be probed (same rule as the single dismissal's 404 for unseen alerts) | closed |
| T-66-44 | Denial of service | dismiss-all payload | low | mitigate | 1 to 50 ids enforced by the payload attributes and again by the service (HostingValidationException mapped to 400) | closed |
| T-66-45 | Information disclosure | channel status badges | low | accept | Badges restate the availability values the settings DTO already returns to viewers allowed to read the owner's settings; no address, Discord id or new field reaches the page | closed |
| T-66-46 | Information disclosure | AccruedCostDto and GameServerAccruedCostMessage billing fields | low | mitigate | Both fields are null whenever the amount is null, including the hidden branch for viewers who may not view the cost owner; frames still go only to the server-view and cost-owner-view intersection; tests cover the hidden and audience cases | closed |
| T-66-47 | Tampering | parseAccruedCostFrame | low | mitigate | The parser type-checks both new fields and drops a malformed frame; frames arrive only on the authenticated per-user socket; the asOf ordering guard is unchanged | closed |
| T-66-48 | Information disclosure | dismissal toasts | low | accept | The toast repeats only the server name and amount the viewer just saw and dismissed, rendered as React text (no HTML), and appears only in the dismissing viewer's tab | closed |
| T-66-49 | Elevation of privilege | relocated banner footer and menu row cost-stop buttons | medium | mitigate | The moved buttons still call only `useCostConfirmation().request`; the shared dialog confirms and the API revalidates; banner and menu tests assert no action call on click after the redesign | closed |
| T-66-50 | Spoofing | unmocked requests in the gap closure journey | low | mitigate | The journey records and aborts unmocked API requests and asserts the list is empty after every step, so it cannot pass against a real backend | closed |
| T-66-51 | Information disclosure | UAT instructions | low | mitigate | No secret is needed for any mandatory step; no token or address is printed | closed |
| T-66-SC | Tampering | NuGet install of MailKit 5.0.0 | high | mitigate | Blocking human checkpoint before install, exact version pin, transitive package diff limited to declared dependencies | closed |
| T-66-SC | Tampering | npm/pip/cargo installs | high | accept | No package is installed by this plan; the phase's only new package (MailKit) is gated in plan 66-02 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

### Verification Evidence (high severity)

| Threat | Evidence |
|--------|----------|
| T-66-01 | `CostAccrualService.cs:67-70,82-83` hides amount and billing facts without `CanViewCostOwnerAsync`; `AccruedCostEndpointTests.ViewerWithoutCostOwnerVisibility_GetsHiddenWithoutAmount` |
| T-66-02 | `HostingAuthorizationService.cs:218-274`; drift test `CostOwnerViewAudienceTests.Audience_MatchesCanViewCostOwnerForEveryMatrixRow` |
| T-66-04 | `SmtpEmailSender.cs:76-79`, `SmtpFailureClassifier.cs`, `DiscordBot.cs:277-289` log fixed codes only; `Validate_InvalidSettings_ThrowsWithoutLeakingValues` |
| T-66-14 | `CostAlertPublisher.cs:46-65,88` audience intersection, fail closed; `Accruals_GoToServerAndOwnerViewersOnly`, `AudienceFailure_SendsNothing_AndLogsOneWarning` |
| T-66-15 | `CostAlertService.cs:91-95` + `CostAlertsController.cs:105` (404); `Dismiss_InvisibleOrUnknownAlert_ThrowsNotFound` |
| T-66-17 | `CostAlertService.cs:46-58`; `ViewableOwnerIds_AgreeWithCanViewCostOwnerForEveryClaimClass` |
| T-66-18 | `CostAlertSettingsService.cs:89-91` (403); `Managers_MaySave`, `ViewHolder_ReadsWithoutManage_AndCannotSave` |
| T-66-24 | `CostAlertDeliveryProcessor.cs:205-209,338-356`; `Logs_NeverContainAddressSubjectBodyOrExceptionText` |
| T-66-SC | `Api.csproj:12` MailKit 5.0.0 is the only manifest change in the phase; approval recorded in `66-02-SUMMARY.md`; Mailpit pinned `v1.34.0` (`AppHost/Program.cs:22`) |

Gap closure plans 66-13 to 66-18 were re-checked against T-66-01, 14, 15, 17 and 30: no regression.

**Informational (not counted):** `CostAlertSettingsService.GetAsync` also lets a `co:use` holder read settings (documented, tested, no sensitive fields); pass-level failure logs in `CostAlertDispatcher.cs:50-53` and `CostAlertWorker.cs:56-58` go through `HostingFailureSanitizer`.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-66-01 | T-66-29 | The API sends values only to authorized viewers (plans 66-01, 66-05); the client shows only what it received. | plan 66-08 | 2026-11-21 |
| AR-66-02 | T-66-34 | The API validates every save and enforces canManage and the revision (plan 66-06); the client rules mirror it for UX only. | plan 66-10 | 2026-11-21 |
| AR-66-03 | T-66-45 | Badges restate availability values the settings DTO already returns to allowed viewers; no address, Discord id or new field reaches the page. | plan 66-15 | 2026-11-21 |
| AR-66-04 | T-66-48 | The toast repeats only the server name and amount the viewer just dismissed, as React text, only in the dismissing tab. | plan 66-17 | 2026-11-21 |
| AR-66-05 | T-66-SC | Plans 66-01 and 66-03 to 66-18 install no package; the only dependency change (MailKit 5.0.0) was gated in plan 66-02. | plans | 2026-11-21 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-22 | 52 | 52 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-22

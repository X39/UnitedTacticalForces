---
phase: 66-accrued-cost-cost-alerts
plan: 06
subsystem: api
tags: [hosting, cost-alerts, settings, authorization, optimistic-concurrency, locale, dotnet]

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-03 CostAlertSetting/CostAlertThreshold entities, recipient resolver, CostAlertOptions, User.Locale
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-02 IEmailSender.IsConfigured and MessageLocales.Normalize
provides:
  - "GET/PUT cost-owners/{id}/alert-settings and GET cost-owners/mine/alert-settings (CostAlertSettingsController)"
  - "ICostAlertSettingsService/CostAlertSettingsService: validated, revisioned save with channel availability and language recording"
  - "CostAlertSettingsDto, CostAlertSettingsUpdate, ECostAlertChannelAvailability, ECostAlertAudience"
  - "GameServerDto.CostAlertThresholds and CanManageCostAlerts, null unless the viewer may see the cost owner"
  - "POST users/me/locale (UserLocaleUpdate), storing only en or de"
affects: [66-07, 66-08, 66-10, 66-11, cost-owner-react-view, workspace-strip]

plan_head_before: 51df1720acf086b2a7072b2bcdef44a367c98937
plan_head_after: 5e7eccaef5d1b0d1f910a7b20d0e55e32cf83361

actuals:
  tokens: 23000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Revision claimed with a conditional ExecuteUpdate inside the save transaction, so concurrent saves on one revision cannot both win"
    - "Privacy rule on the server projection: summary fields are set only when canSeeOwner, else null"

key-files:
  created:
    - sources/Api/DTO/ECostAlertChannelAvailability.cs
    - sources/Api/DTO/ECostAlertAudience.cs
    - sources/Api/DTO/CostAlertSettingsDto.cs
    - sources/Api/DTO/Updates/CostAlertSettingsUpdate.cs
    - sources/Api/DTO/Updates/UserLocaleUpdate.cs
    - sources/Api/Services/GameServerLifecycle/ICostAlertSettingsService.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertSettingsService.cs
    - sources/Api/Controllers/CostAlertSettingsController.cs
    - tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertServerSummaryTests.cs
    - tests/Api.Tests/GameServerHosting/UserLocaleEndpointTests.cs
  modified:
    - sources/Api/DTO/GameServerDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Controllers/UsersController.cs

key-decisions:
  - "canManage is exactly CanUseCostOwnerAsync (hosting administrator, the owner's user, or co:use for the owner)"
  - "A co:use holder may also read the settings (not only CanViewCostOwnerAsync viewers), because group alert recipients are exactly the co:use holders"
  - "Channel flags default to on when no row exists; Discord is ChannelUnavailable only when Discord:Enabled is false, email when IEmailSender.IsConfigured is false"
  - "ICostAlertSettingsService is public (the public controller depends on it); the implementation stays internal like the other alert services"
  - "Optimistic concurrency without a model change: a conditional ExecuteUpdate on (id, revision) claims the revision, a first save races on the existing unique owner index and maps DbUpdateException to 409"

requirements-completed: [CST-04, CST-05, CST-06]

coverage:
  - id: D1
    description: "An owner reads empty defaults and saves validated, sorted amounts with an incremented revision; the second save replaces amounts and flags"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#OwnerWithoutRow_ReadsEmptyDefaults_AndCreatesNoRow"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#Save_SortsAmounts_IncrementsRevision_RecordsLanguage"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#SecondSave_ReplacesAmountsAndFlags"
        status: pass
    human_judgment: false
  - id: D2
    description: "Invalid amounts or language return 400 and a stale revision returns 409; neither changes stored amounts, flags, revision or the recorded language"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#InvalidAmounts_ReturnBadRequestAndChangeNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#StaleRevision_ReturnsConflictAndChangesNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#BoundaryAmounts_AreAccepted_AndEmptyListSwitchesAlertsOff"
        status: pass
    human_judgment: false
  - id: D3
    description: "Owner user, hosting administrator and co:use holder may save; co:view holder reads read-only; unrelated user 403; unknown owner 404; mine returns the personal owner or 204"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#Managers_MaySave"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#ViewHolder_ReadsWithoutManage_AndCannotSave"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#UnrelatedUser_IsForbidden_AndUnknownOwnerIsNotFound"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#Mine_ReturnsPersonalOwner_Or204"
        status: pass
    human_judgment: false
  - id: D4
    description: "Channel availability follows Discord:Enabled, IEmailSender.IsConfigured and the recipient's linked account and address"
    requirement: "CST-06"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs#ChannelAvailability_FollowsConfigurationAndRecipient"
        status: pass
    human_judgment: false
  - id: D5
    description: "The server DTO carries ascending alert amounts and the manage flag only for viewers who may see the cost owner, in detail and fleet list"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/CostAlertServerSummaryTests.cs"
        status: pass
    human_judgment: false
  - id: D6
    description: "POST users/me/locale stores only a normalized en or de on the caller; unsupported input is 400 and no user id is 401"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/UserLocaleEndpointTests.cs"
        status: pass
    human_judgment: false
  - id: D7
    description: "The conditional revision claim under true concurrent saves on PostgreSQL is not exercised (tests run on SQLite, sequentially)"
    requirement: "CST-04"
    verification: []
    human_judgment: true

duration: about 35min
completed: 2026-11-21
status: complete
---

# Phase 66 Plan 06: Cost Alert Settings API Summary

**Owners and their managers can read and save per-owner alert amounts and channel flags through validated, revisioned endpoints that report which channels can deliver and record the saver's language; the server DTO carries the amounts only for viewers who may see the owner, and users can record their language switch.**

## Performance

- **Duration:** about 35 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 11 created, 3 modified

## Accomplishments

- Tracer slice end to end: DTOs, enums, service, controller, 24 tests green (validation matrix, 409, claim classes, availability, language).
- `GameServerDto.CostAlertThresholds` and `CanManageCostAlerts` projected in `ProjectServerAsync` behind `canSeeOwner`; `ServerQuery` now includes the setting and thresholds.
- `POST users/me/locale` added to `UsersController` without a constructor change.

## Task Commits

1. **Task 1: Tracer, manage cost alert settings per cost owner** - `d219fd3f` (feat, api). Also contains `UserLocaleUpdate.cs`, which Task 3 uses.
2. **Task 2: Project cost alert amounts on the server for owner viewers** - `54668fec` (feat, api)
3. **Task 3: Record the language a user reads messages in** - `5e7eccae` (feat, api)

## Verification

- `CostAlertSettingsTests`: 24 passed. Task 2 slice (`CostAlertServerSummaryTests`, `GameServerProjectionServiceTests`, `GameServerListAccessTests`): 48 passed. Task 3 slice (`UserLocaleEndpointTests`, `AuthenticationProviderAvailabilityTests`): 21 passed.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting|FullyQualifiedName~Authentication"`: 928 passed, 1 failed. The failure is pre-existing and unrelated (see Deferred Issues).
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- No file under `sources/ReactApp`, `sources/WebApp` or `sources/Database` was changed by this plan's commits; no migration was needed.
- Guard greps: `alert-settings` appears 3 times in the controller, `inapp` 0 times in `CostAlertSettingsDto.cs`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A co:use holder who cannot "view" the owner was refused**
- **Found during:** Task 1 test run (`Managers_MaySave("use")`)
- **Issue:** The plan required both `CanViewCostOwnerAsync` and `CanUseCostOwnerAsync` to save, but `CanViewCostOwnerAsync` does not include a `co:use` claim, so a pure co:use holder (the group alert recipient and a stated manager) got 403, contradicting the must-have "a co:use holder may save".
- **Fix:** Saving requires only `CanUseCostOwnerAsync`; reading is allowed for viewers or managers. The response after a save is built from a reloaded owner with `CanManage` true.
- **Files modified:** `CostAlertSettingsService.cs`, `ICostAlertSettingsService.cs`
- **Commit:** `d219fd3f`

**2. [Rule 2 - Missing critical] Atomic revision claim instead of a read-then-write compare**
- **Found during:** Task 1 design
- **Issue:** Comparing a loaded revision and then updating lets two concurrent saves from different requests both pass on PostgreSQL read committed; a concurrency token would need a model change and migration, which this plan excludes.
- **Fix:** `ExecuteUpdateAsync` on `(id, revision)` inside the save transaction claims the revision and takes the row lock; zero rows affected maps to 409. A first save relies on the existing unique owner index and maps `DbUpdateException` to 409.
- **Files modified:** `CostAlertSettingsService.cs`
- **Commit:** `d219fd3f`

**3. [Rule 3 - Blocking] `UserLocaleUpdate` committed with Task 1**
- The file belongs to Task 3 but was staged with the Task 1 files. It compiles standalone and no behavior changed; Task 3 only adds the action and tests.

**Total deviations:** 3 (one bug, one critical hardening, one process note). No scope change.

## Deferred Issues

- `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` fails because it searches `Program.cs` for the exact text `app.UseWebSockets();` while Program.cs calls it with `WebSocketOptions`. Pre-existing, `Program.cs` untouched here. Logged in `deferred-items.md`.

## Notes for Dependent Plans

- The OpenAPI snapshot and React types are not regenerated here; plan 66-08 owns that.
- `GET cost-owners/mine/alert-settings` returns 204 for a user without a personal owner; the React view must treat that as "no personal owner", not an error.
- The settings DTO exposes `Discord` and `Email` availability, `Audience` and `MessageLanguage`; the in-app channel is deliberately not a field.
- A save with `Language` set records the caller's locale; a save without it leaves the locale untouched.

## Known Stubs

None.

## Threat Flags

None. Surfaces added match the plan's threat model (T-66-18 claim checks and tests per claim class, T-66-19 revision claim and 409, T-66-20 server validation with no partial write, T-66-21 summary fields null without owner visibility, T-66-22 own-record write with an en/de whitelist).

## Self-Check: PASSED

- All 11 created and 3 modified files exist (build and test run succeeded).
- Commits `d219fd3f`, `54668fec`, `5e7eccae` are ancestors of HEAD.

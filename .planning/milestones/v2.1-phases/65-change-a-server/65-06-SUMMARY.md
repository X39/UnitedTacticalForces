---
phase: 65-change-a-server
plan: 06
subsystem: api
tags: [configuration, game-definitions, tier, openapi, profiles]

requires:
  - phase: 65-01
    provides: configuration controller marks that this plan keeps and annotates
provides:
  - EGameConfigurationTier enum (Advanced = 0, Common = 1) in GameServerImplementations
  - GameConfigurationDefinition.Tier declared per game; Common sets for Arma 3, ASKA and Counter-Strike 2
  - GameConfigurationEntryDto.Tier projected on GET and PUT /game-servers/{id}/configuration
  - ProducesResponseType annotations on the four configuration endpoints so GameConfigurationDto reaches the OpenAPI document
  - ConfigurationProfileSummaryDto.LatestRevisionId on every profile summary projection
affects: [65-09, 65-10, 65-11, change-a-server settings UI]

plan_head_before: 6af49afcb7b10954760ae62d1119b7d3cd9f406f
plan_head_after: 6e8e95708c3707b1ac291b652c3c839ff428e6af

actuals:
  tokens: 7500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Each game definition owns its Common set; undeclared options default to Advanced through enum value 0"
    - "Optional trailing tier parameter on the per-game private Definition helper"

key-files:
  created:
    - sources/GameServerImplementations/EGameConfigurationTier.cs
    - tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs
  modified:
    - sources/GameServerImplementations/GameConfigurationDefinition.cs
    - sources/GameServerImplementations/Arma3ConfigurationImplementation.cs
    - sources/GameServerImplementations/AskaConfigurationImplementation.cs
    - sources/GameServerImplementations/CounterStrike2ConfigurationImplementation.cs
    - sources/Api/DTO/GameConfigurationDto.cs
    - sources/Api/Controllers/GameServerConfigurationController.cs
    - sources/Api/Controllers/ConfigurationProfilesController.cs

key-decisions:
  - "Tier is a property on the definition (init, default Advanced) instead of a new positional record parameter, so existing constructor calls stay valid"
  - "The Headless Client placement is Common because it changes the host count and cost"
  - "No sensitive option of any game is Common; a test guards this for every registered game"

patterns-established:
  - "The UI renders Common first and Advanced behind a disclosure from the API-declared tier; no Common list exists in the client"

requirements-completed: [CHG-02]

coverage:
  - id: D1
    description: "Every game definition declares its Common options; undeclared options are Advanced; no sensitive option is Common"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#Arma3_DeclaresExactlyTheDocumentedCommonSet"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#OtherGames_DeclareExactlyTheDocumentedCommonSet"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#NoSensitiveDefinition_IsCommon"
        status: pass
    human_judgment: false
  - id: D2
    description: "GET configuration returns the declared tier per entry and the endpoints declare GameConfigurationDto responses"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#Get_ReturnsTheDeclaredTierPerEntry"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every profile summary carries LatestRevisionId (null without revisions) so a new profile can be pinned to its current revision"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#ProfileOptions_CarryTheLatestRevisionId"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameConfigurationTierTests.cs#ProfilesController_SummariesCarryTheLatestRevisionId"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 06: Common and Advanced configuration tiers Summary

**Each game declares its own Common configuration options through a new `EGameConfigurationTier`, the tier reaches the annotated configuration response, and every profile summary carries `LatestRevisionId`.**

## Performance

- **Duration:** about 20 min
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 9 changed (2 created, 7 modified)

## Accomplishments

- `EGameConfigurationTier` with `Advanced = 0` first, so a definition without a declaration is Advanced; `GameConfigurationDefinition.Tier` is an init-only property next to `IsControlPlaneOnly`, and it also reaches the profile pages through `ConfigurationProfileDefinitionCatalogDto.Definitions` additively.
- Common sets: Arma 3 (placement, hostname, maxPlayers, persistent, forcedDifficulty, BattlEye), ASKA (server name, display name, region, mode), Counter-Strike 2 (hostname, maxplayers, mp_maxrounds, bot_quota).
- `GET` and `PUT /game-servers/{id}/configuration` declare `GameConfigurationDto` responses, and profile selection and profile options are annotated too, so the generated types will include the tier (65-09 regenerates the snapshot and clients).
- `LatestRevisionId` is projected in `GetProfileOptionsAsync` and in all four `ConfigurationProfilesController` summaries (options, list, detail, creation response using the new revision key).

## Task Commits

1. **Task 1 (tracer): Arma 3 Common setting reaches the response as tier Common** - `b72b785e` (feat(api))
2. **Task 2: ASKA and CS2 Common sets, annotations, latest revision id** - `6e8e9570` (feat(api))

## Verification Results

- Tracer gate: `dotnet test --filter GameConfigurationTier|GameServerConfigurationService` passed (8 tests) before expansion.
- `dotnet test --filter GameConfigurationTier|ConfigurationProfilesControllerTests|OpenApiRouteCompatibility` passed (15 tests).
- `dotnet test --filter GameServerHosting|OpenApi` passed (705 tests, 0 failed).
- `dotnet build X39.UnitedTacticalForces.slnx` succeeded (0 errors).
- Acceptance greps: enum 1, Arma 3 Common 6, ASKA Common 4, CS2 Common 4, `Tier = q.Definition.Tier` 1, `ProducesResponseType<GameConfigurationDto>` 2, `LatestRevisionId` 4 (profiles controller) and 1 (configuration controller), `ProducesResponseType` 10 (at least 9).
- No commit touches `sources/ReactApp` or `sources/WebApp`.

## Deviations from Plan

None - plan executed exactly as written. The TDD ordering was relaxed in the same way as plan 65-01: implementation and tests were written together and run green rather than committed as separate RED and GREEN steps.

## Issues Encountered

Several controller, DTO and test files use CRLF line endings, so scripted multi-line replacements failed silently until line endings were normalized; the final edits preserve each file's endings.

## Known Stubs

None.

## Threat Flags

None. T-65-16 is mitigated by `NoSensitiveDefinition_IsCommon` across every registered game; T-65-17 is unchanged because `SelectProfileAsync` still validates that a pinned revision belongs to the selected profile.

## Next Phase Readiness

Plan 65-09 regenerates the OpenAPI snapshot and React types so `GameConfigurationDto`, `GameConfigurationEntryDto.tier` and `ConfigurationProfileSummaryDto.latestRevisionId` reach the client; the settings UI can render Common first and Advanced behind a disclosure, and pin a newly chosen profile with `latestRevisionId`.

## Self-Check: PASSED

- Created files verified on disk; commits `b72b785e` and `6e8e9570` verified as ancestors of HEAD.

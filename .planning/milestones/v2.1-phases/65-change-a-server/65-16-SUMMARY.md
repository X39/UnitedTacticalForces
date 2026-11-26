---
phase: 65-change-a-server
plan: 16
subsystem: api
tags: [game-server-secrets, rotation, openapi, aspnetcore, credentials]
gap_closure: true
gap_ids: [G-65-6]

requires:
  - phase: 65-change-a-server
    provides: "65-11 secret service, controller and ManageSecrets/RevealSecrets claims; 65-15 React contract tooling (contracts:generate / contracts:check)"
provides:
  - "POST /game-servers/{id}/secrets/{key}/rotate returns the generated GameServerSecretValueDto once (200, Cache-Control no-store) to a ManageSecrets caller"
  - "Reveal also answers Cache-Control no-store"
  - "GameSecretDefinition.AcceptsChosenValue and ValueFormat (EGameSecretValueFormat SingleLine/Token) declared per credential by each game"
  - "PUT /game-servers/{id}/secrets accepts a chosen value for declared generated credentials as a UserProvided version and validates the value format with fixed messages"
  - "GET /game-servers/{id}/secrets projects acceptsChosenValue and valueFormat"
  - "Regenerated OpenAPI snapshot and src/generated/api.ts carrying the rotate 200 response, both status fields and the EGameSecretValueFormat schema"
affects: [65-17, 65-18, 65-19]

actuals:
  tokens: 11800   # chars/4 over the hand-written diff (generated snapshot and api.ts excluded)
  tasks: 2
  commits: 2
plan_head_before: 373874b279c8ed50914574d157efa9b4e9d7b92a
plan_head_after: ec31643d3d56a01f7a0265f53c1d12c8d6620e52

tech-stack:
  added: []
  patterns:
    - "Games declare per-credential value rules in the definition catalog; the API enforces them and projects them through the status DTO"
    - "Value-bearing responses carry Cache-Control no-store"

key-files:
  created:
    - sources/Api/Services/GameServerDefinition/EGameSecretValueFormat.cs
  modified:
    - sources/Api/Services/GameServerDefinition/GameSecretDefinition.cs
    - sources/Api/Services/GameServerDefinition/Arma3GameServerDefinitionRegistration.cs
    - sources/Api/Services/GameServerDefinition/CounterStrike2GameServerDefinitionRegistration.cs
    - sources/Api/Services/GameServerSecrets/GameServerSecretService.cs
    - sources/Api/Services/GameServerSecrets/IGameServerSecretService.cs
    - sources/Api/Controllers/GameServerSecretsController.cs
    - sources/Api/DTO/GameServerSecretStatusDto.cs
    - sources/Api/DTO/GameServerSecretValueDto.cs
    - tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs
    - tests/Api.Tests/GameServerHosting/TestGameServerSecretService.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "Rotate returns only the value it just generated, to any ManageSecrets caller; reveal keeps its own RevealSecrets gate (D-07)"
  - "Chosen values are declared per credential (AcceptsChosenValue); Arma 3 admin, server-command and BattlEye RCon passwords and the CS2 RCON password accept them, other generated credentials stay platform-managed (D-08)"
  - "SingleLine rejects control characters; Token additionally restricts to printable ASCII 0x21-0x7E without double quote and backslash, used for BattlEye RCon and CS2 RCON"
  - "Validation messages are fixed English strings and never include the submitted value"

patterns-established:
  - "Fixed-message validation: rejected secret values never appear in exception messages or ValidationProblem bodies"

requirements-completed: [CHG-07]

coverage:
  - id: D1
    description: "Rotation returns the newly generated credential value once with Cache-Control no-store to a manage-only caller; audit carries key and version only"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#RotateAsync_ReturnsTheGeneratedValueAndAuditsWithoutIt"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs#RotateAsync_WithManageOnly_ReturnsTheValueWithNoStore"
        status: pass
    human_judgment: false
  - id: D2
    description: "Games declare chosen-value acceptance and value format; the API stores accepted chosen values as UserProvided versions and rejects unusable ones without echoing them"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#SetAsync_ChosenValueForGeneratedSecret_StoresUserProvidedVersion"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#SetAsync_TokenCredentialWithUnusableValue_IsRejectedWithoutEchoingTheValue"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs#SetAsync_InvalidTokenValue_ReturnsValidationProblemWithoutTheValue"
        status: pass
    human_judgment: false
  - id: D3
    description: "OpenAPI snapshot and generated TypeScript types equal the exporter and generator output and carry the new contract"
    requirement: "CHG-07"
    verification:
      - kind: other
        ref: "dotnet run --project sources/Api/Api.csproj -- --export-openapi + cmp; npm --prefix sources/ReactApp run contracts:check; npm --prefix sources/ReactApp run typecheck"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-11-21
status: complete
---

# Phase 65 Plan 16: Credential Values API Summary

**Rotate now returns the generated credential once (200, no-store) to manage-only callers, and games declare which generated credentials accept a member-chosen value and in which format (SingleLine/Token), enforced by the API and published in a regenerated OpenAPI contract.**

## Performance

- **Duration:** about 8 min
- **Tasks:** 2 of 2
- **Files changed:** 14 (612 insertions, 65 deletions, generated files included)

## Accomplishments

- **Task 1 (tracer):** `IGameServerSecretService.RotateAsync` returns `GameServerSecretValueDto`; the service returns the value it generated and audits the key and version with a summary stating the one-time disclosure, never the value. The controller returns `200` with `Cache-Control: no-store` for rotate and reveal. The interface and the value DTO now have full XML docs. Snapshot and `api.ts` regenerated.
- **Task 2:** new `EGameSecretValueFormat` (`SingleLine`, `Token`), `GameSecretDefinition.AcceptsChosenValue` and `ValueFormat`, declared for the Arma 3 administrator, server command and BattlEye RCon passwords and the Counter-Strike 2 RCON password. `SetAsync` accepts a chosen value for those as a `UserProvided` version and validates it; generated credentials without the declaration still answer "This secret is managed by the platform." `GameServerSecretStatusDto` gains `AcceptsChosenValue` and `ValueFormat`.
- Tests added: service (rotate value and audit, chosen value stored, undeclared generated credential rejected, five Token rejections, SingleLine spaces and line break, status projection, Arma 3 declarations) and controller (manage-only rotate with no-store and reveal parity, archived conflict body, chosen value 204, invalid token ValidationProblem without the value, reveal no-store).

## Task Commits

1. **Task 1: rotate returns the generated value once** - `8c3317fd` (feat(api)!, BREAKING CHANGE note included)
2. **Task 2: games accept chosen values for generated credentials** - `ec31643d` (feat(api))

## Deviations from Plan

None - plan executed exactly as written. One test-only adjustment: the controller test helper now takes a claims parameter and shares a single service between controllers so that a second controller can unprotect values protected by the first (each `EphemeralDataProtectionProvider` has its own key ring). Counter-Strike 2 `ResolveRequiredAsync` needs the required `gslt`, so the chosen-value test sets it first.

## Issues Encountered

None.

## Verification Results

- `dotnet test tests/Api.Tests --filter GameServerSecret`: 24 passed.
- `dotnet build X39.UnitedTacticalForces.slnx`: 0 errors.
- `dotnet test X39.UnitedTacticalForces.slnx --no-build --filter "GameServerHosting|OpenApi"`: 719 passed, 0 failed.
- Fresh OpenAPI export `cmp` equal to `sources/ReactApp/openapi/utf-api.json`; `contracts:check` and `typecheck` exit 0.
- `dotnet ef migrations has-pending-model-changes`: no changes.
- `git status --porcelain -- sources/WebApp` empty; `sources/Common/Ui/routes.json` unchanged; no React source other than `src/generated/api.ts` changed since c24a26c8.
- All acceptance criteria of both tasks pass.

## Known Stubs

None.

## Threat Flags

None. All threats of the plan's register (T-65-43, -46, -47, -48) are mitigated as specified; T-65-45 is accepted by design.

## Next

Ready for 65-17 (one-time value UI), which consumes the rotate 200 response and `GameServerSecretValueDto` from the regenerated contract.

## Self-Check: PASSED

- Created file `sources/Api/Services/GameServerDefinition/EGameSecretValueFormat.cs` exists.
- Commits `8c3317fd` and `ec31643d` are ancestors of HEAD.

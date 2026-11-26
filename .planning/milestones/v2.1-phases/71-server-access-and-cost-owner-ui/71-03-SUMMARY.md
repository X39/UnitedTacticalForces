---
phase: 71-server-access-and-cost-owner-ui
plan: 03
subsystem: api
tags: [aspnet-core, ef-core, npgsql, openapi, cost-owner, access-control, game-server-hosting]
status: complete

requires:
  - phase: 71-01
    provides: LikePattern helper, escaped ILIKE search pattern and the candidate route conventions
provides:
  - "GET /game-servers/{id}/cost-owner/options: Modify-gated, searchable, paged list of the owners the caller may transfer a server to"
  - "Kind (User, Verein, ExternalOrganization) on every cost owner option, on the new route and on the creation route"
  - "GameServerDto.canManageAccess, costOwnerIsFrozen and costOwnerAssignedAt"
  - "Regenerated utf-api.json and src/generated/api.ts"
affects: [71-04, 71-05, 71-06, cost-owner-panel, access-panel]

actuals:
  tokens: 14000
  tasks: 2
  commits: 2
plan_head_before: 43dec80f8b1f2cca1d5e9d9f13f756b001fd3841
plan_head_after: 514bedef656a29ab00542f94ae833245a15c0202

tech-stack:
  added: []
  patterns:
    - "One shared usable-owner query (administrator, own owner, exact co:use grant) behind the creation and the transfer options"
    - "Owner-visibility rule (canSeeOwner) gates every owner-derived field of the server read"
    - "Offline Npgsql translation test of the escaped ILIKE search with ToQueryString"

key-files:
  created: []
  modified:
    - sources/Api/DTO/GameServerCostOwnerOptionDto.cs
    - sources/Api/DTO/GameServerDto.cs
    - sources/Api/Services/GameServerLifecycle/IGameServerProjectionService.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Controllers/GameServersController.cs
    - tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "The creation method keeps its unpaged result and only gains Kind; the usable-owner filter and the ordering/projection are extracted into two private helpers shared with the transfer method"
  - "The service re-validates take (1 to 50, HostingValidationException) besides the controller bound check, as plan 71-01 did for the access candidates"
  - "costOwnerAssignedAt reads the most recent open assignment (TimeStampUnassigned null) of the server, in the detail read only (includeAutomation), so the fleet list keeps its query count"

patterns-established:
  - "Server read facts that derive from the cost owner are null unless CanViewCostOwnerAsync holds"

requirements-completed: [API-01, API-05, D-17]

coverage:
  - id: D1
    description: "Cost owner transfer options route: administrator gets every owner, Modify member gets own owner first plus exact co:use grants, current owner excluded, frozen flagged, kind reported, take bound"
    requirement: "API-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_ForAdministrator_ListEveryOwnerExceptTheCurrentOne"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_ForModifyMember_ListOwnOwnerFirstThenExactGrants"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_ExcludeTheCurrentOwnerEvenWhenItIsTheCallersOwn"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_HonorTheTakeLimitAfterOrdering"
        status: pass
    human_judgment: false
  - id: D2
    description: "Options route authorization and errors: Modify policy, 403 without Modify, 404 for unknown or archived server, 400 for take 0 and 51"
    requirement: "API-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs#StorageEstimateRoutes_RequireExpectedAuthorization"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs#CostOwnerOptionsEndpoint_WithTakeOutsideBounds_ReturnsBadRequestWithoutDelegating"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_WithoutModify_ThrowForbidden"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#TransferOptions_ForUnknownOrArchivedServer_ThrowNotFound"
        status: pass
    human_judgment: false
  - id: D3
    description: "Escaped display-name search translates for Npgsql to ILIKE with the backslash escape clause"
    requirement: "API-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#ApplyDisplayNameSearch_TranslatesForNpgsqlToEscapedIlike"
        status: pass
    human_judgment: false
  - id: D4
    description: "Kind on every cost owner option, including the unchanged creation route"
    requirement: "API-05"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs#CreationOptions_CarryTheKindOfEveryOwner"
        status: pass
    human_judgment: false
  - id: D5
    description: "canManageAccess mirrors ServerManageAccessRequirement.HasExactPermission in the detail and the list"
    requirement: "API-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CanManageAccess_MirrorsTheManageAccessRequirement"
        status: pass
    human_judgment: false
  - id: D6
    description: "costOwnerIsFrozen and costOwnerAssignedAt follow the owner-visibility rule; assignment time in the detail only"
    requirement: "API-01"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerIsFrozen_MirrorsTheOwnerForAViewerWhoMaySeeIt"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerFacts_AreWithheldWhenTheViewerCannotSeeTheOwner"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerAssignedAt_IsTheOpenAssignmentInTheDetailOnly"
        status: pass
    human_judgment: false
  - id: D7
    description: "OpenAPI snapshot and generated TypeScript regenerated and in sync; legacy WebApp builds with nothing changed under sources/WebApp"
    requirement: "D-17"
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run contracts:check; dotnet build sources/WebApp/WebApp.csproj; git diff --stat 43dec80f -- sources/WebApp"
        status: pass
    human_judgment: false
---

# Phase 71 Plan 03: Cost owner options and server access facts Summary

**A Modify-gated, searchable `cost-owner/options` route sharing the creation rule, an owner `Kind` on every option, and `canManageAccess`, `costOwnerIsFrozen` and `costOwnerAssignedAt` on the server read behind the owner-visibility rule.**

## Performance

- **Duration:** about 10 min of wall clock (start 2026-11-26)
- **Tasks:** 2 (1 tracer, 1 auto/TDD)
- **Files changed:** 10 modified, none created
- **Commits:** 2 (measured from the plan head ledger)

## Accomplishments

- `GET /game-servers/{gameServerId}/cost-owner/options` behind the `Modify` policy with `search` (escaped case-insensitive `ILIKE` on the display name through `LikePattern`) and `take` (1 to 50, default 20; 0 and 51 answer 400). Administrators receive every owner, everyone else their own owner and the owners with an exact `co:use` grant; the server's current owner is excluded, frozen owners are listed and flagged; order is own owner, display name, id. 403 without Modify, 404 for an unknown or archived server.
- The usable-owner rule lives once in a private query used by both `GetCreationCostOwnerOptionsAsync` (unpaged, only `Kind` added) and `GetCostOwnerOptionsAsync`.
- `GameServerCostOwnerOptionDto.Kind` (`ECostOwnerKind`) on every option.
- `GameServerDto` gains `CanManageAccess` (exactly `ServerManageAccessRequirement.HasExactPermission` for `PrimaryKey.ToString("D")`), `CostOwnerIsFrozen` (null when the owner is hidden) and `CostOwnerAssignedAt` (open assignment time, detail only, null when hidden). The list does not issue the extra assignment query.
- Snapshot and `api.ts` regenerated per task; `contracts:check` is clean; `sources/WebApp` is byte-identical to the base and builds.

## Task Commits

1. **Task 1: transferable cost owners route (tracer)** - `e87c198b` (feat)
2. **Task 2: manage access and cost owner facts on servers** - `514bedef` (feat)

Tracer gate (auto-approved path, end-of-phase mode, automated verify only): the tracer verify was re-run end to end after commit and passed (tests, build, snapshot equality, contracts:check) before expansion.

## Verification

- Task 1 filter (`CostOwnerOptions|GameServerAccessController|OpenApi`): 44 passed, 0 failed.
- Task 2 filter (`GameServerProjection|GameServerListAccess|GameServerNotificationAccess|CostOwnerOptions|OpenApi`): 81 passed, 0 failed.
- Whole `tests/Api.Tests`: 1792 passed, 0 failed.
- Exported snapshot equals the committed `utf-api.json` (`diff --strip-trailing-cr`); `contracts:check` leaves no diff; working tree clean after the final commit.
- `dotnet build sources/WebApp/WebApp.csproj`: 0 errors; `git diff --stat 43dec80f -- sources/WebApp` is empty.
- Acceptance greps for both tasks: all PASS (counts 1, 1, 1, 3, 1 for task 1; 1, 1, 1, 1, 1 for task 2).
- Npgsql translation of the display-name search is proven offline with `ToQueryString` (`ILIKE`, `ESCAPE`, escaped pattern; blank search adds no filter).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Service-level take validation**
- **Found during:** Task 1
- **Issue:** the plan bounds `take` only in the controller; another caller of the service would bypass the cap (T-71-07 requires the cap).
- **Fix:** `GetCostOwnerOptionsAsync` throws `HostingValidationException` (maps to 400) for `take` outside 1 to 50, after the Modify check; the controller keeps its ProblemDetails 400 and never calls the service for a bad take. Covered by `TransferOptions_WithTakeOutsideBounds_ThrowValidation`.
- **Commit:** `e87c198b`

**2. [Rule 2 - Missing critical functionality] XML documentation on touched members**
- **Found during:** Task 1
- **Issue:** AGENTS.md requires XML docs on every member; the new interface member, route, helpers, constants and DTO members had none by default.
- **Fix:** all new members carry summary, params, returns and exceptions. Pre-existing undocumented members of the touched files were left as they were (out of scope).
- **Commit:** `e87c198b`, `514bedef`

**Total deviations:** 2 auto-fixed (both Rule 2). **Impact:** none on scope or contract.

## Authentication Gates

None.

## Issues Encountered

- The shell wrapper refuses compound `git` commands in a worktree; they were split into plain commands. No effect on the result.
- The system attribution reminder (Sonnet 5.5) differed from the dispatch note (Opus 5.5); the commits carry the reminder's line, which names the model actually running.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-71-07 mitigated (Modify policy plus `HasServerPermission` re-check, usable owners only, current owner excluded, take capped), T-71-08 mitigated (frozen flag and assignment time only behind `canSeeOwner`, hidden case asserted by test), T-71-09 accepted unchanged (`ChangeCostOwnerAsync` untouched).

## Cross-plan findings

- **XPF-71-03-1:** `costOwnerAssignedAt` is null both when the owner is hidden and in the list read, and the generated type is `null | Instant` (optional). The Cost owner panel must show "since" only when the detail read supplies it and must not request it from list rows.
- **XPF-71-03-2:** `canManageAccess` uses the exact rule of the ManageAccess requirement, which does NOT count `sv:modify` or the Modify role-based paths; the Access tab gate in the React app must use this flag, not `canModify`.
- **XPF-71-03-3:** the options route excludes the current owner, so an empty result is a normal outcome (for instance a member whose only usable owner already pays); the UI needs an empty state distinct from a load error.

## Next Phase Readiness

The contract carries everything the Cost owner panel and the Access gate need from the API (options with kind, frozen flag, assignment time, manage-access flag). Ready for the remaining wave 2 and wave 3 plans.

## Self-Check: PASSED

- Modified files exist and are committed (working tree clean).
- Commits `e87c198b` and `514bedef` are ancestors of HEAD.

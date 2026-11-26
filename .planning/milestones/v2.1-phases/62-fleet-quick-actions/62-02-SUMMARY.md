---
phase: 62-fleet-quick-actions
plan: 02
subsystem: api
tags: [aspnetcore, dto, openapi, openapi-typescript, privacy, xunit]

# Dependency graph
requires:
  - phase: 62-fleet-quick-actions
    provides: "GameServerProjectionService with per-server canSeeOwner (existing hosting projection)"
provides:
  - "GameServerDto.CostOwnerUserId: the user behind a user-kind cost owner, withheld when the viewer cannot see the owner"
  - "Regenerated React OpenAPI snapshot and api.ts carrying costOwnerUserId as null | string"
affects: [62-fleet-quick-actions Mine filter, react fleet table, fleet provider]

actuals:
  tokens: 2245
  tasks: 2
  commits: 2
plan_head_before: 90fd52b2353b748bcbbfcb4a1e84a477d3561fe7
plan_head_after: 578d7f8bec07f02db53aa269e833a71deab63e99

tech-stack:
  added: []
  patterns:
    - "Privacy-gated DTO projection: personal identifiers ride the same visibility decision as the display name they belong to"

key-files:
  created: []
  modified:
    - sources/Api/DTO/GameServerDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "CostOwnerUserId reuses the existing canSeeOwner result and is set only for ECostOwnerKind.User owners; no extra query, include or authorization call"
  - "The Kiota client under sources/WebApp/UtfApi is not regenerated: the classic WebApp does not read the field"
  - "Run the OpenAPI export with an absolute output path: dotnet run resolves relative paths against sources/Api, not the repository root"

patterns-established:
  - "Export the OpenAPI snapshot locally with an absolute path (or $RUNNER_TEMP in CI) so the file is not written under sources/Api"

requirements-completed: [FLT-02]

coverage:
  - id: D1
    description: "GameServerDto exposes CostOwnerUserId, set only for a visible user-kind cost owner and null for hidden owners and Verein owners"
    requirement: "FLT-02"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerUserId_IsProjectedForTheOwningUser"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerUserId_IsWithheldWhenTheViewerCannotSeeTheOwner"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerUserId_IsProjectedWhenTheViewerMayViewTheOwner"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerUserId_IsNullForANonUserOwner"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#CostOwnerUserId_IsProjectedForAHostingAdministrator"
        status: pass
      - kind: integration
        ref: "dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~GameServerHosting (538 passed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "React OpenAPI snapshot matches the API export byte-for-byte, and the generated TypeScript type carries costOwnerUserId?: null | string"
    requirement: "FLT-02"
    verification:
      - kind: other
        ref: "dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <abs tmp> && cmp --silent sources/ReactApp/openapi/utf-api.json <abs tmp>"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run contracts:check"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run typecheck"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 02: Cost Owner User Id Projection Summary

**GameServerDto now carries a nullable `CostOwnerUserId`. It holds the paying user's id only when the cost owner is a user and the viewer may already see that owner. The React OpenAPI snapshot and the `api.ts` types were regenerated to match.**

## Performance

- **Duration:** about 15 min
- **Started:** 2026-11-19T08:24:00Z
- **Completed:** 2026-11-19T08:39:13Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- `GameServerDto.CostOwnerUserId` (Guid?) has a one-line XML summary and a value element, so the OpenAPI text has no line-ending differences between platforms.
- `ProjectServerAsync` sets the field from `CurrentCostOwner.UserFk` only when `canSeeOwner` is true and the owner kind is `User`. It reuses the visibility check that is already computed and adds no query. A comment states the privacy rule.
- Five xunit tests cover these cases: the owning user, a hidden owner (null and "Assigned owner"), a viewer with `co:view`, a Verein owner (null), and a hosting administrator.
- `utf-api.json` and `api.ts` were regenerated with the documented commands. The only change in each file is the new field. The CI `cmp` comparison, `contracts:check` and `typecheck` pass.

## Task Commits

1. **Task 1: CostOwnerUserId on the DTO and its privacy-preserving projection** - `737dfe00` (feat)
2. **Task 2: Regenerate the OpenAPI snapshot and the TypeScript contract types** - `578d7f8b` (build)

## Files Created/Modified
- `sources/Api/DTO/GameServerDto.cs` - adds the documented `CostOwnerUserId` member
- `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs` - projects the field behind the existing visibility check
- `tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs` - five `CostOwnerUserId_*` tests
- `sources/ReactApp/openapi/utf-api.json` - regenerated snapshot; adds nullable uuid `costOwnerUserId`
- `sources/ReactApp/src/generated/api.ts` - regenerated types; adds `costOwnerUserId?: null | string`

## Decisions Made
- The field is projection-only. No EF entity, migration, worker, provider or lifecycle change was made (`git diff de6261e4 -- sources/Database sources/WebApp` is empty).
- The change is additive, so the commit has no breaking-change marker.
- The Kiota client in `sources/WebApp/UtfApi` was left untouched, as the plan and ROADMAP require.

## TDD Notes
The task was marked `tdd="true"`. I wrote the tests first and confirmed RED: the build failed with CS1061 because `GameServerDto` had no `CostOwnerUserId`. Then I implemented the field and reached GREEN (5/5). The plan names one `feat(api)` commit for the task, so the RED and GREEN steps are in that one commit. A separate RED commit would have left a revision where the test project does not build.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] OpenAPI export with a relative path wrote to the wrong location**
- **Found during:** Task 2
- **Issue:** The plan said the export handler accepts a relative path from the repository root. `dotnet run --project sources/Api/Api.csproj` runs with `sources/Api` as its working directory, so `Path.GetFullPath` wrote the export to `sources/Api/sources/ReactApp/openapi/utf-api.json`. The tracked snapshot did not change.
- **Fix:** I deleted the stray untracked file I had created and ran the export again with an absolute path into `sources/ReactApp/openapi/utf-api.json`. The byte-comparison check also used an absolute temporary path inside the checkout, and that file was deleted afterwards.
- **Files modified:** none beyond the plan's files (the stray file was removed before committing)
- **Verification:** the diff contains only `costOwnerUserId`; `cmp --silent` exits 0; the staged blob contains no CR bytes
- **Committed in:** 578d7f8b

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Only the command invocation changed. CI is not affected because it already exports to the absolute `$RUNNER_TEMP` path.

## Issues Encountered
- The build shows existing CS1591 warnings for undocumented members such as `GameServerDto.CostOwnerId` and `CostOwnerName`. They are outside this plan's scope, and the plan says not to add documentation to other members. The new member produces no warning.
- I found no drift in the snapshot from before this plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The React Mine filter can match `costOwnerUserId === me.id` or `createdById === me.id` using only fleet DTO fields (D-08).

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

## Self-Check: PASSED
- FOUND: sources/Api/DTO/GameServerDto.cs, GameServerProjectionService.cs, GameServerProjectionServiceTests.cs, utf-api.json, api.ts
- FOUND: 737dfe00, 578d7f8b (ancestors of HEAD)

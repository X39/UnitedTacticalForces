---
phase: 63-workspace-shell-activity
plan: 02
subsystem: api
tags: [aspnetcore, dto, projection, openapi, hosting, activity, privacy]

requires:
  - phase: 62-fleet-quick-actions
    provides: CostOwnerUserId projection precedent, one-line DTO documentation rule, absolute-path OpenAPI export
provides:
  - nullable RequestedByName on LifecycleJobDto (job requester nickname)
  - nullable ActorName on HostingOperationDto (audit actor nickname)
  - single ProjectActorName rule with the Hidden member placeholder
  - regenerated React OpenAPI snapshot and TypeScript contract types carrying both fields
affects: [63-03, 63-04, 63-05, 63-06, 63-07, 63-08, 63-09, 63-10, 63-11, 63-12]

actuals:
  tokens: 5050
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Actor display names are projected through one private rule: living nickname, else Hidden member when a user reference exists, else null"
    - "Read queries that project a user navigation Include it explicitly, and the rule keeps unloaded navigations honest"

key-files:
  created: []
  modified:
    - sources/Api/DTO/LifecycleJobDto.cs
    - sources/Api/DTO/HostingOperationDto.cs
    - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
    - sources/Api/Controllers/LifecycleJobsController.cs
    - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts
    - sources/ReactApp/tests/hosting/audit-feed.test.tsx

key-decisions:
  - "Field names are requestedByName and actorName; null means a platform actor"
  - "The single hidden-user placeholder literal is Hidden member, mirrored by hiddenActorName in activityLabels.ts"
  - "The actor is visible to exactly the existing server view audience; only the nickname is projected, never an id, e-mail or claim"
  - "A present user reference with a deleted or unloaded user projects the placeholder, never null, so a member action is never shown as the platform"

patterns-established:
  - "ProjectActorName(User?, Guid?) is the one place that decides actor visibility for job and operation DTOs"

requirements-completed: [ACT-01]

coverage:
  - id: D1
    description: "A user-requested job projects the requester's nickname, a platform job projects null, a deleted or unloaded requester projects Hidden member"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#RequestedByName"
        status: pass
    human_judgment: false
  - id: D2
    description: "An operational audit record projects the actor's nickname, null for platform records and Hidden member for a deleted actor"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#ActorName"
        status: pass
    human_judgment: false
  - id: D3
    description: "A viewer without the server view permission still receives no jobs and therefore no actor names"
    requirement: ACT-01
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs#RequestedByName_IsNotExposedWithoutTheViewPermission"
        status: pass
    human_judgment: false
  - id: D4
    description: "The React OpenAPI snapshot is byte-identical to the API export and the generated types carry both fields as nullable strings"
    verification:
      - kind: command
        ref: "npm --prefix sources/ReactApp run contracts:check"
        status: pass
      - kind: command
        ref: "dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi <absolute path>, then cmp against utf-api.json"
        status: pass
      - kind: command
        ref: "npm --prefix sources/ReactApp run typecheck"
        status: pass
    human_judgment: false

duration: 14min
completed: 2026-11-19
status: complete
commits: 3
plan_head_before: 9c27fe02eb1ce2b9552d0a8220d97a2a7d6254ec
plan_head_after: b50037a3616bd103f5cfd96f24c4e16dca5f07d9
---

# Phase 63 Plan 02: Activity Actor Projection Summary

**Additive `requestedByName` and `actorName` nickname fields on the job and operation DTOs, with one privacy rule (living nickname, `Hidden member` for deleted or unloaded users, null for the platform) and a refreshed React OpenAPI snapshot and generated types.**

## Performance

- **Duration:** about 14 min (start time was not captured at run start; estimated from the prior plan's completion)
- **Completed:** 2026-11-19
- **Tasks:** 3
- **Files modified:** 8 (no files created)

## Accomplishments

- `LifecycleJobDto.RequestedByName` and `HostingOperationDto.ActorName` are projected through the private `ProjectActorName(User?, Guid?)` rule in `GameServerProjectionService`; the `HiddenActorName` constant documents that the React app mirrors it.
- `GetJobsAsync`, `GetOperationsAsync` and `LifecycleJobsController.GetAsync` now include the user navigation. Retry and skip responses, which load jobs without the include, return the placeholder instead of null, so a member action never reads as the platform.
- Eight focused xunit tests: five `RequestedByName` (nickname, platform null, deleted placeholder, unloaded-navigation placeholder, no-view-permission empty list) and three `ActorName` (nickname, platform null, deleted placeholder). The whole `GameServerHosting` folder (649 tests) stays green.
- `utf-api.json` and `api.ts` were regenerated by the export and generator only. The diff contains exactly the two nullable string properties; a second export is byte-identical (`cmp` exit 0); `contracts:check`, `typecheck` and the audit feed test pass.

## Task Commits

1. **Task 1: Tracer, job requester nickname through ProjectJob and the jobs query** - `71033ce1` (feat)
2. **Task 2: Operation actor through ProjectOperation and the operations query** - `6c4fdeff` (feat)
3. **Task 3: Refresh the OpenAPI snapshot and TypeScript contract types** - `b50037a3` (build)

**Plan metadata:** recorded in the docs commit that carries this summary.

## Decisions Made

- The helper is a private static member of `GameServerProjectionService`, shared by `ProjectJob` and the static `ProjectOperation`, so the rule exists once.
- Documentation of the new DTO members is a single-line summary plus value element, matching the Phase 62-02 rule so OpenAPI descriptions carry no platform-dependent line endings. Existing undocumented DTO members were not retrofitted.
- The `E`-prefix, XML documentation and one-type-per-file rules of AGENTS.md were followed; no new type was needed and no migration was created because nothing persisted changed.

## Deviations from Plan

None - plan executed exactly as written.

Notes (not deviations):
- Task 1 had a visible RED run (compile failure on the missing member) before implementation. Task 2 tests and implementation were written together, without a separate failing run, because the rule and fixtures were already proven by Task 1.
- The plan mentioned uncommitted user edits in `sources/Api/Program.cs` and `sources/AppHost/Program.cs`; at execution time neither file had tracked modifications, and neither was touched or staged.

## Issues Encountered

Two of three ad-hoc verification exports to a new relative-to-checkout temporary path left an empty directory with the target name instead of a file (the export then failed opening the path as a file); the third attempt to a fresh name wrote the file normally. The cause was not identified and does not affect the committed snapshot, which was produced by the first export over the tracked file and verified byte-identical. The stray directories were removed and nothing was staged from them. Not pursued further (outside this plan's scope).

## Known Stubs

None.

## Threat Flags

None beyond the plan's register: T-63-04 (nickname only, existing view audience, deleted users withheld) and T-63-05 (placeholder instead of null) are mitigated and covered by tests. No new endpoint, auth path or schema change.

## Next Phase Readiness

Ready for the remaining wave plans. The React side can read `requestedByName` and `actorName` as nullable strings and mirror `Hidden member` as `hiddenActorName` (already defined in `activityLabels.ts` by 63-01). The classic Kiota client under `sources/WebApp/UtfApi` was intentionally not regenerated.

## Self-Check: PASSED

- Modified files exist; commits `71033ce1`, `6c4fdeff` and `b50037a3` are ancestors of HEAD.
- `dotnet test --filter RequestedByName` 5 passed; `--filter ActorName` 3 passed; `--filter GameServerHosting` 649 passed.
- `contracts:check` exit 0, `typecheck` exit 0, `tests/hosting/audit-feed.test.tsx` 7 passed, Prettier clean for the edited fixture.
- `git diff --name-only 9c27fe02 -- sources/WebApp sources/Database` prints nothing.

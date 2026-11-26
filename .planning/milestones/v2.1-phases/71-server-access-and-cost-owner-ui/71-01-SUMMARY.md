---
phase: 71-server-access-and-cost-owner-ui
plan: 01
subsystem: api
tags: [aspnet-core, ef-core, npgsql, openapi, access-control, game-server-hosting]
status: complete

requires:
  - phase: earlier-hosting-phases
    provides: existing per-server access routes and the eight-claim delegation allowlist
provides:
  - "GET /game-servers/{id}/access/users/options: searchable member candidates (fingerprint detail, take 1-50)"
  - "GET /game-servers/{id}/access/roles/options: searchable role candidates with active memberCount"
  - "Detail fingerprint on listed user grants and memberCount on listed role grants"
  - "Assignment-scope rule: a ManageAccess holder may only grant or revoke permissions they hold; assignablePermissions on the access list"
  - "Regenerated utf-api.json and src/generated/api.ts"
affects: [71-02, 71-03, 71-04, 71-05, 71-06, server-access-panel, cost-owner-ui]

actuals:
  tokens: 22900
  tasks: 3
  commits: 3
plan_head_before: 6b56ecb80f069080c1cc2eec19ce1bef13cb0901
plan_head_after: d4908256900791a285ba75ead63c2d041f338d21

tech-stack:
  added: []
  patterns:
    - "Escaped ILIKE search through LikePattern.Contains plus the backslash escape argument"
    - "Offline Npgsql translation tests with ToQueryString on a metadata-only context"
    - "Fingerprint detail (# plus last six hex characters of the id) instead of chat-provider names"

key-files:
  created:
    - sources/Api/Helpers/LikePattern.cs
    - sources/Api/DTO/GameServerUserAccessCandidateDto.cs
    - sources/Api/DTO/GameServerRoleAccessCandidateDto.cs
  modified:
    - sources/Api/DTO/GameServerUserAccessGrantDto.cs
    - sources/Api/DTO/GameServerRoleAccessGrantDto.cs
    - sources/Api/DTO/GameServerAccessDto.cs
    - sources/Api/Services/GameServerLifecycle/IGameServerAccessService.cs
    - sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs
    - sources/Api/Controllers/GameServersController.cs
    - tests/Api.Tests/GameServerHosting/NoOpGameServerAccessService.cs
    - tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs
    - tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts

key-decisions:
  - "Fingerprint detail replaces any chat-provider account name for every viewer (research Critical Finding 2), so no claim-gated user field is widened"
  - "memberCount counts active members only: verified, not deleted, not banned (orchestrator decision 3)"
  - "Assignment scope lives in GameServerAccessService (not the controller): sv and empty-valued adm, adm:sv, adm:hosting may assign everything, everyone else only the allowlisted permissions they hold as exact string-typed claims on this server (orchestrator decision 2)"
  - "The service re-validates take (1-50, HostingValidationException) in addition to the controller bound check so the rule holds for every caller"
  - "Legacy WebApp and its Kiota client untouched; compatibility proven by building sources/WebApp (orchestrator decision 1)"

patterns-established:
  - "Candidate routes: ManageAccess policy on the action, ProblemDetails 400 for a bad take before delegating, TryMapException for the rest"

requirements-completed: [API-02, API-03, API-04, D-17]

coverage:
  - id: D1
    description: "Member candidate search route with exclusion of granted, deleted, banned and unverified users, escaped nickname search, ordering and take bounds"
    requirement: "API-02"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#SearchUserCandidates_ReturnsOnlyActiveMembersWithoutExactGrantOrderedByNicknameThenId"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#ApplyNicknameSearch_TranslatesForNpgsqlToEscapedIlike"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs#UserOptionsEndpoint_WithTakeOutsideBounds_ReturnsBadRequestWithoutDelegating"
        status: pass
    human_judgment: false
  - id: D2
    description: "Role candidate search route with active member counts"
    requirement: "API-03"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#SearchRoleCandidates_CountsOnlyActiveMembers"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#SearchRoleCandidates_ExcludesExactGrantsOrdersByTitleThenIdAndHonorsTake"
        status: pass
    human_judgment: false
  - id: D3
    description: "Fingerprint detail on candidates and listed user grants without any chat-provider name"
    requirement: "API-04"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#List_ProjectsTheUserFingerprintAndTheRoleActiveMemberCount"
        status: pass
    human_judgment: false
  - id: D4
    description: "Assignment scope: ManageAccess holders cannot hand out permissions they do not hold; assignablePermissions reported on the access list"
    requirement: "D-17"
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#ManageAccessAlone_CannotGrantFullControlToSelf"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#ManageAccessWithView_CanOnlyAssignViewAndManageAccessToUsersAndRoles"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs#Get_ReportsAssignablePermissionsInAllowlistOrderForTheCaller"
        status: pass
    human_judgment: false
  - id: D5
    description: "OpenAPI snapshot and generated TypeScript types regenerated and in sync; legacy WebApp still builds untouched"
    requirement: "D-17"
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run contracts:check; dotnet build sources/WebApp/WebApp.csproj"
        status: pass
    human_judgment: false
---

# Phase 71 Plan 01: Server access candidates and delegation scope Summary

**Searchable member and role candidate routes with id-fingerprint disambiguation and active member counts, plus a server-side rule that a ManageAccess holder can only assign the permissions they hold, exposed as `assignablePermissions`.**

## Performance

- **Duration:** about 40 min
- **Completed:** 2026-11-26
- **Tasks:** 3 (1 tracer, 2 auto/TDD)
- **Files changed:** 14 (3 created, 11 modified)
- **Commits:** 3 (measured from the plan head ledger)

## Accomplishments

- `GET /game-servers/{id}/access/users/options` and `.../roles/options` behind the ManageAccess policy, `take` 1 to 50 (default 20; 0 and 51 answer 400), exclusion of every subject that already holds an exact allowlisted grant on the server, escaped case-insensitive search (`LikePattern`), ordered by nickname or title then id.
- Candidates and listed user grants carry `detail` = `#` plus the last six hex characters of the user id; no field of the linked chat-provider account is read anywhere in the service (grep gate: 0 matches).
- Role candidates and listed role grants carry `memberCount` (active members only), computed in the query projection and in one extra keyed count query for the list.
- Delegation gap closed: grants and revokes outside the caller's own effective per-server permissions answer 403 before any lookup, write or audit record; `sv:manage-access` alone cannot grant `sv`. `GET /access` returns `assignablePermissions` in allowlist order.
- `utf-api.json` and `src/generated/api.ts` regenerated in each task; `contracts:check` is clean after the final commit; `sources/WebApp` is byte-identical to the base and still builds.

## Task Commits

1. **Task 1: member candidate search (tracer)** - `c3195e73` (feat)
2. **Task 2: role candidates, member counts, detail fingerprint** - `e301e927` (feat)
3. **Task 3: assignment scope and assignable set** - `d4908256` (fix)

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj` (whole project): 1762 passed, 0 failed.
- Access and OpenAPI filters pass after each task; Npgsql translation of the nickname search, the title search and the role member count is proven offline with `ToQueryString`.
- Exported snapshot equals the committed `utf-api.json` (generated from the same build in each task); `npm --prefix sources/ReactApp run contracts:check` leaves no diff.
- `dotnet build sources/WebApp/WebApp.csproj`: 0 errors; `git diff --quiet 6b56ecb8 -- sources/WebApp` exits 0.
- Acceptance greps: all PASS (LikePattern 1, `access/users/options` 1, `access/roles/options` 1, `SearchUserCandidatesAsync` in interface 1 and NoOp 1, generated paths present, `Discord` in the access service 0, `ManageAccessAlone_CannotGrantFullControlToSelf` 1, `HostingForbiddenException` 2).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] XML documentation on the whole access service surface**
- **Found during:** Task 1
- **Issue:** `IGameServerAccessService`, `GameServerAccessService` and its public members had no XML documentation, which AGENTS.md requires for every type and member the plan touches.
- **Fix:** documented every member of the interface (summary, params, returns, exceptions), added `<inheritdoc />` to the implementation members and a documented class header; new DTOs, helper and controller routes carry final-wording docs (they become OpenAPI descriptions).
- **Files modified:** `IGameServerAccessService.cs`, `GameServerAccessService.cs`
- **Commit:** `c3195e73`

**2. [Rule 2 - Missing critical functionality] Service-level take validation**
- **Found during:** Task 1
- **Issue:** the plan bounds `take` only in the controller; a second caller of the service would bypass the cap.
- **Fix:** the service throws `HostingValidationException` (maps to 400) for `take` outside 1 to 50; the controller keeps the ProblemDetails 400 and never calls the service for a bad take.
- **Commit:** `c3195e73`

**Total deviations:** 2 auto-fixed (both Rule 2). **Impact:** none on scope or contract; the second is defense in depth.

## Authentication Gates

None.

## Issues Encountered

- The shell wrapper refused compound `git` commands in a worktree; they were split into plain commands. No effect on the result.
- The Claims table has a unique index on identifier and value, so the exclusion test uses a different permission per grant variant (as the existing list test does).

## Known Stubs

None.

## Threat Flags

None. The new routes and rule are the ones the plan's threat model covers (T-71-01 to T-71-05): EoP mitigated by the assignment-scope rule and its tests, disclosure limited by the ManageAccess policy, take cap and fingerprint detail, LIKE wildcards escaped.

## Cross-plan findings

- **XPF-71-01-1:** the React Access panel plans (later plans of phase 71) must drive the permission toggles from `assignablePermissions`, not `allowedPermissions`, and show non-assignable permissions as read-only; otherwise a ManageAccess-only member sees controls that now answer 403.
- **XPF-71-01-2:** the other actions of `GameServersController` still have no XML documentation (pre-existing); only the new routes were documented here. Worth a separate documentation pass.

## Next Phase Readiness

The contract carries everything plans 71-03 onward need from the API for the Access panel (candidates, detail, memberCount, assignablePermissions). Ready for the wave 2 plans.

## Self-Check: PASSED

- Created files exist: `LikePattern.cs`, `GameServerUserAccessCandidateDto.cs`, `GameServerRoleAccessCandidateDto.cs`.
- Commits `c3195e73`, `e301e927`, `d4908256` are ancestors of HEAD.

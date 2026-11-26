---
phase: 65-change-a-server
plan: 04
subsystem: api
tags: [hosting, secrets, pending-start, lifecycle-executor, conflict-409]

requires:
  - phase: 65-01
    provides: PendingStartChanges mark and consume rules, pending-start columns on GameServer
provides:
  - Credentials area marked as pending in the same save as a set or rotate secret version and its audit record
  - Archived servers reject secret set and rotate with HostingConflictException mapped to 409 Conflict
  - Secret controller response annotations and XML documentation
  - Command-creation consumption of Configuration and Credentials for the primary Start or Restart command
affects: [65-05, 65-09, change-a-server banner, change-a-server credentials section]

plan_head_before: afac4b2360bc25ad460096ca99d05285efabe191
plan_head_after: 3d2cce12b07ca2afc865652d75943340f4574880

actuals:
  tokens: 14000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Executor consumes pending-start flags inside the server-mutation transaction that persists the rendering command"
    - "Archived state enforced in the owning service, mapped to 409 in the controller"

key-files:
  created:
    - tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs
  modified:
    - sources/Api/Services/GameServerSecrets/GameServerSecretService.cs
    - sources/Api/Services/GameServerSecrets/IGameServerSecretService.cs
    - sources/Api/Controllers/GameServerSecretsController.cs
    - sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs
    - tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs
    - tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs

key-decisions:
  - "Reveal stays allowed and audited on archived servers (read-only); only set and rotate reject archived servers"
  - "Archived rejection is HostingConflictException with a fixed sentence, mapped to 409 ProblemDetails, so no secret value can reach a response"
  - "Consumption happens only when a new primary command is created, after reloading the server entry, so reused commands and auxiliary or readiness commands never consume"

patterns-established:
  - "PendingStartChanges.ConsumeAtCommandCreation is called once in EnsureCommandPersistedAsync before its SaveChangesAsync"

requirements-completed: [CHG-07, CHG-04]

coverage:
  - id: D1
    description: "Setting or rotating a credential marks Credentials as waiting for the next start in the same save as the version and audit record"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#SetAsync_MarksCredentialsPending"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#RotateAsync_MarksCredentialsAndKeepsEarlierPendingSince"
        status: pass
    human_judgment: false
  - id: D2
    description: "Archived servers reject credential set and rotate with 409 before any change; reveal remains allowed and audited"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#SetAndRotate_OnArchivedServer_ThrowConflictAndChangeNothing"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs#SetAsync_OnArchivedServer_ReturnsConflictWithoutTheValue"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs#RevealAsync_OnArchivedServer_ReturnsValueAndAudits"
        status: pass
    human_judgment: false
  - id: D3
    description: "Secret endpoint responses never contain a submitted secret value"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs#SetAsync_OnArchivedServer_ReturnsConflictWithoutTheValue"
        status: pass
    human_judgment: false
  - id: D4
    description: "A Restart primary command consumes Configuration and Credentials but keeps ModPack; collect-logs and reused commands consume nothing"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#RestartCommand_ConsumesConfigurationAndCredentialsButKeepsModPack"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#CollectLogsCommand_LeavesPendingAreasUntouched"
        status: pass
      - kind: unit
        ref: "tests/Api.Tests/GameServerHosting/LifecycleJobExecutorCloudTests.cs#ReusedRestartCommand_DoesNotConsumeAgain"
        status: pass
    human_judgment: false

duration: 14min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 04: Credential pending marks, archived rejection and command-creation consumption Summary

**Credential set and rotate now mark Credentials pending and reject archived servers with 409, and the primary Start or Restart worker command consumes pending Configuration and Credentials atomically with the command that froze them.**

## Performance

- **Duration:** 14 min (about 19:43Z to 19:57Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 7 changed (1 created, 6 modified)

## Accomplishments

- `GameServerSecretService.SetAsync` and `RotateAsync` throw `HostingConflictException("Archived servers cannot change their credentials.")` right after loading the server, before any retire or add, and call `PendingStartChanges.MarkChanged(server, Credentials, now)` before the single `SaveChangesAsync`, so the mark commits with the new version and its `Key=` audit record. `RevealAsync`, `LoadAsync` and audit detail strings are unchanged.
- `GameServerSecretsController` maps `HostingConflictException` to `Problem(409)` for set and rotate, carries the full `ProducesResponseType` set (204, 400, 403, 404, 409) and has XML documentation on the class and all four actions.
- `LifecycleJobExecutor.EnsureCommandPersistedAsync` reloads the server entry on the new-command path and calls `PendingStartChanges.ConsumeAtCommandCreation` before its save, inside the existing server-mutation transaction. The early return for an existing command, auxiliary and readiness commands, and non Start/Restart job kinds leave the flags untouched.
- Tests: four service cases, four controller cases (new file) and three executor cases.

## Task Commits

1. **Task 1 (tracer): credential marks and archived rejection** - `9990681d` (fix(api))
2. **Task 2: command-creation consumption** - `3d2cce12` (feat(api))

## Verification Results

- Tracer gate: `dotnet test --filter GameServerSecret` passed (10 tests) and was verified before expansion.
- `dotnet test --filter LifecycleJobExecutorCloudTests|PendingStartChangeTracking` passed (74 tests).
- `dotnet test --filter GameServerHosting` passed (680 tests, 0 failed).
- `dotnet build X39.UnitedTacticalForces.slnx` succeeded, 0 errors.
- Acceptance greps: Credentials 2, IsArchived 2, controller HostingConflictException 2, Status409Conflict 4, ConsumeAtCommandCreation 1, PendingStartChangeAreas in executor tests 5.
- No commit touches `sources/ReactApp`, `sources/WebApp` or `sources/CloudWorker`; no change to the worker, worker protocol or command payload contents.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] XML documentation on the secret service contract**
- **Found during:** Task 1
- **Issue:** AGENTS.md requires XML documentation with `<exception>` elements on every touched member. `SetAsync` and `RotateAsync` are declared on `IGameServerSecretService`, which the plan did not list and which had no docs.
- **Fix:** Documented `SetAsync` and `RotateAsync` on the interface (including `HostingConflictException`), added `<inheritdoc />` on the two implementations and a class summary on `GameServerSecretService`.
- **Files modified:** sources/Api/Services/GameServerSecrets/IGameServerSecretService.cs, sources/Api/Services/GameServerSecrets/GameServerSecretService.cs
- **Commit:** 9990681d

**Total deviations:** 1 auto-fixed (documentation requirement). **Impact:** documentation only, no behavior change.

## Issues Encountered

None. The plan's TDD ordering was relaxed in the same way as plan 65-01: implementation and tests were written together and then run green rather than committed as separate RED and GREEN steps.

## Known Stubs

None.

## Threat Flags

None. T-65-09 to T-65-11 are mitigated as planned (archived rejection before any version change, fixed-sentence 409 detail, audit records unchanged including reveal on archived servers); T-65-12 remains accepted.

## Next Phase Readiness

The pending-start signal is now complete on the server for configuration and credentials (mod pack marking belongs to plan 65-05). Plan 65-09 regenerates the OpenAPI snapshot, which will now include the 409 and 400 response annotations of the secret mutation endpoints.

## Self-Check: PASSED

- Created file `tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs` verified on disk; commits `9990681d` and `3d2cce12` are ancestors of HEAD.

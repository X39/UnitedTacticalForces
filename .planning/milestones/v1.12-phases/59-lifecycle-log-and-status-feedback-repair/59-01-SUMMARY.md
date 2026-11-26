---
phase: 59-lifecycle-log-and-status-feedback-repair
plan: 01
subsystem: api-ui
tags: [lifecycle-jobs, operational-status, ef-core, blazor, kiota]

requires:
  - phase: 58-cloud-lifecycle-and-cost-policy-repair
    provides: durable lifecycle jobs, cloud runtime status, worker/provider projections
provides:
  - Durable lifecycle job status reasons with timestamps and sanitized detail.
  - API runtime/job/worker/provider operational labels for selected-server surfaces.
  - WebApp timeline rows that render status labels, reasons, requester, worker, timestamps, and next steps.
affects: [game-server-lifecycle, cloud-runtime-status, selected-server-workspace, webapp-generated-client]

tech-stack:
  added: []
  patterns:
    - Persist lifecycle transition reason separately from execution status.
    - Project display-ready operational labels in the API before Razor rendering.
    - Map generated DTO labels into hand-written WebApp repository models.

key-files:
  created:
    - sources/Contract/GameServer/EOperationalStatusKind.cs
    - sources/Contract/GameServer/ELifecycleJobStatusReason.cs
    - sources/Api/Data/Migrations/20260705124532_AddLifecycleJobStatusReasons.cs
    - sources/WebApp/UtfApi/Models/EOperationalStatusKind.cs
    - sources/WebApp/UtfApi/Models/ELifecycleJobStatusReason.cs
  modified:
    - sources/Api/Data/Hosting/LifecycleJob.cs
    - sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs
    - sources/Api/DTO/LifecycleJobDto.cs
    - sources/Api/DTO/CloudRuntimeStatusDto.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Pages/Servers/Details.razor

key-decisions:
  - "Lifecycle transition evidence is stored as owner-safe reason enum/detail/timestamp fields instead of inferred in the client."
  - "Selected-server operational labels are projected by the API and only fallback to stable English labels in the WebApp."
  - "Generated WebApp Kiota models were updated manually because the plan allowed regeneration or update and no running API/OpenAPI endpoint was available."

patterns-established:
  - "Lifecycle job DTOs carry StatusKind, StatusLabel, StatusReasonDetail, NextStepLabel, and RequestedByLabel for timeline rendering."
  - "Cloud runtime status DTOs carry separate runtime, worker, provider, and job operational label triplets."
  - "Selected-server Razor surfaces use explicit labels such as Unknown, Not available, Waiting for worker, and No lifecycle job instead of unexplained n/a."

requirements-completed: [OPS-01, OPS-03]

duration: 2h15m
completed: 2026-07-05
---

# Phase 59 Plan 01 Summary

**Durable lifecycle status reasons and selected-server operational labels for job/runtime/worker/provider feedback**

## Performance

- **Duration:** 2h15m
- **Started:** 2026-07-05T12:45:32+02:00
- **Completed:** 2026-07-05T15:00:09+02:00
- **Tasks:** 3
- **Files modified:** 26

## Accomplishments

- Added persisted lifecycle status reason, detail, and timestamp fields plus an EF migration and transition writes for queued, claimed, retry, stale recovery, final failure, and success paths.
- Added owner-safe operational status taxonomy and API DTO projections for lifecycle jobs plus runtime, worker, provider, and job status labels.
- Updated WebApp generated models, repository records, workspace summaries, and `Details.razor` so selected-server status surfaces show labels, reasons, requester/worker context, timestamps, and next steps.

## Task Commits

1. **Task 1: Add durable status-reason fields and transition writes** - `ee2b9dac` (feat)
2. **Task 2: Project lifecycle and runtime operational labels server-side** - `5236096a` (feat)
3. **Task 3: Map labels into WebApp models and repair the job timeline UI** - `9b6f44a5` (feat)

## Files Created/Modified

- `sources/Contract/GameServer/ELifecycleJobStatusReason.cs` - durable transition reason enum.
- `sources/Contract/GameServer/EOperationalStatusKind.cs` - owner-safe selected-server status taxonomy.
- `sources/Api/Data/Hosting/LifecycleJob.cs` - persisted reason/detail/timestamp fields.
- `sources/Api/Data/Migrations/20260705124532_AddLifecycleJobStatusReasons.cs` - EF migration for lifecycle reason fields.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` - transition writes for claim, retry, stale recovery, failure, and completion.
- `sources/Api/DTO/LifecycleJobDto.cs` and `sources/Api/DTO/CloudRuntimeStatusDto.cs` - operational label/reason projection fields.
- `sources/Api/Transform.cs` and `sources/Api/Controllers/GameServersController.cs` - server-side label, next-step, and reason projection logic.
- `sources/WebApp/UtfApi/Models/*.cs` - generated client DTO/enum updates for the new API fields.
- `sources/WebApp/Services/GameServerRepository/*.cs` - hand-written repository model mappings for operational status fields.
- `sources/WebApp/Pages/Servers/Details.razor` - selected-server runtime/worker/provider labels and lifecycle job timeline rows.
- `tests/Api.Tests/GameServerLifecycle/*.cs`, `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`, and `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - focused coverage for transition reasons, API projections, and WebApp source boundaries.

## Decisions Made

- Kept the API as the source of display truth for operational labels so Razor does not infer lifecycle semantics from raw enum combinations.
- Used stable English labels for this phase, matching the plan, rather than introducing localization plumbing mid-repair.
- Manually updated generated Kiota files because no local running API/OpenAPI endpoint was available; the changes follow existing generated model patterns.

## Deviations from Plan

None - plan scope was executed as written. The generated client output was updated manually under the plan's "regenerate or update" instruction.

## Issues Encountered

- `.codex.md` was referenced by the plan but was not present in the repository. Execution continued from AGENTS instructions and the phase context artifacts.
- WebApp build still reports pre-existing duplicate-using/nullability warnings outside this task. No new build errors were introduced.

## Verification

- `rg -n "ELifecycleJobStatusReason|StatusReason|TimeStampStatusReason|WorkerLeaseExpiredRecovered|WorkerLeaseExpiredFailed" sources\Contract sources\Api`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~LifecycleJobQueueServiceTests|FullyQualifiedName~GameServerLifecycleJobServiceTests"`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~GameServerLifecycle" --logger "console;verbosity=minimal"`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --logger "console;verbosity=minimal"`
- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- `rg -n "RuntimeStatus\.ToString\(\) \?\? MissingValue|job\.Status\.ToString\(\)|job\.FailureReason \?\? MissingValue|job\.AssignedWorkerId \?\? MissingValue" sources\WebApp\Pages\Servers\Details.razor`

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 59-01 is ready for the next wave. The selected-server workspace now receives durable job evidence and operational labels that later log streaming and recovery feedback can build on.

---
*Phase: 59-lifecycle-log-and-status-feedback-repair*
*Completed: 2026-07-05*

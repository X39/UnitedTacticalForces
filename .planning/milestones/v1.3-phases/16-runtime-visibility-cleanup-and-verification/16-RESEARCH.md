---
phase: 16
type: research
status: complete
created_at: "2026-06-17T15:55:00.0000000Z"
---

# Phase 16 Research

## Relevant Code

- `sources/Api/Controllers/GameServersController.cs`
  - Existing server-scoped endpoints include lifecycle jobs, cloud status, logs, and lifecycle job enqueue actions.
  - `GetCloudSessionStatusesAsync` already exposes safe cloud session/worker status for a single server.
  - `EnqueueLifecycleJobAsync` centralizes authenticated lifecycle job queueing and maps invalid queue attempts to
    `403`.
- `sources/Api/Services/CloudInventory/CloudInventoryService.cs`
  - Builds safe cloud session status projections using `CloudGameServerSession`, `CloudResourceInventoryRecord`,
    `CloudWorkerBootstrapCredential`, `LifecycleWorker`, and latest dry-run plan state.
- `sources/Api/Services/GameServerLifecycle/GameServerLifecycleJobService.cs`
  - Existing `Delete` job kind is queueable when the local controller is not running. For cloud cleanup, the worker is
    the executor and provider teardown is separate.
- `sources/CloudWorker/Arma3LifecycleJobExecutor.cs`
  - Handles install, update, configure, start, stop, restart, and collect logs.
  - Unsupported job kinds currently report retryable failure.
- `sources/CloudWorker/Arma3WorkerPaths.cs`
  - Computes all worker-local runtime paths under `{installRoot}/233780/{gameServerId:00000000}`.
- `sources/WebApp/Pages/Servers/Index.razor`
  - Existing selected-server details already show cloud status, lifecycle jobs, logs, and management buttons.
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`
  - Contains SQLite-backed controller/service visibility tests and helper setup.
- `tests/Api.Tests/CloudWorker/Arma3LifecycleJobExecutorTests.cs`
  - Contains fake command/process runner tests for worker lifecycle behavior and redaction.

## Decisions

- Use an additive `CloudRuntimeStatusDto` rather than modifying the existing `CloudSessionStatusDto`; the new DTO is a
  runtime-first projection for server owner/admin views.
- Use the existing `ELifecycleJobKind.Delete` as the worker-side runtime cleanup operation behind a dedicated
  `cloud-runtime/cleanup` route. Provider teardown remains under cloud inventory and keeps its own cost-stop semantics.
- Use failed `RetryPending` lifecycle jobs as the retry visibility source; no retry endpoint is added in this phase
  because queue recovery already makes retry-pending jobs worker-claimable.
- Keep WebApp calls as raw HTTP for the new endpoints, matching newer lifecycle repository methods and avoiding Kiota
  regeneration churn.

## Risks

- API-computed cleanup flags can only reason from persisted API state, not live process state. The worker still performs
  defensive stop/log collection before deleting runtime files.
- `Delete` has legacy local server-delete meaning. The new cleanup endpoint only uses it for active cloud sessions with
  a cloud worker, and the existing server delete endpoint remains admin-only.
- Authenticated browser UAT cannot be fully proven without local Steam/Discord sessions and representative cloud data.

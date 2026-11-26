---
phase: 15
type: research
status: complete
created_at: "2026-06-17T15:40:00.0000000Z"
---

# Phase 15 Research

## Relevant Code

- `sources/CloudWorker/Arma3LifecycleJobExecutor.cs`
  - Currently handles `Install`, `Update`, and `Configure`.
  - Unsupported job kinds report retryable failure.
- `sources/CloudWorker/Arma3WorkerPaths.cs`
  - Matches local layout: `{installRoot}/233780/{gameServerId:00000000}` with `server-instance`, `mods`,
    `battl-eye`, and `profiles`.
- `sources/Api/Controllers/LifecycleWorkersController.cs`
  - Worker-facing routes live under `lifecycle-workers/cloud/{workerId}` and use bearer worker credentials.
- `sources/Api/Services/CloudWorkerDispatch/CloudWorkerDispatchService.cs`
  - Authenticates cloud workers, enforces session-scoped job reports, persists logs, and delegates terminal job state to
    `LifecycleJobQueueService`.
- `sources/Api/Services/CloudWorkerRegistration/CloudWorkerRegistrationService.cs`
  - Handles registration and heartbeat; heartbeat is the right place for jobless restart recovery observations.
- `sources/Api/Services/GameServerController/Controllers/Arma3GameServerController.cs`
  - Local launch argument source for Arma process compatibility.

## Decisions

- Use the existing `ELifetimeStatus` enum for worker runtime status reports instead of adding a new public status enum.
- Persist worker runtime state as a worker-local JSON file rather than adding a new API table in Phase 15; API durable
  state remains `LifecycleJobs`, `GameServer.Status`, `LifetimeEvents`, and `GameServerLogs`.
- Use a detached Linux launch wrapper that writes stdout/stderr to a runtime log file and stores a pid, so the worker can
  return from a start job while Arma continues running.
- Worker heartbeat may include optional runtime status. The API updates the assigned cloud session's game server only
  after normal worker credential/session authentication.

## Risks

- Live Arma startup semantics are not verified without operator credentials and installed game files.
- Detached process launch and signal handling are Linux-focused; tests cover command/state behavior with fakes.
- Long-running process log tailing is pull-based through collect/start/stop/recovery jobs in this phase, not continuous
  streaming after the worker exits.

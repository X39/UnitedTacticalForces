---
phase: 15
type: context
status: complete
created_at: "2026-06-17T15:40:00.0000000Z"
requirements:
  - RUN-08
  - RUN-09
  - RELY-10
  - RELY-11
---

# Phase 15 Context

## Objective

Let the cloud worker start, stop, restart, observe, and recover the native Arma 3 process while durable API state
remains authoritative after API or worker restarts.

## Existing Foundation

- Phase 13 added cloud worker registration, credential-authenticated claim/report endpoints, persisted lifecycle jobs,
  persisted game-server logs, and retry/abandon recovery.
- Phase 14 added worker-side Arma 3 install/update/configuration execution, tool validation, config rendering, workshop
  downloads, lowercased workshop content, progress/log/success/failure reporting, and claim mod-pack snapshots.
- Existing local Arma 3 controller remains the compatibility source for process arguments: `arma3server_x64`,
  `-pid`, `-cfg`, `-config`, `-bepath`, `-profiles`, `-port`, `-serverMod`, and `-mod`.

## Implementation Shape

- Keep the worker project API-independent; shared enum types come from `sources/Contract`.
- Add worker runtime state persisted under the worker state directory so process id, log path, and log offsets survive
  worker restarts.
- Add a worker process runner abstraction so tests can verify launch/stop/recovery without starting Arma.
- Extend the worker/API protocol with runtime-status reports:
  - job-scoped reports for start/stop/restart/collect-log jobs;
  - heartbeat-carried status for worker restart recovery when no job is running.
- Persist worker-reported runtime status into `GameServer.Status` and `LifetimeEvent`, and broadcast update-stream
  messages through the existing `IUpdateStreamService`.

## Constraints

- Do not implement provider cleanup or cloud resource deletion in Phase 15.
- Do not expose Steam credentials, worker credentials, or secret-bearing command lines in DTOs, logs, audits, or
  failure messages.
- Keep local worker/controller behavior intact.
- Tests must use fake process execution; live Arma runtime is operator/environment gated.

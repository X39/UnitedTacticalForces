---
phase: 16
type: context
status: planned
created_at: "2026-06-17T15:55:00.0000000Z"
requirements:
  - HETZ-07
  - CLN-03
  - VIS-10
  - VIS-11
  - RELY-13
---

# Phase 16 Context

## Objective

Complete the v1.3 cloud Arma runtime loop by making runtime state visible to admins and server owners, adding safe
runtime cleanup semantics, and closing verification coverage for the cloud worker lifecycle.

## Existing Foundation

- Phase 13 added cloud worker bootstrap, registration, credential-authenticated job claim/report endpoints, scoped
  lifecycle job claims, persistent logs, and retry/lease recovery.
- Phase 14 added cloud worker Arma 3 install, update, configuration rendering, SteamCMD/DepotDownloader execution, and
  redacted tool output.
- Phase 15 added cloud worker start/stop/restart/collect-log execution, worker-local process state, runtime-status
  reports, heartbeat recovery, durable `GameServer.Status`, `LifetimeEvent`, `GameServerLog`, and update-stream output.
- The WebApp server dashboard already shows server status, lifecycle jobs, cloud session status, recent logs, and
  management actions through `IGameServerRepository`.

## Implementation Shape

- Add a server-scoped runtime summary DTO and endpoint under `game-servers/{gameServerId}` that composes existing
  persisted rows:
  - cloud session endpoint and provisioned state;
  - registered worker, heartbeat status, and last heartbeat;
  - current durable lifecycle job and progress/failure;
  - recent log timestamp and last sanitized failure;
  - API-computed `CanRetry` and `CanCleanup` action flags.
- Add a cloud runtime cleanup endpoint that queues a cloud-scoped lifecycle cleanup job only when the persisted runtime
  state says cleanup is safe and applicable.
- Implement worker-side cleanup by stopping the owned Arma process if present, collecting remaining runtime logs, and
  deleting only the computed server runtime directory under the configured install root.
- Extend the existing `/servers` Blazor page rather than creating a separate runtime page.

## Constraints

- Runtime cleanup must not call provider teardown and must not mark cloud provider resources or sessions deleted.
- Server owner visibility must stay resource-scoped; global cloud inventory remains admin-only.
- Runtime retry and cleanup controls must use API-computed flags instead of frontend-only inference.
- Steam credentials, worker credentials, provider tokens, and secret-like failure text must remain absent from DTOs,
  persisted logs, audits, and UI projections.
- Live authenticated/browser and live provider UAT remain operator-gated unless explicit credentials and cleanup windows
  are provided.

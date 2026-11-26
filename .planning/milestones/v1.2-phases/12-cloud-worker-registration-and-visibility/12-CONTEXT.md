# Phase 12: Cloud Worker Registration And Visibility - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Let bootstrapped cloud workers register, heartbeat, report capabilities, survive API restarts, and expose safe provisioning/bootstrap controls in admin and owner views.

**Requirements:** WORK-08, WORK-09, WORK-10, RELY-08, VIS-07, VIS-08, VIS-09, RELY-09

**Success Criteria:**

1. Cloud worker authenticates to the API, registers itself, and links to its cloud session and inventory records.
2. Cloud worker heartbeat and capability reports include game type support, operating system, architecture, region, and one-session worker shape.
3. Persisted worker projections distinguish local and cloud workers without regressing existing local worker execution.
4. API restart preserves provisioned resource state, worker identity, heartbeat state, cleanup-needed state, and bootstrap status.
5. Admin and server-owner views expose provisioning, bootstrap, teardown, cleanup-needed, orphan-candidate, and retryable states without exposing secrets.

</domain>

<decisions>
## Implementation Decisions

### Reuse Existing Worker Registry

Extend `LifecycleWorker` and `LifecycleWorkerRegistryService` for cloud workers rather than introducing a parallel cloud-worker table. Local workers must continue to heartbeat and claim lifecycle jobs with the same API behavior they have today.

### Keep Worker Secrets Out Of DTO Bodies

Use worker-facing request/response headers for bootstrap and worker authentication material. Public DTOs, generated WebApp models, audit records, logs, and provider status output must never contain raw worker credential material or credential hashes.

### State-Driven Controls

Expose retry and cleanup controls only from persisted state. If an already-created VM has no secure bootstrap delivery path, show bootstrap as not retryable instead of creating a UI action that cannot safely work.

</decisions>

<code_context>
## Existing Code Insights

- `LifecycleWorker` currently persists worker id, display name, enabled flag, registration timestamp, heartbeat timestamp, and last claimed job timestamp.
- `LifecycleWorkerRegistryService.HeartbeatAsync` creates or updates local worker rows and is used by `LocalGameServerLifecycleWorker`.
- Phase 11 added `CloudWorkerBootstrapCredential` and `ICloudWorkerBootstrapService.ConsumeBootstrapAsync` for one-time bootstrap validation.
- `CloudGameServerSession` already stores game server, billing account, billing assignment, provider, placement, public endpoint, resource state, and deletion timestamps.
- `CloudInventoryController` exposes admin-only cloud session, resource, dry-run, estimate, provisioning, and teardown endpoints.
- `GameServersController` exposes resource-scoped server reads under `game-servers/{gameServerId}` for server owners.
- `sources/WebApp/Pages/Servers/Cloud.razor` is the existing admin cloud operations surface and `sources/WebApp/Pages/Servers/Index.razor` is the existing server-owner/admin server detail surface.

</code_context>

<specifics>
## Specific Ideas

- Add cloud metadata to `LifecycleWorker`: kind, cloud session id, game server id, billing snapshots, provider/region hints, OS, architecture, supported controller identifiers, one-session shape, credential hash, and last capability timestamp.
- Add worker-facing endpoints under `lifecycle-workers/cloud/{workerId}` for registration and heartbeat.
- Registration should consume the Phase 11 bootstrap credential, persist the cloud worker link, and return a safe body plus one raw worker credential in a response header.
- Heartbeat should require the worker id and worker credential via headers and should update heartbeat/capability fields without appending noisy heartbeat audit records.
- Admin cloud UI should show sessions, workers, bootstrap state, cleanup-needed/orphan states, and safe action availability.
- Server-owner view should show only cloud status for the selected game server and never show provider tokens or worker credentials.

</specifics>

<deferred>
## Deferred Ideas

Cloud Arma 3 install/start/update operations remain deferred beyond v1.2. A standalone bootstrap retry for an already-created VM remains unavailable until a secure delivery channel exists.
</deferred>

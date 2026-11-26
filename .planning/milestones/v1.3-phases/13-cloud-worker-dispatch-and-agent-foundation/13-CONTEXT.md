# Phase 13: Cloud Worker Dispatch And Agent Foundation - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Add the worker-facing job protocol and cloud worker agent foundation so a registered cloud worker can
safely claim only lifecycle jobs for its own cloud session and game server, execute from bounded API
snapshots without direct database access, and report progress, logs, success, retry, and failure while
preserving local worker compatibility and secret redaction.

</domain>

<decisions>
## Implementation Decisions

### Worker Authentication And Credential Lifecycle
- Authenticate worker job calls with dedicated worker credential headers using the existing Phase 12
  worker identity and hash-only API storage. Browser cookie/user auth must remain separate from worker
  authentication.
- Support explicit server-side worker credential rotation or invalidation, but do not auto-rotate during
  active jobs in this phase so long-running install/update operations are not interrupted.
- Return raw worker credential material only at registration/resume handoff and never in later DTOs,
  generated client models, logs, audits, job errors, or normal status responses.
- The worker stores its raw credential locally with restrictive filesystem permissions. The API stores
  only verifier/hash material.

### Job Claiming And Execution Snapshot Shape
- Cloud workers claim lifecycle jobs through worker-facing endpoints, not direct database access.
- Claim eligibility must be constrained by the authenticated worker's persisted cloud session and game
  server link. Local worker claiming behavior remains compatible with existing local lifecycle jobs.
- Claim responses should return bounded immutable execution snapshots: lifecycle job identity, operation
  type, server identity, controller/game type, selected runtime/configuration data required by the worker,
  public endpoint/ports, and non-secret tool/runtime settings.
- Snapshots must exclude provider tokens, Steam passwords, bootstrap credentials, worker credential hashes,
  bearer values, and secret-bearing command lines. Include only references or redacted markers where a
  later worker step must resolve a server-side secret.

### Worker Reporting And Durable State Transitions
- Worker reports should use credential-authenticated endpoints for heartbeat, progress, logs, runtime
  status, success, retryable failure, terminal failure, and lease refresh.
- Reports update durable lifecycle job state, lifecycle status, sanitized game-server logs, update-stream
  visible events, and hosting audit records through API-owned services.
- Heartbeat/progress reporting should avoid noisy immutable audit entries for routine pings while still
  persisting meaningful state transitions, failures, retries, and terminal outcomes.
- Worker retry reporting should be explicit: a worker can report that a job should be retried only when
  the persisted job state and attempt budget allow it.

### Local Compatibility And Security Boundaries
- Extend the existing `LifecycleWorker` / registry model from Phase 12 rather than creating a parallel
  cloud-worker table or bypassing the lifecycle job model.
- Keep current local Arma 3 worker behavior operational; cloud scoping rules must not prevent local workers
  from claiming local lifecycle jobs.
- Keep all worker-facing DTOs/API responses secret-safe by default and add tests for representative
  redaction cases covering Steam, provider, bootstrap, worker, hash, bearer, and command-line material.
- Prefer small API adapter/controller/service additions around existing lifecycle services instead of
  placing worker protocol logic directly into large existing controllers.

### the agent's Discretion
Implementation details not fixed above are at the agent's discretion, including exact endpoint naming,
DTO granularity, database indexes, service boundaries, and test helper design, provided they preserve
the Phase 13 requirements and existing repository conventions.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LifecycleWorker` and `LifecycleWorkerRegistryService` already persist worker identity, registration,
  heartbeat, capabilities, cloud session/game server links, and credential hash material from Phase 12.
- `CloudWorkerBootstrapCredential` and `ICloudWorkerBootstrapService.ConsumeBootstrapAsync` provide the
  one-time bootstrap validation path created in Phase 11.
- `CloudGameServerSession` stores the cloud session, game server, provider placement, public endpoint,
  billing owner snapshots, cleanup state, and provider resource state.
- The API already has persisted lifecycle jobs, job leases, game-server logs, hosting audit records, and
  update-stream message patterns from v1.0-v1.2.

### Established Patterns
- Server-side entities live under `sources/Api/Data/*`, DTOs under `sources/Api/DTO`, shared enum/contracts
  under `sources/Contract`, and API behavior in controllers/services.
- WebApp generated Kiota models are treated as generated output; worker-only secret-bearing protocol shapes
  should avoid exposing raw secrets through public browser DTOs.
- Authorization-sensitive server operations use explicit API checks and persisted state rather than
  process-local assumptions.
- Logs and audit/error messages must be sanitized before persistence and projection.

### Integration Points
- Worker-facing HTTP endpoints should live in API controller/service code near lifecycle worker concerns,
  then call lifecycle/job services instead of giving workers direct `ApiDbContext` access.
- Lifecycle job claiming must integrate with the existing local worker path and persisted job state.
- Cloud scope checks need the authenticated worker's `LifecycleWorker` row, its `CloudGameServerSession`,
  and assigned `GameServer` relationship.
- Focused tests should cover worker authentication, claim scoping, report transitions, local compatibility,
  and redaction.

</code_context>

<specifics>
## Specific Ideas

- Use worker id plus worker credential headers for claim/report endpoints.
- Add claim response snapshots that are intentionally bounded and immutable for a single job attempt.
- Keep routine heartbeats cheap and stateful, while treating failures, retries, completion, and security
  failures as audit-worthy.
- Add a cloud worker agent foundation only as far as needed to authenticate, persist credentials locally,
  claim jobs, and report outcomes; Arma 3 file/process execution belongs to later v1.3 phases.

</specifics>

<deferred>
## Deferred Ideas

- Actual Arma 3 SteamCMD/DepotDownloader install/update/configuration execution is Phase 14.
- Native Linux Arma process lifecycle and recovery is Phase 15.
- Runtime admin/server-owner visibility, cleanup UI, retry gating, and end-to-end verification are Phase 16.
- Provider reconciliation, running cost dashboards, spending policies, multi-session workers, Docker/Compose,
  and headless clients remain outside v1.3 unless planned later.

</deferred>

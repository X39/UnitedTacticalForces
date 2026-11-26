# Phase 13: Cloud Worker Dispatch And Agent Foundation - Research

## Existing Surface

- Phase 12 already extended `LifecycleWorker` for cloud registration, including `Kind`, `CloudSessionFk`,
  `GameServerFk`, billing snapshot fields, provider placement hints, capability metadata, and hash-only
  `CredentialHash`.
- `LifecycleWorkersController` already exposes anonymous worker registration and heartbeat endpoints under
  `lifecycle-workers/cloud/{workerId}`. It reads bootstrap/worker credentials from `Authorization: Bearer ...`
  and returns raw worker credential material only in the `X-UTF-Worker-Credential` response header.
- `LifecycleJobQueueService` owns durable job claiming, success, failure/retry, and stale lease recovery. Today
  `ClaimNextAsync` only checks worker enablement, so local and cloud worker scopes are not separated yet.
- `LifecycleJob` has no direct cloud/local discriminator. The safest Phase 13 scope boundary is to derive cloud
  eligibility from an undeleted `CloudGameServerSession` for the job's `GameServerFk`, and from the authenticated
  cloud worker's own `CloudSessionFk`/`GameServerFk`.
- `GameServerLifecycleJobService` enqueues jobs with stable billing snapshots. Claiming and reports should reuse
  that persisted lifecycle job truth instead of writing worker-owned state outside the queue service.

## Implementation Guidance

- Add a small `sources/CloudWorker` console project for the agent foundation. It should parse a bootstrap file,
  register or resume from a local credential store, heartbeat, and claim once. Later phases can add Arma 3
  install/config/process execution behind this shell.
- Keep worker protocol DTOs in API response/payload classes and do not place secret fields in browser-facing DTOs.
  Claim responses can include lifecycle job data, game server identity, cloud endpoint/placement, active non-sensitive
  configuration entries, and timing/attempt metadata.
- Add a worker protocol service to centralize credential authentication, scoped claiming, report transitions, log
  persistence, and redaction. Thin controller endpoints should call that service.
- Reuse `LifecycleFailureReasonSanitizer` for all worker-supplied message, log, progress, and failure text before
  persisting it into logs, jobs, or audits.
- Preserve local compatibility by keeping `ClaimNextAsync(workerId, leaseDuration, ...)` as the local worker API and
  ensuring local workers still claim non-cloud jobs.

## Risks

- If local workers can still claim jobs for cloud-session game servers, cloud jobs may execute on the API host.
- If cloud workers can claim by game server id without checking their registered session, one cloud VM could operate
  another session's jobs.
- If the new worker project references the API project, it may accidentally couple worker code to server internals or
  expose secret-bearing DTOs. Prefer local request/response records and shared `Contract` enums only.
- Command-line and log redaction must be defensive because Phases 14 and 15 will add external tool output.

## Verification

- Add service tests for cloud-scoped claiming and local compatibility.
- Add controller tests for bearer credential requirements and safe response bodies.
- Add worker client/unit tests for bootstrap parsing, local credential persistence permissions intent, registration,
  resume, and claim request construction.
- Run focused API and CloudWorker tests plus a solution build.

## RESEARCH COMPLETE

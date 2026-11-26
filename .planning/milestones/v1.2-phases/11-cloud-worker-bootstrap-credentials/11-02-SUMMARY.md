---
phase: 11
plan: 11-02
completed: 2026-06-16
requirements-completed:
  - HETZ-06
  - WORK-05
  - WORK-06
  - WORK-07
---

# 11-02 Summary: Scoped Bootstrap Credential Issuance

## Completed

- Added `ICloudWorkerBootstrapService`, `CloudWorkerBootstrapService`, and `CloudWorkerBootstrapBundle` for server-side bootstrap issuance.
- Generated cloud worker ids as `cloud-{sessionId}-{32 lowercase hex chars}` and high-entropy base64url bootstrap values.
- Stored only SHA-256 hashes in `CloudWorkerBootstrapCredential`, with short-lived expiry configured by `CloudWorkers:BootstrapCredentialLifetimeMinutes` and a 15-minute default.
- Revoked prior unconsumed bootstrap rows for the same cloud session before issuing a new active bootstrap.
- Added one-time consume validation for worker id, raw credential, expiry, revocation, and previous consumption.
- Rendered a cloud-init document containing only API base URL, worker id, raw bootstrap credential, session id, game server id, provider identifier, and expiry.
- Added focused tests for issuance, superseding, consumption, invalid credential paths, exact user-data JSON keys, API URL fallback behavior, and configured secret exclusion.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter CloudWorkerBootstrap --no-restore --verbosity minimal`

## Notes

- Bootstrap audit records include worker id and cloud session id but not raw bootstrap material.
- Missing `General:ApiBaseUrl` falls back to an empty value only when the host environment is `Test`; non-test environments throw without echoing secret vocabulary.
- The targeted test command still reports the existing OpenTelemetry advisory and pre-existing nullable/XML documentation warnings.

## Self-check

Passed.

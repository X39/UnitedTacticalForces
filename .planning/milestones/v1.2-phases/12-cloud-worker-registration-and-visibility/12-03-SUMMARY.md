---
phase: 12
plan: 12-03
completed: 2026-06-16
requirements-completed:
  - VIS-07
  - VIS-08
  - VIS-09
  - RELY-08
  - RELY-09
---

# 12-03 Summary: Cloud Worker And Session Visibility

## Completed

- Added `CloudSessionStatusDto` as a safe combined projection for cloud session, resources, bootstrap credentials, worker heartbeat, and retryable action state.
- Exposed admin session status through `GET cloud-inventory/session-statuses`.
- Exposed resource-scoped server-owner status through `GET game-servers/{gameServerId}/cloud-status`.
- Added status flags for provisioned, cleanup-needed, orphan-candidate, bootstrap pending/expired/consumed, worker registration, worker status, and retryable actions.
- Wired the admin cloud operations page with a sessions tab, worker/bootstrap/cleanup badges, and state-gated provision/teardown buttons.
- Added a compact cloud status section to the server detail view through `IGameServerRepository`.
- Kept raw worker credentials, credential hashes, provider tokens, and secret-bearing values out of DTOs, UI models, and tests.
- Extended API visibility tests for combined status projection, controller endpoints, redaction, restart-persistent worker/session state, and authorization policy boundaries.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "CloudWorker|LifecycleWorker|CloudInventory" --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

## Commits

- `7aea9a6 feat(12-03): expose cloud session worker visibility`

## Notes

- The WebApp still uses the temporary raw `HttpClient` repository path for cloud admin/status calls until the deferred Kiota regeneration work is picked up.
- Browser/UAT with authenticated Aspire data remains deferred because representative server-admin data and full local provider setup are outside this run.
- The verification commands still report the existing OpenTelemetry advisory and pre-existing ConfigParser/FluentMarkdown warnings.

## Deviations

- Endpoint authorization is covered by API/controller tests and reflection checks; browser authorization UAT remains deferred with the existing milestone-level UAT item.

## Self-check

Passed.

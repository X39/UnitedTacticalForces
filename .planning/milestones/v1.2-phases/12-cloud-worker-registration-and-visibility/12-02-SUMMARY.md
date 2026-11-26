---
phase: 12
plan: 12-02
completed: 2026-06-16
requirements-completed:
  - WORK-08
  - WORK-09
---

# 12-02 Summary: Cloud Worker Registration And Heartbeat

## Completed

- Added `ICloudWorkerRegistrationService` and `CloudWorkerRegistrationService` for consuming one-time bootstrap credentials.
- Generated a high-entropy worker credential during registration and stored only its SHA-256 hash on `LifecycleWorker`.
- Linked registered cloud workers to cloud session, game server, billing account, billing assignment, provider, location, and network-zone metadata.
- Persisted reported worker capabilities including supported controller identifiers, operating system, architecture, one-session shape, and capability timestamps.
- Added anonymous worker-facing `POST lifecycle-workers/cloud/{workerId}/register` and `POST lifecycle-workers/cloud/{workerId}/heartbeat` endpoints.
- Returned the one raw worker credential only through the registration response header `X-UTF-Worker-Credential`.
- Kept request/response DTOs and audit records free of raw bootstrap credentials, raw worker credentials, credential hashes, provider tokens, and secret-bearing field names.
- Added registration, heartbeat, rejection, DTO redaction, controller, endpoint-attribute, and restart-persistence tests.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "CloudWorkerRegistration|CloudWorkerBootstrap|LifecycleWorker" --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

## Commits

- `35146a1 feat(12-02): add cloud worker registration heartbeat`

## Notes

- `WORK-08` and `WORK-09` are complete for the backend worker registration and heartbeat surface.
- `VIS-07`, `VIS-08`, `VIS-09`, `RELY-08`, and final `RELY-09` closure remain in `12-03`.
- The verification commands still report the existing OpenTelemetry advisory and pre-existing nullable/XML documentation warnings.

## Deviations

- The controller/API tests live together in `CloudWorkerRegistrationServiceTests.cs` instead of a separate `CloudWorkerRegistrationApiTests.cs` file because the endpoint coverage is small and shares setup.

## Self-check

Passed.

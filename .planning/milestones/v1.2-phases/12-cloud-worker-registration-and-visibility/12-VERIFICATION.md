---
phase: 12
status: passed
verified: 2026-06-16
requirements:
  - WORK-08
  - WORK-09
  - WORK-10
  - RELY-08
  - VIS-07
  - VIS-08
  - VIS-09
  - RELY-09
---

# Phase 12 Verification: Cloud Worker Registration And Visibility

## Result

Passed.

## Requirement Coverage

- `WORK-08`: `CloudWorkerRegistrationService.RegisterAsync` consumes one-time bootstrap credentials, generates and hashes a worker credential, links the worker to cloud session, game server, billing account, assignment, provider, and writes sanitized registration audit records.
- `WORK-09`: `HeartbeatAsync` authenticates cloud workers by hashed worker credential and updates heartbeat, display name, supported controller identifiers, region, OS, architecture, one-session shape, and capability report timestamp.
- `WORK-10`: `LifecycleWorker` persists `Kind = Local` or `Kind = Cloud`; local heartbeat continues to work without cloud metadata while cloud worker ID collisions are rejected.
- `RELY-08`: Cloud sessions, bootstrap consumption, worker identity, credential hashes, heartbeat metadata, capability metadata, and cleanup-needed state persist in EF storage and survive new `DbContext` instances.
- `VIS-07`: `CloudInventoryController.GetSessionStatusesAsync` exposes administrator-safe status for provisioning, bootstrap, worker, cleanup, orphan-candidate, and retryable actions.
- `VIS-08`: `GameServersController.GetCloudSessionStatusesAsync` exposes resource-scoped server-owner status without provider tokens, worker credentials, credential hashes, authorization headers, or bearer material.
- `VIS-09`: `CloudInventoryService.CreateSessionStatusDto` computes `CanProvision`, `CanTeardown`, `CanRetryCleanup`, and retryable action labels from persisted state.
- `RELY-09`: Targeted tests cover provider boundaries, dry-run validation before provisioning, ownership preservation on partial failure, secret redaction, worker registration/heartbeat, visibility projections, and cleanup transitions.

## Evidence

- Plans and summaries: `12-01`, `12-02`, and `12-03` completed local/cloud worker metadata, registration/heartbeat endpoints, and cloud session visibility.
- Source: `sources/Api/Services/CloudWorkerRegistration/CloudWorkerRegistrationService.cs` handles bootstrap consumption, credential hashing, cloud metadata persistence, heartbeat authentication, and capability updates.
- Source: `sources/Api/Controllers/LifecycleWorkersController.cs` exposes anonymous bearer-based cloud worker registration and heartbeat inside the otherwise authorized controller and returns the raw worker credential only in the `X-UTF-Worker-Credential` response header.
- Source: `sources/Api/Services/CloudInventory/CloudInventoryService.cs` combines sessions, resources, bootstrap credentials, workers, cleanup state, endpoint data, and retry gating into safe DTOs.
- Source: `sources/Api/Controllers/CloudInventoryController.cs` and `sources/Api/Controllers/GameServersController.cs` expose admin and resource-scoped status endpoints.
- Tests: `CloudWorkerRegistrationServiceTests` cover register, heartbeat rejection, restart with new context, controller response-body/header safety, and endpoint `AllowAnonymous` attributes.
- Tests: `LifecycleWorkerRegistryServiceTests` cover local worker compatibility, cloud ID collision rejection, DTO redaction, and cloud metadata persistence across a new context.
- Tests: `CloudInventoryVisibilityTests` cover admin and server-scoped status, retryable actions, safe serialization, missing server `NotFound`, and sanitized teardown result mapping.

## Automated Checks

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "CloudWorkerRegistration|CloudWorkerBootstrap|LifecycleWorker" --no-restore --verbosity minimal` - passed, 16 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "CloudWorker|LifecycleWorker|CloudInventory" --no-restore --verbosity minimal` - passed, 30 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --verbosity minimal` - passed, 78 tests.
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` - succeeded with known warnings.

The first parallel attempt hit Windows file locks in build outputs. After `dotnet build-server shutdown`, the same checks were rerun serially and passed.

## Residual Risk

- WebApp cloud admin and server cloud-status calls still use temporary raw `HttpClient` repository paths until OpenAPI/Kiota regeneration is available.
- Authenticated Aspire/API browser UAT with representative cloud-session and server-owner data remains deferred.

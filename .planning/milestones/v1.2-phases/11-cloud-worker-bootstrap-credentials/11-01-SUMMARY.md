---
phase: 11
plan: 11-01
completed: 2026-06-16
requirements-completed:
  - HETZ-06
  - WORK-05
  - WORK-07
---

# 11-01 Summary: Durable Bootstrap Credential Storage

## Completed

- Added `CloudWorkerBootstrapCredential` as durable bootstrap identity storage tied to a cloud session, game server, billing account, billing assignment, and creator.
- Persisted only `CredentialHash` for credential material and added indexes for worker id, cloud session, and expiry lookup.
- Registered the entity in `ApiDbContext`, configured restrictive relationships, and generated the EF migration and model snapshot.
- Added `CloudWorkerBootstrapCredentialIssued` as the hosting audit event kind later issuance code will record.
- Added a redacted `CloudWorkerBootstrapCredentialDto` plus transform mapping that omits the credential hash.
- Added focused SQLite tests for persisted hash-only storage, restrictive cloud session delete behavior, DTO property safety, and serialized DTO redaction.

## Verification

- `dotnet ef migrations add AddCloudWorkerBootstrapCredentials --project sources\Api\Api.csproj --startup-project sources\Api\Api.csproj --output-dir Data\Migrations`
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter CloudWorkerBootstrap --no-restore --verbosity minimal`

## Notes

- This plan creates the durable identity and redaction foundation; issuance, one-time consumption, and VM user-data rendering remain in 11-02.
- The targeted build/test commands still report the existing OpenTelemetry advisory and pre-existing nullable/XML documentation warnings.

## Self-check

Passed.

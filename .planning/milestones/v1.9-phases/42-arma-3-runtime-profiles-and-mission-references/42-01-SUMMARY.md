---
phase: 42-arma-3-runtime-profiles-and-mission-references
plan: 01
status: complete
completed_at: "2026-06-21T22:56:00Z"
commit: pending
---

# 42-01 Summary

## Completed

- Added versioned `Arma3MissionReference` rows with stable mission ids, current/enabled flags, supersession links, terrain/world/mission file references, optional Workshop item id, actor attribution, and sanitized-audit-ready metadata.
- Added versioned `Arma3RuntimeProfile` rows with stable runtime profile ids, mod-pack revision and mission reference version FKs, configuration preset name, directory/file assumptions, port policy, and explicit runtime options.
- Added `Arma3RuntimeProfileConfigurationEntry` for non-sensitive allowlisted configuration preset entries owned by a runtime profile version.
- Added runtime profile and mission snapshot references to `CloudDryRunPlan` and `CloudGameServerSession`.
- Added EF model indexes and restrict-delete relationships for mission/profile versioning, references, and snapshots.
- Added hosting audit event kinds for mission reference and runtime profile create/update/disable/selection/snapshot events.
- Generated EF migration `20260621225535_AddArma3RuntimeProfiles`.

## Verification

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity normal`
- `dotnet ef migrations add AddArma3RuntimeProfiles --project sources/Api/Api.csproj --startup-project sources/Api/Api.csproj`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal`

## Notes

- The build passed with the repository's existing XML documentation, nullable, and xUnit analyzer warnings.
- No live provider, Steam, Arma, browser, teardown, or cleanup UAT was run.

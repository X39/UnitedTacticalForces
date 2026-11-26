---
phase: 43-template-aware-planning-and-provisioning-gates
plan: 02
status: complete
completed_at: "2026-06-21T23:55:00Z"
commit: pending
---

# 43-02 Summary

## Completed

- Added first-class dry-run review states for unavailable, mismatched, and policy-blocked cloud server templates and runtime profiles.
- Added review snapshot checks for selected template/profile consistency, current enabled rows, compatible Arma 3/Hetzner scope, and canonical snapshot equality.
- Added provisioning preflight validation for plan/session template, runtime profile, and mission reference snapshots before Hetzner token resolution.
- Blocked stale, disabled, incomplete, mismatched, or mutated template/profile snapshots before any provider mutation path.
- Updated the Blazor cloud workflow review state model and UI labels/classes for the new blocker states.
- Added focused review tests for valid template/profile review, template policy violation, disabled template, disabled runtime profile, and runtime profile mismatch.
- Added provisioning tests proving disabled template/profile and runtime-profile snapshot mismatch fail before token resolution or provider calls.

## Verification

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~Arma3CloudPlanReview" --no-build --verbosity normal`
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudProvisioningServiceTests" --no-build --verbosity normal`
- `git diff --check`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal /p:UseSharedCompilation=false -m:1`

## Notes

- Full solution build passes with the repository's existing generated WebApp Kiota nullable warnings.
- Parallel `dotnet test` initially hit MSBuild output file locks; sequential reruns passed.

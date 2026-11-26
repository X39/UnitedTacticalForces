---
phase: 56-generic-runtime-template-and-plan-snapshots
plan: 03
subsystem: api-worker
tags: [cloud-worker, runtime-recipe, generic-hosting, redaction, stale-plan]
requires:
  - phase: 56-01
    provides: Generic selector/catalog facade and controller-scoped game selection.
  - phase: 56-02
    provides: Generic dry-run/review contracts plus provider plan stale and runtime review states.
provides:
  - Generic cloud worker runtime recipe envelope.
  - Allowlisted Arma 3 recipe body for existing worker execution.
  - Generic runtime recipe persistence for dry-run plan/session snapshots.
  - Legacy Arma snapshot compatibility readers for provisioning and review.
  - Worker redaction, dispatch, stale-plan, runtime-review, and boundary tests.
affects: [phase-56, cloud-worker, cloud-planning, cloud-provisioning, runtime-review]
tech-stack:
  added: []
  patterns:
    - Generic runtime recipe envelope with exactly one game-specific allowlisted body.
    - API-prepared executable recipes; workers do not infer selector, policy, stale-plan, cost, or provider state.
    - Legacy snapshot reader for persisted Arma 3 dry-run/session data during migration.
key-files:
  created:
    - sources/Api/CloudRuntimeRecipeSnapshots.cs
    - sources/Api/DTO/CloudWorkerRuntimeRecipeDto.cs
  deleted:
    - sources/Api/DTO/CloudWorkerRuntimeProfileSnapshotDto.cs
  modified:
    - sources/Api/CloudSelectionContext.cs
    - sources/Api/DTO/CloudWorkerJobClaimDto.cs
    - sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs
    - sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs
    - sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs
    - sources/Api/Services/CloudWorkerDispatch/CloudWorkerDispatchService.cs
    - sources/CloudWorker/CloudWorkerApiClient.cs
    - sources/CloudWorker/Arma3LifecycleJobExecutor.cs
    - sources/CloudWorker/Arma3LaunchBuilder.cs
    - tests/Api.Tests/CloudWorkerDispatch/CloudWorkerDispatchServiceTests.cs
    - tests/Api.Tests/CloudWorker/CloudWorkerApiClientTests.cs
    - tests/Api.Tests/CloudWorker/CloudWorkerRuntimeTests.cs
    - tests/Api.Tests/CloudWorker/Arma3LifecycleJobExecutorTests.cs
    - tests/Api.Tests/Arma3RuntimeProfiles/Arma3RuntimeProfileRedactionTests.cs
    - tests/Api.Tests/WebApp/GenericGameHostingContractBoundaryTests.cs
key-decisions:
  - "Worker claim payloads now use RuntimeRecipe instead of a top-level Arma runtime profile snapshot."
  - "Arma 3 remains supported through an allowlisted Arma3 recipe body, while the generic envelope carries selected game type, controller identifier, hosting mode, runtime/mission identifiers, and input fingerprint."
  - "Dry-run plans persist the generic recipe JSON in the existing runtime profile snapshot column for migration compatibility; readers accept both the new generic recipe and legacy Arma snapshot JSON."
  - "Unsupported or mismatched worker runtime recipes fail before any worker command execution."
requirements-completed: [GEN-03, GEN-04]
decisions-covered: [D-08, D-09, D-10, D-12, D-16, D-18, D-19, D-20, D-21, D-22, D-23]
completed: 2026-07-04
---

# Phase 56-03 Summary

Wave 3 completes the worker-facing part of GEN-03 and GEN-04. The API now prepares a generic recipe for worker claims, and the CloudWorker client/executor consumes that generic envelope with an allowlisted Arma 3 body.

## Runtime Recipe Contract

- Added `CloudWorkerRuntimeRecipeDto` as the API worker claim envelope.
- Replaced `CloudWorkerRuntimeProfileSnapshotDto` with a generic recipe that includes selected game type, controller identifier, recipe kind/version, hosting mode, runtime/profile/mission identifiers, safe display names, and `InputFingerprint`.
- Added the Arma 3-specific recipe body under `Arma3`, keeping mod-pack, mission, config, port, launch, and runtime values available without exposing provider tokens, Steam credentials, worker credentials, cookies, bearer tokens, or raw secret fields.
- Updated the CloudWorker protocol model from `RuntimeProfile` to `RuntimeRecipe`.
- Updated `Arma3LifecycleJobExecutor` to reject unsupported, mismatched, or non-Arma runtime recipes before executing install/start/stop commands.
- Updated `Arma3LaunchBuilder` to read recipe-provided FPS, mission preload, and fallback game port values.

## Snapshot Compatibility

- Added `CloudRuntimeRecipeSnapshots` to create and read the generic recipe envelope.
- Dry-run planning now persists a generic recipe JSON snapshot while retaining the existing storage column for compatibility.
- Review, selection, provisioning, and dispatch readers accept both the generic recipe JSON and legacy Arma runtime/mission snapshot JSON.
- This preserves cost, owner, selected game, hosting mode, template/runtime choices, debug details, and audit context while moving the boundary to generic game-scoped data.

## Stale And Review Safety

- GEN-03 is covered by worker claims scoped through `SelectedGameType`, `ControllerIdentifier`, and an explicit recipe kind/version.
- GEN-04 is covered by preserving Arma 3 runtime execution through the generic recipe body.
- D-08 through D-10 are covered by the worker recipe envelope and executor validation.
- D-18 through D-23 remain covered by the Wave 2 provider plan stale and runtime review required states, plus the Wave 3 compatibility path that keeps runtime review data available to provisioning and dispatch.
- Provider plan stale and runtime review required behavior is tested through the planning/provisioning/policy boundary buckets; worker dispatch cannot silently infer changed runtime content outside the API-prepared recipe.

## Verification

- `dotnet build X39.UnitedTacticalForces.slnx` - passed, 0 warnings, 0 errors.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName~CloudWorkerDispatch|FullyQualifiedName~CloudWorkerApiClient|FullyQualifiedName~CloudWorkerRuntime|FullyQualifiedName~Arma3LifecycleJobExecutor|FullyQualifiedName~Arma3ConfigurationRenderer"` - passed, 28 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudProvisioning|FullyQualifiedName~CloudPolicies|FullyQualifiedName~CloudReconciliation|FullyQualifiedName~BillingOwnershipAndAudit"` - passed, 209 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests|FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~Arma3RuntimeProfileRedactionTests"` - passed, 63 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName!~WebApp"` - passed, 663 tests.
- `rg -n "CloudWorkerRuntimeProfileSnapshotDto|CloudWorkerRuntimeProfileSnapshotBody|CloudWorkerMissionReferenceSnapshotBody|CloudWorkerRuntimeProfileConfigurationEntryBody" sources\Api sources\CloudWorker` - no matches.
- `rg -n "Deserialize<Arma3RuntimeProfileSnapshotDto>|Deserialize<Arma3MissionReferenceSnapshotDto>|Arma3RuntimeProfileSnapshotCreated|Arma3RuntimeProfileSelected" sources\Api sources\CloudWorker` - no matches.
- `rg -n "RuntimeRecipe|Arma3RuntimeRecipe|CloudWorkerRuntimeRecipe|CloudWorkerArma3RuntimeRecipe" sources\Api sources\CloudWorker tests\Api.Tests\CloudWorker tests\Api.Tests\CloudWorkerDispatch tests\Api.Tests\WebApp\GenericGameHostingContractBoundaryTests.cs tests\Api.Tests\Arma3RuntimeProfiles\Arma3RuntimeProfileRedactionTests.cs` - found the expected generic recipe implementation and tests.
- `git diff --check` - passed; only Git line-ending normalization warnings were printed.

## Known Validation Limits

- `dotnet test tests/Api.Tests/Api.Tests.csproj --no-build` was run and failed only in two pre-existing WebApp boundary tests unrelated to Phase 56: missing `sources/WebApp/ApiModelCompatibilityPartials.cs`, and missing `VisibleAssignedClaims` in the role detail route.
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"` was attempted and timed out after 184 seconds. The leftover Blazor test server and test host processes from that timeout were cleaned up.
- Live Hetzner, Steam, and Arma 3 worker UAT was not run in this wave.

## Completion Notes

Phase 56 now has generic selector, dry-run/review, stale-state, and worker recipe boundaries while preserving Arma 3 as the first concrete game implementation. Remaining Arma names are implementation internals, compatibility DTOs, tests, and the Arma-specific recipe body.

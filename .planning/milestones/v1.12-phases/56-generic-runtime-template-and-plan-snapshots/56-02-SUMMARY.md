---
phase: 56-generic-runtime-template-and-plan-snapshots
plan: 02
subsystem: api
tags: [cloud-planning, dry-run, review, generic-hosting, webapp]
requires:
  - phase: 56-01
    provides: Generic selector/catalog facade and generated client route support.
provides:
  - Generic dry-run and review DTO contracts.
  - Generic cloud dry-run planner and review service interfaces.
  - Server-controller-based Arma 3 planner/review adapter dispatch.
  - Distinct provider plan stale and runtime review required states.
affects: [phase-56, cloud-planning, cloud-provisioning, webapp-server-workspace]
tech-stack:
  added: []
  patterns:
    - Server-selected cloud planning adapters keyed by controller identifier.
    - Public cloud planning contracts use generic names while concrete game behavior stays behind adapters.
key-files:
  created:
    - sources/Api/Services/CloudPlanning/CloudDryRunPlanReviewService.cs
    - sources/Api/Services/CloudPlanning/CloudDryRunPlannerService.cs
    - sources/Api/Services/CloudPlanning/ICloudDryRunPlanReviewAdapter.cs
    - sources/Api/Services/CloudPlanning/ICloudDryRunPlannerAdapter.cs
  modified:
    - sources/Api/Controllers/CloudPlanningController.cs
    - sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs
    - sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs
    - sources/Api/DTO/CloudDryRunPlanResultDto.cs
    - sources/Api/DTO/CloudDryRunPlanReviewDto.cs
    - sources/Api/DTO/CloudDryRunPlanReviewStateDto.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudDryRunPlanReviewModels.cs
    - sources/WebApp/Pages/Servers/Advanced.razor
key-decisions:
  - "Generic dry-run/review services dispatch from the persisted server controller identifier rather than a client-selected Arma contract."
  - "Provider stale and runtime review required states are explicit review states, with existing input fingerprints and persisted snapshots still enforcing provisioning safety before token resolution."
  - "Wave 2 used mechanical generated-client model/request-builder updates instead of a live Kiota regeneration because route shapes were unchanged and API OpenAPI startup requires database bootstrap."
patterns-established:
  - "Generic service interface plus game-specific adapter for dry-run planning and review."
  - "Runtime-recipe changes surface as review/acknowledgement blockers without forcing provider plan regeneration unless provider assumptions changed."
requirements-completed: [GEN-03, GEN-04]
duration: resumed
completed: 2026-07-04
---

# Phase 56-02 Summary

Generic cloud dry-run and review contracts now sit in front of the Arma 3 implementation, with distinct provider stale and runtime review states.

## Scope

Implemented the GEN-03/GEN-04 dry-run and review contract cleanup from Plan 02. Public API DTOs, generated-client model names, repository mapper methods, and Razor-facing review state labels no longer use Arma-named dry-run/review contracts. Arma 3 remains the first concrete implementation behind the generic facade.

## Contract Changes

- Renamed public API dry-run and review DTOs from `Arma3Cloud*` to generic `Cloud*` names:
  - `CloudDryRunPlanResultDto`
  - `CloudDryRunPlanReviewDto`
  - `CloudDryRunPlanReviewStateDto`
  - `CloudDryRunPlanReviewResourceDto`
  - `CloudDryRunPlanCostBreakdownDto`
  - `CloudFirewallRuleDto`
  - `CloudPlanAssumptionDto`
  - `CloudPlanValidationFailureDto`
- Renamed generic service contracts to `ICloudDryRunPlannerService` and `ICloudDryRunPlanReviewService`.
- Added adapter contracts so Arma 3 planning/review behavior is selected from the persisted server controller identifier, not from a public Arma response type.
- Updated WebApp generated-style models, request builders, repository mappers, and review models to consume generic dry-run/review names.

## Freshness Semantics

- Added explicit review states for provider plan stale and runtime review required:
  - `ProviderPlanStale`
  - `RuntimeReviewRequired`
- Provider-impact stale cases, including template/provider-input fingerprint mismatch, still block provisioning before provider token resolution or mutation.
- Runtime recipe/content drift now returns `RuntimeReviewRequired` so mission/mod/profile/launch recipe changes are decision-visible instead of silently auto-applied.
- Existing persisted `InputFingerprint` and snapshot checks remain in place. This wave did not add EF columns or a migration; it makes D-11 through D-23 semantics explicit through review state classification and tests.
- Superseded provider plans remain separate from runtime re-review needs: a replacement provider dry-run still supersedes prior provider plans, while runtime drift asks for review/acknowledgement.

## WebApp And Generated Client

- Updated generated-client model/request-builder files mechanically for the renamed DTOs and enum values.
- Did not run a live Kiota regeneration in this wave. The route shapes were unchanged from Wave 1, and API OpenAPI startup requires database bootstrap; the changed generated files are limited to model names, discriminator names, return types, and enum values.
- Verified the generated-style edits with solution build plus WebApp repository boundary tests.

## Allowed Arma Internals

Arma names remain implementation-internal where they describe the concrete first game adapter, runtime/profile persistence, mission snapshot details, tests, and worker execution paths. User-facing dry-run/review contracts and repository model names are generic.

## Verification

- `dotnet build X39.UnitedTacticalForces.slnx` - passed, 311 warnings, 0 errors. The warnings are existing documentation/nullable/unused/import warnings outside the Wave 2 contract change.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3CloudPlannerTests|FullyQualifiedName~Arma3CloudPlanReviewApiTests|FullyQualifiedName~Arma3CloudPlanReviewStateTests"` - passed, 20 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudProvisioning"` - passed, 67 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudDryRunPlanReviewBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests"` - passed, 54 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests"` - passed, 5 tests.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyGate"` - passed, 30 tests.
- `rg -n "Arma3CloudDryRunPlanDto|Arma3CloudDryRunPlanReviewDto|Arma3CloudDryRunPlanReviewStateDto|IArma3CloudDryRunPlannerService|IArma3CloudDryRunPlanReviewService" sources\Api tests\Api.Tests` - no matches.
- `rg -n "FromArma3DryRunPlan|Arma3CloudDryRunPlanDto|Arma3CloudDryRunPlanReviewDto|Arma3CloudDryRunPlanReviewStateDto" sources\WebApp tests\Api.Tests` - no matches.
- `rg -n "ProviderPlanStale|RuntimeReviewRequired|ValidProviderPlan|CurrentRecipe|ProviderFingerprint|RuntimeRecipeFingerprint|InputFingerprint" sources\Api tests\Api.Tests` - found the new explicit states plus existing `InputFingerprint` provisioning/review gates.

## Deferred

Wave 3 owns worker runtime recipe envelope changes, downstream worker snapshots, and final full-suite validation for the phase.

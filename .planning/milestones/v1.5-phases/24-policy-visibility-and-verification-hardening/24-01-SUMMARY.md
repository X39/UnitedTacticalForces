---
phase: 24-policy-visibility-and-verification-hardening
plan: 01
subsystem: api/webapp-client/cloud-policy
tags:
  - policy-visibility
  - generated-client
  - authorization
  - verification
dependencies:
  requires:
    - Phase 21 durable spending policy decisions
    - Phase 22 approval request workflow
    - Phase 23 automatic action attempts
    - Phase 17-20 cost and reconciliation visibility
  provides:
    - Admin policy visibility endpoint
    - Server-scoped policy visibility endpoint
    - WebApp repository accessors for policy visibility
  affects:
    - phase-24
    - cloud-policy
    - cloud-operations-ui
tech-stack:
  added: []
  patterns:
    - Scoped API projection service
    - Controller authorization attributes
    - Kiota-style request builders and repository mapping facade
key-files:
  created:
    - sources/Api/DTO/CloudSpendingPolicyVisibilityDto.cs
    - sources/Api/DTO/CloudSpendingPolicyVisibilityRowDto.cs
    - sources/Api/Services/CloudPolicies/ICloudSpendingPolicyVisibilityService.cs
    - sources/Api/Services/CloudPolicies/CloudSpendingPolicyVisibilityService.cs
    - tests/Api.Tests/CloudPolicies/CloudSpendingPolicyVisibilityTests.cs
    - sources/WebApp/UtfApi/CloudSpendingPolicies/CloudSpendingPoliciesRequestBuilder.cs
    - sources/WebApp/UtfApi/CloudSpendingPolicies/Visibility/VisibilityRequestBuilder.cs
    - sources/WebApp/UtfApi/GameServers/Item/CloudPolicyVisibility/CloudPolicyVisibilityRequestBuilder.cs
    - sources/WebApp/UtfApi/Models/CloudSpendingPolicyVisibilityDto.cs
    - sources/WebApp/UtfApi/Models/CloudSpendingPolicyVisibilityRowDto.cs
  modified:
    - sources/Api/Controllers/CloudSpendingPoliciesController.cs
    - sources/Api/Controllers/GameServersController.cs
    - sources/WebApp/UtfApi/ApiClient.cs
    - sources/WebApp/UtfApi/GameServers/Item/WithGameServerItemRequestBuilder.cs
    - sources/WebApp/Services/CloudAdminRepository/CloudAdminModels.cs
    - sources/WebApp/Services/CloudAdminRepository/CloudAdminRepositoryImpl.cs
    - sources/WebApp/Services/CloudAdminRepository/ICloudAdminRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
decisions:
  - Admin visibility stays server-admin only and composes policy, cost, reconciliation, approval, decision, and automatic action state server-side.
  - Server visibility is scoped by game server and related sessions, returns 404 for missing servers, and uses resource-based server authorization.
  - The local OpenAPI endpoint was unavailable during Kiota update, so request builders and models were added in generated style and verified by WebApp build.
metrics:
  completed: 2026-06-18T13:21:08Z
requirements-completed:
  - VIS-16
  - VIS-17
  - VIS-18
  - RELY-20
---

# Phase 24 Plan 01: Policy API And Client Summary

Phase 24 now exposes policy visibility through durable server-side projections and WebApp repository facades.

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add visibility DTOs and projection service | 434b577 | API DTOs, visibility service, tests |
| 2 | Expose authorized admin and server endpoints | 434b577 | `CloudSpendingPoliciesController`, `GameServersController` |
| 3 | Wire WebApp generated-pattern client and repositories | 4d14c32 | `UtfApi`, cloud admin and game server repositories |

## What Changed

- Added aggregate and row DTOs for effective policy, threshold ratios, pending approvals, denied decisions, automatic action attempts, cleanup risk, and required next steps.
- Added `CloudSpendingPolicyVisibilityService` for admin and server-scoped projections.
- Added `GET cloud-spending-policies/visibility` and `GET game-servers/{gameServerId}/cloud-policy-visibility`.
- Added generated-style request builders and WebApp repository methods without introducing a new browser raw HTTP bridge.
- Added focused API tests for projection composition, server scoping, missing server behavior, authorization attributes, and secret-bearing DTO field names.

## Verification

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyVisibilityTests" --verbosity quiet /clp:ErrorsOnly` | Passed | 5 tests passed. |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | 0 errors; existing warning debt remains. |
| `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed | 0 errors; 39 existing warnings. |

## Deviations from Plan

`kiota update` could not connect to the local API OpenAPI endpoint because the API was not running. The client changes were applied manually in Kiota-generated style and verified by build.

## Self-Check: PASSED

- Policy visibility is composed server-side.
- Endpoint authorization remains admin-only for aggregate view and resource-scoped for server view.
- WebApp access is routed through repository facades.

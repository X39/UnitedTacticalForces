---
phase: 34-generated-client-and-repository-gate-foundation
plan: 03
subsystem: webapp-repository
tags: [blazor, webapp, repository, kiota, policy-gate, redaction, cost-03]
requires:
  - phase: 34-generated-client-and-repository-gate-foundation
    plan: 02
    provides: generated-client repository cleanup for cloud workflow reads and lifecycle results
provides:
  - Policy-aware generated Kiota request sender for cloud workflow action results
  - Kiota-parsable policy gate transport without AdditionalData exposure
  - Repository facade methods for dry-run planning, provisioning, teardown, cost refresh, status, and audit action results
  - Sanitized failure and redaction-blocked action result mapping
affects: [phase-34, phase-35, phase-36, phase-37, phase-38, cost-03]
tech-stack:
  added: []
  patterns:
    - Generated RequestInformation sends through protected IRequestAdapter
    - Repository-local policy gate error mapping for 409/4XX/5XX responses
    - Allowlisted CloudWorkflowActionResult projections before UI rendering
key-files:
  created:
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs
    - .planning/phases/34-generated-client-and-repository-gate-foundation/34-03-SUMMARY.md
  modified:
    - sources/WebApp/Services/RepositoryBase.cs
    - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs
    - sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
key-decisions:
  - "Policy gate conflicts are parsed through a repository-local Kiota IParsable transport that does not implement IAdditionalDataHolder."
  - "Untyped ApiException failures map only status code and action metadata into CloudWorkflowActionResult failures."
  - "Continuation query parameters are added only from typed method parameters named acknowledgedDecisionId and approvalRequestId."
requirements-completed: [COST-03]
duration: 75min
completed: 2026-06-19
---

# Phase 34 Plan 03: Policy-Gated Action Result Mapping Summary

**Policy-gated cloud workflow actions now return typed, redacted repository results with continuation identity preserved.**

## Performance

- **Duration:** 75 min
- **Completed:** 2026-06-19
- **Tasks:** 2
- **Files modified:** 7 implementation files plus this summary

## Accomplishments

- Exposed the generated Kiota `IRequestAdapter` from `RepositoryBase` while preserving the existing generated `Client` facade.
- Added `CloudWorkflowKiotaSender` for generated `RequestInformation` sends with policy gate error mapping and continuation query parameters.
- Converted the repository-local policy gate transport into Kiota `IParsable` types without `IAdditionalDataHolder`, so unknown/raw fields are ignored.
- Added sanitized failure and redaction-blocked result mapping without exception bodies, headers, raw payloads, or token-bearing values.
- Added repository facade methods for Arma 3 cloud dry-run planning, provisioning, teardown, cost snapshot refresh, workflow status, and audit context results.
- Preserved policy continuation fields: decision id, approval request id, action kind, scope key, game server id, session id, dry-run plan id, billing account id, billing assignment id, and expiration where present.

## Task Commits

1. **Task 1: Add policy-aware generated request sender** - `4c00eff` (feat)
2. **Task 2: Add planning, provisioning, teardown, and cost action result methods** - `eaee6e6` (feat)

## Files Created/Modified

- `sources/WebApp/Services/RepositoryBase.cs` - Added protected request adapter access for generated request sends.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs` - Added shared generated-request sender with policy gate and sanitized failure mapping.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs` - Added Kiota parsing contract for allowlisted policy gate fields without extension data.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - Added dry-run, runtime status, policy visibility, sanitized failure, and redaction-blocked result mappings.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - Added cloud workflow action result facade methods.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - Implemented generated-client-backed result methods for planning, provisioning, teardown, cost, status, and audit.

## Decisions Made

- Used Kiota `RequestInformation` from generated builders for billable actions, then appended only typed continuation query parameters.
- Kept policy gate parsing repository-local because the generated WebApp client still has no generated gate result model.
- Mapped runtime and policy visibility into a combined workflow status result so later UI can display current status and continuation identity without generated DTO binding.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added mapper failure helpers with the sender task**
- **Found during:** Task 1
- **Issue:** The new shared sender needed sanitized failure and redaction-blocked `CloudWorkflowActionResult` creation to avoid exposing raw `ApiException` details.
- **Fix:** Added `FromSanitizedFailure` and `FromRedactionFailure` in `CloudWorkflowResultMapper`.
- **Files modified:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`
- **Commit:** `4c00eff`

**2. [Rule 3 - Blocking] Corrected dry-run validation failure field mapping**
- **Found during:** Task 2 verification
- **Issue:** The mapper initially referenced a non-existent generated `Field` property on `Arma3CloudPlanValidationFailureDto`.
- **Fix:** Mapped validation rows using generated `Target`, then `Code`, then a stable `Validation` label.
- **Files modified:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`
- **Verification:** `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- **Commit:** `4c00eff`

## TDD Gate Compliance

The plan tasks were marked `tdd="true"`, but the user-provided write scope allowed only implementation files and this summary. No test files were created because that would have violated the scoped instruction. Verification used the plan's automated build and source-boundary scans.

## Issues Encountered

- `gsd-tools` was not on PATH in this PowerShell shell; SDK commands were run through `node C:\Users\msili\.codex\gsd-core\bin\gsd-tools.cjs` when needed.
- `ctx7` was not installed, so the Context7 CLI fallback could not fetch Kiota documentation. Local generated Kiota code and installed package XML docs were used for API signatures.
- `dotnet build` passes with existing generated nullable-context warnings under `sources/WebApp/UtfApi/**`; generated files were not edited.
- The repository had pre-existing unrelated dirty items: `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/`. They were left untouched.

## Verification

- PASS: `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal`
- PASS: `rg -n "ReadAsStringAsync|HttpClient\.SendAsync|Authorization|Bearer|InputSnapshotJson|RiskDetailsJson|AdditionalData" sources\WebApp\Services\GameServerRepository\CloudWorkflow sources\WebApp\Services\GameServerRepository\GameServerRepositoryImpl.cs` found forbidden terms only in `CloudWorkflowRedactionGuard.cs`.
- PASS: `rg -n "ProvisionCloudDryRunPlanResultAsync|TeardownCloudSessionResultAsync|CloudWorkflowKiotaSender|acknowledgedDecisionId|approvalRequestId" sources\WebApp\Services\GameServerRepository`
- PASS: Stub scan found only intentional default null parameters, empty result collections, established mapper labels, and the existing unsupported-lifecycle validation result.
- DEFERRED: Manual/browser/authenticated UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by operator instruction.

## Known Stubs

None. The scan hit intentional empty collection defaults, optional null parameters, and an existing unsupported-lifecycle validation message; none are placeholder stubs.

## Threat Flags

None.

## User Setup Required

None.

## Self-Check: PASSED

- Found summary file path.
- Found commits `4c00eff` and `eaee6e6` in git history.
- Build and repository-boundary scans passed with only noted generated-file warnings and expected redaction guard-list matches.

---
*Phase: 34-generated-client-and-repository-gate-foundation*
*Completed: 2026-06-19*

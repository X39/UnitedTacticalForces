---
phase: 22-approval-gates-and-exception-workflow
status: passed
verified_at: 2026-06-18T12:15:18Z
requirements:
  - COST-15
  - COST-16
  - COST-17
  - RELY-18
---

# Phase 22 Verification: Approval Gates And Exception Workflow

## Result

Phase 22 passes for the planned backend scope.

Billable cloud action paths now evaluate policy before execution, return visible gate results for non-allow decisions, create durable approval requests for approval-required decisions, support warning acknowledgement and approved scoped exceptions, and prevent direct controller calls from bypassing the gate.

## Requirement Coverage

| Requirement | Evidence | Status |
|-------------|----------|--------|
| COST-15 | Gate result DTO/status, controller gate integration, direct endpoint conflict tests | Passed |
| COST-16 | Gate delegates to persisted Phase 21 evaluator and blocks warn/approval/deny without acknowledgement or approval | Passed |
| COST-17 | Durable approval entity/service/API with approve/deny/expire/scope validation tests | Passed |
| RELY-18 | Direct provisioning, teardown, and lifecycle endpoint bypass tests plus admin approval route authorization checks | Passed |

## Verification Commands

| Command | Result |
|---------|--------|
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyControllerTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests"` | Passed, 45 tests |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudSpendingPolicyGateTests|FullyQualifiedName~CloudSpendingPolicyGateEndpointTests|FullyQualifiedName~CloudSpendingPolicyControllerTests"` | Passed, 49 tests |
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity quiet /clp:ErrorsOnly` | Passed, 0 errors |
| `node "$HOME\\.codex\\gsd-core\\bin\\gsd-tools.cjs" verify schema-drift 22` | Passed, no drift |
| `node "$HOME\\.codex\\gsd-core\\bin\\gsd-tools.cjs" verify codebase-drift 22` | Non-blocking warning |

## Residual Notes

The broader `FullyQualifiedName~CloudSpendingPolicy` test filter executed after 22-02 reached older Phase 21 evaluation tests and reported three fixture/expectation failures unrelated to this phase:

- duplicate `CloudCostSnapshots(SessionFk, Source)` fixture data
- duplicate active `GameServerBillingAssignments(GameServerFk)` fixture data
- provider-specific freshness expectation mismatch when selected-provider cost metadata is unknown

Those failures do not affect the 22-01 through 22-03 approval-gate scope and are documented for follow-up.

## Drift

Schema drift check is clean. Codebase drift check remains a non-blocking warning for pre-existing root files (`.gitignore`, `.gitmodules`, `AGENTS.md`, icons/readme/settings) and an unknown `.planning/config.json` key.

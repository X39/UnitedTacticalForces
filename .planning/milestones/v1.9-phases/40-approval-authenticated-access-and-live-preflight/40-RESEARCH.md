# Phase 40 Research: Approval, Authenticated Access, And Live Preflight

## Scope

Phase 40 turns the Phase 39 handoff prerequisites into durable, secret-safe preflight state before live provider mutation. The repository already has the underlying cloud records for plans, cost, policy, reconciliation, sessions, and audit. The missing piece is an operator-facing readiness record that ties the selected server, owner, reviewed dry-run plan, approval identifiers, access checks, and provider readiness into a single gate.

## Findings

| ID | Finding | Evidence | Impact |
|----|---------|----------|--------|
| R-40-01 | Live provisioning is currently reachable from `GameServersController.ProvisionReviewedCloudDryRunPlanAsync` after dry-run freshness and spending-policy gate checks. | `sources/Api/Controllers/GameServersController.cs` | Phase 40 should add an explicit preflight completion gate before `_cloudServerOperationService.ProvisionReviewedDryRunPlanAsync`. |
| R-40-02 | Durable records already exist for dry-run plans, cost estimates, provider configuration, metadata freshness, spending-policy approval requests, provider reconciliation runs, billing ownership, and hosting audit. | `sources/Api/Data/Hosting/*.cs`, `sources/Api/Data/ApiDbContext.cs` | The preflight feature should reference these records instead of duplicating their details. |
| R-40-03 | There is no durable preflight or approval-package entity yet. | `rg "preflight" sources/Api tests/Api.Tests` | Add a small hosting entity and DTO surface for evidence rows and overall readiness. |
| R-40-04 | Existing audit persistence sanitizes summaries/details through `HostingAuditService`. | `sources/Api/Services/GameServerLifecycle/HostingAuditService.cs` | Preflight changes should append audit rows through this service and avoid raw `HostingAuditRecords.Add` in new service code. |
| R-40-05 | Tests use SQLite in-memory contexts with small fake services for cloud controllers. | `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`, `tests/Api.Tests/CloudPlanning/*.cs` | Add focused API/service tests in the same style; do not require Aspire, PostgreSQL, browser auth, or live provider credentials. |
| R-40-06 | WebApp cloud workflow tests are source-boundary tests that assert repository/UI calls and forbidden copy. | `tests/Api.Tests/WebApp/*.cs` | Add boundary tests for new repository/UI preflight methods and labels if WebApp changes are made. |
| R-40-07 | Existing UI for `/servers` and `/servers/cloud` is dense operational Fluent UI. | `sources/WebApp/Pages/Servers/*.razor`, `40-UI-SPEC.md` | New UI must use the existing compact operational style and avoid marketing/explanatory screens. |

## Security Constraints

- Store token references only; never store provider tokens, Steam credentials, worker credentials, worker hashes, bearer tokens, cookies, authorization headers, OAuth codes, or raw secret-bearing payloads.
- Accept operator-entered evidence only through allowlisted fields with bounded lengths.
- Treat access evidence as route, role/scope, actor, timestamp, and outcome. Do not persist session material.
- Keep Phase 41 provider mutation blocked until the selected preflight package is complete and current.

## Implementation Direction

1. Add a durable `CloudLivePreflightRecord` aggregate under hosting data with a small enum-backed status and focused foreign keys to game server, billing account, assignment, dry-run plan, policy approval request, and optional reconciliation run.
2. Add payloads/DTOs for recording approval and evidence rows. Use the existing API DTO pattern and `Transform` mapping.
3. Add a service that computes readiness from persisted cloud data, records approval/evidence, appends audit, and validates provisioning readiness.
4. Wire server-scoped API endpoints to get, record, and refresh preflight state.
5. Add a provisioning guard before live provider mutation.
6. Surface the preflight state on `/servers` and `/servers/cloud` using the approved Phase 40 UI-SPEC labels and colors.


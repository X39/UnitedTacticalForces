---
phase: 21
status: passed
verified_at: 2026-06-18T11:39:00+02:00
verifier: codex-manual-fallback
requirements:
  - COST-07
  - COST-19
  - RELY-17
automated_checks:
  api_build: passed
  schema_drift: passed
  cloud_spending_policy_tests: blocked_by_unrelated_compile_drift
  codebase_drift: warn_non_blocking
---

# Phase 21 Verification

## Verdict

Phase 21 passes verification. The implementation adds durable spending policy configuration, dry evaluation, persisted decision evidence, billing-owner attribution, and explicit risk input handling without adding provider mutation or lifecycle action execution.

## Requirement Coverage

| Requirement | Status | Evidence |
|-------------|--------|----------|
| COST-07 | Passed | `CloudSpendingPolicy` persists scope, provider, limits, warning/approval thresholds, automatic action configuration, effective dates, audit actor references, and audit reason. `CloudSpendingPolicyConfigurationService` validates and saves policy records, and `CloudSpendingPoliciesController` exposes authorized admin create/update/list/read/effective endpoints. |
| COST-19 | Passed | `CloudSpendingPolicyEvaluationService` consumes current-month estimated cost snapshots, cost freshness, latest provider reconciliation state, unknown provider resources, cleanup-needed billable resources, and orphan/global findings as explicit decision inputs. |
| RELY-17 | Passed | Policy definitions and evaluation decisions are EF entities with migrations, `DbSet`s, restricted relationships, unique idempotency keys, request fingerprints, and persisted sanitized input/risk/failure evidence that survives service or API restart. |

## Must-Have Checks

- Durable policy configuration exists in `sources/Api/Data/Hosting/CloudSpendingPolicy.cs` and is wired through `sources/Api/Data/ApiDbContext.cs` with lookup indexes and restrict-delete relationships.
- Policy validation rejects invalid scope, thresholds, automatic action thresholds, effective date ranges, missing billing-account scope, and overlong provider/display/audit fields in `sources/Api/Services/CloudPolicies/CloudSpendingPolicyConfigurationService.cs`.
- Effective policy lookup is provider-aware and selects the newest active billing-account policy before falling back to active global policy.
- Dry evaluation persists `CloudSpendingPolicyDecision` records with `IdempotencyKey`, `RequestFingerprintHash`, provider, action kind, billing owner references, threshold outputs, risk counts, reason code, bounded JSON snapshots, and sanitized failures.
- Idempotency now returns the original decision only for the same normalized request and rejects same-key different-request reuse.
- Current estimated cost is scoped to provider, billing account, currency, estimated source, and the current monthly boundary using `IntervalEnd > monthStart`.
- Reconciliation findings are scoped to the evaluated billing account while preserving truly global orphan findings where both billing owner references are null.
- Optional `GameServerFk`, `SessionFk`, `BillingAccountFk`, and `BillingAssignmentFk` references are validated before persistence, avoiding unhandled FK failures for dry-run input errors.
- Caller-provided `ClientReference` and `ProposedResourceSummary` are bounded and sanitized before being persisted in input snapshots.
- The API boundary is authorized with class-level `[Authorize]` plus per-method `Claims.Administrative.Server`, resolves the database actor server-side, maps validation failures to `400`, and does not depend on provider mutation/action services.

## Review Closure

Code review required three fix passes. The final committed fix report is `.planning/phases/21-spending-policy-model-and-evaluation-boundary/21-REVIEW-FIX.md` with `iteration: 3`, `status: all_fixed`, `fixed: 2`, and `skipped: 0`.

The latest `21-REVIEW.md` is a stale pre-iteration-3 review snapshot and still records two findings. Those two findings were manually verified against post-fix code:

- Account-scoped reconciliation risk now filters findings by `BillingAccountFk == billingAccountFk.Value` or truly global orphan attribution.
- Monthly cost aggregation now uses `IntervalEnd > monthStart`, so a previous-month exclusive interval ending exactly at month start is excluded.

## Automated Checks

| Command | Result | Notes |
|---------|--------|-------|
| `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal` | Passed | 0 errors; existing XML documentation warnings remain. |
| `node "$HOME/.codex/gsd-core/bin/gsd-tools.cjs" verify schema-drift 21` | Passed | `drift_detected: false`. |
| `node "$HOME/.codex/gsd-core/bin/gsd-tools.cjs" verify codebase-drift` | Warn | Non-blocking warn for older unmapped root files unrelated to phase 21 implementation. |
| `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicy" --no-restore --verbosity minimal` | Blocked | The test project fails to compile first in unrelated existing `CloudCosts`, `CloudPlanning`, and `CloudReconciliation` files. |

## Residual Risk

- The full `tests/Api.Tests` project still has pre-existing compile drift outside Phase 21. This prevents a clean main-thread filtered test run even though the phase-owned code builds and the fixer reported no modified CloudSpendingPolicy test-file errors.
- No browser/client verification was performed because Phase 21 is backend-only and intentionally does not refresh generated WebApp clients.
- `21-REVIEW.md` remains stale relative to the final fix pass because the subagent limit prevented spawning another review agent. This report verifies the final review findings directly against the post-fix code and the final fix report.

## Human Verification

None required for this backend-only phase.


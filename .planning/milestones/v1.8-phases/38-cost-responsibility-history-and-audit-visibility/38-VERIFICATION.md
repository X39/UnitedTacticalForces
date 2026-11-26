---
phase: 38-cost-responsibility-history-and-audit-visibility
verified: 2026-06-21T11:44:00Z
status: human_needed
score: 3/3 must-haves verified
overrides_applied: 0
deferred:
  - truth: "Authenticated browser walkthrough remains unverified."
    addressed_in: "Phase 39 / LIVE-01"
    evidence: "Phase 38 automated checks compile and source-test the UI; no browser auth session was run."
  - truth: "Live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and provider billable-resource verification remain unverified."
    addressed_in: "Phase 39 / LIVE-01 and LIVE-02"
    evidence: "Phase 38 scope explicitly excludes live provider/runtime UAT."
audit_acknowledged:
  milestone: v1.12
  at: 2026-11-18
  status: human_needed
---

# Phase 38: Cost Responsibility, History, And Audit Visibility Verification Report

**Phase Goal:** Authorized users can inspect active and historical cost responsibility and sanitized audit context for servers they can inspect, without confusing internal attribution with payment or invoice workflows.
**Verified:** 2026-06-21T11:44:00Z
**Status:** human_needed

## Goal Achievement

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | COST-01: Authorized server-scoped users can inspect active cloud cost responsibility with owner assignment, estimated/reconciled/adjusted labels, reconciliation status, and cleanup risk. | VERIFIED | Existing server-scoped cost visibility remains on `GetCloudCostVisibilityAsync`; `/servers` continues loading `_selectedCostVisibility` and now loads owner/audit context for any selected server. |
| 2 | COST-02: Authorized server-scoped users can inspect historical owner assignments and sanitized audit context after transfers, teardown, runtime cleanup, or local deletion records. | VERIFIED | `GetBillingOwnerHistoryAsync` and `GetHostingAuditRecordsAsync` use `Authorize($"{Claims.ResourceBased.Server.All}:*")`; direct controller tests return persisted assignment and audit rows. |
| 3 | The UI labels this as internal cost responsibility / attribution and does not introduce payment, checkout, customer invoice, tax, or legal invoice workflows. | VERIFIED | `/servers` renders `Cost responsibility history`, `Internal attribution`, and `Cost responsibility audit context`; boundary tests reject payment/checkout/invoice/tax wording in the Phase 38 section. |

**Score:** 3/3 must-haves verified by code and automated tests. Overall status is `human_needed` because authenticated browser and live provider/runtime UAT remain deferred and were not claimed.

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `sources/Api/Controllers/GameServersController.cs` | Read-only server-scoped owner history and audit endpoints. | VERIFIED | Both read endpoints use server resource inspection policy; mutation remains administrative. |
| `sources/WebApp/Pages/Servers/Index.razor` | Selected-server cost responsibility history and audit visibility. | VERIFIED | Owner history/audit loading is no longer gated by `CanManageServers`; sections render empty states and safe cost-owner formatting. |
| `tests/Api.Tests/CloudCosts/CloudCostVisibilityApiTests.cs` | API authorization/read projection coverage. | VERIFIED | Reflection and direct controller tests cover policies and persisted rows. |
| `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | UI/repository boundary language coverage. | VERIFIED | Source-boundary test covers selected-server load, copy, repository calls, and forbidden commercial wording. |

## Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Focused Phase 38 API and WebApp boundary tests | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudCostVisibilityApiTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | Passed: 30 tests, 0 failed. | PASS |
| Solution compile including Razor page | `dotnet build X39.UnitedTacticalForces.slnx --no-restore` | Build succeeded with 39 existing generated WebApp nullable warnings and 0 errors. | PASS |

## Auto-Fixed Verification Issue

| Issue | Fix | Result |
|-------|-----|--------|
| First solution build failed with CS0136 due reused pattern variable names in `FormatAuditCostOwner`. | Renamed pattern variables for transfer/new-only/previous-only cases. | Final solution build passed. |

## Human Verification Required

| Test | Expected | Why Human |
|------|----------|-----------|
| Authenticated browser walkthrough of `/servers` as server-scoped non-admin and admin users. | Cost responsibility history/audit context is visible to server-scoped readers; mutation controls remain admin-only. | Requires real auth cookies/session and visual interaction checks. |
| Browser viewport review of the selected-server detail stack. | Tables fit without overlap and empty/data states remain readable. | Source/build tests cannot prove layout on rendered viewports. |
| Live owner transfer/teardown/runtime cleanup data walkthrough. | Historical intervals and audit records remain understandable after lifecycle changes. | Requires realistic persisted data and possibly live/deferred operations. |

## Gaps Summary

No implementation blockers were found for the automated Phase 38 contract. The phase remains `human_needed` only because authenticated browser and live provider/runtime evidence are explicitly deferred.

---

_Verified: 2026-06-21T11:44:00Z_
_Verifier: the agent (inline gsd-verifier)_

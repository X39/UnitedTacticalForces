---
phase: 37-provisioning-runtime-operation-and-teardown
verified: 2026-06-20T17:07:58Z
status: human_needed
score: 5/5 must-haves verified
overrides_applied: 0
deferred:
  - truth: "Authenticated browser walkthrough remains unverified."
    addressed_in: "Phase 39 / LIVE-01"
    evidence: "ROADMAP Phase 39 preserves deferred live UAT boundaries; REQUIREMENTS.md LIVE-01 tracks operator-approved authenticated/live evidence."
  - truth: "Live Hetzner provisioning and teardown mutation remains unverified."
    addressed_in: "Phase 39 / LIVE-01"
    evidence: "Phase 37 boundary says do not claim live Phase 30 UAT closure; out-of-scope requirements exclude live OPS-02/OPS-03 UAT closure."
  - truth: "Live Steam and Arma runtime operation remains unverified."
    addressed_in: "Phase 39 / LIVE-02"
    evidence: "REQUIREMENTS.md records Phase 30 live Hetzner/Steam/Arma UAT closure as deferred."
human_verification:
  - test: "Authenticated browser walkthrough of /servers provisioning, runtime action, retry, and provider teardown panels."
    expected: "Controls are visible only for authorized users, copy is clear, confirmation flow is understandable, and no forbidden fields appear."
    why_human: "Visual flow, authorization cookies, focus behavior, and browser interaction quality cannot be proven by source grep."
  - test: "Operator-approved live Hetzner provisioning and provider teardown."
    expected: "Provider resources are created and later deleted, with final provider-side billable resource count verified as zero."
    why_human: "Live provider mutation requires credentials, budget, approval, and cleanup window and is explicitly deferred."
  - test: "Live Steam/Arma runtime install, update, start, stop, restart, collect logs, and cleanup runtime."
    expected: "Arma runtime works on the provisioned worker and failures remain sanitized."
    why_human: "Requires live Steam/Arma tooling and provider worker runtime; Phase 37 automated checks intentionally do not execute it."
audit_acknowledged:
  milestone: v1.12
  at: 2026-11-18
  status: human_needed
---

# Phase 37: Provisioning, Runtime Operation, And Teardown Verification Report

**Phase Goal:** Authorized users can provision reviewed plans, operate cloud runtime jobs, and tear down provider resources while seeing cost-stop and cleanup boundaries clearly.
**Verified:** 2026-06-20T17:07:58Z
**Status:** human_needed
**Re-verification:** No - initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Authorized user can provision provider resources from the reviewed valid cloud dry-run plan and see session, resource, bootstrap, and worker status. | VERIFIED | `GameServersController.ProvisionReviewedCloudDryRunPlanAsync` is server-scoped at `POST {gameServerId}/cloud-dry-run-plans/{dryRunPlanId}/provision`, checks plan/server scope before gate/provider calls, validates current reviewed plan snapshot and metadata freshness, then delegates to cloud operation provisioning. `CloudInventoryService.CreateSessionStatusDto` projects session, resources, bootstrap credentials, worker status, and provisioning eligibility. |
| 2 | Authorized user can operate install, update, start, stop, restart, collect logs, and runtime cleanup actions with durable job progress and sanitized failure visibility. | VERIFIED | Runtime endpoints enqueue `ELifecycleJobKind` values through `EnqueueLifecycleJobAsync`, which evaluates the cloud lifecycle policy gate before `_gameServerLifecycleJobService.EnqueueAsync`. `CreateCloudRuntimeStatusAsync` projects current job, progress, last failure, retry, cleanup, worker, heartbeat, and available actions. |
| 3 | Authorized user can tear down provider resources through an explicit cost-stop action separate from runtime stop, runtime cleanup, and local server deletion. | VERIFIED | `TeardownProviderResourcesAsync` is a separate server-scoped `provider-teardown` route requiring server configuration authorization and delegates to `TeardownProviderResourcesAsync` only after policy gate success. Runtime cleanup uses `CleanupCloudRuntimeAsync` and queues `ELifecycleJobKind.Delete`; UI copy and repository labels distinguish `Cleanup runtime` from `Teardown provider resources`. |
| 4 | Authorized user can see retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states after provisioning or teardown. | VERIFIED | `CloudSessionStatusDto` includes `NeedsCleanup`, `HasOrphanCandidate`, `HasPartialFailure`, `CleanupNeededResourceCount`, `RemainingBillableResourceCount`, `CanRetryCleanup`, and `RetryableActions`; `/servers` renders cleanup, orphan, partial failure, billable, retry, and review billable resource states. |
| 5 | Runtime status and provider teardown status remain visible enough for the user to understand which resources may still cost money. | VERIFIED | `/servers` renders cloud runtime status, cost source, billable resources, cleanup risk, policy state, and provider teardown warnings. The teardown section states provider teardown is the explicit cost-stop action and runtime stop/runtime cleanup do not stop provider billing. |

**Score:** 5/5 truths verified by code and automated tests. Overall status is `human_needed` because manual/authenticated browser and live provider/runtime UAT are explicitly deferred and not claimed passed.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|--------------|----------|
| 1 | Authenticated browser walkthrough | Phase 39 / LIVE-01 | Phase 37 and REQUIREMENTS.md preserve authenticated browser UAT as deferred. |
| 2 | Live Hetzner provisioning/teardown and final provider billable count | Phase 39 / LIVE-01 | REQUIREMENTS.md out-of-scope section excludes live OPS-02/OPS-03 and Phase 30 closure. |
| 3 | Live Steam/Arma runtime operation | Phase 39 / LIVE-02 | REQUIREMENTS.md tracks Phase 30 live Hetzner/Steam/Arma UAT closure as deferred. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `sources/Api/Controllers/GameServersController.cs` | Server-scoped provision, runtime action, runtime cleanup, runtime status, and provider teardown routes. | VERIFIED | Substantive route methods found; route-scope checks precede policy/provider calls; durable lifecycle enqueue path is wired. |
| `sources/Api/Services/CloudInventory/CloudInventoryService.cs` | Owner-safe cloud status projection with cleanup/orphan/partial/remaining billable state. | VERIFIED | Status projection gathers sessions, resources, bootstrap credentials, workers, dry-run plans and returns allowlisted DTO fields. |
| `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs` | Policy gate semantics and deterministic idempotency. | VERIFIED | Provisioning, teardown, and lifecycle evaluations call the policy evaluator; idempotency key includes action/scope/cost material and is hashed deterministically. |
| `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs` | Deterministic decision reuse with contradiction checks. | VERIFIED | Existing decision lookup validates request fingerprint before reuse and rejects mismatched idempotency material. |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudProvisioningRuntimeCommandSender.cs` | Repository-local Kiota request helper for server-scoped routes. | VERIFIED | Builds `RequestInformation` for provision, provider teardown, and runtime cleanup server routes without editing generated client files. |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | Razor-facing facade methods for provision, teardown, runtime actions, and cleanup. | VERIFIED | Methods call `CloudWorkflowKiotaSender.SendAsync` with continuation query ids and safe mappers. |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | Redaction-guarded operation/result mapping. | VERIFIED | Teardown mapper treats remaining billable resources as failure/retry/review state, not full success. |
| `sources/WebApp/Pages/Servers/Index.razor` | `/servers` operation UI. | VERIFIED | `ServersProvisioningRuntimeTeardown` region renders provisioning, runtime status/actions, retry, provider teardown, risk states, and deferred UAT copy. |
| `tests/Api.Tests/...` Phase 37 tests | Backend, policy, repository, and UI boundary coverage. | VERIFIED | Focused local test run passed 119 selected tests with 0 failures. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `GameServersController` | Cloud provisioning operation service | `ProvisionReviewedCloudDryRunPlanAsync` after route/current-plan/policy gates | WIRED | Controller delegates only after `DryRunPlanBelongsToGameServerAsync`, `ValidateReviewedCloudDryRunPlanIsCurrentAsync`, and `EvaluateProvisioningAsync`. |
| `GameServersController` | Cloud teardown operation service | `TeardownProviderResourcesAsync` after route/policy gates | WIRED | Controller validates session belongs to route server before `EvaluateTeardownAsync` and service delegation. |
| `GameServersController` | `IGameServerLifecycleJobService` | Runtime action endpoints through `EnqueueLifecycleJobAsync` | WIRED | Install/update/start/stop/restart/collect logs/cleanup all flow through durable lifecycle queue. |
| `CloudInventoryService` | `CloudSessionStatusDto` | Safe status projection | WIRED | Remaining billable, cleanup, orphan, partial failure, bootstrap, and worker fields are populated from persisted state. |
| WebApp repository | GameServersController server-scoped routes | `CloudProvisioningRuntimeCommandSender` `RequestInformation` | WIRED | Repository facade uses server-scoped routes for provision and provider teardown. |
| `Index.razor` | `IGameServerRepository` | Provision/runtime/teardown handlers | WIRED | UI handlers call repository facade methods rather than generated request builders or raw browser HTTP in the Phase 37 region. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `GameServersController.ProvisionReviewedCloudDryRunPlanAsync` | `CloudProvisioningResultDto` | Existing cloud operation/provisioning service after EF-backed route, dry-run, assignment, metadata, and policy checks. | Yes | FLOWING |
| `GameServersController.CreateCloudRuntimeStatusAsync` | `CloudRuntimeStatusDto` | EF `GameServers`, `LifecycleJobs`, `GameServerLogs`, and `CloudInventoryService.GetSessionStatusesForGameServerAsync`. | Yes | FLOWING |
| `CloudInventoryService.CreateSessionStatusDto` | `CloudSessionStatusDto` | EF sessions/resources/bootstrap credentials/workers/dry-run plans. | Yes | FLOWING |
| WebApp repository operation methods | `CloudWorkflowActionResult` | Kiota `RequestAdapter` requests to server routes plus `CloudWorkflowResultMapper`. | Yes | FLOWING |
| `Index.razor` Phase 37 panels | `_selectedCloudWorkflowResult`, `_selectedCloudRuntimeStatus`, `_selectedCostVisibility`, policy rows | Repository facade methods and selected server state. | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Focused Phase 37, policy, lifecycle, repository, and UI boundary tests | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~CloudSpendingPolicyGateTests|FullyQualifiedName~CloudSpendingPolicyEvaluationTests|FullyQualifiedName~LifecycleJobQueueServiceTests|FullyQualifiedName~Arma3LifecycleJobExecutorTests" -p:UseSharedCompilation=false` | Passed: 119 tests, 0 failed. | PASS |
| Solution build | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal -p:UseSharedCompilation=false` | Build succeeded with 39 existing generated WebApp `UtfApi` nullable warnings and 0 errors. | PASS |

### Probe Execution

| Probe | Command | Result | Status |
|-------|---------|--------|--------|
| None declared | `Get-ChildItem scripts -Recurse -Filter 'probe-*.sh'`; phase artifact grep for probe paths | No `scripts` directory and no phase-declared probes. | SKIPPED |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| OPS-01 | 37-01, 37-03, 37-04 | Provision provider resources from reviewed valid plan and see session/resource/bootstrap/worker status. | SATISFIED | Server-scoped provision endpoint, current-plan validation, status projection, repository route helper, and `/servers` status UI exist and are tested. |
| OPS-02 | 37-02, 37-03, 37-04 | Operate install/update/start/stop/restart/collect logs/runtime cleanup through durable lifecycle jobs. | SATISFIED | Runtime action endpoints and repository/UI calls queue lifecycle jobs and expose progress/retry/failure state. Live Steam/Arma execution remains deferred. |
| OPS-03 | 37-01, 37-03, 37-04 | Provider teardown is explicit cost-stop separate from runtime stop/cleanup/local deletion. | SATISFIED | Separate `provider-teardown` route, provider teardown repository method, and UI section/copy distinguish cost stop from runtime cleanup. Live provider mutation remains deferred. |
| OPS-04 | 37-01, 37-02, 37-03, 37-04 | Show retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states. | SATISFIED | DTOs, mappers, tests, and UI render these states from persisted/session/resource/job data. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| N/A | N/A | No blocking `TBD`, `FIXME`, or `XXX` markers found in reviewed Phase 37 files. | INFO | Benign `return null`/`return []` matches are normal optional/empty-result paths, not stubs. |

### Human Verification Required

### 1. Authenticated Browser Walkthrough

**Test:** Log in as authorized and unauthorized users, navigate `/servers`, exercise selected-server provisioning/runtime/teardown controls through non-live-safe states.
**Expected:** Authorized controls and copy match source-verified behavior; unauthorized users cannot mutate; focus/dialog behavior is usable; no generated/raw/secret fields appear.
**Why human:** Browser auth/session behavior, visual affordance, and real interaction quality cannot be proven by static source inspection.

### 2. Live Provider Mutation And Cleanup

**Test:** With explicit operator approval, run live Hetzner provisioning and provider teardown, then verify final provider-side billable resources are zero.
**Expected:** Provider resources are created, tracked, torn down, and externally confirmed non-billable after teardown.
**Why human:** Requires live credentials, budget, provider project, cleanup window, and destructive mutation; explicitly deferred.

### 3. Live Steam/Arma Runtime UAT

**Test:** On a live provisioned worker, run install, update, start, stop, restart, collect logs, and cleanup runtime.
**Expected:** Runtime operations work against Steam/Arma and status/failure output stays sanitized.
**Why human:** Requires live Steam/Arma tooling and runtime environment; explicitly deferred.

### Gaps Summary

No implementation blockers were found for the automated/code-verifiable Phase 37 contract. The phase should not be marked fully passed because authenticated browser UAT and live Hetzner/Steam/Arma/provider mutation evidence remain deferred by project scope and have not been executed.

---

_Verified: 2026-06-20T17:07:58Z_
_Verifier: the agent (gsd-verifier)_

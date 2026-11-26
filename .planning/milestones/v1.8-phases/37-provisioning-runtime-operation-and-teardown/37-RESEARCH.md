# Phase 37: Provisioning, Runtime Operation, And Teardown - Research

**Researched:** 2026-06-20
**Domain:** ASP.NET Core cloud control-plane operations, Blazor WebAssembly workflow UI, durable lifecycle jobs, provider teardown
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
No explicit locked implementation decisions were recorded because discuss phase was skipped. The following phase boundary is binding:

Authorized users can provision reviewed plans, operate cloud runtime jobs, and tear down provider resources while seeing cost-stop and cleanup boundaries clearly.

Implementation boundaries:

- Keep the first cloud shape to one Arma 3 server per Hetzner-backed worker.
- Do not add multi-provider or multi-session worker scope.
- Keep provider teardown as the explicit cost-stop boundary.
- Do not claim live Phase 30 UAT closure.
- Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, and teardown UAT remain deferred by user instruction.

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, prior phase artifacts, and codebase conventions to guide decisions.

### Deferred Ideas (OUT OF SCOPE)
- Live Hetzner provider mutation proof.
- Steam and Arma runtime UAT proof.
- Authenticated browser UAT.
- Phase 30 closure evidence.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| OPS-01 | Authorized user can provision provider resources from the reviewed valid cloud dry-run plan and see session, resource, bootstrap, and worker status. | Existing provisioning service, cloud inventory status DTOs, generated repository facade, and partial `/servers` result panel already provide most backend and UI seams. [VERIFIED: codebase grep] |
| OPS-02 | Authorized user can operate cloud runtime lifecycle actions for install, update, start, stop, restart, collect logs, and runtime cleanup. | Existing lifecycle job kinds, cloud worker dispatch, runtime status DTO, and repository lifecycle facade support durable jobs and sanitized progress/failure display. [VERIFIED: codebase grep] |
| OPS-03 | Authorized user can tear down provider resources as the explicit cost-stop action, separate from runtime stop, runtime cleanup, and local server deletion. | Existing teardown service deletes provider resources and reports remaining billable resources; runtime cleanup queues `ELifecycleJobKind.Delete` without provider deletion. [VERIFIED: codebase grep] |
| OPS-04 | Authorized user can see retryable, cleanup-needed, orphan-risk, partial-failure, and remaining-billable-resource states after provisioning or teardown. | Existing resource/session states, cloud session status flags, reconciliation findings, teardown result fields, and workflow mapper fields cover the required vocabulary; UI needs completion and stricter presentation. [VERIFIED: codebase grep] |
</phase_requirements>

## Summary

Phase 37 should plan around existing control-plane capabilities, not a new provisioning subsystem. `CloudInventoryController` already exposes provider provisioning and teardown endpoints, `CloudProvisioningService` creates Hetzner firewall/server/IP/volume resources from valid dry-run records, and `CloudTeardownService` deletes provider resources while reporting `CleanupFailed`, `DeletedResourceCount`, and `RemainingBillableResourceCount`. [VERIFIED: codebase grep]

The largest implementation gap is productizing the owner `/servers` experience for Phase 37 semantics: provision reviewed plan, runtime job actions, retry/cleanup/orphan/partial-failure states, and a distinct destructive provider teardown confirmation. Current `/servers` code already has `Cloud action result`, `Cloud runtime`, continuation identity, and some cleanup copy, but labels and actions still need to match `37-UI-SPEC.md`, especially "Teardown provider resources" and "Cleanup runtime". [VERIFIED: codebase grep]

**Primary recommendation:** Use the existing `CloudInventoryController`, `GameServersController`, cloud lifecycle job queue, `IGameServerRepository`, `CloudWorkflowResultMapper`, and `/servers` panels; plan focused changes to owner authorization, generated-client/repository coverage, UI state completeness, redaction tests, and automated component/API tests while keeping live provider/browser UAT explicitly deferred. [VERIFIED: codebase grep]

## Project Constraints (from AGENTS.md)

- Keep ASP.NET Core API, Blazor WebAssembly, .NET Aspire, EF Core/Npgsql, generated Kiota clients, Fluent UI Blazor, xUnit, and the existing `sources/` tree. [VERIFIED: AGENTS.md]
- Do not edit generated `sources/WebApp/UtfApi/**`, EF migration generated files, `obj/**`, or designer files as style sources. [VERIFIED: AGENTS.md]
- WebApp cloud integration must use generated Kiota clients behind `IGameServerRepository`; do not add raw browser HTTP bridges. [VERIFIED: AGENTS.md]
- Use DTO/payload/update boundaries instead of returning EF entities to clients. [VERIFIED: AGENTS.md]
- Preserve server/resource authorization checks for game-server and cloud workflow actions. [VERIFIED: AGENTS.md]
- Do not expose provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, raw provider payloads, or secret-bearing JSON to frontend clients, logs, evidence, or audit displays. [VERIFIED: AGENTS.md]
- Keep provider teardown/deletion as the cost-stop boundary; runtime stop and runtime cleanup do not stop Hetzner billing. [VERIFIED: AGENTS.md]
- Do not implement multi-provider orchestration, multi-session workers, payment processing, customer invoicing, checkout, taxes, Kubernetes, generic cluster platforms, or Phase 30 live UAT closure. [VERIFIED: AGENTS.md]
- Add focused tests around persistence, job transitions, authorization-sensitive actions, worker recovery, redaction, and frontend repository/UI boundaries. [VERIFIED: AGENTS.md]
- Use `.editorconfig`, file-scoped namespaces, one public type per file, PascalCase public members, private `_field` names, and targeted `dotnet build` / `dotnet test` verification. [VERIFIED: AGENTS.md]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Provision reviewed dry-run plan | API / Backend | Database / Storage, Provider API | API owns policy gate validation, dry-run/session/resource state validation, token resolution, provider calls, audit, and persistence. [VERIFIED: codebase grep] |
| Runtime lifecycle jobs | API / Backend | Cloud worker, Database / Storage | API queues durable `LifecycleJob` rows; cloud workers claim/report progress; database stores job, worker, logs, and runtime state. [VERIFIED: codebase grep] |
| Provider teardown cost stop | API / Backend | Provider API, Database / Storage | Teardown service owns provider deletion order, resource state transitions, remaining billable counts, and audit. [VERIFIED: codebase grep] |
| Retry/cleanup/orphan/partial-failure visibility | API / Backend | Browser / Client | API projects safe status DTOs; Blazor renders allowlisted rows, badges, next steps, and deferred UAT copy. [VERIFIED: codebase grep] |
| Workflow action rendering and continuation | Browser / Client | API / Backend | `/servers` stores pending action identity and passes only displayed `acknowledgedDecisionId` / `approvalRequestId` through repository methods. [VERIFIED: 36-VERIFICATION.md] |
| Cost-stop education | Browser / Client | API / Backend | UI copy must distinguish runtime stop/cleanup from provider teardown, while API state exposes remaining billable resources. [VERIFIED: 37-UI-SPEC.md] |

## Standard Stack

### Core

| Library / Component | Version | Purpose | Why Standard |
|---------------------|---------|---------|--------------|
| .NET target framework | `net9.0` | API, WebApp, CloudWorker, and tests | All active projects target `net9.0`; keep Phase 37 in the active solution stack. [VERIFIED: codebase grep] |
| ASP.NET Core API | 9.0.4 package family | Controllers, authorization, OpenAPI, hosted services | Existing cloud endpoints live in API controllers and services. [VERIFIED: codebase grep] |
| EF Core with Npgsql | `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 | Persistent sessions, resource inventory, workers, jobs, costs, policies, audit | Existing control-plane state is EF-backed and persisted in `ApiDbContext`. [VERIFIED: codebase grep] |
| Blazor WebAssembly | 9.0.4 package family | `/servers` owner/operator UI | Existing owner workflow is Blazor WebAssembly. [VERIFIED: codebase grep] |
| Microsoft Fluent UI Blazor | 4.11.8 | Buttons, message bars, dialogs, progress, grid styling | `37-UI-SPEC.md` requires Fluent UI Blazor and existing `.servers-page` conventions. [VERIFIED: 37-UI-SPEC.md] |
| Microsoft Kiota Bundle | 1.22.2 | Generated WebApp API client | Current `WebApp.csproj` uses Kiota Bundle 1.22.2; repository facades wrap generated clients. [VERIFIED: codebase grep] |
| xUnit + Microsoft.NET.Test.Sdk | xUnit 2.9.3, test SDK 17.13.0 | Automated API/service/boundary tests | Existing `tests/Api.Tests` covers cloud service and WebApp source-boundary behavior. [VERIFIED: codebase grep] |

### Supporting

| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `sources/CloudWorker` | `net9.0` exe | Worker-side Arma 3 lifecycle execution and runtime status reporting | Use for OPS-02 planning; do not reimplement worker protocol in UI. [VERIFIED: codebase grep] |
| `CloudWorkflowKiotaSender` | local | Continuation query patching and safe policy conflict mapping | Use for provisioning, lifecycle, teardown, and future generated-client gaps. [VERIFIED: codebase grep] |
| `CloudWorkflowRedactionGuard` | local | UI/display string field and vocabulary guard | Use before rendering any workflow result, resource, cost, audit, failure, or approval text. [VERIFIED: codebase grep] |
| SQLite EF provider | 9.0.4 in tests | In-memory relational tests | Use for Phase 37 API/service tests that do not require PostgreSQL-specific behavior. [VERIFIED: codebase grep] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Existing API service methods | New orchestration service layer | Not recommended for Phase 37 because provisioning, teardown, lifecycle, policy, inventory, and cost services already exist and tests cover them. [VERIFIED: codebase grep] |
| Generated Kiota + repository facade | Raw `HttpClient` calls from Razor | Forbidden by project constraints and existing boundary tests. [VERIFIED: AGENTS.md] |
| Durable lifecycle jobs | Fire-and-forget browser/API work | Not recommended because OPS-02 requires durable progress and retry visibility. [VERIFIED: REQUIREMENTS.md] |

**Installation:** No new external packages are recommended for Phase 37. [VERIFIED: codebase grep]

## Package Legitimacy Audit

No package installation is recommended. Package legitimacy gate is not applicable. [VERIFIED: codebase grep]

## Architecture Patterns

### System Architecture Diagram

```text
/servers selected server
  -> IGameServerRepository facade
    -> generated Kiota request builders / CloudWorkflowKiotaSender
      -> API controllers
        -> policy gate check
          -> blocked: CloudSpendingPolicyGateResultDto -> repository safe policy result -> UI next step
          -> allowed: provisioning / lifecycle / teardown service
             -> EF Core persisted state: session, resources, jobs, workers, logs, audit, cost snapshots
             -> Hetzner client only inside API provider services
             -> cloud worker claims lifecycle jobs and reports progress/runtime/log/failure
      -> repository safe projection / redaction guard
        -> /servers status, runtime, result, teardown, retry, cleanup panels
```

### Recommended Project Structure

```text
sources/Api/Controllers/
  CloudInventoryController.cs        # Provision/teardown admin routes; owner-safe additions if needed
  GameServersController.cs           # Server-scoped cloud runtime/status/lifecycle routes
sources/Api/Services/
  CloudProvisioning/                 # Provider resource creation from reviewed plan
  CloudTeardown/                     # Provider deletion and cost-stop result
  CloudInventory/                    # Safe session/resource/bootstrap/worker projection
  CloudWorkerDispatch/               # Cloud worker claim/report protocol
  GameServerLifecycle/               # Durable lifecycle job queue/retry/audit
sources/WebApp/Services/GameServerRepository/
  IGameServerRepository.cs           # Razor-facing facade
  GameServerRepositoryImpl.cs        # Generated-client wrapper only
  CloudWorkflow/                     # Safe result models, redaction, mapper, Kiota sender
sources/WebApp/Pages/Servers/
  Index.razor(.css)                  # Owner/operator workflow panels
tests/Api.Tests/
  CloudProvisioning/CloudProvisioningServiceTests.cs
  CloudTeardown/CloudTeardownServiceTests.cs
  CloudInventory/CloudInventoryVisibilityTests.cs
  WebApp/CloudWorkflowRepositoryBoundaryTests.cs
```

### Pattern 1: Policy-Gated Action Before Mutation

**What:** API action evaluates the spending policy gate with continuation ids before calling the mutating service. [VERIFIED: codebase grep]

**When to use:** Provisioning and provider teardown must use this path; runtime start/restart/update/install currently map through lifecycle policy gates. [VERIFIED: codebase grep]

```csharp
// Source: sources/Api/Controllers/CloudInventoryController.cs
var gate = await cloudSpendingPolicyGateService.EvaluateProvisioningAsync(
    planId, user, cancellationToken, acknowledgedDecisionId, approvalRequestId);
if (!gate.CanProceed)
    return Conflict(gate.ToDto());

var result = await cloudProvisioningService.ProvisionDryRunPlanAsync(planId, user, cancellationToken);
return Ok(result);
```

### Pattern 2: Durable Runtime Work Through Lifecycle Jobs

**What:** User action queues a `LifecycleJob`; workers claim jobs by scope and report progress, logs, status, success, or retryable failure. [VERIFIED: codebase grep]

**When to use:** OPS-02 install, update, start, stop, restart, collect logs, and runtime cleanup. [VERIFIED: REQUIREMENTS.md]

```csharp
// Source: sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs
Eligible queued or retry-pending jobs are claimed with a lease, worker id, attempt count,
and timestamps; failures can become RetryPending until MaxAttemptCount is reached.
```

### Pattern 3: Safe Repository Projection

**What:** Razor consumes `CloudWorkflowActionResult` and safe hand-written models, never generated DTOs or raw payload strings. [VERIFIED: codebase grep]

**When to use:** All Phase 37 UI updates: provisioning result, runtime status, teardown result, retry state, cost/risk rows, and audit rows. [VERIFIED: 37-UI-SPEC.md]

```csharp
// Source: sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
return CloudWorkflowKiotaSender.SendAsync(
    RequestAdapter,
    requestInfo,
    CloudTeardownResultDto.CreateFromDiscriminatorValue,
    result => CloudWorkflowResultMapper.FromTeardownResult(result),
    CloudWorkflowActionKind.Teardown,
    "Teardown cloud resources",
    acknowledgedDecisionId,
    approvalRequestId,
    cancellationToken);
```

### Anti-Patterns to Avoid

- **Treating runtime cleanup as cost stop:** `CleanupCloudRuntimeAsync` queues `ELifecycleJobKind.Delete` for worker-side cleanup and leaves session/resource provider state untouched. [VERIFIED: codebase grep]
- **Showing success when billable resources remain:** `CloudTeardownResultDto.RemainingBillableResourceCount > 0` means provider cost may continue; UI must not show fully complete success. [VERIFIED: codebase grep]
- **Bypassing generated client facade:** Existing tests assert cloud workflow methods use repository/generator boundaries. [VERIFIED: codebase grep]
- **Rendering raw failure or payload data:** `CloudWorkflowRedactionGuard` and service sanitizers exist because provider/worker/Steam secrets are in scope. [VERIFIED: codebase grep]
- **Claiming live UAT:** Phase artifacts and user instruction defer authenticated browser, live Hetzner, Steam, Arma, provisioning, teardown, final billable resource verification, OPS-02/OPS-03 live UAT, and Phase 30 evidence. [VERIFIED: 37-CONTEXT.md]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Provider provisioning | New provider mutation path | `ICloudProvisioningService.ProvisionDryRunPlanAsync` | Already validates plan/session/resource/owner/metadata/token state and persists audit. [VERIFIED: codebase grep] |
| Provider cost-stop teardown | Runtime cleanup or local delete flow | `ICloudTeardownService.TeardownSessionAsync` | Already deletes provider resource kinds and returns remaining billable counts. [VERIFIED: codebase grep] |
| Runtime job orchestration | Ad hoc background tasks | `IGameServerLifecycleJobService` and `ILifecycleJobQueueService` | Durable queued/running/retry/failed/succeeded state is already modeled. [VERIFIED: codebase grep] |
| Worker protocol | UI/API polling protocol invented for Phase 37 | `CloudWorkerDispatchService` and `sources/CloudWorker` | Claim/progress/log/runtime/failure/success protocol is already implemented and tested. [VERIFIED: codebase grep] |
| Redaction | New string filters per view | `CloudWorkflowRedactionGuard` plus `LifecycleFailureReasonSanitizer` | Shared allowlist/blocklist prevents duplicated incomplete redaction. [VERIFIED: codebase grep] |
| Policy continuation | UI-generated continuation ids | Existing Phase 36 pending-action identity and `CloudWorkflowKiotaSender` query patching | Prevents stale/mismatched warning/approval reuse. [VERIFIED: 36-VERIFICATION.md] |

**Key insight:** Phase 37 is an integration and productization phase over existing durable cloud primitives; custom parallel mechanisms would increase state divergence and risk wrong cost-stop semantics. [VERIFIED: codebase grep]

## Common Pitfalls

### Pitfall 1: Admin-Only Provision/Teardown Endpoints Blocking Owner Workflow
**What goes wrong:** `/servers` owner users can review policy but cannot provision/teardown because `CloudInventoryController.ProvisionDryRunPlanAsync` and `TeardownSessionAsync` are `[Authorize(Claims.Administrative.Server)]`. [VERIFIED: codebase grep]
**Why it happens:** Earlier admin console routes predate the owner workflow. [VERIFIED: ROADMAP.md]
**How to avoid:** Plan owner-safe server-scoped endpoints or adjust authorization with exact resource ownership checks before exposing `/servers` controls. [VERIFIED: codebase grep]
**Warning signs:** UI button appears but API returns 403 for non-admin owner/operator. [VERIFIED: 37-UI-SPEC.md]

### Pitfall 2: `ELifecycleJobKind.Delete` Means Two Different Things
**What goes wrong:** Runtime cleanup and local server delete can be confused with provider teardown. [VERIFIED: codebase grep]
**Why it happens:** `CleanupCloudRuntimeAsync` queues `Delete`, while provider teardown is a separate cloud inventory DELETE endpoint. [VERIFIED: codebase grep]
**How to avoid:** UI labels must say `Cleanup runtime` for worker cleanup and `Teardown provider resources` for cost stop. [VERIFIED: 37-UI-SPEC.md]
**Warning signs:** Copy says "delete", "done", "cleanup complete", or "stop cost" near runtime cleanup. [VERIFIED: 37-UI-SPEC.md]

### Pitfall 3: Partial Provisioning Leaves Cleanup Needed
**What goes wrong:** Provisioning can create some provider resources and then fail, leaving session/resource state `CleanupNeeded`. [VERIFIED: codebase grep]
**Why it happens:** `CloudProvisioningService.MarkProvisioningFailedAsync` marks created resources cleanup-needed after provider exceptions. [VERIFIED: codebase grep]
**How to avoid:** Plan UI and repository mapping for failed provisioning to show retry/cleanup-needed/resource rows instead of only generic failure. [VERIFIED: codebase grep]
**Warning signs:** Failed provisioning result has no session/resource table or next cleanup action. [VERIFIED: 37-UI-SPEC.md]

### Pitfall 4: Success Styling With Remaining Billable Resources
**What goes wrong:** Teardown result appears successful despite remaining billable resources. [VERIFIED: codebase grep]
**Why it happens:** Current mapper has generated-client teardown path without full `Failure` object and uses generic status labels; UI can underspecify partial failure. [VERIFIED: codebase grep]
**How to avoid:** Treat `RemainingBillableResourceCount > 0` as danger/warning with required next step `Review billable resources`; success only when remaining count is zero. [VERIFIED: 37-UI-SPEC.md]
**Warning signs:** `Teardown completed` shown while remaining billable resources > 0. [VERIFIED: 37-UI-SPEC.md]

### Pitfall 5: Exposing Credential Vocabulary Through Details
**What goes wrong:** Provider token, bootstrap credential, worker hash, Steam credential, raw JSON, or request/response text reaches UI/evidence. [VERIFIED: AGENTS.md]
**Why it happens:** Cloud operations touch provider tokens, cloud-init user data, worker credentials, Steam tooling, and generated DTO transport. [VERIFIED: codebase grep]
**How to avoid:** Keep UI on safe repository models; fail closed on redaction; add source-boundary tests for Phase 37 region. [VERIFIED: codebase grep]
**Warning signs:** Razor binds `AdditionalData`, `DetailsJson`, `TokenReference`, `CredentialHash`, exception bodies, or raw payload fields. [VERIFIED: codebase grep]

## Code Examples

### Cost-Stop Teardown Result Contract

```csharp
// Source: sources/Api/DTO/CloudTeardownResultDto.cs
public int DeletedResourceCount { get; init; }
public int RemainingBillableResourceCount { get; init; }
public bool CleanupFailed { get; init; }
public string? FailureSummary { get; init; }
```

Use these exact fields to drive Phase 37 UI states and tests. [VERIFIED: codebase grep]

### Runtime Status Projection Contract

```csharp
// Source: sources/Api/DTO/CloudRuntimeStatusDto.cs
public LifecycleJobDto? CurrentJob { get; init; }
public int? CurrentJobProgressPercent { get; init; }
public string? LastFailureReason { get; init; }
public bool CanRetry { get; init; }
public bool CanCleanup { get; init; }
public IReadOnlyList<string> AvailableActions { get; init; } = [];
```

Use these fields for OPS-02 durable progress, retry, and runtime cleanup controls. [VERIFIED: codebase grep]

### Session Status Projection Contract

```csharp
// Source: sources/Api/DTO/CloudSessionStatusDto.cs
public bool CanProvision { get; init; }
public bool CanTeardown { get; init; }
public bool CanRetryCleanup { get; init; }
public IReadOnlyList<string> RetryableActions { get; init; } = [];
public bool NeedsCleanup { get; init; }
public bool HasOrphanCandidate { get; init; }
```

Use these fields for OPS-01 and OPS-04 status rows and action eligibility. [VERIFIED: codebase grep]

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| API-owned local process state for lifecycle actions | Persistent `LifecycleJob` and `LifecycleWorker` entities with worker claim/retry/recovery | v1.0-v1.3 milestones before Phase 37 | Phase 37 should display durable jobs instead of firing direct runtime operations from UI. [VERIFIED: ROADMAP.md] |
| Non-mutating dry-run only | Reviewed dry-run plus policy gate continuation can call provisioning/teardown endpoints | Phases 35-36 | Phase 37 can consume existing gates and repository patterns. [VERIFIED: 36-VERIFICATION.md] |
| Admin fleet console as cloud surface | `/servers` owner/operator workflow is primary, `/servers/cloud` remains admin provider/fleet console | v1.8 decisions | Phase 37 UI should extend `/servers` and avoid routing normal owners to `/servers/cloud`. [VERIFIED: STATE.md] |
| Runtime stop/cleanup ambiguity | Provider teardown/deletion is explicit cost-stop boundary | v1.8 decisions | UI and tests must preserve the distinction. [VERIFIED: STATE.md] |

**Deprecated/outdated:** Do not use live Phase 30 provider/browser evidence as a Phase 37 success signal; it remains deferred until a separate operator-approved evidence milestone. [VERIFIED: REQUIREMENTS.md]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| None | All Phase 37 research claims are sourced from project artifacts or local code reads in this session. | All | No assumption-gated install or architecture decision is needed. |

## Open Questions (RESOLVED)

1. **RESOLVED: owner-safe provisioning/teardown should use new server-scoped API routes, not relaxed cloud inventory authorization.**
   - What we know: Existing routes are admin-only, while `/servers` is the owner/operator workflow. [VERIFIED: codebase grep]
   - Decision: Add server-scoped owner/operator routes under `GameServersController` that internally call existing services after resource authorization and scope validation. Keep admin cloud inventory routes for `/servers/cloud`. [VERIFIED: AGENTS.md]
   - Planning impact: `37-01-PLAN.md` owns the API route shape and coverage.

2. **RESOLVED: runtime cleanup must remain cleanup runtime, not provider teardown.**
   - What we know: `CloudSpendingPolicyGateService` maps `ELifecycleJobKind.Delete` to `ProviderTeardown`, but `CleanupCloudRuntimeAsync` queues `Delete` without provider deletion. [VERIFIED: codebase grep]
   - Decision: Plan a focused policy/action-kind review so runtime cleanup is never displayed, audited, or confirmed as provider teardown unless it actually deletes provider resources. Provider teardown remains the explicit cost-stop action. [VERIFIED: 37-UI-SPEC.md]
   - Planning impact: `37-02-PLAN.md` owns runtime job semantics and `37-04-PLAN.md` owns UI copy/confirmation separation.

3. **RESOLVED: generated-client work is conditional on new owner-safe route additions and must not be hand-edited.**
   - What we know: Repository methods reference generated paths for provision, teardown, lifecycle, and runtime status. [VERIFIED: codebase grep]
   - Decision: Include a Wave 0 generated-client check. If Phase 37 adds owner-safe routes missing from `sources/WebApp/UtfApi/**`, regenerate through the project-supported client generation path and keep generated files out of hand edits. [VERIFIED: STATE.md]
   - Planning impact: `37-03-PLAN.md` owns repository-safe access, generated-client boundary checks, mapper updates, and redaction tests.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test all projects | Yes | 10.0.201 installed; projects target net9.0 | Use installed SDK if it builds target framework. [VERIFIED: command output] |
| PostgreSQL | Full Aspire/API persistence runtime | Not probed for research | Project requires PostgreSQL for full app | Use SQLite in-memory for automated service/controller tests where current tests already do. [VERIFIED: codebase grep] |
| Hetzner credentials / live provider | Live provisioning/teardown UAT | Not probed by instruction | N/A | Do not run live provider mutation; use fake `IHetznerCloudClient` tests. [VERIFIED: 37-CONTEXT.md] |
| Steam/Arma runtime credentials/files | Live OPS-02 UAT | Not probed by instruction | N/A | Keep live Steam/Arma UAT deferred; use worker/service tests. [VERIFIED: REQUIREMENTS.md] |
| Browser/authenticated session | Manual UI UAT | Not probed by instruction | N/A | Use source-boundary/component-style tests only; manual UAT remains deferred. [VERIFIED: 37-CONTEXT.md] |

**Missing dependencies with no fallback:** None for planning and automated non-live tests. [VERIFIED: command output]

**Missing dependencies with fallback:** Live provider, Steam/Arma, authenticated browser UAT are intentionally deferred and must be represented as gaps, not failures. [VERIFIED: 37-CONTEXT.md]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 and EF Core SQLite 9.0.4. [VERIFIED: codebase grep] |
| Config file | `tests/Api.Tests/Api.Tests.csproj`. [VERIFIED: codebase grep] |
| Quick run command | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| OPS-01 | Provision valid reviewed plan and project session/resource/bootstrap/worker status without secrets | API/service + repository/UI boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | Existing partial; add owner-safe route/UI action tests. [VERIFIED: codebase grep] |
| OPS-02 | Queue and display install/update/start/stop/restart/collect logs/runtime cleanup as durable jobs | API/service + worker dispatch + source-boundary UI | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkerDispatchServiceTests|FullyQualifiedName~Arma3LifecycleJobExecutorTests"` | Existing partial; add `/servers` action label/control tests. [VERIFIED: codebase grep] |
| OPS-03 | Provider teardown is explicit cost-stop and separate from runtime cleanup/local delete | API/service + UI source-boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | Existing partial; add destructive confirmation/copy tests. [VERIFIED: codebase grep] |
| OPS-04 | Retryable, cleanup-needed, orphan-risk, partial-failure, remaining-billable states visible | API/service + mapper + UI source-boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudTeardownServiceTests|FullyQualifiedName~CloudInventoryVisibilityTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"` | Existing partial; add state-specific UI copy/tests. [VERIFIED: codebase grep] |

### Sampling Rate

- **Per task commit:** Focused command for touched subsystem: provisioning/teardown/inventory/lifecycle/WebApp boundary. [VERIFIED: codebase grep]
- **Per wave merge:** `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --verbosity minimal`. [VERIFIED: codebase grep]
- **Phase gate:** `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` and focused Phase 37 tests. [VERIFIED: 36-VERIFICATION.md]

### Wave 0 Gaps

- [ ] `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` additions for owner-safe provisioning/teardown status, if route authorization changes. [VERIFIED: codebase grep]
- [ ] `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` for Phase 37 UI copy, no raw generated DTO binding, teardown confirmation, deferred-UAT copy, and forbidden terms. [VERIFIED: 37-UI-SPEC.md]
- [ ] `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` additions for any new owner-safe generated route and `Teardown provider resources` label. [VERIFIED: codebase grep]
- [ ] Mapper tests/source checks for partial teardown failure to ensure remaining billable resources do not render as full success. [VERIFIED: codebase grep]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes | Existing ASP.NET Core `[Authorize]` and authenticated user lookup before mutating actions. [VERIFIED: codebase grep] |
| V3 Session Management | yes | Existing cookie/session behavior through WebApp/API; Phase 37 should not add client-side token handling. [VERIFIED: AGENTS.md] |
| V4 Access Control | yes | Resource-based server claims plus billing owner/scope checks before owner workflow actions. [VERIFIED: codebase grep] |
| V5 Input Validation | yes | Existing DTO payload validation, EF state validation, policy gate scope validation, and redaction guard. [VERIFIED: codebase grep] |
| V6 Cryptography | yes | Worker credential hash comparison uses SHA-256 and fixed-time comparison; do not expose or reimplement credential handling. [VERIFIED: codebase grep] |
| V7 Error Handling | yes | Use sanitized failures and redaction-blocked states; do not display provider exception bodies. [VERIFIED: codebase grep] |
| V9 Communications | yes | Provider/worker secrets stay server-side; UI uses repository-safe projections only. [VERIFIED: AGENTS.md] |

### Known Threat Patterns for Phase 37

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Owner provisioning a stale/superseded/wrong-owner plan | Elevation of Privilege / Tampering | Preserve dry-run review blockers and `CloudProvisioningService` validation before token resolution. [VERIFIED: codebase grep] |
| Warning/approval replay across scope | Elevation of Privilege | Preserve Phase 36 continuation identity and exact scope/expiration matching. [VERIFIED: 36-VERIFICATION.md] |
| Secret leakage through provider failures | Information Disclosure | Use `LifecycleFailureReasonSanitizer`, `CloudWorkflowRedactionGuard`, and fail-closed UI rendering. [VERIFIED: codebase grep] |
| Runtime cleanup mistaken for provider cost stop | Spoofing / Repudiation | Explicit UI copy and separate provider teardown confirmation. [VERIFIED: 37-UI-SPEC.md] |
| Partial provider deletion hidden as success | Repudiation / Financial impact | Display remaining billable resources, cleanup-needed resources, and retry/review next steps. [VERIFIED: codebase grep] |
| Cloud worker claiming another server's job | Elevation of Privilege | Keep `LifecycleJobQueueService.ApplyWorkerClaimScope` scoped by worker kind/session/game-server. [VERIFIED: codebase grep] |

## Sources

### Primary (HIGH confidence)

- `AGENTS.md` - project constraints, stack, architecture, conventions, security, testing. [VERIFIED: file read]
- `.planning/STATE.md` - current phase, deferred UAT boundaries, recent v1.8 decisions. [VERIFIED: file read]
- `.planning/ROADMAP.md` - Phase 37 goal/success criteria and implementation boundaries. [VERIFIED: file read]
- `.planning/REQUIREMENTS.md` - OPS-01 through OPS-04 and out-of-scope live UAT. [VERIFIED: file read]
- `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-CONTEXT.md` - phase constraints and deferred ideas. [VERIFIED: file read]
- `.planning/phases/37-provisioning-runtime-operation-and-teardown/37-UI-SPEC.md` - UI, copy, accessibility, redaction, and state contracts. [VERIFIED: file read]
- `.planning/phases/36-policy-gate-and-approval-ux/36-VERIFICATION.md` - verified Phase 36 policy gate and continuation behavior. [VERIFIED: file read]
- `sources/Api/Controllers/CloudInventoryController.cs` - provisioning/teardown endpoints and policy gates. [VERIFIED: codebase grep]
- `sources/Api/Controllers/GameServersController.cs` - server-scoped cloud status/runtime/cleanup and lifecycle job queueing. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` - Hetzner provisioning validation, mutation, audit, failure cleanup-needed state. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` - provider deletion order, cleanup-needed state, remaining billable count. [VERIFIED: codebase grep]
- `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` - durable job claim/retry/recovery semantics. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudWorkerDispatch/CloudWorkerDispatchService.cs` - worker authentication, claim snapshot, progress/log/runtime/failure reporting. [VERIFIED: codebase grep]
- `sources/WebApp/Services/GameServerRepository/*` and `CloudWorkflow/*` - repository facade, generated-client sender, mapper, redaction guard. [VERIFIED: codebase grep]
- `sources/WebApp/Pages/Servers/Index.razor(.css)` - existing owner workflow panels and partial runtime/status UI. [VERIFIED: codebase grep]
- `tests/Api.Tests/**` - existing automated coverage for provisioning, teardown, inventory visibility, worker dispatch, worker execution, policy, and WebApp boundary checks. [VERIFIED: codebase grep]

### Secondary (MEDIUM confidence)

- None. This research is codebase and phase-artifact grounded; no external docs were needed because Phase 37 recommends no new packages or external architecture. [VERIFIED: codebase grep]

### Tertiary (LOW confidence)

- None. [VERIFIED: file read]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - verified from csproj files and project artifacts. [VERIFIED: codebase grep]
- Architecture: HIGH - existing API/service/worker/WebApp seams were read directly. [VERIFIED: codebase grep]
- Pitfalls: HIGH - derived from existing services, tests, UI spec, and Phase 36 verification. [VERIFIED: codebase grep]
- Live UAT readiness: HIGH that it is deferred, LOW that live behavior is proven because live Hetzner/Steam/Arma/browser mutation was intentionally not run. [VERIFIED: 37-CONTEXT.md]

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 for codebase-local planning; revisit before any live provider/UAT milestone because provider behavior and credentials are intentionally unverified. [VERIFIED: 37-CONTEXT.md]

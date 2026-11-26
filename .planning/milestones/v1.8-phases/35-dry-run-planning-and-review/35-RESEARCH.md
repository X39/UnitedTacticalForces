# Phase 35: Dry-Run Planning And Review - Research

**Researched:** 2026-06-20
**Domain:** ASP.NET Core 9 / EF Core cloud dry-run planning, Blazor WebAssembly `/servers` review UX, Kiota repository projection
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
## Implementation Decisions

### Phase Boundary
- Dry-run planning must remain non-mutating with respect to cloud providers.
- Provider provisioning must not be available from stale, invalid, mismatched, superseded, or owner-mismatched plans.
- Review content must be sanitized and must preserve Phase 34 redaction guarantees for tokens, credentials, headers, worker hashes, and raw secret-bearing payloads.
- Cost estimates must clearly label source and freshness rather than implying reconciled provider billing.

### Product Flow
- Continue the v1.8 owner-facing path from `/servers`; `/servers/cloud` remains the admin/provider/fleet surface.
- Use generated Kiota clients and WebApp repository facades for WebApp integration, preserving the Phase 34 boundary against raw browser HTTP bridges.
- Prefer regenerating a plan when owner assignment, inputs, metadata freshness, or validation state makes the current reviewed plan unusable.
- Treat plan invalidity and supersession as actionable workflow states, not silent failures.

### Verification Boundary
- Automated/source verification should cover non-mutating behavior, stale-plan blocking, owner mismatch blocking, supersession, regeneration, and redaction.
- Manual browser/authenticated/live-provider/Steam/Arma UAT is deferred by operator instruction and must not be claimed as passed.
- Phase 30 live evidence remains deferred and out of scope unless a separate operator-approved live evidence milestone is started.

### the agent's Discretion
Implementation choices are otherwise at the agent's discretion, guided by ROADMAP.md, REQUIREMENTS.md, existing Phase 33 and Phase 34 workflow patterns, and current codebase conventions.

### Deferred Ideas (OUT OF SCOPE)
## Deferred Ideas

- Live Hetzner provisioning, provider-console verification, final provider billable resource verification, SteamCMD/DepotDownloader validation, and native Arma runtime UAT.
- Policy approval UX expansion beyond whatever is required to show that invalid or blocked plans cannot be provisioned.
- Payment processing, external invoicing, taxes, checkout, multi-provider orchestration, multi-session workers, resize, snapshots, and backups.
- Broad redesign of server administration pages outside the dry-run planning and review workflow.
</user_constraints>

## Summary

Phase 35 should build on the existing local-only `CloudPlanningController.CreateArma3DryRunPlanAsync`, `Arma3CloudDryRunPlannerService`, `CloudInventoryService`, `CloudCostEstimateService`, generated Kiota route `GameServers/{id}/CloudDryRunPlans/Arma3`, and Phase 34 `CloudWorkflowActionResult` repository surface. [VERIFIED: codebase grep] The existing dry-run service already persists a session, inventory resources, dry-run plan, cost estimate, and audit record without calling `IHetznerCloudClient` or `HttpClient`; the planner should preserve that non-provider-mutating boundary. [VERIFIED: tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs] [VERIFIED: tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs]

The missing Phase 35 slice is not the core dry-run calculation; it is review readiness and blocking semantics for the owner-facing `/servers` flow. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor] Existing provisioning validation rejects non-valid, stale-metadata, owner-mismatched, session-mismatched, and consumed plans before resolving provider credentials, but existing dry-run creation does not mark older plans `Superseded`, does not persist or compare a typed input snapshot/fingerprint, and does not expose an owner-safe full review projection with resources, firewall rules, assumptions, validation failures, freshness, and estimate labels. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] [VERIFIED: sources/Contract/GameServer/ECloudDryRunPlanStatus.cs] [VERIFIED: codebase grep]

**Primary recommendation:** Add owner-scoped dry-run review contracts/endpoints under `game-servers/{id}`, persist enough plan-review state to classify current/stale/superseded/input-mismatched/owner-mismatched plans, regenerate through the existing planner service, regenerate Kiota artifacts, map to repository-safe review models, and render the approved Phase 35 panel in `/servers` without enabling provider mutation. [VERIFIED: 35-CONTEXT.md] [VERIFIED: 35-UI-SPEC.md] [VERIFIED: codebase grep]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Generate Arma 3 dry-run plan | API / Backend | Database / Storage | `CloudPlanningController` and `Arma3CloudDryRunPlannerService` own validation and persistence; EF stores sessions, plans, resources, estimates, and audit. [VERIFIED: sources/Api/Controllers/CloudPlanningController.cs] [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs] |
| Persist review/supersession/input-match state | Database / Storage | API / Backend | `CloudDryRunPlan` currently stores status and metadata timestamp; Phase 35 needs durable review fields or a projection source for supersession and mismatch checks. [VERIFIED: sources/Api/Data/Hosting/CloudDryRunPlan.cs] |
| Block invalid provider provisioning | API / Backend | Database / Storage | `CloudProvisioningService.ValidateAsync` is the authoritative server-side provider-mutation gate; UI disabling is only explanatory. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] |
| Owner-facing dry-run review | Browser / Client | API / Backend | `/servers` renders guided setup and repository-backed cloud workflow results; it should consume typed repository projections, not raw generated DTOs. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Redaction and allowlisting | WebApp repository | API / Backend | Phase 34 added `CloudWorkflowRedactionGuard` and mapper models; Phase 35 should extend these or add sibling review models with the same guard. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] |

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PLAN-01 | Authorized user can generate a non-mutating Arma 3 cloud dry-run plan for the selected game server and billing owner. | Use existing `CreateArma3DryRunPlanAsync` through generated Kiota and `CreateArma3CloudDryRunPlanResultAsync`; transform Phase 33 readiness payload into `Arma3CloudDryRunPlanRequestPayload`. [VERIFIED: .planning/REQUIREMENTS.md] [VERIFIED: sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs] |
| PLAN-02 | Authorized user can review planned resources, assumptions, firewall rules, validation failures, metadata freshness, and hourly or monthly estimates before provider mutation. | Add a typed review projection because current `CloudWorkflowActionResult.FromArma3DryRunPlan` only exposes compact metadata/cost/validation rows and does not render all Phase 35 review sections. [VERIFIED: .planning/REQUIREMENTS.md] [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs] [VERIFIED: 35-UI-SPEC.md] |
| PLAN-03 | Authorized user is blocked from provisioning stale, invalid, mismatched, superseded, or owner-mismatched cloud dry-run plans. | Keep `CloudProvisioningService` as authoritative blocker and add explicit supersession/input mismatch state before later provisioning UI consumes plans. [VERIFIED: .planning/REQUIREMENTS.md] [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] |
</phase_requirements>

## Project Constraints (from AGENTS.md)

- Keep the existing game-controller abstraction and do not replace local Arma 3 support. [VERIFIED: AGENTS.md]
- Lifecycle, job, worker, ownership, and audit state must survive API restarts. [VERIFIED: AGENTS.md]
- Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes. [VERIFIED: AGENTS.md]
- Steam credentials, provider tokens, worker credentials, worker hashes, authorization headers, bearer tokens, and raw secret-bearing payloads must not reach frontend clients or unredacted logs. [VERIFIED: AGENTS.md] [VERIFIED: .planning/REQUIREMENTS.md]
- Existing Aspire, PostgreSQL, API, and Blazor WebApp development must keep working. [VERIFIED: AGENTS.md]
- Use C# file-scoped namespaces, one public type per file, DTO/payload suffix conventions, generated Kiota clients behind repository facades, and `.editorconfig` formatting. [VERIFIED: AGENTS.md]
- Treat `sources/WebApp/UtfApi/**` and `sources/Api/Data/Migrations/**` as generated; do not hand-edit generated Kiota files except as regeneration output. [VERIFIED: AGENTS.md]
- Use `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` and focused tests as primary verification. [VERIFIED: AGENTS.md]
- Do not make direct repo edits outside GSD workflow unless explicitly requested; this research task explicitly requests writing only the research artifact. [VERIFIED: AGENTS.md] [VERIFIED: user prompt]

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core | 9.0.4 in project references | Authenticated API controllers and OpenAPI-generation surface. [VERIFIED: AGENTS.md] | Existing API stack; Phase 35 endpoints should follow controller/DTO conventions. [VERIFIED: AGENTS.md] |
| Entity Framework Core + Npgsql | 9.0.4 | Persist dry-run review state, migrations, sessions, resources, estimates, and owner snapshots. [VERIFIED: AGENTS.md] | Existing persistence stack for `CloudDryRunPlan`, `CloudGameServerSession`, `CloudResourceInventoryRecord`, and `CloudCostEstimate`. [VERIFIED: codebase grep] |
| Microsoft Kiota | 1.17.2 | Generated WebApp client request builders and DTOs. [VERIFIED: AGENTS.md] | Existing WebApp pattern requires generated clients behind repositories. [VERIFIED: AGENTS.md] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Blazor WebAssembly + Fluent UI Blazor | Blazor 9.0.4, Fluent UI 4.11.8 | Owner-facing `/servers` dry-run review panel. [VERIFIED: AGENTS.md] [VERIFIED: 35-UI-SPEC.md] | Existing UI surface and approved design contract use Fluent components. [VERIFIED: 35-UI-SPEC.md] |
| xUnit + EF Core SQLite | xUnit 2.9.3, SQLite package 9.0.4 | Focused API/service/source-boundary tests. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] | Current cloud planning/provisioning tests use SQLite in-memory contexts and source scans. [VERIFIED: tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Existing `CloudWorkflowModels` / `CloudWorkflowRedactionGuard` | repo-local | Repository-safe result taxonomy and forbidden display checks. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs] | Extend or mirror for dry-run review projections before binding anything in Razor. [VERIFIED: 35-UI-SPEC.md] |
| Existing `IBillingOwnershipService` | repo-local | Active owner validation and owner mismatch checks. [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs] | Use for plan generation and provisioning blocker validation. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] |
| Existing `IHetznerMetadataSyncService` | repo-local | Cached metadata status and stale threshold. [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs] | Use to label freshness and decide regeneration need; do not live-sync provider metadata in Phase 35. [VERIFIED: 35-CONTEXT.md] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New packages | None | No new external dependencies are needed or allowed by the user prompt; use the existing stack. [VERIFIED: user prompt] |
| Raw browser HTTP calls | `HttpClient` from Razor | Explicitly forbidden by Phase 34 and tests; repository facade must own generated-client calls. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Rendering `CloudDryRunPlanDto.ValidationDetailsJson` / `PlannedOperationsJson` | Raw JSON display | Forbidden by UI spec and redaction tests; parse/project typed safe fields instead. [VERIFIED: 35-UI-SPEC.md] [VERIFIED: sources/Api/DTO/CloudDryRunPlanDto.cs] |

**Installation:** no package installation. [VERIFIED: user prompt]

## Package Legitimacy Audit

Not applicable: Phase 35 research recommends no external packages and no package-manager installs. [VERIFIED: user prompt]

## Architecture Patterns

### System Architecture Diagram

```text
/servers guided inputs
  -> WebApp repository facade
  -> generated Kiota GameServers/{id}/CloudDryRunPlans/Arma3
  -> CloudPlanningController
  -> Arma3CloudDryRunPlannerService
  -> CloudInventoryService + CloudCostEstimateService
  -> PostgreSQL persisted session/resources/plan/estimate/audit
  -> owner-safe review endpoint/projection
  -> WebApp repository review model + redaction guard
  -> /servers dry-run plan review panel

Future provisioning attempt
  -> CloudProvisioningService.ValidateAsync
  -> rejects non-valid, stale metadata, owner mismatch, snapshot mismatch, consumed/superseded states
  -> provider client is not called unless validation passes in later provisioning phase
```

### Recommended Project Structure

```text
sources/Api/DTO/
  Arma3CloudDryRunPlanReviewDto.cs        # owner-safe review projection
  Arma3CloudDryRunPlanReviewStateDto.cs   # optional state details if kept separate
sources/Api/Services/CloudPlanning/
  Arma3CloudDryRunPlanReviewService.cs    # latest/current/superseded/mismatch projection
sources/Api/Data/Hosting/
  CloudDryRunPlan.cs                      # add durable review fields if needed
sources/Api/Data/Migrations/
  <timestamp>_AddCloudDryRunPlanReviewState.cs
sources/WebApp/Services/GameServerRepository/CloudWorkflow/
  CloudDryRunPlanReviewModels.cs          # repository-safe typed review models
  CloudWorkflowResultMapper.cs            # map generated review DTOs into safe projection
sources/WebApp/Pages/Servers/
  Index.razor                             # render approved Phase 35 review panel
  Index.razor.css                         # use UI-SPEC classes and spacing
tests/Api.Tests/CloudPlanning/
  Arma3CloudPlanReviewTests.cs            # review state, supersession, mismatch tests
tests/Api.Tests/WebApp/
  CloudDryRunPlanReviewBoundaryTests.cs   # repository/UI redaction and generated-client boundary
```

### Pattern 1: Keep Dry-Run Generation Providerless

**What:** The planner service may read cached provider configuration/metadata, persist local control-plane records, and create cost estimates, but it must not call `IHetznerCloudClient`, `IHttpClientFactory`, or `HttpClient`. [VERIFIED: tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs]  
**When to use:** Every Phase 35 generation/regeneration path. [VERIFIED: 35-CONTEXT.md]  
**Example:**

```csharp
// Source: tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs
var constructorParameterTypes = typeof(Arma3CloudDryRunPlannerService)
    .GetConstructors()
    .SelectMany(q => q.GetParameters())
    .Select(q => q.ParameterType)
    .ToArray();

Assert.DoesNotContain(typeof(IHetznerCloudClient), constructorParameterTypes);
Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameterTypes);
Assert.DoesNotContain(typeof(HttpClient), constructorParameterTypes);
```

### Pattern 2: Server-Side Review State Is Authoritative

**What:** Persist or compute review state server-side using plan status, latest plan id, active owner assignment, metadata source timestamp, session/resource snapshot consistency, and input fingerprint/snapshot. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] [VERIFIED: sources/Api/Data/Hosting/CloudDryRunPlan.cs]  
**When to use:** Plan review, regeneration prompts, and later provisioning gates. [VERIFIED: .planning/REQUIREMENTS.md]  
**Planner note:** Add either durable `InputSnapshotHash`/`SupersededByPlanFk`/`TimeStampSuperseded` fields or an equivalent normalized review-state table; current schema lacks these fields. [VERIFIED: codebase grep]

### Pattern 3: Repository-Safe Projection Before Razor

**What:** Convert generated DTOs to hand-written WebApp review models that contain only allowed fields; use `CloudWorkflowRedactionGuard` for all display strings. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs]  
**When to use:** `/servers` plan review sections for resources, firewall rules, assumptions, validation failures, estimates, and next step labels. [VERIFIED: 35-UI-SPEC.md]  
**Example:**

```csharp
// Source: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
private static string Safe(string? value, string fieldName)
    => CloudWorkflowRedactionGuard.ValidateDisplayText(value, fieldName) ?? string.Empty;
```

### Anti-Patterns to Avoid

- **Adding a provisioning button in Phase 35:** UI spec says provider mutation is hidden or disabled and no provider mutation command is introduced. [VERIFIED: 35-UI-SPEC.md]
- **Treating UI disabled state as the gate:** provisioning must remain blocked by server-side validation, not only by Razor logic. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs]
- **Binding generated DTOs or raw JSON directly in Razor:** generated DTOs implement `IAdditionalDataHolder`, and raw plan fields include `ValidationDetailsJson` and `PlannedOperationsJson`. [VERIFIED: sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanDto.cs] [VERIFIED: sources/Api/DTO/CloudDryRunPlanDto.cs]
- **Creating a parallel `/servers/cloud` owner workflow:** Phase 35 owner review belongs in `/servers`; `/servers/cloud` remains admin/provider/fleet. [VERIFIED: 35-CONTEXT.md]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| API auth and resource scope | Custom user/server permission checks in Razor | Existing `[Authorize]` policies and server-side controller checks | Current controllers use resource policies such as `Claims.ResourceBased.Server.Configuration` and `Server.All`. [VERIFIED: sources/Api/Controllers/CloudPlanningController.cs] [VERIFIED: sources/Api/Controllers/GameServersController.cs] |
| Provider metadata freshness | Live provider calls during review | `IHetznerMetadataSyncService.GetStatusAsync` and persisted `MetadataSourceTimestamp` | Phase 35 is non-mutating and existing planner uses cached metadata. [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs] |
| Cost estimate math | New estimate calculator in WebApp | Existing `CloudCostEstimateService` and returned estimate DTO | Existing service calculates hourly/monthly/storage/IP/firewall/additional costs and freshness. [VERIFIED: sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs] |
| Redaction scanning | Ad hoc string filtering in components | `CloudWorkflowRedactionGuard` plus boundary tests | Phase 34 established the forbidden field/display-term guard and tests. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Generated API calls | Raw `HttpClient` in `/servers` | Kiota request builders inside `GameServerRepositoryImpl` | Existing boundary test fails Razor/raw HTTP usage. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |

**Key insight:** Phase 35 should make reviewed plan state explicit and durable enough for later provisioning, but provider mutation remains in future phases and must stay unreachable from the UI. [VERIFIED: 35-CONTEXT.md] [VERIFIED: 35-UI-SPEC.md]

## Common Pitfalls

### Pitfall 1: Valid Does Not Mean Current
**What goes wrong:** A persisted `Valid` plan is treated as provisionable after inputs, metadata, or ownership changed. [VERIFIED: sources/Contract/GameServer/ECloudDryRunPlanStatus.cs]  
**Why it happens:** Current status enum has `Superseded`, but dry-run creation does not mark older plans superseded and the entity has no input fingerprint field. [VERIFIED: codebase grep]  
**How to avoid:** Mark earlier unconsumed plans for the same game server/owner/session as `Superseded` when a new plan is generated, and compute review-state mismatch from normalized current inputs and active owner. [VERIFIED: .planning/REQUIREMENTS.md]  
**Warning signs:** Multiple `Valid` plans for one active guided server with no `Superseded` transition. [VERIFIED: codebase grep]

### Pitfall 2: Raw JSON Seems Convenient
**What goes wrong:** UI renders `ValidationDetailsJson`, `PlannedOperationsJson`, `DetailsJson`, or generated `AdditionalData`. [VERIFIED: sources/Api/DTO/CloudDryRunPlanDto.cs] [VERIFIED: sources/WebApp/UtfApi/Models/CloudDryRunPlanDto.cs]  
**Why it happens:** Existing admin DTOs expose raw JSON for persistence/debug projection. [VERIFIED: sources/Api/DTO/CloudDryRunPlanDto.cs]  
**How to avoid:** Parse at the API or repository boundary into typed `ValidationFailures`, `FirewallRules`, `Assumptions`, and resource rows. [VERIFIED: 35-UI-SPEC.md]  
**Warning signs:** Razor contains `Json`, `AdditionalData`, `CloudDryRunPlanDto`, or generated `UtfApi` imports in plan review markup. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs]

### Pitfall 3: Cost Source Ambiguity
**What goes wrong:** Dry-run estimates appear as reconciled provider billing. [VERIFIED: 35-CONTEXT.md]  
**Why it happens:** Existing `CloudWorkflowCostSummary` has generic cost fields such as accumulated/reconciled/adjusted values. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs]  
**How to avoid:** Label Phase 35 values as `Dry-run estimate`, include freshness `Fresh`/`Stale`/`Unknown`, and keep hourly/monthly amounts close to source/freshness labels. [VERIFIED: 35-UI-SPEC.md]  
**Warning signs:** UI copy says reconciled, adjusted, invoice, payment, checkout, charge, or customer billing. [VERIFIED: 35-UI-SPEC.md]

### Pitfall 4: Provisioning Gate Drift
**What goes wrong:** UI allows continuing from stale/superseded/owner-mismatched plans before backend supports the same rule. [VERIFIED: .planning/REQUIREMENTS.md]  
**Why it happens:** Phase 34 continuation infrastructure already has provisioning action types, but Phase 35 must not enable provider mutation. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor]  
**How to avoid:** Keep future provisioning disabled in Phase 35 and add backend tests proving rejected plans do not call the provider client. [VERIFIED: 35-UI-SPEC.md] [VERIFIED: tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs]  
**Warning signs:** `CanContinueSelectedCloudWorkflow` becomes true for dry-run provisioning in Phase 35 UI. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor]

## Code Examples

### Existing Generated-Client Repository Call

```csharp
// Source: sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
var requestInfo = Client.GameServers[gameServerId]
    .CloudDryRunPlans
    .Arma3
    .ToPostRequestInformation(payload);
```

### Existing Provisioning Blocker Shape

```csharp
// Source: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs
if (context.Plan.Status != ECloudDryRunPlanStatus.Valid)
    throw new InvalidOperationException("Only valid, unused dry-run plans can be provisioned.");
```

### Existing Metadata Staleness Check

```csharp
// Source: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs
if ((metadata.SourceTimestamp.Value - context.Plan.MetadataSourceTimestamp.Value).Duration()
    > TimeSpan.FromSeconds(1))
{
    throw new InvalidOperationException("Dry-run plan metadata snapshot is stale.");
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Phase 33 readiness only | Guided `/servers` flow validates owner, selected server, and planning inputs without creating dry-run plans. | Phase 33 | Phase 35 should transform readiness payloads into dry-run request payloads. [VERIFIED: .planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-02-SUMMARY.md] |
| Raw WebApp HTTP for some workflow actions | Phase 34 cloud workflow result methods use generated Kiota request information and repository projections. | Phase 34 | Phase 35 should add review methods through the repository, not Razor HTTP. [VERIFIED: .planning/phases/34-generated-client-and-repository-gate-foundation/34-03-SUMMARY.md] |
| Admin inventory details only | Owner `/servers` workflow needs selected dry-run review. | Phase 35 scope | Add owner-safe plan review endpoint/projection under game server route. [VERIFIED: 35-CONTEXT.md] |

**Deprecated/outdated:**
- Treating `ValidationDetailsJson`, `PlannedOperationsJson`, or `DetailsJson` as UI display data is outdated for owner workflow review. [VERIFIED: 35-UI-SPEC.md]
- Claiming browser/authenticated/live Hetzner/Steam/Arma UAT is explicitly forbidden in this phase. [VERIFIED: user prompt] [VERIFIED: 35-CONTEXT.md]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|

All implementation recommendations above are grounded in required planning docs or codebase inspection; no external ecosystem claims were needed. [VERIFIED: user prompt]

## Open Questions (RESOLVED)

1. **Should input mismatch be persisted as a hash or computed from a stored normalized snapshot?** - RESOLVED: persist a normalized, non-secret input fingerprint plus client-safe summary fields; do not persist raw JSON input dumps.
   - What we know: current plan records have no input snapshot/fingerprint, while requirements need mismatched-plan blocking. [VERIFIED: sources/Api/Data/Hosting/CloudDryRunPlan.cs] [VERIFIED: .planning/REQUIREMENTS.md]
   - What's unclear: the preferred durable representation is not locked. [VERIFIED: 35-CONTEXT.md]
   - Recommendation: persist a normalized, non-secret input fingerprint plus client-safe summary fields; do not persist raw JSON input dumps. [VERIFIED: 35-UI-SPEC.md]

2. **Should existing valid plans be backfilled as current or left as historical?** - RESOLVED: add nullable fields and handle legacy nulls in the latest-plan projection; supersession only needs to be enforced for new plans.
   - What we know: current migrations already include dry-run plans, sessions, resources, and estimates. [VERIFIED: sources/Api/Data/Migrations]
   - What's unclear: no operator decision says historical plans must be migrated into a current review state. [VERIFIED: 35-CONTEXT.md]
   - Recommendation: migration should add nullable fields and compute supersession only for new plans; planner may add a service-level latest-plan projection that handles null legacy fields. [VERIFIED: codebase grep]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test | yes | 10.0.201 installed; projects target net9.0 | Use installed SDK if it supports net9 targeting; otherwise install .NET 9 SDK. [VERIFIED: dotnet --version] [VERIFIED: AGENTS.md] |
| PostgreSQL | local Aspire/API runtime | not probed for research | required by runtime config | SQLite in-memory tests cover focused API/service behavior. [VERIFIED: AGENTS.md] [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Network/package registry | external package discovery | restricted | not used | No package installs recommended. [VERIFIED: user prompt] |

**Missing dependencies with no fallback:** none for research and planning. [VERIFIED: user prompt]

**Missing dependencies with fallback:** PostgreSQL was not probed; focused tests use SQLite, while full local runtime still needs PostgreSQL/Aspire. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] [VERIFIED: AGENTS.md]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 and EF Core SQLite 9.0.4. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Config file | `tests/Api.Tests/Api.Tests.csproj`. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Quick run command | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudProvisioning|FullyQualifiedName~WebApp" --verbosity minimal` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PLAN-01 | Generate dry-run plan from guided readiness without provider client/HTTP dependency. | unit/API | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Arma3CloudPlannerTests|FullyQualifiedName~Arma3CloudApiTests" --verbosity minimal` | Existing plus Phase 35 additions. [VERIFIED: tests/Api.Tests/CloudPlanning] |
| PLAN-02 | Review projection includes resources, assumptions, firewall rules, validation failures, freshness, hourly/monthly estimates, and cost source labels without raw JSON. | unit/source boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudDryRunPlanReview" --verbosity minimal` | Wave 0 gap. [VERIFIED: 35-UI-SPEC.md] |
| PLAN-03 | Provisioning blocked for stale, invalid, mismatched, superseded, and owner-mismatched plans with no provider client calls. | unit | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudProvisioningServiceTests" --verbosity minimal` | Partial existing; add superseded/input mismatch coverage. [VERIFIED: tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs] |
| COST-03 carryover | UI/repository projections do not expose forbidden fields or live-UAT claims. | source boundary | `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Existing; extend for review models. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |

### Sampling Rate

- **Per task commit:** run the focused command for touched area: planning service/API, provisioning blocker, repository boundary, or Razor source scan. [VERIFIED: tests/Api.Tests/Api.Tests.csproj]
- **Per wave merge:** `dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~CloudProvisioning|FullyQualifiedName~WebApp" --verbosity minimal`. [VERIFIED: tests/Api.Tests]
- **Phase gate:** `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` plus focused API/source-boundary tests; do not claim manual/live UAT. [VERIFIED: AGENTS.md] [VERIFIED: user prompt]

### Wave 0 Gaps

- [ ] `tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewTests.cs` — owner-safe latest review, stale/superseded/mismatch state, cost labels, typed firewall/assumption/validation projection. [VERIFIED: 35-UI-SPEC.md]
- [ ] `tests/Api.Tests/CloudPlanning/Arma3CloudPlanSupersessionTests.cs` or equivalent additions — new plan marks older unconsumed plan superseded and leaves provisioned/failed history intact. [VERIFIED: sources/Contract/GameServer/ECloudDryRunPlanStatus.cs]
- [ ] `tests/Api.Tests/WebApp/CloudDryRunPlanReviewBoundaryTests.cs` — review models/Razor do not expose `ValidationDetailsJson`, `PlannedOperationsJson`, `DetailsJson`, `AdditionalData`, tokens, credentials, worker hashes, or live-UAT claims. [VERIFIED: 35-UI-SPEC.md]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes | Existing ASP.NET Core `[Authorize]` and database-user lookup. [VERIFIED: sources/Api/Controllers/CloudPlanningController.cs] |
| V3 Session Management | yes | Existing authenticated API/WebApp cookie flow; Phase 35 adds no session mechanism. [VERIFIED: AGENTS.md] |
| V4 Access Control | yes | Resource-based server policies and owner-scoped billing account filtering. [VERIFIED: sources/Api/Controllers/GameServersController.cs] |
| V5 Input Validation | yes | Existing readiness and planner validation for owner, metadata, ports, storage, provider metadata, and controller identifier. [VERIFIED: sources/Api/Controllers/GameServersController.cs] [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs] |
| V6 Cryptography | no new crypto | Do not create credential hashing or token storage in Phase 35; avoid displaying secret references. [VERIFIED: 35-UI-SPEC.md] |

### Known Threat Patterns for Phase 35

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stale/superseded plan replay | Tampering | Server-side validation in provisioning plus durable supersession/input-match state. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] |
| Owner mismatch after transfer | Elevation of Privilege | Compare plan owner snapshot with active `GameServerBillingAssignment` before provisioning and during review. [VERIFIED: sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs] |
| Secret disclosure through raw DTO/JSON | Information Disclosure | Allowlisted repository projection and `CloudWorkflowRedactionGuard`; no raw JSON in Razor. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] |
| Misleading live evidence claims | Repudiation / Information Disclosure | Tests/source scans should preserve deferred UAT wording and avoid live-provider success claims. [VERIFIED: user prompt] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |

## Sources

### Primary (HIGH confidence)

- `.planning/ROADMAP.md` — Phase 35 goal, boundaries, success criteria, dependency on Phase 34. [VERIFIED: required reading]
- `.planning/REQUIREMENTS.md` — PLAN-01, PLAN-02, PLAN-03, COST-03, out-of-scope boundaries. [VERIFIED: required reading]
- `.planning/phases/35-dry-run-planning-and-review/35-CONTEXT.md` — locked Phase 35 constraints. [VERIFIED: required reading]
- `.planning/phases/35-dry-run-planning-and-review/35-UI-SPEC.md` — approved UI and redaction contract. [VERIFIED: required reading]
- `sources/Api/Controllers/CloudPlanningController.cs` — dry-run route and authorization. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` — dry-run validation and persistence behavior. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` — provider-mutation blockers. [VERIFIED: codebase grep]
- `sources/WebApp/Services/GameServerRepository/**` and `sources/WebApp/Pages/Servers/Index.razor` — repository/UI integration. [VERIFIED: codebase grep]
- `tests/Api.Tests/**` — focused cloud planning/provisioning/repository boundary tests. [VERIFIED: codebase grep]

### Secondary (MEDIUM confidence)

- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/*-SUMMARY.md` and `.planning/phases/34-generated-client-and-repository-gate-foundation/*-SUMMARY.md` — phase pattern history and implementation decisions. [VERIFIED: codebase grep]

### Tertiary (LOW confidence)

- None; network/external package research was intentionally skipped by operator instruction. [VERIFIED: user prompt]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — derived from AGENTS.md, csproj files, and current code. [VERIFIED: AGENTS.md] [VERIFIED: tests/Api.Tests/Api.Tests.csproj]
- Architecture: HIGH — derived from current controllers/services/entities/repositories/UI. [VERIFIED: codebase grep]
- Pitfalls: HIGH — derived from current missing fields, existing provisioning blockers, and approved UI/redaction contract. [VERIFIED: codebase grep] [VERIFIED: 35-UI-SPEC.md]

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 for codebase-local planning, or sooner if Phase 35/36 implementation changes dry-run/provisioning contracts. [VERIFIED: system date]

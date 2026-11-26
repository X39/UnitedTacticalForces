# Phase 34: Generated Client And Repository Gate Foundation - Pattern Map

**Mapped:** 2026-06-19
**Files analyzed:** 8 target areas
**Analogs found:** 8 / 8

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | service contract | request-response | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | service facade | request-response/transform | `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/*CloudWorkflowResult*.cs` or similar | local projection model | transform | `sources/WebApp/Services/CloudAdminRepository/CloudAdminModels.cs` | exact |
| `sources/WebApp/Services/CloudAdminRepository/CloudAdminRepositoryImpl.cs` | generated-client adapter | request-response/transform | `sources/WebApp/Services/CloudAdminRepository/CloudAdminRepositoryImpl.cs` | exact |
| `sources/WebApp/Pages/Servers/Index.razor` | component/page | event-driven request-response rendering | `sources/WebApp/Pages/Servers/Index.razor` | exact |
| `sources/WebApp/Pages/Servers/Index.razor.css` | component CSS | UI layout | `sources/WebApp/Pages/Servers/Index.razor.css` | exact |
| `sources/Api/Controllers/GameServersController.cs` and cloud controllers | controller | request-response | `sources/Api/Controllers/GameServersController.cs`, `CloudInventoryController.cs`, `CloudSpendingPoliciesController.cs` | exact |
| `tests/Api.Tests/**` or future WebApp/repository tests | test | transform/request-response | `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`, `CloudSpendingPolicyVisibilityTests.cs`, `CloudRuntimeCostSnapshotServiceTests.cs` | role-match |

## Pattern Assignments

### WebApp Repository Contract: `IGameServerRepository.cs`

**Analog:** `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`

**Use this pattern for owner-facing cloud workflow facade methods** (lines 31-48, 82-110):
```csharp
Task<GuidedCloudEntryOptionsDto> GetGuidedCloudEntryOptionsAsync(CancellationToken cancellationToken = default);

Task<GameServerBillingAssignmentDto> AssignGuidedCloudEntryBillingOwnerAsync(
    long gameServerId,
    GuidedCloudEntryBillingOwnerSelectionPayload payload,
    CancellationToken cancellationToken = default
);

Task<CloudAdminModels.CloudCostVisibility?> GetCloudCostVisibilityAsync(
    long gameServerId,
    CancellationToken cancellationToken = default
);

Task<CloudAdminModels.CloudSpendingPolicyVisibility?> GetCloudPolicyVisibilityAsync(
    long gameServerId,
    CancellationToken cancellationToken = default
);
```

Planner guidance: add typed repository methods here first; Razor pages should not inject generated request builders or use raw browser HTTP bridges.

### WebApp Repository Implementation: `GameServerRepositoryImpl.cs`

**Analog:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`

**Imports and DI pattern** (lines 1-14):
```csharp
using Microsoft.Kiota.Abstractions;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.UnitedTacticalForces.WebApp.Services.CloudAdminRepository;
using X39.Util.DependencyInjection.Attributes;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl) : RepositoryBase(httpClientFactory, baseUrl),
    IGameServerRepository
```

**Generated Kiota usage pattern** (lines 86-136):
```csharp
var options = await Client.GameServers
    .CloudEntry
    .Options
    .GetAsync(cancellationToken: cancellationToken)
    .ConfigureAwait(false);
return options ?? throw new InvalidOperationException("Failed to get guided cloud entry options.");

var readiness = await Client.GameServers[gameServerId]
    .CloudEntry
    .Readiness
    .PostAsync(payload, cancellationToken: cancellationToken)
    .ConfigureAwait(false);
return readiness ?? throw new InvalidOperationException("Failed to validate guided cloud entry readiness.");
```

**NotFound-to-null and projection pattern** (lines 273-323):
```csharp
try
{
    var visibility = await Client.GameServers[gameServerId]
        .CloudCostVisibility
        .GetAsync(cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    return visibility is null ? null : CloudAdminRepositoryImpl.MapCostVisibility(visibility);
}
catch (ApiException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
{
    return null;
}
```

Planner guidance: Phase 34 should follow this facade pattern, but new user-visible result methods should return local allowlisted result/projection types rather than raw generated DTOs when DTOs contain raw JSON, `AdditionalData`, or secret-bearing fields.

### Local Projection Models: `CloudAdminModels.cs`

**Analog:** `sources/WebApp/Services/CloudAdminRepository/CloudAdminModels.cs`

**Projection shape pattern** (lines 229-265):
```csharp
public record CloudCostVisibility
{
    public IReadOnlyList<CloudSessionCostVisibility> Sessions { get; init; } = [];
    public CloudReconciliationVisibilitySummary Reconciliation { get; init; } = new();
    public CloudBillingReconciliationSummary BillingReconciliation { get; init; } = new();
    public decimal CurrentEstimatedCost { get; init; }
    public decimal AccumulatedEstimatedCost { get; init; }
    public decimal AccumulatedProviderAllocatedCost { get; init; }
    public decimal AccumulatedAdjustedCost { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int ActiveBillableResourceCount { get; init; }
    public int CleanupNeededBillableResourceCount { get; init; }
    public bool HasCostSnapshots { get; init; }
    public bool HasCleanupRisk { get; init; }
    public DateTimeOffset TimeStampProjected { get; init; }
}
```

**Policy continuation identity pattern** (lines 327-390):
```csharp
public record CloudSpendingPolicyDecision
{
    public long PrimaryKey { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public string ProviderIdentifier { get; init; } = string.Empty;
    public ECloudBillableActionKind ActionKind { get; init; }
    public ECloudSpendingPolicyDecision Decision { get; init; }
    public long? GameServerFk { get; init; }
    public long? SessionFk { get; init; }
    public long? BillingAccountFk { get; init; }
    public long? BillingAssignmentFk { get; init; }
    public decimal ProjectedTotalCost { get; init; }
    public string DecisionReasonCode { get; init; } = string.Empty;
    public string? SanitizedFailureSummary { get; init; }
    public string? SanitizedFailureDetails { get; init; }
}

public record CloudSpendingPolicyApprovalRequest
{
    public long PrimaryKey { get; init; }
    public ECloudSpendingPolicyApprovalStatus Status { get; init; }
    public long DecisionFk { get; init; }
    public long? DryRunPlanFk { get; init; }
    public DateTimeOffset TimeStampExpires { get; init; }
}
```

Planner warning: `CloudSpendingPolicyDecision` currently includes `InputSnapshotJson` and `RiskDetailsJson` in lines 354-355. Phase 34 user-facing projections must not bind or render those raw JSON fields. Create smaller result models if needed.

**Provisioning/teardown result pattern** (lines 785-802):
```csharp
public record CloudProvisioningResult
{
    public CloudDryRunPlan Plan { get; init; } = new();
    public CloudGameServerSession? Session { get; init; }
    public IReadOnlyList<CloudResourceInventoryRecord> Resources { get; init; } = [];
    public string? PublicEndpointHost { get; init; }
    public int? PublicEndpointPort { get; init; }
}

public record CloudTeardownResult
{
    public CloudGameServerSession Session { get; init; } = new();
    public IReadOnlyList<CloudResourceInventoryRecord> Resources { get; init; } = [];
    public int DeletedResourceCount { get; init; }
    public int RemainingBillableResourceCount { get; init; }
    public bool CleanupFailed { get; init; }
    public string? FailureSummary { get; init; }
}
```

### Generated Client Adapter: `CloudAdminRepositoryImpl.cs`

**Analog:** `sources/WebApp/Services/CloudAdminRepository/CloudAdminRepositoryImpl.cs`

**Generated client calls behind facade** (lines 56-83):
```csharp
var sessions = await Client.CloudInventory
    .Sessions
    .GetAsync(cancellationToken: cancellationToken)
    .ConfigureAwait(false);
return MapCollection(sessions, Map);

var plans = await Client.CloudInventory
    .DryRunPlans
    .GetAsync(cancellationToken: cancellationToken)
    .ConfigureAwait(false);
return MapCollection(plans, Map);
```

**Allowlisted mapping from generated DTOs** (lines 646-688):
```csharp
private static CloudAdminModels.CloudCostVisibility Map(ApiModels.CloudCostVisibilityDto value)
    => new()
    {
        Sessions                           = MapCollection(value.Sessions, Map),
        Reconciliation                     = value.Reconciliation is null ? new() : Map(value.Reconciliation),
        BillingReconciliation              = MapBillingReconciliation(value.AdditionalData, "billingReconciliation"),
        CurrentEstimatedCost               = ToDecimal(value.CurrentEstimatedCost),
        AccumulatedEstimatedCost           = ToDecimal(value.AccumulatedEstimatedCost),
        AccumulatedProviderAllocatedCost    =
            GetAdditionalDecimal(value.AdditionalData, "accumulatedProviderAllocatedCost"),
        AccumulatedAdjustedCost            = GetAdditionalDecimal(value.AdditionalData, "accumulatedAdjustedCost"),
        Currency                           = value.Currency ?? string.Empty,
        ActiveBillableResourceCount        = value.ActiveBillableResourceCount.GetValueOrDefault(),
        CleanupNeededBillableResourceCount = value.CleanupNeededBillableResourceCount.GetValueOrDefault(),
        HasCostSnapshots                   = value.HasCostSnapshots.GetValueOrDefault(),
        HasCleanupRisk                     = value.HasCleanupRisk.GetValueOrDefault(),
        TimeStampProjected                 = value.TimeStampProjected.GetValueOrDefault(),
    };
```

**Internal map reuse from owner repository** (lines 1323-1328):
```csharp
internal static CloudAdminModels.CloudCostVisibility MapCostVisibility(ApiModels.CloudCostVisibilityDto value)
    => Map(value);

internal static CloudAdminModels.CloudSpendingPolicyVisibility MapPolicyVisibility(
    ApiModels.CloudSpendingPolicyVisibilityDto value)
    => Map(value);
```

Planner guidance: generated `sources/WebApp/UtfApi/**` files contain route builders and DTO names such as `CloudProvisioningResultDto`, `CloudTeardownResultDto`, `CloudSpendingPolicyGateResultDto`, `CloudRuntimeStatusDto`, `CloudCostVisibilityDto`, `CloudSpendingPolicyVisibilityDto`, `HostingAuditRecordDto`, and `CloudWorkerBootstrapCredentialDto`. Consume them through repository adapters only; do not hand-edit generated files.

### API Controller Result and Policy Gate Patterns

**Analogs:** `sources/Api/Controllers/GameServersController.cs`, `CloudInventoryController.cs`, `CloudSpendingPoliciesController.cs`

**Server-scoped cost and policy visibility endpoints** (`GameServersController.cs` lines 411-448):
```csharp
[ProducesResponseType<CloudCostVisibilityDto>(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[HttpGet("{gameServerId:long}/cloud-cost-visibility", Name = nameof(GetCloudCostVisibilityAsync))]
[Authorize($"{Claims.ResourceBased.Server.All}:*")]
public async Task<IActionResult> GetCloudCostVisibilityAsync(...)
{
    var visibility = await _cloudCostVisibilityService
        .GetGameServerCostVisibilityAsync(gameServerId, cancellationToken)
        .ConfigureAwait(false);
    return visibility is null
        ? NotFound()
        : Ok(visibility);
}
```

**Policy-gated action pattern** (`CloudInventoryController.cs` lines 82-115):
```csharp
[ProducesResponseType<CloudProvisioningResultDto>(StatusCodes.Status200OK)]
[ProducesResponseType<CloudSpendingPolicyGateResultDto>(StatusCodes.Status409Conflict)]
[HttpPost("dry-run-plans/{planId:long}/provision", Name = nameof(ProvisionDryRunPlanAsync))]
public async Task<IActionResult> ProvisionDryRunPlanAsync(...)
{
    var gate = await cloudSpendingPolicyGateService
        .EvaluateProvisioningAsync(planId, user, cancellationToken, acknowledgedDecisionId, approvalRequestId)
        .ConfigureAwait(false);
    if (!gate.CanProceed)
        return Conflict(gate.ToDto());

    var result = await cloudProvisioningService
        .ProvisionDryRunPlanAsync(planId, user, cancellationToken)
        .ConfigureAwait(false);
```

**Policy approval command pattern** (`CloudSpendingPoliciesController.cs` lines 259-336):
```csharp
[ProducesResponseType<CloudSpendingPolicyApprovalRequestDto>(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[HttpPost("approvals/{approvalRequestId:long}/approve", Name = nameof(ApproveApprovalRequestAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> ApproveApprovalRequestAsync(...)
{
    var user = await GetActorAsync(cancellationToken).ConfigureAwait(false);
    if (user is null)
        return Unauthorized();
```

Planner guidance: Phase 34 should preserve decision id, approval request id, action kind, scope/session/plan/owner ids, expiration, and sanitized summaries. It must not expose raw policy input snapshots or provider/worker credentials in WebApp result models.

### `/servers` UI Result Rendering

**Analog:** `sources/WebApp/Pages/Servers/Index.razor`

**Inject repository facades, not generated clients** (lines 15-26):
```razor
@using X39.UnitedTacticalForces.WebApp.Services.CloudAdminRepository
@using X39.UnitedTacticalForces.WebApp.Services.GameServerRepository
@inject IGameServerRepository GameServerRepository
@inject ILifecycleWorkerRepository LifecycleWorkerRepository
@inject IModPackRepository ModPackRepository
```

**Render structured result summaries via `detail-section` and `meta-grid`** (lines 575-654):
```razor
<section class="detail-section">
    <div class="section-title">Cloud runtime</div>
    <dl class="meta-grid">
        <dt>Readiness</dt>
        <dd><span class="@GetRuntimeReadyClass(cloudRuntime)">@FormatCloudRuntimeReadiness(cloudRuntime)</span></dd>
        <dt>Estimated runtime cost</dt>
        <dd>@FormatMoney(_selectedCostVisibility?.CurrentEstimatedCost, _selectedCostVisibility?.Currency)</dd>
        <dt>Policy decision</dt>
        <dd>
            <span class="@GetPolicyDecisionClass(selectedPolicyRow?.LatestDecision)">
                @FormatPolicyDecision(selectedPolicyRow?.LatestDecision)
            </span>
        </dd>
        <dt>Policy next step</dt>
        <dd>@FormatPolicyNextStep(selectedPolicyRow)</dd>
    </dl>
    <p class="risk-note">Provider teardown stops billable resources; Runtime cleanup does not delete provider resources.</p>
</section>
```

**Policy formatting helpers** (lines 2124-2193):
```csharp
private static string FormatPolicyDecision(CloudAdminModels.CloudSpendingPolicyDecision? decision)
    => decision is null
        ? "No decision"
        : $"{FormatPolicyDecisionResult(decision.Decision)} {FormatMoney(decision.ProjectedTotalCost, decision.Currency)}";

private static string FormatPolicyApproval(CloudAdminModels.CloudSpendingPolicyVisibilityRow? row)
{
    if (row is null)
        return "No policy visibility";
    var approval = row.PendingApproval ?? row.LatestApprovalRequest;
    if (approval is null)
        return row.HasPendingApproval ? "Pending approval" : "No request";
    return $"{FormatPolicyApprovalStatus(approval.Status)} #{approval.PrimaryKey}";
}
```

Planner guidance: add Phase 34 results below the grid/selected server detail using `FluentMessageBar`, `FluentProgress`, `dl.meta-grid`, and status badges. Never render raw JSON, `AdditionalData`, exception bodies, headers, or raw generated DTO dumps.

### `/servers` Route CSS

**Analog:** `sources/WebApp/Pages/Servers/Index.razor.css`

**Layout and panel pattern** (lines 1-41):
```css
.servers-page {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding: 16px;
}

.detail-section {
    border: 1px solid var(--neutral-stroke-rest);
    border-radius: 8px;
    overflow: auto;
    padding: 12px;
}
```

**Metadata grid and wrapping pattern** (lines 146-161):
```css
.meta-grid {
    display: grid;
    gap: 7px 12px;
    grid-template-columns: max-content minmax(0, 1fr);
    margin: 0;
}

.meta-grid dd {
    margin: 0;
    min-width: 0;
    overflow-wrap: anywhere;
}
```

**Status and responsive patterns** (lines 122-143, 366-390):
```css
.status-warning {
    background: color-mix(in srgb, goldenrod 18%, transparent);
    border-color: color-mix(in srgb, goldenrod 48%, transparent);
    color: color-mix(in srgb, goldenrod 70%, var(--neutral-foreground-rest));
}

@media (max-width: 860px) {
    .guided-cloud-grid,
    .details-grid,
    .form-grid,
    .secondary-form {
        grid-template-columns: 1fr;
    }
}
```

Planner guidance: Phase 34 UI spec calls out Phase 33 drift. When touching this CSS, normalize new/touched typography to px values from `34-UI-SPEC.md`, keep 4px spacing multiples, keep mobile touch targets, and avoid adding nested cards.

### Tests for Redaction, Projection, and Boundary Coverage

**Analogs:** `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`, `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyVisibilityTests.cs`, `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs`

**Serialized projection redaction assertion** (`CloudInventoryVisibilityTests.cs` lines 115-145):
```csharp
var serialized = JsonSerializer.Serialize(
    new
    {
        details,
        Plans = (await context.CloudInventoryService.GetDryRunPlansAsync(CancellationToken.None))
            .Select(q => q.ToDto())
            .ToArray(),
        Resources = (await context.CloudInventoryService.GetResourceInventoryRecordsAsync(CancellationToken.None))
            .Select(q => q.ToDto())
            .ToArray(),
    }
);
Assert.DoesNotContain("fake-token", serialized, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("secret", serialized, StringComparison.OrdinalIgnoreCase);
```

**Forbidden DTO term list pattern** (`CloudSpendingPolicyVisibilityTests.cs` lines 20-27):
```csharp
private static readonly string[] ForbiddenDtoTerms =
[
    "Password",
    "Secret",
    "Token",
    "Credential",
    "ApiKey",
];
```

**Sensitive word list pattern** (`CloudRuntimeCostSnapshotServiceTests.cs` lines 22-35):
```csharp
private static readonly string[] SensitiveWords =
[
    "token",
    "password",
    "secret",
    "authorization",
    "bearer",
    "api key",
    "apikey",
    "bootstrap",
    "credential",
    "user data",
];
```

Planner guidance: extend tests with repository-boundary or API projection checks that serialize the new result models and assert forbidden terms/values are absent. Good coverage targets include policy gate result mapping, provisioning/teardown result mapping, audit result projection, and `/servers` display model construction.

## Existing Cloud DTO/Model Names To Reuse Or Wrap

Generated client files under `sources/WebApp/UtfApi/**` are generated-code boundaries. Do not ask executors to hand-edit them. Relevant generated models/routes already exist:

| Area | Generated Models / Routes |
|------|---------------------------|
| Guided entry | `GuidedCloudEntryOptionsDto`, `GuidedCloudEntryReadinessDto`, `GuidedCloudEntryServerOptionDto`, `GameServers/CloudEntry/**` |
| Dry-run planning | `Arma3CloudDryRunPlanRequestPayload`, `Arma3CloudDryRunPlanDto`, `CloudDryRunPlanDto`, `CloudDryRunPlanDetailsDto`, `CloudInventory/DryRunPlans/**`, `GameServers/{id}/CloudDryRunPlans/**` |
| Policy | `CloudSpendingPolicyVisibilityDto`, `CloudSpendingPolicyVisibilityRowDto`, `CloudSpendingPolicyDecisionDto`, `CloudSpendingPolicyApprovalRequestDto`, `CloudSpendingPolicyGateResultDto`, `CloudSpendingPolicies/**` |
| Provisioning/teardown | `CloudProvisioningResultDto`, `CloudTeardownResultDto`, `CloudInventory/DryRunPlans/{id}/Provision`, `CloudInventory/Sessions/**` |
| Lifecycle/runtime | `CloudRuntimeStatusDto`, `LifecycleJobDto`, `LifecycleWorkerDto`, `GameServers/{id}/CloudRuntime`, `GameServers/{id}/LifecycleJobs/**` |
| Cost/billing | `CloudCostVisibilityDto`, `CloudRuntimeCostSummaryDto`, `CloudRuntimeCostOwnerIntervalDto`, `CloudRuntimeCostResourceContributionDto`, `BillingAccountDto`, `GameServerBillingAssignmentDto` |
| Audit | `HostingAuditRecordDto`, `GameServers/{id}/AuditRecords` |
| Sensitive generated model requiring wrapping | `CloudWorkerBootstrapCredentialDto` and any model with raw JSON/`AdditionalData`/secret-like fields |

## Shared Patterns

### Generated-Code Boundary

**Source:** AGENTS.md and `sources/WebApp/UtfApi/**`
**Apply to:** All Phase 34 WebApp work

Do not hand-edit `sources/WebApp/UtfApi/**`. Add hand-written behavior under `sources/WebApp/Services/**`, usually as repository facade methods and local projection records.

### Repository Facade Boundary

**Source:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`
**Apply to:** `/servers` cloud workflow actions

Use `Client.GameServers[...]`, `Client.CloudInventory`, and other generated request builders only inside repository implementations. Return typed local projection results to Razor pages.

### Redaction and Allowlisting

**Source:** `sources/WebApp/Services/CloudAdminRepository/CloudAdminRepositoryImpl.cs`, `CloudAdminModels.cs`, API redaction tests
**Apply to:** policy, provisioning, lifecycle, teardown, cost, and audit results

Projection models should include ids, status, owner/billing assignment ids, costs, timestamps, retry/cleanup flags, sanitized summaries, and next-step labels. They should exclude provider tokens, Steam credentials, worker credentials, worker hashes, auth headers, bearer tokens, raw request/response bodies, raw audit payload JSON, raw policy input/risk JSON, and raw `AdditionalData`.

### `/servers` UI Rendering

**Source:** `sources/WebApp/Pages/Servers/Index.razor` and `.css`
**Apply to:** Phase 34 result surfaces

Use `section.detail-section`, `dl.meta-grid`, `.status-badge`, `FluentMessageBar`, `FluentProgress`, `.runtime-actions`/`.guided-actions`, and route-scoped CSS. Keep `/servers/cloud` as the admin/fleet console; do not route normal owner workflow results there.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| WebApp repository unit tests for Kiota facade redaction | test | transform | No existing WebApp test project was found. Planner can either add focused API projection tests under `tests/Api.Tests` or introduce a WebApp test project if Phase 34 scope justifies it. |
| Dedicated generic `CloudWorkflowResult` model | local projection model | transform | Existing projections are feature-specific in `CloudAdminModels`; Phase 34 may create a small normalized result model if it reduces duplication and enforces redaction. |

## Metadata

**Analog search scope:** `sources/WebApp/Services`, `sources/WebApp/Pages/Servers`, `sources/WebApp/UtfApi`, `sources/Api/Controllers`, `tests/Api.Tests`
**Files scanned:** 200+ generated/client/controller/test files by `rg --files`; targeted excerpts read from 10 files
**Pattern extraction date:** 2026-06-19

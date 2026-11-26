# Phase 35: Dry-Run Planning And Review - Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 13
**Analogs found:** 13 / 13

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Controllers/CloudPlanningController.cs` | controller | request-response | `sources/Api/Controllers/CloudPlanningController.cs` | exact |
| `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs` | service | request-response / transform | `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` | role-match |
| `sources/Api/Services/CloudPlanning/IArma3CloudDryRunPlanReviewService.cs` | service | request-response | `sources/Api/Services/CloudPlanning/IArma3CloudDryRunPlannerService.cs` | role-match |
| `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` | service | CRUD / transform | `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` | exact |
| `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` | service | request-response / CRUD | `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` | exact |
| `sources/Api/Data/Hosting/CloudDryRunPlan.cs` | model | CRUD | `sources/Api/Data/Hosting/CloudDryRunPlan.cs` | exact |
| `sources/Api/Data/ApiDbContext.cs` | config | CRUD | `sources/Api/Data/ApiDbContext.cs` | exact |
| `sources/Api/DTO/Arma3CloudDryRunPlanReviewDto.cs` | model | transform | `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs` | role-match |
| `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | service | request-response | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | service | request-response / transform | `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudDryRunPlanReviewModels.cs` | model | transform | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | utility | transform | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | exact |
| `sources/WebApp/Pages/Servers/Index.razor` and `.css` | component | event-driven / request-response | `sources/WebApp/Pages/Servers/Index.razor` and `.css` | exact |
| `tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewTests.cs` | test | CRUD / request-response | `tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs` | role-match |
| `tests/Api.Tests/WebApp/CloudDryRunPlanReviewBoundaryTests.cs` | test | transform / source-scan | `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | exact |

## Pattern Assignments

### `sources/Api/Controllers/CloudPlanningController.cs` (controller, request-response)

**Analog:** `sources/Api/Controllers/CloudPlanningController.cs`

**Imports and route pattern** (lines 1-18):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.CloudPlanning;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.GameServers + "/{gameServerId:long}/cloud-dry-run-plans")]
[Authorize]
public class CloudPlanningController(
```

**Auth and response pattern** (lines 32-45):
```csharp
[ProducesResponseType<Arma3CloudDryRunPlanDto>(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[HttpPost("arma3", Name = nameof(CreateArma3DryRunPlanAsync))]
[Authorize(Claims.ResourceBased.Server.Configuration)]
public async Task<IActionResult> CreateArma3DryRunPlanAsync(
    [FromRoute] long gameServerId,
    [FromBody] Arma3CloudDryRunPlanRequestPayload payload,
    CancellationToken cancellationToken)
{
    var user = await User.GetDatabaseUserAsync(apiDbContext, cancellationToken);
    if (user is null)
        return Unauthorized();
```

**Validation and error handling pattern** (lines 47-80):
```csharp
if (payload.GameServerFk is not null && payload.GameServerFk != gameServerId)
    return BadRequest();

if (!await apiDbContext.GameServers.AnyAsync(
        q => q.PrimaryKey == gameServerId && q.IsActive,
        cancellationToken
    ).ConfigureAwait(false))
{
    return NotFound();
}

try
{
    var result = await arma3CloudDryRunPlannerService
        .CreatePlanAsync(
            gameServerId,
            payload with
            {
                GameServerFk = gameServerId,
            },
            user,
            cancellationToken
        )
        .ConfigureAwait(false);
    return Ok(result);
}
catch (ArgumentException)
{
    return BadRequest();
}
catch (InvalidOperationException)
{
    return BadRequest();
}
```

Apply this to review endpoints: keep owner/server authorization server-side, return `Unauthorized`, `NotFound`, `BadRequest`, or `Ok(dto)`, and do not expose raw exception bodies.

---

### `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlanReviewService.cs` (service, request-response / transform)

**Analog:** `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs`

**DI and constructor pattern** (lines 18-29):
```csharp
/// <summary>
/// Default implementation of <see cref="IArma3CloudDryRunPlannerService"/>.
/// </summary>
[Scoped<Arma3CloudDryRunPlannerService, IArma3CloudDryRunPlannerService>]
internal class Arma3CloudDryRunPlannerService(
    ApiDbContext apiDbContext,
    IConfiguration configuration,
    IBillingOwnershipService billingOwnershipService,
    ICloudInventoryService cloudInventoryService,
    ICloudCostEstimateService cloudCostEstimateService,
    IHetznerMetadataSyncService hetznerMetadataSyncService)
    : IArma3CloudDryRunPlannerService
```

**Owner and active-server validation pattern** (lines 55-68):
```csharp
var gameServer = await apiDbContext.GameServers
    .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && q.IsActive, cancellationToken)
    .ConfigureAwait(false);
if (gameServer is null)
    throw new InvalidOperationException($"Game server {gameServerId} does not exist or is inactive.");

var activeAssignment = await billingOwnershipService
    .RequireActiveAssignmentAsync(gameServerId, cancellationToken)
    .ConfigureAwait(false);
if (payload.SelectedBillingAccountFk != activeAssignment.BillingAccountFk
    || payload.SelectedBillingAssignmentFk != activeAssignment.PrimaryKey)
{
    throw new InvalidOperationException("Selected billing owner does not match the active game-server owner.");
}
```

**Review projection source fields** (lines 137-171):
```csharp
return new Arma3CloudDryRunPlanDto
{
    IsValid                     = failures.Count == 0,
    DryRunPlanFk                = dryRunPlan.PrimaryKey,
    SessionFk                   = dryRunPlan.SessionFk,
    GameServerFk                = gameServer.PrimaryKey,
    SelectedBillingAccountFk    = activeAssignment.BillingAccountFk,
    SelectedBillingAssignmentFk = activeAssignment.PrimaryKey,
    ProviderIdentifier          = HetznerProviderConfiguration.HetznerProviderIdentifier,
    ServerTypeName              = serverTypeSelection.Value?.Name,
    LocationName                = locationSelection.Value?.Name,
    DatacenterName              = datacenterSelection.Value?.Name,
    ImageName                   = imageSelection.Value?.Name,
    PlannedResourceState        = resourceState,
    MetadataSourceTimestamp     = metadataStatus.SourceTimestamp,
    ValidationFailures          = failures,
    FirewallRules               = firewallRules,
    Assumptions                 = assumptions,
    DryRunPlan                  = dryRunPlan.ToDto(),
    CostEstimateFk              = costEstimate?.PrimaryKey,
    EstimatedHourlyCost         = costEstimate?.EstimatedHourlyCost,
    EstimatedMonthlyCost        = costEstimate?.EstimatedMonthlyCost,
    Currency                    = costEstimate?.Currency,
    CostEstimate                = costEstimate?.ToDto(),
};
```

**Typed review content pattern** (lines 409-455 and 457-529):
```csharp
private IEnumerable<Arma3CloudFirewallRuleDto> CreateFirewallRules(
    Arma3Ports ports,
    Arma3CloudDryRunPlanRequestPayload payload)
{
    yield return new Arma3CloudFirewallRuleDto
    {
        Protocol    = "udp",
        Port        = ports.GamePort,
        SourceCidr  = AnySourceCidr,
        Description = "Arma 3 game traffic.",
    };
    ...
}

private static IEnumerable<Arma3CloudPlanAssumptionDto> CreateAssumptions(
    Arma3CloudDryRunPlanRequestPayload payload,
    Selection<HetznerServerType> serverTypeSelection,
    Selection<HetznerLocation> locationSelection,
    Selection<HetznerImage> imageSelection,
    Arma3Ports ports,
    int storageGigabytes,
    IReadOnlyCollection<Arma3CloudFirewallRuleDto> firewallRules)
```

Review service should parse/project persisted plan records into typed firewall, assumption, validation, resource, and cost rows. Do not return raw `ValidationDetailsJson`, `PlannedOperationsJson`, or `DetailsJson` to owner-facing WebApp models.

---

### `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` (service, CRUD / transform)

**Analog:** `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs`

**Non-mutating persistence pattern** (lines 113-135):
```csharp
var dryRunPlan = await cloudInventoryService
    .CreateDryRunPlanAsync(
        CreateInventoryPayload(
            gameServer,
            serverTypeSelection.Value,
            locationSelection.Value,
            datacenterSelection.Value,
            imageSelection.Value,
            failures,
            assumptions,
            firewallRules,
            ports,
            storageGigabytes,
            resourceState),
        actor,
        cancellationToken
    )
    .ConfigureAwait(false);
var costEstimate = failures.Count == 0
    ? await cloudCostEstimateService
        .CreateEstimateAsync(dryRunPlan.PrimaryKey, storageGigabytes, actor, cancellationToken)
        .ConfigureAwait(false)
    : null;
```

**Plan payload and no-provider-mutation marker** (lines 531-593):
```csharp
private static CloudDryRunPlanCreationPayload CreateInventoryPayload(
    GameServer gameServer,
    HetznerServerType? serverType,
    HetznerLocation? location,
    HetznerDatacenter? datacenter,
    HetznerImage? image,
    IReadOnlyList<Arma3CloudPlanValidationFailureDto> failures,
    IReadOnlyCollection<Arma3CloudPlanAssumptionDto> assumptions,
    IReadOnlyCollection<Arma3CloudFirewallRuleDto> firewallRules,
    Arma3Ports ports,
    int storageGigabytes,
    ECloudResourceState resourceState)
{
    var isValid = failures.Count == 0;
    var plannedOperations = Serialize(
        new
        {
            assumption = OneWorkerAssumption,
            assumptions,
            firewallRules,
            ports,
            storageGigabytes,
            noProviderMutation = true,
        });

    return new CloudDryRunPlanCreationPayload
    {
        GameServerFk          = gameServer.PrimaryKey,
        ProviderIdentifier    = HetznerProviderConfiguration.HetznerProviderIdentifier,
        DisplayName           = $"Arma 3 cloud dry-run for {gameServer.Title}",
        ValidationSummary     = isValid
            ? "Arma 3 cloud dry-run validation passed."
            : "Arma 3 cloud dry-run validation failed.",
        ValidationDetailsJson = validationDetails,
        PlannedOperationsJson = plannedOperations,
        PlannedResources =
        [
            CreateResource(ECloudResourceKind.CloudGameServerSession, "Arma 3 cloud game-server session", resourceState, null),
            CreateResource(ECloudResourceKind.Server, "Arma 3 CCX worker", resourceState, resourceDetails),
            CreateResource(ECloudResourceKind.IpAddress, "Arma 3 public IP address", resourceState, null),
            CreateResource(ECloudResourceKind.Firewall, "Arma 3 firewall", resourceState, resourceDetails),
            CreateResource(ECloudResourceKind.Volume, "Arma 3 persistent storage", resourceState, resourceDetails),
        ],
    };
}
```

Supersession/regeneration should extend this service path: mark older unconsumed current plans as `Superseded`, persist safe review fingerprint/summary fields, and keep the constructor free of `IHetznerCloudClient`, `IHttpClientFactory`, and `HttpClient`.

---

### `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` (service, request-response / CRUD)

**Analog:** `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs`

**Authoritative blocker pattern** (lines 108-126):
```csharp
private async Task<string> ValidateAsync(
    ProvisioningContext context,
    CancellationToken cancellationToken)
{
    if (context.Plan.Status != ECloudDryRunPlanStatus.Valid)
        throw new InvalidOperationException("Only valid, unused dry-run plans can be provisioned.");
    if (!string.Equals(
            context.Plan.ProviderIdentifier,
            HetznerProviderConfiguration.HetznerProviderIdentifier,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Only Hetzner dry-run plans can be provisioned.");
    }

    ValidateSessionAndResources(context);
    await ValidateActiveOwnerAsync(context, cancellationToken).ConfigureAwait(false);
    await ValidateMetadataAsync(context, cancellationToken).ConfigureAwait(false);
    return await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
}
```

**Snapshot mismatch pattern** (lines 128-166):
```csharp
if (session.GameServerFk != plan.GameServerFk
    || session.BillingAccountFk != plan.BillingAccountFk
    || session.BillingAssignmentFk != plan.BillingAssignmentFk
    || !string.Equals(session.ProviderIdentifier, plan.ProviderIdentifier, StringComparison.Ordinal))
{
    throw new InvalidOperationException("Dry-run plan and session snapshots do not match.");
}

foreach (var resource in context.Resources)
{
    if (resource.SessionFk != session.PrimaryKey
        || resource.GameServerFk != plan.GameServerFk
        || resource.BillingAccountFk != plan.BillingAccountFk
        || resource.BillingAssignmentFk != plan.BillingAssignmentFk
        || !string.Equals(resource.ProviderIdentifier, plan.ProviderIdentifier, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Dry-run resource snapshots do not match the plan.");
    }

    if (resource.State is not ECloudResourceState.DryRun and not ECloudResourceState.ReadyForFutureProvisioning)
        throw new InvalidOperationException("All dry-run resources must be unused before provisioning.");
    if (!string.IsNullOrWhiteSpace(resource.ProviderResourceId))
        throw new InvalidOperationException("Dry-run plan has already provisioned provider resources.");
}
```

**Owner and metadata blockers** (lines 180-222):
```csharp
var assignment = await apiDbContext.GameServerBillingAssignments
    .Include(q => q.BillingAccount)
    .SingleOrDefaultAsync(
        q => q.PrimaryKey == context.Plan.BillingAssignmentFk
             && q.GameServerFk == context.Plan.GameServerFk
             && q.TimeStampUnassigned == null,
        cancellationToken
    )
    .ConfigureAwait(false);
if (assignment is null
    || assignment.BillingAccountFk != context.Plan.BillingAccountFk
    || assignment.BillingAccount?.IsActive != true)
{
    throw new InvalidOperationException("Dry-run plan billing owner no longer matches the active owner.");
}

if ((metadata.SourceTimestamp.Value - context.Plan.MetadataSourceTimestamp.Value).Duration()
    > TimeSpan.FromSeconds(1))
{
    throw new InvalidOperationException("Dry-run plan metadata snapshot is stale.");
}
```

Add Phase 35 `Superseded` and input-fingerprint mismatch blockers here when provisioning remains present in code. UI disabled states are explanatory only; this service is the gate that must keep provider client calls unreachable.

---

### `sources/Api/Data/Hosting/CloudDryRunPlan.cs` and `sources/Api/Data/ApiDbContext.cs` (model/config, CRUD)

**Analogs:** `sources/Api/Data/Hosting/CloudDryRunPlan.cs`, `sources/Api/Data/ApiDbContext.cs`

**Entity pattern** (`CloudDryRunPlan.cs` lines 12-23, 86-122):
```csharp
[Index(nameof(SessionFk))]
[Index(nameof(GameServerFk))]
[Index(nameof(BillingAccountFk))]
[Index(nameof(BillingAssignmentFk))]
[Index(nameof(ProviderIdentifier), nameof(Status))]
public class CloudDryRunPlan
{
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Dry-run plan status.
    /// </summary>
    public ECloudDryRunPlanStatus Status { get; set; }

    [MaxLength(1024)]
    public string? ValidationSummary { get; set; }

    [MaxLength(8192)]
    public string? ValidationDetailsJson { get; set; }

    [MaxLength(8192)]
    public string? PlannedOperationsJson { get; set; }

    public DateTimeOffset? MetadataSourceTimestamp { get; set; }
    public DateTimeOffset TimeStampCreated { get; set; }
    public DateTimeOffset TimeStampUpdated { get; set; }
}
```

**DbSet and relationship pattern** (`ApiDbContext.cs` lines 66-79, 227-255):
```csharp
public DbSet<CloudCostEstimate> CloudCostEstimates { get; set; } = null!;
public DbSet<CloudDryRunPlan> CloudDryRunPlans { get; set; } = null!;
public DbSet<CloudGameServerSession> CloudGameServerSessions { get; set; } = null!;
public DbSet<CloudResourceInventoryRecord> CloudResourceInventoryRecords { get; set; } = null!;

modelBuilder.Entity<CloudDryRunPlan>()
    .HasOne(q => q.Session)
    .WithMany(q => q.DryRunPlans)
    .HasForeignKey(q => q.SessionFk)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<CloudDryRunPlan>()
    .HasOne(q => q.GameServer)
    .WithMany()
    .HasForeignKey(q => q.GameServerFk)
    .OnDelete(DeleteBehavior.Restrict);
```

For new review state fields, follow existing annotations and restricted relationship conventions. Add nullable migration fields for legacy safety. Do not use generated migration files as style sources, but EF migrations are expected output when entity fields change.

---

### `sources/Api/DTO/Arma3CloudDryRunPlanReviewDto.cs` (model, transform)

**Analogs:** `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs`, `sources/Api/DTO/CloudDryRunPlanDto.cs`

**DTO record pattern** (`Arma3CloudDryRunPlanDto.cs` lines 5-18):
```csharp
/// <summary>
/// Describes the result of an Arma 3 cloud dry-run plan request.
/// </summary>
public record Arma3CloudDryRunPlanDto
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Persisted dry-run plan foreign key, when a plan was persisted.
    /// </summary>
    public long? DryRunPlanFk { get; init; }
```

**Typed safe collections pattern** (`Arma3CloudDryRunPlanDto.cs` lines 120-168):
```csharp
public IReadOnlyList<Arma3CloudPlanValidationFailureDto> ValidationFailures { get; init; } = [];

public IReadOnlyList<Arma3CloudFirewallRuleDto> FirewallRules { get; init; } = [];

public IReadOnlyList<Arma3CloudPlanAssumptionDto> Assumptions { get; init; } = [];

public CloudDryRunPlanDto? DryRunPlan { get; init; }

public long? CostEstimateFk { get; init; }

public decimal? EstimatedHourlyCost { get; init; }

public decimal? EstimatedMonthlyCost { get; init; }

public string? Currency { get; init; }

public CloudCostEstimateDto? CostEstimate { get; init; }
```

**Raw JSON warning source** (`CloudDryRunPlanDto.cs` lines 55-68):
```csharp
/// <summary>
/// Bounded validation details as JSON.
/// </summary>
public string? ValidationDetailsJson { get; init; }

/// <summary>
/// Bounded planned operation details as JSON.
/// </summary>
public string? PlannedOperationsJson { get; init; }
```

Phase 35 owner review DTOs should expose typed safe fields and state labels. Keep raw JSON fields out of the owner review DTO even if existing admin DTOs contain them.

---

### `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` (service, request-response / transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`

**Repository registration and generated-client boundary** (lines 1-15):
```csharp
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Kiota.Abstractions;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.UnitedTacticalForces.WebApp.Services.CloudAdminRepository;
using X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.CloudWorkflow;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl) : RepositoryBase(httpClientFactory, baseUrl),
    IGameServerRepository
```

**Generated Kiota action pattern** (lines 328-347):
```csharp
public Task<CloudWorkflowModels.CloudWorkflowActionResult> CreateArma3CloudDryRunPlanResultAsync(
    long gameServerId,
    Arma3CloudDryRunPlanRequestPayload payload,
    CancellationToken cancellationToken = default
)
{
    var requestInfo = Client.GameServers[gameServerId]
        .CloudDryRunPlans
        .Arma3
        .ToPostRequestInformation(payload);
    return CloudWorkflowKiotaSender.SendAsync(
        RequestAdapter,
        requestInfo,
        Arma3CloudDryRunPlanDto.CreateFromDiscriminatorValue,
        (result) => CloudWorkflowResultMapper.FromArma3DryRunPlan(result),
        CloudWorkflowModels.CloudWorkflowActionKind.DryRunPlanning,
        "Create Arma 3 cloud dry-run plan",
        cancellationToken: cancellationToken
    );
}
```

Add review methods as repository facade methods using generated request information and mapper projections. Do not add new raw HTTP calls for Phase 35 owner-facing review.

---

### `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudDryRunPlanReviewModels.cs` (model, transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs`

**Result taxonomy pattern** (lines 5-49):
```csharp
public enum CloudWorkflowResultState
{
    Loading,
    Success,
    PolicyAllow,
    PolicyWarning,
    ApprovalRequired,
    Denied,
    ValidationBlocked,
    Failure,
    Empty,
}

public record CloudWorkflowActionResult
{
    public CloudWorkflowResultState State { get; init; }
    public CloudWorkflowActionKind ActionKind { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? SanitizedDetails { get; init; }
    public string RequiredNextStep { get; init; } = string.Empty;
    public bool IsRetryable { get; init; }
    public DateTimeOffset? TimeStampResult { get; init; }
    public CloudWorkflowValidationResult? Validation { get; init; }
    public CloudWorkflowCostSummary? Cost { get; init; }
    public IReadOnlyList<CloudWorkflowResourceSummary> Resources { get; init; } = [];
    public IReadOnlyList<CloudWorkflowMetadataRow> Metadata { get; init; } = [];
}
```

**Cost and resource model pattern** (lines 128-149, 168-192):
```csharp
public record CloudWorkflowCostSummary
{
    public string SourceLabel { get; init; } = string.Empty;
    public string FreshnessLabel { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal CurrentEstimatedCost { get; init; }
    public decimal AccumulatedEstimatedCost { get; init; }
    public decimal? ConfidenceScore { get; init; }
    public DateTimeOffset? TimeStampProjected { get; init; }
    public DateTimeOffset? TimeStampSnapshot { get; init; }
}

public record CloudWorkflowResourceSummary
{
    public long ResourceId { get; init; }
    public long? SessionId { get; init; }
    public long? GameServerId { get; init; }
    public string ProviderIdentifier { get; init; } = string.Empty;
    public string? ProviderResourceId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;
    public string StateLabel { get; init; } = string.Empty;
    public string? StateReason { get; init; }
    public DateTimeOffset? MetadataSourceTimestamp { get; init; }
}
```

Create sibling review models with typed `PlannedResources`, `FirewallRules`, `Assumptions`, `ValidationFailures`, `CostBreakdown`, and review-state fields. Keep them hand-written and allowlisted.

---

### `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` (utility, transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`

**Dry-run mapping pattern** (lines 186-244):
```csharp
public static CloudWorkflowModels.CloudWorkflowActionResult FromArma3DryRunPlan(
    ApiModels.Arma3CloudDryRunPlanDto? plan,
    string actionName = "Create Arma 3 cloud dry-run plan")
{
    if (plan is null)
        return Empty(actionName, CloudWorkflowModels.CloudWorkflowActionKind.DryRunPlanning);

    var state = plan.IsValid.GetValueOrDefault()
        ? CloudWorkflowModels.CloudWorkflowResultState.Success
        : CloudWorkflowModels.CloudWorkflowResultState.ValidationBlocked;
    var validationFailures = plan.ValidationFailures?
        .Select((failure) => Row(failure.Target ?? failure.Code ?? "Validation", failure.Message))
        .ToArray() ?? [];

    return new CloudWorkflowModels.CloudWorkflowActionResult
    {
        State            = state,
        ActionKind       = CloudWorkflowModels.CloudWorkflowActionKind.DryRunPlanning,
        ActionName       = Safe(actionName, nameof(CloudWorkflowModels.CloudWorkflowActionResult.ActionName)),
        StatusLabel      = plan.IsValid.GetValueOrDefault() ? "Dry-run plan ready" : "Validation blocked",
        Summary          = Safe(
            plan.IsValid.GetValueOrDefault()
                ? "Arma 3 cloud dry-run plan returned a redacted repository result."
                : "Arma 3 cloud dry-run planning returned validation blockers.",
            nameof(CloudWorkflowModels.CloudWorkflowActionResult.Summary)
        ),
        RequiredNextStep = plan.IsValid.GetValueOrDefault() ? "Review policy decision" : "Regenerate cloud plan",
        Cost             = new CloudWorkflowModels.CloudWorkflowCostSummary
        {
            SourceLabel          = "Dry-run estimate",
            FreshnessLabel       = plan.MetadataSourceTimestamp is null ? "Unknown freshness" : "Metadata projected",
            Currency             = Safe(plan.Currency, nameof(CloudWorkflowModels.CloudWorkflowCostSummary.Currency)),
            CurrentEstimatedCost = ToDecimal(plan.EstimatedHourlyCost),
        },
        Metadata         =
        [
            Row("Dry-run plan", plan.DryRunPlanFk?.ToString() ?? plan.DryRunPlan?.PrimaryKey?.ToString()),
            Row("Game server", plan.GameServerFk?.ToString() ?? plan.DryRunPlan?.GameServerFk?.ToString()),
            Row("Billing account", plan.SelectedBillingAccountFk?.ToString() ?? plan.DryRunPlan?.BillingAccountFk?.ToString()),
            Row("Billing assignment", plan.SelectedBillingAssignmentFk?.ToString() ?? plan.DryRunPlan?.BillingAssignmentFk?.ToString()),
            Row("Provider", plan.ProviderIdentifier),
        ],
    };
}
```

**Sanitization helper pattern** (lines 872-877, 950-951):
```csharp
private static CloudWorkflowModels.CloudWorkflowMetadataRow Row(string label, string? value)
    => new()
    {
        Label = Safe(label, nameof(CloudWorkflowModels.CloudWorkflowMetadataRow.Label)),
        Value = Safe(string.IsNullOrWhiteSpace(value) ? "None" : value, nameof(CloudWorkflowModels.CloudWorkflowMetadataRow.Value)),
    };

private static string Safe(string? value, string fieldName)
    => CloudWorkflowRedactionGuard.ValidateDisplayText(value, fieldName) ?? string.Empty;
```

All review mapper strings should pass through `Safe`. Do not bind generated Kiota DTOs or `AdditionalData` directly to Razor.

---

### `sources/WebApp/Pages/Servers/Index.razor` and `.css` (component, event-driven / request-response)

**Analogs:** `sources/WebApp/Pages/Servers/Index.razor`, `sources/WebApp/Pages/Servers/Index.razor.css`

**Guided owner workflow panel pattern** (`Index.razor` lines 247-314):
```razor
<section class="detail-section guided-cloud-panel">
    <div class="section-title">Cloud setup</div>
    @if (_guidedCloudLoadError is not null)
    {
        <FluentMessageBar Intent="MessageIntent.Error">@_guidedCloudLoadError</FluentMessageBar>
    }
    @if (_isGuidedCloudLoading)
    {
        <FluentStack Orientation="Orientation.Vertical" HorizontalAlignment="HorizontalAlignment.Center">
            Loading cloud setup...
            <FluentProgress Width="240px"/>
        </FluentStack>
    }
    else
    {
        @if (_guidedCloudValidationMessages.Count > 0)
        {
            <FluentMessageBar Intent="MessageIntent.Warning">
                Cloud planning cannot continue. Fix the highlighted fields and refresh owner eligibility.
            </FluentMessageBar>
        }
        ...
        <div class="guided-form-heading">
            <div>
                <h2>Arma 3 cloud planning</h2>
                <p>Choose an eligible server, active billing owner, and complete planning inputs.</p>
            </div>
            <span class="@GetGuidedPanelStatusClass()">@FormatGuidedPanelStatus()</span>
        </div>
```

**Current result panel pattern** (`Index.razor` lines 577-668):
```razor
<section class="detail-section cloud-workflow-result-panel">
    <div class="cloud-result-header">
        <div>
            <div class="section-title">Cloud action result</div>
            <p class="cloud-result-copy">Repository-backed cloud workflow result for the selected server.</p>
        </div>
        <span class="@GetCloudWorkflowResultClass(_selectedCloudWorkflowResult)">
            @FormatCloudWorkflowResultState(_selectedCloudWorkflowResult)
        </span>
    </div>

    @if (_isCloudWorkflowResultLoading)
    {
        <FluentStack Orientation="Orientation.Vertical">
            Loading cloud workflow result...
            <FluentProgress Width="240px"/>
        </FluentStack>
    }
    else if (_cloudWorkflowResultError is not null)
    {
        <FluentMessageBar Intent="MessageIntent.Error">@_cloudWorkflowResultError</FluentMessageBar>
    }
    else if (_selectedCloudWorkflowResult is null
             || _selectedCloudWorkflowResult.State == CloudWorkflowModels.CloudWorkflowResultState.Empty)
    {
        <FluentMessageBar Intent="MessageIntent.Warning">
            <strong>No cloud workflow result.</strong>
            Select an eligible Arma 3 server and complete cloud setup before cloud workflow results are available.
        </FluentMessageBar>
    }
    else if (IsCloudWorkflowResultBlocked(_selectedCloudWorkflowResult))
    {
        <FluentMessageBar Intent="MessageIntent.Error">
            Cloud result was blocked because it contained a forbidden field. The result was not displayed.
        </FluentMessageBar>
    }
```

**Repository action flow pattern** (`Index.razor` lines 1500-1559, 1660-1695):
```csharp
private async Task ExecuteCloudWorkflowActionAsync(
    PendingCloudWorkflowAction action,
    CloudWorkflowModels.CloudWorkflowPolicyGateResult? continuationGate = null)
{
    _busyServerId   = action.GameServerId ?? _selectedServer?.PrimaryKey ?? action.TargetId;
    _errorMessage   = null;
    _successMessage = null;
    try
    {
        var acknowledgedDecisionId = GetAcknowledgedDecisionId(continuationGate);
        var approvalRequestId = GetApprovalRequestId(continuationGate);
        var result = await RunCloudWorkflowActionAsync(action, acknowledgedDecisionId, approvalRequestId)
            .ConfigureAwait(false);
        ...
        StoreSelectedCloudWorkflowResult(result, pendingAction);
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
    }
    finally
    {
        _busyServerId = null;
    }
}

private async Task LoadSelectedCloudWorkflowResultAsync()
{
    _selectedCloudWorkflowResult = null;
    _pendingCloudWorkflowAction = null;
    _cloudWorkflowResultError = null;
    if (_selectedServer is null)
        return;

    _isCloudWorkflowResultLoading = true;
    try
    {
        var result = await GameServerRepository
            .GetCloudWorkflowStatusResultAsync(_selectedServer.PrimaryKey)
            .ConfigureAwait(false);
        StoreSelectedCloudWorkflowResult(result);
    }
    catch
    {
        _cloudWorkflowResultError = "Cloud action result could not be loaded. Refresh cloud status and try again.";
    }
    finally
    {
        _isCloudWorkflowResultLoading = false;
    }
}
```

**CSS structure pattern** (`Index.razor.css` lines 104-160, 186-242, 254-264):
```css
.status-badge {
    align-items: center;
    border: 1px solid transparent;
    border-radius: 999px;
    display: inline-flex;
    font-size: 13px;
    gap: 4px;
    line-height: 1.2;
    padding: 4px 8px;
    white-space: nowrap;
}

.meta-grid {
    display: grid;
    gap: 8px 16px;
    grid-template-columns: max-content minmax(0, 1fr);
    margin: 0;
}

.cloud-workflow-result-panel {
    border-color: #2563eb;
    display: flex;
    flex-direction: column;
    gap: 16px;
    grid-column: 1 / -1;
}

.data-table {
    border-collapse: collapse;
    font-size: 14px;
    min-width: 100%;
}

.guided-cloud-panel {
    display: flex;
    flex-direction: column;
    gap: 16px;
}
```

Phase 35 should add the plan review panel under the same `/servers` selected-server workflow. Use `FluentMessageBar`, `dl.meta-grid`, `.data-table`, `.guided-validation-list`, and `.guided-actions`. Keep future provider provisioning hidden or disabled with explicit copy; do not enable mutation in this phase.

---

### `tests/Api.Tests/CloudPlanning/Arma3CloudPlanReviewTests.cs` (test, CRUD / request-response)

**Analog:** `tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs`

**SQLite test context pattern** (lines 29-45, 375-383):
```csharp
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var context = await CreateReadyContextAsync(connection);

var result = await context.PlannerService.CreatePlanAsync(
    context.GameServer.PrimaryKey,
    new Arma3CloudDryRunPlanRequestPayload
    {
        SelectedBillingAccountFk    = context.BillingAccount!.PrimaryKey,
        SelectedBillingAssignmentFk = context.Assignment!.PrimaryKey,
        StorageGigabytes            = 150,
        RconPort                    = 2306,
        AdministrativeSourceCidr    = "10.0.0.0/8",
    },
    context.Actor,
    CancellationToken.None
);

private static async Task<ApiDbContext> CreateDbContextAsync(SqliteConnection connection)
{
    var options = new DbContextOptionsBuilder<SqliteApiDbContext>()
        .UseSqlite(connection)
        .Options;
    var dbContext = new SqliteApiDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();
    return dbContext;
}
```

**Non-mutating and persistence assertions** (lines 76-115):
```csharp
context.DbContext.ChangeTracker.Clear();
var session = await context.DbContext.CloudGameServerSessions.SingleAsync();
Assert.Null(session.ProviderSessionId);
Assert.Equal(ECloudResourceState.DryRun, session.State);
Assert.Equal(context.BillingAccount.PrimaryKey, session.BillingAccountFk);
Assert.Equal(context.Assignment.PrimaryKey, session.BillingAssignmentFk);

var resources = await context.DbContext.CloudResourceInventoryRecords
    .OrderBy(q => q.Kind)
    .ToArrayAsync();
Assert.All(resources, resource =>
{
    Assert.Null(resource.ProviderResourceId);
    Assert.Equal(ECloudResourceState.ReadyForFutureProvisioning, resource.State);
    Assert.Equal(context.GameServer.PrimaryKey, resource.GameServerFk);
    Assert.Equal(context.BillingAccount.PrimaryKey, resource.BillingAccountFk);
    Assert.Equal(context.Assignment.PrimaryKey, resource.BillingAssignmentFk);
    Assert.Equal(context.Actor.PrimaryKey, resource.PlannedByFk);
});

var persistedPlan = await context.DbContext.CloudDryRunPlans.SingleAsync();
Assert.Equal(ECloudDryRunPlanStatus.Valid, persistedPlan.Status);
Assert.Contains("noProviderMutation", persistedPlan.PlannedOperationsJson, StringComparison.Ordinal);
```

**Providerless constructor guard** (lines 259-270):
```csharp
[Fact]
public void Planner_DoesNotDependOnProviderClientOrHttpClient()
{
    var constructorParameterTypes = typeof(Arma3CloudDryRunPlannerService)
        .GetConstructors()
        .SelectMany(q => q.GetParameters())
        .Select(q => q.ParameterType)
        .ToArray();
    Assert.DoesNotContain(typeof(IHetznerCloudClient), constructorParameterTypes);
    Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameterTypes);
    Assert.DoesNotContain(typeof(HttpClient), constructorParameterTypes);
}
```

Add tests for latest review projection, supersession, input mismatch, owner mismatch, metadata stale labels, and typed safe review content. Use the same SQLite helper and explicit persistence assertions.

---

### `tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs` (test, request-response / CRUD)

**Analog:** `tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs`

**No-provider-call blocker tests** (lines 137-198):
```csharp
[Fact]
public async Task ProvisionDryRunPlanAsync_ConsumedPlanDoesNotCallProvider()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var client = new FakeHetznerCloudClient();
    var context = await CreateReadyContextAsync(connection, client);
    context.Plan.Status = ECloudDryRunPlanStatus.Provisioned;
    await context.DbContext.SaveChangesAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => context.ProvisioningService.ProvisionDryRunPlanAsync(
            context.Plan.PrimaryKey,
            context.Actor,
            CancellationToken.None
        )
    );

    Assert.Empty(client.Calls);
}

[Fact]
public async Task ProvisionDryRunPlanAsync_StaleMetadataDoesNotCallProvider()
...
Assert.Empty(client.Calls);

[Fact]
public async Task ProvisionDryRunPlanAsync_OwnerMismatchDoesNotCallProvider()
...
Assert.Empty(client.Calls);
```

**Fake provider boundary pattern** (lines 592-611, 658-680):
```csharp
private sealed class FakeHetznerCloudClient : IHetznerCloudClient
{
    public List<string> Calls { get; } = [];

    public Task<HetznerCloudClientResult> VerifyCredentialsAsync(
        string token,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Provisioning tests do not verify credentials through the client.");

    public Task<HetznerCloudClientResult<HetznerCloudProvisionedFirewall>> CreateFirewallAsync(
        string token,
        HetznerCloudFirewallCreateRequest request,
        CancellationToken cancellationToken)
    {
        Calls.Add("firewall");
        Assert.Equal(Token, token);
        FirewallRequest = request;
        return Task.FromResult(
            HetznerCloudClientResult<HetznerCloudProvisionedFirewall>.Success(
                new HetznerCloudProvisionedFirewall(101, request.Name, 201)
            )
        );
    }
```

Add tests for `Superseded` and input-fingerprint mismatch blockers here. Assertion must remain `Assert.Empty(client.Calls)` for every invalid, stale, superseded, owner-mismatched, or input-mismatched plan.

---

### `tests/Api.Tests/WebApp/CloudDryRunPlanReviewBoundaryTests.cs` (test, transform / source-scan)

**Analog:** `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`

**Forbidden field and raw boundary pattern** (lines 8-64):
```csharp
private static readonly string[] ForbiddenDisplayFields =
[
    "InputSnapshotJson",
    "RiskDetailsJson",
    "ValidationDetailsJson",
    "PlannedOperationsJson",
    "DetailsJson",
    "AdditionalData",
    "CredentialHash",
    "TokenReference",
    "AuthorizationHeader",
    "BearerToken",
    "SteamPassword",
    "SteamApiKey",
    "ProviderToken",
    "WorkerCredential",
    "WorkerCredentialHash",
    "WorkerHash",
    "RawJson",
    "RawPayload",
    "ExceptionBody",
    "ResponseHeaders",
];

private static readonly string[] ServersBoundaryMarkers =
[
    "RequestBuilder",
    "UtfApi",
    "HttpClient",
    "GetFromJsonAsync",
    "PostAsJsonAsync",
    "ReadFromJsonAsync",
    .. ForbiddenDisplayFields,
];
```

**Source-scan assertions** (lines 84-153):
```csharp
[Fact]
public void CloudWorkflowProjectionFiles_DoNotExposeForbiddenDisplayFields()
{
    var files = new[]
    {
        "sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs",
        "sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs",
        "sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs",
    };

    var failures = new List<string>();
    foreach (var file in files)
    {
        var source = ReadRepositoryFile(file);
        foreach (var propertyName in GetPublicPropertyNames(source))
        {
            foreach (var forbiddenField in ForbiddenDisplayFields)
            {
                if (propertyName.Equals(forbiddenField, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"{file}: public property '{propertyName}' exposes forbidden field '{forbiddenField}'.");
            }
        }
    }

    failures.AddRange(FindMarkerHits(
        "sources/WebApp/Pages/Servers/Index.razor",
        ForbiddenDisplayFields,
        lineMustContainResultMarkup: true
    ));

    AssertNoFailures(failures);
}

[Fact]
public void ServersOwnerWorkflow_DoesNotUseGeneratedBuildersOrRawHttp()
{
    var failures = FindMarkerHits("sources/WebApp/Pages/Servers/Index.razor", ServersBoundaryMarkers);

    AssertNoFailures(failures);
}
```

**Deferred UAT evidence boundary** (lines 66-82, 338-354):
```csharp
private static readonly string[] DeferredEvidenceClaimMarkers =
[
    "live UAT passed",
    "Hetzner verified live",
    "Steam verified live",
    "Arma runtime verified live",
    "authenticated browser UAT passed",
    "provider mutation verified live",
    "provisioning verified live",
    "teardown UAT passed",
    "Phase 30 UAT passed",
];

[Fact]
public void Phase34Sources_DoNotClaimDeferredLiveEvidence()
{
    var files = new[]
    {
        "sources/WebApp/Pages/Servers/Index.razor",
        "sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs",
        "sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs",
        "sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs",
    };

    var failures = files.SelectMany((file) => FindMarkerHits(file, DeferredEvidenceClaimMarkers)).ToArray();

    AssertNoFailures(failures);
}
```

Extend this test for Phase 35 review model files and UI sections. Automated/source tests are in scope; browser/authenticated/live-provider/Steam/Arma UAT must remain explicitly deferred.

## Shared Patterns

### Authentication And Resource Scope

**Source:** `sources/Api/Controllers/CloudPlanningController.cs` lines 15-18 and 36-45
**Apply to:** All Phase 35 dry-run review/generation endpoints.

```csharp
[ApiController]
[Route(Constants.Routes.GameServers + "/{gameServerId:long}/cloud-dry-run-plans")]
[Authorize]
public class CloudPlanningController(...)

[HttpPost("arma3", Name = nameof(CreateArma3DryRunPlanAsync))]
[Authorize(Claims.ResourceBased.Server.Configuration)]
public async Task<IActionResult> CreateArma3DryRunPlanAsync(...)
{
    var user = await User.GetDatabaseUserAsync(apiDbContext, cancellationToken);
    if (user is null)
        return Unauthorized();
```

### Providerless Dry-Run

**Source:** `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` lines 113-135 and `tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs` lines 259-270
**Apply to:** Generation, regeneration, and review-state service changes.

```csharp
var dryRunPlan = await cloudInventoryService
    .CreateDryRunPlanAsync(...);
var costEstimate = failures.Count == 0
    ? await cloudCostEstimateService.CreateEstimateAsync(...)
    : null;

Assert.DoesNotContain(typeof(IHetznerCloudClient), constructorParameterTypes);
Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameterTypes);
Assert.DoesNotContain(typeof(HttpClient), constructorParameterTypes);
```

### Authoritative Provisioning Blockers

**Source:** `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` lines 108-126, 128-166, 180-222
**Apply to:** Invalid, stale, mismatched, superseded, and owner-mismatched blocking.

```csharp
if (context.Plan.Status != ECloudDryRunPlanStatus.Valid)
    throw new InvalidOperationException("Only valid, unused dry-run plans can be provisioned.");

ValidateSessionAndResources(context);
await ValidateActiveOwnerAsync(context, cancellationToken).ConfigureAwait(false);
await ValidateMetadataAsync(context, cancellationToken).ConfigureAwait(false);
```

### Repository Projection And Redaction

**Source:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs` lines 5-78 and `CloudWorkflowResultMapper.cs` lines 872-877, 950-951
**Apply to:** All WebApp dry-run review display models and mapper outputs.

```csharp
public static readonly IReadOnlySet<string> ForbiddenFieldNames = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase
)
{
    "AdditionalData",
    "Authorization",
    "CredentialHash",
    "DetailsJson",
    "InputSnapshotJson",
    "PlannedOperationsJson",
    "TokenReference",
    "ValidationDetailsJson",
    "WorkerHash",
};

private static string Safe(string? value, string fieldName)
    => CloudWorkflowRedactionGuard.ValidateDisplayText(value, fieldName) ?? string.Empty;
```

### `/servers` UI Density And Components

**Source:** `sources/WebApp/Pages/Servers/Index.razor` lines 247-314, 577-668 and `Index.razor.css` lines 104-160, 186-264
**Apply to:** Dry-run review panel in `/servers`.

```razor
<FluentMessageBar Intent="@GetCloudWorkflowMessageIntent(_selectedCloudWorkflowResult)">
    <strong>@GetCloudWorkflowHeading(_selectedCloudWorkflowResult)</strong>
    @GetCloudWorkflowBody(_selectedCloudWorkflowResult)
</FluentMessageBar>

<dl class="meta-grid cloud-result-meta">
    @foreach (var row in GetCloudWorkflowResultRows(_selectedCloudWorkflowResult))
    {
        <dt>@row.Label</dt>
        <dd>@row.Value</dd>
    }
</dl>

<div class="guided-actions cloud-result-actions">
    <FluentButton IconStart="@(new Icons.Regular.Size20.ArrowSyncCircle())">
        Refresh cloud status
    </FluentButton>
</div>
```

### Automated Verification Boundary

**Source:** `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` lines 66-82, 338-354
**Apply to:** Phase 35 tests and final verification notes.

```csharp
private static readonly string[] DeferredEvidenceClaimMarkers =
[
    "live UAT passed",
    "Hetzner verified live",
    "Steam verified live",
    "Arma runtime verified live",
    "authenticated browser UAT passed",
    "provider mutation verified live",
    "provisioning UAT passed",
    "Phase 30 UAT passed",
];
```

## No Analog Found

No Phase 35 files lack a usable analog. The closest analogs are current cloud planning, cloud provisioning, Phase 34 repository result/redaction, `/servers` guided workflow, and API source-boundary tests.

## Metadata

**Analog search scope:** `sources/Api/Controllers`, `sources/Api/Services/CloudPlanning`, `sources/Api/Services/CloudProvisioning`, `sources/Api/Data/Hosting`, `sources/Api/DTO`, `sources/WebApp/Services/GameServerRepository`, `sources/WebApp/Pages/Servers`, `tests/Api.Tests/CloudPlanning`, `tests/Api.Tests/CloudProvisioning`, `tests/Api.Tests/WebApp`
**Files scanned:** 20+
**Pattern extraction date:** 2026-06-20
**UAT boundary:** Automated/source tests only. Manual browser/authenticated/live-provider/Steam/Arma/provider-mutation/Phase 30 evidence remains deferred and must not be reported as passed.

# Phase 37: Provisioning, Runtime Operation, And Teardown - Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 25 planned work areas
**Analogs found:** 25 / 25

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Controllers/GameServersController.cs` | controller | request-response | `sources/Api/Controllers/GameServersController.cs` cloud runtime/status methods | exact |
| `sources/Api/Controllers/CloudInventoryController.cs` | controller | request-response | `sources/Api/Controllers/CloudInventoryController.cs` provisioning/teardown endpoints | exact |
| `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` | service | CRUD + provider I/O | same file | exact |
| `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` | service | CRUD + provider I/O | same file | exact |
| `sources/Api/Services/CloudInventory/CloudInventoryService.cs` | service | transform | same file status projection | exact |
| `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` | service | event-driven + batch | same file | exact |
| `sources/Api/Services/CloudWorkerDispatch/CloudWorkerDispatchService.cs` | service | request-response + event-driven | same file | exact |
| `sources/Api/DTO/CloudProvisioningResultDto.cs` | DTO | transform | `sources/Api/DTO/CloudTeardownResultDto.cs` | exact |
| `sources/Api/DTO/CloudTeardownResultDto.cs` | DTO | transform | same file | exact |
| `sources/Api/DTO/CloudRuntimeStatusDto.cs` | DTO | transform | same file | exact |
| `sources/Api/DTO/CloudSessionStatusDto.cs` | DTO | transform | same file | exact |
| `sources/Api/Data/Hosting/*` cloud session/resource/job/worker entities | model | CRUD | `CloudGameServerSession`, `CloudResourceInventoryRecord`, `LifecycleJob`, `LifecycleWorker` | exact |
| `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | repository facade | request-response | same file cloud workflow methods | exact |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | repository implementation | request-response | same file Kiota sender methods | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | utility | transform | same file provisioning/runtime/teardown mapping | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs` | utility | transform | same file | exact |
| `sources/WebApp/Pages/Servers/Index.razor` | component/page | event-driven + request-response | same file dry-run, policy, result, runtime panels | exact |
| `sources/WebApp/Pages/Servers/Index.razor.css` | component style | transform | same file panel/table/status classes | exact |
| `tests/Api.Tests/CloudProvisioning/CloudProvisioningServiceTests.cs` | test | CRUD + provider I/O | same file | exact |
| `tests/Api.Tests/CloudTeardown/CloudTeardownServiceTests.cs` | test | CRUD + provider I/O | same file | exact |
| `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` | test | request-response + transform | same file | exact |
| `tests/Api.Tests/GameServerLifecycle/LifecycleJobQueueServiceTests.cs` | test | event-driven + batch | same file | exact |
| `tests/Api.Tests/CloudWorkerDispatch/CloudWorkerDispatchServiceTests.cs` | test | request-response + event-driven | same file | exact |
| `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | test | static boundary scan | same file | exact |
| `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` | test | static boundary scan | `PolicyGateApprovalUxBoundaryTests.cs` | role-match |

## Pattern Assignments

### `sources/Api/Controllers/GameServersController.cs` (controller, request-response)

**Use for:** owner/operator server-scoped Phase 37 routes: cloud runtime status, runtime lifecycle actions, cleanup runtime, likely owner-safe provision/teardown wrappers if added.

**Imports and dependency pattern** (`GameServersController.cs` lines 1-18, 32-61):

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.CloudInventory;
using X39.UnitedTacticalForces.Api.Services.CloudPolicies;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

private readonly IGameServerLifecycleJobService _gameServerLifecycleJobService;
private readonly ICloudInventoryService _cloudInventoryService;
private readonly ICloudSpendingPolicyGateService _cloudSpendingPolicyGateService;
```

**Resource auth pattern** (`GameServersController.cs` lines 388-400, 411-424, 435-448):

```csharp
[HttpGet("{gameServerId:long}/cloud-runtime", Name = nameof(GetCloudRuntimeStatusAsync))]
[Authorize($"{Claims.ResourceBased.Server.All}:*")]
public async Task<IActionResult> GetCloudRuntimeStatusAsync(
    [FromRoute] long gameServerId,
    CancellationToken cancellationToken)
{
    var runtimeStatus = await CreateCloudRuntimeStatusAsync(gameServerId, cancellationToken)
        .ConfigureAwait(false);
    return runtimeStatus is null ? NotFound() : Ok(runtimeStatus);
}
```

**Runtime cleanup boundary** (`GameServersController.cs` lines 1143-1177):

```csharp
/// Queues cloud runtime cleanup without deleting provider resources or stopping cost attribution.
[HttpPost("{gameServerId:long}/cloud-runtime/cleanup", Name = nameof(CleanupCloudRuntimeAsync))]
[Authorize(Claims.ResourceBased.Server.StartStop)]
public async Task<IActionResult> CleanupCloudRuntimeAsync(...)
{
    var runtimeStatus = await CreateCloudRuntimeStatusAsync(gameServerId, cancellationToken);
    if (runtimeStatus is null)
        return NotFound();
    if (!runtimeStatus.CanCleanup)
        return Forbid();

    return await EnqueueLifecycleJobAsync(gameServerId, ELifecycleJobKind.Delete, cancellationToken, ...);
}
```

**Runtime projection pattern** (`GameServersController.cs` lines 2099-2176):

```csharp
var currentJob = lifecycleJobs.FirstOrDefault(IsActiveLifecycleJob);
var lastFailedJob = lifecycleJobs.FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.FailureReason)
                                                      && q.Status is ELifecycleJobStatus.Failed
                                                          or ELifecycleJobStatus.RetryPending);
return new CloudRuntimeStatusDto
{
    Session = sessionStatus?.Session,
    Worker = sessionStatus?.Worker,
    CurrentJob = currentJob?.ToDto(),
    CurrentJobProgressPercent = progress.percent,
    LastFailureReason = lastFailedJob?.FailureReason,
    CanRetry = canRetry,
    CanCleanup = canCleanup,
    AvailableActions = CreateRuntimeActions(canRetry, canCleanup),
};
```

**Planner note:** keep owner workflow additions server-scoped here when possible. Existing provider mutation endpoints in `CloudInventoryController` are admin-only; Phase 37 `/servers` owner actions should not rely on `/servers/cloud` admin console authorization.

### `sources/Api/Controllers/CloudInventoryController.cs` (controller, request-response)

**Use for:** admin analog and service-call shape for provisioning/teardown; copy policy gate and result/error mapping, but revisit auth for owner-safe routes.

**Controller setup** (`CloudInventoryController.cs` lines 1-34):

```csharp
[ApiController]
[Route(Constants.Routes.CloudInventory)]
[Authorize]
public class CloudInventoryController(
    ApiDbContext apiDbContext,
    ICloudSpendingPolicyGateService cloudSpendingPolicyGateService,
    ICloudProvisioningService cloudProvisioningService,
    ICloudTeardownService cloudTeardownService)
    : ControllerBase
```

**Provisioning gate before mutation** (`CloudInventoryController.cs` lines 82-117):

```csharp
[HttpPost("dry-run-plans/{planId:long}/provision", Name = nameof(ProvisionDryRunPlanAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> ProvisionDryRunPlanAsync(
    [FromRoute] long planId,
    CancellationToken cancellationToken,
    [FromQuery] long? acknowledgedDecisionId = null,
    [FromQuery] long? approvalRequestId = null)
{
    var gate = await cloudSpendingPolicyGateService.EvaluateProvisioningAsync(
        planId, user, cancellationToken, acknowledgedDecisionId, approvalRequestId);
    if (!gate.CanProceed)
        return Conflict(gate.ToDto());

    var result = await cloudProvisioningService.ProvisionDryRunPlanAsync(planId, user, cancellationToken);
    return Ok(result);
}
```

**Teardown cost-stop gate** (`CloudInventoryController.cs` lines 136-179):

```csharp
[HttpDelete("sessions/{sessionId:long}", Name = nameof(TeardownSessionAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> TeardownSessionAsync(...)
{
    var gate = await cloudSpendingPolicyGateService.EvaluateTeardownAsync(
        sessionId, user, cancellationToken, acknowledgedDecisionId, approvalRequestId);
    if (!gate.CanProceed)
        return Conflict(gate.ToDto());

    var result = await cloudTeardownService.TeardownSessionAsync(sessionId, user, cancellationToken);
    return Ok(result);
}
```

**Error mapping** (`CloudInventoryController.cs` lines 118-125, 180-187):

```csharp
catch (KeyNotFoundException)
{
    return NotFound();
}
catch (InvalidOperationException)
{
    return BadRequest();
}
```

### `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs` (service, CRUD + provider I/O)

**Use for:** provider resource creation, validation order, audit, partial-failure cleanup-needed state, secret-safe provider failures.

**DI and setup** (`CloudProvisioningService.cs` lines 1-24, 31-38):

```csharp
[Scoped<CloudProvisioningService, ICloudProvisioningService>]
internal class CloudProvisioningService(
    ApiDbContext apiDbContext,
    IConfiguration configuration,
    IHetznerSecretResolver hetznerSecretResolver,
    IHetznerCloudClient hetznerCloudClient,
    IHostingAuditService hostingAuditService,
    ICloudWorkerBootstrapService cloudWorkerBootstrapService)
    : ICloudProvisioningService
```

**Validation-before-token/mutation** (`CloudProvisioningService.cs` lines 41-80, 108-117):

```csharp
var context = await LoadContextAsync(dryRunPlanId, cancellationToken);
var token = await ValidateAsync(context, cancellationToken);
await AppendAuditAsync(EHostingAuditEventKind.CloudProvisioningStarted, ...);

try
{
    await ProvisionAsync(context, token, actor, cancellationToken);
    await AppendAuditAsync(EHostingAuditEventKind.CloudProvisioningSucceeded, ...);
}
catch (HetznerProvisioningException ex)
{
    await MarkProvisioningFailedAsync(context, actor, ex.Message, cancellationToken);
    throw new InvalidOperationException(ex.Message, ex);
}
```

**Safe result projection** (`CloudProvisioningService.cs` lines 500-517):

```csharp
return new CloudProvisioningResultDto
{
    Plan               = plan.ToDto(),
    Session            = session?.ToDto(),
    Resources          = resources.Select(q => q.ToDto()).ToArray(),
    PublicEndpointHost = session?.PublicEndpointHost,
    PublicEndpointPort = session?.PublicEndpointPort,
};
```

**Redacted provider failure** (`CloudProvisioningService.cs` lines 560-571):

```csharp
var message = string.IsNullOrWhiteSpace(result.FailureMessage)
    ? fallbackMessage
    : result.FailureMessage;
throw new HetznerProvisioningException(
    LifecycleFailureReasonSanitizer.SanitizeDetails(message) ?? fallbackMessage);
```

### `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` (service, CRUD + provider I/O)

**Use for:** explicit provider teardown cost-stop, deletion order, partial failure, remaining billable resources, cleanup-needed state.

**Service shape** (`CloudTeardownService.cs` lines 1-25):

```csharp
[Scoped<CloudTeardownService, ICloudTeardownService>]
internal class CloudTeardownService(
    ApiDbContext apiDbContext,
    IHetznerSecretResolver hetznerSecretResolver,
    IHetznerCloudClient hetznerCloudClient,
    IHostingAuditService hostingAuditService)
    : ICloudTeardownService
```

**Provider deletion order and audit** (`CloudTeardownService.cs` lines 28-120):

```csharp
await AppendAuditAsync(EHostingAuditEventKind.CloudTeardownStarted, ...);

foreach (var resource in context.Resources.Where(q => q.Kind == ECloudResourceKind.Volume))
    if (await DetachAndDeleteVolumeAsync(...)) deletedResourceCount++;

foreach (var resource in context.Resources.Where(q => q.Kind == ECloudResourceKind.Server))
    if (await DeleteResourceAsync(..., hetznerCloudClient.DeleteServerAsync, ...))
    {
        deletedResourceCount++;
        await MarkSessionInventoryDeletedAsync(context, actor, cancellationToken);
    }
```

**Partial failure and remaining billable boundary** (`CloudTeardownService.cs` lines 106-125):

```csharp
var remainingProviderResources = CountRemainingProviderResources(context.Resources);
var remainingBillableResources = CountRemainingBillableProviderResources(context.Resources);
if (remainingProviderResources == 0)
{
    MarkSessionDeleted(context.Session);
    await AppendAuditAsync(EHostingAuditEventKind.CloudTeardownSucceeded, ...);
}
else
{
    var failureSummary = BuildFailureSummary(failures, remainingBillableResources);
    context.Session.State = ECloudResourceState.CleanupNeeded;
    await AppendAuditAsync(EHostingAuditEventKind.CloudTeardownFailed, ...);
}
```

**Resource cost-stop state** (`CloudTeardownService.cs` lines 318-329):

```csharp
resource.State = ECloudResourceState.Deleted;
resource.StateReason = "Provider resource deleted. Cost attribution stopped.";
resource.TimeStampDeleted = now;
resource.TimeStampUpdated = now;
resource.TimeStampStateChanged = now;
```

### `sources/Api/Services/CloudInventory/CloudInventoryService.cs` (service, transform)

**Use for:** session/resource/bootstrap/worker status projection, retryable actions, cleanup/orphan flags.

**Status projection** (`CloudInventoryService.cs` lines 363-413):

```csharp
var canProvision = latestDryRunPlan?.Status == ECloudDryRunPlanStatus.Valid
                   && sessionCanProvision
                   && string.IsNullOrWhiteSpace(session.ProviderSessionId)
                   && resources.Count > 0
                   && resources.All(IsUnusedProvisionableResource);
var canTeardown = sessionCanTeardown && resources.Any(HasProviderResource);
var needsCleanup = session.State == ECloudResourceState.CleanupNeeded
                   || resources.Any(q => q.State == ECloudResourceState.CleanupNeeded);

return new CloudSessionStatusDto
{
    IsProvisioned = session.State == ECloudResourceState.FutureProvisioned
                    || resources.Any(q => q.State == ECloudResourceState.FutureProvisioned),
    NeedsCleanup = needsCleanup,
    HasOrphanCandidate = session.State == ECloudResourceState.OrphanCandidate
                         || resources.Any(q => q.State == ECloudResourceState.OrphanCandidate),
    CanProvision = canProvision,
    CanTeardown = canTeardown,
    CanRetryCleanup = canRetryCleanup,
    RetryableActions = GetRetryableActions(canProvision, canTeardown, canRetryCleanup),
};
```

**Retry action vocabulary** (`CloudInventoryService.cs` lines 424-437):

```csharp
if (canProvision)
    actions.Add("provision");
if (canRetryCleanup)
    actions.Add("retry-cleanup");
else if (canTeardown)
    actions.Add("teardown");
```

### `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` (service, event-driven + batch)

**Use for:** durable lifecycle job progress, retry, stale lease recovery, audit.

**Claim pattern** (`LifecycleJobQueueService.cs` lines 21-80):

```csharp
var candidateIds = await ApplyWorkerClaimScope(EligibleJobs(now), worker)
    .OrderBy(q => q.TimeStampCreated)
    .ThenBy(q => q.PrimaryKey)
    .Select(q => q.PrimaryKey)
    .Take(ClaimCandidateLimit)
    .ToListAsync(cancellationToken);

var updated = await ApplyWorkerClaimScope(EligibleJobs(now).Where(q => q.PrimaryKey == candidateId), worker)
    .ExecuteUpdateAsync(q => q
        .SetProperty(job => job.Status, ELifecycleJobStatus.Running)
        .SetProperty(job => job.AssignedWorkerId, workerId)
        .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
        .SetProperty(job => job.TimeStampLeaseExpires, leaseExpires), cancellationToken);
```

**Retry/fail pattern with sanitizer** (`LifecycleJobQueueService.cs` lines 153-231):

```csharp
failureReason = LifecycleFailureReasonSanitizer.SanitizeFailureReason(failureReason);
if (allowRetry)
{
    var retryUpdated = await apiDbContext.LifecycleJobs
        .Where(q => q.AttemptCount < q.MaxAttemptCount)
        .ExecuteUpdateAsync(q => q
            .SetProperty(job => job.Status, ELifecycleJobStatus.RetryPending)
            .SetProperty(job => job.TimeStampNextAttempt, nextAttempt)
            .SetProperty(job => job.FailureReason, failureReason), cancellationToken);
}
```

**Audit pattern** (`LifecycleJobQueueService.cs` lines 321-369):

```csharp
await hostingAuditService.AppendAsync(new HostingAuditRecord
{
    Kind                = auditEventKind,
    ActorFk             = lifecycleJob.RequestedByFk,
    GameServerFk        = lifecycleJob.GameServerFk,
    LifecycleJobFk      = lifecycleJob.PrimaryKey,
    BillingAccountFk    = lifecycleJob.BillingAccountFk,
    BillingAssignmentFk = lifecycleJob.BillingAssignmentFk,
    WorkerId            = workerId,
    Summary             = summary,
    Details             = details,
}, cancellationToken);
```

### `sources/Api/Services/CloudWorkerDispatch/CloudWorkerDispatchService.cs` (service, request-response + event-driven)

**Use for:** worker authentication, safe claim snapshots, progress/log/runtime/failure reporting.

**Claim and progress pattern** (`CloudWorkerDispatchService.cs` lines 33-83):

```csharp
var worker = await AuthenticateWorkerAsync(workerId, workerCredential, cancellationToken);
if (worker is null)
    return new CloudWorkerClaimResult(false, null);

var job = await lifecycleJobQueueService.ClaimNextAsync(worker.PrimaryKey, leaseDuration, cancellationToken);
if (job is null)
    return new CloudWorkerClaimResult(true, null);

var message = FormatProgressMessage(payload);
if (!string.IsNullOrWhiteSpace(message))
    await AppendLogAsync(job.GameServerFk, LogLevel.Information, "cloud-worker-progress", message, cancellationToken);
```

**Worker secret boundary** (`CloudWorkerDispatchService.cs` lines 210-232):

```csharp
var worker = await apiDbContext.LifecycleWorkers.AsNoTracking()
    .SingleOrDefaultAsync(q => q.PrimaryKey == normalizedWorkerId, cancellationToken);
if (worker is null
    || worker.Kind != ELifecycleWorkerKind.Cloud
    || !worker.IsEnabled
    || worker.CredentialHash is null
    || worker.CloudSessionFk is null
    || worker.GameServerFk is null
    || !CredentialMatches(worker.CredentialHash, workerCredential))
    return null;
```

**Sensitive configuration masking** (`CloudWorkerDispatchService.cs` lines 266-278):

```csharp
.Select(q => new CloudWorkerConfigurationEntrySnapshotDto
{
    Realm       = q.Realm,
    Path        = q.Path,
    Value       = q.IsSensitive ? Constants.PasswordReplacement : q.Value,
    IsSensitive = q.IsSensitive,
})
```

### DTOs and EF Entities (model/DTO, CRUD + transform)

**Teardown result contract** (`CloudTeardownResultDto.cs` lines 6-36):

```csharp
public record CloudTeardownResultDto
{
    public CloudGameServerSessionDto Session { get; init; } = new();
    public IReadOnlyList<CloudResourceInventoryRecordDto> Resources { get; init; } = [];
    public int DeletedResourceCount { get; init; }
    public int RemainingBillableResourceCount { get; init; }
    public bool CleanupFailed { get; init; }
    public string? FailureSummary { get; init; }
}
```

**Runtime status contract** (`CloudRuntimeStatusDto.cs` lines 8-98):

```csharp
public record CloudRuntimeStatusDto
{
    public CloudGameServerSessionDto? Session { get; init; }
    public LifecycleWorkerDto? Worker { get; init; }
    public LifecycleJobDto? CurrentJob { get; init; }
    public int? CurrentJobProgressPercent { get; init; }
    public string? LastFailureReason { get; init; }
    public bool CanRetry { get; init; }
    public bool CanCleanup { get; init; }
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
}
```

**Session status contract** (`CloudSessionStatusDto.cs` lines 8-103):

```csharp
public record CloudSessionStatusDto
{
    public IReadOnlyList<CloudResourceInventoryRecordDto> Resources { get; init; } = [];
    public IReadOnlyList<CloudWorkerBootstrapCredentialDto> BootstrapCredentials { get; init; } = [];
    public bool IsProvisioned { get; init; }
    public bool NeedsCleanup { get; init; }
    public bool HasOrphanCandidate { get; init; }
    public bool CanProvision { get; init; }
    public bool CanTeardown { get; init; }
    public bool CanRetryCleanup { get; init; }
    public IReadOnlyList<string> RetryableActions { get; init; } = [];
}
```

**Entity ownership:** add or modify persistent Phase 37 state only in `sources/Api/Data/Hosting/` and expose it through `ApiDbContext`; do not return EF entities directly. Generated migrations under `sources/Api/Data/Migrations/**` are not style sources.

### `sources/WebApp/Services/GameServerRepository/*` (repository facade, request-response)

**Use for:** all Razor-facing Phase 37 actions and safe models. Do not call raw browser HTTP from `/servers`.

**Facade method pattern** (`IGameServerRepository.cs` lines 149-176):

```csharp
Task<CloudWorkflowModels.CloudWorkflowActionResult> ProvisionCloudDryRunPlanResultAsync(
    long dryRunPlanId,
    long? acknowledgedDecisionId = null,
    long? approvalRequestId = null,
    CancellationToken cancellationToken = default);

Task<CloudWorkflowModels.CloudWorkflowActionResult> TeardownCloudSessionResultAsync(...);
Task<CloudWorkflowModels.CloudWorkflowActionResult> GetCloudWorkflowStatusResultAsync(...);
```

**Generated-client wrapper pattern** (`GameServerRepositoryImpl.cs` lines 449-493):

```csharp
var requestInfo = Client.CloudInventory.DryRunPlans[dryRunPlanId].Provision.ToPostRequestInformation();
return CloudWorkflowKiotaSender.SendAsync(
    RequestAdapter,
    requestInfo,
    CloudProvisioningResultDto.CreateFromDiscriminatorValue,
    result => CloudWorkflowResultMapper.FromProvisioningResult(result),
    CloudWorkflowModels.CloudWorkflowActionKind.Provisioning,
    "Provision cloud resources",
    acknowledgedDecisionId,
    approvalRequestId,
    cancellationToken);
```

**Runtime status + policy promotion pattern** (`GameServerRepositoryImpl.cs` lines 518-555):

```csharp
var runtimeResult = await CloudWorkflowKiotaSender.SendAsync(... FromRuntimeStatus ...);
var visibility = await GetCloudPolicyVisibilityAsync(gameServerId, cancellationToken);
var policyResult = CloudWorkflowResultMapper.FromPolicyVisibility(visibility);
return runtimeResult with
{
    PolicyGate = policyResult.PolicyGate,
    Cost       = runtimeResult.Cost ?? policyResult.Cost,
    Metadata   = runtimeResult.Metadata.Concat(policyResult.Metadata).ToArray(),
};
```

**Planner note:** if Phase 37 adds owner-safe server-scoped provision/teardown routes, regenerate Kiota narrowly and update these repository methods to use `Client.GameServers[gameServerId]...`; do not hand-edit `sources/WebApp/UtfApi/**`.

### `CloudWorkflowResultMapper.cs` and `CloudWorkflowRedactionGuard.cs` (utility, transform)

**Use for:** all Phase 37 display projection, partial failure, remaining-billable states, and redaction fail-closed behavior.

**Provisioning result mapping** (`CloudWorkflowResultMapper.cs` lines 590-617):

```csharp
return new CloudWorkflowActionResult
{
    State            = CloudWorkflowResultState.Success,
    ActionKind       = CloudWorkflowActionKind.Provisioning,
    ActionName       = Safe(actionName, nameof(CloudWorkflowActionResult.ActionName)),
    StatusLabel      = "Provisioned",
    RequiredNextStep = "Refresh cloud status",
    Resources        = resources,
    Metadata =
    [
        Row("Dry-run plan", result.Plan?.PrimaryKey?.ToString()),
        Row("Session", result.Session?.PrimaryKey?.ToString()),
        Row("Public endpoint", FormatEndpoint(result.PublicEndpointHost, result.PublicEndpointPort)),
    ],
};
```

**Teardown partial-failure mapping** (`CloudWorkflowResultMapper.cs` lines 620-659, 663-692):

```csharp
State = result.CleanupFailed
    ? CloudWorkflowResultState.Failure
    : CloudWorkflowResultState.Success,
StatusLabel = result.CleanupFailed ? "Cleanup failed" : "Teardown completed",
Summary = Safe($"{result.DeletedResourceCount} resources deleted; {result.RemainingBillableResourceCount} billable resources remain.", ...),
RequiredNextStep = result.RemainingBillableResourceCount > 0 ? "Review billable resources" : "Refresh cloud status",
IsRetryable = result.CleanupFailed,
```

**Policy cleanup risk mapping** (`CloudWorkflowResultMapper.cs` lines 778-819):

```csharp
CleanupNeededBillableResourceCount = decision.CleanupNeededBillableResourceCount,
OrphanFindingCount = decision.OrphanFindingCount,
HasCleanupRisk = decision.CleanupNeededBillableResourceCount > 0 || decision.OrphanFindingCount > 0,
SanitizedFailureSummary = Safe(decision.SanitizedFailureSummary, ...),
SanitizedFailureDetails = Safe(decision.SanitizedFailureDetails, ...),
```

**Redaction guard** (`CloudWorkflowRedactionGuard.cs` lines 5-34, 36-78):

```csharp
public static readonly IReadOnlySet<string> ForbiddenFieldNames =
[
    "AdditionalData", "Authorization", "CredentialHash", "DetailsJson",
    "RawJson", "SteamPassword", "TokenReference", "WorkerCredential", "WorkerHash",
];

public static string? ValidateDisplayText(string? value, string fieldName)
{
    if (IsForbiddenFieldName(fieldName))
        throw new InvalidOperationException(...);
    if (ContainsForbiddenDisplayTerm(value))
        throw new InvalidOperationException(...);
    return value;
}
```

### `sources/WebApp/Pages/Servers/Index.razor` (component/page, event-driven + request-response)

**Use for:** `/servers` owner/operator Phase 37 panels. Extend existing panels; do not create a separate owner route.

**Cloud action result panel** (`Index.razor` lines 1120-1211):

```razor
<section class="detail-section cloud-workflow-result-panel">
    <div class="cloud-result-header">
        <div class="section-title">Cloud action result</div>
        <span class="@GetCloudWorkflowResultClass(_selectedCloudWorkflowResult)">
            @FormatCloudWorkflowResultState(_selectedCloudWorkflowResult)
        </span>
    </div>

    @if (IsCloudWorkflowResultBlocked(_selectedCloudWorkflowResult))
    {
        <FluentMessageBar Intent="MessageIntent.Error">
            Cloud result was blocked because it contained a forbidden field. The result was not displayed.
        </FluentMessageBar>
    }
</section>
```

**Runtime panel and cost-stop copy** (`Index.razor` lines 1214-1315):

```razor
<section class="detail-section">
    <div class="section-title">Cloud runtime</div>
    <dl class="meta-grid">
        <dt>Readiness</dt>
        <dd><span class="@GetRuntimeReadyClass(cloudRuntime)">@FormatCloudRuntimeReadiness(cloudRuntime)</span></dd>
        <dt>Current job</dt>
        <dd>@FormatLifecycleJob(cloudRuntime.CurrentJob)</dd>
        <dt>Last failure</dt>
        <dd>@(cloudRuntime.LastFailureReason ?? MissingValue)</dd>
        <dt>Cleanup risk</dt>
        <dd><span class="@GetSelectedCleanupRiskClass()">@FormatSelectedCleanupRisk()</span></dd>
    </dl>
    <p class="risk-note">Provider teardown stops billable resources; Runtime cleanup does not delete provider resources.</p>
</section>
```

**Repository-only execution** (`Index.razor` lines 2391-2450):

```csharp
var acknowledgedDecisionId = GetAcknowledgedDecisionId(continuationGate);
var approvalRequestId = GetApprovalRequestId(continuationGate);
var result = await RunCloudWorkflowActionAsync(action, acknowledgedDecisionId, approvalRequestId);

PendingCloudWorkflowActionKind.Provisioning => GameServerRepository.ProvisionCloudDryRunPlanResultAsync(...);
PendingCloudWorkflowActionKind.Teardown => GameServerRepository.TeardownCloudSessionResultAsync(...);
```

**Continuation identity mapping** (`Index.razor` lines 2869-2910):

```csharp
if (policyGate.ActionKind == CloudWorkflowActionKind.Provisioning
    && policyGate.DryRunPlanId is { } dryRunPlanId)
    return new PendingCloudWorkflowAction(PendingCloudWorkflowActionKind.Provisioning, dryRunPlanId, policyGate.GameServerId);

if (policyGate.ActionKind == CloudWorkflowActionKind.Teardown
    && policyGate.SessionId is { } sessionId)
    return new PendingCloudWorkflowAction(PendingCloudWorkflowActionKind.Teardown, sessionId, policyGate.GameServerId);
```

**Required Phase 37 UI changes:** replace disabled Phase 35 copy at `Index.razor` line 826, rename runtime cleanup button text from `Cleanup` to `Cleanup runtime` (`Index.razor` lines 1305-1312), and use `Teardown provider resources` instead of `Teardown cloud resources` in visible copy and confirmation. Preserve deferred-UAT message at lines 798-800.

### `sources/WebApp/Pages/Servers/Index.razor.css` (component style, transform)

**Use for:** Phase 37 layout and visual states.

**Core page and status tokens** (`Index.razor.css` lines 1-48, 104-140):

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
    padding: 16px;
}

.status-badge {
    border-radius: 999px;
    font-size: 13px;
    gap: 4px;
    padding: 4px 8px;
}
```

**Runtime/action/result styles** (`Index.razor.css` lines 146-190, 210-279):

```css
.meta-grid {
    display: grid;
    gap: 8px 16px;
    grid-template-columns: max-content minmax(0, 1fr);
}

.runtime-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 16px;
}

.cloud-workflow-result-panel {
    border-color: #2563eb;
    display: flex;
    flex-direction: column;
    gap: 16px;
}
```

**Mobile pattern** (`Index.razor.css` lines 532-572):

```css
@media (max-width: 860px) {
    .servers-toolbar,
    .guided-form-heading,
    .guided-actions {
        flex-direction: column;
    }

    .guided-actions fluent-button,
    .cloud-result-actions fluent-button {
        width: 100%;
    }
}
```

## Shared Patterns

### Authentication And Authorization

**Sources:** `GameServersController.cs`, `CloudInventoryController.cs`
**Apply to:** all Phase 37 controller actions.

Use `[Authorize]` at class level, `User.GetDatabaseUserAsync(...)` for actor lookup before mutation, and resource-based claims for owner/operator `/servers` routes. Existing `CloudInventoryController` provider mutation routes are `[Authorize(Claims.Administrative.Server)]`; use them as service-call analogs, not as the owner UX authorization model.

### Policy Gate Before Billable Mutation

**Source:** `CloudInventoryController.cs` lines 101-116 and 164-179
**Apply to:** provisioning, provider teardown, and policy-sensitive lifecycle actions.

Always evaluate the gate with `acknowledgedDecisionId` and `approvalRequestId` before calling provider/lifecycle services. Return `Conflict(gate.ToDto())` when `!gate.CanProceed`.

### Provider Cost-Stop Semantics

**Source:** `CloudTeardownService.cs` lines 106-125, 318-329
**Apply to:** teardown service, repository mapper, `/servers` copy and tests.

Only provider teardown/deletion stops provider billing. Runtime stop and runtime cleanup must stay separate. Success state is allowed only when remaining billable resources are zero; otherwise show cleanup-needed or partial-failure next step.

### Durable Runtime Jobs

**Sources:** `LifecycleJobQueueService.cs`, `CloudWorkerDispatchService.cs`, `GameServersController.cs`
**Apply to:** install, update, start, stop, restart, collect logs, runtime cleanup, retry visibility.

Queue lifecycle jobs, let workers claim/report progress, and project `CurrentJob`, progress, retry, and sanitized failure through `CloudRuntimeStatusDto`. Do not add fire-and-forget runtime operations.

### Redaction And Generated-Client Boundary

**Sources:** `CloudWorkflowRedactionGuard.cs`, `CloudWorkflowResultMapper.cs`, `GameServerRepositoryImpl.cs`
**Apply to:** all WebApp repository models, Razor rendering, tests, and evidence.

Razor consumes `IGameServerRepository` and hand-written safe models only. Do not bind generated DTOs, `AdditionalData`, raw JSON, exception bodies, headers, tokens, worker hashes, Steam credentials, or provider details to UI. Fail closed with the existing redaction-blocked message.

### Testing

**Sources:** `CloudProvisioningServiceTests.cs`, `CloudTeardownServiceTests.cs`, `CloudInventoryVisibilityTests.cs`, `CloudWorkflowRepositoryBoundaryTests.cs`, `PolicyGateApprovalUxBoundaryTests.cs`
**Apply to:** Phase 37 automated tests.

Use SQLite in-memory EF tests with fake Hetzner/provider services for backend behavior; use static source-boundary tests for WebApp repository/UI copy, redaction, generated-client boundaries, and deferred-UAT copy. Do not claim live provider/browser UAT.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| None | - | - | Existing control-plane, repository, UI, and test patterns cover all Phase 37 planned work areas. |

## Ownership And Planning Notes

- API owner/operator route work belongs in `GameServersController` when tied to selected `/servers` workflow and resource-based server auth.
- Admin fleet/provider console routes remain in `CloudInventoryController` for `/servers/cloud`.
- Provider mutation implementation belongs in `CloudProvisioningService` and `CloudTeardownService`; do not duplicate Hetzner calls in controllers or WebApp.
- Runtime execution belongs in lifecycle job services and cloud worker dispatch; do not run runtime work directly from API controllers.
- WebApp integration belongs behind `IGameServerRepository` and `GameServerRepositoryImpl`; regenerate Kiota narrowly for new routes and never hand-edit `sources/WebApp/UtfApi/**`.
- UI work belongs in `sources/WebApp/Pages/Servers/Index.razor` and `.css`; keep `/servers/cloud` admin-only.
- Tests belong in `tests/Api.Tests/CloudProvisioning`, `CloudTeardown`, `CloudInventory`, `GameServerLifecycle`, `CloudWorkerDispatch`, and `WebApp`.

## Metadata

**Analog search scope:** `sources/Api/Controllers`, `sources/Api/Services`, `sources/Api/DTO`, `sources/Api/Data/Hosting`, `sources/WebApp/Services/GameServerRepository`, `sources/WebApp/Pages/Servers`, `tests/Api.Tests`
**Files scanned:** 25 primary analog/work-area files plus planning inputs
**Pattern extraction date:** 2026-06-20
**Deferred boundaries:** Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation/provisioning/teardown UAT, final provider-side billable-resource verification, OPS-02/OPS-03 live UAT, and Phase 30 evidence remain deferred and must not be claimed as passed.

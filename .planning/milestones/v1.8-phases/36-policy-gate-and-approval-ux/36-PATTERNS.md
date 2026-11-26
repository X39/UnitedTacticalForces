# Phase 36: Policy Gate And Approval UX - Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 12
**Analogs found:** 12 / 12

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Controllers/CloudSpendingPoliciesController.cs` | controller | request-response | `sources/Api/Controllers/CloudSpendingPoliciesController.cs` | exact |
| `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs` | service | CRUD / request-response | `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs` | exact |
| `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs` | service | request-response | `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs` | exact |
| `sources/Api/DTO/Payloads/CloudSpendingPolicyApprovalDecisionPayload.cs` | model | request-response | `sources/Api/DTO/Payloads/CloudSpendingPolicyApprovalDecisionPayload.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | service facade | request-response | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | service | request-response / transform | `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` | model | transform | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | utility | transform | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs` | utility | request-response | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs` | exact |
| `sources/WebApp/Pages/Servers/Index.razor` | component | event-driven / request-response | `sources/WebApp/Pages/Servers/Index.razor` | exact |
| `sources/WebApp/Pages/Servers/Index.razor.css` | component style | transform | `sources/WebApp/Pages/Servers/Index.razor.css` | exact |
| `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs` | test | batch / source-boundary | `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | role-match |

## Pattern Assignments

### `sources/Api/Controllers/CloudSpendingPoliciesController.cs` (controller, request-response)

**Analog:** `sources/Api/Controllers/CloudSpendingPoliciesController.cs`

**Imports and controller pattern** (lines 1-27):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.CloudPolicies;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.CloudSpendingPolicies)]
[Authorize]
public class CloudSpendingPoliciesController(...) : ControllerBase
```

**Approval command endpoint pattern** (lines 259-297, 308-346):
```csharp
[ProducesResponseType<CloudSpendingPolicyApprovalRequestDto>(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[HttpPost("approvals/{approvalRequestId:long}/approve", Name = nameof(ApproveApprovalRequestAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> ApproveApprovalRequestAsync(
    [FromRoute] long approvalRequestId,
    [FromBody] CloudSpendingPolicyApprovalDecisionPayload payload,
    CancellationToken cancellationToken)
{
    var user = await GetActorAsync(cancellationToken).ConfigureAwait(false);
    if (user is null)
        return Unauthorized();

    try
    {
        var request = await cloudSpendingPolicyApprovalService
            .ApproveAsync(approvalRequestId, payload, user, cancellationToken)
            .ConfigureAwait(false);
        return Ok(request.ToDto());
    }
    catch (KeyNotFoundException) { return NotFound(); }
    catch (ArgumentOutOfRangeException) { return BadRequest(); }
    catch (ArgumentException) { return BadRequest(); }
    catch (InvalidOperationException) { return BadRequest(); }
}
```

**Actor resolution pattern** (lines 408-421):
```csharp
private async Task<User?> GetActorAsync(CancellationToken cancellationToken)
{
    if (ControllerContext.HttpContext?.User is not { } principal)
        return null;

    try
    {
        return await principal.GetDatabaseUserAsync(apiDbContext, cancellationToken).ConfigureAwait(false);
    }
    catch (InvalidOperationException)
    {
        return null;
    }
}
```

### `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs` (service, CRUD / request-response)

**Analog:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs`

**DI and service shape** (lines 1-18):
```csharp
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.CloudPolicies;

[Scoped<CloudSpendingPolicyApprovalService, ICloudSpendingPolicyApprovalService>]
internal class CloudSpendingPolicyApprovalService(ApiDbContext apiDbContext)
    : ICloudSpendingPolicyApprovalService
```

**Pending reuse and persistence pattern** (lines 71-119):
```csharp
var pending = await apiDbContext.CloudSpendingPolicyApprovalRequests
    .Where(q => q.Status == ECloudSpendingPolicyApprovalStatus.Pending)
    .Where(q => q.ProviderIdentifier == decision.ProviderIdentifier
                && q.ActionKind == decision.ActionKind
                && q.PolicyFk == decision.PolicyFk
                && q.GameServerFk == decision.GameServerFk
                && q.SessionFk == decision.SessionFk
                && q.BillingAccountFk == decision.BillingAccountFk
                && q.BillingAssignmentFk == decision.BillingAssignmentFk
                && q.DryRunPlanFk == dryRunPlanFk
                && q.ResourceInventoryRecordFk == resourceInventoryRecordFk
                && q.TimeStampExpires > now)
    .OrderByDescending(q => q.TimeStampCreated)
    .ThenByDescending(q => q.PrimaryKey)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);
if (pending is not null)
    return pending;
```

**Approve / deny transition pattern** (lines 123-174):
```csharp
ExpireIfPending(request, now);
if (request.Status != ECloudSpendingPolicyApprovalStatus.Pending)
    throw new InvalidOperationException("Only pending approval requests can be approved.");

request.Status            = ECloudSpendingPolicyApprovalStatus.Denied;
request.ResolvedByFk      = denier.PrimaryKey;
request.DenialReason      = SanitizeReason(payload.Reason);
request.TimeStampResolved = now;
request.TimeStampUpdated  = now;
request.AuditDetailsJson  = CreateAuditDetails("denied", denier.PrimaryKey, now);
await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

**Continuation validation pattern** (lines 178-214, 276-298):
```csharp
if (request.Status != ECloudSpendingPolicyApprovalStatus.Approved
    || request.TimeStampExpires <= now
    || !MatchesDecisionScope(request, decision)
    || request.DryRunPlanFk != dryRunPlanFk
    || request.ResourceInventoryRecordFk != resourceInventoryRecordFk)
{
    return null;
}
```

### `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs` (service, request-response)

**Analog:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs`

**Gate entry point pattern** (lines 23-67):
```csharp
public async Task<CloudSpendingPolicyGateResult> EvaluateProvisioningAsync(
    long dryRunPlanId,
    User actor,
    CancellationToken cancellationToken,
    long? acknowledgedDecisionId = null,
    long? approvalRequestId = null)
{
    ArgumentNullException.ThrowIfNull(actor);
    var plan = await apiDbContext.CloudDryRunPlans
        .AsNoTracking()
        .SingleOrDefaultAsync(q => q.PrimaryKey == dryRunPlanId, cancellationToken)
        .ConfigureAwait(false);
    if (plan is null)
        throw new KeyNotFoundException($"Cloud dry-run plan {dryRunPlanId} does not exist.");
```

**Warning/approval gate before mutation pattern** (lines 264-323):
```csharp
if (decision.Decision == ECloudSpendingPolicyDecision.Warn
    && await cloudSpendingPolicyApprovalService
        .IsWarningAcknowledgedAsync(decision, acknowledgedDecisionId, cancellationToken)
        .ConfigureAwait(false))
{
    return new CloudSpendingPolicyGateResult(ECloudSpendingPolicyGateStatus.Allowed, decision, ...);
}

if (decision.Decision == ECloudSpendingPolicyDecision.ApprovalRequired)
{
    var approvedRequest = await cloudSpendingPolicyApprovalService
        .ValidateApprovedAsync(decision, approvalRequestId, dryRunPlanFk, resourceInventoryRecordFk, cancellationToken)
        .ConfigureAwait(false);
    if (approvedRequest is not null)
        return new CloudSpendingPolicyGateResult(ECloudSpendingPolicyGateStatus.Allowed, decision, ..., approvedRequest);

    var pendingRequest = await cloudSpendingPolicyApprovalService
        .CreateOrReturnPendingAsync(decision, actor, dryRunPlanFk, resourceInventoryRecordFk, proposedResourceSummary, cancellationToken)
        .ConfigureAwait(false);
    return new CloudSpendingPolicyGateResult(ECloudSpendingPolicyGateStatus.ApprovalRequired, decision, ..., pendingRequest);
}
```

### `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` (service facade, request-response)

**Analog:** `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`

**Repository-safe facade pattern** (lines 104-130, 183-189):
```csharp
Task<CloudAdminModels.CloudSpendingPolicyVisibility?> GetCloudPolicyVisibilityAsync(
    long gameServerId,
    CancellationToken cancellationToken = default
);

Task<CloudWorkflowModels.CloudWorkflowActionResult> ProvisionCloudDryRunPlanResultAsync(
    long dryRunPlanId,
    long? acknowledgedDecisionId = null,
    long? approvalRequestId = null,
    CancellationToken cancellationToken = default
);

Task<CloudWorkflowModels.CloudWorkflowActionResult> RunCloudLifecycleActionAsync(
    long gameServerId,
    ContractGameServer.ELifecycleJobKind lifecycleJobKind,
    long? acknowledgedDecisionId = null,
    long? approvalRequestId = null,
    CancellationToken cancellationToken = default
);
```

Planner should add approve/deny facade methods here returning safe hand-written WebApp models, not generated `UtfApi` DTOs.

### `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` (service, request-response / transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`

**DI and generated-client repository pattern** (lines 1-15):
```csharp
using System.Collections.Immutable;
using System.Net;
using Microsoft.Kiota.Abstractions;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IGameServerRepository
```

**Safe visibility mapping pattern** (lines 290-307):
```csharp
try
{
    var visibility = await Client.GameServers[gameServerId]
        .CloudPolicyVisibility
        .GetAsync(cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    return visibility is null ? null : CloudAdminRepositoryImpl.MapPolicyVisibility(visibility);
}
catch (ApiException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
{
    return null;
}
```

**Policy-aware generated request pattern** (lines 360-405, 613-636):
```csharp
var requestInfo = Client.CloudInventory
    .DryRunPlans[dryRunPlanId]
    .Provision
    .ToPostRequestInformation();
return CloudWorkflowKiotaSender.SendAsync(
    RequestAdapter,
    requestInfo,
    CloudProvisioningResultDto.CreateFromDiscriminatorValue,
    (result) => CloudWorkflowResultMapper.FromProvisioningResult(result),
    CloudWorkflowModels.CloudWorkflowActionKind.Provisioning,
    "Provision cloud resources",
    acknowledgedDecisionId,
    approvalRequestId,
    cancellationToken
);
```

Do not add raw `HttpClient` calls for new approval commands unless generated Kiota coverage is not available and the planner explicitly scopes a repository-local generated-request equivalent. Existing boundary tests treat raw HTTP in cloud workflow methods as a failure.

### `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs` (model, transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs`

**State taxonomy and safe policy gate model** (lines 5-16, 52-88):
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

public record CloudWorkflowPolicyGateResult
{
    public CloudWorkflowResultState State { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public string RequiredNextStep { get; init; } = string.Empty;
    public long DecisionId { get; init; }
    public long? ApprovalRequestId { get; init; }
    public string ScopeKey { get; init; } = string.Empty;
    public long? GameServerId { get; init; }
    public long? SessionId { get; init; }
    public long? DryRunPlanId { get; init; }
    public long? BillingAccountId { get; init; }
    public long? BillingAssignmentId { get; init; }
    public DateTimeOffset? TimeStampExpires { get; init; }
}
```

Add Phase 36 approval queue/detail safe models in this folder if generated approval DTOs need projection before Razor. Include only allowlisted identity, status, cost, reason, resolver, and timestamp fields from the UI spec.

### `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` (utility, transform)

**Analog:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs`

**Policy gate result projection pattern** (lines 78-107):
```csharp
public static CloudWorkflowModels.CloudWorkflowActionResult FromPolicyGate(
    CloudWorkflowPolicyGateTransport? transport,
    string actionName = "Cloud policy gate")
{
    if (transport is null)
        return Empty(actionName, CloudWorkflowModels.CloudWorkflowActionKind.PolicyEvaluation);

    var policyGate = Map(transport);
    return new CloudWorkflowModels.CloudWorkflowActionResult
    {
        State            = policyGate.State,
        ActionKind       = policyGate.ActionKind,
        ActionName       = Safe(actionName, nameof(CloudWorkflowModels.CloudWorkflowActionResult.ActionName)),
        StatusLabel      = policyGate.StatusLabel,
        Summary          = policyGate.Summary,
        RequiredNextStep = policyGate.RequiredNextStep,
        PolicyGate       = policyGate,
        Metadata         =
        [
            Row("Decision id", policyGate.DecisionId.ToString()),
            Row("Approval request id", policyGate.ApprovalRequestId?.ToString()),
            Row("Scope", policyGate.ScopeKey),
        ],
    };
}
```

**State/next-step mapping pattern** (lines 1071-1103):
```csharp
private static CloudWorkflowModels.CloudWorkflowResultState ToPolicyState(CloudWorkflowPolicyGateTransport transport)
{
    if (transport.IsDenied)
        return CloudWorkflowModels.CloudWorkflowResultState.Denied;
    if (transport.RequiresApproval)
        return CloudWorkflowModels.CloudWorkflowResultState.ApprovalRequired;
    if (transport.RequiresAcknowledgement)
        return CloudWorkflowModels.CloudWorkflowResultState.PolicyWarning;
    return transport.CanProceed
        ? CloudWorkflowModels.CloudWorkflowResultState.PolicyAllow
        : CloudWorkflowModels.CloudWorkflowResultState.Failure;
}
```

### `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs` (utility, request-response)

**Analog:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs`

**Kiota error mapping and continuation identity pattern** (lines 13-18, 32-53, 55-88):
```csharp
private static readonly Dictionary<string, ParsableFactory<IParsable>> ErrorMappings = new()
{
    { "409", CreatePolicyGate },
    { "4XX", CreatePolicyGate },
    { "5XX", CreatePolicyGate },
};

AddContinuationQueryParameters(requestInformation, acknowledgedDecisionId, approvalRequestId);

try
{
    var result = await requestAdapter
        .SendAsync(requestInformation, successParserFactory, ErrorMappings, cancellationToken)
        .ConfigureAwait(false);
    return successMapper(result);
}
catch (CloudWorkflowPolicyGateTransport ex) when (ex.ResponseStatusCode == 409 || ex.Decision.PrimaryKey != 0)
{
    return CloudWorkflowResultMapper.FromPolicyGate(ex, actionName);
}
catch (ApiException ex)
{
    return CloudWorkflowResultMapper.FromSanitizedFailure(ex.ResponseStatusCode, actionKind, actionName);
}
catch (InvalidOperationException)
{
    return CloudWorkflowResultMapper.FromRedactionFailure(actionKind, actionName);
}
```

### `sources/WebApp/Pages/Servers/Index.razor` (component, event-driven / request-response)

**Analog:** `sources/WebApp/Pages/Servers/Index.razor`

**Panel structure and deferred UAT copy** (lines 584-623, 797-823):
```razor
<section class="detail-section dry-run-plan-review-panel">
    <div class="dry-run-review-header">
        <div>
            <div class="section-title">Dry-run plan review</div>
            <p class="cloud-result-copy">
                Review planned resources, firewall rules, assumptions, validation, and cost estimates before any provider action.
            </p>
        </div>
        <span class="@GetDryRunReviewClass(_selectedDryRunPlanReview)">
            @FormatDryRunReviewState(_selectedDryRunPlanReview)
        </span>
    </div>
    ...
    <FluentMessageBar Intent="MessageIntent.Info">
        Live Hetzner, Steam, Arma, authenticated browser UAT, provider mutation, provisioning, teardown, and Phase 30 evidence are deferred.
    </FluentMessageBar>
    <div class="guided-actions dry-run-review-actions">
        <FluentButton Disabled="true">Review policy decision</FluentButton>
    </div>
</section>
```

**Result rendering pattern** (lines 826-917):
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
    ...
    <dl class="meta-grid cloud-result-meta">
        @foreach (var row in GetCloudWorkflowResultRows(_selectedCloudWorkflowResult))
        {
            <dt>@row.Label</dt>
            <dd>@row.Value</dd>
        }
    </dl>
    <div class="guided-actions cloud-result-actions">
        @if (CanContinueSelectedCloudWorkflow)
        {
            <FluentButton IconStart="@(new Icons.Regular.Size20.PlayCircle())"
                          Appearance="Appearance.Accent"
                          OnClick="ContinueSelectedCloudWorkflowAsync">
                @FormatContinueCloudWorkflowAction()
            </FluentButton>
        }
    </div>
</section>
```

**Continuation identity pattern** (lines 1829-1896, 2059-2149):
```csharp
private async Task ContinueSelectedCloudWorkflowAsync()
{
    if (_pendingCloudWorkflowAction is not { } action || GetSelectedContinuationGate() is not { } gate)
        return;

    await ExecuteCloudWorkflowActionAsync(action, gate).ConfigureAwait(false);
}

private static bool CanContinuePolicyGate(CloudWorkflowModels.CloudWorkflowPolicyGateResult? gate)
    => gate is { State: CloudWorkflowModels.CloudWorkflowResultState.PolicyWarning, DecisionId: > 0 }
       || gate is
       {
           State: CloudWorkflowModels.CloudWorkflowResultState.ApprovalRequired,
           ApprovalRequestId: not null,
       };
```

**Status text/badge helpers** (lines 2721-2744):
```csharp
private static string GetCloudWorkflowResultClass(CloudWorkflowModels.CloudWorkflowActionResult? result)
    => result?.State switch
    {
        CloudWorkflowModels.CloudWorkflowResultState.Success
            or CloudWorkflowModels.CloudWorkflowResultState.PolicyAllow => "status-badge status-success",
        CloudWorkflowModels.CloudWorkflowResultState.PolicyWarning => "status-badge status-info",
        CloudWorkflowModels.CloudWorkflowResultState.ApprovalRequired
            or CloudWorkflowModels.CloudWorkflowResultState.ValidationBlocked => "status-badge status-warning",
        CloudWorkflowModels.CloudWorkflowResultState.Denied
            or CloudWorkflowModels.CloudWorkflowResultState.Failure => "status-badge status-danger",
        _ => "status-badge status-neutral",
    };
```

### `sources/WebApp/Pages/Servers/Index.razor.css` (component style, transform)

**Analog:** `sources/WebApp/Pages/Servers/Index.razor.css`

**Status/meta/panel/table pattern** (lines 104-161, 186-285):
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

.meta-grid dd {
    margin: 0;
    min-width: 0;
    overflow-wrap: anywhere;
}

.cloud-workflow-result-panel,
.dry-run-plan-review-panel {
    border-color: #2563eb;
    display: flex;
    flex-direction: column;
    gap: 16px;
    grid-column: 1 / -1;
}
```

**Actions and responsive pattern** (lines 423-482):
```css
.guided-actions {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}

@media (max-width: 860px) {
    .guided-actions {
        align-items: stretch;
        flex-direction: column;
    }

    .cloud-result-header,
    .dry-run-review-header {
        flex-direction: column;
    }

    .guided-actions fluent-button,
    .cloud-result-actions fluent-button,
    .dry-run-review-actions fluent-button {
        width: 100%;
    }
}
```

### `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs` (test, batch / source-boundary)

**Analog:** `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`

**Boundary marker pattern** (lines 8-53):
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
    "RawJson",
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

**Razor boundary/source scan pattern** (lines 84-123, 369-393):
```csharp
[Fact]
public void ServersOwnerWorkflow_DoesNotUseGeneratedBuildersOrRawHttp()
{
    var failures = FindMarkerHits("sources/WebApp/Pages/Servers/Index.razor", ServersBoundaryMarkers);

    AssertNoFailures(failures);
}

private static IEnumerable<string> FindMarkerHits(
    string repositoryRelativePath,
    IEnumerable<string> markers,
    bool lineMustContainResultMarkup = false)
{
    var lines = ReadRepositoryFile(repositoryRelativePath).Split(["\r\n", "\n"], StringSplitOptions.None);
    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        foreach (var marker in markers)
        {
            if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                yield return $"{repositoryRelativePath}:{i + 1}: found marker '{marker}' in `{line.Trim()}`.";
        }
    }
}
```

**Approval service test pattern** (source: `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs`, lines 87-143, 145-185, 187-290):
```csharp
[Fact]
public async Task ApproveAsync_TransitionsPendingAndValidatesApprovedScope()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await using var dbContext = await CreateDbContextAsync(connection);
    var context = await CreateReadyApprovalContextAsync(dbContext);
    var service = new CloudSpendingPolicyApprovalService(dbContext);
    ...
    Assert.NotNull(await service.ValidateApprovedAsync(...));
    Assert.Null(await service.ValidateApprovedAsync(wrongScopeDecision, approved.PrimaryKey, ...));
}
```

## Shared Patterns

### Authorization And Actor Resolution

**Source:** `sources/Api/Controllers/CloudSpendingPoliciesController.cs`
**Apply to:** API approval command endpoints and tests.

Use `[Authorize(Claims.Administrative.Server)]` on approver endpoints, resolve the actor with `GetDatabaseUserAsync`, return `Unauthorized()` if absent, and translate `KeyNotFoundException` to `NotFound()` plus validation exceptions to `BadRequest()`.

### Durable Scope Matching

**Source:** `sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs`
**Apply to:** warning acknowledgement, approval continuation, service tests, UI enablement.

Server-side authority is exact persisted scope matching: provider, action kind, policy, game server, session, billing account, billing assignment, dry-run plan, resource inventory record, status, and expiration. UI should display these fields and pass only the displayed `acknowledgedDecisionId` or `approvalRequestId`; it must not be treated as the authority.

### Repository Boundary

**Source:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`, `CloudWorkflowKiotaSender.cs`, `CloudWorkflowResultMapper.cs`
**Apply to:** all WebApp policy approval commands and `/servers` UI.

Razor consumes `IGameServerRepository` and safe hand-written models. Generated `sources/WebApp/UtfApi/**` request builders stay behind repository methods. `CloudWorkflowKiotaSender` owns Kiota request information, error mappings, continuation query parameters, sanitized failures, and redaction-failure conversion.

### Redaction

**Source:** `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs`
**Apply to:** safe projection models, mapper methods, Razor rendering, boundary tests.

```csharp
public static string? ValidateDisplayText(string? value, string fieldName)
{
    if (IsForbiddenFieldName(fieldName))
        throw new InvalidOperationException($"Cloud workflow field '{fieldName}' is not displayable.");

    if (ContainsForbiddenDisplayTerm(value))
        throw new InvalidOperationException($"Cloud workflow field '{fieldName}' contains forbidden display text.");

    return value;
}
```

### Generated Kiota Boundary

**Source:** `sources/WebApp/UtfApi/GameServers/Item/CloudPolicyVisibility/CloudPolicyVisibilityRequestBuilder.cs`
**Apply to:** generated client refresh or generated-equivalent approval command routing.

Generated request builders are marked `// <auto-generated/>`, inherit `BaseRequestBuilder`, set URL templates in constructors, expose `GetAsync`/`PostAsync` methods, and create `RequestInformation` with method, URL template, path parameters, request body, and `Accept` headers. Do not hand-edit generated files as source style. If approval routes are missing, plan either a generated refresh or a tightly scoped equivalent outside Razor.

### UI Layout

**Source:** `sources/WebApp/Pages/Servers/Index.razor` and `.razor.css`
**Apply to:** policy gate panel, approval queue/detail, approver actions.

Use `section.detail-section`, `FluentMessageBar`, `dl.meta-grid`, `.data-table`, `.guided-actions`, existing `status-badge` classes, and mobile full-width action buttons. Keep deferred-UAT copy present and do not claim manual/authenticated/browser/live-provider/Steam/Arma/Hetzner/provisioning/teardown/Phase 30 UAT as passed.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| None | - | - | Existing codebase has direct analogs for each Phase 36 file category. |

## Metadata

**Analog search scope:** `sources/Api/Controllers`, `sources/Api/Services/CloudPolicies`, `sources/Api/DTO/Payloads`, `sources/WebApp/Services/GameServerRepository`, `sources/WebApp/Pages/Servers`, `sources/WebApp/UtfApi`, `tests/Api.Tests/CloudPolicies`, `tests/Api.Tests/WebApp`
**Files scanned:** 17
**Pattern extraction date:** 2026-06-20
**Manual/live UAT:** Deferred by phase instruction; not required and not claimed.

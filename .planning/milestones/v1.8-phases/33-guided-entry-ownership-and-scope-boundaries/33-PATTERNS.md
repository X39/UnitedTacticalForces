# Phase 33: Guided Entry, Ownership, And Scope Boundaries - Pattern Map

**Mapped:** 2026-06-19
**Files analyzed:** 14 likely new/modified files
**Analogs found:** 14 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Controllers/GameServersController.cs` | controller | request-response, CRUD | `sources/Api/Controllers/GameServersController.cs` | exact |
| `sources/Api/Controllers/CloudPlanningController.cs` | controller | request-response, transform | `sources/Api/Controllers/CloudPlanningController.cs` | exact |
| `sources/Api/Services/GameServerLifecycle/IBillingOwnershipService.cs` | service | CRUD | `sources/Api/Services/GameServerLifecycle/IBillingOwnershipService.cs` | exact |
| `sources/Api/Services/GameServerLifecycle/BillingOwnershipService.cs` | service | CRUD, audit event-driven | `sources/Api/Services/GameServerLifecycle/BillingOwnershipService.cs` | exact |
| `sources/Api/DTO/GuidedCloudEntryOptionsDto.cs` | model | request-response | `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs` | role-match |
| `sources/Api/DTO/GuidedCloudEntryReadinessDto.cs` | model | request-response, transform | `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs` | role-match |
| `sources/Api/DTO/Payloads/GuidedCloudEntryReadinessPayload.cs` | model | request-response | `sources/Api/DTO/Payloads/Arma3CloudDryRunPlanRequestPayload.cs` | exact |
| `sources/Api/DTO/Payloads/GameServerCreationPayload.cs` | model | request-response | `sources/Api/DTO/Payloads/GameServerCreationPayload.cs` | exact |
| `sources/WebApp/Pages/Servers/Index.razor` | component | request-response, event-driven UI | `sources/WebApp/Pages/Servers/Index.razor` | exact |
| `sources/WebApp/Pages/Servers/Index.razor.css` | component style | transform | `sources/WebApp/Pages/Servers/Index.razor.css` | exact |
| `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | service facade | request-response | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | service facade | request-response, CRUD | `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | exact |
| `sources/WebApp/Services/GameServerRepository/GuidedCloudEntry*.cs` | model | request-response | `sources/WebApp/Services/GameServerRepository/BillingAccountInfo.cs` and adjacent info records | role-match |
| `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` | test | request-response, CRUD | `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs` | exact |

## Pattern Assignments

### `sources/Api/Controllers/GameServersController.cs` (controller, request-response/CRUD)

**Analog:** `sources/Api/Controllers/GameServersController.cs`

**Imports and controller shape** (lines 1-28):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.GameServers)]
[Authorize]
public class GameServersController : ControllerBase
```

**Resource-scoped listing pattern** (lines 1777-1805):
```csharp
private async IAsyncEnumerable<GameServer> GetUserAccessibleDatabaseGameServersAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var allServers = _apiDbContext.GameServers
        .Where(q => q.IsActive)
        .OrderBy(q => q.Title)
        .ToAsyncEnumerable();

    if (User.HasClaim(Claims.Administrative.All, string.Empty)
        || User.HasClaim(Claims.Administrative.Server, string.Empty))
    {
        await foreach (var server in allServers.WithCancellation(cancellationToken))
            yield return server;
    }
    else
    {
        await foreach (var server in allServers.WithCancellation(cancellationToken))
        {
            var hasAnyServerClaim = User.HasClaim(claim
                => claim.Value == server.PrimaryKey.ToString()
                   && (claim.Type == Claims.ResourceBased.Server.All
                       || claim.Type.StartsWith(Claims.ResourceBased.Server.All + ":")));
            if (hasAnyServerClaim)
                yield return server;
        }
    }
}
```

**Active billing owner endpoint pattern** (lines 353-368):
```csharp
[ProducesResponseType<GameServerBillingAssignmentDto>(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[HttpGet("{gameServerId:long}/billing-owner", Name = nameof(GetActiveBillingOwnerAsync))]
[Authorize($"{Claims.ResourceBased.Server.All}:*")]
public async Task<IActionResult> GetActiveBillingOwnerAsync(
    [FromRoute] long gameServerId,
    CancellationToken cancellationToken)
{
    if (!await _apiDbContext.GameServers.AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
        return NotFound();
    var activeAssignment = await _billingOwnershipService
        .GetActiveAssignmentAsync(gameServerId, cancellationToken)
        .ConfigureAwait(false);
    return activeAssignment is null
        ? NotFound()
        : Ok(activeAssignment.ToDto());
}
```

**Creation side-effect boundary to preserve or avoid intentionally** (lines 1416-1478):
```csharp
[HttpPost("create/{controllerIdentifier}", Name = nameof(CreateGameServerAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> CreateGameServerAsync(
    [FromRoute] string controllerIdentifier,
    [FromBody] GameServerCreationPayload payload,
    CancellationToken cancellationToken)
{
    var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
    if (user is null)
        return Forbid();
    if (!controllerIdentifiers.Contains(controllerIdentifier)
        || string.IsNullOrWhiteSpace(payload.Title))
        return BadRequest();

    await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
    await _apiDbContext.GameServers.AddAsync(gameServer, cancellationToken);
    await _apiDbContext.SaveChangesAsync(cancellationToken);
    await _billingOwnershipService.AssignOwnerAsync(...).ConfigureAwait(false);
    var result = await EnqueueLifecycleJobAsync(gameServer, user, ELifecycleJobKind.Install, cancellationToken);
    if (result is OkObjectResult)
        await dbTransaction.CommitAsync(cancellationToken);
    return result;
}
```

Planner note: if Phase 33 needs "create new Arma 3 for planning" before billable/lifecycle work, do not accidentally reuse this route without accepting the install job side effect.

### `sources/Api/Controllers/CloudPlanningController.cs` (controller, request-response/transform)

**Analog:** `sources/Api/Controllers/CloudPlanningController.cs`

**Route, auth, and dependency pattern** (lines 15-20):
```csharp
[ApiController]
[Route(Constants.Routes.GameServers + "/{gameServerId:long}/cloud-dry-run-plans")]
[Authorize]
public class CloudPlanningController(
    ApiDbContext apiDbContext,
    IArma3CloudDryRunPlannerService arma3CloudDryRunPlannerService)
    : ControllerBase
```

**Request validation and no-provider dependency pattern** (lines 32-80):
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

    if (payload.GameServerFk is not null && payload.GameServerFk != gameServerId)
        return BadRequest();

    if (!await apiDbContext.GameServers.AnyAsync(
            q => q.PrimaryKey == gameServerId && q.IsActive,
            cancellationToken).ConfigureAwait(false))
        return NotFound();

    try
    {
        var result = await arma3CloudDryRunPlannerService
            .CreatePlanAsync(gameServerId, payload with { GameServerFk = gameServerId }, user, cancellationToken)
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
}
```

### `sources/Api/Services/GameServerLifecycle/BillingOwnershipService.cs` (service, CRUD/audit)

**Analog:** `sources/Api/Services/GameServerLifecycle/BillingOwnershipService.cs`

**DI and service shape** (lines 1-17):
```csharp
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<BillingOwnershipService, IBillingOwnershipService>]
internal class BillingOwnershipService(
    ApiDbContext apiDbContext,
    IHostingAuditService hostingAuditService)
    : IBillingOwnershipService
```

**Create active user-owned billing account** (lines 20-63):
```csharp
public async Task<BillingAccount> CreateUserBillingAccountAsync(
    string displayName,
    Guid ownerUserFk,
    User actor,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(displayName))
        throw new ArgumentException("Billing account display name is required.", nameof(displayName));
    var ownerExists = await apiDbContext.Users
        .AnyAsync(q => q.PrimaryKey == ownerUserFk, cancellationToken)
        .ConfigureAwait(false);
    if (!ownerExists)
        throw new InvalidOperationException($"Billing account owner user {ownerUserFk} does not exist.");

    var billingAccount = new BillingAccount { IsActive = true, OwnerUserFk = ownerUserFk, CreatedByFk = actor.PrimaryKey };
    apiDbContext.BillingAccounts.Add(billingAccount);
    await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    await hostingAuditService.AppendAsync(new HostingAuditRecord { Kind = EHostingAuditEventKind.BillingAccountCreated }, cancellationToken)
        .ConfigureAwait(false);
    return billingAccount;
}
```

**Assign owner, close previous active owner, audit** (lines 67-135):
```csharp
if (!billingAccount.IsActive)
    throw new InvalidOperationException($"Billing account {billingAccount.PrimaryKey} is inactive.");

var activeAssignments = await apiDbContext.GameServerBillingAssignments
    .Where(q => q.GameServerFk == gameServer.PrimaryKey)
    .Where(q => q.TimeStampUnassigned == null)
    .ToListAsync(cancellationToken)
    .ConfigureAwait(false);
if (activeAssignments.Count > 1)
    throw new InvalidOperationException($"Game server {gameServer.PrimaryKey} has more than one active billing owner.");

var currentAssignment = activeAssignments.SingleOrDefault();
if (currentAssignment?.BillingAccountFk == billingAccount.PrimaryKey)
    return currentAssignment;

if (currentAssignment is not null)
{
    currentAssignment.TimeStampUnassigned = now;
    currentAssignment.UnassignedByFk      = actor.PrimaryKey;
    currentAssignment.UnassignmentReason  = NormalizeReason(reason);
}

apiDbContext.GameServerBillingAssignments.Add(assignment);
await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
await hostingAuditService.AppendAsync(new HostingAuditRecord { Kind = EHostingAuditEventKind.BillingOwnerAssigned }, cancellationToken)
    .ConfigureAwait(false);
```

**Active assignment invariant** (lines 139-170 and `ApiDbContext.cs` lines 101-105):
```csharp
public async Task<GameServerBillingAssignment?> GetActiveAssignmentAsync(long gameServerId, CancellationToken cancellationToken)
{
    var activeAssignments = await apiDbContext.GameServerBillingAssignments
        .Include(q => q.BillingAccount)
        .Where(q => q.GameServerFk == gameServerId)
        .Where(q => q.TimeStampUnassigned == null)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
    if (activeAssignments.Count == 0)
        return null;
    if (activeAssignments.Count > 1)
        throw new InvalidOperationException($"Game server {gameServerId} has more than one active billing owner.");
    return activeAssignments.Single().BillingAccount?.IsActive == true ? activeAssignments.Single() : null;
}

modelBuilder.Entity<GameServerBillingAssignment>()
    .HasIndex(q => q.GameServerFk)
    .HasDatabaseName("IX_GameServerBillingAssignments_GameServerFk_Active")
    .IsUnique()
    .HasFilter("\"TimeStampUnassigned\" IS NULL");
```

### `sources/Api/DTO/Payloads/GuidedCloudEntryReadinessPayload.cs` (model, request-response)

**Analog:** `sources/Api/DTO/Payloads/Arma3CloudDryRunPlanRequestPayload.cs`

**Record payload with XML docs and init properties** (lines 1-77):
```csharp
namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Payload used to request an Arma 3 cloud dry-run plan.
/// </summary>
public record Arma3CloudDryRunPlanRequestPayload
{
    public long? GameServerFk { get; init; }
    public long SelectedBillingAccountFk { get; init; }
    public long SelectedBillingAssignmentFk { get; init; }
    public string? ServerTypeName { get; init; }
    public string? LocationName { get; init; }
    public string? DatacenterName { get; init; }
    public string? ImageName { get; init; }
    public int? GamePort { get; init; }
    public int? SteamQueryPort { get; init; }
    public int? SteamPort { get; init; }
    public int? RconPort { get; init; }
    public int? StorageGigabytes { get; init; }
    public bool ExposeSsh { get; init; } = true;
    public string? AdministrativeSourceCidr { get; init; }
}
```

Use this shape for any non-mutating readiness payload; include modpack/runtime basics explicitly if readiness cannot infer them from the selected server.

### `sources/WebApp/Pages/Servers/Index.razor` (component, request-response/event-driven UI)

**Analog:** `sources/WebApp/Pages/Servers/Index.razor`

**Route, imports, injected repositories** (lines 1-24):
```razor
@page "/servers"
@using X39.UnitedTacticalForces.WebApp.Api.Models
@using X39.UnitedTacticalForces.WebApp.Services
@using X39.UnitedTacticalForces.WebApp.Services.GameServerRepository
@inject IStringLocalizer<Language> Localizer
@inject IGameServerRepository GameServerRepository
@inject BaseUrl BaseUrl
@inject MeService Me
```

**Toolbar and primary action pattern** (lines 28-61):
```razor
<div class="servers-page">
    <header class="servers-toolbar">
        <div>
            <h1>@Localizer[nameof(Language.Pages_GameServers_Title)]</h1>
            <div class="server-counts">
                <span>@_serverRows.Count servers</span>
                <span>@_serverRows.Count(q => q.LatestJob is { Status: ... }) active jobs</span>
            </div>
        </div>
        <div class="toolbar-actions">
            <FluentButton IconStart="@(new Icons.Regular.Size20.ArrowSyncCircle())"
                          Disabled="@_isLoading"
                          OnClick="@(() => RefreshAsync())">
                Refresh
            </FluentButton>
            <FluentButton IconStart="@(new Icons.Regular.Size24.New())"
                          Appearance="Appearance.Accent"
                          OnClick="@(() => _showCreateForm = !_showCreateForm)">
                @Localizer[nameof(Language.Pages_GameServers_CreateNewGameServer)]
            </FluentButton>
        </div>
    </header>
```

**Message bars, form grid, and disabled CTA** (lines 64-120):
```razor
@if (_errorMessage is not null)
{
    <FluentMessageBar Intent="MessageIntent.Error">@_errorMessage</FluentMessageBar>
}

<section class="detail-section create-section">
    <div class="section-title">Create server</div>
    <div class="form-grid">
        <label>
            <span>Controller</span>
            <select @bind="_selectedControllerIdentifier">...</select>
        </label>
        <label>
            <span>Billing owner</span>
            <select @bind="_selectedBillingAccountFk">...</select>
        </label>
        <label>
            <span>Title</span>
            <input @bind="_newServerTitle" maxlength="120"/>
        </label>
        <FluentButton IconStart="@(new Icons.Regular.Size20.PlayCircle())"
                      Appearance="Appearance.Accent"
                      Disabled="@(!CanSubmitCreateServer)"
                      OnClick="CreateServerAsync">
            Create
        </FluentButton>
    </div>
</section>
```

**Server grid selection pattern** (lines 132-221):
```razor
<FluentDataGrid TGridItem="ServerRow"
                Items="@_serverRows.AsQueryable()"
                AutoFit="true"
                GenerateHeader="@GenerateHeaderOption.Sticky">
    <ChildContent>
        <TemplateColumn Title="" Width="64px">
            <FluentButton IconStart="@(new Icons.Regular.Size24.Open())"
                          Title="Open"
                          Disabled="@(context.PrimaryKey == _selectedServer?.PrimaryKey)"
                          OnClick="@(() => SelectServerAsync(context))"/>
        </TemplateColumn>
        <TemplateColumn Title="Billing owner">
            @FormatActiveOwner(context.ActiveBillingOwner)
        </TemplateColumn>
    </ChildContent>
</FluentDataGrid>
```

**State loading and display authorization pattern** (lines 630-713):
```csharp
private bool CanViewServers => Me.HasBaseClaimOrServerAdmin(Claims.ResourceBased.Server.All);
private bool CanManageServers => Me.HasClaimOrServerAdmin();

protected override async Task OnInitializedAsync()
{
    await RefreshAsync().ConfigureAwait(false);
}

private async Task RefreshAsync(bool clearMessages = true)
{
    _isLoading = true;
    try
    {
        if (!CanViewServers)
        {
            _serverRows.Clear();
            _selectedServer = null;
            return;
        }

        if (CanManageServers)
        {
            _billingAccounts = await GameServerRepository.GetBillingAccountsAsync().ConfigureAwait(false);
            _controllerIdentifiers = await GameServerRepository.GetGameServerControllersAsync().ConfigureAwait(false);
        }

        var gameServers = await GameServerRepository.GetGameServersAsync().ConfigureAwait(false);
        foreach (var gameServerInfo in gameServers)
        {
            row.ActiveBillingOwner = await GameServerRepository.GetActiveBillingOwnerAsync(primaryKey).ConfigureAwait(false);
            row.LifecycleJobs = await GameServerRepository.GetLifecycleJobsAsync(primaryKey).ConfigureAwait(false);
        }
        await LoadSelectedServerDetailsAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
    }
    finally
    {
        _isLoading = false;
    }
}
```

**Button permission helpers** (lines 870-890):
```csharp
private bool CanStartStop(ServerRow serverRow)
    => HasServerPermission(Claims.ResourceBased.Server.StartStop, serverRow);

private bool HasServerPermission(string claim, ServerRow serverRow)
{
    if (!Me.IsAuthenticated || serverRow.PrimaryKey <= 0)
        return false;

    var serverId = serverRow.PrimaryKey.ToString();
    return Me.HasResourceClaimOrServerAdmin(Claims.ResourceBased.Server.All, serverId)
```

Planner note: treat `MeService` checks as display logic only. API endpoints must enforce owner/server scope.

### `sources/WebApp/Pages/Servers/Index.razor.css` (component style, transform)

**Analog:** `sources/WebApp/Pages/Servers/Index.razor.css`

**Page shell, toolbar, form, panel, badges, responsive collapse** (lines 1-61, 104-161, 209-218):
```css
.servers-page {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding: 16px;
}

.servers-toolbar {
    align-items: flex-start;
    display: flex;
    gap: 16px;
    justify-content: space-between;
}

.detail-section {
    border: 1px solid var(--neutral-stroke-rest);
    border-radius: 8px;
    overflow: auto;
    padding: 12px;
}

.form-grid {
    align-items: end;
    display: grid;
    gap: 12px;
    grid-template-columns: minmax(180px, 1fr) minmax(180px, 1fr) minmax(180px, 1fr) auto;
}

.status-badge {
    align-items: center;
    border: 1px solid transparent;
    border-radius: 999px;
    display: inline-flex;
    font-size: 0.8rem;
    gap: 4px;
    line-height: 1.2;
    padding: 3px 8px;
    white-space: nowrap;
}

.meta-grid {
    display: grid;
    gap: 7px 12px;
    grid-template-columns: max-content minmax(0, 1fr);
    margin: 0;
}

@media (max-width: 860px) {
    .servers-toolbar {
        flex-direction: column;
    }

    .details-grid,
    .form-grid,
    .secondary-form {
        grid-template-columns: 1fr;
    }
}
```

### `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` (service facade, request-response)

**Analog:** `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`

**Interface method grouping pattern** (lines 27-119):
```csharp
Task<IReadOnlyCollection<GameServerInfoDto>> GetGameServersAsync(CancellationToken cancellationToken = default);

Task<IReadOnlyCollection<BillingAccountInfo>> GetBillingAccountsAsync(CancellationToken cancellationToken = default);

Task<GameServerBillingAssignmentInfo?> GetActiveBillingOwnerAsync(
    long gameServerId,
    CancellationToken cancellationToken = default);

Task<GameServerBillingAssignmentInfo> AssignBillingOwnerAsync(
    long gameServerId,
    long billingAccountFk,
    string? reason,
    CancellationToken cancellationToken = default);

Task<LifecycleJobInfo> CreateGameServerAsync(
    string controllerIdentifier,
    string title,
    long billingAccountFk,
    CancellationToken cancellationToken = default);
```

Add guided-entry/readiness methods here rather than calling generated clients or raw `HttpClient` directly from Razor.

### `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` (service facade, request-response/CRUD)

**Analog:** `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`

**DI and generated client usage** (lines 1-14, 63-72):
```csharp
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Kiota.Abstractions;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IGameServerRepository
{
    public async Task<IReadOnlyCollection<GameServerInfoDto>> GetGameServersAsync(CancellationToken cancellationToken = default)
    {
        var gameServers = await Client.GameServers.All.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return gameServers?.ToImmutableArray() ?? [];
    }
}
```

**404-to-null pattern** (lines 149-166 and 220-236):
```csharp
public async Task<GameServerBillingAssignmentInfo?> GetActiveBillingOwnerAsync(
    long gameServerId,
    CancellationToken cancellationToken = default)
{
    try
    {
        return await HttpClient.GetFromJsonAsync<GameServerBillingAssignmentInfo>(
                BaseUrl.ResolveApiUrl($"game-servers/{gameServerId}/billing-owner"),
                cancellationToken)
            .ConfigureAwait(false);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
}
```

**POST helper pattern** (lines 319-373 and 556-588):
```csharp
public async Task<GameServerBillingAssignmentInfo> AssignBillingOwnerAsync(...)
{
    return await PostJsonRequiredAsync<BillingOwnerAssignmentInfo, GameServerBillingAssignmentInfo>(
            $"game-servers/{gameServerId}/billing-owner",
            new BillingOwnerAssignmentInfo { BillingAccountFk = billingAccountFk, Reason = reason },
            cancellationToken)
        .ConfigureAwait(false);
}

private async Task<TResponse> PostJsonRequiredAsync<TBody, TResponse>(
    string path,
    TBody body,
    CancellationToken cancellationToken)
{
    var response = await HttpClient
        .PostAsJsonAsync(BaseUrl.ResolveApiUrl(path), body, cancellationToken)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await ReadRequiredAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
}
```

### `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs` (test, request-response/CRUD)

**Analog:** `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs`

**Controller metadata authorization test** (lines 35-51):
```csharp
[Fact]
public void CreateArma3DryRunPlanAsync_RequiresResourceConfigurationPolicy()
{
    var controllerType = typeof(CloudPlanningController);
    var routeAttribute = Assert.Single(controllerType.GetCustomAttributes<RouteAttribute>());
    Assert.Contains("{gameServerId:long}", routeAttribute.Template, StringComparison.Ordinal);

    var method = controllerType.GetMethod(nameof(CloudPlanningController.CreateArma3DryRunPlanAsync));
    var authorizeAttributes = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
        .Cast<AuthorizeAttribute>()
        .ToArray();
    Assert.Contains(authorizeAttributes, q => q.Policy == Claims.ResourceBased.Server.Configuration);
}
```

**SQLite API test setup and no-side-effects assertion** (lines 108-130, 297-398):
```csharp
[Fact]
public async Task CreateArma3DryRunPlanAsync_ReturnsBadRequestForSelectedOwnerMismatchWithoutSideEffects()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var context = await CreateReadyContextAsync(connection);
    var controller = CreatePlanningController(context);

    var actionResult = await controller.CreateArma3DryRunPlanAsync(...);

    Assert.IsType<BadRequestResult>(actionResult);
    Assert.False(await context.DbContext.CloudDryRunPlans.AnyAsync());
    Assert.False(await context.DbContext.CloudGameServerSessions.AnyAsync());
    Assert.False(await context.DbContext.CloudResourceInventoryRecords.AnyAsync());
    Assert.False(await context.DbContext.CloudCostEstimates.AnyAsync());
}

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

**Provider mutation guard** (lines 250-262, 599-615):
```csharp
[Fact]
public void CloudPlanningApi_DoesNotDependOnProviderClientOrHttpClient()
{
    var constructorParameterTypes = typeof(CloudPlanningController)
        .GetConstructors()
        .SelectMany(q => q.GetParameters())
        .Select(q => q.ParameterType)
        .ToArray();

    Assert.DoesNotContain(typeof(IHetznerCloudClient), constructorParameterTypes);
    Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameterTypes);
    Assert.DoesNotContain(typeof(HttpClient), constructorParameterTypes);
}

private sealed class ThrowingCloudProvisioningService : ICloudProvisioningService
{
    public Task<CloudProvisioningResultDto> ProvisionDryRunPlanAsync(...)
        => throw new NotSupportedException("API visibility tests must not provision provider resources.");
}
```

### `tests/Api.Tests/GameServerLifecycle/BillingOwnershipAndAuditTests.cs` (test, CRUD/audit)

**Analog:** `tests/Api.Tests/GameServerLifecycle/BillingOwnershipAndAuditTests.cs`

**Ownership transfer and audit pattern** (lines 18-79):
```csharp
var auditService = new HostingAuditService(dbContext);
var ownershipService = new BillingOwnershipService(dbContext, auditService);
var firstAccount = await ownershipService.CreateUserBillingAccountAsync(...);
var secondAccount = await ownershipService.CreateUserBillingAccountAsync(...);

var firstAssignment = await ownershipService.AssignOwnerAsync(gameServer, firstAccount, actor, "Initial owner", CancellationToken.None);
var secondAssignment = await ownershipService.AssignOwnerAsync(gameServer, secondAccount, actor, "Transfer owner", CancellationToken.None);

var assignments = await dbContext.GameServerBillingAssignments.OrderBy(q => q.PrimaryKey).ToListAsync();
Assert.NotNull(assignments[0].TimeStampUnassigned);
Assert.Null(assignments[1].TimeStampUnassigned);

var transferAudit = await dbContext.HostingAuditRecords.SingleAsync(q => q.Kind == EHostingAuditEventKind.BillingOwnerTransferred);
Assert.Equal(firstAccount.PrimaryKey, transferAudit.PreviousBillingAccountFk);
Assert.Equal(secondAccount.PrimaryKey, transferAudit.NewBillingAccountFk);
```

**Secret redaction test pattern** (lines 81-150):
```csharp
await ownershipService.AssignOwnerAsync(gameServer, firstAccount, actor, "Initial password token owner", CancellationToken.None);
await ownershipService.AssignOwnerAsync(gameServer, secondAccount, actor, "Transfer password token owner", CancellationToken.None);

Assert.All(assignments, assignment =>
{
    Assert.DoesNotContain("password", assignment.Reason, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("token", assignment.Reason, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("redacted", assignment.Reason, StringComparison.OrdinalIgnoreCase);
});
```

## Shared Patterns

### Authorization

**Source:** `sources/Api/Controllers/GameServersController.cs` lines 25-28, 353-357, 1777-1805; `sources/Api/Controllers/CloudPlanningController.cs` lines 15-17, 36-38.
**Apply to:** all owner/eligibility/readiness API endpoints.

Use `[Authorize]` on controllers, route or method policies for server-resource actions, and server-side filtering/lookup before returning IDs. Client-side `MeService` checks only hide controls.

### Error Handling

**Source:** `sources/Api/Controllers/CloudPlanningController.cs` lines 43-80; `sources/Api/Controllers/GameServersController.cs` lines 278-312, 1740-1774.
**Apply to:** readiness endpoints, owner assignment endpoints, repository methods.

Use `Unauthorized()` when the database actor cannot be resolved, `NotFound()` for missing/inaccessible route resources, `BadRequest()` for validation/invariant failures, and log expected lifecycle queue failures at debug level without exposing secret values.

### Ownership And Audit

**Source:** `sources/Api/Services/GameServerLifecycle/BillingOwnershipService.cs` lines 67-176; `sources/Api/Data/ApiDbContext.cs` lines 101-105.
**Apply to:** any billing owner selection, assignment, or new server planning path.

Use `IBillingOwnershipService`; do not write `GameServerBillingAssignment` rows directly from controllers. Preserve the one-active-assignment invariant and sanitized audit records.

### Repository Boundary

**Source:** `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` lines 27-119; `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` lines 12-14, 556-588.
**Apply to:** all new WebApp calls for guided cloud entry.

Add interface methods and implementation methods in `GameServerRepository`; Razor should not call generated Kiota builders or ad hoc endpoints directly. Prefer generated `Client.GameServers...` builders when available; use existing `HttpClient` helper pattern only for routes missing generated builders.

### UI Layout

**Source:** `sources/WebApp/Pages/Servers/Index.razor` lines 28-120, 132-221, 630-713; `sources/WebApp/Pages/Servers/Index.razor.css` lines 1-61, 104-161, 209-218.
**Apply to:** `/servers` guided cloud entry panel and validation summary.

Keep one dense operational page with `FluentDataGrid` first, a bordered `.detail-section` tool surface for guided setup, `FluentMessageBar` for failures, status badges for state, and mobile collapse at `860px`.

### No Provider Mutation

**Source:** `tests/Api.Tests/CloudPlanning/Arma3CloudApiTests.cs` lines 250-262, 599-615.
**Apply to:** all Phase 33 API tests and service constructors.

Add tests asserting guided-entry/readiness controllers do not depend on `IHetznerCloudClient`, `ICloudProvisioningService`, `ICloudTeardownService`, `IHttpClientFactory`, or `HttpClient` unless the route explicitly remains admin/provider-only.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| none | - | - | Existing controllers, services, WebApp repository, `/servers` UI, DTO, and API test patterns cover the expected Phase 33 files. |

## Metadata

**Analog search scope:** `sources/Api`, `sources/WebApp`, `tests/Api.Tests`.
**Files scanned:** 20 focused files plus grep across API/WebApp/tests.
**Pattern extraction date:** 2026-06-19
**Notes:** `sources/WebApp/UtfApi/**` and `sources/Api/Data/Migrations/**` are generated and were intentionally not used as style sources. No repo-local project skills were found under `.codex/skills` or `.agents/skills`.

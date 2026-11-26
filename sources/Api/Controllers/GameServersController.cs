using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Durable game-server records and asynchronous lifecycle actions.
/// </summary>
[ApiController]
[Authorize]
[Route(Constants.Routes.GameServers)]
public sealed class GameServersController(
    IGameServerLifecycleService lifecycleService,
    IGameServerProjectionService projectionService,
    IGameServerAccessService accessService) : ControllerBase
{
    [HttpGet("capabilities")]
    [ProducesResponseType<IReadOnlyCollection<GameServerCapabilityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapabilitiesAsync(CancellationToken cancellationToken)
        => Ok(await projectionService.GetCapabilitiesAsync(User, cancellationToken).ConfigureAwait(false));

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<GameServerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        => Ok(await projectionService.GetServersAsync(User, cancellationToken).ConfigureAwait(false));

    [HttpGet("notifications")]
    [ProducesResponseType<IReadOnlyCollection<GameServerNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationsAsync(CancellationToken cancellationToken)
        => Ok(await projectionService.GetNotificationsAsync(User, cancellationToken).ConfigureAwait(false));

    [HttpPost]
    [Authorize(Claims.Creation.Server)]
    [ProducesResponseType<GameServerDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] GameServerCreationPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var server = await lifecycleService.CreateAsync(payload, User, cancellationToken).ConfigureAwait(false);
            var dto = await projectionService
                .GetServerAsync(server.PrimaryKey, User, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtRoute(
                nameof(GameServersController) + "." + nameof(GetAsync),
                new { gameServerId = server.PrimaryKey },
                dto);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("{gameServerId:guid}", Name = nameof(GameServersController) + "." + nameof(GetAsync))]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<GameServerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var server = await projectionService.GetServerAsync(gameServerId, User, cancellationToken).ConfigureAwait(false);
        return server is null ? NotFound() : Ok(server);
    }

    [HttpGet("{gameServerId:guid}/mod-pack-options")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<GameServerModPackOptionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModPackOptionsAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var server = await projectionService.GetServerAsync(gameServerId, User, cancellationToken)
            .ConfigureAwait(false);
        if (server is null)
            return NotFound();
        return Ok(await projectionService
            .GetModPackOptionsAsync(gameServerId, User, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the flattened contents of one mod pack of the server's game, resolved as the next Start would.
    /// </summary>
    /// <param name="gameServerId">The game server the pack is inspected for.</param>
    /// <param name="modPackDefinitionId">The mod pack definition to inspect.</param>
    /// <param name="contentService">The contents projection service.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the contents (reporting unavailability with a safe reason when the pack cannot be resolved), or 404
    /// when the server, the pack or the view permission does not allow the lookup.
    /// </returns>
    [HttpGet("{gameServerId:guid}/mod-pack-options/{modPackDefinitionId:long}/contents")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<GameServerModPackContentsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModPackContentsAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] long modPackDefinitionId,
        [FromServices] IGameServerModPackContentService contentService,
        CancellationToken cancellationToken)
    {
        var contents = await contentService
            .GetContentsAsync(gameServerId, modPackDefinitionId, User, cancellationToken)
            .ConfigureAwait(false);
        return contents is null ? NotFound() : Ok(contents);
    }

    /// <summary>
    /// Gets the provider cost accrued by the current or most recent session of the server, calculated by the control
    /// plane from charged ledger slices.
    /// </summary>
    /// <param name="gameServerId">The game server whose session cost is requested.</param>
    /// <param name="accrualService">The accrued cost calculation service.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the accrued cost, where a null session means the server never cost and a null amount carries an
    /// unavailable reason (including <c>hidden</c> for a viewer who may not view the cost owner), or 404 when the
    /// server is unknown or not viewable.
    /// </returns>
    [HttpGet("{gameServerId:guid}/accrued-cost")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<GameServerAccruedCostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccruedCostAsync(
        [FromRoute] Guid gameServerId,
        [FromServices] ICostAccrualService accrualService,
        CancellationToken cancellationToken)
    {
        try
        {
            var accruedCost = await accrualService
                .GetForServerAsync(gameServerId, User, NodaSystemClock.Instance.GetCurrentInstant(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(accruedCost);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Gets the current metric readings of the server, decided by the control plane from the latest sample of its
    /// primary worker.
    /// </summary>
    /// <param name="gameServerId">The game server whose readings are requested.</param>
    /// <param name="metricsService">The service that decides the readings.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the readings, where every reading is a value or one reason code and never both, or 404 when the
    /// server is unknown or not viewable.
    /// </returns>
    [HttpGet("{gameServerId:guid}/metrics")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<GameServerMetricsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMetricsAsync(
        [FromRoute] Guid gameServerId,
        [FromServices] IServerMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await metricsService
                .GetCurrentAsync(gameServerId, User, NodaSystemClock.Instance.GetCurrentInstant(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(metrics);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Gets the retained metric history of the server for one range from the rollup tier of that range.
    /// </summary>
    /// <param name="gameServerId">The game server whose history is requested.</param>
    /// <param name="range">The range code: <c>1h</c>, <c>24h</c> or <c>7d</c>.</param>
    /// <param name="metricsService">The service that reads the history.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the series, where a window the worker never reported has no point, 400 when the range is missing or
    /// not one of the three codes, or 404 when the server is unknown or not viewable.
    /// </returns>
    [HttpGet("{gameServerId:guid}/metrics/history")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<GameServerMetricsHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMetricsHistoryAsync(
        [FromRoute] Guid gameServerId,
        [FromQuery] string? range,
        [FromServices] IServerMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        if (!ServerMetricResolutions.TryParse(range, out var parsedRange))
            return BadRequest(new { error = "Unknown range." });
        try
        {
            var history = await metricsService
                .GetHistoryAsync(
                    gameServerId,
                    User,
                    parsedRange,
                    NodaSystemClock.Instance.GetCurrentInstant(),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(history);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("creation/mod-pack-options")]
    [Authorize(Claims.Creation.Server)]
    [ProducesResponseType<IReadOnlyCollection<GameServerModPackOptionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCreationModPackOptionsAsync(
        [FromQuery] string gameIdentifier,
        CancellationToken cancellationToken)
        => Ok(await projectionService
            .GetCreationModPackOptionsAsync(gameIdentifier, cancellationToken)
            .ConfigureAwait(false));

    [HttpGet("creation/cost-owner-options")]
    [Authorize(Claims.Creation.Server)]
    [ProducesResponseType<IReadOnlyCollection<GameServerCostOwnerOptionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCreationCostOwnerOptionsAsync(CancellationToken cancellationToken)
        => Ok(await projectionService
            .GetCreationCostOwnerOptionsAsync(User, cancellationToken)
            .ConfigureAwait(false));

    [HttpPost("storage-demand-estimate")]
    [Authorize(Claims.Creation.Server)]
    [ProducesResponseType<StorageDemandDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EstimateStorageDemandAsync(
        [FromBody] StorageDemandEstimatePayload payload,
        CancellationToken cancellationToken)
        => Ok(await projectionService.EstimateStorageDemandAsync(payload, cancellationToken).ConfigureAwait(false));

    [HttpGet("{gameServerId:guid}/storage-demand")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<StorageDemandDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStorageDemandAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var demand = await projectionService.GetStorageDemandAsync(gameServerId, User, cancellationToken)
            .ConfigureAwait(false);
        return demand is null ? NotFound() : Ok(demand);
    }

    [HttpPost("{gameServerId:guid}/actions/{actionId}")]
    [Authorize(Claims.ResourceBased.Server.Operate)]
    [ProducesResponseType<LifecycleJobDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ExecuteActionAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string actionId,
        [FromBody] GameServerActionPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await lifecycleService
                .EnqueueActionAsync(gameServerId, actionId, payload.Confirmation, User, cancellationToken)
                .ConfigureAwait(false);
            return AcceptedAtRoute(
                "LifecycleJobs",
                new { lifecycleJobId = job.PrimaryKey },
                projectionService.ProjectJob(job, User));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("{gameServerId:guid}/jobs")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<LifecycleJobDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobsAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
        => Ok(await projectionService.GetJobsAsync(gameServerId, User, cancellationToken).ConfigureAwait(false));

    [HttpGet("{gameServerId:guid}/operations")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<HostingOperationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationsAsync(
        [FromRoute] Guid gameServerId,
        [FromQuery] int take = 80,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200)
            return BadRequest(new { error = "Take must be between 1 and 200." });
        return Ok(await projectionService
            .GetOperationsAsync(gameServerId, User, take, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the cost owners the caller may transfer the server to. Administrators receive every owner, everyone else
    /// receives their own owner and the owners they hold an exact cost-owner use grant for. The server's current owner
    /// is never listed and frozen owners are listed with their frozen flag set.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="search">The optional owner display-name search text; blank text matches every owner.</param>
    /// <param name="take">The maximum number of owners, from 1 to 50.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching owners, the caller's own owner first, then by display name and identity.</returns>
    [HttpGet("{gameServerId:guid}/cost-owner/options")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType<IReadOnlyCollection<GameServerCostOwnerOptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCostOwnerOptionsAsync(
        [FromRoute] Guid gameServerId,
        [FromQuery] string? search,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > HostingSearchLimits.MaximumTake)
            return BadRequest(CostOwnerLimitProblem());
        try
        {
            return Ok(await projectionService
                .GetCostOwnerOptionsAsync(gameServerId, search, take, User, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut("{gameServerId:guid}/cost-owner")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeCostOwnerAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerCostOwnerUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService
                .ChangeCostOwnerAsync(gameServerId, payload.CostOwnerId, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut("{gameServerId:guid}/runtime-profile")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeRuntimeProfileAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerRuntimeProfileUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService
                .ChangeRuntimeProfileAsync(gameServerId, payload.RuntimeProfileIdentifier, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut("{gameServerId:guid}/runtime-storage")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeRuntimeStorageAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerRuntimeStorageUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService
                .ChangeRuntimeStorageAsync(
                    gameServerId,
                    payload.Strategy,
                    payload.ExternalStorageSizeGiB,
                    User,
                    cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut("{gameServerId:guid}/mod-pack")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeModPackAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerModPackSelectionPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService
                .ChangeModPackAsync(gameServerId, payload.ModPackDefinitionId, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut("{gameServerId:guid}/content-selection")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeContentSelectionAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerContentSelectionPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService.ChangeContentSelectionAsync(gameServerId, payload.TerrainId,
                payload.TerrainVersionId, payload.ScenarioId, payload.ScenarioVersionId, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpDelete("{gameServerId:guid}")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        try
        {
            await lifecycleService.ArchiveAsync(gameServerId, User, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("{gameServerId:guid}/access")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType<GameServerAccessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await accessService.GetAsync(gameServerId, User, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Searches the members that can still be given direct access to the server: verified members who are neither
    /// deleted nor banned and hold no direct grant on the server.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="search">The optional nickname search text; blank text matches every member.</param>
    /// <param name="take">The maximum number of candidates, from 1 to 50.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching candidates ordered by nickname and then identity.</returns>
    [HttpGet("{gameServerId:guid}/access/users/options")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType<GameServerUserAccessCandidateDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAccessOptionsAsync(
        [FromRoute] Guid gameServerId,
        [FromQuery] string? search,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > HostingSearchLimits.MaximumTake)
            return BadRequest(CandidateLimitProblem());
        try
        {
            return Ok(await accessService
                .SearchUserCandidatesAsync(gameServerId, search, take, User, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Searches the roles that can still be given direct access to the server: roles that hold no direct grant on
    /// the server. Each candidate reports its active member count.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="search">The optional role title search text; blank text matches every role.</param>
    /// <param name="take">The maximum number of candidates, from 1 to 50.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching candidates ordered by title and then identity.</returns>
    [HttpGet("{gameServerId:guid}/access/roles/options")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType<GameServerRoleAccessCandidateDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoleAccessOptionsAsync(
        [FromRoute] Guid gameServerId,
        [FromQuery] string? search,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > HostingSearchLimits.MaximumTake)
            return BadRequest(CandidateLimitProblem());
        try
        {
            return Ok(await accessService
                .SearchRoleCandidatesAsync(gameServerId, search, take, User, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("{gameServerId:guid}/access/users/grant")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GrantUserAccessAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerAccessPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await accessService
                .GrantUserAsync(gameServerId, payload.UserId, payload.Permission, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("{gameServerId:guid}/access/roles/grant")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GrantRoleAccessAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerRoleAccessPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await accessService
                .GrantRoleAsync(gameServerId, payload.RoleId, payload.Permission, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("{gameServerId:guid}/access/roles/revoke")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeRoleAccessAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerRoleAccessPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await accessService
                .RevokeRoleAsync(gameServerId, payload.RoleId, payload.Permission, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("{gameServerId:guid}/access/users/revoke")]
    [Authorize(Claims.ResourceBased.Server.ManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeUserAccessAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerAccessPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await accessService
                .RevokeUserAsync(gameServerId, payload.UserId, payload.Permission, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    private static ProblemDetails CandidateLimitProblem()
        => new()
        {
            Title = "The candidate limit is invalid.",
            Detail = $"Take must be between 1 and {HostingSearchLimits.MaximumTake}.",
            Status = StatusCodes.Status400BadRequest,
        };

    private static ProblemDetails CostOwnerLimitProblem()
        => new()
        {
            Title = "The cost owner limit is invalid.",
            Detail = $"Take must be between 1 and {HostingSearchLimits.MaximumTake}.",
            Status = StatusCodes.Status400BadRequest,
        };

    private static bool TryMapException(Exception exception, out IActionResult result)
    {
        result = exception switch
        {
            HostingNotFoundException => new NotFoundObjectResult(new { error = exception.Message }),
            HostingForbiddenException => new ForbidResult(),
            HostingValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            HostingConflictException => new ConflictObjectResult(new { error = exception.Message }),
            DbUpdateException => new ConflictObjectResult(new
            {
                error = "The server changed concurrently; reload its current state and try again.",
            }),
            _ => null!,
        };
        return result is not null;
    }
}

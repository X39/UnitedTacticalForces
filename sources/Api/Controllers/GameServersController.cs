using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

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

    [HttpGet("{gameServerId:guid}/actions")]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<GameServerActionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActionsAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var server = await projectionService.GetServerAsync(gameServerId, User, cancellationToken).ConfigureAwait(false);
        return server is null ? NotFound() : Ok(server.Actions);
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

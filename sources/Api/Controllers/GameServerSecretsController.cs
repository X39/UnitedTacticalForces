using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route("game-servers/{gameServerId:guid}/secrets")]
public sealed class GameServerSecretsController(
    IGameServerSecretService secretService,
    IHostingAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<GameServerSecretStatusDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(Guid gameServerId, CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, gameServerId, Claims.ResourceBased.Server.ManageSecrets))
            return Forbid();
        try
        {
            return Ok(await secretService.GetStatusesAsync(gameServerId, cancellationToken).ConfigureAwait(false));
        }
        catch (HostingNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut]
    public async Task<IActionResult> SetAsync(
        Guid gameServerId,
        GameServerSecretValuePayload payload,
        CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, gameServerId, Claims.ResourceBased.Server.ManageSecrets))
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            await secretService.SetAsync(gameServerId, payload.Key, payload.Value, payload.Origin, actorId, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (HostingNotFoundException)
        {
            return NotFound();
        }
        catch (HostingValidationException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpPost("{key}/rotate")]
    public async Task<IActionResult> RotateAsync(Guid gameServerId, string key, CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, gameServerId, Claims.ResourceBased.Server.ManageSecrets))
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            await secretService.RotateAsync(gameServerId, key, actorId, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (HostingNotFoundException)
        {
            return NotFound();
        }
        catch (HostingValidationException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpPost("{key}/reveal")]
    [ProducesResponseType<GameServerSecretValueDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevealAsync(Guid gameServerId, string key, CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, gameServerId, Claims.ResourceBased.Server.RevealSecrets))
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            return Ok(await secretService.RevealAsync(gameServerId, key, actorId, cancellationToken).ConfigureAwait(false));
        }
        catch (HostingNotFoundException)
        {
            return NotFound();
        }
        catch (HostingForbiddenException)
        {
            return Forbid();
        }
    }
}

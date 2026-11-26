using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Exposes the secret status, update, rotation and reveal endpoints of a game server. No response of this controller
/// contains a secret value except the explicit reveal endpoint and the one-time value a rotation has just generated.
/// Both of those responses are marked <c>Cache-Control: no-store</c>.
/// </summary>
/// <param name="secretService">The service that stores and resolves the secrets.</param>
/// <param name="authorizationService">The per-server authorization checks.</param>
[ApiController]
[Authorize]
[Route("game-servers/{gameServerId:guid}/secrets")]
public sealed class GameServerSecretsController(
    IGameServerSecretService secretService,
    IHostingAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>
    /// Lists the secret slots of a game and whether each has a current value.
    /// </summary>
    /// <param name="gameServerId">The identifier of the game server.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The secret statuses, <c>403</c> without the manage permission, or <c>404</c> for an unknown server.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<GameServerSecretStatusDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Supplies a new value for a user-provided or externally issued secret.
    /// </summary>
    /// <param name="gameServerId">The identifier of the game server.</param>
    /// <param name="payload">The secret key, value and origin. The value is never echoed back.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// <c>204</c> on success, <c>400</c> for an invalid secret, <c>403</c> without the manage permission, <c>404</c>
    /// for an unknown server, or <c>409</c> when the server is archived.
    /// </returns>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
        catch (HostingConflictException exception)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: exception.Message);
        }
    }

    /// <summary>
    /// Replaces a platform-generated secret with a freshly generated value and returns that value once to the
    /// acting member. The reveal permission is not required because only the value this call generated is returned.
    /// </summary>
    /// <param name="gameServerId">The identifier of the game server.</param>
    /// <param name="key">The key of the generated secret.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// <c>200</c> with the newly generated value, <c>400</c> for an unknown or non-generated key, <c>403</c>
    /// without the manage permission, <c>404</c> for an unknown server, or <c>409</c> when the server is archived.
    /// The response is never cached.
    /// </returns>
    [HttpPost("{key}/rotate")]
    [ProducesResponseType<GameServerSecretValueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RotateAsync(Guid gameServerId, string key, CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, gameServerId, Claims.ResourceBased.Server.ManageSecrets))
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            var result = await secretService.RotateAsync(gameServerId, key, actorId, cancellationToken)
                .ConfigureAwait(false);
            Response.Headers.CacheControl = "no-store";
            return Ok(result);
        }
        catch (HostingNotFoundException)
        {
            return NotFound();
        }
        catch (HostingValidationException exception)
        {
            return ValidationProblem(exception.Message);
        }
        catch (HostingConflictException exception)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: exception.Message);
        }
    }

    /// <summary>
    /// Reveals the current value of a revealable secret and records an audit entry. Reveal stays available on
    /// archived servers because it is read-only.
    /// </summary>
    /// <param name="gameServerId">The identifier of the game server.</param>
    /// <param name="key">The key of the secret to reveal.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// The secret value with <c>Cache-Control: no-store</c>, <c>403</c> without the reveal permission or for a
    /// non-revealable secret, or <c>404</c>.
    /// </returns>
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
            var result = await secretService.RevealAsync(gameServerId, key, actorId, cancellationToken)
                .ConfigureAwait(false);
            Response.Headers.CacheControl = "no-store";
            return Ok(result);
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Saves the automation settings of a game server: idle shutdown, maximum runtime, the time zone and weekly rules.
/// </summary>
/// <param name="settingsService">The service that applies authorization, validation and concurrency.</param>
[ApiController]
[Authorize]
[Route("game-servers/{gameServerId:guid}/automation")]
public sealed class GameServerAutomationController(IServerAutomationSettingsService settingsService) : ControllerBase
{
    /// <summary>
    /// Replaces the automation settings and weekly schedule rules of one game server.
    /// </summary>
    /// <param name="gameServerId">The server whose automation is saved.</param>
    /// <param name="update">The new settings and the version they are based on.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the saved automation block; 400 with <c>{ error, fields }</c> for an invalid update; 403 when the
    /// caller may view but not change the automation; 404 when the caller may not view the server; 409 when the
    /// server is archived or the version is stale; 503 while the control plane is paused.
    /// </returns>
    [HttpPut(Name = nameof(UpdateAutomationAsync))]
    [ProducesResponseType<ServerAutomationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateAutomationAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] ServerAutomationUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await settingsService
                .UpdateAsync(gameServerId, update, User, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Maps a service exception to its HTTP result.
    /// </summary>
    /// <param name="exception">The exception thrown by the service.</param>
    /// <param name="result">The mapped result, or <see langword="null"/> when the exception is not mapped.</param>
    /// <returns><see langword="true"/> when <paramref name="result"/> was set.</returns>
    private static bool TryMapException(Exception exception, out IActionResult result)
    {
        result = exception switch
        {
            HostingNotFoundException => new NotFoundObjectResult(new { error = exception.Message }),
            HostingForbiddenException => new ForbidResult(),
            HostingFieldValidationException fieldException => new BadRequestObjectResult(
                new { error = exception.Message, fields = fieldException.Fields }),
            HostingValidationException => new BadRequestObjectResult(
                new { error = exception.Message, fields = Array.Empty<string>() }),
            HostingConflictException => new ConflictObjectResult(new { error = exception.Message }),
            _ => null!,
        };
        return result is not null;
    }
}

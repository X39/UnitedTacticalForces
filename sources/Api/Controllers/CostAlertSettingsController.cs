using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Reads and saves the cost alert settings of cost owners, under the cost-owners route.
/// </summary>
/// <param name="settingsService">The service that applies visibility, authorization, validation and concurrency.</param>
[ApiController]
[Authorize]
[Route(Constants.Routes.CostOwners)]
public sealed class CostAlertSettingsController(ICostAlertSettingsService settingsService) : ControllerBase
{
    /// <summary>
    /// Reads the alert settings of one cost owner.
    /// </summary>
    /// <param name="costOwnerId">The cost owner whose settings are read.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the settings; 403 when the caller may not view the owner; 404 when the owner does not exist.
    /// </returns>
    [HttpGet("{costOwnerId:guid}/alert-settings", Name = nameof(GetAlertSettingsAsync))]
    [ProducesResponseType<CostAlertSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAlertSettingsAsync(
        [FromRoute] Guid costOwnerId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await settingsService.GetAsync(costOwnerId, User, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Reads the alert settings of the caller's personal cost owner.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>200 with the settings, or 204 when the caller has no personal cost owner.</returns>
    [HttpGet("mine/alert-settings", Name = nameof(GetMyAlertSettingsAsync))]
    [ProducesResponseType<CostAlertSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMyAlertSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetPersonalAsync(User, cancellationToken).ConfigureAwait(false);
        return settings is null ? NoContent() : Ok(settings);
    }

    /// <summary>
    /// Replaces the alert settings of one cost owner.
    /// </summary>
    /// <param name="costOwnerId">The cost owner whose settings are saved.</param>
    /// <param name="update">The new configuration and the revision it is based on.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// 200 with the stored settings; 400 for an invalid configuration; 403 when the caller may not manage the owner;
    /// 404 when the owner does not exist; 409 when the revision is stale.
    /// </returns>
    [HttpPut("{costOwnerId:guid}/alert-settings", Name = nameof(UpdateAlertSettingsAsync))]
    [ProducesResponseType<CostAlertSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAlertSettingsAsync(
        [FromRoute] Guid costOwnerId,
        [FromBody] CostAlertSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await settingsService
                .UpdateAsync(costOwnerId, update, User, cancellationToken)
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
            HostingNotFoundException   => new NotFoundObjectResult(new { error = exception.Message }),
            HostingForbiddenException  => new ForbidResult(),
            HostingValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            HostingConflictException   => new ConflictObjectResult(new { error = exception.Message }),
            _                          => null!,
        };
        return result is not null;
    }
}

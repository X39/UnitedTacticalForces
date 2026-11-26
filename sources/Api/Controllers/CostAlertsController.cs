using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Lists and dismisses the in-app cost alerts of the cost owners the caller may view.
/// </summary>
/// <param name="costAlertService">The service that applies cost-owner visibility and per-viewer dismissal.</param>
[ApiController]
[Authorize]
[Route(Constants.Routes.CostAlerts)]
public sealed class CostAlertsController(ICostAlertService costAlertService) : ControllerBase
{
    /// <summary>
    /// Lists the caller's undismissed cost alerts, newest first, at most 50.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>A 200 response with the alerts, or 401 when the caller has no user id.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<CostAlertDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        return Ok(await costAlertService.ListAsync(User, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Dismisses one cost alert for the caller only.
    /// </summary>
    /// <param name="costAlertId">The identifier of the alert.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// A 204 response once the alert is dismissed for the caller, including when it already was; 404 when the alert
    /// does not exist or belongs to a cost owner the caller may not view.
    /// </returns>
    [HttpPost("{costAlertId:guid}/dismissal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissAsync(
        [FromRoute] Guid costAlertId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await costAlertService.DismissAsync(costAlertId, User, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Dismisses, for the caller only, the listed visible undismissed cost alerts of one server.
    /// </summary>
    /// <param name="payload">The server and the ids of the alerts the client shows.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// A 204 response, also when no listed alert was visible or matched the server, so ids cannot be probed; 400 when
    /// the id list is empty or holds more than 50 ids.
    /// </returns>
    [HttpPost("dismissals")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissAllForServerAsync(
        [FromBody] CostAlertDismissAllPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();
        try
        {
            await costAlertService
                .DismissAllForServerAsync(payload.GameServerId, payload.CostAlertIds, User, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
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
            HostingValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            _ => null!,
        };
        return result is not null;
    }
}

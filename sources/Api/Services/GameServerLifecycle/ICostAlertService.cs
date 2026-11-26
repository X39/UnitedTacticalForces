using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Lists and dismisses the in-app cost alerts a viewer may see.
/// </summary>
/// <remarks>
/// Visibility follows <see cref="IHostingAuthorizationService.CanViewCostOwnerAsync"/>: a viewer sees the alerts of
/// every cost owner they may view, also after the session ended. Dismissal is stored per viewer and never hides the
/// alert from other viewers of the same owner.
/// </remarks>
public interface ICostAlertService
{
    /// <summary>
    /// Lists the undismissed alerts of every cost owner the principal may view, newest first.
    /// </summary>
    /// <param name="principal">The viewer whose visibility and dismissals are applied.</param>
    /// <param name="cancellationToken">A token that cancels the database query.</param>
    /// <returns>
    /// A task resolving to at most 50 alerts; empty when the principal has no user id or no visible alert.
    /// </returns>
    Task<IReadOnlyCollection<CostAlertDto>> ListAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    /// <summary>
    /// Dismisses one alert for the principal.
    /// </summary>
    /// <param name="costAlertId">The identifier of the alert to dismiss.</param>
    /// <param name="principal">The viewer who dismisses the alert.</param>
    /// <param name="cancellationToken">A token that cancels the database work.</param>
    /// <returns>A task completing once the alert is dismissed for the principal; repeating it succeeds.</returns>
    /// <exception cref="HostingNotFoundException">
    /// The alert does not exist or its cost owner may not be viewed by the principal; both look identical so ids
    /// cannot be probed.
    /// </exception>
    Task DismissAsync(Guid costAlertId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    /// <summary>
    /// Dismisses the listed alerts of one server that the principal may see and has not dismissed yet.
    /// </summary>
    /// <param name="gameServerId">The identifier of the game server whose alerts are dismissed.</param>
    /// <param name="costAlertIds">
    /// The ids of the alerts the viewer was shown, one to 50. Alerts raised after the viewer's list was loaded are
    /// not in this list and stay undismissed; ids of another server or of an owner the principal may not view are
    /// ignored without a signal.
    /// </param>
    /// <param name="principal">The viewer who dismisses the alerts.</param>
    /// <param name="cancellationToken">A token that cancels the database work.</param>
    /// <returns>A task completing once the matching alerts are dismissed; nothing matching is not an error.</returns>
    /// <exception cref="HostingValidationException">
    /// <paramref name="costAlertIds"/> is empty or holds more than 50 ids.
    /// </exception>
    Task DismissAllForServerAsync(
        Guid gameServerId,
        IReadOnlyCollection<Guid> costAlertIds,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}

using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Reads and saves the cost alert configuration of a cost owner for the people who may view or manage it.
/// </summary>
public interface ICostAlertSettingsService
{
    /// <summary>
    /// Reads the alert settings of one cost owner.
    /// </summary>
    /// <param name="costOwnerId">The cost owner whose settings are read.</param>
    /// <param name="principal">The viewer the authorization rules are evaluated for.</param>
    /// <param name="cancellationToken">A token that cancels the database queries.</param>
    /// <returns>A task resolving to the settings; an owner without a saved setting reads as no alerts.</returns>
    /// <exception cref="HostingNotFoundException">The cost owner does not exist.</exception>
    /// <exception cref="HostingForbiddenException">The principal may neither view nor use the cost owner.</exception>
    Task<CostAlertSettingsDto> GetAsync(
        Guid costOwnerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the alert settings of the caller's personal cost owner.
    /// </summary>
    /// <param name="principal">The caller.</param>
    /// <param name="cancellationToken">A token that cancels the database queries.</param>
    /// <returns>
    /// A task resolving to the settings, or <see langword="null"/> when the principal has no user id or no
    /// personal cost owner.
    /// </returns>
    Task<CostAlertSettingsDto?> GetPersonalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the alert settings of one cost owner.
    /// </summary>
    /// <param name="costOwnerId">The cost owner whose settings are saved.</param>
    /// <param name="update">The new configuration together with the revision it is based on.</param>
    /// <param name="principal">The user who saves.</param>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>A task resolving to the settings as stored after the save.</returns>
    /// <exception cref="HostingNotFoundException">The cost owner does not exist.</exception>
    /// <exception cref="HostingForbiddenException">The principal may not use the cost owner.</exception>
    /// <exception cref="HostingValidationException">The update is invalid; nothing was changed.</exception>
    /// <exception cref="HostingConflictException">
    /// The revision is stale or a concurrent first save won; nothing was changed.
    /// </exception>
    Task<CostAlertSettingsDto> UpdateAsync(
        Guid costOwnerId,
        CostAlertSettingsUpdate update,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}

using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Saves the automation settings of a game server: authorization, validation, optimistic concurrency, start consent
/// and audit.
/// </summary>
public interface IServerAutomationSettingsService
{
    /// <summary>
    /// Replaces the automation settings and weekly schedule rules of a server.
    /// </summary>
    /// <param name="gameServerId">The server whose automation is saved; it alone decides authorization.</param>
    /// <param name="update">The new settings and the version they are based on.</param>
    /// <param name="principal">The caller.</param>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>A task resolving to the saved automation block exactly as the server detail projects it.</returns>
    /// <exception cref="Services.GameServerLifecycle.HostingNotFoundException">
    /// The caller may not view the server or it does not exist.
    /// </exception>
    /// <exception cref="Services.GameServerLifecycle.HostingForbiddenException">
    /// The caller may view the server but may not change its automation.
    /// </exception>
    /// <exception cref="Services.GameServerLifecycle.HostingConflictException">
    /// The server is archived, the version is stale, or a concurrent save won.
    /// </exception>
    /// <exception cref="Services.GameServerLifecycle.HostingFieldValidationException">
    /// The update violates a rule; the exception names every failing field and nothing is stored.
    /// </exception>
    Task<ServerAutomationDto> UpdateAsync(
        Guid gameServerId,
        ServerAutomationUpdate update,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}

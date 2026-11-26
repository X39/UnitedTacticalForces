using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Builds the automation read model of a game server: the saved settings, the limits, and the live status that the
/// control plane calculates for the browser.
/// </summary>
/// <remarks>
/// The projector reads and never writes. The evaluator of the automation uses the same pure idle clock, so what the
/// panel shows and what the evaluator decides cannot disagree.
/// </remarks>
public interface IServerAutomationProjector
{
    /// <summary>
    /// Decides whether a viewer may change the automation settings of a server.
    /// </summary>
    /// <param name="principal">The viewer.</param>
    /// <param name="server">The server, with its current cost owner key loaded.</param>
    /// <param name="cancellationToken">Cancels the cost owner lookup.</param>
    /// <returns>
    /// <see langword="true"/> when the viewer holds the server modify permission and may use the current cost owner of
    /// the server; hosting administrators pass both checks.
    /// </returns>
    Task<bool> CanManageAsync(ClaimsPrincipal principal, GameServer server, CancellationToken cancellationToken);

    /// <summary>
    /// Projects the automation block of a server.
    /// </summary>
    /// <param name="server">The server with its deployments and lifecycle jobs loaded.</param>
    /// <param name="now">The instant every calculated value is relative to.</param>
    /// <param name="cancellationToken">Cancels the database and metrics reads.</param>
    /// <returns>The automation block, or <see langword="null"/> when the game definition of the server is unknown.</returns>
    Task<ServerAutomationDto?> ProjectAsync(GameServer server, Instant now, CancellationToken cancellationToken);
}

using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Calculates the accrued provider cost of game server sessions on the control plane from durable ledger slices.
/// </summary>
public interface ICostAccrualService
{
    /// <summary>
    /// Gets the accrued cost of the current or most recent session of one server for one viewer.
    /// </summary>
    /// <param name="gameServerId">The game server whose session cost is calculated.</param>
    /// <param name="principal">The principal asking; it must be able to view the server.</param>
    /// <param name="now">The current instant used while the session may still cost.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>
    /// A task resolving to the accrued cost. The session is <see langword="null"/> when the server never had a
    /// session that cost, and its amount is <see langword="null"/> with reason
    /// <see cref="AccruedCostUnavailableReasons.Hidden"/> when the principal may not view the session's cost owner.
    /// </returns>
    /// <exception cref="HostingNotFoundException">
    /// The server does not exist or the principal may not view it.
    /// </exception>
    Task<GameServerAccruedCostDto> GetForServerAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        Instant now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calculates the accrued cost of many deployments with one ledger query, without any authorization decision.
    /// </summary>
    /// <param name="deployments">The deployments to calculate; duplicates by id are calculated once.</param>
    /// <param name="now">The current instant used for deployments that may still cost.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>A task resolving to the accrued cost per deployment id, with an entry for every deployment.</returns>
    Task<IReadOnlyDictionary<Guid, AccruedCostResult>> CalculateForDeploymentsAsync(
        IReadOnlyCollection<GameServerDeployment> deployments,
        Instant now,
        CancellationToken cancellationToken);
}

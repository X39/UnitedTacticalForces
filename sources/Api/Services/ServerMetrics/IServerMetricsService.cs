using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// Stores server metrics reported by dedicated workers.
/// </summary>
public interface IServerMetricsService
{
    /// <summary>
    /// Validates one worker sample and stores it as the latest snapshot of its server.
    /// </summary>
    /// <param name="source">The attribution built from the authenticated worker.</param>
    /// <param name="sample">The sample reported by the worker.</param>
    /// <param name="receivedAt">The API receipt time of the sample.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A task resolving to the result; <see cref="ServerMetricsIngestResult.Stored"/> is <see langword="false"/>
    /// for a sample from a host that is not the primary host.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The sample is invalid, belongs to another deployment, or lies outside the accepted time window.
    /// </exception>
    Task<ServerMetricsIngestResult> IngestAsync(
        ServerMetricSampleSource source,
        WorkerMetricsSample sample,
        Instant receivedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Decides the current readings of a server without any authorization check.
    /// </summary>
    /// <param name="gameServerId">The game server to evaluate.</param>
    /// <param name="now">The instant the freshness of the worker and of the sample is judged against.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A task resolving to the readings. Every reading is a value or exactly one reason code, never both.
    /// </returns>
    /// <remarks>
    /// For internal callers that already authorized the audience, such as the update stream publisher. Callers acting
    /// for a user must use <see cref="GetCurrentAsync"/>.
    /// </remarks>
    Task<GameServerMetricsDto> EvaluateCurrentAsync(
        Guid gameServerId,
        Instant now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Decides the current readings of a server for a member who may view it.
    /// </summary>
    /// <param name="gameServerId">The game server to evaluate.</param>
    /// <param name="principal">The member asking for the readings.</param>
    /// <param name="now">The instant the freshness of the worker and of the sample is judged against.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task resolving to the readings, see <see cref="EvaluateCurrentAsync"/>.</returns>
    /// <exception cref="HostingNotFoundException">
    /// The server does not exist or the principal may not view it.
    /// </exception>
    Task<GameServerMetricsDto> GetCurrentAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        Instant now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the retained history of a server for one range from the durable rollup tier of that range.
    /// </summary>
    /// <param name="gameServerId">The game server to read.</param>
    /// <param name="principal">The member asking for the history.</param>
    /// <param name="range">The range, which selects the tier and the window.</param>
    /// <param name="now">The end of the window.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A task resolving to the series with API-computed aggregates. Buckets that never received a sample are absent,
    /// so a gap stays a gap.
    /// </returns>
    /// <exception cref="HostingNotFoundException">
    /// The server does not exist or the principal may not view it.
    /// </exception>
    Task<GameServerMetricsHistoryDto> GetHistoryAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        EServerMetricsRange range,
        Instant now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the rollup rows that are older than the retention of their tier.
    /// </summary>
    /// <param name="now">The instant the retention is measured back from.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task resolving to the number of removed rollup rows.</returns>
    /// <remarks>
    /// The latest snapshot of every server is never removed. The operation keeps no state, so it is safe to repeat
    /// and to resume after a restart.
    /// </remarks>
    Task<int> PruneAsync(Instant now, CancellationToken cancellationToken);
}

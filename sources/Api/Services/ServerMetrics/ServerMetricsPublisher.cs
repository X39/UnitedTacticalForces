using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// Publishes the current readings of a server to its view audience through the update stream.
/// </summary>
/// <remarks>
/// <para>
/// The readings are decided by <see cref="IServerMetricsService.EvaluateCurrentAsync"/>, so the browser receives the
/// same values and reason codes as a REST read. The frame goes to <see cref="FleetChangeStreamPaths.ForUser"/> of
/// every user who may view the server. The audience is resolved on every publication and fails closed: a failed
/// audience read sends nothing and logs one warning with the server id, exactly like the cost alert publisher.
/// </para>
/// <para>
/// No fleet-changed signal is raised per sample. The publisher never throws, so an audience or send failure can never
/// fail the worker's acknowledgement of a stored sample.
/// </para>
/// </remarks>
/// <param name="metrics">The service that decides the current readings.</param>
/// <param name="authorizationService">The service that resolves the view audience of a server.</param>
/// <param name="updateStreamService">The update stream the frames are sent through.</param>
/// <param name="logger">The logger that receives warnings naming ids only.</param>
[Scoped<ServerMetricsPublisher, IServerMetricsPublisher>]
internal sealed class ServerMetricsPublisher(
    IServerMetricsService metrics,
    IHostingAuthorizationService authorizationService,
    IUpdateStreamService updateStreamService,
    ILogger<ServerMetricsPublisher> logger) : IServerMetricsPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(Guid gameServerId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Guid> viewers;
        try
        {
            var audience = await authorizationService
                .GetServerViewAudienceAsync([gameServerId], cancellationToken)
                .ConfigureAwait(false);
            if (!audience.TryGetValue(gameServerId, out var resolved) || resolved.Count == 0)
                return;
            viewers = resolved;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve the audience of game server {GameServerId}; the metrics frame was not sent",
                gameServerId);
            return;
        }

        GameServerMetricsMessage message;
        try
        {
            var readings = await metrics
                .EvaluateCurrentAsync(gameServerId, NodaSystemClock.Instance.GetCurrentInstant(), cancellationToken)
                .ConfigureAwait(false);
            message = ToMessage(readings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to decide the metrics readings of game server {GameServerId}; the metrics frame was not sent",
                gameServerId);
            return;
        }

        foreach (var userId in viewers.Distinct())
        {
            try
            {
                await updateStreamService
                    .SendUpdateAsync(FleetChangeStreamPaths.ForUser(userId), message)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to publish a metrics frame for game server {GameServerId} to user {UserId}",
                    gameServerId,
                    userId);
            }
        }
    }

    /// <summary>
    /// Maps the readings decided by the control plane to the flat browser frame.
    /// </summary>
    /// <param name="readings">The readings to map.</param>
    /// <returns>The frame carrying the same values and reason codes with instants as offsets.</returns>
    private static GameServerMetricsMessage ToMessage(GameServerMetricsDto readings)
        => new()
        {
            ServerId                      = readings.GameServerId,
            AsOf                          = readings.AsOf?.ToDateTimeOffset(),
            StaleAfterSeconds             = readings.StaleAfterSeconds,
            RunningSince                  = readings.RunningSince?.ToDateTimeOffset(),
            RunningSinceUnavailableReason = readings.RunningSinceUnavailableReason,
            CpuPercent                    = readings.CpuPercent,
            CpuUnavailableReason          = readings.CpuUnavailableReason,
            MemoryUsedBytes               = readings.MemoryUsedBytes,
            MemoryTotalBytes              = readings.MemoryTotalBytes,
            MemoryUnavailableReason       = readings.MemoryUnavailableReason,
            PlayerCount                   = readings.PlayerCount,
            PlayerCapacity                = readings.PlayerCapacity,
            PlayersUnavailableReason      = readings.PlayersUnavailableReason,
        };
}

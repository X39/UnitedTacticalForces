using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Periodically removes server metric rollup rows that are older than the retention of their tier.
/// </summary>
/// <remarks>
/// The pass keeps no state of its own: it deletes by age against the durable tables, so a restart of the API neither
/// loses nor repeats work. The latest snapshot of each server is never pruned. It is registered only while the
/// hosting control plane is not paused.
/// </remarks>
/// <param name="scopeFactory">The factory that creates one dependency scope per pruning pass.</param>
/// <param name="options">The metrics options that provide the retentions and the pruning interval.</param>
/// <param name="logger">The logger that receives the pass result and sanitized failure diagnostics.</param>
internal sealed class ServerMetricsPruneWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ServerMetricsOptions> options,
    ILogger<ServerMetricsPruneWorker> logger) : BackgroundService
{
    private readonly ServerMetricsOptions _options = options.Value;

    /// <summary>
    /// Runs pruning passes every <see cref="ServerMetricsOptions.PruneInterval" /> until the host stops.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the host stops.</returns>
    /// <exception cref="InvalidOperationException">The metrics options are invalid.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<IServerMetricsService>()
                    .PruneAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
                    .ConfigureAwait(false);
                logger.LogDebug("Server metrics pruning removed {RemovedRows} expired rows", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Server metrics pruning failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            try
            {
                await Task.Delay(_options.PruneInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Periodically evaluates the idle and maximum runtime rules of every server with automation, opening, withdrawing
/// and executing automatic stop warnings.
/// </summary>
/// <remarks>
/// The worker is registered only while the hosting control plane is not paused. It holds no timer state in memory:
/// every pass recomputes its decisions from PostgreSQL and the current instant, so a restart of the API neither loses
/// nor repeats a stop (AUT-06). The evaluator isolates failures per server; the loop additionally survives an
/// exception of a whole pass. After each committed pass the raised and withdrawn warnings are pushed to the update
/// stream on a best-effort basis.
/// </remarks>
/// <param name="scopeFactory">The factory that creates one dependency scope per evaluation pass.</param>
/// <param name="options">The automation options that provide the evaluation interval.</param>
/// <param name="logger">The logger that receives sanitized failure diagnostics.</param>
internal sealed class ServerAutomationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ServerAutomationOptions> options,
    ILogger<ServerAutomationWorker> logger) : BackgroundService
{
    private readonly ServerAutomationOptions _options = options.Value;

    /// <summary>
    /// Runs evaluation passes every <see cref="ServerAutomationOptions.EvaluationInterval" /> until the host stops.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the host stops.</returns>
    /// <exception cref="InvalidOperationException">The automation options are invalid.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IServerAutomationEvaluator>()
                    .EvaluateAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
                    .ConfigureAwait(false);

                // Publishing happens after the evaluation transactions committed and is best effort; the publisher
                // logs its own failures, and the REST reads stay the source of truth.
                var publisher = scope.ServiceProvider.GetRequiredService<ICostAlertPublisher>();
                await publisher.PublishRaisedAsync(result.RaisedAlerts, stoppingToken).ConfigureAwait(false);
                await publisher.PublishResolvedAsync(result.ResolvedAlerts, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Server automation evaluation failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            try
            {
                await Task.Delay(_options.EvaluationInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

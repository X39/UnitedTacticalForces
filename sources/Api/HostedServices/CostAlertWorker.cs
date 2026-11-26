using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Periodically evaluates the accrued cost of sessions against the thresholds of their cost owners and records each
/// new crossing together with its delivery intents.
/// </summary>
/// <remarks>
/// The loop recomputes everything from the durable ledger on every pass, so it needs no state of its own and a
/// restart of the API neither loses nor repeats a crossing. After each pass the accrued values of costing sessions
/// and the newly recorded alerts are pushed to the update stream, to authorized viewers only. It is registered only
/// while the hosting control plane is not paused.
/// </remarks>
/// <param name="scopeFactory">The factory that creates one dependency scope per evaluation pass.</param>
/// <param name="options">The cost alert options that provide the evaluation interval.</param>
/// <param name="logger">The logger that receives sanitized failure diagnostics.</param>
internal sealed class CostAlertWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertWorker> logger) : BackgroundService
{
    private readonly CostAlertOptions _options = options.Value;

    /// <summary>
    /// Runs evaluation passes every <see cref="CostAlertOptions.EvaluationInterval" /> until the host stops.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the host stops.</returns>
    /// <exception cref="InvalidOperationException">The cost alert options are invalid.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<ICostAlertEvaluator>()
                    .EvaluateAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
                    .ConfigureAwait(false);

                // Publishing happens after the evaluation transaction committed and is best effort; the publisher
                // logs its own failures, and the REST list stays the source of truth.
                var publisher = scope.ServiceProvider.GetRequiredService<ICostAlertPublisher>();
                await publisher.PublishAccrualsAsync(result.Accruals, stoppingToken).ConfigureAwait(false);
                await publisher.PublishRaisedAsync(result.RaisedAlerts, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Cost alert evaluation failed and will retry: {Failure}",
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

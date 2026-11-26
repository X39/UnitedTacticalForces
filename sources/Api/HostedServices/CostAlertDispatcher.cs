using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Periodically delivers due cost alert deliveries from the outbox to Discord and email.
/// </summary>
/// <remarks>
/// Every pass runs in its own dependency scope and delegates to <see cref="ICostAlertDeliveryProcessor" />, which
/// claims rows before sending them. A pass that filled the batch is followed immediately by another one; otherwise
/// the loop waits <see cref="CostAlertOptions.DispatchInterval" />. It is registered only while the hosting control
/// plane is not paused.
/// </remarks>
/// <param name="scopeFactory">The factory that creates one dependency scope per pass.</param>
/// <param name="options">The cost alert options with the dispatch interval and batch size.</param>
/// <param name="logger">The logger that receives sanitized failure diagnostics.</param>
internal sealed class CostAlertDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertDispatcher> logger) : BackgroundService
{
    private readonly CostAlertOptions _options = options.Value;

    /// <summary>
    /// Runs delivery passes until the host stops.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the host stops.</returns>
    /// <exception cref="InvalidOperationException">The cost alert options are invalid.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                using var scope = scopeFactory.CreateScope();
                claimed = await scope.ServiceProvider.GetRequiredService<ICostAlertDeliveryProcessor>()
                    .ProcessDueAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Cost alert delivery pass failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            if (claimed >= _options.DeliveryBatchSize)
                continue;

            try
            {
                await Task.Delay(_options.DispatchInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

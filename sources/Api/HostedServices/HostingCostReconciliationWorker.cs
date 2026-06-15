using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Periodically charges the next estimated provider-cost slice or starts automatic cost-stop before credit expires.
/// </summary>
internal sealed class HostingCostReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<HostingFinanceOptions> options,
    ILogger<HostingCostReconciliationWorker> logger) : BackgroundService
{
    private readonly HostingFinanceOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IHostingCostReconciler>()
                    .ReconcileAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Hosting cost-slice reconciliation failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            try
            {
                await Task.Delay(_options.ReconciliationInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

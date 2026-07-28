using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using LifecycleWorkerOptions = X39.UnitedTacticalForces.Api.Services.GameServerLifecycle.LifecycleWorkerOptions;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Runs provider-wide reconciliation independently of lifecycle-job execution.
/// </summary>
internal sealed class ProviderResourceReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProviderReconciliationOptions> options,
    IOptions<LifecycleWorkerOptions> lifecycleOptions,
    ILogger<ProviderResourceReconciliationWorker> logger) : BackgroundService
{
    private readonly ProviderReconciliationOptions _options = options.Value;
    private readonly LifecycleWorkerOptions _lifecycleOptions = lifecycleOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Configuration that can close a durable intent while its bounded provider settlement is still active is
        // unsafe. Validate before entering the caught scan loop so the host cannot run silently degraded.
        _options.ValidateAgainstProviderSettlement(_lifecycleOptions.ProviderSettlementTimeout);
        if (_options.InitialDelay > TimeSpan.Zero)
            await Task.Delay(_options.InitialDelay, stoppingToken).ConfigureAwait(false);
        logger.LogInformation("Provider-wide hosting reconciliation started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IProviderResourceReconciler>();
                await reconciler.ReconcileAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Provider-wide hosting reconciliation pass failed: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            try
            {
                await Task.Delay(_options.ScanInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

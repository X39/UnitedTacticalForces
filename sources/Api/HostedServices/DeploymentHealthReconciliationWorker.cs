using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Continuously detects idle worker/process loss so billable deployments cannot remain unattended indefinitely.
/// </summary>
internal sealed class DeploymentHealthReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<LifecycleWorkerOptions> options,
    ILogger<DeploymentHealthReconciliationWorker> logger) : BackgroundService
{
    private readonly LifecycleWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.HealthReconciliationInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("Hosting:Worker:HealthReconciliationInterval must be greater than zero.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDeploymentHealthReconciler>()
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
                    "Deployment health reconciliation failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }

            try
            {
                await Task.Delay(_options.HealthReconciliationInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

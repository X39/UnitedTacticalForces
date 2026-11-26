using X39.UnitedTacticalForces.Api.HostedServices;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>Registers hosting processors only when an operator explicitly enables their execution.</summary>
public static class HostingControlPlaneServiceCollectionExtensions
{
    /// <summary>
    /// Validates the startup pause before registering processors. Paused hosts never resolve a queue, reconcile
    /// provider resources, collect costs, evaluate cost alerts, evaluate server automation, prune server metric
    /// history, or recover expired leases through these background services.
    /// </summary>
    /// <param name="services">The API dependency registrations.</param>
    /// <param name="configuration">The final operator configuration.</param>
    /// <returns>The same collection for further registrations.</returns>
    /// <exception cref="InvalidOperationException">The configured pause value is invalid.</exception>
    public static IServiceCollection AddHostingControlPlane(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var state = new HostingControlPlaneState(configuration);
        services.AddSingleton(state);
        if (!state.IsPaused)
        {
            services.AddHostedService<GameServerControlPlaneWorker>();
            services.AddHostedService<DeploymentHealthReconciliationWorker>();
            services.AddHostedService<HostingCostReconciliationWorker>();
            services.AddHostedService<CostAlertWorker>();
            services.AddHostedService<ServerAutomationWorker>();
            services.AddHostedService<CostAlertDispatcher>();
            services.AddHostedService<ProviderResourceReconciliationWorker>();
            services.AddHostedService<ServerMetricsPruneWorker>();
        }
        return services;
    }
}

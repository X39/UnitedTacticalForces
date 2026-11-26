namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Development-only replacement for <see cref="DeploymentHealthReconciler" /> used together with the emulated
/// provider for manual UAT. Seeded running deployments have no external worker, so the real reconciler would
/// fault them as "worker missing" and stop them within seconds. It is registered by <c>Program</c> only when
/// <see cref="HostingRuntime.EmulatedHetznerRuntimeProvider.ConfigurationKey" /> is enabled in Development.
/// </summary>
internal sealed class EmulatedDeploymentHealthReconciler : IDeploymentHealthReconciler
{
    /// <inheritdoc />
    public Task ReconcileAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

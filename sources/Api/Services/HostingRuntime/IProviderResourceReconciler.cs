namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal interface IProviderResourceReconciler
{
    Task ReconcileAllAsync(CancellationToken cancellationToken = default);
}
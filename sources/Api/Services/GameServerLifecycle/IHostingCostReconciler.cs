namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

internal interface IHostingCostReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

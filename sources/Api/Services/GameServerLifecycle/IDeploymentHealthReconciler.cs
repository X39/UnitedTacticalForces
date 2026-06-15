namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

internal interface IDeploymentHealthReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

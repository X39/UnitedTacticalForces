namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

internal interface IHostingCostSliceService
{
    Task ReserveInitialSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default);

    Task CommitInitialReservationAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default);

    Task ReleaseInitialReservationAsync(
        Guid deploymentId,
        string reason,
        CancellationToken cancellationToken = default);

    Task AuthorizeNextSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        bool enforceAffordability,
        CancellationToken cancellationToken = default);
}
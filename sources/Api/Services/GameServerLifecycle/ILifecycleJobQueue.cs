using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface ILifecycleJobQueue
{
    Task<LifecycleJob?> ClaimNextAsync(
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default,
        bool terminateOnly = false);

    Task<bool> RenewLeaseAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        string failureReason,
        TimeSpan retryDelay,
        bool retryAllowed = true,
        CancellationToken cancellationToken = default);
}

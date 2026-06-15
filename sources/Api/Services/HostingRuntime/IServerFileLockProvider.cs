namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public interface IServerFileLockProvider
{
    ValueTask<IAsyncDisposable> AcquireAsync(Guid serverId, CancellationToken cancellationToken = default);
}

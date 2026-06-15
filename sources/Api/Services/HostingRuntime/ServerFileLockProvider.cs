using System.Collections.Concurrent;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

[Singleton<ServerFileLockProvider, IServerFileLockProvider>]
public sealed class ServerFileLockProvider : IServerFileLockProvider
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}

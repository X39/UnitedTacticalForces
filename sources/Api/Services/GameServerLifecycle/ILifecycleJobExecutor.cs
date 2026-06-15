using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface ILifecycleJobExecutor
{
    Task ExecuteAsync(LifecycleJob lifecycleJob, CancellationToken cancellationToken = default);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Exercises real host startup against retained jobs without allowing execution dependencies to activate.</summary>
public sealed class HostingControlPlanePausedStartupTests
{
    /// <summary>Starting a paused host leaves runnable and recoverable persisted jobs untouched.</summary>
    /// <param name="status">The retained lifecycle state that would otherwise qualify for processing.</param>
    /// <returns>A task completing after startup, shutdown, and persisted-state verification.</returns>
    [Theory]
    [InlineData(ELifecycleJobStatus.Queued)]
    [InlineData(ELifecycleJobStatus.RetryPending)]
    [InlineData(ELifecycleJobStatus.Running)]
    public async Task PausedStartup_PreservesRetainedWorkWithoutResolvingExecution(ELifecycleJobStatus status)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var past = Instant.FromUnixTimeSeconds(1_700_000_000);
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Sequence = 1,
            Status = status,
            Stage = "retained-before-restart",
            Attempt = status == ELifecycleJobStatus.Queued ? 0 : 1,
            LeaseOwner = status == ELifecycleJobStatus.Running ? "previous-process" : null,
            LeaseExpires = status == ELifecycleJobStatus.Running ? past : null,
            TimeStampNextAttempt = status == ELifecycleJobStatus.RetryPending ? past : null,
            TimeStampCreated = past,
            TimeStampUpdated = past,
        };
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        var expectedAttempt = job.Attempt;
        var expectedLeaseOwner = job.LeaseOwner;
        store.DbContext.ChangeTracker.Clear();
        var executionDependencyResolutions = 0;
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<DatabaseContext>(store.DbContext);
                services.AddScoped<ILifecycleJobQueue>(_ =>
                {
                    executionDependencyResolutions++;
                    throw new InvalidOperationException("Paused startup must not resolve the job queue.");
                });
                services.AddScoped<ILifecycleJobExecutor>(_ =>
                {
                    executionDependencyResolutions++;
                    throw new InvalidOperationException("Paused startup must not resolve the executor.");
                });
                services.AddHostingControlPlane(new ConfigurationBuilder().Build());
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Empty(host.Services.GetServices<IHostedService>());
        Assert.Equal(0, executionDependencyResolutions);
        var persisted = await store.DbContext.LifecycleJobs.AsNoTracking().SingleAsync();
        Assert.Equal(status, persisted.Status);
        Assert.Equal("retained-before-restart", persisted.Stage);
        Assert.Equal(expectedAttempt, persisted.Attempt);
        Assert.Equal(expectedLeaseOwner, persisted.LeaseOwner);
        Assert.Equal(job.LeaseExpires, persisted.LeaseExpires);
        Assert.Equal(job.TimeStampNextAttempt, persisted.TimeStampNextAttempt);
        Assert.Equal(past, persisted.TimeStampUpdated);
        Assert.Null(persisted.TimeStampCompleted);
        Assert.Empty(await store.DbContext.LifecycleJobAttempts.ToArrayAsync());
        Assert.Empty(await store.DbContext.WorkerCommands.ToArrayAsync());
    }
}

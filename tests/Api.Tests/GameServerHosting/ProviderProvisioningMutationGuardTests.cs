using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class ProviderProvisioningMutationGuardTests
{
    [Fact]
    public async Task ExecuteCreate_WhenDurableBarrierIsClosed_DoesNotInvokeProviderMutation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Terminating);
        seed.Deployment!.IsProvisioningBlocked = true;
        await store.DbContext.SaveChangesAsync();
        var guard = new ProviderProvisioningMutationGuard(new SqliteApiDbContextFactory(store.Connection));
        var mutationCalls = 0;

        var result = await guard.ExecuteCreateAsync(
            seed.Server.PrimaryKey,
            seed.Deployment.PrimaryKey,
            _ =>
            {
                mutationCalls++;
                return Task.FromResult("created");
            });

        Assert.False(result.IsAuthorized);
        Assert.Null(result.Value);
        Assert.Equal(0, mutationCalls);
    }

    [Fact]
    public async Task ExecuteCreate_WhenDurableBarrierIsOpen_InvokesExactlyOneProviderMutation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Provisioning);
        var guard = new ProviderProvisioningMutationGuard(new SqliteApiDbContextFactory(store.Connection));
        var mutationCalls = 0;

        var result = await guard.ExecuteCreateAsync(
            seed.Server.PrimaryKey,
            seed.Deployment!.PrimaryKey,
            _ =>
            {
                mutationCalls++;
                return Task.FromResult("created");
            });

        Assert.True(result.IsAuthorized);
        Assert.Equal("created", result.Value);
        Assert.Equal(1, mutationCalls);
    }

    [Fact]
    public async Task ProvisioningBarrier_RejectsAStaleLifecycleStateWriteAfterTerminateCommits()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Provisioning);
        var factory = new SqliteApiDbContextFactory(store.Connection);
        await using var staleContext = await factory.CreateDbContextAsync();
        var staleDeployment = await staleContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment!.PrimaryKey);

        seed.Deployment!.IsProvisioningBlocked = true;
        seed.Deployment.Status = EDeploymentStatus.Terminating;
        seed.Deployment.Stage = "termination-requested";
        await store.DbContext.SaveChangesAsync();

        staleDeployment.Status = EDeploymentStatus.Running;
        staleDeployment.Stage = "running";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment.PrimaryKey);
        Assert.True(persisted.IsProvisioningBlocked);
        Assert.Equal(EDeploymentStatus.Terminating, persisted.Status);
        Assert.Equal("termination-requested", persisted.Stage);
    }

    [Fact]
    public async Task TerminalBoundary_RejectsPostProviderMetadataFromAStaleExecutor()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Stopping);
        seed.Deployment!.IsProvisioningBlocked = true;
        await store.DbContext.SaveChangesAsync();
        var factory = new SqliteApiDbContextFactory(store.Connection);
        await using var staleContext = await factory.CreateDbContextAsync();
        var staleDeployment = await staleContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment.PrimaryKey);

        var resolvedAt = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Deployment.Status = EDeploymentStatus.Terminated;
        seed.Deployment.Stage = "terminated";
        seed.Deployment.PossibleCost = false;
        seed.Deployment.TimeStampResolved = resolvedAt;
        await store.DbContext.SaveChangesAsync();

        // Simulate a provider call that began before finalization and returned an uncertain result afterward.
        staleDeployment.Stage = "provider-release-outcome-unknown";
        staleDeployment.PossibleCost = true;
        staleDeployment.TimeStampUpdated = resolvedAt + Duration.FromSeconds(1);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment.PrimaryKey);
        Assert.Equal(EDeploymentStatus.Terminated, persisted.Status);
        Assert.Equal("terminated", persisted.Stage);
        Assert.Equal(resolvedAt, persisted.TimeStampResolved);
        Assert.False(persisted.PossibleCost);
    }

    private sealed class SqliteApiDbContextFactory(SqliteConnection connection) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
                .UseSqlite(connection)
                .Options;
            return new NodaTimeSqliteApiDbContext(options);
        }

        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

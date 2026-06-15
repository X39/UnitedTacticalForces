using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HostingPersistenceInvariantTests
{
    [Fact]
    public async Task Database_RejectsSecondActiveCostOwnerAssignmentForSameServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        store.DbContext.GameServerCostOwnerAssignments.Add(new GameServerCostOwnerAssignment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            AssignedByFk = seed.User.PrimaryKey,
            TimeStampAssigned = NodaSystemClock.Instance.GetCurrentInstant(),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => store.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsSecondUnresolvedDeploymentForSameServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.GameServerDeployments.Add(new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            Status = EDeploymentStatus.Provisioning,
            Stage = "queued",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => store.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_AllowsNextDeploymentOnlyAfterPriorDeploymentIsResolved()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Deployment!.Status = EDeploymentStatus.Stopped;
        seed.Deployment.Stage = "stopped";
        seed.Deployment.TimeStampResolved = now;
        seed.Deployment.TimeStampUpdated = now;
        await store.DbContext.SaveChangesAsync();

        var next = new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            Status = EDeploymentStatus.Provisioning,
            Stage = "queued",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.GameServerDeployments.Add(next);
        await store.DbContext.SaveChangesAsync();

        Assert.Equal(
            2,
            await store.DbContext.GameServerDeployments.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey));
        Assert.Equal(
            next.PrimaryKey,
            await store.DbContext.GameServerDeployments
                .Where(q => q.GameServerFk == seed.Server.PrimaryKey && q.TimeStampResolved == null)
                .Select(q => q.PrimaryKey)
                .SingleAsync());
    }

    [Fact]
    public void Model_ConfiguresSingleActiveOwnerAndDeploymentIndexes()
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new NodaTimeSqliteApiDbContext(options);

        AssertUniqueFilteredIndex<GameServerCostOwnerAssignment>(
            dbContext.Model,
            GameServerCostOwnerAssignment.ActiveServerIndexName,
            nameof(GameServerCostOwnerAssignment.GameServerFk),
            GameServerCostOwnerAssignment.ActiveServerIndexFilter);
        AssertUniqueFilteredIndex<GameServerDeployment>(
            dbContext.Model,
            GameServerDeployment.ActiveServerIndexName,
            nameof(GameServerDeployment.GameServerFk),
            GameServerDeployment.ActiveServerIndexFilter);
    }

    [Fact]
    public void DeploymentCostOwner_IsAnInitOnlyAttributionSnapshot()
    {
        var setter = typeof(GameServerDeployment)
            .GetProperty(nameof(GameServerDeployment.CostOwnerFk))!
            .SetMethod!;

        Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
    }

    private static void AssertUniqueFilteredIndex<TEntity>(
        IModel model,
        string indexName,
        string propertyName,
        string expectedFilter)
    {
        var entity = Assert.Single(model.GetEntityTypes(), q => q.ClrType == typeof(TEntity));
        var index = Assert.Single(entity.GetIndexes(), q => q.GetDatabaseName() == indexName);
        Assert.True(index.IsUnique);
        Assert.Equal([propertyName], index.Properties.Select(q => q.Name).ToArray());
        Assert.Equal(expectedFilter, index.GetFilter());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace Api.Tests.GameServerHosting;

public sealed class HostingCostSliceServiceTests
{
    [Fact]
    public async Task InitialReservation_DoesNotDebitUntilCommitted_AndCommitIsIdempotent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 10m;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await service.ReserveInitialSliceAsync(seed.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);
        await service.ReserveInitialSliceAsync(seed.Deployment.PrimaryKey, TestRuntimeProvider.Identifier);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(seed.Deployment).ReloadAsync();
        Assert.Equal(10m, seed.CostOwner.Balance);
        Assert.Equal(0.0250m, seed.Deployment.ReservedProviderCostEur);
        Assert.NotNull(seed.Deployment.CostReservationStartsAt);
        Assert.NotNull(seed.Deployment.CostReservationExpiresAt);
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());

        await service.CommitInitialReservationAsync(seed.Deployment.PrimaryKey, TestRuntimeProvider.Identifier);
        await service.CommitInitialReservationAsync(seed.Deployment.PrimaryKey, TestRuntimeProvider.Identifier);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(seed.Deployment).ReloadAsync();
        Assert.Equal(9.9750m, seed.CostOwner.Balance);
        Assert.Null(seed.Deployment.ReservedProviderCostEur);
        Assert.Null(seed.Deployment.CostReservationStartsAt);
        Assert.Null(seed.Deployment.CostReservationExpiresAt);
        Assert.NotNull(seed.Deployment.CostAuthorizationExpiresAt);
        Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task InitialReservation_DefinitiveNoResourceReleaseNeverChangesBalance()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 10m;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await service.ReserveInitialSliceAsync(seed.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);
        await service.ReleaseInitialReservationAsync(seed.Deployment.PrimaryKey, "provider rejected before allocation");
        await service.ReleaseInitialReservationAsync(seed.Deployment.PrimaryKey, "retry");

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(seed.Deployment).ReloadAsync();
        Assert.Equal(10m, seed.CostOwner.Balance);
        Assert.Null(seed.Deployment.ReservedProviderCostEur);
        Assert.Null(seed.Deployment.CostAuthorizationExpiresAt);
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task InitialReservation_CommitsThePersistedRateWhenConfigurationChangesMidProvisioning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 10m;
        await store.DbContext.SaveChangesAsync();

        await CreateService(store, hourlyRate: 0.10m)
            .ReserveInitialSliceAsync(seed.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);
        await CreateService(store, hourlyRate: 0.20m)
            .CommitInitialReservationAsync(seed.Deployment.PrimaryKey, TestRuntimeProvider.Identifier);

        var entry = await store.DbContext.HostingCostLedgerEntries.SingleAsync();
        Assert.Equal(-0.0250m, entry.Amount);
        Assert.Equal(0.10m, entry.EstimatedHourlyRate);
    }

    [Fact]
    public async Task InitialReservation_CountsOtherOutstandingReservationsForSameOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var second = await HostingTestData.SeedServerAsync(store.DbContext);
        first.CostOwner.Balance = 5.04m;
        var now = Instant.FromUnixTimeSeconds(1_700_000_100);
        var secondDeployment = new X39.UnitedTacticalForces.Database.Hosting.GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = second.Server.PrimaryKey,
            CostOwnerFk = first.CostOwner.PrimaryKey,
            Status = X39.UnitedTacticalForces.Hosting.Contracts.EDeploymentStatus.Provisioning,
            Stage = "provider-intent-persisted",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.GameServerDeployments.Add(secondDeployment);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await service.ReserveInitialSliceAsync(first.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);

        await Assert.ThrowsAsync<HostingAffordabilityException>(() => service.ReserveInitialSliceAsync(
            secondDeployment.PrimaryKey,
            TestRuntimeProvider.Identifier));
        await store.DbContext.Entry(first.CostOwner).ReloadAsync();
        Assert.Equal(5.04m, first.CostOwner.Balance);
        Assert.Single(store.DbContext.GameServerDeployments.Where(q => q.ReservedProviderCostEur != null));
    }

    [Fact]
    public async Task AuthorizeNextSlice_ChargesOneDurableSliceOnlyOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 10m;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await service.AuthorizeNextSliceAsync(
            seed.Deployment!.PrimaryKey,
            TestRuntimeProvider.Identifier,
            enforceAffordability: true);
        await service.AuthorizeNextSliceAsync(
            seed.Deployment.PrimaryKey,
            TestRuntimeProvider.Identifier,
            enforceAffordability: true);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(seed.Deployment).ReloadAsync();
        var entry = await store.DbContext.HostingCostLedgerEntries.SingleAsync();
        Assert.Equal(9.9750m, seed.CostOwner.Balance);
        Assert.Equal(-0.0250m, entry.Amount);
        Assert.Equal(seed.CostOwner.PrimaryKey, entry.CostOwnerFk);
        Assert.Equal(seed.Deployment.PrimaryKey, entry.DeploymentFk);
        Assert.NotNull(seed.Deployment.CostAuthorizationExpiresAt);
    }

    [Fact]
    public async Task AuthorizeNextSlice_RejectsBeforeProviderCostWhenSafetyRangeWouldBeCrossed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 5.01m;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await Assert.ThrowsAsync<HostingAffordabilityException>(() => service.AuthorizeNextSliceAsync(
            seed.Deployment!.PrimaryKey,
            TestRuntimeProvider.Identifier,
            enforceAffordability: true));

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(seed.Deployment!).ReloadAsync();
        Assert.Equal(5.01m, seed.CostOwner.Balance);
        Assert.Null(seed.Deployment!.CostAuthorizationExpiresAt);
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task AuthorizeNextSlice_ContinuesCleanupAccountingAsDebt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 0m;
        seed.Deployment!.IsProvisioningBlocked = true;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await service.AuthorizeNextSliceAsync(
            seed.Deployment.PrimaryKey,
            TestRuntimeProvider.Identifier,
            enforceAffordability: false);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(-0.0250m, seed.CostOwner.Balance);
        Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    private static HostingCostSliceService CreateService(HostingTestStore store, decimal hourlyRate = 0.10m)
        => new(
            store.DbContext,
            new HostingAuditWriter(),
            Options.Create(new HostingFinanceOptions
            {
                MinimumSafetyRangeEur = 5m,
                ReservationSlice = TimeSpan.FromMinutes(15),
                ReconciliationInterval = TimeSpan.FromSeconds(30),
                EstimatedHourlyCostEur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    [TestRuntimeProvider.Identifier] = hourlyRate,
                },
            }));
}

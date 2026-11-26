using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the read-only affordability pre-check of a scheduled start agrees with the executor's initial
/// reservation and never writes. With a 0.10 EUR hourly rate and a 15 minute slice the first slice costs 0.025 EUR and
/// the configured safety range is 5 EUR.
/// </summary>
public sealed class HostingCostSliceAffordabilityTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>
    /// A funded owner with no reservations can pay for the first slice.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task FundedOwner_CanReserve()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Balance = 100m;
        await store.DbContext.SaveChangesAsync();

        var result = await CreateService(store).CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now);

        Assert.True(result);
    }

    /// <summary>
    /// The allowance counts toward the spendable headroom, like in the executor's reservation.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task OwnerWithAllowance_CanReserveFromTheAllowance()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Balance = 0m;
        seed.CostOwner.Allowance = 10m;
        await store.DbContext.SaveChangesAsync();

        var result = await CreateService(store).CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now);

        Assert.True(result);
    }

    /// <summary>
    /// The deployment's own positive estimate replaces the configured provider rate.
    /// </summary>
    /// <param name="balance">The cost owner balance.</param>
    /// <param name="expected">Whether the owner can reserve the 10 EUR slice of a 40 EUR hourly estimate.</param>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Theory]
    [InlineData(15.0, true)]
    [InlineData(14.99, false)]
    public async Task PositiveEstimate_ReplacesTheConfiguredRate(double balance, bool expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Balance = (decimal) balance;
        await store.DbContext.SaveChangesAsync();

        var result = await CreateService(store).CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            40m,
            TestRuntimeProvider.Identifier,
            Now);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// A balance below the slice plus the safety range cannot reserve; exactly the slice plus the range can.
    /// </summary>
    /// <param name="balance">The cost owner balance.</param>
    /// <param name="expected">Whether the owner can reserve.</param>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Theory]
    [InlineData("5.0250", true)]
    [InlineData("5.0249", false)]
    [InlineData("0", false)]
    [InlineData("-3", false)]
    public async Task BalanceBelowSliceAndSafetyRange_CannotReserve(string balance, bool expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Balance = decimal.Parse(balance, System.Globalization.CultureInfo.InvariantCulture);
        await store.DbContext.SaveChangesAsync();

        var result = await CreateService(store).CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Reservations of the owner's other deployments reduce the headroom.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task OtherOutstandingReservations_ReduceTheHeadroom()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        var second = await HostingTestData.SeedServerAsync(store.DbContext);
        first.CostOwner.Balance = 6m;
        store.DbContext.GameServerDeployments.Add(new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = second.Server.PrimaryKey,
            CostOwnerFk = first.CostOwner.PrimaryKey,
            Status = EDeploymentStatus.Provisioning,
            Stage = "provider-intent-persisted",
            ReservedProviderCostEur = 1m,
            CostReservationStartsAt = Now,
            CostReservationExpiresAt = Now + Duration.FromMinutes(15),
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        });
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var withReservation = await service.CanReserveInitialSliceAsync(
            first.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now);
        first.CostOwner.Balance = 6.1m;
        await store.DbContext.SaveChangesAsync();
        var withMoreBalance = await service.CanReserveInitialSliceAsync(
            first.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now);

        Assert.False(withReservation);
        Assert.True(withMoreBalance);
    }

    /// <summary>
    /// A frozen owner, a missing owner, a non-EUR owner and a provider without a configured rate cannot reserve.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task FrozenMissingNonEurOwnerOrUnknownRate_CannotReserve()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Balance = 100m;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        Assert.True(await service.CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now));
        Assert.False(await service.CanReserveInitialSliceAsync(
            Guid.NewGuid(),
            null,
            TestRuntimeProvider.Identifier,
            Now));
        Assert.False(await service.CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            "provider-without-rate",
            Now));

        seed.CostOwner.Currency = "USD";
        await store.DbContext.SaveChangesAsync();
        Assert.False(await service.CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now));

        seed.CostOwner.Currency = "EUR";
        seed.CostOwner.IsFrozen = true;
        await store.DbContext.SaveChangesAsync();
        Assert.False(await service.CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now));
    }

    /// <summary>
    /// The check leaves the reservation columns, the balance, the ledger and the audit trail untouched.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task Check_WritesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = 100m;
        await store.DbContext.SaveChangesAsync();

        Assert.True(await CreateService(store).CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            null,
            TestRuntimeProvider.Identifier,
            Now));

        Assert.False(store.DbContext.ChangeTracker.HasChanges());
        store.DbContext.ChangeTracker.Clear();
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync();
        Assert.Null(deployment.ReservedProviderCostEur);
        Assert.Null(deployment.CostReservationStartsAt);
        Assert.Null(deployment.CostReservationExpiresAt);
        Assert.Null(deployment.CostAuthorizationExpiresAt);
        Assert.Equal(100m, (await store.DbContext.CostOwners.SingleAsync()).Balance);
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    /// <summary>
    /// For the same owner, rate and policy the pre-check is true exactly when the executor's reservation succeeds on a
    /// fresh deployment, on both sides of the safety range boundary.
    /// </summary>
    /// <param name="balance">The cost owner balance.</param>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Theory]
    [InlineData("100")]
    [InlineData("5.0251")]
    [InlineData("5.0250")]
    [InlineData("5.0249")]
    [InlineData("0.5")]
    public async Task Check_AgreesWithTheExecutorsReservation(string balance)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.CostOwner.Balance = decimal.Parse(balance, System.Globalization.CultureInfo.InvariantCulture);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var predicted = await service.CanReserveInitialSliceAsync(
            seed.CostOwner.PrimaryKey,
            seed.Deployment!.EstimatedHourlyCostEur,
            TestRuntimeProvider.Identifier,
            Now);
        var reserved = true;
        try
        {
            await service.ReserveInitialSliceAsync(seed.Deployment.PrimaryKey, TestRuntimeProvider.Identifier);
        }
        catch (HostingAffordabilityException)
        {
            reserved = false;
        }

        Assert.Equal(reserved, predicted);
    }

    private static HostingCostSliceService CreateService(HostingTestStore store)
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
                    [TestRuntimeProvider.Identifier] = 0.10m,
                },
            }));
}

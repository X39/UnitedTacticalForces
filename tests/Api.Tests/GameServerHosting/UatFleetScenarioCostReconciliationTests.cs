using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the real cost reconciler funds the seeded UAT fleet with normal provider-cost slices instead of
/// cost-stopping it. The UAT profile runs the real reconciler, so a seed whose cost owner cannot keep the safety range
/// would turn every possible-cost session off within seconds of startup.
/// </summary>
public sealed class UatFleetScenarioCostReconciliationTests
{
    /// <summary>
    /// The titles of every seeded session that may still generate provider cost and is therefore visited by the
    /// cost reconciler.
    /// </summary>
    private static readonly string[] PossibleCostTitles =
    [
        "UAT 2 Running",
        "UAT 3 Starting (waiting for worker)",
        "UAT 4 Stopping",
        "UAT 6 Attention (preservation failed)",
        "UAT 7 Unconfirmed stop (stopped, cost possible)",
        UatFleetScenarioSeeder.CostingScenarioTitle,
        UatFleetScenarioSeeder.MetricsScenarioTitle,
        UatFleetScenarioSeeder.IdleScenarioTitle,
        UatFleetScenarioSeeder.MaxRuntimeScenarioTitle,
        UatFleetScenarioSeeder.ScheduledStopScenarioTitle,
    ];

    /// <summary>
    /// One pass of the real cost reconciler over a freshly seeded UAT fleet charges a slice for every session whose
    /// authorization is due, queues no automatic cost-stop and leaves every seeded status and fault code in place.
    /// Fifteen minutes after seeding is the point where the unfunded seed used to cost-stop the costing session, once
    /// its prepaid window entered the 30 second renewal threshold; that case expects a renewal instead.
    /// </summary>
    /// <param name="seededMinutesAgo">
    /// How many minutes before the pass the fleet was seeded. Zero is a fresh start. Fifteen is the point where the
    /// prepaid window of the costing session enters the renewal threshold, which used to cost-stop it when the owner
    /// had no credit; this case is a recurrence guard.
    /// </param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public async Task FirstReconcilerPass_FundsEverySeededSessionWithoutCostStop(int seededMinutesAgo)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        // The reconciler and the slice service read the real clock directly, so the seed has to be anchored to it.
        var seedInstant = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(seededMinutesAgo);
        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: seedInstant);
        var seeded = await LoadDeploymentsAsync(store);
        var options = Options.Create(new HostingFinanceOptions
        {
            EstimatedHourlyCostEur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [HetznerRuntimeProvider.Identifier] = 0.10m,
            },
        });
        var auditWriter = new HostingAuditWriter();
        var reconciler = new HostingCostReconciler(
            store.DbContext,
            new HostingCostSliceService(store.DbContext, auditWriter, options),
            new AutomaticCostStopService(store.DbContext, auditWriter),
            options,
            NullLogger<HostingCostReconciler>.Instance);

        var passStartedAt = NodaSystemClock.Instance.GetCurrentInstant();
        await reconciler.ReconcileAsync();
        store.DbContext.ChangeTracker.Clear();

        var renewalThreshold = passStartedAt + Duration.FromTimeSpan(options.Value.ReconciliationInterval);
        var dueCount = seeded.Count(q => q.Deployment.CostAuthorizationExpiresAt is not { } expiry
                                         || expiry <= renewalThreshold);
        var reconciled = await LoadDeploymentsAsync(store);
        Assert.Empty(await store.DbContext.LifecycleJobs.AsNoTracking().ToListAsync());
        Assert.Equal(PossibleCostTitles.Length, reconciled.Count);
        foreach (var title in PossibleCostTitles)
        {
            var before = seeded.Single(q => q.Title == title);
            var after = reconciled.Single(q => q.Title == title);
            Assert.Equal(before.Deployment.Status, after.Deployment.Status);
            Assert.Equal(before.Deployment.FaultCode, after.Deployment.FaultCode);
            Assert.Null(after.Deployment.TimeStampResolved);
            Assert.True(after.Deployment.PossibleCost, title);
            Assert.False(after.Deployment.IsProvisioningBlocked, title);
            Assert.True(after.Deployment.CostAuthorizationExpiresAt > renewalThreshold, title);
        }

        Assert.Equal(
            dueCount,
            await store.DbContext.HostingCostLedgerEntries
                .AsNoTracking()
                .CountAsync(q => q.Kind == EHostingLedgerEntryKind.ProviderCostSlice
                                 && q.TimeStampCreated >= passStartedAt));
        // The seeded costing slice plus one new slice per authorization that was due.
        Assert.Equal(
            1 + dueCount,
            await store.DbContext.HostingCostLedgerEntries
                .AsNoTracking()
                .CountAsync(q => q.Kind == EHostingLedgerEntryKind.ProviderCostSlice));
        var owner = await store.DbContext.CostOwners
            .AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == seed.CostOwner.PrimaryKey);
        Assert.True(owner.Balance >= 5m, $"The owner balance {owner.Balance} fell below the safety range.");
    }

    /// <summary>
    /// Loads the active deployment of every seeded possible-cost session without tracking it.
    /// </summary>
    /// <param name="store">The store that holds the seeded data.</param>
    /// <returns>A task resolving to the server title and the deployment snapshot of each session.</returns>
    private static async Task<List<(string Title, GameServerDeployment Deployment)>> LoadDeploymentsAsync(
        HostingTestStore store)
    {
        var deployments = await store.DbContext.GameServerDeployments
            .AsNoTracking()
            .Include(q => q.GameServer)
            .Where(q => q.PossibleCost)
            .ToListAsync();
        return deployments
            .Where(q => PossibleCostTitles.Contains(q.GameServer!.Title))
            .Select(q => (q.GameServer!.Title, q))
            .ToList();
    }
}

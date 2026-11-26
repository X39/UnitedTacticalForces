using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// D-05 proof that pruning removes each rollup tier at its own retention, never touches the latest snapshot, and
/// keeps no state so it behaves the same after a restart. The tests run against in-memory SQLite.
/// </summary>
public sealed class ServerMetricsPruneTests
{
    private const long Gib = 1024L * 1024L * 1024L;

    /// <summary>
    /// A time aligned to every tier, so retention boundaries fall exactly on bucket starts.
    /// </summary>
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_100 + 864_000);

    /// <summary>
    /// Every tier is cut at its own retention: 2 hours, 26 hours and 7 days 1 hour. A bucket that starts exactly at
    /// the retention limit is kept, younger rows stay, and the snapshot is never pruned.
    /// </summary>
    /// <returns>A task completing after the rows left in each tier were verified.</returns>
    [Fact]
    public async Task Prune_CutsEachTierAtItsOwnRetention_AndKeepsTheSnapshot()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedHistoryAsync(store);
        var snapshotBefore = await store.DbContext.ServerMetricSnapshots.AsNoTracking().SingleAsync();

        var removed = await ServerMetricsTestData.Create(store.DbContext).PruneAsync(Now, CancellationToken.None);

        // Three hours old loses its 15 s row, 27 hours old loses 15 s and 60 s, 8 days old loses all three.
        Assert.Equal(1 + 2 + 3, removed);
        Assert.Equal(
            [Now - Duration.FromHours(2), Now - Duration.FromHours(1)],
            await StartsOfAsync(store, ServerMetricResolutions.Raw));
        Assert.Equal(
            [Now - Duration.FromHours(3), Now - Duration.FromHours(2), Now - Duration.FromHours(1)],
            await StartsOfAsync(store, ServerMetricResolutions.Minute));
        Assert.Equal(
            [
                Now - Duration.FromHours(27),
                Now - Duration.FromHours(3),
                Now - Duration.FromHours(2),
                Now - Duration.FromHours(1),
            ],
            await StartsOfAsync(store, ServerMetricResolutions.FiveMinutes));
        var snapshotAfter = await store.DbContext.ServerMetricSnapshots.AsNoTracking().SingleAsync();
        Assert.Equal(snapshotBefore.PrimaryKey, snapshotAfter.PrimaryKey);
        Assert.Equal(snapshotBefore.ReceivedAt, snapshotAfter.ReceivedAt);
    }

    /// <summary>
    /// A second pass removes nothing, and a fresh service over the same store, which is what a restart builds, prunes
    /// exactly like the long-lived one would have because no state is kept outside the tables.
    /// </summary>
    /// <returns>A task completing after both passes were verified.</returns>
    [Fact]
    public async Task Prune_IsIdempotent_AndRestartSafe()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedHistoryAsync(store);

        var first = await ServerMetricsTestData.Create(store.DbContext).PruneAsync(Now, CancellationToken.None);
        var afterRestart = await ServerMetricsTestData.Create(store.DbContext).PruneAsync(Now, CancellationToken.None);
        var later = await ServerMetricsTestData.Create(store.DbContext)
            .PruneAsync(Now + Duration.FromHours(3), CancellationToken.None);

        Assert.Equal(6, first);
        Assert.Equal(0, afterRestart);
        Assert.True(later > 0, "Advancing the clock lets the next pass prune the newly expired rows.");
    }

    /// <summary>
    /// The tier retentions come from the options, so a shorter configured retention prunes sooner.
    /// </summary>
    /// <returns>A task completing after the configured cut was verified.</returns>
    [Fact]
    public async Task Prune_UsesTheConfiguredRetention()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedHistoryAsync(store);
        var options = new ServerMetricsOptions { RawRetention = TimeSpan.FromHours(1) };

        await ServerMetricsTestData.Create(store.DbContext, options).PruneAsync(Now, CancellationToken.None);

        Assert.Equal([Now - Duration.FromHours(1)], await StartsOfAsync(store, ServerMetricResolutions.Raw));
    }

    private static async Task<Instant[]> StartsOfAsync(HostingTestStore store, int resolution)
        => (await store.DbContext.ServerMetricBuckets
                .AsNoTracking()
                .Where(q => q.ResolutionSeconds == resolution)
                .ToListAsync())
            .Select(q => q.BucketStart)
            .OrderBy(q => q)
            .ToArray();

    /// <summary>
    /// Ingests one sample at 8 days, 27 hours, 3 hours, 2 hours and 1 hour before <see cref="Now"/>; every sample
    /// writes a row in each of the three tiers.
    /// </summary>
    /// <param name="store">The test store to fill.</param>
    /// <returns>A task completing after the rows were written.</returns>
    private static async Task SeedHistoryAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        var worker = new LifecycleWorker
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = deployment.PrimaryKey,
            Identity         = "metrics-prune-worker",
            Status           = EWorkerStatus.Online,
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };
        store.DbContext.LifecycleWorkers.Add(worker);
        await store.DbContext.SaveChangesAsync();
        var service = ServerMetricsTestData.Create(store.DbContext);
        foreach (var age in new[]
                 {
                     Duration.FromDays(8),
                     Duration.FromHours(27),
                     Duration.FromHours(3),
                     Duration.FromHours(2),
                     Duration.FromHours(1),
                 })
        {
            var receivedAt = Now - age;
            await service.IngestAsync(
                ServerMetricsTestData.PrimarySource(deployment, worker.PrimaryKey),
                ServerMetricsTestData.Sample(
                    deployment.PrimaryKey,
                    receivedAt.ToDateTimeOffset(),
                    10d,
                    Gib,
                    8 * Gib,
                    1,
                    32),
                receivedAt,
                CancellationToken.None);
        }
    }
}

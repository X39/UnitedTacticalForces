using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// MET-05 proof that a worker metrics sample is validated, attributed from its trusted source and stored as the
/// server's single snapshot row, with every reading either a value or a reason and a fresh zero staying zero.
/// The tests run against in-memory SQLite.
/// </summary>
public sealed class ServerMetricIngestTests
{
    private const long Gib = 1024L * 1024L * 1024L;

    private static readonly Instant Receipt = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A primary sample with all readings becomes the server's snapshot with the source's attribution.
    /// </summary>
    /// <returns>A task completing after the snapshot was verified.</returns>
    [Fact]
    public async Task PrimarySample_IsStoredAsSnapshot_WithSourceAttribution()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var sampledAt = DateTimeOffset.FromUnixTimeSeconds(1_699_999_995);

        var result = await service.IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker),
            ServerMetricsTestData.Sample(deployment.PrimaryKey, sampledAt, 40d, 3 * Gib, 7 * Gib, 5, 32),
            Receipt,
            CancellationToken.None);

        Assert.True(result.Stored);
        Assert.Equal(deployment.GameServerFk, result.GameServerId);
        var snapshot = await store.DbContext.ServerMetricSnapshots.SingleAsync();
        Assert.Equal(deployment.GameServerFk, snapshot.GameServerFk);
        Assert.Equal(deployment.PrimaryKey, snapshot.DeploymentFk);
        Assert.Equal(worker, snapshot.WorkerFk);
        Assert.Equal(Receipt, snapshot.ReceivedAt);
        Assert.Equal(sampledAt, snapshot.WorkerSampledAt);
        Assert.Equal(40d, snapshot.CpuPercent);
        Assert.Equal(3 * Gib, snapshot.MemoryUsedBytes);
        Assert.Equal(7 * Gib, snapshot.MemoryTotalBytes);
        Assert.Equal(5, snapshot.PlayerCount);
        Assert.Equal(32, snapshot.PlayerCapacity);
        Assert.Null(snapshot.CpuUnavailableReason);
        Assert.Null(snapshot.MemoryUnavailableReason);
        Assert.Null(snapshot.PlayersUnavailableReason);
    }

    /// <summary>
    /// A second sample replaces every reading and reason of the same single row.
    /// </summary>
    /// <returns>A task completing after the replacement was verified.</returns>
    [Fact]
    public async Task SecondSample_ReplacesTheSnapshotRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);
        var first = DateTimeOffset.FromUnixTimeSeconds(1_699_999_990);
        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(deployment.PrimaryKey, first, 40d, 3 * Gib, 7 * Gib, 5, 32),
            Receipt,
            CancellationToken.None);

        var secondReceipt = Receipt + Duration.FromSeconds(15);
        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                first.AddSeconds(15),
                null,
                null,
                null,
                null,
                cpuReason: WorkerMetricUnavailableReasons.Measuring,
                memoryReason: WorkerMetricUnavailableReasons.QueryFailed,
                playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport),
            secondReceipt,
            CancellationToken.None);

        var snapshot = await store.DbContext.ServerMetricSnapshots.SingleAsync();
        Assert.Equal(secondReceipt, snapshot.ReceivedAt);
        Assert.Null(snapshot.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, snapshot.CpuUnavailableReason);
        Assert.Null(snapshot.MemoryUsedBytes);
        Assert.Null(snapshot.MemoryTotalBytes);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, snapshot.MemoryUnavailableReason);
        Assert.Null(snapshot.PlayerCount);
        Assert.Null(snapshot.PlayerCapacity);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, snapshot.PlayersUnavailableReason);
    }

    /// <summary>
    /// An unavailable player count is stored as a reason, while a measured zero stays a zero without a reason.
    /// </summary>
    /// <returns>A task completing after both cases were verified.</returns>
    [Fact]
    public async Task UnavailablePlayers_StoreReason_AndMeasuredZeroStaysZero()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);
        var sampledAt = DateTimeOffset.FromUnixTimeSeconds(1_699_999_995);

        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                sampledAt,
                0d,
                0,
                7 * Gib,
                null,
                playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport),
            Receipt,
            CancellationToken.None);
        var unavailable = await store.DbContext.ServerMetricSnapshots.AsNoTracking().SingleAsync();
        Assert.Null(unavailable.PlayerCount);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, unavailable.PlayersUnavailableReason);
        Assert.Equal(0d, unavailable.CpuPercent);
        Assert.Equal(0L, unavailable.MemoryUsedBytes);

        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(deployment.PrimaryKey, sampledAt, 0d, 0, 7 * Gib, 0, 32),
            Receipt + Duration.FromSeconds(15),
            CancellationToken.None);
        var measured = await store.DbContext.ServerMetricSnapshots.AsNoTracking().SingleAsync();
        Assert.Equal(0, measured.PlayerCount);
        Assert.Null(measured.PlayersUnavailableReason);
    }

    /// <summary>
    /// A sample from a host that is not the primary host is accepted as valid but writes nothing.
    /// </summary>
    /// <returns>A task completing after the missing write was verified.</returns>
    [Fact]
    public async Task NonPrimarySource_ReturnsNotStored_AndWritesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);

        var result = await service.IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker) with { IsPrimaryHost = false },
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                DateTimeOffset.FromUnixTimeSeconds(1_699_999_995),
                40d,
                3 * Gib,
                7 * Gib,
                5,
                32),
            Receipt,
            CancellationToken.None);

        Assert.False(result.Stored);
        Assert.Empty(await store.DbContext.ServerMetricSnapshots.ToListAsync());
    }

    /// <summary>
    /// A contradictory or out-of-contract sample is rejected before any write.
    /// </summary>
    /// <param name="scenario">The name of the invalid combination.</param>
    /// <returns>A task completing after the rejection and the missing write were verified.</returns>
    [Theory]
    [InlineData("cpu-value-and-reason")]
    [InlineData("memory-used-above-total")]
    [InlineData("memory-used-without-total")]
    [InlineData("unknown-reason")]
    [InlineData("machine-reason-for-players")]
    [InlineData("capacity-without-count")]
    [InlineData("cpu-neither")]
    public async Task InvalidSample_IsRejected_AndWritesNothing(string scenario)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var sampledAt = DateTimeOffset.FromUnixTimeSeconds(1_699_999_995);
        var valid = ServerMetricsTestData.Sample(deployment.PrimaryKey, sampledAt, 40d, 3 * Gib, 7 * Gib, 5, 32);
        var invalid = scenario switch
        {
            "cpu-value-and-reason" => valid with { CpuUnavailableReason = WorkerMetricUnavailableReasons.Measuring },
            "memory-used-above-total" => valid with { MemoryUsedBytes = 8 * Gib },
            "memory-used-without-total" => valid with { MemoryTotalBytes = null },
            "unknown-reason" => valid with { CpuPercent = null, CpuUnavailableReason = "gremlins" },
            "machine-reason-for-players" => valid with
            {
                PlayerCount = null,
                PlayersUnavailableReason = WorkerMetricUnavailableReasons.UnsupportedPlatform,
            },
            "capacity-without-count" => valid with { PlayerCount = null },
            "cpu-neither" => valid with { CpuPercent = null },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => service.IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker),
            invalid,
            Receipt,
            CancellationToken.None));

        Assert.Empty(await store.DbContext.ServerMetricSnapshots.ToListAsync());
    }

    /// <summary>
    /// A capacity without a count is accepted when the sample carries a players reason.
    /// </summary>
    [Fact]
    public void CapacityWithoutCount_IsValid_WhenPlayersReasonIsPresent()
    {
        var sample = ServerMetricsTestData.Sample(
            Guid.NewGuid(),
            DateTimeOffset.FromUnixTimeSeconds(1_699_999_995),
            40d,
            3 * Gib,
            7 * Gib,
            null,
            32,
            playersReason: WorkerMetricUnavailableReasons.Measuring);

        Assert.True(Validator.TryValidateObject(sample, new ValidationContext(sample), [], true));
    }

    /// <summary>
    /// One primary sample is also merged into the 15, 60 and 300 second rows, each aligned to the Unix epoch and
    /// carrying the value as count, minimum, maximum and sum.
    /// </summary>
    /// <returns>A task completing after the three rows were verified.</returns>
    [Fact]
    public async Task PrimarySample_CreatesThreeAlignedBuckets()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);

        await service.IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker),
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                DateTimeOffset.FromUnixTimeSeconds(1_699_999_995),
                40d,
                3 * Gib,
                7 * Gib,
                5,
                32),
            Receipt,
            CancellationToken.None);

        var buckets = await store.DbContext.ServerMetricBuckets.AsNoTracking().ToListAsync();
        Assert.Equal(3, buckets.Count);
        AssertBucketStart(buckets, 15, 1_699_999_995);
        AssertBucketStart(buckets, 60, 1_699_999_980);
        AssertBucketStart(buckets, 300, 1_699_999_800);
        foreach (var bucket in buckets)
        {
            Assert.Equal(deployment.GameServerFk, bucket.GameServerFk);
            Assert.Equal(1, bucket.CpuCount);
            Assert.Equal(40d, bucket.CpuSum);
            Assert.Equal(40d, bucket.CpuMin);
            Assert.Equal(40d, bucket.CpuMax);
            Assert.Equal(1, bucket.MemoryCount);
            Assert.Equal(3 * Gib, bucket.MemoryUsedSum);
            Assert.Equal(3 * Gib, bucket.MemoryUsedMin);
            Assert.Equal(3 * Gib, bucket.MemoryUsedMax);
            Assert.Equal(7 * Gib, bucket.MemoryTotalBytes);
            Assert.Equal(1, bucket.PlayersCount);
            Assert.Equal(5L, bucket.PlayersSum);
            Assert.Equal(5, bucket.PlayersMin);
            Assert.Equal(5, bucket.PlayersMax);
            Assert.Equal(32, bucket.PlayerCapacity);
        }
    }

    /// <summary>
    /// Three samples in one 60 second window give that row the exact count and sum with the lowest minimum and the
    /// highest maximum.
    /// </summary>
    /// <returns>A task completing after the merged row was verified.</returns>
    [Fact]
    public async Task SamplesInOneMinute_MergeCountSumMinimumAndMaximum()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);
        var cpuValues = new[] { 10d, 30d, 20d };
        var offsets = new long[] { 5, 20, 40 };

        for (var i = 0; i < cpuValues.Length; i++)
        {
            var received = 1_699_999_980 + offsets[i];
            await service.IngestAsync(
                source,
                ServerMetricsTestData.Sample(
                    deployment.PrimaryKey,
                    DateTimeOffset.FromUnixTimeSeconds(received - 1),
                    cpuValues[i],
                    (i + 1) * Gib,
                    8 * Gib,
                    i),
                Instant.FromUnixTimeSeconds(received),
                CancellationToken.None);
        }

        var minute = await store.DbContext.ServerMetricBuckets.AsNoTracking()
            .SingleAsync(q => q.ResolutionSeconds == 60);
        Assert.Equal(3, minute.CpuCount);
        Assert.Equal(60d, minute.CpuSum);
        Assert.Equal(10d, minute.CpuMin);
        Assert.Equal(30d, minute.CpuMax);
        Assert.Equal(3, minute.MemoryCount);
        Assert.Equal(6 * Gib, minute.MemoryUsedSum);
        Assert.Equal(1 * Gib, minute.MemoryUsedMin);
        Assert.Equal(3 * Gib, minute.MemoryUsedMax);
        Assert.Equal(3, minute.PlayersCount);
        Assert.Equal(3L, minute.PlayersSum);
        Assert.Equal(0, minute.PlayersMin);
        Assert.Equal(2, minute.PlayersMax);
        Assert.Equal(1, await store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// D-05 proof: a single spike among low values keeps its maximum in the 300 second row.
    /// </summary>
    /// <returns>A task completing after the spike was verified in the coarsest tier.</returns>
    [Fact]
    public async Task Spike_SurvivesInTheFiveMinuteTier()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);
        var cpuValues = new[] { 10d, 10d, 95d, 10d };

        for (var i = 0; i < cpuValues.Length; i++)
        {
            var received = 1_699_999_800 + (i * 70);
            await service.IngestAsync(
                source,
                ServerMetricsTestData.Sample(
                    deployment.PrimaryKey,
                    DateTimeOffset.FromUnixTimeSeconds(received - 1),
                    cpuValues[i],
                    Gib,
                    8 * Gib,
                    1),
                Instant.FromUnixTimeSeconds(received),
                CancellationToken.None);
        }

        var fiveMinutes = await store.DbContext.ServerMetricBuckets.AsNoTracking()
            .SingleAsync(q => q.ResolutionSeconds == 300);
        Assert.Equal(4, fiveMinutes.CpuCount);
        Assert.Equal(125d, fiveMinutes.CpuSum);
        Assert.Equal(10d, fiveMinutes.CpuMin);
        Assert.Equal(95d, fiveMinutes.CpuMax);
    }

    /// <summary>
    /// An unavailable player count does not touch the players aggregate, while CPU and memory still count and the
    /// total memory and capacity take the latest non-null value.
    /// </summary>
    /// <returns>A task completing after the counts and latest values were verified.</returns>
    [Fact]
    public async Task UnavailablePlayers_AreNotCounted_AndLatestTotalsWin()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);
        var sampledAt = DateTimeOffset.FromUnixTimeSeconds(1_699_999_995);
        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(deployment.PrimaryKey, sampledAt, 20d, Gib, 8 * Gib, 4, 16),
            Receipt,
            CancellationToken.None);

        await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                sampledAt,
                30d,
                2 * Gib,
                16 * Gib,
                null,
                32,
                playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport),
            Receipt + Duration.FromSeconds(1),
            CancellationToken.None);

        var raw = await store.DbContext.ServerMetricBuckets.AsNoTracking()
            .SingleAsync(q => q.ResolutionSeconds == 15);
        Assert.Equal(2, raw.CpuCount);
        Assert.Equal(2, raw.MemoryCount);
        Assert.Equal(1, raw.PlayersCount);
        Assert.Equal(4L, raw.PlayersSum);
        Assert.Equal(4, raw.PlayersMin);
        Assert.Equal(4, raw.PlayersMax);
        Assert.Equal(16 * Gib, raw.MemoryTotalBytes);
        Assert.Equal(32, raw.PlayerCapacity);
    }

    /// <summary>
    /// A sample for another deployment, from the future beyond the skew limit or older than the stale limit is
    /// rejected and writes neither a snapshot nor a bucket.
    /// </summary>
    /// <param name="scenario">The name of the rejected case.</param>
    /// <returns>A task completing after the rejection and the missing writes were verified.</returns>
    [Theory]
    [InlineData("other-deployment")]
    [InlineData("future")]
    [InlineData("stale")]
    public async Task ForeignFutureOrStaleSample_IsRejected_AndWritesNothing(string scenario)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var defaults = new ServerMetricsOptions();
        var deploymentId = scenario == "other-deployment" ? Guid.NewGuid() : deployment.PrimaryKey;
        var sampledAt = scenario switch
        {
            "future" => Receipt + Duration.FromTimeSpan(defaults.MaxSampleSkew) + Duration.FromSeconds(1),
            "stale" => Receipt - Duration.FromTimeSpan(defaults.StaleAfter) - Duration.FromSeconds(1),
            _ => Receipt,
        };

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => service.IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker),
            ServerMetricsTestData.Sample(
                deploymentId,
                sampledAt.ToDateTimeOffset(),
                40d,
                3 * Gib,
                7 * Gib,
                5,
                32),
            Receipt,
            CancellationToken.None));

        Assert.Empty(await store.DbContext.ServerMetricSnapshots.ToListAsync());
        Assert.Empty(await store.DbContext.ServerMetricBuckets.ToListAsync());
    }

    /// <summary>
    /// Samples exactly at the skew and stale limits are still accepted.
    /// </summary>
    /// <returns>A task completing after both boundary samples were stored.</returns>
    [Fact]
    public async Task SamplesAtTheSkewAndStaleLimits_AreAccepted()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (deployment, worker) = await SeedAsync(store);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var defaults = new ServerMetricsOptions();
        var source = ServerMetricsTestData.PrimarySource(deployment, worker);

        var future = await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                (Receipt + Duration.FromTimeSpan(defaults.MaxSampleSkew)).ToDateTimeOffset(),
                1d,
                Gib,
                2 * Gib,
                0),
            Receipt,
            CancellationToken.None);
        var stale = await service.IngestAsync(
            source,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                (Receipt - Duration.FromTimeSpan(defaults.StaleAfter)).ToDateTimeOffset(),
                1d,
                Gib,
                2 * Gib,
                0),
            Receipt,
            CancellationToken.None);

        Assert.True(future.Stored);
        Assert.True(stale.Stored);
    }

    /// <summary>
    /// The default options are valid and each unusable value is refused.
    /// </summary>
    /// <param name="scenario">The name of the unusable setting.</param>
    [Theory]
    [InlineData("report-interval-zero")]
    [InlineData("stale-intervals-one")]
    [InlineData("skew-zero")]
    [InlineData("prune-zero")]
    [InlineData("raw-retention-zero")]
    [InlineData("raw-retention-short")]
    [InlineData("minute-retention-short")]
    [InlineData("five-minute-retention-short")]
    public void Options_RejectUnusableValues(string scenario)
    {
        new ServerMetricsOptions().Validate();
        var invalid = scenario switch
        {
            "report-interval-zero" => new ServerMetricsOptions { ReportInterval = TimeSpan.Zero },
            "stale-intervals-one" => new ServerMetricsOptions { StaleAfterIntervals = 1 },
            "skew-zero" => new ServerMetricsOptions { MaxSampleSkew = TimeSpan.Zero },
            "prune-zero" => new ServerMetricsOptions { PruneInterval = TimeSpan.Zero },
            "raw-retention-zero" => new ServerMetricsOptions { RawRetention = TimeSpan.Zero },
            "raw-retention-short" => new ServerMetricsOptions { RawRetention = TimeSpan.FromMinutes(59) },
            "minute-retention-short" => new ServerMetricsOptions { MinuteRetention = TimeSpan.FromHours(23) },
            "five-minute-retention-short" => new ServerMetricsOptions { FiveMinuteRetention = TimeSpan.FromDays(6) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        Assert.Throws<InvalidOperationException>(invalid.Validate);
    }

    private static void AssertBucketStart(IEnumerable<ServerMetricBucket> buckets, int resolution, long unixSeconds)
        => Assert.Equal(
            Instant.FromUnixTimeSeconds(unixSeconds),
            Assert.Single(buckets, q => q.ResolutionSeconds == resolution).BucketStart);

    private static async Task<(GameServerDeployment Deployment, Guid WorkerId)> SeedAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        var worker = new LifecycleWorker
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = deployment.PrimaryKey,
            Identity         = "metrics-worker",
            Status           = EWorkerStatus.Online,
            TimeStampCreated = Receipt,
            TimeStampUpdated = Receipt,
        };
        store.DbContext.LifecycleWorkers.Add(worker);
        await store.DbContext.SaveChangesAsync();
        return (deployment, worker.PrimaryKey);
    }
}

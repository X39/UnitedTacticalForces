using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// MET-04 and D-09 proof that the history is read from the durable rollup tiers with API-computed aggregates, that
/// buckets the worker never reported stay absent, and that only viewers may read it. The samples are ingested through
/// the real ingest path so the tiers are real. The tests run against in-memory SQLite.
/// </summary>
public sealed class ServerMetricsHistoryTests
{
    private const long Gib = 1024L * 1024L * 1024L;

    /// <summary>
    /// A receipt time aligned to every tier, so the windows of the three tiers start at the same instant.
    /// </summary>
    private static readonly Instant Receipt = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>
    /// The first sample is a bucket-0 reading and the second lies 45 seconds later, leaving two empty 15 second
    /// windows between them. The hour history has two points and nothing in the gap.
    /// </summary>
    /// <returns>A task completing after the series was verified.</returns>
    [Fact]
    public async Task HourRange_ReturnsComputedAggregates_AndLeavesEmptyWindowsAbsent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        var second = Receipt + Duration.FromSeconds(45);
        await IngestAsync(store, seed, Receipt, 10d, 2 * Gib, 8 * Gib, 2, 32);
        await IngestAsync(store, seed, second, 30d, 4 * Gib, 8 * Gib, 6, 32);
        var now = Receipt + Duration.FromSeconds(60);

        var dto = await ReadAsync(store, seed, EServerMetricsRange.Hour, now);

        Assert.Equal(seed.Server.PrimaryKey, dto.GameServerId);
        Assert.Equal("1h", dto.Range);
        Assert.Equal(15, dto.BucketSeconds);
        Assert.Equal(now - Duration.FromHours(1), dto.From);
        Assert.Equal(now, dto.To);
        Assert.Equal([Receipt, second], dto.Points.Select(q => q.BucketStart).ToArray());
        Assert.Equal(new ServerMetricAggregateDto { Average = 10d, Minimum = 10d, Maximum = 10d }, dto.Points[0].Cpu);
        Assert.Equal(new ServerMetricAggregateDto { Average = 30d, Minimum = 30d, Maximum = 30d }, dto.Points[1].Cpu);
        Assert.Equal(
            new ServerMetricAggregateDto { Average = 4 * Gib, Minimum = 4 * Gib, Maximum = 4 * Gib },
            dto.Points[1].MemoryUsed);
        Assert.Equal(6d, dto.Points[1].Players!.Average);
    }

    /// <summary>
    /// The day and week ranges read the coarser tiers, whose single bucket merges both samples into the average,
    /// minimum and maximum.
    /// </summary>
    /// <param name="range">The range under test.</param>
    /// <param name="bucketSeconds">The tier length the range must read.</param>
    /// <param name="code">The range code the history must carry.</param>
    /// <returns>A task completing after the tier was verified.</returns>
    [Theory]
    [InlineData(EServerMetricsRange.Day, 60, "24h")]
    [InlineData(EServerMetricsRange.Week, 300, "7d")]
    public async Task CoarserRanges_ReadTheirOwnTier(EServerMetricsRange range, int bucketSeconds, string code)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        await IngestAsync(store, seed, Receipt, 10d, 2 * Gib, 8 * Gib, 2, 32);
        await IngestAsync(store, seed, Receipt + Duration.FromSeconds(45), 30d, 4 * Gib, 8 * Gib, 6, 32);
        var now = Receipt + Duration.FromSeconds(60);

        var dto = await ReadAsync(store, seed, range, now);

        Assert.Equal(code, dto.Range);
        Assert.Equal(bucketSeconds, dto.BucketSeconds);
        Assert.Equal(now - ServerMetricResolutions.Span(range), dto.From);
        var point = Assert.Single(dto.Points);
        Assert.Equal(Receipt, point.BucketStart);
        Assert.Equal(new ServerMetricAggregateDto { Average = 20d, Minimum = 10d, Maximum = 30d }, point.Cpu);
        Assert.Equal(new ServerMetricAggregateDto { Average = 4d, Minimum = 2d, Maximum = 6d }, point.Players);
    }

    /// <summary>
    /// A bucket whose samples never carried a player count has no players aggregate while CPU and memory keep their
    /// values, and a measured zero stays a zero aggregate. The average is rounded to two decimals.
    /// </summary>
    /// <returns>A task completing after the nulls and the rounding were verified.</returns>
    [Fact]
    public async Task UnavailableMetric_IsNullNotZero_AndMeasuredZeroStaysZero()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        var zeroWindow = Receipt + Duration.FromSeconds(15);
        await IngestAsync(store, seed, Receipt, 10d, Gib, 8 * Gib, null, null);
        await IngestAsync(store, seed, Receipt + Duration.FromSeconds(1), 10d, Gib, 8 * Gib, null, null);
        await IngestAsync(store, seed, Receipt + Duration.FromSeconds(2), 11d, Gib, 8 * Gib, null, null);
        await IngestAsync(store, seed, zeroWindow, 5d, Gib, 8 * Gib, 0, 32);

        var dto = await ReadAsync(store, seed, EServerMetricsRange.Hour, Receipt + Duration.FromSeconds(30));

        Assert.Equal(2, dto.Points.Count);
        Assert.Null(dto.Points[0].Players);
        Assert.Equal(10.33d, dto.Points[0].Cpu!.Average);
        Assert.Equal(10d, dto.Points[0].Cpu!.Minimum);
        Assert.Equal(11d, dto.Points[0].Cpu!.Maximum);
        Assert.NotNull(dto.Points[0].MemoryUsed);
        Assert.Equal(new ServerMetricAggregateDto { Average = 0d, Minimum = 0d, Maximum = 0d }, dto.Points[1].Players);
    }

    /// <summary>
    /// The memory total and player capacity are the newest non-null values of the range, even when a later bucket
    /// reported neither.
    /// </summary>
    /// <returns>A task completing after the newest values were verified.</returns>
    [Fact]
    public async Task TotalsAndCapacity_AreTheNewestNonNullValues()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        await IngestAsync(store, seed, Receipt, 10d, Gib, 4 * Gib, 1, 16);
        await IngestAsync(store, seed, Receipt + Duration.FromSeconds(15), 10d, Gib, 8 * Gib, 1, 32);
        await IngestAsync(store, seed, Receipt + Duration.FromSeconds(30), null, null, null, null, null);

        var dto = await ReadAsync(store, seed, EServerMetricsRange.Hour, Receipt + Duration.FromSeconds(45));

        Assert.Equal(3, dto.Points.Count);
        Assert.Null(dto.Points[2].Cpu);
        Assert.Null(dto.Points[2].MemoryUsed);
        Assert.Null(dto.Points[2].Players);
        Assert.Equal(8 * Gib, dto.MemoryTotalBytes);
        Assert.Equal(32, dto.PlayerCapacity);
    }

    /// <summary>
    /// A server without any bucket has an empty series and no totals, never a synthesized point.
    /// </summary>
    /// <returns>A task completing after the empty series was verified.</returns>
    [Fact]
    public async Task ServerWithoutHistory_ReturnsAnEmptySeries()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var dto = await ReadAsync(store, seed, EServerMetricsRange.Week, Receipt);

        Assert.Empty(dto.Points);
        Assert.Null(dto.MemoryTotalBytes);
        Assert.Null(dto.PlayerCapacity);
    }

    /// <summary>
    /// Only buckets inside the window are returned: a bucket older than the range is left out of the hour but still
    /// belongs to the day, and a fresh service over the same store (a reload) reads the same series.
    /// </summary>
    /// <returns>A task completing after the window and the reload were verified.</returns>
    [Fact]
    public async Task Window_IsRespected_AndAReloadReadsTheSameSeries()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        var old = Receipt - Duration.FromHours(2);
        await IngestAsync(store, seed, old, 70d, Gib, 8 * Gib, 1, 32);
        await IngestAsync(store, seed, Receipt, 10d, Gib, 8 * Gib, 1, 32);
        var now = Receipt + Duration.FromSeconds(30);

        var hour = await ReadAsync(store, seed, EServerMetricsRange.Hour, now);
        var day = await ReadAsync(store, seed, EServerMetricsRange.Day, now);
        var reloaded = await ReadAsync(store, seed, EServerMetricsRange.Day, now);

        Assert.Equal([Receipt], hour.Points.Select(q => q.BucketStart).ToArray());
        Assert.Equal(2, day.Points.Count);
        Assert.Equal(70d, day.Points[0].Cpu!.Average);
        Assert.Equal(day.Points.Select(q => q.BucketStart), reloaded.Points.Select(q => q.BucketStart));
        Assert.Equal(day.Points.Select(q => q.Cpu), reloaded.Points.Select(q => q.Cpu));
    }

    /// <summary>
    /// A principal without the server view claim and an unknown server both get not found.
    /// </summary>
    /// <returns>A task completing after both refusals were verified.</returns>
    [Fact]
    public async Task NonViewerAndUnknownServer_GetNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        await IngestAsync(store, seed, Receipt, 10d, Gib, 8 * Gib, 1, 32);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
        var outsider = HostingTestData.Principal(seed.User);

        var dto = await service.GetHistoryAsync(
            seed.Server.PrimaryKey, viewer, EServerMetricsRange.Hour, Receipt, CancellationToken.None);
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.GetHistoryAsync(
                seed.Server.PrimaryKey, outsider, EServerMetricsRange.Hour, Receipt, CancellationToken.None));
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.GetHistoryAsync(
                Guid.NewGuid(), viewer, EServerMetricsRange.Hour, Receipt, CancellationToken.None));

        Assert.Single(dto.Points);
    }

    /// <summary>
    /// Exactly the three range codes parse, case-sensitively, and every parsed range formats back to its code.
    /// </summary>
    /// <param name="value">The code to parse.</param>
    /// <param name="accepted">Whether the code must be accepted.</param>
    [Theory]
    [InlineData("1h", true)]
    [InlineData("24h", true)]
    [InlineData("7d", true)]
    [InlineData("2h", false)]
    [InlineData("1H", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_AcceptsExactlyTheThreeCodes(string? value, bool accepted)
    {
        var parsed = ServerMetricResolutions.TryParse(value, out var range);

        Assert.Equal(accepted, parsed);
        if (accepted) Assert.Equal(value, ServerMetricResolutions.Format(range));
    }

    private static async Task<HostingTestSeed> SeedAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        store.DbContext.LifecycleWorkers.Add(
            new LifecycleWorker
            {
                PrimaryKey       = Guid.NewGuid(),
                DeploymentFk     = seed.Deployment!.PrimaryKey,
                Identity         = "metrics-history-worker",
                Status           = EWorkerStatus.Online,
                TimeStampCreated = Receipt,
                TimeStampUpdated = Receipt,
            });
        await store.DbContext.SaveChangesAsync();
        return seed;
    }

    private static async Task IngestAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        Instant receivedAt,
        double? cpu,
        long? used,
        long? total,
        int? players,
        int? capacity)
    {
        var deployment = seed.Deployment!;
        var worker = store.DbContext.LifecycleWorkers.Single(q => q.DeploymentFk == deployment.PrimaryKey);
        await ServerMetricsTestData.Create(store.DbContext).IngestAsync(
            ServerMetricsTestData.PrimarySource(deployment, worker.PrimaryKey),
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                receivedAt.ToDateTimeOffset(),
                cpu,
                used,
                total,
                players,
                capacity,
                cpuReason: cpu is null ? WorkerMetricUnavailableReasons.Measuring : null,
                memoryReason: used is null ? WorkerMetricUnavailableReasons.QueryFailed : null,
                playersReason: players is null ? WorkerMetricUnavailableReasons.GameDoesNotReport : null),
            receivedAt,
            CancellationToken.None);
    }

    private static Task<GameServerMetricsHistoryDto> ReadAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        EServerMetricsRange range,
        Instant now)
        => ServerMetricsTestData.Create(store.DbContext).GetHistoryAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            range,
            now,
            CancellationToken.None);
}

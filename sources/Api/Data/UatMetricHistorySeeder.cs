using NodaTime;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Data;

/// <summary>
/// Development-only seeder that writes a deterministic week of tiered server metric history so the metrics charts, their
/// gaps and the seven day range can be checked right after a UAT reseed.
/// </summary>
/// <remarks>
/// <para>
/// This is UAT seed data written directly like the Phase 66 cost slice seed: it fills the rollup rows without going
/// through the worker channel. Live values never enter this way; they arrive only through the worker channel handler,
/// fed by the UAT worker emulation, which reads the same synthetic series through <see cref="SampleAt"/>.
/// </para>
/// <para>
/// The history covers the retention of each tier: 300 second rows for seven days, 60 second rows for 26 hours and
/// 15 second rows for two hours, all ending at the last complete window before the seeding instant. A stopped period
/// two days ago and a short worker outage 40 minutes ago are left out of every tier as missing rows, so a gap stays a
/// gap. One minute and one five-minute window five hours ago carry a CPU spike that survives in their maximum.
/// </para>
/// </remarks>
internal static class UatMetricHistorySeeder
{
    /// <summary>The number of bytes in one gibibyte.</summary>
    private const long Gibibyte = 1024L * 1024 * 1024;

    /// <summary>The seconds between two samples of a worker, which is the finest resolution.</summary>
    private const int SampleSeconds = ServerMetricResolutions.Raw;

    /// <summary>The CPU percentage the samples around the spike are capped at so the bucket average stays plain.</summary>
    private const double SpikeNeighborCpuCap = 40d;

    /// <summary>The total memory of the emulated host in bytes: 7 GiB.</summary>
    internal const long MemoryTotalBytes = 7 * Gibibyte;

    /// <summary>The player capacity of the emulated game server.</summary>
    internal const int PlayerCapacity = 32;

    /// <summary>The CPU percentage of the one spike sample five hours before seeding.</summary>
    internal const double SpikeCpuMaximum = 95d;

    /// <summary>How long before seeding the stopped period begins.</summary>
    internal static readonly Duration StoppedPeriodAge = Duration.FromDays(2);

    /// <summary>How long the stopped period lasts.</summary>
    internal static readonly Duration StoppedPeriodLength = Duration.FromHours(3);

    /// <summary>How long before seeding the short worker outage begins.</summary>
    internal static readonly Duration OutageAge = Duration.FromMinutes(40);

    /// <summary>How long the short worker outage lasts.</summary>
    internal static readonly Duration OutageLength = Duration.FromMinutes(10);

    /// <summary>How long before seeding the spike windows begin.</summary>
    internal static readonly Duration SpikeAge = Duration.FromHours(5);

    /// <summary>The seeded tiers: the window length in seconds and how far back each tier reaches.</summary>
    private static readonly (int ResolutionSeconds, Duration Span)[] Tiers =
    [
        (ServerMetricResolutions.FiveMinutes, Duration.FromDays(7)),
        (ServerMetricResolutions.Minute, Duration.FromHours(26)),
        (ServerMetricResolutions.Raw, Duration.FromHours(2)),
    ];

    /// <summary>
    /// Adds the tiered history rows of one server to the database context without saving them.
    /// </summary>
    /// <param name="dbContext">The database context the rollup rows are added to.</param>
    /// <param name="server">The server the history belongs to.</param>
    /// <param name="now">The seeding instant every offset of the history is relative to.</param>
    internal static void Add(DatabaseContext dbContext, GameServer server, Instant now)
    {
        var stoppedStart = now - StoppedPeriodAge;
        var stoppedEnd = stoppedStart + StoppedPeriodLength;
        var outageStart = now - OutageAge;
        var outageEnd = outageStart + OutageLength;
        foreach (var (resolution, span) in Tiers)
        {
            var end = ServerMetricResolutions.Floor(now, resolution);
            var earliest = now - span;
            var start = ServerMetricResolutions.Floor(earliest, resolution);
            if (start < earliest)
                start += Duration.FromSeconds(resolution);
            var spikeStart = ServerMetricResolutions.Floor(now - SpikeAge, resolution);
            for (var bucketStart = start; bucketStart < end; bucketStart += Duration.FromSeconds(resolution))
            {
                if (IsWithin(bucketStart, stoppedStart, stoppedEnd) || IsWithin(bucketStart, outageStart, outageEnd))
                    continue;
                var hasSpike = resolution != ServerMetricResolutions.Raw && bucketStart == spikeStart;
                dbContext.ServerMetricBuckets.Add(CreateBucket(server, resolution, bucketStart, hasSpike));
            }
        }
    }

    /// <summary>
    /// Gets the synthetic readings of the emulated host at an instant. The series is smooth, bounded and a pure function
    /// of the instant, so the seeded history and the live emulation continue each other.
    /// </summary>
    /// <param name="instant">The instant to read the series at.</param>
    /// <returns>
    /// A CPU percentage from 20 to 60, the used memory in bytes from 3.2 to 3.6 GiB and a player count from 0 to 12.
    /// </returns>
    internal static (double CpuPercent, long MemoryUsedBytes, int PlayerCount) SampleAt(Instant instant)
    {
        var seconds = (double) instant.ToUnixTimeSeconds();
        var cpu = 40d + 20d * Math.Sin(2d * Math.PI * seconds / 1800d);
        var memory = 3.4d * Gibibyte + 0.2d * Gibibyte * Math.Sin(2d * Math.PI * seconds / 5400d);
        var players = 6d + 6d * Math.Sin(2d * Math.PI * seconds / 7200d);
        return (Math.Round(cpu, 2), (long) Math.Round(memory), (int) Math.Round(players));
    }

    /// <summary>
    /// Tells whether a window start lies inside a half-open interval.
    /// </summary>
    /// <param name="instant">The window start to test.</param>
    /// <param name="start">The inclusive start of the interval.</param>
    /// <param name="end">The exclusive end of the interval.</param>
    /// <returns><see langword="true"/> when the window start is at or after the start and before the end.</returns>
    private static bool IsWithin(Instant instant, Instant start, Instant end) => instant >= start && instant < end;

    /// <summary>
    /// Builds one rollup row from the samples a worker would have reported in its window.
    /// </summary>
    /// <param name="server">The server the row belongs to.</param>
    /// <param name="resolution">The window length in seconds.</param>
    /// <param name="bucketStart">The inclusive window start.</param>
    /// <param name="hasSpike">Whether the first sample of the window is the CPU spike.</param>
    /// <returns>The row with count, sum, minimum and maximum of every metric.</returns>
    private static ServerMetricBucket CreateBucket(
        GameServer server,
        int resolution,
        Instant bucketStart,
        bool hasSpike)
    {
        var bucket = new ServerMetricBucket
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            GameServer = server,
            ResolutionSeconds = resolution,
            BucketStart = bucketStart,
            MemoryTotalBytes = MemoryTotalBytes,
            PlayerCapacity = PlayerCapacity,
        };
        var samples = resolution / SampleSeconds;
        for (var index = 0; index < samples; index++)
        {
            var (cpu, memory, players) = SampleAt(bucketStart + Duration.FromSeconds(index * SampleSeconds));
            cpu = hasSpike ? index == 0 ? SpikeCpuMaximum : Math.Min(cpu, SpikeNeighborCpuCap) : cpu;
            bucket.CpuCount++;
            bucket.CpuSum += cpu;
            bucket.CpuMin = bucket.CpuMin is { } cpuMin ? Math.Min(cpuMin, cpu) : cpu;
            bucket.CpuMax = bucket.CpuMax is { } cpuMax ? Math.Max(cpuMax, cpu) : cpu;
            bucket.MemoryCount++;
            bucket.MemoryUsedSum += memory;
            bucket.MemoryUsedMin = bucket.MemoryUsedMin is { } memoryMin ? Math.Min(memoryMin, memory) : memory;
            bucket.MemoryUsedMax = bucket.MemoryUsedMax is { } memoryMax ? Math.Max(memoryMax, memory) : memory;
            bucket.PlayersCount++;
            bucket.PlayersSum += players;
            bucket.PlayersMin = bucket.PlayersMin is { } playersMin ? Math.Min(playersMin, players) : players;
            bucket.PlayersMax = bucket.PlayersMax is { } playersMax ? Math.Max(playersMax, players) : players;
        }

        return bucket;
    }
}

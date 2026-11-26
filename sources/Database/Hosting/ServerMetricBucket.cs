using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// One streaming rollup row of a server's primary-host metrics: every sample received in one time window of one
/// resolution, merged into a count, sum, minimum and maximum per metric.
/// </summary>
/// <remarks>
/// Rows exist at 15, 60 and 300 second resolution. The window is the API receipt time floored to the resolution
/// since the Unix epoch. The minimum and maximum are kept next to the sum so the peak of a spike survives
/// downsampling, and the average is the sum divided by the count. A metric that was unavailable in a sample adds
/// nothing to its count, so unavailable readings never pull an average towards zero. Rows are keyed by server,
/// because history spans sessions and servers are archived, never deleted.
/// </remarks>
[Index(nameof(GameServerFk), nameof(ResolutionSeconds), nameof(BucketStart), IsUnique = true)]
[Index(nameof(ResolutionSeconds), nameof(BucketStart))]
public sealed class ServerMetricBucket : IPrimaryKey<Guid>
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the server the samples belong to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server the samples belong to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the window length in seconds: 15, 60 or 300.
    /// </summary>
    public int ResolutionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the inclusive start of the window, aligned to <see cref="ResolutionSeconds"/> since the Unix
    /// epoch.
    /// </summary>
    public Instant BucketStart { get; set; }

    /// <summary>
    /// Gets or sets the number of samples that carried a CPU value.
    /// </summary>
    public int CpuCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the CPU percentages of those samples.
    /// </summary>
    public double CpuSum { get; set; }

    /// <summary>
    /// Gets or sets the lowest CPU percentage, or <see langword="null"/> while <see cref="CpuCount"/> is zero.
    /// </summary>
    public double? CpuMin { get; set; }

    /// <summary>
    /// Gets or sets the highest CPU percentage, or <see langword="null"/> while <see cref="CpuCount"/> is zero.
    /// </summary>
    public double? CpuMax { get; set; }

    /// <summary>
    /// Gets or sets the number of samples that carried a memory value.
    /// </summary>
    public int MemoryCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the used memory bytes of those samples.
    /// </summary>
    public long MemoryUsedSum { get; set; }

    /// <summary>
    /// Gets or sets the lowest used memory in bytes, or <see langword="null"/> while <see cref="MemoryCount"/> is
    /// zero.
    /// </summary>
    public long? MemoryUsedMin { get; set; }

    /// <summary>
    /// Gets or sets the highest used memory in bytes, or <see langword="null"/> while <see cref="MemoryCount"/> is
    /// zero.
    /// </summary>
    public long? MemoryUsedMax { get; set; }

    /// <summary>
    /// Gets or sets the latest reported total memory in bytes, or <see langword="null"/> when none was reported.
    /// </summary>
    public long? MemoryTotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of samples that carried a player count.
    /// </summary>
    public int PlayersCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the player counts of those samples.
    /// </summary>
    public long PlayersSum { get; set; }

    /// <summary>
    /// Gets or sets the lowest player count, or <see langword="null"/> while <see cref="PlayersCount"/> is zero.
    /// </summary>
    public int? PlayersMin { get; set; }

    /// <summary>
    /// Gets or sets the highest player count, or <see langword="null"/> while <see cref="PlayersCount"/> is zero.
    /// </summary>
    public int? PlayersMax { get; set; }

    /// <summary>
    /// Gets or sets the latest reported player capacity, or <see langword="null"/> when none was reported.
    /// </summary>
    public int? PlayerCapacity { get; set; }
}

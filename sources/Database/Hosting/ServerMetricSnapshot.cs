using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The latest accepted primary-host metrics sample of one server, with every reading and its unavailable reason.
/// Exactly one row exists per server.
/// </summary>
/// <remarks>
/// Current readings are read from this row so they never need arithmetic over the rollup tiers. Each reading is
/// either a value or a reason code, never both and never a zero stand-in. The row is replaced by every accepted
/// sample and is not tracked by the fleet change interceptor, because metrics are not fleet state.
/// </remarks>
[Index(nameof(GameServerFk), IsUnique = true)]
public sealed class ServerMetricSnapshot : IPrimaryKey<Guid>
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the server the sample belongs to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server the sample belongs to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the deployment (session) the sample was taken in.
    /// </summary>
    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the deployment the sample was taken in.
    /// </summary>
    public Guid DeploymentFk { get; set; }

    /// <summary>
    /// Gets or sets the worker that reported the sample.
    /// </summary>
    [ForeignKey(nameof(WorkerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleWorker? Worker { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the worker that reported the sample.
    /// </summary>
    public Guid WorkerFk { get; set; }

    /// <summary>
    /// Gets or sets the API receipt time of the sample, which is the time freshness and bucketing are judged by.
    /// </summary>
    public Instant ReceivedAt { get; set; }

    /// <summary>
    /// Gets or sets the sampling time reported by the worker, kept for diagnostics only.
    /// </summary>
    public DateTimeOffset WorkerSampledAt { get; set; }

    /// <summary>
    /// Gets or sets the CPU utilization of the host in percent, or <see langword="null"/> when a reason applies.
    /// </summary>
    public double? CpuPercent { get; set; }

    /// <summary>
    /// Gets or sets why the CPU reading is unavailable.
    /// </summary>
    [MaxLength(64)]
    public string? CpuUnavailableReason { get; set; }

    /// <summary>
    /// Gets or sets the memory in use in bytes, or <see langword="null"/> when a reason applies.
    /// </summary>
    public long? MemoryUsedBytes { get; set; }

    /// <summary>
    /// Gets or sets the total memory in bytes, or <see langword="null"/> when a reason applies.
    /// </summary>
    public long? MemoryTotalBytes { get; set; }

    /// <summary>
    /// Gets or sets why the memory reading is unavailable.
    /// </summary>
    [MaxLength(64)]
    public string? MemoryUnavailableReason { get; set; }

    /// <summary>
    /// Gets or sets the number of connected players, where 0 is a real empty server, or <see langword="null"/>
    /// when a reason applies.
    /// </summary>
    public int? PlayerCount { get; set; }

    /// <summary>
    /// Gets or sets the player capacity of the server, or <see langword="null"/> when unknown.
    /// </summary>
    public int? PlayerCapacity { get; set; }

    /// <summary>
    /// Gets or sets why the player count is unavailable.
    /// </summary>
    [MaxLength(64)]
    public string? PlayersUnavailableReason { get; set; }
}

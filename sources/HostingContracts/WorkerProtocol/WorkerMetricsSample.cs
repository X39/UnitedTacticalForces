using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// One CPU, memory and player count sample of a deployment's primary host, reported by its dedicated worker.
/// </summary>
/// <remarks>
/// Every reading is either a value or a reason code from <see cref="WorkerMetricUnavailableReasons"/>, never both
/// and never neither. Zero is a real measured value and must not be sent as a stand-in for an unavailable reading.
/// The worker reports its own sampling time for diagnostics only; the API buckets history by its own receipt time.
/// </remarks>
public sealed record WorkerMetricsSample : IValidatableObject
{
    /// <summary>
    /// Gets the deployment the worker belongs to. The API rejects a sample whose deployment is not the one of the
    /// authenticated worker.
    /// </summary>
    /// <value>The deployment identifier.</value>
    public Guid DeploymentId { get; init; }

    /// <summary>
    /// Gets the instant the worker took the sample.
    /// </summary>
    /// <value>The worker-side sampling time.</value>
    public DateTimeOffset SampledAt { get; init; }

    /// <summary>
    /// Gets the CPU utilization of the whole host in percent.
    /// </summary>
    /// <value>A value from 0 to 100, or <see langword="null"/> when <see cref="CpuUnavailableReason"/> is set.</value>
    [Range(0, 100)]
    public double? CpuPercent { get; init; }

    /// <summary>
    /// Gets why <see cref="CpuPercent"/> is unavailable.
    /// </summary>
    /// <value>One of <see cref="WorkerMetricUnavailableReasons.WorkerMachineReasons"/>, or <see langword="null"/>.</value>
    [MaxLength(64)]
    public string? CpuUnavailableReason { get; init; }

    /// <summary>
    /// Gets the memory in use on the host in bytes.
    /// </summary>
    /// <value>
    /// A non-negative byte count that accompanies <see cref="MemoryTotalBytes"/>, or <see langword="null"/> when
    /// <see cref="MemoryUnavailableReason"/> is set.
    /// </value>
    [Range(0, long.MaxValue)]
    public long? MemoryUsedBytes { get; init; }

    /// <summary>
    /// Gets the total memory of the host in bytes.
    /// </summary>
    /// <value>A positive byte count that accompanies <see cref="MemoryUsedBytes"/>, or <see langword="null"/>.</value>
    [Range(1, long.MaxValue)]
    public long? MemoryTotalBytes { get; init; }

    /// <summary>
    /// Gets why the memory reading is unavailable.
    /// </summary>
    /// <value>One of <see cref="WorkerMetricUnavailableReasons.WorkerMachineReasons"/>, or <see langword="null"/>.</value>
    [MaxLength(64)]
    public string? MemoryUnavailableReason { get; init; }

    /// <summary>
    /// Gets the number of players connected to the game server.
    /// </summary>
    /// <value>
    /// A count from 0 to 1024 where 0 is a real empty server, or <see langword="null"/> when
    /// <see cref="PlayersUnavailableReason"/> is set.
    /// </value>
    [Range(0, 1024)]
    public int? PlayerCount { get; init; }

    /// <summary>
    /// Gets the player capacity of the game server.
    /// </summary>
    /// <value>
    /// A positive slot count, or <see langword="null"/> when unknown. It may be present without a count only
    /// together with <see cref="PlayersUnavailableReason"/>.
    /// </value>
    [Range(1, 1024)]
    public int? PlayerCapacity { get; init; }

    /// <summary>
    /// Gets why <see cref="PlayerCount"/> is unavailable.
    /// </summary>
    /// <value>One of <see cref="WorkerMetricUnavailableReasons.WorkerPlayerReasons"/>, or <see langword="null"/>.</value>
    [MaxLength(64)]
    public string? PlayersUnavailableReason { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CpuPercent.HasValue == (CpuUnavailableReason is not null))
        {
            yield return new ValidationResult(
                "CPU must provide either a value or a reason.",
                [nameof(CpuPercent), nameof(CpuUnavailableReason)]);
        }
        else if (CpuUnavailableReason is not null
                 && !WorkerMetricUnavailableReasons.WorkerMachineReasons.Contains(CpuUnavailableReason))
        {
            yield return new ValidationResult(
                "CPU reason is not an accepted machine reason.",
                [nameof(CpuUnavailableReason)]);
        }

        if (MemoryUsedBytes.HasValue != MemoryTotalBytes.HasValue)
        {
            yield return new ValidationResult(
                "Memory must provide both used and total bytes or neither.",
                [nameof(MemoryUsedBytes), nameof(MemoryTotalBytes)]);
        }
        else if (MemoryUsedBytes.HasValue == (MemoryUnavailableReason is not null))
        {
            yield return new ValidationResult(
                "Memory must provide either values or a reason.",
                [nameof(MemoryUsedBytes), nameof(MemoryUnavailableReason)]);
        }
        else if (MemoryUnavailableReason is not null
                 && !WorkerMetricUnavailableReasons.WorkerMachineReasons.Contains(MemoryUnavailableReason))
        {
            yield return new ValidationResult(
                "Memory reason is not an accepted machine reason.",
                [nameof(MemoryUnavailableReason)]);
        }

        if (MemoryUsedBytes > MemoryTotalBytes)
        {
            yield return new ValidationResult(
                "Memory used bytes cannot exceed total bytes.",
                [nameof(MemoryUsedBytes)]);
        }

        if (PlayerCount.HasValue == (PlayersUnavailableReason is not null))
        {
            yield return new ValidationResult(
                "Players must provide either a count or a reason.",
                [nameof(PlayerCount), nameof(PlayersUnavailableReason)]);
        }
        else if (PlayersUnavailableReason is not null
                 && !WorkerMetricUnavailableReasons.WorkerPlayerReasons.Contains(PlayersUnavailableReason))
        {
            yield return new ValidationResult(
                "Players reason is not an accepted player reason.",
                [nameof(PlayersUnavailableReason)]);
        }

        if (PlayerCount > PlayerCapacity)
        {
            yield return new ValidationResult(
                "Player count cannot exceed player capacity.",
                [nameof(PlayerCount)]);
        }
    }
}

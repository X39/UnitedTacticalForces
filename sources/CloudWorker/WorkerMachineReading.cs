using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// One reading of the machine the game runs on, bounded by the worker container's memory limit. Every figure is
/// either a measured value or exactly one reason from <see cref="WorkerMetricUnavailableReasons.WorkerMachineReasons" />;
/// an unmeasurable value is never replaced by zero.
/// </summary>
/// <param name="CpuPercent">The CPU utilisation from 0 to 100, or <see langword="null" /> when unavailable.</param>
/// <param name="CpuUnavailableReason">The reason the CPU figure is unavailable, otherwise <see langword="null" />.</param>
/// <param name="MemoryUsedBytes">The used memory in bytes, or <see langword="null" /> when unavailable.</param>
/// <param name="MemoryTotalBytes">The memory limit in bytes, or <see langword="null" /> when unavailable.</param>
/// <param name="MemoryUnavailableReason">The reason the memory figures are unavailable, otherwise <see langword="null" />.</param>
public sealed record WorkerMachineReading(
    double? CpuPercent,
    string? CpuUnavailableReason,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    string? MemoryUnavailableReason);

using System.Globalization;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Samples CPU from <c>/proc/stat</c> deltas and memory from cgroup v2 with a <c>/proc/meminfo</c> fallback.
/// </summary>
/// <remarks>
/// The readings describe the machine the game runs on, bounded by the worker container's memory limit. Memory is the
/// cgroup working set (<c>memory.current</c> minus <c>inactive_file</c>) of <c>memory.max</c>. A value that cannot be
/// measured is null with a reason (measuring, query-failed or unsupported-platform), never zero. The sampler is
/// thread-safe; the first CPU call has no previous counters and therefore reports measuring.
/// </remarks>
/// <param name="readFile">
/// Reads a file and returns its text, or null when it is unreadable. Defaults to a reader that maps
/// <see cref="IOException" /> and <see cref="UnauthorizedAccessException" /> to null.
/// </param>
/// <param name="isLinux">Tells whether the host is Linux. Defaults to <see cref="OperatingSystem.IsLinux" />.</param>
public sealed class WorkerMetricsSampler(Func<string, string?>? readFile = null, Func<bool>? isLinux = null)
    : IWorkerMetricsSampler
{
    private const string ProcStatPath = "/proc/stat";
    private const string MeminfoPath = "/proc/meminfo";
    private const string CgroupCurrentPath = "/sys/fs/cgroup/memory.current";
    private const string CgroupStatPath = "/sys/fs/cgroup/memory.stat";
    private const string CgroupMaxPath = "/sys/fs/cgroup/memory.max";

    private readonly Func<string, string?> _readFile = readFile ?? ReadFileOrNull;
    private readonly Func<bool> _isLinux = isLinux ?? OperatingSystem.IsLinux;
    private readonly object _lock = new();
    private (long Total, long Idle)? _previousCpu;

    /// <inheritdoc />
    public WorkerMachineReading Sample()
    {
        if (!_isLinux())
        {
            return new WorkerMachineReading(
                null,
                WorkerMetricUnavailableReasons.UnsupportedPlatform,
                null,
                null,
                WorkerMetricUnavailableReasons.UnsupportedPlatform);
        }
        var (cpu, cpuReason) = SampleCpu();
        var (used, total, memoryReason) = SampleMemory();
        return new WorkerMachineReading(cpu, cpuReason, used, total, memoryReason);
    }

    private (double? Percent, string? Reason) SampleCpu()
    {
        if (!TryReadCpuCounters(out var current))
            return (null, WorkerMetricUnavailableReasons.QueryFailed);
        lock (_lock)
        {
            var previous = _previousCpu;
            _previousCpu = current;
            if (previous is null) return (null, WorkerMetricUnavailableReasons.Measuring);
            var totalDelta = current.Total - previous.Value.Total;
            var idleDelta = current.Idle - previous.Value.Idle;
            if (totalDelta <= 0 || idleDelta < 0) return (null, WorkerMetricUnavailableReasons.Measuring);
            var percent = 100.0 * (1.0 - (double)idleDelta / totalDelta);
            return (Math.Round(Math.Clamp(percent, 0.0, 100.0), 1), null);
        }
    }

    private bool TryReadCpuCounters(out (long Total, long Idle) counters)
    {
        counters = default;
        var content = _readFile(ProcStatPath);
        if (string.IsNullOrEmpty(content)) return false;
        foreach (var line in content.Split('\n'))
        {
            if (!line.StartsWith("cpu ", StringComparison.Ordinal)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // parts[0] is the label; user nice system idle iowait irq softirq steal follow, guest is already in user.
            var columns = Math.Min(parts.Length - 1, 8);
            if (columns < 4) return false;
            long total = 0;
            for (var index = 1; index <= columns; index++)
            {
                if (!long.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    return false;
                total += value;
            }
            _ = long.TryParse(
                columns >= 5 ? parts[5] : "0", NumberStyles.None, CultureInfo.InvariantCulture, out var iowait);
            _ = long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var idle);
            counters = (total, idle + iowait);
            return true;
        }
        return false;
    }

    private (long? Used, long? Total, string? Reason) SampleMemory()
    {
        if (TryReadCgroupMemory(out var used, out var total)
            || TryReadMeminfoMemory(out used, out total))
            return (used, total, null);
        return (null, null, WorkerMetricUnavailableReasons.QueryFailed);
    }

    private bool TryReadCgroupMemory(out long used, out long total)
    {
        used = total = 0;
        if (!TryParseSingleNumber(_readFile(CgroupCurrentPath), out var current)) return false;
        var limitText = _readFile(CgroupMaxPath)?.Trim();
        if (string.IsNullOrEmpty(limitText)) return false;
        if (limitText == "max")
        {
            if (!TryReadMemTotal(out total)) return false;
        }
        else if (!long.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out total) || total <= 0)
        {
            return false;
        }
        var inactiveFile = ReadStatValue(_readFile(CgroupStatPath), "inactive_file");
        used = Math.Clamp(current - inactiveFile, 0, total);
        return true;
    }

    private bool TryReadMeminfoMemory(out long used, out long total)
    {
        used = 0;
        var content = _readFile(MeminfoPath);
        if (!TryReadKilobytes(content, "MemTotal:", out var totalKb)
            || !TryReadKilobytes(content, "MemAvailable:", out var availableKb))
        {
            total = 0;
            return false;
        }
        total = totalKb * 1024;
        used = Math.Clamp(total - availableKb * 1024, 0, total);
        return total > 0;
    }

    private bool TryReadMemTotal(out long total)
    {
        total = 0;
        if (!TryReadKilobytes(_readFile(MeminfoPath), "MemTotal:", out var kilobytes)) return false;
        total = kilobytes * 1024;
        return total > 0;
    }

    private static bool TryReadKilobytes(string? content, string key, out long kilobytes)
    {
        kilobytes = 0;
        if (string.IsNullOrEmpty(content)) return false;
        foreach (var line in content.Split('\n'))
        {
            if (!line.StartsWith(key, StringComparison.Ordinal)) continue;
            var parts = line[key.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0
                   && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out kilobytes);
        }
        return false;
    }

    private static long ReadStatValue(string? content, string key)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        foreach (var line in content.Split('\n'))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == key
                                  && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return 0;
    }

    private static bool TryParseSingleNumber(string? content, out long value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(content)
               && long.TryParse(content.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string? ReadFileOrNull(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

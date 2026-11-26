using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Verifies machine CPU and memory sampling from /proc and cgroup v2 fixtures.</summary>
public sealed class WorkerMetricsSamplerTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private const long Mib = 1024L * 1024;
    private const string ProcStat = "/proc/stat";
    private const string ProcMeminfo = "/proc/meminfo";
    private const string CgroupCurrent = "/sys/fs/cgroup/memory.current";
    private const string CgroupStat = "/sys/fs/cgroup/memory.stat";
    private const string CgroupMax = "/sys/fs/cgroup/memory.max";

    /// <summary>Verifies that the first CPU sample is measuring, never zero.</summary>
    [Fact]
    public void Sample_FirstCpuSample_ReportsMeasuring()
    {
        var files = new Dictionary<string, string> { [ProcStat] = "cpu  100 0 100 700 100 0 0 0 0 0\n" };
        var sampler = Linux(files);

        var reading = sampler.Sample();

        Assert.Null(reading.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, reading.CpuUnavailableReason);
    }

    /// <summary>Verifies the idle-including-iowait CPU formula over two samples.</summary>
    [Fact]
    public void Sample_SecondCpuSample_ReportsPercentFromDeltas()
    {
        var files = new Dictionary<string, string> { [ProcStat] = "cpu  100 0 100 700 100 0 0 0 0 0\ncpu0 1 1 1 1\n" };
        var sampler = Linux(files);
        sampler.Sample();
        files[ProcStat] = "cpu  200 0 200 780 120 0 0 0 0 0\ncpu0 9 9 9 9\n";

        var reading = sampler.Sample();

        Assert.Equal(66.7, reading.CpuPercent);
        Assert.Null(reading.CpuUnavailableReason);
    }

    /// <summary>Verifies that a fully busy interval is clamped to 100 percent.</summary>
    [Fact]
    public void Sample_FullyBusyInterval_ReportsOneHundredPercent()
    {
        var files = new Dictionary<string, string> { [ProcStat] = "cpu  100 0 100 700 100 0 0 0\n" };
        var sampler = Linux(files);
        sampler.Sample();
        files[ProcStat] = "cpu  400 0 100 700 100 0 0 0\n";

        Assert.Equal(100.0, sampler.Sample().CpuPercent);
    }

    /// <summary>Verifies that unchanged or regressing counters report measuring, not zero.</summary>
    [Fact]
    public void Sample_UnchangedOrRegressedCounters_ReportMeasuring()
    {
        var files = new Dictionary<string, string> { [ProcStat] = "cpu  100 0 100 700 100 0 0 0\n" };
        var sampler = Linux(files);
        sampler.Sample();

        var unchanged = sampler.Sample();
        files[ProcStat] = "cpu  10 0 10 70 10 0 0 0\n";
        var regressed = sampler.Sample();

        Assert.Null(unchanged.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, unchanged.CpuUnavailableReason);
        Assert.Null(regressed.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, regressed.CpuUnavailableReason);
    }

    /// <summary>Verifies that an unreadable or malformed /proc/stat reports query-failed.</summary>
    /// <param name="content">The /proc/stat content, or null for an unreadable file.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage without a cpu line\n")]
    [InlineData("cpu  1 2\n")]
    [InlineData("cpu  a b c d e\n")]
    public void Sample_UnreadableOrMalformedProcStat_ReportsQueryFailed(string? content)
    {
        var files = new Dictionary<string, string>();
        if (content is not null) files[ProcStat] = content;

        var reading = Linux(files).Sample();

        Assert.Null(reading.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, reading.CpuUnavailableReason);
    }

    /// <summary>Verifies the cgroup v2 memory formula subtracts the inactive file cache.</summary>
    [Fact]
    public void Sample_CgroupV2_ReportsWorkingSetOfLimit()
    {
        var files = new Dictionary<string, string>
        {
            [CgroupCurrent] = (3 * Gib).ToString() + "\n",
            [CgroupStat] = $"anon 1\ninactive_file {512 * Mib}\nactive_file 5\n",
            [CgroupMax] = (7 * Gib).ToString() + "\n",
        };

        var reading = Linux(files).Sample();

        Assert.Equal(3 * Gib - 512 * Mib, reading.MemoryUsedBytes);
        Assert.Equal(7 * Gib, reading.MemoryTotalBytes);
        Assert.Null(reading.MemoryUnavailableReason);
    }

    /// <summary>Verifies that an unlimited cgroup uses the host total from /proc/meminfo.</summary>
    [Fact]
    public void Sample_CgroupWithoutLimit_UsesMemTotalAsTotal()
    {
        var files = new Dictionary<string, string>
        {
            [CgroupCurrent] = Gib.ToString(),
            [CgroupStat] = "inactive_file 0\n",
            [CgroupMax] = "max\n",
            [ProcMeminfo] = "MemTotal:       8388608 kB\nMemAvailable:   1000 kB\n",
        };

        var reading = Linux(files).Sample();

        Assert.Equal(Gib, reading.MemoryUsedBytes);
        Assert.Equal(8 * Gib, reading.MemoryTotalBytes);
    }

    /// <summary>Verifies that used memory is never negative and never above the total.</summary>
    [Fact]
    public void Sample_InactiveFileAboveCurrent_ClampsUsedToZero()
    {
        var files = new Dictionary<string, string>
        {
            [CgroupCurrent] = Mib.ToString(),
            [CgroupStat] = $"inactive_file {2 * Mib}\n",
            [CgroupMax] = Gib.ToString(),
        };

        Assert.Equal(0, Linux(files).Sample().MemoryUsedBytes);
    }

    /// <summary>Verifies the /proc/meminfo fallback when no cgroup files are readable.</summary>
    [Fact]
    public void Sample_WithoutCgroupFiles_FallsBackToMeminfo()
    {
        var files = new Dictionary<string, string>
        {
            [ProcMeminfo] = "MemTotal:       4194304 kB\nMemFree: 1 kB\nMemAvailable:   1048576 kB\n",
        };

        var reading = Linux(files).Sample();

        Assert.Equal(3 * Gib, reading.MemoryUsedBytes);
        Assert.Equal(4 * Gib, reading.MemoryTotalBytes);
        Assert.Null(reading.MemoryUnavailableReason);
    }

    /// <summary>Verifies that nothing readable reports query-failed instead of zero.</summary>
    [Fact]
    public void Sample_NothingReadable_ReportsQueryFailedForBoth()
    {
        var reading = Linux(new Dictionary<string, string>()).Sample();

        Assert.Null(reading.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, reading.CpuUnavailableReason);
        Assert.Null(reading.MemoryUsedBytes);
        Assert.Null(reading.MemoryTotalBytes);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, reading.MemoryUnavailableReason);
    }

    /// <summary>Verifies that a non-Linux host reports unsupported-platform and reads no file.</summary>
    [Fact]
    public void Sample_NonLinuxHost_ReportsUnsupportedPlatformWithoutReading()
    {
        var reads = 0;
        var sampler = new WorkerMetricsSampler(_ =>
        {
            reads++;
            return "x";
        }, () => false);

        var reading = sampler.Sample();

        Assert.Equal(0, reads);
        Assert.Null(reading.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.UnsupportedPlatform, reading.CpuUnavailableReason);
        Assert.Null(reading.MemoryUsedBytes);
        Assert.Equal(WorkerMetricUnavailableReasons.UnsupportedPlatform, reading.MemoryUnavailableReason);
    }

    /// <summary>Verifies that the default sampler always returns a value or a reason, never a silent zero.</summary>
    [Fact]
    public void Sample_DefaultSampler_ReturnsValueOrReasonForEachReading()
    {
        var reading = new WorkerMetricsSampler().Sample();

        Assert.True(reading.CpuPercent is not null ^ reading.CpuUnavailableReason is not null);
        Assert.True(reading.MemoryUsedBytes is not null ^ reading.MemoryUnavailableReason is not null);
    }

    private static WorkerMetricsSampler Linux(Dictionary<string, string> files)
        => new(path => files.GetValueOrDefault(path), () => true);
}

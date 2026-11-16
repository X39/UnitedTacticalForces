using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Checks raw allocation boundaries independently from content preparation headroom.</summary>
public sealed class RuntimeStorageCapacityPolicyTests
{
    /// <summary>The byte count used by provider GiB allocations.</summary>
    private const long GiB = RuntimeStoragePlanResolver.GiB;

    /// <summary>Requires extra raw storage even when the content peak is an exact allocation multiple.</summary>
    [Fact]
    public void AutomaticCapacity_AddsFilesystemReserveWithoutChangingPeak()
    {
        var plan = Resolve(ERuntimeStorageStrategy.AutomaticExternal, null, 17 * GiB);
        Assert.Equal(20 * GiB, plan.SelectedCapacityBytes);
        Assert.Equal(17 * GiB, plan.RequiredCapacityBytes);
        Assert.Equal(17 * GiB, plan.EstimatedUsableCapacityBytes);
        Assert.Equal(1, plan.CapacityPolicyVersion);
        Assert.Equal(0.02m, plan.EstimatedHourlyCostEur);
    }

    /// <summary>Checks equality and either adjacent byte at a fixed volume's usable capacity.</summary>
    /// <param name="offset">The difference from the estimated usable capacity.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void FixedCapacity_UsesUsableBoundary(long offset)
    {
        var peak = 17 * GiB + offset;
        if (offset > 0)
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(ERuntimeStorageStrategy.FixedExternal, 20, peak));
            return;
        }
        Assert.Equal(20 * GiB, Resolve(ERuntimeStorageStrategy.FixedExternal, 20, peak).SelectedCapacityBytes);
    }

    /// <summary>Checks that the next raw increment is necessary only after the usable boundary is exceeded.</summary>
    /// <param name="offset">The difference from twenty GiB's usable capacity.</param>
    /// <param name="expectedGiB">The smallest sufficient provider allocation.</param>
    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 20)]
    [InlineData(1, 21)]
    public void AutomaticCapacity_SelectsSmallestSufficientSize(long offset, long expectedGiB)
        => Assert.Equal(expectedGiB * GiB,
            Resolve(ERuntimeStorageStrategy.AutomaticExternal, null, 17 * GiB + offset).SelectedCapacityBytes);

    /// <summary>Honors the smallest valid increment even when the provider minimum is not aligned.</summary>
    [Fact]
    public void AutomaticCapacity_AlignsMinimumAndHonorsMaximum()
    {
        var constraints = new RuntimeStorageCapabilities(true, 11, 20, 5);
        Assert.Equal(15, RuntimeStoragePlanResolver.GetMinimumExternalCapacityGiB(0, constraints));
        Assert.Equal(20, RuntimeStoragePlanResolver.GetMinimumExternalCapacityGiB(17 * GiB, constraints));
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeStoragePlanResolver.GetMinimumExternalCapacityGiB(17 * GiB + 1, constraints));
    }

    /// <summary>Ensures temporary installation workspace cannot be mistaken for filesystem overhead.</summary>
    [Fact]
    public void FixedCapacity_RejectsPeakEvenWhenSteadyContentFits()
    {
        var (workspace, margin, peak, overflowed) =
            X39.UnitedTacticalForces.Api.Services.StorageDemand.StorageDemandService.CalculatePeakPolicy(
                8 * GiB, 8 * GiB, 1m, GiB, 20m);
        Assert.False(overflowed);
        Assert.Equal(9 * GiB, workspace);
        Assert.True(margin > 0);
        Assert.Throws<InvalidOperationException>(() => Resolve(ERuntimeStorageStrategy.FixedExternal, 20, peak));
        Assert.True(Resolve(ERuntimeStorageStrategy.AutomaticExternal, null, peak).EstimatedUsableCapacityBytes >= peak);
    }

    /// <summary>Verifies the inverse formula at byte precision rather than relying on floating-point rounding.</summary>
    /// <param name="peak">The required usable byte count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(18253611009)]
    [InlineData(8000000000000000000)]
    public void MinimumRawCapacity_IsExact(long peak)
    {
        var raw = RuntimeStorageCapacityPolicy.GetMinimumRawCapacityBytes(peak);
        Assert.True(RuntimeStorageCapacityPolicy.EstimateUsableBytes(raw) >= peak);
        if (raw > 0)
            Assert.True(RuntimeStorageCapacityPolicy.EstimateUsableBytes(raw - 1) < peak);
    }

    /// <summary>Rejects impossible demand, oversized raw allocation, and invalid negative input without wrapping.</summary>
    [Fact]
    public void CapacityArithmetic_FailsClosedAtNumericLimits()
    {
        Assert.Throws<InvalidOperationException>(() => Resolve(ERuntimeStorageStrategy.AutomaticExternal, null, long.MaxValue));
        Assert.Throws<InvalidOperationException>(() => Resolve(ERuntimeStorageStrategy.FixedExternal, long.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeStorageCapacityPolicy.EstimateUsableBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeStorageCapacityPolicy.GetMinimumRawCapacityBytes(-1));
        Assert.InRange(RuntimeStorageCapacityPolicy.EstimateUsableBytes(long.MaxValue), 0, long.MaxValue);
    }

    /// <summary>Resolves a priced volume with realistic raw provider constraints.</summary>
    /// <param name="strategy">The requested allocation mode.</param>
    /// <param name="fixedGiB">The requested fixed raw size.</param>
    /// <param name="peak">The peak preparation demand.</param>
    /// <returns>The capacity plan under test.</returns>
    private static RuntimeStoragePlan Resolve(ERuntimeStorageStrategy strategy, long? fixedGiB, long peak)
        => RuntimeStoragePlanResolver.Resolve(strategy, fixedGiB, peak, 5 * GiB,
            new RuntimeProfile("test", "Test", "Test profile", DiskGiB: 40),
            new RuntimeStorageCapabilities(true, 10, 100, 1, 0.001m));
}

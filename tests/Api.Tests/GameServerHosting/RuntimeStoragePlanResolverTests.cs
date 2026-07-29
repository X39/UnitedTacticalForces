using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class RuntimeStoragePlanResolverTests
{
    private const long GiB = RuntimeStoragePlanResolver.GiB;

    [Fact]
    public void AutomaticExternal_RoundsPeakRequirementToProviderIncrement()
    {
        var plan = RuntimeStoragePlanResolver.Resolve(
            ERuntimeStorageStrategy.AutomaticExternal,
            null,
            17 * GiB + 1,
            8 * GiB,
            Profile(40),
            Capabilities(minimumGiB: 10, maximumGiB: 100, incrementGiB: 5));

        Assert.Equal(20 * GiB, plan.SelectedCapacityBytes);
        Assert.Equal(0.02m, plan.EstimatedHourlyCostEur);
        Assert.True(plan.UsesExternalStorage);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(105)]
    [InlineData(12)]
    public void FixedExternal_RejectsProviderMinimumMaximumAndIncrement(long selectedGiB)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeStoragePlanResolver.Resolve(
            ERuntimeStorageStrategy.FixedExternal,
            selectedGiB,
            GiB,
            8 * GiB,
            Profile(40),
            Capabilities(minimumGiB: 10, maximumGiB: 100, incrementGiB: 5)));

        Assert.Contains("capacity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IncludedDisk_UsesOneFilesystemAndRejectsInsufficientUsableCapacity()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeStoragePlanResolver.Resolve(
            ERuntimeStorageStrategy.IncludedDisk,
            null,
            33 * GiB,
            8 * GiB,
            Profile(40),
            Capabilities()));

        Assert.Contains("included", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalStorage_StillRequiresEnoughRootDiskForBootstrap()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeStoragePlanResolver.Resolve(
            ERuntimeStorageStrategy.AutomaticExternal,
            null,
            20 * GiB,
            8 * GiB,
            Profile(4),
            Capabilities()));

        Assert.Contains("root disk", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeProfile Profile(int diskGiB)
        => new("test", "Test", "Test profile", 4, 8, diskGiB, 0.1m, 73m);

    private static RuntimeStorageCapabilities Capabilities(
        long minimumGiB = 10,
        long maximumGiB = 100,
        long incrementGiB = 1)
        => new(true, minimumGiB, maximumGiB, incrementGiB, 0.001m, "/srv/utf/deployment");
}

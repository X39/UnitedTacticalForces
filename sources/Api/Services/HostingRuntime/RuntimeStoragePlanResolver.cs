using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal static class RuntimeStoragePlanResolver
{
    internal const long GiB = 1_073_741_824L;

    public static RuntimeStoragePlan Resolve(
        ERuntimeStorageStrategy strategy,
        long? fixedCapacityGiB,
        long requiredCapacityBytes,
        long rootDiskReserveBytes,
        RuntimeProfile profile,
        RuntimeStorageCapabilities capabilities)
    {
        if (requiredCapacityBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredCapacityBytes));
        var includedBytes = profile.DiskGiB is { } diskGiB
            ? checked((long)Math.Max(0, diskGiB) * GiB)
            : throw new InvalidOperationException("The selected compute profile does not report included disk capacity.");
        if (includedBytes < rootDiskReserveBytes)
            throw new InvalidOperationException("The compute profile does not leave enough root disk for OS and bootstrap operation.");

        if (strategy is ERuntimeStorageStrategy.IncludedDisk)
        {
            var usable = includedBytes - Math.Max(0, rootDiskReserveBytes);
            if (usable < requiredCapacityBytes)
                throw new InvalidOperationException("The included server disk is insufficient for the peak deployment requirement.");
            return new RuntimeStoragePlan(strategy, requiredCapacityBytes, usable, "/srv/united-tactical-forces/runtime", 0m);
        }

        if (!capabilities.SupportsExternalBlockStorage || capabilities.ReadinessFailure is not null)
            throw new InvalidOperationException(capabilities.ReadinessFailure ?? "The provider does not support external block storage.");
        if (capabilities.MinimumCapacityGiB <= 0
            || capabilities.MaximumCapacityGiB < capabilities.MinimumCapacityGiB
            || capabilities.CapacityIncrementGiB <= 0)
        {
            throw new InvalidOperationException("The provider's external-storage capacity constraints are invalid.");
        }

        var requiredGiB = DivideRoundUp(requiredCapacityBytes, GiB);
        var selectedGiB = strategy switch
        {
            ERuntimeStorageStrategy.AutomaticExternal => RoundUp(
                Math.Max(requiredGiB, capabilities.MinimumCapacityGiB),
                capabilities.CapacityIncrementGiB),
            ERuntimeStorageStrategy.FixedExternal when fixedCapacityGiB.HasValue => fixedCapacityGiB.Value,
            ERuntimeStorageStrategy.FixedExternal => throw new InvalidOperationException(
                "A fixed external-storage capacity is required."),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };
        if (selectedGiB < capabilities.MinimumCapacityGiB || selectedGiB > capabilities.MaximumCapacityGiB)
            throw new InvalidOperationException("The external-storage capacity is outside the provider-supported range.");
        if (selectedGiB % capabilities.CapacityIncrementGiB != 0)
            throw new InvalidOperationException("The external-storage capacity does not match the provider increment.");
        var selectedBytes = checked(selectedGiB * GiB);
        if (selectedBytes < requiredCapacityBytes)
            throw new InvalidOperationException("The selected external-storage capacity is below the peak deployment requirement.");

        return new RuntimeStoragePlan(
            strategy,
            requiredCapacityBytes,
            selectedBytes,
            capabilities.DeploymentMountPath,
            selectedGiB * capabilities.EstimatedHourlyCostPerGiBEur);
    }

    private static long DivideRoundUp(long value, long divisor)
        => value == 0 ? 0 : value / divisor + (value % divisor == 0 ? 0 : 1);

    private static long RoundUp(long value, long increment)
        => checked(DivideRoundUp(value, increment) * increment);
}

using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Resolves peak demand to a priced allocation before provider mutation.</summary>
internal static class RuntimeStoragePlanResolver
{
    /// <summary>The byte count in one provider allocation unit.</summary>
    internal const long GiB = 1_073_741_824L;

    /// <summary>Selects storage satisfying the preparation peak and provider constraints.</summary>
    /// <param name="strategy">The requested allocation strategy.</param>
    /// <param name="fixedCapacityGiB">The raw external size when fixed sizing is selected.</param>
    /// <param name="requiredCapacityBytes">Peak content and preparation demand.</param>
    /// <param name="rootDiskReserveBytes">Capacity reserved for the operating system and bootstrap.</param>
    /// <param name="profile">The selected compute allocation.</param>
    /// <param name="capabilities">The provider's external storage constraints and rates.</param>
    /// <returns>A frozen storage allocation including external filesystem capacity evidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Demand, reserve, or strategy is invalid.</exception>
    /// <exception cref="InvalidOperationException">The allocation cannot satisfy demand or provider constraints.</exception>
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
        ArgumentOutOfRangeException.ThrowIfNegative(rootDiskReserveBytes);
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

        ValidateCapabilities(capabilities);
        var selectedGiB = strategy switch
        {
            ERuntimeStorageStrategy.AutomaticExternal => GetMinimumExternalCapacityGiB(requiredCapacityBytes, capabilities),
            ERuntimeStorageStrategy.FixedExternal when fixedCapacityGiB.HasValue => fixedCapacityGiB.Value,
            ERuntimeStorageStrategy.FixedExternal => throw new InvalidOperationException(
                "A fixed external-storage capacity is required."),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };
        if (selectedGiB < capabilities.MinimumCapacityGiB || selectedGiB > capabilities.MaximumCapacityGiB)
            throw new InvalidOperationException("The external-storage capacity is outside the provider-supported range.");
        if (selectedGiB % capabilities.CapacityIncrementGiB != 0)
            throw new InvalidOperationException("The external-storage capacity does not match the provider increment.");
        if (selectedGiB > long.MaxValue / GiB)
            throw new InvalidOperationException("The external-storage capacity exceeds the supported byte range.");
        var selectedBytes = selectedGiB * GiB;
        var usableBytes = RuntimeStorageCapacityPolicy.EstimateUsableBytes(selectedBytes);
        if (usableBytes < requiredCapacityBytes)
            throw new InvalidOperationException(
                "The selected external-storage capacity cannot provide enough usable filesystem space "
                + "for the peak deployment requirement.");

        return new RuntimeStoragePlan(
            strategy,
            requiredCapacityBytes,
            selectedBytes,
            capabilities.DeploymentMountPath,
            CalculateHourlyCost(selectedGiB, capabilities.EstimatedHourlyCostPerGiBEur))
        {
            CapacityPolicyVersion = RuntimeStorageCapacityPolicy.CurrentVersion,
            EstimatedUsableCapacityBytes = usableBytes,
        };
    }

    /// <summary>Finds the smallest provider-supported raw allocation whose usable forecast meets peak demand.</summary>
    /// <param name="requiredCapacityBytes">Peak preparation demand in bytes.</param>
    /// <param name="capabilities">External capacity limits and increments.</param>
    /// <returns>The minimum sufficient raw allocation in GiB.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Demand is negative.</exception>
    /// <exception cref="InvalidOperationException">No supported allocation can meet demand.</exception>
    internal static long GetMinimumExternalCapacityGiB(long requiredCapacityBytes, RuntimeStorageCapabilities capabilities)
    {
        ValidateCapabilities(capabilities);
        var rawBytes = RuntimeStorageCapacityPolicy.GetMinimumRawCapacityBytes(requiredCapacityBytes);
        var minimum = Math.Max(DivideRoundUp(rawBytes, GiB), capabilities.MinimumCapacityGiB);
        var increments = DivideRoundUp(minimum, capabilities.CapacityIncrementGiB);
        var maximum = Math.Min(capabilities.MaximumCapacityGiB, long.MaxValue / GiB);
        if (increments > maximum / capabilities.CapacityIncrementGiB)
            throw new InvalidOperationException("The peak deployment requirement exceeds provider-supported storage capacity.");
        return increments * capabilities.CapacityIncrementGiB;
    }

    /// <summary>Rejects unavailable storage or invalid provider capacity and price metadata.</summary>
    /// <param name="capabilities">The advertised storage capabilities.</param>
    /// <exception cref="InvalidOperationException">Storage is unavailable or constraints are invalid.</exception>
    private static void ValidateCapabilities(RuntimeStorageCapabilities capabilities)
    {
        if (!capabilities.SupportsExternalBlockStorage || capabilities.ReadinessFailure is not null)
            throw new InvalidOperationException(capabilities.ReadinessFailure ?? "The provider does not support external block storage.");
        if (capabilities.MinimumCapacityGiB <= 0
            || capabilities.MaximumCapacityGiB < capabilities.MinimumCapacityGiB
            || capabilities.CapacityIncrementGiB <= 0
            || capabilities.EstimatedHourlyCostPerGiBEur < 0)
        {
            throw new InvalidOperationException("The provider's external-storage capacity constraints are invalid.");
        }
    }

    /// <summary>Prices the actual raw allocation without allowing decimal overflow to escape preflight.</summary>
    /// <param name="capacityGiB">The selected raw allocation.</param>
    /// <param name="hourlyRate">The rate per GiB.</param>
    /// <returns>The hourly charge in EUR.</returns>
    /// <exception cref="InvalidOperationException">The charge cannot be represented.</exception>
    private static decimal CalculateHourlyCost(long capacityGiB, decimal hourlyRate)
    {
        try
        {
            return checked(capacityGiB * hourlyRate);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException("The external-storage price exceeds the supported range.", ex);
        }
    }

    /// <summary>Rounds nonnegative integral quantities upward without overflowing their sum.</summary>
    /// <param name="value">The quantity to divide.</param>
    /// <param name="divisor">The positive unit size.</param>
    /// <returns>The ceiling of the quotient.</returns>
    private static long DivideRoundUp(long value, long divisor)
        => value == 0 ? 0 : value / divisor + (value % divisor == 0 ? 0 : 1);
}

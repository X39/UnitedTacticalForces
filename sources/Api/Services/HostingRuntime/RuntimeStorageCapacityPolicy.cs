namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Estimates external filesystem capacity separately from peak content and preparation demand.</summary>
internal static class RuntimeStorageCapacityPolicy
{
    /// <summary>The immutable policy reserving ten percent of raw capacity plus one GiB for filesystem overhead.</summary>
    internal const int CurrentVersion = 1;

    /// <summary>Estimates writable capacity after filesystem metadata, reserved blocks, and fixed overhead.</summary>
    /// <param name="rawCapacityBytes">The provisioned block device size in bytes.</param>
    /// <returns>A conservative forecast; actual mounted availability must still be checked before installation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The raw capacity is negative.</exception>
    internal static long EstimateUsableBytes(long rawCapacityBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rawCapacityBytes);
        var percentageReserve = rawCapacityBytes / 10 + (rawCapacityBytes % 10 == 0 ? 0 : 1);
        return Math.Max(0, rawCapacityBytes - percentageReserve - RuntimeStoragePlanResolver.GiB);
    }

    /// <summary>Inverts the usable capacity forecast without intermediate integer overflow.</summary>
    /// <param name="requiredCapacityBytes">Peak content, workspace, and content safety margin in bytes.</param>
    /// <returns>The smallest raw byte count meeting the demand, before provider increments are applied.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The required capacity is negative.</exception>
    /// <exception cref="InvalidOperationException">No representable raw allocation satisfies the demand.</exception>
    internal static long GetMinimumRawCapacityBytes(long requiredCapacityBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredCapacityBytes);
        if (requiredCapacityBytes == 0)
            return 0;
        var raw = decimal.Ceiling((requiredCapacityBytes + (decimal)RuntimeStoragePlanResolver.GiB) * 10m / 9m);
        if (raw > long.MaxValue)
            throw new InvalidOperationException("The peak deployment requirement exceeds supported storage capacity.");
        return (long)raw;
    }
}

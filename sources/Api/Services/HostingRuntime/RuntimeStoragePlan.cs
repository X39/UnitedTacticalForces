using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Resolved deployment storage intent. Included and external capacity are deliberately not additive: the deployment
/// root resides wholly on the selected filesystem.
/// </summary>
/// <param name="Strategy">The filesystem allocation strategy.</param>
/// <param name="RequiredCapacityBytes">Peak content and preparation demand, without filesystem overhead.</param>
/// <param name="SelectedCapacityBytes">Raw external allocation, or usable included-disk capacity after system reserve.</param>
/// <param name="DeploymentRoot">The filesystem root containing deployment content.</param>
/// <param name="EstimatedHourlyCostEur">The frozen hourly charge for the selected external allocation.</param>
public sealed record RuntimeStoragePlan(
    ERuntimeStorageStrategy Strategy,
    long RequiredCapacityBytes,
    long SelectedCapacityBytes,
    string DeploymentRoot,
    decimal EstimatedHourlyCostEur)
{
    /// <summary>Gets whether deployment content resides on a separately provisioned block device.</summary>
    /// <value>True for automatic or fixed external storage.</value>
    public bool UsesExternalStorage => Strategy is not ERuntimeStorageStrategy.IncludedDisk;

    /// <summary>Gets the immutable capacity forecast policy used when the allocation was accepted.</summary>
    /// <value>The policy version, or null for included disks and historical snapshots without a forecast.</value>
    public int? CapacityPolicyVersion { get; init; }

    /// <summary>Gets the forecast writable capacity frozen before provider mutation.</summary>
    /// <value>Estimated usable bytes, or null when a historical snapshot has no capacity evidence.</value>
    public long? EstimatedUsableCapacityBytes { get; init; }
}

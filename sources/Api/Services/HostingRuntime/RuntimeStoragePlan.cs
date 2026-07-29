using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Resolved deployment storage intent. Included and external capacity are deliberately not additive: the deployment
/// root resides wholly on the selected filesystem.
/// </summary>
public sealed record RuntimeStoragePlan(
    ERuntimeStorageStrategy Strategy,
    long RequiredCapacityBytes,
    long SelectedCapacityBytes,
    string DeploymentRoot,
    decimal EstimatedHourlyCostEur)
{
    public bool UsesExternalStorage => Strategy is not ERuntimeStorageStrategy.IncludedDisk;
}

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Provider-neutral constraints for ephemeral external block storage.
/// </summary>
public sealed record RuntimeStorageCapabilities(
    bool SupportsExternalBlockStorage,
    long MinimumCapacityGiB = 0,
    long MaximumCapacityGiB = 0,
    long CapacityIncrementGiB = 1,
    decimal EstimatedHourlyCostPerGiBEur = 0,
    string DeploymentMountPath = "/srv/united-tactical-forces/runtime",
    string? ReadinessFailure = null);

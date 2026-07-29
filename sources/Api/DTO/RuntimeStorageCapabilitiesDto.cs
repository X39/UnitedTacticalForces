namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record RuntimeStorageCapabilitiesDto
{
    public bool SupportsExternalBlockStorage { get; init; }
    public long MinimumCapacityGiB { get; init; }
    public long MaximumCapacityGiB { get; init; }
    public long CapacityIncrementGiB { get; init; }
    public decimal EstimatedHourlyCostPerGiBEur { get; init; }
    public string DeploymentMountPath { get; init; } = string.Empty;
    public string? ReadinessFailure { get; init; }
}

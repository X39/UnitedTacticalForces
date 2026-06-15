using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One stored path-selection used before intentional deprovisioning.
/// </summary>
public sealed record PreservationSelectionDto
{
    public Guid PrimaryKey { get; init; }
    public string Path { get; init; } = string.Empty;
    public EPreservationScope Scope { get; init; }
    public EPreservationMode Mode { get; init; }
    public bool SeededByGame { get; init; }
}

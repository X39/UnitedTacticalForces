namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents editable role metadata.
/// </summary>
public record RoleUpdate
{
    /// <summary>
    /// The human-readable role title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The role description.
    /// </summary>
    public string? Description { get; init; }
}

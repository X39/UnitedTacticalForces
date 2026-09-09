namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Renames an external-organization cost owner.
/// </summary>
public sealed record CostOwnerDisplayNameUpdatePayload
{
    /// <summary>
    /// Gets the new display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}

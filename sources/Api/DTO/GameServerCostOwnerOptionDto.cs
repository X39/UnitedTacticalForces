namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// A billing owner the current caller may assign while creating a game server.
/// </summary>
public sealed record GameServerCostOwnerOptionDto
{
    /// <summary>
    /// Stable cost-owner identifier.
    /// </summary>
    public Guid CostOwnerId { get; init; }

    /// <summary>
    /// Human-readable owner name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this owner currently rejects new cost-generating assignments.
    /// </summary>
    public bool IsFrozen { get; init; }

    /// <summary>
    /// Whether this is the authenticated caller's personal cost owner.
    /// </summary>
    public bool IsCurrentUser { get; init; }
}

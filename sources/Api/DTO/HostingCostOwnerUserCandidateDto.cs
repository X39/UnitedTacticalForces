namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Identifies an active user who does not yet have a hosting cost owner.
/// </summary>
public sealed record HostingCostOwnerUserCandidateDto
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the current user nickname.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}

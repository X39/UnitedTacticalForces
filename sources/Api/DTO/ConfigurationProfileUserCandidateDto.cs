namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Identifies a user who can receive direct configuration-profile access.
/// </summary>
public sealed record ConfigurationProfileUserCandidateDto
{
    /// <summary>Gets the user identity.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the current display name.</summary>
    public string Nickname { get; init; } = string.Empty;
}

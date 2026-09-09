namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Identifies a role which can receive direct configuration-profile access.
/// </summary>
public sealed record ConfigurationProfileRoleCandidateDto
{
    /// <summary>Gets the role identity.</summary>
    public long RoleId { get; init; }

    /// <summary>Gets the current role title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the current role description.</summary>
    public string Description { get; init; } = string.Empty;
}

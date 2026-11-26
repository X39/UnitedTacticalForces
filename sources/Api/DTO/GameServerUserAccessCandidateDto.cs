namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One member that can be given direct access to a game server.
/// </summary>
public sealed record GameServerUserAccessCandidateDto
{
    /// <summary>
    /// User identity.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Current display name.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// Short fingerprint of the user identity, the character # followed by the last six characters of the user id,
    /// that tells members with equal nicknames apart.
    /// </summary>
    public string Detail { get; init; } = string.Empty;
}

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Command Center aggregate snapshot for community administration.
/// </summary>
public record CommandCenterCommunitySnapshotDto
{
    /// <summary>
    /// Total amount of user records.
    /// </summary>
    public long MemberCount { get; init; }

    /// <summary>
    /// Total amount of verified user records.
    /// </summary>
    public long VerifiedMemberCount { get; init; }

    /// <summary>
    /// Total amount of unverified user records.
    /// </summary>
    public long UnverifiedMemberCount { get; init; }

    /// <summary>
    /// Total amount of banned user records.
    /// </summary>
    public long BannedMemberCount { get; init; }

    /// <summary>
    /// Total amount of deleted user records.
    /// </summary>
    public long DeletedMemberCount { get; init; }

    /// <summary>
    /// Total amount of active formal Verein memberships.
    /// </summary>
    public long ActiveVereinMemberCount { get; init; }

    /// <summary>
    /// Total amount of suspended formal Verein memberships.
    /// </summary>
    public long SuspendedVereinMemberCount { get; init; }

    /// <summary>
    /// Total amount of ended formal Verein memberships.
    /// </summary>
    public long EndedVereinMemberCount { get; init; }

    /// <summary>
    /// Total amount of role records.
    /// </summary>
    public long RoleCount { get; init; }

    /// <summary>
    /// Total amount of claim records.
    /// </summary>
    public long ClaimCount { get; init; }

    /// <summary>
    /// Matching member rows for the current page query.
    /// </summary>
    public CommandCenterCommunityMemberDto[] Members { get; init; } = [];
}

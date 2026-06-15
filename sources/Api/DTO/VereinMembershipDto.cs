using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents formal Verein membership state for a user.
/// </summary>
public record VereinMembershipDto
{
    /// <summary>
    /// Membership primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// User this membership belongs to.
    /// </summary>
    public Guid UserFk { get; init; }

    /// <summary>
    /// User display name when included in the current projection.
    /// </summary>
    public string? UserNickname { get; init; }

    /// <summary>
    /// Optional association member number.
    /// </summary>
    public string? MemberNumber { get; init; }

    /// <summary>
    /// Current membership status.
    /// </summary>
    public EVereinMembershipStatus Status { get; init; }

    /// <summary>
    /// Whether this membership currently grants member-only access.
    /// </summary>
    public bool IsActiveMember { get; init; }

    /// <summary>
    /// Membership start timestamp.
    /// </summary>
    public Instant TimeStampStarted { get; init; }

    /// <summary>
    /// Membership end timestamp.
    /// </summary>
    public Instant? TimeStampEnded { get; init; }

    /// <summary>
    /// Administrative notes.
    /// </summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }
}

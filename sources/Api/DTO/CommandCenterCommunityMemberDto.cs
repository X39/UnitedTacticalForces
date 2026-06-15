namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a compact user row for Command Center community administration.
/// </summary>
public record CommandCenterCommunityMemberDto
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// The user's avatar image bytes.
    /// </summary>
    public byte[] Avatar { get; init; } = [];

    /// <summary>
    /// The MIME type of the user's avatar.
    /// </summary>
    public string AvatarMimeType { get; init; } = string.Empty;

    /// <summary>
    /// Whether this user has verified their identity.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Whether this user is currently banned.
    /// </summary>
    public bool IsBanned { get; init; }

    /// <summary>
    /// Whether this user has been deleted and anonymized.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Formal membership status if this user has a Verein membership record.
    /// </summary>
    public string? VereinMembershipStatus { get; init; }

    /// <summary>
    /// Optional association member number.
    /// </summary>
    public string? VereinMemberNumber { get; init; }

    /// <summary>
    /// Whether this user is currently an active formal Vereinsmitglied.
    /// </summary>
    public bool IsActiveVereinMember { get; init; }

    /// <summary>
    /// Membership start timestamp.
    /// </summary>
    public Instant? VereinMembershipStarted { get; init; }

    /// <summary>
    /// Membership end timestamp.
    /// </summary>
    public Instant? VereinMembershipEnded { get; init; }

    /// <summary>
    /// The role titles assigned to this user.
    /// </summary>
    public string[] RoleTitles { get; init; } = [];

    /// <summary>
    /// The amount of direct claims assigned to this user.
    /// </summary>
    public int DirectClaimCount { get; init; }
}

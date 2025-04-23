namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for a user in the system.
/// </summary>
public record FullUserDto
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// The nickname of this user.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// The email address of this user.
    /// </summary>
    public string? EMail { get; init; }

    /// <summary>
    /// Whether this user is banned.
    /// </summary>
    public bool IsBanned { get; init; }

    /// <summary>
    /// The users avatar.
    /// </summary>
    public byte[] Avatar { get; init; } = [];

    /// <summary>
    /// The mime type of the users avatar.
    /// </summary>
    public string AvatarMimeType { get; init; } = string.Empty;

    /// <summary>
    /// Whether this user has verified their identity.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Whether this user has been deleted.
    /// </summary>
    /// <remarks>
    /// Deleted users are not removed from the database but are marked as deleted to retain data integrity.
    /// The data must be anonymized when setting this property to true to comply with GDPR.
    /// </remarks>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// The steam user data.
    /// </summary>
    public ulong? SteamId64 { get; init; }

    /// <summary>
    /// The discord user data.
    /// </summary>
    public ulong? DiscordId { get; init; }

    /// <summary>
    /// The discord user data.
    /// </summary>
    public string? DiscordUsername { get; init; }

    /// <summary>
    /// The claims this user has.
    /// </summary>
    public PlainClaimDto[] Claims { get; init; } = [];

    /// <summary>
    /// The roles this user has.
    /// </summary>
    public RoleDto[] Roles { get; init; } = [];
}

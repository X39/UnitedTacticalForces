namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for a user in the system.
/// </summary>
public record FullUserDto
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// The nickname of this user.
    /// </summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// The email address of this user.
    /// </summary>
    public string? EMail { get; set; }

    /// <summary>
    /// Whether this user is banned.
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// The users avatar.
    /// </summary>
    public byte[] Avatar { get; set; } = [];

    /// <summary>
    /// The mime type of the users avatar.
    /// </summary>
    public string AvatarMimeType { get; set; } = string.Empty;

    /// <summary>
    /// Whether this user has verified their identity.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Whether this user has been deleted.
    /// </summary>
    /// <remarks>
    /// Deleted users are not removed from the database but are marked as deleted to retain data integrity.
    /// The data must be anonymized when setting this property to true to comply with GDPR.
    /// </remarks>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The steam user data.
    /// </summary>
    public ulong? SteamId64 { get; set; }

    /// <summary>
    /// The discord user data.
    /// </summary>
    public ulong? DiscordId { get; set; }

    /// <summary>
    /// The discord user data.
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// The roles this user has.
    /// </summary>
    public RoleDto[] Roles { get; set; } = [];

    /// <summary>
    /// The claims this user has.
    /// </summary>
    public PlainClaimDto[] Claims { get; set; } = [];
}

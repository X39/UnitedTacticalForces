namespace X39.UnitedTacticalForces.Api.DTO;

public record PlainUserDto
{
    /// <summary>
    /// The unique identifier of the user hosting the event.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// The nickname of this user.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// The users avatar.
    /// </summary>
    public byte[] Avatar { get; init; } = [];

    /// <summary>
    /// The mime type of the users avatar.
    /// </summary>
    public string AvatarMimeType { get; init; } = string.Empty;
}

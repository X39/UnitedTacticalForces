namespace X39.UnitedTacticalForces.Api.DTO.Updates;

public record UserUpdate
{
    public byte[]? Avatar { get; set; }
    public string? AvatarMimeType { get; set; }
    public string? Nickname { get; set; }
    public string? EMail { get; set; }
    public bool? IsBanned { get; set; }
    public bool? IsVerified { get; set; }
}

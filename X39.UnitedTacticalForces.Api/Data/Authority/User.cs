using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

public class User
{
    [Key] public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string SteamUuid { get; set; } = string.Empty;
    public string? EMail { get; set; }
    public ICollection<Privilege>? Privileges { get; set; }

    public byte[] Avatar { get; set; } = Array.Empty<byte>();
    public string AvatarMimeType { get; set; } = string.Empty;
}
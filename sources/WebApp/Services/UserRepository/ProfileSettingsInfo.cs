namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public sealed class ProfileSettingsInfo
{
    public Guid PrimaryKey { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? EMail { get; set; }
    public byte[] Avatar { get; set; } = [];
    public string AvatarMimeType { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsVereinMember { get; set; }
    public string? VereinMembershipStatus { get; set; }
    public string? VereinMemberNumber { get; set; }
    public bool CanDeleteAccount { get; set; }
    public string? AccountDeletionBlockedReason { get; set; }
    public string? SteamId64 { get; set; }
    public string? DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

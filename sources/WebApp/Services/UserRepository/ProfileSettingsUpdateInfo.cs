namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public sealed class ProfileSettingsUpdateInfo
{
    public byte[]? Avatar { get; set; }
    public string? AvatarMimeType { get; set; }
    public string? Nickname { get; set; }
    public string? EMail { get; set; }
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

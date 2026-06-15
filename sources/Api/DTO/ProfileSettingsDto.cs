namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Editable profile settings for the currently authenticated user.
/// </summary>
public record ProfileSettingsDto
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// The nickname shown throughout the application.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// Optional email address.
    /// </summary>
    public string? EMail { get; init; }

    /// <summary>
    /// User avatar image bytes.
    /// </summary>
    public byte[] Avatar { get; init; } = [];

    /// <summary>
    /// MIME type of the avatar.
    /// </summary>
    public string AvatarMimeType { get; init; } = string.Empty;

    /// <summary>
    /// Whether the account is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Whether the user is an active formal Vereinsmitglied.
    /// </summary>
    public bool IsVereinMember { get; init; }

    /// <summary>
    /// Formal Verein membership status, if available.
    /// </summary>
    public string? VereinMembershipStatus { get; init; }

    /// <summary>
    /// Formal Verein member number, if available.
    /// </summary>
    public string? VereinMemberNumber { get; init; }

    /// <summary>
    /// Whether the account can be deleted through the self-service settings page.
    /// </summary>
    public bool CanDeleteAccount { get; init; }

    /// <summary>
    /// Reason why self-service account deletion is blocked, if applicable.
    /// </summary>
    public string? AccountDeletionBlockedReason { get; init; }

    /// <summary>
    /// Linked Steam identifier, if available.
    /// </summary>
    public string? SteamId64 { get; init; }

    /// <summary>
    /// Linked Discord identifier, if available.
    /// </summary>
    public string? DiscordId { get; init; }

    /// <summary>
    /// Linked Discord username, if available.
    /// </summary>
    public string? DiscordUsername { get; init; }

    /// <summary>
    /// Legal first name for formal Verein membership records.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Legal last name for formal Verein membership records.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Date of birth for formal Verein membership records.
    /// </summary>
    public LocalDate? DateOfBirth { get; init; }

    /// <summary>
    /// Optional phone number for formal Verein membership contact.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// First postal address line for formal Verein membership contact.
    /// </summary>
    public string? AddressLine1 { get; init; }

    /// <summary>
    /// Optional second postal address line for formal Verein membership contact.
    /// </summary>
    public string? AddressLine2 { get; init; }

    /// <summary>
    /// Postal code for formal Verein membership contact.
    /// </summary>
    public string? PostalCode { get; init; }

    /// <summary>
    /// City for formal Verein membership contact.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// Country for formal Verein membership contact.
    /// </summary>
    public string? Country { get; init; }
}

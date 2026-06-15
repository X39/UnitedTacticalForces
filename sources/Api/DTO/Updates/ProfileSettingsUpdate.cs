namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Editable profile settings for the currently authenticated user.
/// </summary>
public record ProfileSettingsUpdate
{
    /// <summary>
    /// Optional avatar bytes.
    /// </summary>
    public byte[]? Avatar { get; init; }

    /// <summary>
    /// MIME type for <see cref="Avatar"/>.
    /// </summary>
    public string? AvatarMimeType { get; init; }

    /// <summary>
    /// The nickname shown throughout the application.
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// Optional email address.
    /// </summary>
    public string? EMail { get; init; }

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

namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for submitting a formal Verein membership request.
/// </summary>
public record VereinMembershipRequestPayload
{
    /// <summary>
    /// Legal first name.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Legal last name.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Date of birth.
    /// </summary>
    public LocalDate DateOfBirth { get; init; }

    /// <summary>
    /// Email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Phone number.
    /// </summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>
    /// First postal address line.
    /// </summary>
    public string AddressLine1 { get; init; } = string.Empty;

    /// <summary>
    /// Optional second postal address line.
    /// </summary>
    public string? AddressLine2 { get; init; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>
    /// City.
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Country.
    /// </summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// Motivation or context.
    /// </summary>
    public string Motivation { get; init; } = string.Empty;

    /// <summary>
    /// Acknowledges Verein membership is separate from mission access.
    /// </summary>
    public bool AcknowledgesMembershipSeparation { get; init; }

    /// <summary>
    /// Acknowledges data processing for Verein administration.
    /// </summary>
    public bool AcknowledgesDataProcessing { get; init; }

    /// <summary>
    /// Acknowledges the current Satzung.
    /// </summary>
    public bool AcknowledgesSatzung { get; init; }
}

using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a formal Verein membership request.
/// </summary>
public record VereinMembershipRequestDto
{
    /// <summary>
    /// Request primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// User who submitted the request.
    /// </summary>
    public Guid UserFk { get; init; }

    /// <summary>
    /// User display name when included in the current projection.
    /// </summary>
    public string? UserNickname { get; init; }

    /// <summary>
    /// Legal first name submitted with the request.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Legal last name submitted with the request.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Date of birth submitted with the request.
    /// </summary>
    public LocalDate DateOfBirth { get; init; }

    /// <summary>
    /// Email address submitted with the request.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Phone number submitted with the request.
    /// </summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>
    /// First postal address line submitted with the request.
    /// </summary>
    public string AddressLine1 { get; init; } = string.Empty;

    /// <summary>
    /// Optional second postal address line submitted with the request.
    /// </summary>
    public string AddressLine2 { get; init; } = string.Empty;

    /// <summary>
    /// Postal code submitted with the request.
    /// </summary>
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>
    /// City submitted with the request.
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Country submitted with the request.
    /// </summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// Applicant motivation or context.
    /// </summary>
    public string Motivation { get; init; } = string.Empty;

    /// <summary>
    /// Acknowledgement that Verein membership is separate from mission access.
    /// </summary>
    public bool AcknowledgesMembershipSeparation { get; init; }

    /// <summary>
    /// Acknowledgement that personal data is processed for Verein administration.
    /// </summary>
    public bool AcknowledgesDataProcessing { get; init; }

    /// <summary>
    /// Acknowledgement that the applicant has read the Satzung.
    /// </summary>
    public bool AcknowledgesSatzung { get; init; }

    /// <summary>
    /// Current request status.
    /// </summary>
    public EVereinMembershipRequestStatus Status { get; init; }

    /// <summary>
    /// Administrative decision reason.
    /// </summary>
    public string DecisionReason { get; init; } = string.Empty;

    /// <summary>
    /// Reviewer user id.
    /// </summary>
    public Guid? ReviewedByFk { get; init; }

    /// <summary>
    /// Reviewer display name when included in the current projection.
    /// </summary>
    public string? ReviewedByName { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }

    /// <summary>
    /// Terminal-state timestamp.
    /// </summary>
    public Instant? TimeStampResolved { get; init; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents an authenticated application for formal Verein membership.
/// </summary>
[Index(nameof(UserFk), nameof(Status))]
[Index(nameof(Status), nameof(TimeStampCreated))]
public class VereinMembershipRequest : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// User who submitted the request.
    /// </summary>
    [ForeignKey(nameof(UserFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    /// <summary>
    /// Foreign key to <see cref="User"/>.
    /// </summary>
    public Guid UserFk { get; set; }

    /// <summary>
    /// Legal first name submitted with the request.
    /// </summary>
    [MaxLength(256)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Legal last name submitted with the request.
    /// </summary>
    [MaxLength(256)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth submitted with the request.
    /// </summary>
    public LocalDate DateOfBirth { get; set; }

    /// <summary>
    /// Email address submitted with the request.
    /// </summary>
    [MaxLength(1024)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Optional phone number submitted with the request.
    /// </summary>
    [MaxLength(64)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// First postal address line submitted with the request.
    /// </summary>
    [MaxLength(512)]
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// Optional second postal address line submitted with the request.
    /// </summary>
    [MaxLength(512)]
    public string AddressLine2 { get; set; } = string.Empty;

    /// <summary>
    /// Postal code submitted with the request.
    /// </summary>
    [MaxLength(32)]
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// City submitted with the request.
    /// </summary>
    [MaxLength(256)]
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Country submitted with the request.
    /// </summary>
    [MaxLength(128)]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Free-form motivation or context submitted by the applicant.
    /// </summary>
    [MaxLength(4096)]
    public string Motivation { get; set; } = string.Empty;

    /// <summary>
    /// Acknowledgement that Verein membership is separate from mission access.
    /// </summary>
    public bool AcknowledgesMembershipSeparation { get; set; }

    /// <summary>
    /// Acknowledgement that personal data is processed for Verein administration.
    /// </summary>
    public bool AcknowledgesDataProcessing { get; set; }

    /// <summary>
    /// Acknowledgement that the applicant has read the Satzung.
    /// </summary>
    public bool AcknowledgesSatzung { get; set; }

    /// <summary>
    /// Current review status.
    /// </summary>
    public EVereinMembershipRequestStatus Status { get; set; }

    /// <summary>
    /// Administrative decision reason visible to Verein managers.
    /// </summary>
    [MaxLength(512)]
    public string DecisionReason { get; set; } = string.Empty;

    /// <summary>
    /// User who last reviewed the request.
    /// </summary>
    [ForeignKey(nameof(ReviewedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? ReviewedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="ReviewedBy"/>.
    /// </summary>
    public Guid? ReviewedByFk { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// Review completion timestamp, if the request has a terminal state.
    /// </summary>
    public Instant? TimeStampResolved { get; set; }

    /// <summary>
    /// Audit records for this request.
    /// </summary>
    public ICollection<VereinMembershipRequestAudit>? Audits { get; set; }
}

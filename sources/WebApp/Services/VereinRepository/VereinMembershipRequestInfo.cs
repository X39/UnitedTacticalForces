using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinMembershipRequestInfo
{
    public long PrimaryKey { get; init; }

    public Guid UserFk { get; init; }

    public string? UserNickname { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string AddressLine1 { get; init; } = string.Empty;

    public string AddressLine2 { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string Motivation { get; init; } = string.Empty;

    public bool AcknowledgesMembershipSeparation { get; init; }

    public bool AcknowledgesDataProcessing { get; init; }

    public bool AcknowledgesSatzung { get; init; }

    public EVereinMembershipRequestStatus Status { get; init; }

    public string DecisionReason { get; init; } = string.Empty;

    public Guid? ReviewedByFk { get; init; }

    public string? ReviewedByName { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }

    public DateTimeOffset? TimeStampResolved { get; init; }
}

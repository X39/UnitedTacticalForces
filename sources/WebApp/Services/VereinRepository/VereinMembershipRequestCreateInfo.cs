namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinMembershipRequestCreateInfo
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string AddressLine1 { get; init; } = string.Empty;

    public string? AddressLine2 { get; init; }

    public string PostalCode { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string Motivation { get; init; } = string.Empty;

    public bool AcknowledgesMembershipSeparation { get; init; }

    public bool AcknowledgesDataProcessing { get; init; }

    public bool AcknowledgesSatzung { get; init; }
}

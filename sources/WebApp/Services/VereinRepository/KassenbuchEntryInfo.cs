using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record KassenbuchEntryInfo
{
    public long PrimaryKey { get; init; }

    public DateTimeOffset TimeStampBooked { get; init; }

    public EKassenbuchEntryDirection Direction { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EUR";

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Reference { get; init; }

    public string? DocumentNumber { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }

    public string? CreatedByName { get; init; }

    public string? UpdatedByName { get; init; }
}

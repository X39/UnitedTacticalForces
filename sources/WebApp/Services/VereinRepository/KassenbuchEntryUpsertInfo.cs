using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record KassenbuchEntryUpsertInfo
{
    public DateTimeOffset TimeStampBooked { get; init; } = DateTimeOffset.Now;

    public EKassenbuchEntryDirection Direction { get; init; }

    public decimal Amount { get; init; }

    public string? Currency { get; init; } = "EUR";

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Reference { get; init; }

    public string? DocumentNumber { get; init; }
}

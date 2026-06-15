using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for creating or updating a Kassenbuch entry.
/// </summary>
public record KassenbuchEntryPayload
{
    /// <summary>
    /// Booking date.
    /// </summary>
    public Instant TimeStampBooked { get; init; }

    /// <summary>
    /// Income or expense direction.
    /// </summary>
    public EKassenbuchEntryDirection Direction { get; init; }

    /// <summary>
    /// Positive amount.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Ledger category.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Entry description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// External reference.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Internal document number.
    /// </summary>
    public string? DocumentNumber { get; init; }
}

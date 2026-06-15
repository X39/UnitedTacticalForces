using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one Kassenbuch ledger entry.
/// </summary>
public record KassenbuchEntryDto
{
    /// <summary>
    /// Entry primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

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
    public string Currency { get; init; } = "EUR";

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

    /// <summary>
    /// Whether the entry is deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }

    /// <summary>
    /// Creator display name.
    /// </summary>
    public string? CreatedByName { get; init; }

    /// <summary>
    /// Last editor display name.
    /// </summary>
    public string? UpdatedByName { get; init; }
}

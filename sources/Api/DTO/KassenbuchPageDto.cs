namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a filtered Kassenbuch page with totals.
/// </summary>
public record KassenbuchPageDto
{
    /// <summary>
    /// Matching ledger entries.
    /// </summary>
    public KassenbuchEntryDto[] Entries { get; init; } = [];

    /// <summary>
    /// Total matching row count.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Total income for the filtered set.
    /// </summary>
    public decimal IncomeTotal { get; init; }

    /// <summary>
    /// Total expenses for the filtered set.
    /// </summary>
    public decimal ExpenseTotal { get; init; }

    /// <summary>
    /// Balance for the filtered set.
    /// </summary>
    public decimal Balance { get; init; }

    /// <summary>
    /// Currency for the totals.
    /// </summary>
    public string Currency { get; init; } = "EUR";
}

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record KassenbuchPageInfo
{
    public KassenbuchEntryInfo[] Entries { get; init; } = [];

    public long TotalCount { get; init; }

    public decimal IncomeTotal { get; init; }

    public decimal ExpenseTotal { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = "EUR";
}

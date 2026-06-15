namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinSettingsUpdateInfo
{
    public decimal? MembershipContributionMonthlyAmount { get; init; }

    public decimal? MembershipContributionAnnualAmount { get; init; }

    public string? MembershipContributionCurrency { get; init; }

    public string? MembershipContributionPublicTextDe { get; init; }

    public string? MembershipContributionPublicTextEn { get; init; }
}

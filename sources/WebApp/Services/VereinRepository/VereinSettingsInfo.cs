namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinSettingsInfo
{
    public const string DefaultContributionPublicTextDe =
        "Eine formale Vereinsmitgliedschaft hilft, laufende Infrastruktur-, Verwaltungs- und Betriebskosten zu tragen. Der aktuelle Beitrag wird vom Verein bestaetigt, bevor dein Antrag abgeschlossen wird.";

    public const string DefaultContributionPublicTextEn =
        "Formal Verein membership helps cover ongoing infrastructure, administration, and operating costs. The current contribution is confirmed by the Verein before your request is completed.";

    public decimal? MembershipContributionMonthlyAmount { get; init; }

    public decimal? MembershipContributionAnnualAmount { get; init; }

    public string MembershipContributionCurrency { get; init; } = "EUR";

    public string MembershipContributionPublicTextDe { get; init; } = DefaultContributionPublicTextDe;

    public string MembershipContributionPublicTextEn { get; init; } = DefaultContributionPublicTextEn;

    public DateTimeOffset? TimeStampUpdated { get; init; }
}

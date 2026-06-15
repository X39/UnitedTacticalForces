namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Public Verein settings safe for Join and Verein pages.
/// </summary>
public record VereinSettingsDto
{
    /// <summary>
    /// Configured monthly contribution amount, if one is currently published.
    /// </summary>
    public decimal? MembershipContributionMonthlyAmount { get; init; }

    /// <summary>
    /// Configured annual contribution amount, if one is currently published.
    /// </summary>
    public decimal? MembershipContributionAnnualAmount { get; init; }

    /// <summary>
    /// Three-letter currency code for the contribution amount.
    /// </summary>
    public string MembershipContributionCurrency { get; init; } = "EUR";

    /// <summary>
    /// German public contribution explanation.
    /// </summary>
    public string MembershipContributionPublicTextDe { get; init; } = string.Empty;

    /// <summary>
    /// English public contribution explanation.
    /// </summary>
    public string MembershipContributionPublicTextEn { get; init; } = string.Empty;

    /// <summary>
    /// Last update timestamp, if the settings have been saved.
    /// </summary>
    public Instant? TimeStampUpdated { get; init; }
}

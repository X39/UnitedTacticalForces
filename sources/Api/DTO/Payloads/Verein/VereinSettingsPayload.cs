namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for updating public Verein settings.
/// </summary>
public record VereinSettingsPayload
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
    public string? MembershipContributionCurrency { get; init; }

    /// <summary>
    /// German public contribution explanation.
    /// </summary>
    public string? MembershipContributionPublicTextDe { get; init; }

    /// <summary>
    /// English public contribution explanation.
    /// </summary>
    public string? MembershipContributionPublicTextEn { get; init; }
}

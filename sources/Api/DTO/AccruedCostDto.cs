namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The accrued provider cost of one hosting session, calculated by the control plane.
/// </summary>
public sealed record AccruedCostDto
{
    /// <summary>
    /// Gets the deployment that forms the session.
    /// </summary>
    /// <value>The primary key of the deployment the amount belongs to.</value>
    public required Guid DeploymentId { get; init; }

    /// <summary>
    /// Gets the accrued amount.
    /// </summary>
    /// <value>
    /// The amount in <see cref="Currency"/>, or <see langword="null"/> when it is unavailable; unavailable is never
    /// zero, and <see cref="UnavailableReason"/> explains why.
    /// </value>
    public required decimal? Amount { get; init; }

    /// <summary>
    /// Gets the currency of <see cref="Amount"/>.
    /// </summary>
    /// <value>The ISO currency code; currently always <c>EUR</c>.</value>
    public string Currency { get; init; } = "EUR";

    /// <summary>
    /// Gets the instant the amount is valid for.
    /// </summary>
    /// <value>The request instant while cost may still accrue, or the resolution instant once it has stopped.</value>
    public required Instant AsOf { get; init; }

    /// <summary>
    /// Gets whether the amount is final.
    /// </summary>
    /// <value><see langword="true"/> when cost has stopped and the amount can no longer advance.</value>
    public required bool IsFinal { get; init; }

    /// <summary>
    /// Gets the reason the amount is unavailable.
    /// </summary>
    /// <value>
    /// <c>rate</c>, <c>not-yet-charged</c> or <c>hidden</c> when <see cref="Amount"/> is <see langword="null"/>;
    /// otherwise <see langword="null"/>.
    /// </value>
    public string? UnavailableReason { get; init; }

    /// <summary>
    /// Gets the length of the provider's billing unit.
    /// </summary>
    /// <value>
    /// The number of whole seconds the provider bills in full for every started unit, for example <c>3600</c> for a
    /// provider that bills every started hour; <see langword="null"/> when <see cref="Amount"/> is
    /// <see langword="null"/>.
    /// </value>
    public int? BillingUnitSeconds { get; init; }

    /// <summary>
    /// Gets the instant the last started billing unit ends at.
    /// </summary>
    /// <value>
    /// The end of the last started billing unit at <see cref="AsOf"/>, which <see cref="Amount"/> covers, so the
    /// next unit starts then; <see langword="null"/> when <see cref="Amount"/> is <see langword="null"/>.
    /// </value>
    public Instant? BilledThrough { get; init; }
}

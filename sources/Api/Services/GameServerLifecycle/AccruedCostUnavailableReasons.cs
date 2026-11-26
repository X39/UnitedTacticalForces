namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The reason codes that explain why an accrued cost amount is unavailable.
/// </summary>
/// <remarks>
/// The codes are plain strings and travel unchanged over REST and the update stream, which share one vocabulary
/// because the update stream serializer has no enum converter.
/// </remarks>
public static class AccruedCostUnavailableReasons
{
    /// <summary>
    /// The deployment has no hourly rate, so no cost can be estimated for it.
    /// </summary>
    public const string Rate = "rate";

    /// <summary>
    /// Cost is possible but no provider-cost slice has been charged to the deployment yet.
    /// </summary>
    public const string NotYetCharged = "not-yet-charged";

    /// <summary>
    /// The viewer may see the server but not the session's cost owner, so no amount is disclosed.
    /// </summary>
    public const string Hidden = "hidden";

    /// <summary>
    /// The provider that billed the session declares no billing unit, or it is not registered, so the billed time
    /// cannot be rounded and no amount is guessed.
    /// </summary>
    public const string BillingUnit = "billing-unit";
}

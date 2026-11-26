using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The accrued provider cost of one deployment as the control plane calculated it for one instant.
/// </summary>
/// <param name="Amount">
/// The accrued amount in the cost owner's currency, or <see langword="null"/> when it is unavailable; unavailable
/// is never represented as zero.
/// </param>
/// <param name="AsOf">
/// The instant the amount is valid for: the request instant while the deployment may still cost, or the resolution
/// instant once cost has stopped.
/// </param>
/// <param name="IsFinal">
/// <see langword="true"/> when cost has stopped and the amount can no longer advance; otherwise
/// <see langword="false"/>.
/// </param>
/// <param name="UnavailableReason">
/// One of the <see cref="AccruedCostUnavailableReasons"/> codes when <paramref name="Amount"/> is
/// <see langword="null"/>; otherwise <see langword="null"/>.
/// </param>
/// <param name="BilledThrough">
/// The end of the last started billing unit at <paramref name="AsOf"/>, which the amount covers, or
/// <see langword="null"/> when no unit was applied.
/// </param>
/// <param name="BillingUnit">
/// The billing unit the amount was rounded to, or <see langword="null"/> when no unit was applied.
/// </param>
public sealed record AccruedCostResult(
    decimal? Amount,
    Instant AsOf,
    bool IsFinal,
    string? UnavailableReason,
    Instant? BilledThrough = null,
    RuntimeBillingUnit? BillingUnit = null);

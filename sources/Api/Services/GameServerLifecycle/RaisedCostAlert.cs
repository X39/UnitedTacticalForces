using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// A cost alert that an evaluation pass recorded, with the data needed to publish it.
/// </summary>
/// <param name="CostAlertId">The primary key of the persisted alert.</param>
/// <param name="GameServerId">The primary key of the server whose session reached the threshold.</param>
/// <param name="ServerTitle">The title of the server at the time of the crossing.</param>
/// <param name="CostOwnerId">The primary key of the cost owner the session is billed to.</param>
/// <param name="ThresholdAmount">The threshold amount that was reached.</param>
/// <param name="AccruedAmount">The accrued cost when the crossing was recorded.</param>
/// <param name="Currency">The ISO currency code of the amounts.</param>
/// <param name="CreatedAt">The instant the crossing was recorded.</param>
/// <param name="Kind">What the alert announces; it defaults to a cost threshold crossing.</param>
/// <param name="StopsAt">The instant the automatic stop follows, or <see langword="null" /> for threshold alerts.</param>
/// <param name="StopReason">Why the stop was announced, or <see langword="null" /> for threshold alerts.</param>
/// <param name="StopLimit">
/// The limit that applied (minutes for an idle stop, hours for a maximum runtime stop), or <see langword="null" />
/// for threshold alerts and schedule stops.
/// </param>
internal sealed record RaisedCostAlert(
    Guid CostAlertId,
    Guid GameServerId,
    string ServerTitle,
    Guid CostOwnerId,
    decimal ThresholdAmount,
    decimal AccruedAmount,
    string Currency,
    Instant CreatedAt,
    ECostAlertKind Kind = ECostAlertKind.CostThreshold,
    Instant? StopsAt = null,
    EAutoStopReason? StopReason = null,
    int? StopLimit = null);

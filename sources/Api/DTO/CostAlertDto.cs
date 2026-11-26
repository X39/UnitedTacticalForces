using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One in-app cost alert: the record that the accrued cost of a session reached one threshold of its cost owner, or
/// that an automatic stop is pending for it.
/// </summary>
public sealed record CostAlertDto
{
    /// <summary>
    /// Gets the identifier of the alert, used to dismiss it.
    /// </summary>
    /// <value>The primary key of the persisted alert.</value>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the server whose session reached the threshold.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid ServerId { get; init; }

    /// <summary>
    /// Gets the title of the server whose session reached the threshold.
    /// </summary>
    /// <value>The current title of the game server.</value>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets the cost owner the session was billed to when the threshold was reached.
    /// </summary>
    /// <value>The primary key of the cost owner.</value>
    public required Guid CostOwnerId { get; init; }

    /// <summary>
    /// Gets the threshold that was reached.
    /// </summary>
    /// <value>The threshold amount in <see cref="Currency"/>.</value>
    public required decimal ThresholdAmount { get; init; }

    /// <summary>
    /// Gets the accrued cost at the instant the crossing was recorded.
    /// </summary>
    /// <value>The accrued amount in <see cref="Currency"/>; it is never updated after the crossing.</value>
    public required decimal AccruedAmount { get; init; }

    /// <summary>
    /// Gets the currency of both amounts.
    /// </summary>
    /// <value>The ISO currency code.</value>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets the instant the crossing was recorded.
    /// </summary>
    /// <value>The creation time of the alert.</value>
    public required Instant CreatedAt { get; init; }

    /// <summary>
    /// Gets what the alert announces.
    /// </summary>
    /// <value>
    /// <see cref="ECostAlertKind.CostThreshold"/> for a threshold crossing and <see cref="ECostAlertKind.AutoStop"/>
    /// for an automatic stop warning.
    /// </value>
    public required ECostAlertKind Kind { get; init; }

    /// <summary>
    /// Gets the instant the announced automatic stop follows unless it is cancelled first.
    /// </summary>
    /// <value>The stop time, or <see langword="null"/> for threshold alerts.</value>
    public required Instant? StopsAt { get; init; }

    /// <summary>
    /// Gets why the automatic stop was announced.
    /// </summary>
    /// <value>The stop reason, or <see langword="null"/> for threshold alerts.</value>
    public required EAutoStopReason? StopReason { get; init; }

    /// <summary>
    /// Gets the limit that applied to the announced automatic stop.
    /// </summary>
    /// <value>
    /// Minutes for an idle stop and hours for a maximum runtime stop, or <see langword="null"/> for threshold alerts
    /// and schedule stops.
    /// </value>
    public required int? StopLimit { get; init; }
}

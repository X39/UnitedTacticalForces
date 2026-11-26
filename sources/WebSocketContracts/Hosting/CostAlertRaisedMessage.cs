namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>
/// Pushes a newly recorded cost alert to the users who may view the alert's cost owner.
/// </summary>
/// <remarks>
/// Sent right after the crossing was committed. It is best effort: the REST list of cost alerts is the source of
/// truth. The message carries only flat scalar properties because the update stream serializer uses web defaults
/// with reference preservation and has no NodaTime or enum converter. Users without a server-section claim have no
/// socket and rely on the REST list, Discord and email.
/// </remarks>
public sealed class CostAlertRaisedMessage
{
    /// <summary>
    /// Gets the identifier of the alert, used to dismiss it.
    /// </summary>
    /// <value>The primary key of the persisted alert.</value>
    public required Guid AlertId { get; init; }

    /// <summary>
    /// Gets the server whose session reached the threshold.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid ServerId { get; init; }

    /// <summary>
    /// Gets the title of the server at the time of the crossing.
    /// </summary>
    /// <value>The server title.</value>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets the cost owner the session is billed to.
    /// </summary>
    /// <value>The primary key of the cost owner.</value>
    public required Guid CostOwnerId { get; init; }

    /// <summary>
    /// Gets the threshold that was reached.
    /// </summary>
    /// <value>The threshold amount in <see cref="Currency"/>.</value>
    public required decimal ThresholdAmount { get; init; }

    /// <summary>
    /// Gets the accrued cost when the crossing was recorded.
    /// </summary>
    /// <value>The accrued amount in <see cref="Currency"/>.</value>
    public required decimal AccruedAmount { get; init; }

    /// <summary>
    /// Gets the currency of both amounts.
    /// </summary>
    /// <value>The ISO currency code.</value>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets the instant the crossing was recorded.
    /// </summary>
    /// <value>The creation time, serialized as an ISO-8601 string with offset.</value>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets what the alert announces.
    /// </summary>
    /// <value>
    /// The name of the alert kind: <c>CostThreshold</c> for a threshold crossing or <c>AutoStop</c> for an automatic
    /// stop warning. It is a string because this project does not reference the hosting contracts.
    /// </value>
    public string Kind { get; init; } = "CostThreshold";

    /// <summary>
    /// Gets the instant the announced automatic stop follows unless it is cancelled first.
    /// </summary>
    /// <value>The stop time, or <see langword="null"/> for threshold alerts.</value>
    public DateTimeOffset? StopsAt { get; init; }

    /// <summary>
    /// Gets why the automatic stop was announced.
    /// </summary>
    /// <value>The name of the stop reason (<c>Idle</c>, <c>MaxRuntime</c> or <c>Schedule</c>), or <see langword="null"/> for threshold alerts.</value>
    public string? StopReason { get; init; }

    /// <summary>
    /// Gets the limit that applied to the announced automatic stop.
    /// </summary>
    /// <value>
    /// Minutes for an idle stop and hours for a maximum runtime stop, or <see langword="null"/> for threshold alerts
    /// and schedule stops.
    /// </value>
    public int? StopLimit { get; init; }
}

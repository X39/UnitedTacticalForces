namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>
/// Tells the dismissing viewer's other tabs and devices that one cost alert was dismissed.
/// </summary>
/// <remarks>
/// Sent only to the per-user path of the viewer who dismissed the alert, never to other viewers of the same cost
/// owner, because dismissal is per viewer. It carries a single flat identifier.
/// </remarks>
public sealed class CostAlertDismissedMessage
{
    /// <summary>
    /// Gets the identifier of the dismissed alert.
    /// </summary>
    /// <value>The primary key of the alert the viewer dismissed.</value>
    public required Guid AlertId { get; init; }
}

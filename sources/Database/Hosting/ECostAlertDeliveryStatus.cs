namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The delivery state of one cost alert for one channel and recipient. Values are persisted as integers and must
/// never be renumbered once rows exist.
/// </summary>
public enum ECostAlertDeliveryStatus
{
    /// <summary>
    /// The delivery is deliverable and waits to be picked up by the dispatcher.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The dispatcher holds a lease on the delivery and is sending it.
    /// </summary>
    Sending = 1,

    /// <summary>
    /// The channel accepted the message.
    /// </summary>
    Sent = 2,

    /// <summary>
    /// The delivery failed permanently or exhausted its retries.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The delivery was never attempted because the channel is off, the recipient is not reachable through it, or
    /// nobody resolved; the status reason names the cause.
    /// </summary>
    Skipped = 4,
}

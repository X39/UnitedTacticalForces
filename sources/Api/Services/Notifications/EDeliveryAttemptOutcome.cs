namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Describes how a single delivery attempt over an external notification channel ended.
/// </summary>
internal enum EDeliveryAttemptOutcome
{
    /// <summary>
    /// The channel accepted the message. This outcome is terminal and the delivery is never attempted again.
    /// </summary>
    Sent,

    /// <summary>
    /// The attempt failed for a reason that may disappear on its own, such as a disconnected bot or an
    /// unreachable server. The delivery is retried with backoff.
    /// </summary>
    Transient,

    /// <summary>
    /// The channel refused the message for a reason that retrying cannot fix, such as a rejected recipient.
    /// The delivery is failed.
    /// </summary>
    Permanent,

    /// <summary>
    /// The channel is not configured on this installation. The delivery is skipped rather than failed.
    /// </summary>
    NotConfigured,
}

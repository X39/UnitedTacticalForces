namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// The truthful result of one delivery attempt over an external notification channel.
/// </summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Reason">
/// A fixed, secret-free reason code for any outcome other than <see cref="EDeliveryAttemptOutcome.Sent" />. It is
/// never an exception message, so it can be persisted and logged safely. <see langword="null" /> when the message
/// was sent.
/// </param>
internal sealed record DeliveryAttemptResult(EDeliveryAttemptOutcome Outcome, string? Reason)
{
    /// <summary>
    /// Creates the result of an accepted message.
    /// </summary>
    /// <returns>A result with outcome <see cref="EDeliveryAttemptOutcome.Sent" /> and no reason.</returns>
    public static DeliveryAttemptResult Sent() => new(EDeliveryAttemptOutcome.Sent, null);

    /// <summary>
    /// Creates the result of a retryable failure.
    /// </summary>
    /// <param name="reason">The fixed reason code.</param>
    /// <returns>A result with outcome <see cref="EDeliveryAttemptOutcome.Transient" />.</returns>
    public static DeliveryAttemptResult Transient(string reason) => new(EDeliveryAttemptOutcome.Transient, reason);

    /// <summary>
    /// Creates the result of a failure that retrying cannot fix.
    /// </summary>
    /// <param name="reason">The fixed reason code.</param>
    /// <returns>A result with outcome <see cref="EDeliveryAttemptOutcome.Permanent" />.</returns>
    public static DeliveryAttemptResult Permanent(string reason) => new(EDeliveryAttemptOutcome.Permanent, reason);

    /// <summary>
    /// Creates the result for a channel that is not configured.
    /// </summary>
    /// <param name="reason">The fixed reason code.</param>
    /// <returns>A result with outcome <see cref="EDeliveryAttemptOutcome.NotConfigured" />.</returns>
    public static DeliveryAttemptResult NotConfigured(string reason)
        => new(EDeliveryAttemptOutcome.NotConfigured, reason);
}

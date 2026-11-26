namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Provider-neutral email delivery that reports a truthful outcome.
/// </summary>
internal interface IEmailSender
{
    /// <summary>
    /// Gets a value indicating whether the sender has the settings it needs to attempt a delivery.
    /// </summary>
    /// <value><see langword="false" /> while email is not configured on this installation.</value>
    bool IsConfigured { get; }

    /// <summary>
    /// Builds the deterministic Message-Id of the email for one alert and one recipient.
    /// </summary>
    /// <param name="costAlertId">The id of the cost alert.</param>
    /// <param name="recipientUserId">The id of the recipient user.</param>
    /// <returns>
    /// A Message-Id of the form <c>{costAlertId:N}.{recipientUserId:N}@{domain}</c>, identical for repeated calls.
    /// </returns>
    string CreateMessageId(Guid costAlertId, Guid recipientUserId);

    /// <summary>
    /// Attempts to deliver one email.
    /// </summary>
    /// <param name="message">The email to send.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>
    /// <see cref="EDeliveryAttemptOutcome.NotConfigured" /> without a network connection while email is not
    /// configured; otherwise the outcome of the attempt with a fixed reason code.
    /// </returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<DeliveryAttemptResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

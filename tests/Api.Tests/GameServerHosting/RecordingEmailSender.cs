using X39.UnitedTacticalForces.Api.Services.Notifications;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// An <see cref="IEmailSender" /> that records every email and answers with scripted outcomes, so delivery tests
/// never open an SMTP connection.
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    /// <summary>
    /// The domain of the deterministic Message-Ids the fake builds.
    /// </summary>
    public const string MessageIdDomain = "alerts.test";

    private readonly Queue<DeliveryAttemptResult> _results = new();

    /// <inheritdoc />
    /// <value>
    /// <see langword="true" /> unless a test turns email off; settable so tests can simulate an unconfigured
    /// installation.
    /// </value>
    public bool IsConfigured { get; set; } = true;

    /// <summary>
    /// Gets the emails the sender was asked to deliver, in call order.
    /// </summary>
    /// <value>One entry per call.</value>
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>
    /// Gets or sets an exception thrown by every call instead of answering, or <see langword="null" /> to answer
    /// normally.
    /// </summary>
    /// <value>The exception to throw, if any.</value>
    public Exception? ThrowOnSend { get; set; }

    /// <summary>
    /// Scripts the results of the next calls. Once the script is used up, calls answer
    /// <see cref="DeliveryAttemptResult.Sent" />.
    /// </summary>
    /// <param name="results">The results returned by the next calls, in order.</param>
    public void Script(params DeliveryAttemptResult[] results)
    {
        foreach (var result in results)
            _results.Enqueue(result);
    }

    /// <inheritdoc />
    public string CreateMessageId(Guid costAlertId, Guid recipientUserId)
        => $"{costAlertId:N}.{recipientUserId:N}@{MessageIdDomain}";

    /// <inheritdoc />
    public Task<DeliveryAttemptResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        if (ThrowOnSend is not null)
            throw ThrowOnSend;
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : DeliveryAttemptResult.Sent());
    }
}

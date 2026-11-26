using X39.UnitedTacticalForces.Api.Services.Notifications;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// A <see cref="IDiscordDirectMessageSender" /> that records every direct message and answers with scripted
/// outcomes, so delivery tests never touch Discord.
/// </summary>
internal sealed class RecordingDiscordDirectMessageSender : IDiscordDirectMessageSender
{
    private readonly Queue<DeliveryAttemptResult> _results = new();

    /// <summary>
    /// Gets the direct messages the sender was asked to deliver, in call order.
    /// </summary>
    /// <value>One entry per call with the Discord user id and the message text.</value>
    public List<(ulong DiscordUserId, string Text)> Sent { get; } = [];

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
    public Task<DeliveryAttemptResult> SendAsync(
        ulong discordUserId,
        string text,
        CancellationToken cancellationToken)
    {
        Sent.Add((discordUserId, text));
        if (ThrowOnSend is not null)
            throw ThrowOnSend;
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : DeliveryAttemptResult.Sent());
    }
}

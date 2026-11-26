namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Sends a direct message to a Discord user and reports a truthful outcome.
/// </summary>
internal interface IDiscordDirectMessageSender
{
    /// <summary>
    /// Attempts to deliver a direct message to the given Discord user.
    /// </summary>
    /// <param name="discordUserId">The Discord snowflake of the recipient.</param>
    /// <param name="text">The message text. Mentions in it are never resolved.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>
    /// The outcome of the attempt. <see cref="EDeliveryAttemptOutcome.Sent" /> is only reported after Discord
    /// accepted the message.
    /// </returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<DeliveryAttemptResult> SendAsync(ulong discordUserId, string text, CancellationToken cancellationToken);
}

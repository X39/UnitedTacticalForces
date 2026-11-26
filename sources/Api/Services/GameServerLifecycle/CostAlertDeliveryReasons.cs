namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The fixed reason codes stored on a skipped or failed cost alert delivery. They are the only failure detail that is
/// persisted, so no exception message or secret reaches the database.
/// </summary>
internal static class CostAlertDeliveryReasons
{
    /// <summary>
    /// The cost owner's setting turns the channel off.
    /// </summary>
    public const string ChannelDisabled = "channel-disabled";

    /// <summary>
    /// The recipient has no linked Discord account.
    /// </summary>
    public const string NotLinked = "not-linked";

    /// <summary>
    /// The recipient has no email address.
    /// </summary>
    public const string NoAddress = "no-address";

    /// <summary>
    /// Nobody resolved as a recipient for the cost owner.
    /// </summary>
    public const string NoRecipient = "no-recipient";

    /// <summary>
    /// The channel is not configured on this installation.
    /// </summary>
    public const string NotConfigured = "not-configured";

    /// <summary>
    /// The recipient was deleted or banned before the delivery was attempted.
    /// </summary>
    public const string RecipientInactive = "recipient-inactive";

    /// <summary>
    /// The delivery failed on every allowed attempt.
    /// </summary>
    public const string RetriesExhausted = "retries-exhausted";

    /// <summary>
    /// The automatic stop the warning announced was cancelled or already started, so a warning no longer helps.
    /// </summary>
    public const string StopResolved = "stop-resolved";
}

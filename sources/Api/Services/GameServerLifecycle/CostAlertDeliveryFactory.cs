using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Creates the delivery intents of a cost alert. Threshold alerts and automatic stop warnings share these rules so a
/// warning reaches exactly the recipients and channels a threshold alert would reach.
/// </summary>
internal static class CostAlertDeliveryFactory
{
    /// <summary>
    /// Creates the delivery intents of one alert: one row per channel and recipient, or one skipped row per channel
    /// when nobody resolved.
    /// </summary>
    /// <param name="alert">The alert the rows belong to.</param>
    /// <param name="setting">
    /// The cost owner's setting, which decides which channels are on, or <see langword="null" /> when the cost owner
    /// has no setting, in which case both channels count as disabled.
    /// </param>
    /// <param name="recipients">The resolved recipients of the cost owner.</param>
    /// <param name="now">The instant the rows are created at.</param>
    /// <returns>The delivery rows to insert with the alert.</returns>
    public static IEnumerable<CostAlertDelivery> CreateDeliveries(
        CostAlert alert,
        CostAlertSetting? setting,
        IReadOnlyList<CostAlertRecipient> recipients,
        Instant now)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(recipients);
        foreach (var channel in new[] { ECostAlertChannel.Discord, ECostAlertChannel.Email })
        {
            var enabled = setting is not null
                          && (channel == ECostAlertChannel.Discord ? setting.DiscordEnabled : setting.EmailEnabled);
            if (recipients.Count == 0)
            {
                yield return CreateDelivery(
                    alert,
                    channel,
                    null,
                    ECostAlertDeliveryStatus.Skipped,
                    CostAlertDeliveryReasons.NoRecipient,
                    now);
                continue;
            }

            foreach (var recipient in recipients)
            {
                var (status, reason) = !enabled
                    ? (ECostAlertDeliveryStatus.Skipped, CostAlertDeliveryReasons.ChannelDisabled)
                    : channel == ECostAlertChannel.Discord && !recipient.HasDiscord
                        ? (ECostAlertDeliveryStatus.Skipped, CostAlertDeliveryReasons.NotLinked)
                        : channel == ECostAlertChannel.Email && !recipient.HasEmail
                            ? (ECostAlertDeliveryStatus.Skipped, CostAlertDeliveryReasons.NoAddress)
                            : (ECostAlertDeliveryStatus.Pending, (string?) null);
                yield return CreateDelivery(alert, channel, recipient.UserId, status, reason, now);
            }
        }
    }

    /// <summary>
    /// Creates one delivery row.
    /// </summary>
    /// <param name="alert">The alert the row belongs to.</param>
    /// <param name="channel">The channel of the delivery.</param>
    /// <param name="recipientUserId">The recipient, or <see langword="null"/> when nobody resolved.</param>
    /// <param name="status">The initial status.</param>
    /// <param name="reason">The reason code of a skipped row, otherwise <see langword="null"/>.</param>
    /// <param name="now">The instant the row is created at.</param>
    /// <returns>The unsaved delivery row.</returns>
    private static CostAlertDelivery CreateDelivery(
        CostAlert alert,
        ECostAlertChannel channel,
        Guid? recipientUserId,
        ECostAlertDeliveryStatus status,
        string? reason,
        Instant now)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            CostAlertFk = alert.PrimaryKey,
            Channel = channel,
            RecipientUserFk = recipientUserId,
            Status = status,
            StatusReason = reason,
            TimeStampNextAttempt = status == ECostAlertDeliveryStatus.Pending ? now : null,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
}

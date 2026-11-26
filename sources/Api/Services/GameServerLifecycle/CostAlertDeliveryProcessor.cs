using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Claims due cost alert deliveries, renders them in the recipient's language, sends them through the Discord or
/// email adapter and records a truthful outcome.
/// </summary>
/// <remarks>
/// <para>
/// A row is claimed before anything is sent: a conditional update persists <see cref="ECostAlertDeliveryStatus.Sending" />,
/// the lease and the incremented attempt count (persist intent first), so a second dispatcher instance cannot claim
/// the same row. The claim predicate only matches pending rows that are due and sending rows whose lease expired;
/// <see cref="ECostAlertDeliveryStatus.Sent" />, <see cref="ECostAlertDeliveryStatus.Failed" /> and
/// <see cref="ECostAlertDeliveryStatus.Skipped" /> are terminal and never claimed again.
/// </para>
/// <para>
/// Delivery is at-least-once: if the process dies between a successful send and the write of
/// <see cref="ECostAlertDeliveryStatus.Sent" />, the row is sent once more after its lease expired. The deterministic
/// email Message-Id marks that copy as the same message.
/// </para>
/// <para>
/// Discord messages go through <see cref="IDiscordDirectMessageSender" /> and never through the bot's
/// connected-only action helper, because that helper silently does nothing while the bot is disconnected and would
/// make an undelivered alert look sent. Logs and stored reasons contain ids and fixed reason codes only.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context the outbox is read and updated with.</param>
/// <param name="discordSender">The adapter that sends direct messages through the Discord bot.</param>
/// <param name="emailSender">The adapter that sends emails over SMTP.</param>
/// <param name="baseUrl">The resolver of the client base URL used for the server link.</param>
/// <param name="options">The cost alert options with lease, batch and retry settings.</param>
/// <param name="logger">The logger that receives one line per outcome without personal data.</param>
[Scoped<CostAlertDeliveryProcessor, ICostAlertDeliveryProcessor>]
internal sealed class CostAlertDeliveryProcessor(
    DatabaseContext dbContext,
    IDiscordDirectMessageSender discordSender,
    IEmailSender emailSender,
    BaseUrl baseUrl,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertDeliveryProcessor> logger) : ICostAlertDeliveryProcessor
{
    /// <summary>
    /// The reason code stored when sending threw an unexpected exception.
    /// </summary>
    private const string DispatchErrorReason = "dispatch-error";

    /// <summary>
    /// The length of the reason column.
    /// </summary>
    private const int ReasonMaxLength = 128;

    /// <summary>
    /// The length of the lease owner column.
    /// </summary>
    private const int LeaseOwnerMaxLength = 256;

    /// <summary>
    /// The identity this process claims leases with: machine name, process id and a random suffix.
    /// </summary>
    private static readonly string LeaseOwnerId = CreateLeaseOwnerId();

    /// <inheritdoc />
    public async Task<int> ProcessDueAsync(Instant now, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var candidateIds = await dbContext.CostAlertDeliveries
            .AsNoTracking()
            .Where(DuePredicate(now))
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.PrimaryKey)
            .Take(settings.DeliveryBatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var claimed = 0;
        foreach (var deliveryId in candidateIds)
        {
            if (!await TryClaimAsync(deliveryId, now, settings, cancellationToken).ConfigureAwait(false))
                continue;
            claimed++;
            await AttemptAsync(deliveryId, now, settings, cancellationToken).ConfigureAwait(false);
        }

        return claimed;
    }

    /// <summary>
    /// Creates the predicate that selects pending deliveries that are due and sending deliveries whose lease
    /// expired.
    /// </summary>
    /// <param name="now">The instant the due check is made at.</param>
    /// <returns>The predicate used by both the candidate query and the conditional claim.</returns>
    private static Expression<Func<CostAlertDelivery, bool>> DuePredicate(Instant now)
        => q => (q.Status == ECostAlertDeliveryStatus.Pending
                 && (q.TimeStampNextAttempt == null || q.TimeStampNextAttempt <= now))
                || (q.Status == ECostAlertDeliveryStatus.Sending
                    && q.LeaseExpires != null
                    && q.LeaseExpires < now);

    /// <summary>
    /// Builds the lease owner identity of this process.
    /// </summary>
    /// <returns>An identity of at most <see cref="LeaseOwnerMaxLength" /> characters.</returns>
    private static string CreateLeaseOwnerId()
    {
        var owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        return owner.Length <= LeaseOwnerMaxLength ? owner : owner[^LeaseOwnerMaxLength..];
    }

    /// <summary>
    /// Truncates a reason code to the width of the reason column.
    /// </summary>
    /// <param name="reason">The reason code, or <see langword="null" />.</param>
    /// <returns>The reason, cut to <see cref="ReasonMaxLength" /> characters.</returns>
    private static string? TruncateReason(string? reason)
        => reason is { Length: > ReasonMaxLength } ? reason[..ReasonMaxLength] : reason;

    /// <summary>
    /// Atomically claims one delivery by persisting the sending state, the lease and the attempt count.
    /// </summary>
    /// <param name="deliveryId">The delivery to claim.</param>
    /// <param name="now">The claim instant.</param>
    /// <param name="settings">The cost alert options.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns><see langword="true" /> when this instance won the claim; otherwise <see langword="false" />.</returns>
    private async Task<bool> TryClaimAsync(
        Guid deliveryId,
        Instant now,
        CostAlertOptions settings,
        CancellationToken cancellationToken)
    {
        var leaseExpires = now + Duration.FromTimeSpan(settings.DeliveryLease);
        var affected = await dbContext.CostAlertDeliveries
            .Where(DuePredicate(now))
            .Where(q => q.PrimaryKey == deliveryId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(q => q.Status, ECostAlertDeliveryStatus.Sending)
                    .SetProperty(q => q.LeaseOwner, LeaseOwnerId)
                    .SetProperty(q => q.LeaseExpires, leaseExpires)
                    .SetProperty(q => q.AttemptCount, q => q.AttemptCount + 1)
                    .SetProperty(q => q.TimeStampUpdated, now),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    /// <summary>
    /// Sends one claimed delivery and records its outcome.
    /// </summary>
    /// <param name="deliveryId">The claimed delivery.</param>
    /// <param name="now">The instant that stamps the recorded outcome.</param>
    /// <param name="settings">The cost alert options.</param>
    /// <param name="cancellationToken">Cancels the send; the lease then expires and the row is reclaimed.</param>
    /// <returns>A task completing after the outcome was recorded.</returns>
    private async Task AttemptAsync(
        Guid deliveryId,
        Instant now,
        CostAlertOptions settings,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.CostAlertDeliveries
            .AsNoTracking()
            .Where(q => q.PrimaryKey == deliveryId)
            .Select(q => new DeliveryFacts(
                q.PrimaryKey,
                q.CostAlertFk,
                q.Channel,
                q.RecipientUserFk,
                q.AttemptCount,
                q.CostAlert!.GameServerFk,
                q.CostAlert.GameServer!.Title,
                q.CostAlert.ThresholdAmount,
                q.CostAlert.AccruedAmount,
                q.CostAlert.Currency,
                q.CostAlert.TimeStampCreated,
                q.CostAlert.Kind,
                q.CostAlert.PendingStop == null ? null : q.CostAlert.PendingStop.StopsAt,
                q.CostAlert.PendingStop == null ? null : q.CostAlert.PendingStop.Reason,
                q.CostAlert.PendingStop == null ? null : q.CostAlert.PendingStop.Limit))
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        var recipient = delivery.RecipientUserFk is { } recipientId
            ? await dbContext.Users
                .AsNoTracking()
                .Where(q => q.PrimaryKey == recipientId)
                .Select(q => new RecipientFacts(q.IsBanned, q.IsDeleted, q.Locale, q.EMail, q.Discord.Id))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        DeliveryAttemptResult result;
        try
        {
            result = await SendAsync(delivery, recipient, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The exception text may contain addresses or credentials, so only a fixed code is kept.
            result = DeliveryAttemptResult.Transient(DispatchErrorReason);
        }

        await RecordOutcomeAsync(delivery, result, now, settings).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the alert for the recipient and hands it to the delivery's channel.
    /// </summary>
    /// <param name="delivery">The facts of the claimed delivery.</param>
    /// <param name="recipient">The recipient, or <see langword="null" /> when the user no longer exists.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The outcome of the attempt.</returns>
    private async Task<DeliveryAttemptResult> SendAsync(
        DeliveryFacts delivery,
        RecipientFacts? recipient,
        CancellationToken cancellationToken)
    {
        if (recipient is null || recipient.IsBanned || recipient.IsDeleted)
            return DeliveryAttemptResult.NotConfigured(CostAlertDeliveryReasons.RecipientInactive);

        switch (delivery.Channel)
        {
            case ECostAlertChannel.Discord:
            {
                if (recipient.DiscordId == 0)
                    return DeliveryAttemptResult.NotConfigured(CostAlertDeliveryReasons.NotLinked);
                var message = Render(delivery, recipient);
                return await discordSender
                    .SendAsync(recipient.DiscordId, CostAlertMessageRenderer.ToDiscordText(message), cancellationToken)
                    .ConfigureAwait(false);
            }
            case ECostAlertChannel.Email:
            {
                if (!emailSender.IsConfigured)
                    return DeliveryAttemptResult.NotConfigured(CostAlertDeliveryReasons.NotConfigured);
                if (string.IsNullOrWhiteSpace(recipient.EMail))
                    return DeliveryAttemptResult.NotConfigured(CostAlertDeliveryReasons.NoAddress);
                var message = Render(delivery, recipient);
                var messageId = emailSender.CreateMessageId(delivery.CostAlertId, delivery.RecipientUserFk!.Value);
                return await emailSender
                    .SendAsync(
                        new EmailMessage(recipient.EMail, message.Subject, message.Body, messageId),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(delivery), delivery.Channel, "Unknown channel.");
        }
    }

    /// <summary>
    /// Renders the alert in the recipient's recorded language: the threshold text for a cost threshold alert and the
    /// automatic shutdown warning for an automatic stop alert.
    /// </summary>
    /// <param name="delivery">The facts of the alert.</param>
    /// <param name="recipient">The recipient whose locale is used.</param>
    /// <returns>The localized subject and body.</returns>
    private CostAlertMessage Render(DeliveryFacts delivery, RecipientFacts recipient)
    {
        if (delivery.Kind == ECostAlertKind.AutoStop)
        {
            if (delivery.StopsAt is not { } stopsAt || delivery.StopReason is not { } stopReason)
                throw new InvalidOperationException("The automatic stop alert has no pending stop.");

            return CostAlertMessageRenderer.RenderAutoStop(
                new AutoStopMessageModel(
                    delivery.ServerTitle,
                    BuildServerLink(delivery.GameServerId),
                    stopReason,
                    delivery.StopLimit,
                    stopsAt),
                recipient.Locale);
        }

        var model = new CostAlertMessageModel(
            delivery.ServerTitle,
            delivery.ThresholdAmount,
            delivery.AccruedAmount,
            delivery.Currency,
            delivery.AlertCreatedAt,
            BuildServerLink(delivery.GameServerId));
        return CostAlertMessageRenderer.Render(model, recipient.Locale);
    }

    /// <summary>
    /// Builds the absolute link to the server in the app.
    /// </summary>
    /// <param name="serverId">The server the link points to.</param>
    /// <returns>
    /// The link, or <see langword="null" /> when the configured client URL is not absolute and the message must use
    /// the sentence without a link.
    /// </returns>
    private string? BuildServerLink(Guid serverId)
    {
        if (!Uri.TryCreate(baseUrl.ClientUrl, UriKind.Absolute, out _))
            return null;
        try
        {
            var path = string.Format(
                CultureInfo.InvariantCulture,
                options.Value.ServerLinkPathTemplate,
                serverId);
            return baseUrl.ResolveClientUrl(path);
        }
        catch (Exception ex) when (ex is FormatException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Records the outcome of an attempt, but only while this process still holds the lease.
    /// </summary>
    /// <param name="delivery">The facts of the attempted delivery.</param>
    /// <param name="result">The outcome of the attempt.</param>
    /// <param name="now">The instant that stamps the update.</param>
    /// <param name="settings">The cost alert options.</param>
    /// <returns>A task completing after the outcome was written or found to be stale.</returns>
    private async Task RecordOutcomeAsync(
        DeliveryFacts delivery,
        DeliveryAttemptResult result,
        Instant now,
        CostAlertOptions settings)
    {
        var (status, reason, nextAttempt, sentAt) = MapOutcome(delivery, result, now, settings);
        reason = TruncateReason(reason);
        // The outcome write is not cancellable: after a send that succeeded, losing the Sent write to a shutdown
        // would resend the alert once the lease expired.
        var affected = await dbContext.CostAlertDeliveries
            .Where(q => q.PrimaryKey == delivery.DeliveryId
                        && q.Status == ECostAlertDeliveryStatus.Sending
                        && q.LeaseOwner == LeaseOwnerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(q => q.Status, status)
                    .SetProperty(q => q.StatusReason, reason)
                    .SetProperty(q => q.LeaseOwner, (string?) null)
                    .SetProperty(q => q.LeaseExpires, (Instant?) null)
                    .SetProperty(q => q.TimeStampNextAttempt, nextAttempt)
                    .SetProperty(q => q.TimeStampSent, sentAt)
                    .SetProperty(q => q.TimeStampUpdated, now),
                CancellationToken.None)
            .ConfigureAwait(false);
        if (affected == 0)
        {
            logger.LogWarning(
                "Cost alert delivery {DeliveryId} of alert {AlertId} over {Channel} lost its lease; the outcome "
                + "{Outcome} was not recorded",
                delivery.DeliveryId,
                delivery.CostAlertId,
                delivery.Channel,
                result.Outcome);
            return;
        }

        logger.LogInformation(
            "Cost alert delivery {DeliveryId} of alert {AlertId} over {Channel} ended as {Status} "
            + "(attempt {Attempt}, reason {Reason})",
            delivery.DeliveryId,
            delivery.CostAlertId,
            delivery.Channel,
            status,
            delivery.AttemptCount,
            reason ?? "none");
    }

    /// <summary>
    /// Maps an attempt outcome to the state that is persisted.
    /// </summary>
    /// <param name="delivery">The facts of the attempted delivery.</param>
    /// <param name="result">The outcome of the attempt.</param>
    /// <param name="now">The instant the outcome is recorded at.</param>
    /// <param name="settings">The cost alert options.</param>
    /// <returns>The status, reason code, next attempt instant and sent instant to store.</returns>
    private static (ECostAlertDeliveryStatus Status, string? Reason, Instant? NextAttempt, Instant? SentAt) MapOutcome(
        DeliveryFacts delivery,
        DeliveryAttemptResult result,
        Instant now,
        CostAlertOptions settings)
    {
        switch (result.Outcome)
        {
            case EDeliveryAttemptOutcome.Sent:
                return (ECostAlertDeliveryStatus.Sent, null, null, now);
            case EDeliveryAttemptOutcome.Transient:
                if (delivery.AttemptCount >= settings.MaxDeliveryAttempts)
                {
                    return (
                        ECostAlertDeliveryStatus.Failed,
                        CostAlertDeliveryReasons.RetriesExhausted,
                        null,
                        null);
                }

                // The delay list applies by attempt number; its last entry covers every later attempt.
                var delays = settings.DeliveryRetryDelays;
                var delay = delays[Math.Clamp(delivery.AttemptCount - 1, 0, delays.Length - 1)];
                return (
                    ECostAlertDeliveryStatus.Pending,
                    result.Reason,
                    now + Duration.FromTimeSpan(delay),
                    null);
            case EDeliveryAttemptOutcome.Permanent:
                return (ECostAlertDeliveryStatus.Failed, result.Reason, null, null);
            case EDeliveryAttemptOutcome.NotConfigured:
                return (
                    ECostAlertDeliveryStatus.Skipped,
                    result.Reason ?? CostAlertDeliveryReasons.NotConfigured,
                    null,
                    null);
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown outcome.");
        }
    }

    /// <summary>
    /// The facts of a claimed delivery and its alert that are needed to send it.
    /// </summary>
    /// <param name="DeliveryId">The delivery id.</param>
    /// <param name="CostAlertId">The alert id.</param>
    /// <param name="Channel">The channel of the delivery.</param>
    /// <param name="RecipientUserFk">The recipient id, or <see langword="null" />.</param>
    /// <param name="AttemptCount">The attempt count after the claim.</param>
    /// <param name="GameServerId">The server the alert is about.</param>
    /// <param name="ServerTitle">The server title.</param>
    /// <param name="ThresholdAmount">The threshold that was reached.</param>
    /// <param name="AccruedAmount">The accrued amount at the crossing.</param>
    /// <param name="Currency">The ISO currency code.</param>
    /// <param name="AlertCreatedAt">The instant the alert was raised.</param>
    /// <param name="Kind">What the alert announces.</param>
    /// <param name="StopsAt">The instant of the announced automatic stop, or <see langword="null" /> for thresholds.</param>
    /// <param name="StopReason">Why the stop was announced, or <see langword="null" /> for thresholds.</param>
    /// <param name="StopLimit">The limit that applied to the stop, or <see langword="null" /> when none applies.</param>
    private sealed record DeliveryFacts(
        Guid DeliveryId,
        Guid CostAlertId,
        ECostAlertChannel Channel,
        Guid? RecipientUserFk,
        int AttemptCount,
        Guid GameServerId,
        string ServerTitle,
        decimal ThresholdAmount,
        decimal AccruedAmount,
        string Currency,
        Instant AlertCreatedAt,
        ECostAlertKind Kind,
        Instant? StopsAt,
        EAutoStopReason? StopReason,
        int? StopLimit);

    /// <summary>
    /// The facts of the recipient that are needed to send a delivery.
    /// </summary>
    /// <param name="IsBanned">Whether the user is banned.</param>
    /// <param name="IsDeleted">Whether the user was deleted.</param>
    /// <param name="Locale">The recorded message language.</param>
    /// <param name="EMail">The app-local email address.</param>
    /// <param name="DiscordId">The linked Discord id, zero when not linked.</param>
    private sealed record RecipientFacts(
        bool IsBanned,
        bool IsDeleted,
        string? Locale,
        string? EMail,
        ulong DiscordId);
}

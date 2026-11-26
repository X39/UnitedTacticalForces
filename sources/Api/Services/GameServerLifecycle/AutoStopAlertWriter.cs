using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Database-backed <see cref="IAutoStopAlertWriter" /> that reuses the cost alert delivery rules.
/// </summary>
/// <remarks>
/// <para>
/// Recipients come from <see cref="ICostAlertRecipientResolver" /> for the deployment's cost owner and the channel
/// flags from that owner's <see cref="CostAlertSetting" />. A cost owner without a setting gets both channels skipped
/// as disabled, but the in-app record is always created. The unique index on the pending stop of the alert allows one
/// alert per pending stop.
/// </para>
/// <para>
/// Resolution skips the rows that are still <see cref="ECostAlertDeliveryStatus.Pending" />. A row that the delivery
/// processor already claimed for sending may still be delivered, which is accepted because the message text is
/// neutral about whether the stop still happens (at-least-once delivery).
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context the alert is added to and read from.</param>
/// <param name="recipientResolver">The resolver of who the alert is addressed to.</param>
[Scoped<AutoStopAlertWriter, IAutoStopAlertWriter>]
internal sealed class AutoStopAlertWriter(
    DatabaseContext dbContext,
    ICostAlertRecipientResolver recipientResolver) : IAutoStopAlertWriter
{
    /// <inheritdoc />
    public async Task<RaisedCostAlert> AddWarningAsync(
        AutomationPendingStop pendingStop,
        GameServerDeployment deployment,
        string serverTitle,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingStop);
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(serverTitle);

        var costOwnerId = deployment.CostOwnerFk;
        var recipients = await recipientResolver
            .ResolveAsync([costOwnerId], cancellationToken)
            .ConfigureAwait(false);
        var setting = await dbContext.CostAlertSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.CostOwnerFk == costOwnerId, cancellationToken)
            .ConfigureAwait(false);
        var currency = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => q.PrimaryKey == costOwnerId)
            .Select(q => q.Currency)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var alert = new CostAlert
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            GameServerFk = deployment.GameServerFk,
            CostOwnerFk = costOwnerId,
            Kind = ECostAlertKind.AutoStop,
            PendingStopFk = pendingStop.PrimaryKey,
            ThresholdAmount = 0m,
            AccruedAmount = 0m,
            Currency = currency ?? "EUR",
            TimeStampCreated = now,
        };
        IReadOnlyList<CostAlertRecipient> ownerRecipients = recipients.TryGetValue(costOwnerId, out var resolved)
            ? resolved
            : [];
        foreach (var delivery in CostAlertDeliveryFactory.CreateDeliveries(alert, setting, ownerRecipients, now))
            alert.Deliveries.Add(delivery);

        dbContext.CostAlerts.Add(alert);
        return new RaisedCostAlert(
            alert.PrimaryKey,
            alert.GameServerFk,
            serverTitle,
            alert.CostOwnerFk,
            alert.ThresholdAmount,
            alert.AccruedAmount,
            alert.Currency,
            now,
            ECostAlertKind.AutoStop,
            pendingStop.StopsAt,
            pendingStop.Reason,
            pendingStop.Limit);
    }

    /// <inheritdoc />
    public async Task<ResolvedCostAlert?> ResolveAsync(
        Guid pendingStopId,
        Instant now,
        CancellationToken cancellationToken)
    {
        var alert = await dbContext.CostAlerts
            .Include(q => q.Deliveries)
            .SingleOrDefaultAsync(q => q.PendingStopFk == pendingStopId, cancellationToken)
            .ConfigureAwait(false);
        if (alert is null)
            return null;

        foreach (var delivery in alert.Deliveries.Where(q => q.Status == ECostAlertDeliveryStatus.Pending))
        {
            delivery.Status = ECostAlertDeliveryStatus.Skipped;
            delivery.StatusReason = CostAlertDeliveryReasons.StopResolved;
            delivery.TimeStampNextAttempt = null;
            delivery.TimeStampUpdated = now;
        }

        return new ResolvedCostAlert(alert.PrimaryKey, alert.GameServerFk, alert.CostOwnerFk);
    }
}

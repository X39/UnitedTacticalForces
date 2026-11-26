using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Publishes cost frames to fail-closed, cost-aware audiences through the update stream.
/// </summary>
/// <remarks>
/// <para>
/// Accrued value frames go to the intersection of the server view audience and the cost owner view audience, so a
/// viewer of a server who may not see its cost owner receives nothing. Alert frames go to the cost owner view
/// audience, including finance viewers without server access. A failed audience read drops the whole batch with one
/// warning instead of sending, exactly like the fleet change notifier.
/// </para>
/// <para>
/// Every frame is sent to <see cref="FleetChangeStreamPaths.ForUser"/> of one recipient. Users without a
/// server-section claim cannot open the fleet socket and therefore have no live push; they rely on the REST list,
/// Discord and email. The publisher runs after the evaluation transaction committed and never throws.
/// </para>
/// </remarks>
/// <param name="updateStreamService">The update stream the frames are sent through.</param>
/// <param name="authorizationService">The service that resolves the view audiences.</param>
/// <param name="logger">The logger that receives warnings naming ids only.</param>
[Scoped<CostAlertPublisher, ICostAlertPublisher>]
internal sealed class CostAlertPublisher(
    IUpdateStreamService updateStreamService,
    IHostingAuthorizationService authorizationService,
    ILogger<CostAlertPublisher> logger) : ICostAlertPublisher
{
    /// <inheritdoc />
    public async Task PublishAccrualsAsync(
        IReadOnlyList<DeploymentAccrual> accruals,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accruals);
        var costing = accruals.Where(q => q.IsCosting).ToArray();
        if (costing.Length == 0)
            return;

        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> serverAudience;
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> ownerAudience;
        try
        {
            serverAudience = await authorizationService
                .GetServerViewAudienceAsync(costing.Select(q => q.GameServerId).Distinct().ToArray(), cancellationToken)
                .ConfigureAwait(false);
            ownerAudience = await authorizationService
                .GetCostOwnerViewAudienceAsync(costing.Select(q => q.CostOwnerId).Distinct().ToArray(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve the audiences of {DeploymentCount} costing deployments; the accrued cost frames "
                + "of this pass were not sent",
                costing.Length);
            return;
        }

        foreach (var accrual in costing)
        {
            if (!serverAudience.TryGetValue(accrual.GameServerId, out var serverViewers)
                || !ownerAudience.TryGetValue(accrual.CostOwnerId, out var ownerViewers))
                continue;

            var message = new GameServerAccruedCostMessage
            {
                ServerId          = accrual.GameServerId,
                DeploymentId      = accrual.DeploymentId,
                Amount            = accrual.Result.Amount,
                Currency          = accrual.Currency,
                AsOf              = accrual.Result.AsOf.ToDateTimeOffset(),
                IsFinal           = accrual.Result.IsFinal,
                UnavailableReason = accrual.Result.UnavailableReason,
                BillingUnitSeconds = BillingUnitSecondsOf(accrual.Result),
                BilledThrough     = accrual.Result.Amount is null
                    ? null
                    : accrual.Result.BilledThrough?.ToDateTimeOffset(),
            };
            var ownerSet = ownerViewers.ToHashSet();
            foreach (var userId in serverViewers.Distinct().Where(ownerSet.Contains))
                await SendAsync(userId, message, accrual.GameServerId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the billing unit length that is pushed next to an accrued amount.
    /// </summary>
    /// <param name="result">The calculated accrued cost.</param>
    /// <returns>
    /// The whole seconds of the unit the amount was rounded to, rounded down, or <see langword="null"/> when the
    /// amount is unavailable or no unit was applied.
    /// </returns>
    private static int? BillingUnitSecondsOf(AccruedCostResult result)
        => result.Amount is null || result.BillingUnit is null
            ? null
            : (int) Math.Min(int.MaxValue, Math.Floor(result.BillingUnit.Length.TotalSeconds));

    /// <inheritdoc />
    public async Task PublishRaisedAsync(IReadOnlyList<RaisedCostAlert> alerts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        if (alerts.Count == 0)
            return;

        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> ownerAudience;
        try
        {
            ownerAudience = await authorizationService
                .GetCostOwnerViewAudienceAsync(alerts.Select(q => q.CostOwnerId).Distinct().ToArray(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve the audience of {AlertCount} new cost alerts; the alert frames of this pass were "
                + "not sent",
                alerts.Count);
            return;
        }

        foreach (var alert in alerts)
        {
            if (!ownerAudience.TryGetValue(alert.CostOwnerId, out var viewers))
                continue;

            var message = new CostAlertRaisedMessage
            {
                AlertId         = alert.CostAlertId,
                ServerId        = alert.GameServerId,
                ServerName      = alert.ServerTitle,
                CostOwnerId     = alert.CostOwnerId,
                ThresholdAmount = alert.ThresholdAmount,
                AccruedAmount   = alert.AccruedAmount,
                Currency        = alert.Currency,
                CreatedAt       = alert.CreatedAt.ToDateTimeOffset(),
                Kind            = alert.Kind.ToString(),
                StopsAt         = alert.StopsAt?.ToDateTimeOffset(),
                StopReason      = alert.StopReason?.ToString(),
                StopLimit       = alert.StopLimit,
            };
            foreach (var userId in viewers.Distinct())
                await SendAsync(userId, message, alert.GameServerId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task PublishDismissedAsync(
        Guid userId,
        IReadOnlyCollection<Guid> costAlertIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(costAlertIds);
        foreach (var costAlertId in costAlertIds)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            await SendAsync(userId, new CostAlertDismissedMessage { AlertId = costAlertId }, null)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task PublishResolvedAsync(IReadOnlyList<ResolvedCostAlert> alerts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        if (alerts.Count == 0)
            return;

        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> ownerAudience;
        try
        {
            ownerAudience = await authorizationService
                .GetCostOwnerViewAudienceAsync(alerts.Select(q => q.CostOwnerId).Distinct().ToArray(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve the audience of {AlertCount} withdrawn automatic stop warnings; the dismissal "
                + "frames were not sent",
                alerts.Count);
            return;
        }

        foreach (var alert in alerts)
        {
            if (!ownerAudience.TryGetValue(alert.CostOwnerId, out var viewers))
                continue;

            var message = new CostAlertDismissedMessage { AlertId = alert.CostAlertId };
            foreach (var userId in viewers.Distinct())
                await SendAsync(userId, message, alert.GameServerId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends one frame to the path of one recipient, logging instead of throwing.
    /// </summary>
    /// <typeparam name="T">The type of the frame.</typeparam>
    /// <param name="userId">The user whose own path receives the frame.</param>
    /// <param name="message">The frame to send.</param>
    /// <param name="gameServerId">The related server for the failure log, or <see langword="null"/> when none.</param>
    /// <returns>A task completing once the send finished or its failure was logged.</returns>
    private async Task SendAsync<T>(Guid userId, T message, Guid? gameServerId)
    {
        try
        {
            await updateStreamService
                .SendUpdateAsync(FleetChangeStreamPaths.ForUser(userId), message)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to publish a {FrameType} frame for game server {GameServerId} to user {UserId}",
                typeof(T).Name,
                gameServerId,
                userId);
        }
    }
}

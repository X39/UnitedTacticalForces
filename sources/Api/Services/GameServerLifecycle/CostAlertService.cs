using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Database-backed listing and per-viewer dismissal of in-app cost alerts.
/// </summary>
/// <remarks>
/// The owner filter of <see cref="ListAsync"/> and the owner check of <see cref="DismissAsync"/> both come from
/// <see cref="IHostingAuthorizationService"/>, which expresses one cost-owner view rule. An alert the viewer may not
/// see is reported as not found, so alert ids cannot be probed. The dismissal rows are unique per alert and viewer,
/// which makes dismissing idempotent across concurrent requests. After a dismissal is stored, the dismissing viewer's
/// own sockets are told so their other tabs and devices drop the alert. An automatic stop warning is listed only while
/// its pending stop is still pending; dismissing it hides it for that viewer alone and never cancels the stop.
/// </remarks>
/// <param name="dbContext">The scoped database context the alerts and dismissals are stored in.</param>
/// <param name="authorizationService">The service that decides which cost owners a viewer may see.</param>
/// <param name="publisher">The publisher that tells the dismissing viewer's other tabs about new dismissals.</param>
/// <param name="logger">The logger that records dismissals by alert id and user id only.</param>
[Scoped<CostAlertService, ICostAlertService>]
internal sealed class CostAlertService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    ICostAlertPublisher publisher,
    ILogger<CostAlertService> logger) : ICostAlertService
{
    /// <summary>
    /// The maximum number of alerts a list call returns.
    /// </summary>
    /// <value>Fifty alerts.</value>
    internal const int MaxListedAlerts = 50;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<CostAlertDto>> ListAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!principal.TryGetUserId(out var userId))
            return [];

        var viewable = await authorizationService
            .GetViewableCostOwnerIdsAsync(principal, cancellationToken)
            .ConfigureAwait(false);
        if (viewable is { Count: 0 })
            return [];

        var query = dbContext.CostAlerts.AsNoTracking()
            .Where(q => !q.Dismissals.Any(d => d.UserFk == userId))
            .Where(q => q.Kind != ECostAlertKind.AutoStop
                        || (q.PendingStop != null && q.PendingStop.Status == EAutoStopStatus.Pending));
        if (viewable is not null)
        {
            var ownerIds = viewable.ToArray();
            query = query.Where(q => ownerIds.Contains(q.CostOwnerFk));
        }

        return await query
            .OrderByDescending(q => q.TimeStampCreated)
            .Take(MaxListedAlerts)
            .Select(q => new CostAlertDto
            {
                Id              = q.PrimaryKey,
                ServerId        = q.GameServerFk,
                ServerName      = q.GameServer!.Title,
                CostOwnerId     = q.CostOwnerFk,
                ThresholdAmount = q.ThresholdAmount,
                AccruedAmount   = q.AccruedAmount,
                Currency        = q.Currency,
                CreatedAt       = q.TimeStampCreated,
                Kind            = q.Kind,
                StopsAt         = q.PendingStop == null ? null : q.PendingStop.StopsAt,
                StopReason      = q.PendingStop == null ? null : q.PendingStop.Reason,
                StopLimit       = q.PendingStop == null ? null : q.PendingStop.Limit,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DismissAsync(Guid costAlertId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!principal.TryGetUserId(out var userId))
            throw new HostingNotFoundException("The cost alert does not exist.");

        var alert = await dbContext.CostAlerts
            .AsNoTracking()
            .Where(q => q.PrimaryKey == costAlertId)
            .Select(q => new { q.PrimaryKey, q.CostOwnerFk })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (alert is null
            || !await authorizationService
                .CanViewCostOwnerAsync(principal, alert.CostOwnerFk, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingNotFoundException("The cost alert does not exist.");

        await DismissAlertsAsync([alert.PrimaryKey], userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DismissAllForServerAsync(
        Guid gameServerId,
        IReadOnlyCollection<Guid> costAlertIds,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(costAlertIds);
        ArgumentNullException.ThrowIfNull(principal);
        if (costAlertIds.Count is < 1 or > MaxListedAlerts)
        {
            throw new HostingValidationException(
                $"Between 1 and {MaxListedAlerts} cost alert ids must be given.");
        }

        if (!principal.TryGetUserId(out var userId))
            return;

        var viewable = await authorizationService
            .GetViewableCostOwnerIdsAsync(principal, cancellationToken)
            .ConfigureAwait(false);
        if (viewable is { Count: 0 })
            return;

        var requestedIds = costAlertIds.Distinct().ToArray();
        var query = dbContext.CostAlerts.AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId
                        && requestedIds.Contains(q.PrimaryKey)
                        && !q.Dismissals.Any(d => d.UserFk == userId));
        if (viewable is not null)
        {
            var ownerIds = viewable.ToArray();
            query = query.Where(q => ownerIds.Contains(q.CostOwnerFk));
        }

        var alertIds = await query
            .Select(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        await DismissAlertsAsync(alertIds, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores one dismissal per alert for the user, skipping alerts the user already dismissed.
    /// </summary>
    /// <param name="alertIds">The ids of the alerts to dismiss; they must already be visible to the user.</param>
    /// <param name="userId">The id of the dismissing viewer.</param>
    /// <param name="cancellationToken">A token that cancels the database work.</param>
    /// <returns>A task completing once the dismissals are stored.</returns>
    /// <remarks>
    /// A unique-index violation means a concurrent request stored the dismissal first, which is the requested end
    /// state, so it is not an error; any other save failure is rethrown.
    /// </remarks>
    private async Task DismissAlertsAsync(
        IReadOnlyCollection<Guid> alertIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (alertIds.Count == 0)
            return;

        var idArray = alertIds.ToArray();
        var already = await dbContext.CostAlertDismissals
            .AsNoTracking()
            .Where(q => q.UserFk == userId && idArray.Contains(q.CostAlertFk))
            .Select(q => q.CostAlertFk)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var added = new List<CostAlertDismissal>();
        foreach (var alertId in idArray.Except(already))
        {
            var dismissal = new CostAlertDismissal
            {
                PrimaryKey       = Guid.NewGuid(),
                CostAlertFk      = alertId,
                UserFk           = userId,
                TimeStampCreated = now,
            };
            dbContext.CostAlertDismissals.Add(dismissal);
            added.Add(dismissal);
        }

        if (added.Count == 0)
            return;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            foreach (var dismissal in added)
                dbContext.Entry(dismissal).State = EntityState.Detached;
            if (await AllDismissedAsync(idArray, userId, cancellationToken).ConfigureAwait(false))
                return;
            throw;
        }

        logger.LogInformation(
            "User {UserId} dismissed cost alerts {CostAlertIds}",
            userId,
            string.Join(',', added.Select(q => q.CostAlertFk)));
        await publisher
            .PublishDismissedAsync(userId, added.Select(q => q.CostAlertFk).ToArray(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns whether every given alert has a stored dismissal by the user.
    /// </summary>
    /// <param name="alertIds">The ids of the alerts to check.</param>
    /// <param name="userId">The id of the viewer.</param>
    /// <param name="cancellationToken">A token that cancels the database work.</param>
    /// <returns>A task resolving to <see langword="true"/> when each alert is dismissed by the user.</returns>
    private async Task<bool> AllDismissedAsync(Guid[] alertIds, Guid userId, CancellationToken cancellationToken)
    {
        var stored = await dbContext.CostAlertDismissals
            .AsNoTracking()
            .Where(q => q.UserFk == userId && alertIds.Contains(q.CostAlertFk))
            .Select(q => q.CostAlertFk)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        return stored == alertIds.Length;
    }
}

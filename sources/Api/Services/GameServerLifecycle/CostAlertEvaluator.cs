using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Compares the accrued cost of sessions with the thresholds of their cost owners and records each crossing once
/// together with its delivery intents.
/// </summary>
/// <remarks>
/// <para>
/// The crossing and its per-channel delivery rows are inserted in one transaction before any side effect happens
/// (persist intent first). The unique index on the deployment and threshold amount makes the pass idempotent: a
/// repeated pass, a restart of the API and a concurrent instance record nothing new for a crossing that exists.
/// </para>
/// <para>
/// Alert rows are never deleted when a threshold is removed from a setting, so re-adding a threshold cannot alert
/// the same session again. A new deployment of the same server starts with fresh crossings.
/// </para>
/// <para>
/// Amounts are compared in memory because the test provider stores decimals as text and cannot compare them in SQL.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context deployments, settings and alerts are read and written with.</param>
/// <param name="accrualService">The service that calculates the accrued cost of the evaluated deployments.</param>
/// <param name="recipientResolver">The resolver of who each alert is addressed to.</param>
/// <param name="options">The cost alert options.</param>
/// <param name="logger">The logger that receives diagnostics without any amount of a viewer-hidden session.</param>
[Scoped<CostAlertEvaluator, ICostAlertEvaluator>]
internal sealed class CostAlertEvaluator(
    DatabaseContext dbContext,
    ICostAccrualService accrualService,
    ICostAlertRecipientResolver recipientResolver,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertEvaluator> logger) : ICostAlertEvaluator
{
    /// <summary>
    /// The number of decimal places threshold amounts are stored with.
    /// </summary>
    private const int ThresholdDecimals = 2;

    /// <summary>
    /// The number of decimal places accrued amounts are stored with.
    /// </summary>
    private const int AccruedDecimals = 4;

    /// <inheritdoc />
    public async Task<CostAlertEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken)
    {
        var cutoff = now - Duration.FromTimeSpan(options.Value.ResolvedLookback);
        var candidates = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Include(q => q.GameServer)
            .Include(q => q.CostOwner)
            .Where(q => (q.TimeStampResolved == null && q.PossibleCost) || q.TimeStampResolved >= cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
            return new CostAlertEvaluationResult([], []);

        var results = await accrualService
            .CalculateForDeploymentsAsync(candidates, now, cancellationToken)
            .ConfigureAwait(false);
        var accruals = candidates
            .Select(q => new DeploymentAccrual(
                q.PrimaryKey,
                q.GameServerFk,
                q.CostOwnerFk,
                q.TimeStampResolved is null && q.PossibleCost,
                results[q.PrimaryKey],
                q.CostOwner?.Currency ?? "EUR"))
            .ToArray();

        var selected = await SelectCrossingsAsync(candidates, accruals, cancellationToken).ConfigureAwait(false);
        if (selected.Count == 0)
            return new CostAlertEvaluationResult(accruals, []);

        var recipients = await recipientResolver
            .ResolveAsync(selected.Select(q => q.Setting.CostOwnerFk).Distinct().ToArray(), cancellationToken)
            .ConfigureAwait(false);
        var alerts = new List<CostAlert>();
        var raised = new List<RaisedCostAlert>();
        foreach (var crossing in selected)
        {
            var alert = new CostAlert
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = crossing.Deployment.PrimaryKey,
                GameServerFk = crossing.Deployment.GameServerFk,
                CostOwnerFk = crossing.Deployment.CostOwnerFk,
                Kind = ECostAlertKind.CostThreshold,
                ThresholdAmount = crossing.Threshold,
                AccruedAmount = crossing.Accrued,
                Currency = crossing.Setting.CostOwner?.Currency ?? "EUR",
                TimeStampCreated = now,
            };
            foreach (var delivery in CostAlertDeliveryFactory.CreateDeliveries(
                         alert,
                         crossing.Setting,
                         recipients[crossing.Setting.CostOwnerFk],
                         now))
            {
                alert.Deliveries.Add(delivery);
            }

            alerts.Add(alert);
            raised.Add(new RaisedCostAlert(
                alert.PrimaryKey,
                alert.GameServerFk,
                crossing.Deployment.GameServer?.Title ?? string.Empty,
                alert.CostOwnerFk,
                alert.ThresholdAmount,
                alert.AccruedAmount,
                alert.Currency,
                now));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            dbContext.CostAlerts.AddRange(alerts);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            // Only a concurrent pass that recorded one of the same crossings is expected; any other failure surfaces.
            var recorded = await LoadRecordedCrossingsAsync(
                    candidates.Select(q => q.PrimaryKey).ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!selected.Any(q => recorded.Contains((q.Deployment.PrimaryKey, q.Threshold))))
                throw;
            logger.LogDebug("A cost alert crossing was already recorded by a concurrent evaluation pass.");
            return new CostAlertEvaluationResult(accruals, []);
        }

        dbContext.ChangeTracker.Clear();
        return new CostAlertEvaluationResult(accruals, raised);
    }

    /// <summary>
    /// Selects the thresholds that are reached by a known accrued amount and not recorded yet.
    /// </summary>
    /// <param name="candidates">The evaluated deployments.</param>
    /// <param name="accruals">The accrued cost per evaluated deployment.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>The crossings to record, ordered by deployment and ascending threshold.</returns>
    private async Task<List<Crossing>> SelectCrossingsAsync(
        IReadOnlyCollection<GameServerDeployment> candidates,
        IReadOnlyCollection<DeploymentAccrual> accruals,
        CancellationToken cancellationToken)
    {
        var ownerIds = candidates.Select(q => q.CostOwnerFk).Distinct().ToArray();
        var settings = (await dbContext.CostAlertSettings
                .AsNoTracking()
                .Include(q => q.Thresholds)
                .Include(q => q.CostOwner)
                .Where(q => ownerIds.Contains(q.CostOwnerFk))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(q => q.Thresholds.Count > 0)
            .ToDictionary(q => q.CostOwnerFk);
        var selected = new List<Crossing>();
        if (settings.Count == 0)
            return selected;

        var recorded = await LoadRecordedCrossingsAsync(
                candidates.Select(q => q.PrimaryKey).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var accrualsById = accruals.ToDictionary(q => q.DeploymentId);
        foreach (var deployment in candidates)
        {
            if (!settings.TryGetValue(deployment.CostOwnerFk, out var setting)
                || accrualsById[deployment.PrimaryKey].Result.Amount is not { } amount)
                continue;

            var thresholds = setting.Thresholds
                .Select(q => decimal.Round(q.Amount, ThresholdDecimals, MidpointRounding.AwayFromZero))
                .Where(q => q > 0m)
                .Distinct()
                .Order();
            foreach (var threshold in thresholds)
            {
                if (threshold > amount || recorded.Contains((deployment.PrimaryKey, threshold)))
                    continue;
                selected.Add(new Crossing(
                    deployment,
                    setting,
                    threshold,
                    decimal.Round(amount, AccruedDecimals, MidpointRounding.AwayFromZero)));
            }
        }

        return selected;
    }

    /// <summary>
    /// Loads the deployment and threshold pairs that already have a recorded alert.
    /// </summary>
    /// <param name="deploymentIds">The deployments to look up.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>The recorded pairs, compared by value in memory.</returns>
    private async Task<HashSet<(Guid DeploymentId, decimal Threshold)>> LoadRecordedCrossingsAsync(
        Guid[] deploymentIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CostAlerts
            .AsNoTracking()
            .Where(q => deploymentIds.Contains(q.DeploymentFk) && q.Kind == ECostAlertKind.CostThreshold)
            .Select(q => new { q.DeploymentFk, q.ThresholdAmount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(q => (q.DeploymentFk, decimal.Round(q.ThresholdAmount, ThresholdDecimals)))
            .ToHashSet();
    }

    /// <summary>
    /// One threshold crossing selected for recording.
    /// </summary>
    /// <param name="Deployment">The deployment whose cost reached the threshold.</param>
    /// <param name="Setting">The setting that holds the threshold.</param>
    /// <param name="Threshold">The threshold amount rounded to its stored scale.</param>
    /// <param name="Accrued">The accrued amount rounded to its stored scale.</param>
    private sealed record Crossing(
        GameServerDeployment Deployment,
        CostAlertSetting Setting,
        decimal Threshold,
        decimal Accrued);
}

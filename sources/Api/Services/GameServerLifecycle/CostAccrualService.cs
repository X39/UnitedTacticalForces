using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Selects the session of a game server and calculates its accrued provider cost from durable ledger slices.
/// </summary>
/// <param name="dbContext">The scoped database context the deployments and ledger slices are read from.</param>
/// <param name="authorizationService">The rules that decide who may view a server and a cost owner.</param>
/// <param name="runtimeProviders">
/// The registered runtime providers, whose declared billing units price the accrued cost by ledger provider identifier.
/// </param>
[Scoped<CostAccrualService, ICostAccrualService>]
internal sealed class CostAccrualService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    IEnumerable<IRuntimeProvider> runtimeProviders) : ICostAccrualService
{
    /// <summary>
    /// The declared billing unit of every registered provider by provider identifier; the first registration wins.
    /// </summary>
    private readonly Dictionary<string, RuntimeBillingUnit?> _billingUnits = BuildBillingUnits(runtimeProviders);

    /// <inheritdoc />
    public async Task<GameServerAccruedCostDto> GetForServerAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.View)
            || !await dbContext.GameServers
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingNotFoundException("The game server was not found.");

        var deployments = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId)
            .OrderByDescending(q => q.TimeStampCreated)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var slicesByDeployment = await LoadSlicesAsync(
                deployments.Select(q => q.PrimaryKey).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);

        // The latest session is the newest deployment that may still cost or has been charged. A never-charged
        // latest attempt therefore does not hide the total of the previous session.
        var session = deployments.FirstOrDefault(q => q.PossibleCost || slicesByDeployment.ContainsKey(q.PrimaryKey));
        if (session is null)
            return new GameServerAccruedCostDto { GameServerId = gameServerId, Session = null };

        var slices = slicesByDeployment.TryGetValue(session.PrimaryKey, out var found)
            ? found
            : new DeploymentSlices([], null);
        var result = CostAccrualCalculator.Calculate(slices.Slices, session, ResolveBillingUnit(slices), now);
        if (!await authorizationService
                .CanViewCostOwnerAsync(principal, session.CostOwnerFk, cancellationToken)
                .ConfigureAwait(false))
            result = result with { Amount = null, UnavailableReason = AccruedCostUnavailableReasons.Hidden };

        return new GameServerAccruedCostDto
        {
            GameServerId = gameServerId,
            Session = new AccruedCostDto
            {
                DeploymentId = session.PrimaryKey,
                Amount = result.Amount,
                AsOf = result.AsOf,
                IsFinal = result.IsFinal,
                UnavailableReason = result.UnavailableReason,
                BillingUnitSeconds = BillingUnitSecondsOf(result),
                BilledThrough = result.Amount is null ? null : result.BilledThrough,
            },
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, AccruedCostResult>> CalculateForDeploymentsAsync(
        IReadOnlyCollection<GameServerDeployment> deployments,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        if (deployments.Count == 0)
            return new Dictionary<Guid, AccruedCostResult>();

        var slicesByDeployment = await LoadSlicesAsync(
                deployments.Select(q => q.PrimaryKey).Distinct().ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var results = new Dictionary<Guid, AccruedCostResult>();
        foreach (var deployment in deployments)
        {
            if (results.ContainsKey(deployment.PrimaryKey))
                continue;
            var slices = slicesByDeployment.TryGetValue(deployment.PrimaryKey, out var found)
                ? found
                : new DeploymentSlices([], null);
            results[deployment.PrimaryKey] = CostAccrualCalculator.Calculate(
                slices.Slices,
                deployment,
                ResolveBillingUnit(slices),
                now);
        }

        return results;
    }

    /// <summary>
    /// Loads the charged provider-cost slices of the given deployments with one query and groups them in memory.
    /// </summary>
    /// <param name="deploymentIds">The ids of the deployments whose slices are loaded.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>
    /// A task resolving to the slices and the ledger provider identifier per deployment id; deployments without a
    /// charged slice have no entry. The amounts are summed in memory because the test provider cannot aggregate
    /// decimals in SQL.
    /// </returns>
    private async Task<Dictionary<Guid, DeploymentSlices>> LoadSlicesAsync(
        Guid[] deploymentIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.HostingCostLedgerEntries
            .AsNoTracking()
            .Where(q => q.Kind == EHostingLedgerEntryKind.ProviderCostSlice
                        && q.SliceStartsAt != null
                        && q.SliceEndsAt != null
                        && q.DeploymentFk != null
                        && deploymentIds.Contains(q.DeploymentFk.Value))
            .Select(q => new
            {
                DeploymentId = q.DeploymentFk!.Value,
                StartsAt = q.SliceStartsAt!.Value,
                EndsAt = q.SliceEndsAt!.Value,
                q.Amount,
                q.EstimatedHourlyRate,
                q.ProviderIdentifier,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .GroupBy(q => q.DeploymentId)
            .ToDictionary(
                q => q.Key,
                q => new DeploymentSlices(
                    q.Select(row => new AccruedCostSlice(
                            row.StartsAt,
                            row.EndsAt,
                            -row.Amount,
                            row.EstimatedHourlyRate))
                        .ToList(),
                    q.Select(row => row.ProviderIdentifier).Distinct(StringComparer.Ordinal).ToList()));
    }

    /// <summary>
    /// Returns the billing unit length that is disclosed next to an accrued amount.
    /// </summary>
    /// <param name="result">The accrued cost result, already nulled for viewers who may not see the amount.</param>
    /// <returns>
    /// The whole seconds of the unit the amount was rounded to, rounded down, or <see langword="null"/> when the
    /// amount is unavailable or no unit was applied.
    /// </returns>
    private static int? BillingUnitSecondsOf(AccruedCostResult result)
        => result.Amount is null || result.BillingUnit is null
            ? null
            : (int) Math.Min(int.MaxValue, Math.Floor(result.BillingUnit.Length.TotalSeconds));

    /// <summary>
    /// Resolves the billing unit that prices a deployment's slices.
    /// </summary>
    /// <param name="slices">The charged slices of the deployment with their ledger provider identifiers.</param>
    /// <returns>
    /// The unit declared by the registered provider that billed every slice, or <see langword="null"/> when the
    /// slices carry no, several or an unregistered provider identifier, or the provider declares no unit.
    /// </returns>
    private RuntimeBillingUnit? ResolveBillingUnit(DeploymentSlices slices)
        => slices.ProviderIdentifiers is [{ } identifier]
           && _billingUnits.TryGetValue(identifier, out var unit)
            ? unit
            : null;

    /// <summary>
    /// Builds the map from provider identifier to declared billing unit.
    /// </summary>
    /// <param name="providers">The registered runtime providers.</param>
    /// <returns>An ordinal map in which the first registration of an identifier wins.</returns>
    private static Dictionary<string, RuntimeBillingUnit?> BuildBillingUnits(IEnumerable<IRuntimeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var units = new Dictionary<string, RuntimeBillingUnit?>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            units.TryAdd(provider.ProviderIdentifier, provider.Capabilities.BillingUnit);
        }

        return units;
    }

    /// <summary>
    /// The charged slices of one deployment together with the distinct ledger provider identifiers that billed them.
    /// </summary>
    /// <param name="Slices">The charged slices.</param>
    /// <param name="ProviderIdentifiers">
    /// The distinct ledger provider identifiers of the slices, which may contain <see langword="null"/>, or
    /// <see langword="null"/> when there are no slices.
    /// </param>
    private sealed record DeploymentSlices(List<AccruedCostSlice> Slices, List<string?>? ProviderIdentifiers);
}

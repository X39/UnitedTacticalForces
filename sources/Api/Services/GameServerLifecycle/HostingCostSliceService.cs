using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Reserves affordability before provider mutation and atomically charges estimated provider-cost slices once cost is
/// possible. A reservation affects spendable headroom but never changes the financial balance.
/// </summary>
[Scoped<HostingCostSliceService, IHostingCostSliceService>]
internal sealed class HostingCostSliceService(
    DatabaseContext dbContext,
    IHostingAuditWriter auditWriter,
    IOptions<HostingFinanceOptions> options) : IHostingCostSliceService
{
    private readonly HostingFinanceOptions _options = options.Value;

    public async Task ReserveInitialSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var locked = await BeginLockedDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        await using var transaction = locked.Transaction;
        var (deployment, owner) = (locked.Deployment, locked.Owner);
        EnsureDeploymentCanBeAccounted(deployment, owner);
        if (owner.IsFrozen)
            throw new HostingAffordabilityException("The deployment cost owner is frozen.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (deployment.CostAuthorizationExpiresAt is { } alreadyCharged
            && alreadyCharged > now + Duration.FromTimeSpan(_options.ReconciliationInterval))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        if (deployment.ReservedProviderCostEur.HasValue)
        {
            EnsureCompleteReservation(deployment);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var rate = _options.GetHourlyRate(providerIdentifier);
        var sliceStartsAt = now;
        var sliceEndsAt = now + Duration.FromTimeSpan(_options.ReservationSlice);
        var amount = CalculateAmount(rate, sliceStartsAt, sliceEndsAt);
        var otherReservations = await GetOtherReservationTotalAsync(
                owner.PrimaryKey,
                deployment.PrimaryKey,
                cancellationToken)
            .ConfigureAwait(false);
        var availableAfter = owner.Balance + (owner.Allowance ?? 0m) - otherReservations - amount;
        if (availableAfter < _options.MinimumSafetyRangeEur)
        {
            throw new HostingAffordabilityException(
                "The cost owner cannot reserve the next provider-cost slice while preserving the required safety range.");
        }

        deployment.CostReservationStartsAt = sliceStartsAt;
        deployment.CostReservationExpiresAt = sliceEndsAt;
        deployment.ReservedProviderCostEur = amount;
        deployment.EstimatedHourlyCostEur = rate;
        deployment.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ProviderCostSliceReserved,
            "Initial provider-cost affordability reserved without changing the financial balance.",
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: owner.PrimaryKey,
            details: $"Provider={providerIdentifier}; AmountEur={amount:F4}; Through={sliceEndsAt}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitInitialReservationAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var locked = await BeginLockedDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        await using var transaction = locked.Transaction;
        var (deployment, owner) = (locked.Deployment, locked.Owner);
        EnsureDeploymentCanBeAccounted(deployment, owner);
        if (!deployment.ReservedProviderCostEur.HasValue)
        {
            if (deployment.CostAuthorizationExpiresAt.HasValue)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            // Recovery from an older/incomplete control-plane attempt must never leave an observed billable resource
            // unaccounted merely because its pre-create reservation record is missing.
            var rate = _options.GetHourlyRate(providerIdentifier);
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            var sliceEndsAt = now + Duration.FromTimeSpan(_options.ReservationSlice);
            ChargeSlice(
                deployment,
                owner,
                providerIdentifier,
                rate,
                now,
                sliceEndsAt,
                CalculateAmount(rate, now, sliceEndsAt));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var reservedRate = GetPersistedReservationRate(deployment);
        CommitReservation(deployment, owner, providerIdentifier, reservedRate);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseInitialReservationAsync(
        Guid deploymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var locked = await BeginLockedDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        await using var transaction = locked.Transaction;
        var (deployment, owner) = (locked.Deployment, locked.Owner);
        if (!deployment.ReservedProviderCostEur.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        EnsureCompleteReservation(deployment);

        var releasedAmount = deployment.ReservedProviderCostEur.Value;
        ClearReservation(deployment);
        deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ProviderCostSliceReservationReleased,
            "Unused provider-cost affordability reservation released without a financial charge.",
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: owner.PrimaryKey,
            details: $"AmountEur={releasedAmount:F4}; Reason={HostingFailureSanitizer.Sanitize(reason, 512)}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AuthorizeNextSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        bool enforceAffordability,
        CancellationToken cancellationToken = default)
    {
        var locked = await BeginLockedDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        await using var transaction = locked.Transaction;
        var (deployment, owner) = (locked.Deployment, locked.Owner);
        EnsureDeploymentCanBeAccounted(deployment, owner);

        if (deployment.ReservedProviderCostEur.HasValue)
        {
            // Once provider cost is possible, the already-authorized reservation becomes consumption even when cleanup
            // is underway. Frozen/credit state cannot erase cost that has already begun.
            CommitReservation(deployment, owner, providerIdentifier, GetPersistedReservationRate(deployment));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        if (owner.IsFrozen && enforceAffordability)
            throw new HostingAffordabilityException("The deployment cost owner is frozen.");

        var rate = _options.GetHourlyRate(providerIdentifier);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (deployment.CostAuthorizationExpiresAt is { } alreadyAuthorized
            && alreadyAuthorized > now + Duration.FromTimeSpan(_options.ReconciliationInterval))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var sliceStartsAt = deployment.CostAuthorizationExpiresAt is { } priorEnd && priorEnd < now
            ? priorEnd
            : deployment.CostAuthorizationExpiresAt ?? now;
        var ordinarySliceEnd = sliceStartsAt + Duration.FromTimeSpan(_options.ReservationSlice);
        var sliceEndsAt = ordinarySliceEnd > now + Duration.FromTimeSpan(_options.ReservationSlice)
            ? ordinarySliceEnd
            : now + Duration.FromTimeSpan(_options.ReservationSlice);
        var amount = CalculateAmount(rate, sliceStartsAt, sliceEndsAt);
        var otherReservations = await GetOtherReservationTotalAsync(
                owner.PrimaryKey,
                deployment.PrimaryKey,
                cancellationToken)
            .ConfigureAwait(false);
        var availableAfter = owner.Balance + (owner.Allowance ?? 0m) - otherReservations - amount;
        if (enforceAffordability && availableAfter < _options.MinimumSafetyRangeEur)
        {
            throw new HostingAffordabilityException(
                "The cost owner cannot fund the next provider-cost slice while preserving the required safety range.");
        }

        ChargeSlice(deployment, owner, providerIdentifier, rate, sliceStartsAt, sliceEndsAt, amount);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<LockedDeployment> BeginLockedDeploymentAsync(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database
            .BeginTransactionAsync(IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var ownerId = await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.PrimaryKey == deploymentId)
                .Select(q => (Guid?)q.CostOwnerFk)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                          ?? throw new HostingAffordabilityException("The deployment has no valid cost owner.");
            if (IsPostgreSql)
            {
                await dbContext.CostOwners
                    .FromSqlInterpolated($"""SELECT * FROM "CostOwners" WHERE "PrimaryKey" = {ownerId} FOR UPDATE""")
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
                await dbContext.GameServerDeployments
                    .FromSqlInterpolated(
                        $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {deploymentId} FOR UPDATE""")
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var deployment = await dbContext.GameServerDeployments
                .SingleAsync(q => q.PrimaryKey == deploymentId, cancellationToken)
                .ConfigureAwait(false);
            var owner = await dbContext.CostOwners
                .SingleAsync(q => q.PrimaryKey == ownerId, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(owner).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return new LockedDeployment(transaction, deployment, owner);
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<decimal> GetOtherReservationTotalAsync(
        Guid costOwnerId,
        Guid excludedDeploymentId,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.CostOwnerFk == costOwnerId
                        && q.PrimaryKey != excludedDeploymentId
                        && q.TimeStampResolved == null
                        && q.ReservedProviderCostEur != null)
            .Select(q => q.ReservedProviderCostEur!.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return reservations.Sum();
    }

    private void CommitReservation(
        GameServerDeployment deployment,
        CostOwner owner,
        string providerIdentifier,
        decimal rate)
    {
        EnsureCompleteReservation(deployment);
        var sliceStartsAt = deployment.CostReservationStartsAt!.Value;
        var sliceEndsAt = deployment.CostReservationExpiresAt!.Value;
        var amount = deployment.ReservedProviderCostEur!.Value;
        ChargeSlice(deployment, owner, providerIdentifier, rate, sliceStartsAt, sliceEndsAt, amount);
        ClearReservation(deployment);
    }

    private void ChargeSlice(
        GameServerDeployment deployment,
        CostOwner owner,
        string providerIdentifier,
        decimal rate,
        Instant sliceStartsAt,
        Instant sliceEndsAt,
        decimal amount)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        owner.Balance -= amount;
        owner.TimeStampUpdated = now;
        deployment.EstimatedHourlyCostEur = rate;
        deployment.CostAuthorizationExpiresAt = sliceEndsAt;
        deployment.TimeStampUpdated = now;
        dbContext.HostingCostLedgerEntries.Add(new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = owner.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            Kind = EHostingLedgerEntryKind.ProviderCostSlice,
            Amount = -amount,
            BalanceAfter = owner.Balance,
            EstimatedHourlyRate = rate,
            Currency = "EUR",
            ProviderIdentifier = providerIdentifier,
            SliceStartsAt = sliceStartsAt,
            SliceEndsAt = sliceEndsAt,
            TimeStampEffective = sliceStartsAt,
            TimeStampCreated = now,
        });
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ProviderCostSliceCharged,
            "Provider-cost slice charged to the deployment cost owner.",
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: owner.PrimaryKey,
            details: $"Provider={providerIdentifier}; AmountEur={amount:F4}; Through={sliceEndsAt}",
            costImpact: ECostImpact.KeepsCostRunning);
    }

    private static void ClearReservation(GameServerDeployment deployment)
    {
        deployment.CostReservationStartsAt = null;
        deployment.CostReservationExpiresAt = null;
        deployment.ReservedProviderCostEur = null;
    }

    private static void EnsureCompleteReservation(GameServerDeployment deployment)
    {
        if (!deployment.ReservedProviderCostEur.HasValue
            || !deployment.CostReservationStartsAt.HasValue
            || !deployment.CostReservationExpiresAt.HasValue)
        {
            throw new InvalidOperationException("The deployment has an incomplete provider-cost reservation.");
        }
    }

    private static decimal GetPersistedReservationRate(GameServerDeployment deployment)
        => deployment.EstimatedHourlyCostEur is > 0m and { } rate
            ? rate
            : throw new InvalidOperationException("The deployment reservation has no valid persisted hourly rate.");

    private static void EnsureDeploymentCanBeAccounted(GameServerDeployment deployment, CostOwner owner)
    {
        if (deployment.TimeStampResolved is not null)
            throw new HostingAffordabilityException("The deployment is already resolved.");
        if (!string.Equals(owner.Currency, "EUR", StringComparison.OrdinalIgnoreCase))
            throw new HostingAffordabilityException("Provider cost authorization currently requires an EUR cost owner.");
    }

    private static decimal CalculateAmount(decimal rate, Instant sliceStartsAt, Instant sliceEndsAt)
    {
        var hours = (decimal)(sliceEndsAt - sliceStartsAt).TotalHours;
        var amount = decimal.Round(rate * hours, 4, MidpointRounding.AwayFromZero);
        return amount <= 0 ? 0.0001m : amount;
    }

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record LockedDeployment(
        IDbContextTransaction Transaction,
        GameServerDeployment Deployment,
        CostOwner Owner);
}

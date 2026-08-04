using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<HostingFinancePolicyService, IHostingFinancePolicyService>]
internal sealed class HostingFinancePolicyService(
    DatabaseContext dbContext,
    IHostingAuditWriter auditWriter,
    IOptions<HostingFinanceOptions> configuredOptions) : IHostingFinancePolicyService
{
    public async Task<EffectiveHostingFinancePolicy> GetAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await dbContext.HostingFinancePolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == HostingFinancePolicy.SingletonKey, cancellationToken)
            .ConfigureAwait(false);
        return persisted is null
            ? FromConfiguration(configuredOptions.Value)
            : FromEntity(persisted);
    }

    public async Task<EffectiveHostingFinancePolicy> UpdateAsync(
        decimal minimumSafetyRangeEur,
        int reservationSliceMinutes,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (minimumSafetyRangeEur is < 0 or > 99_999_999_999_999.9999m)
            throw new HostingValidationException("The minimum safety range must be a finite non-negative EUR amount.");
        if (reservationSliceMinutes is < 1 or > 1440)
            throw new HostingValidationException("The reservation slice must be between 1 and 1440 minutes.");
        if (TimeSpan.FromMinutes(reservationSliceMinutes) <= configuredOptions.Value.ReconciliationInterval)
        {
            throw new HostingValidationException(
                "The reservation slice must be longer than the configured cost-reconciliation interval.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var policy = await dbContext.HostingFinancePolicies
            .SingleOrDefaultAsync(q => q.PrimaryKey == HostingFinancePolicy.SingletonKey, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            policy = new HostingFinancePolicy { PrimaryKey = HostingFinancePolicy.SingletonKey };
            dbContext.HostingFinancePolicies.Add(policy);
        }

        policy.MinimumSafetyRangeEur = minimumSafetyRangeEur;
        policy.ReservationSliceMinutes = reservationSliceMinutes;
        policy.TimeStampUpdated = now;
        policy.UpdatedByFk = actorId;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.HostingFinancePolicyChanged,
            "The installation-wide hosting affordability policy was changed.",
            actorId: actorId,
            details: $"MinimumSafetyRangeEur={minimumSafetyRangeEur:F4}; ReservationSliceMinutes={reservationSliceMinutes}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return FromEntity(policy);
    }

    private static EffectiveHostingFinancePolicy FromEntity(HostingFinancePolicy policy)
        => new(
            policy.MinimumSafetyRangeEur,
            TimeSpan.FromMinutes(policy.ReservationSliceMinutes),
            policy.TimeStampUpdated,
            policy.UpdatedByFk);

    private static EffectiveHostingFinancePolicy FromConfiguration(HostingFinanceOptions options)
    {
        options.Validate();
        return new(options.MinimumSafetyRangeEur, options.ReservationSlice, null, null);
    }
}

using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Calculates the provider cost a deployment has accrued from its charged ledger slices.
/// </summary>
/// <remarks>
/// Slices are charged in advance: a 15-minute reservation is charged before it elapses and renewed before it
/// expires, so the charged amounts alone are not the cost accrued so far. A provider bills in its own unit, for
/// example every started hour, so the session's billable time, counted from its first charged slice, is rounded up
/// as a whole to the provider's <see cref="RuntimeBillingUnit"/>. Rounding every reservation slice separately would
/// bill the unit once per slice. The charged slice amounts price the part of the billed time they cover, each
/// prorated by the fraction of its span that lies before the billed-through instant, and the newest slice's hourly
/// rate prices any billed time beyond it, so a session that may still cost keeps advancing even when no newer slice
/// was charged. The result is restart-safe because it is a pure function of durable ledger rows. Once cost has
/// stopped the as-of instant freezes at the resolution instant, so the billed time and the final total no longer
/// change. The unit comes only from the provider's declared capabilities; a missing unit makes the amount
/// unavailable instead of a silently lower prorated value. Correction and reversal ledger entries are deliberately
/// excluded: the accrued amount is provider-cost slices only.
/// </remarks>
internal static class CostAccrualCalculator
{
    /// <summary>
    /// The number of decimal places the accrued amount is rounded to, matching the charged slice amounts.
    /// </summary>
    private const int AmountDecimals = 4;

    /// <summary>
    /// Resolves the instant the accrued amount is valid for and whether it can still advance.
    /// </summary>
    /// <param name="deployment">The deployment whose resolution state is evaluated.</param>
    /// <param name="now">The current instant, used while the deployment may still cost.</param>
    /// <returns>
    /// The resolution instant with <see langword="true"/> when the deployment has a resolution instant and can no
    /// longer cost; otherwise <paramref name="now"/> with <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="deployment"/> is <see langword="null"/>.</exception>
    public static (Instant AsOf, bool IsFinal) ResolveAsOf(GameServerDeployment deployment, Instant now)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        return deployment is { TimeStampResolved: { } resolved, PossibleCost: false }
            ? (resolved, true)
            : (now, false);
    }

    /// <summary>
    /// Calculates the accrued cost of a deployment from its charged slices, rounded up to the billing unit.
    /// </summary>
    /// <param name="slices">The charged provider-cost slices of the deployment.</param>
    /// <param name="deployment">The deployment the slices belong to.</param>
    /// <param name="billingUnit">
    /// The billing unit the provider that charged the slices declares, or <see langword="null"/> when none is known.
    /// </param>
    /// <param name="now">The current instant, used while the deployment may still cost.</param>
    /// <returns>
    /// The amount rounded to four decimals, or an unavailable result: reason
    /// <see cref="AccruedCostUnavailableReasons.Rate"/> or <see cref="AccruedCostUnavailableReasons.NotYetCharged"/>
    /// when the deployment may cost but has no slice yet, and <see cref="AccruedCostUnavailableReasons.BillingUnit"/>
    /// when slices exist but <paramref name="billingUnit"/> is <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="slices"/> or <paramref name="deployment"/> is <see langword="null"/>.
    /// </exception>
    public static AccruedCostResult Calculate(
        IReadOnlyCollection<AccruedCostSlice> slices,
        GameServerDeployment deployment,
        RuntimeBillingUnit? billingUnit,
        Instant now)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(deployment);
        var (asOf, isFinal) = ResolveAsOf(deployment, now);
        if (slices.Count == 0)
        {
            if (!deployment.PossibleCost)
                return new AccruedCostResult(0m, asOf, isFinal, null, null, billingUnit);
            var reason = deployment.EstimatedHourlyCostEur is not > 0m
                ? AccruedCostUnavailableReasons.Rate
                : AccruedCostUnavailableReasons.NotYetCharged;
            return new AccruedCostResult(null, asOf, isFinal, reason);
        }

        if (billingUnit is null)
            return new AccruedCostResult(null, asOf, isFinal, AccruedCostUnavailableReasons.BillingUnit);

        var anchor = slices.Min(q => q.StartsAt);
        var billedThrough = billingUnit.BilledThrough(anchor, asOf);
        var total = 0m;
        var latest = slices.MaxBy(q => q.EndsAt);
        foreach (var slice in slices)
        {
            total += slice.Amount * ElapsedFraction(slice, billedThrough);
        }

        if (billedThrough > latest.EndsAt)
            total += HourlyRateOf(latest) * (decimal) (billedThrough - latest.EndsAt).TotalHours;

        return new AccruedCostResult(
            decimal.Round(total, AmountDecimals, MidpointRounding.AwayFromZero),
            asOf,
            isFinal,
            null,
            billedThrough,
            billingUnit);
    }

    /// <summary>
    /// Returns the hourly rate a slice was charged at.
    /// </summary>
    /// <param name="slice">The slice whose rate is resolved.</param>
    /// <returns>
    /// The recorded hourly rate, otherwise the slice amount divided by its span in hours, or zero for an empty span.
    /// </returns>
    private static decimal HourlyRateOf(AccruedCostSlice slice)
    {
        if (slice.HourlyRate is { } rate)
            return rate;
        var spanHours = (decimal) (slice.EndsAt - slice.StartsAt).TotalHours;
        return spanHours > 0m ? slice.Amount / spanHours : 0m;
    }

    /// <summary>
    /// Returns the fraction of a slice's span that has elapsed at the as-of instant, clamped to zero through one.
    /// </summary>
    /// <param name="slice">The slice whose span is evaluated.</param>
    /// <param name="asOf">The instant the elapsed fraction is measured at.</param>
    /// <returns>A decimal ratio between zero and one.</returns>
    private static decimal ElapsedFraction(AccruedCostSlice slice, Instant asOf)
    {
        var spanSeconds = (decimal) (slice.EndsAt - slice.StartsAt).TotalSeconds;
        if (spanSeconds <= 0m)
            return asOf >= slice.EndsAt ? 1m : 0m;
        var elapsedSeconds = (decimal) (Instant.Min(asOf, slice.EndsAt) - slice.StartsAt).TotalSeconds;
        return Math.Clamp(elapsedSeconds / spanSeconds, 0m, 1m);
    }
}

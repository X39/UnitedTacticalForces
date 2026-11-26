using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Hosting;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Pins the accrued cost billing unit rule of <see cref="CostAccrualCalculator" />: the session's billable time
/// is rounded up to the provider's billing unit, the charged slices price the covered part, the newest slice's
/// rate prices the rest, and the as-of instant freezes at resolution once cost has stopped. The tests use
/// in-memory deployments and slices and need no database.
/// </summary>
public sealed class CostAccrualCalculatorTests
{
    /// <summary>
    /// The instant every slice of these tests is measured from.
    /// </summary>
    private static readonly Instant T = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A per-started-second unit, under which billed time equals elapsed time for whole-second instants.
    /// </summary>
    private static readonly RuntimeBillingUnit PerSecond = RuntimeBillingUnit.PerStarted(Duration.FromSeconds(1));

    /// <summary>
    /// Two consecutive 15-minute slices are summed after prorating the second by its elapsed fraction.
    /// </summary>
    [Fact]
    public void ConsecutiveSlices_ProrateTheSliceInProgress()
    {
        var slices = new[]
        {
            Slice(0, 15, 0.0250m),
            Slice(15, 30, 0.0250m),
        };

        var result = CostAccrualCalculator.Calculate(slices, Costing(), PerSecond, T + Duration.FromMinutes(20));

        Assert.Equal(0.0333m, result.Amount);
        Assert.False(result.IsFinal);
        Assert.Null(result.UnavailableReason);
    }

    /// <summary>
    /// A catch-up slice that started in the past is prorated over its own span, not over 15 minutes.
    /// </summary>
    [Fact]
    public void CatchUpSlice_IsProratedOverItsOwnSpan()
    {
        var slices = new[] { Slice(-30, 15, 0.0900m) };

        var result = CostAccrualCalculator.Calculate(slices, Costing(), PerSecond, T);

        // 30 of 45 minutes elapsed: two thirds of 0.0900.
        Assert.Equal(0.0600m, result.Amount);
    }

    /// <summary>
    /// A slice that has not started contributes nothing, and once it has ended a session that may still cost
    /// keeps advancing at the slice's hourly rate even though no newer slice was charged.
    /// </summary>
    [Fact]
    public void SliceBeforeStartAndAfterEnd_ContributeNothingAndKeepAdvancingAtTheSliceRate()
    {
        var slices = new[] { Slice(0, 15, 0.0250m) };

        var before = CostAccrualCalculator.Calculate(slices, Costing(), PerSecond, T - Duration.FromMinutes(5));
        var after = CostAccrualCalculator.Calculate(slices, Costing(), PerSecond, T + Duration.FromHours(2));

        Assert.Equal(0m, before.Amount);
        // 0.0250 for the slice plus 1.75 h past its end at 0.10 EUR/h.
        Assert.Equal(0.2000m, after.Amount);
    }

    /// <summary>
    /// A deployment resolved mid-slice returns the same final amount at the resolution instant and a day later.
    /// </summary>
    [Fact]
    public void ResolvedMidSlice_StopsAdvancingAndIsFinal()
    {
        var slices = new[] { Slice(0, 15, 0.0250m) };
        var resolvedAt = T + Duration.FromMinutes(9);
        var deployment = Resolved(resolvedAt);

        var atResolution = CostAccrualCalculator.Calculate(slices, deployment, PerSecond, resolvedAt);
        var dayLater = CostAccrualCalculator.Calculate(slices, deployment, PerSecond, resolvedAt + Duration.FromDays(1));

        Assert.Equal(0.0150m, atResolution.Amount);
        Assert.Equal(atResolution.Amount, dayLater.Amount);
        Assert.True(atResolution.IsFinal);
        Assert.True(dayLater.IsFinal);
        Assert.Equal(resolvedAt, dayLater.AsOf);
    }

    /// <summary>
    /// A deployment with a resolution instant that still reports possible cost is not final and measures at now.
    /// </summary>
    [Fact]
    public void ResolvedButStillPossibleCost_IsNotFinalAndUsesNow()
    {
        var now = T + Duration.FromMinutes(12);
        var deployment = Costing();
        deployment.TimeStampResolved = T + Duration.FromMinutes(9);

        var (asOf, isFinal) = CostAccrualCalculator.ResolveAsOf(deployment, now);
        var result = CostAccrualCalculator.Calculate([Slice(0, 15, 0.0250m)], deployment, PerSecond, now);

        Assert.Equal(now, asOf);
        Assert.False(isFinal);
        Assert.False(result.IsFinal);
        Assert.Equal(0.0200m, result.Amount);
    }

    /// <summary>
    /// A deployment that may cost reports the as-of instant of now until it is resolved.
    /// </summary>
    [Fact]
    public void UnresolvedDeployment_ResolvesAsOfToNow()
    {
        var now = T + Duration.FromMinutes(3);

        var (asOf, isFinal) = CostAccrualCalculator.ResolveAsOf(Costing(), now);

        Assert.Equal(now, asOf);
        Assert.False(isFinal);
    }

    /// <summary>
    /// A sub-cent slice at 30 percent elapsed rounds to four decimals, and a midpoint rounds away from zero.
    /// </summary>
    [Fact]
    public void Rounding_UsesFourDecimalsAwayFromZero()
    {
        var tiny = CostAccrualCalculator.Calculate(
            [Slice(0, 10, 0.0001m)], Costing(), PerSecond, T + Duration.FromMinutes(3));
        var midpoint = CostAccrualCalculator.Calculate(
            [Slice(0, 10, 0.0005m)], Costing(), PerSecond, T + Duration.FromMinutes(5));

        Assert.Equal(0.0000m, tiny.Amount);
        Assert.Equal(0.0003m, midpoint.Amount);
    }

    /// <summary>
    /// A settled deployment without any slice yields zero at its resolution instant; the calculator invents no
    /// reason, because session selection already excludes sessions that never cost.
    /// </summary>
    [Fact]
    public void SettledWithoutSlices_ReturnsZeroAtResolution()
    {
        var resolvedAt = T + Duration.FromMinutes(9);

        var result = CostAccrualCalculator.Calculate(
            [], Resolved(resolvedAt), PerSecond, resolvedAt + Duration.FromHours(1));

        Assert.Equal(0m, result.Amount);
        Assert.True(result.IsFinal);
        Assert.Equal(resolvedAt, result.AsOf);
        Assert.Null(result.UnavailableReason);
    }

    /// <summary>
    /// A deployment that may cost without a slice is unavailable, reporting the rate reason when no positive hourly
    /// rate exists and not-yet-charged otherwise.
    /// </summary>
    /// <param name="hourlyRate">The estimated hourly cost of the deployment, or <see langword="null"/>.</param>
    /// <param name="expectedReason">The reason the result is expected to carry.</param>
    [Theory]
    [InlineData(null, AccruedCostUnavailableReasons.Rate)]
    [InlineData(0.0, AccruedCostUnavailableReasons.Rate)]
    [InlineData(0.1, AccruedCostUnavailableReasons.NotYetCharged)]
    public void PossibleCostWithoutSlices_IsUnavailableNeverZero(double? hourlyRate, string expectedReason)
    {
        var deployment = Costing();
        deployment.EstimatedHourlyCostEur = hourlyRate is null ? null : (decimal) hourlyRate;

        var result = CostAccrualCalculator.Calculate([], deployment, PerSecond, T);

        Assert.Null(result.Amount);
        Assert.Equal(expectedReason, result.UnavailableReason);
    }

    /// <summary>
    /// A Hetzner-style started-hour session that ran five minutes has cost a full hour: the amount is the hourly rate
    /// times one hour, the billed-through instant is one hour after the first slice and the unit is reported.
    /// </summary>
    [Fact]
    public void StartedHour_FiveMinuteSession_BillsAFullHour()
    {
        var result = CostAccrualCalculator.Calculate(
            [Slice(0, 15, 0.0250m)], Costing(), RuntimeBillingUnit.StartedHour, T + Duration.FromMinutes(5));

        Assert.Equal(0.1000m, result.Amount);
        Assert.Equal(T + Duration.FromHours(1), result.BilledThrough);
        Assert.Equal(RuntimeBillingUnit.StartedHour, result.BillingUnit);
        Assert.False(result.IsFinal);
        Assert.Null(result.UnavailableReason);
    }

    /// <summary>
    /// At exactly 60 minutes four slices cost one hour, and one second later the second hour has started.
    /// </summary>
    [Fact]
    public void StartedHour_HourBoundary_StepsFromOneToTwoHoursOneSecondLater()
    {
        var fourSlices = new[] { Slice(0, 15, 0.0250m), Slice(15, 30, 0.0250m), Slice(30, 45, 0.0250m), Slice(45, 60, 0.0250m) };
        var fiveSlices = fourSlices.Append(Slice(60, 75, 0.0250m)).ToArray();

        var atBoundary = CostAccrualCalculator.Calculate(
            fourSlices, Costing(), RuntimeBillingUnit.StartedHour, T + Duration.FromMinutes(60));
        var oneSecondLater = CostAccrualCalculator.Calculate(
            fiveSlices, Costing(), RuntimeBillingUnit.StartedHour, T + Duration.FromMinutes(60) + Duration.FromSeconds(1));

        Assert.Equal(0.1000m, atBoundary.Amount);
        Assert.Equal(0.2000m, oneSecondLater.Amount);
    }

    /// <summary>
    /// With several slices the hours are counted from the first slice, and the billed time beyond the last charged
    /// slice is priced at that slice's rate: 75 charged minutes plus 45 minutes.
    /// </summary>
    [Fact]
    public void StartedHour_SeveralSlices_CountHoursFromTheFirstSlice()
    {
        var slices = Enumerable.Range(0, 5).Select(i => Slice(i * 15, (i + 1) * 15, 0.0250m)).ToArray();

        var result = CostAccrualCalculator.Calculate(
            slices, Costing(), RuntimeBillingUnit.StartedHour, T + Duration.FromMinutes(61));

        Assert.Equal(0.2000m, result.Amount);
        Assert.Equal(T + Duration.FromHours(2), result.BilledThrough);
    }

    /// <summary>
    /// The billed time past the last slice is priced at the recorded hourly rate, and at the rate derived from the
    /// slice amount and span when the ledger entry recorded none.
    /// </summary>
    [Fact]
    public void StartedHour_ExtensionRate_UsesRecordedRateOrDerivesIt()
    {
        var now = T + Duration.FromMinutes(5);

        var recorded = CostAccrualCalculator.Calculate(
            [Slice(0, 15, 0.0034m, 0.0137m)], Costing(), RuntimeBillingUnit.StartedHour, now);
        var derived = CostAccrualCalculator.Calculate(
            [Slice(0, 15, 0.0034m)], Costing(), RuntimeBillingUnit.StartedHour, now);

        Assert.Equal(0.0137m, recorded.Amount);
        Assert.Equal(0.0136m, derived.Amount);
    }

    /// <summary>
    /// A catch-up slice that started 30 minutes before its charge still bills one full hour at the charge instant.
    /// </summary>
    [Fact]
    public void StartedHour_CatchUpSlice_RoundsThirtyStartedMinutesToOneHour()
    {
        var result = CostAccrualCalculator.Calculate(
            [Slice(-30, 15, 0.0750m)], Costing(), RuntimeBillingUnit.StartedHour, T);

        Assert.Equal(0.1000m, result.Amount);
    }

    /// <summary>
    /// A session resolved at minute 61 reports two hours at the resolution instant and the same a day later, and a
    /// session resolved before its first slice started reports zero.
    /// </summary>
    [Fact]
    public void StartedHour_ResolvedAtMinuteSixtyOne_FreezesAtTwoHours()
    {
        var slices = new[]
        {
            Slice(0, 15, 0.0250m), Slice(15, 30, 0.0250m), Slice(30, 45, 0.0250m),
            Slice(45, 60, 0.0250m), Slice(60, 75, 0.0250m),
        };
        var resolvedAt = T + Duration.FromMinutes(61);
        var deployment = Resolved(resolvedAt);

        var atResolution = CostAccrualCalculator.Calculate(slices, deployment, RuntimeBillingUnit.StartedHour, resolvedAt);
        var dayLater = CostAccrualCalculator.Calculate(
            slices, deployment, RuntimeBillingUnit.StartedHour, resolvedAt + Duration.FromDays(1));
        var beforeFirstSlice = CostAccrualCalculator.Calculate(
            slices,
            Resolved(T - Duration.FromMinutes(5)),
            RuntimeBillingUnit.StartedHour,
            T + Duration.FromDays(1));

        Assert.Equal(0.2000m, atResolution.Amount);
        Assert.Equal(0.2000m, dayLater.Amount);
        Assert.True(atResolution.IsFinal);
        Assert.True(dayLater.IsFinal);
        Assert.Equal(resolvedAt, dayLater.AsOf);
        Assert.Equal(0.0000m, beforeFirstSlice.Amount);
    }

    /// <summary>
    /// A session that may still cost keeps advancing at the last slice's rate when no newer slice was charged: at
    /// minute 130 the third hour has started.
    /// </summary>
    [Fact]
    public void StartedHour_StalledCharging_KeepsAdvancingAtTheLastSliceRate()
    {
        var result = CostAccrualCalculator.Calculate(
            [Slice(0, 15, 0.0250m)], Costing(), RuntimeBillingUnit.StartedHour, T + Duration.FromMinutes(130));

        Assert.Equal(0.3000m, result.Amount);
    }

    /// <summary>
    /// A provider stub with another unit is rounded to its own unit, which proves the rule is provider-neutral.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed seconds since the first slice.</param>
    /// <param name="expectedAmount">The amount a per-started-minute unit bills.</param>
    [Theory]
    [InlineData(90, 0.0033)]
    [InlineData(60, 0.0017)]
    public void PerStartedMinute_RoundsToItsOwnUnit(int elapsedSeconds, double expectedAmount)
    {
        var result = CostAccrualCalculator.Calculate(
            [Slice(0, 15, 0.0250m)],
            Costing(),
            RuntimeBillingUnit.PerStarted(Duration.FromMinutes(1)),
            T + Duration.FromSeconds(elapsedSeconds));

        Assert.Equal((decimal) expectedAmount, result.Amount);
    }

    /// <summary>
    /// Slices without a known billing unit make the amount unavailable with the billing-unit reason instead of a
    /// silently prorated, lower amount, while the no-slice reasons stay unchanged for any unit.
    /// </summary>
    [Fact]
    public void MissingBillingUnit_IsUnavailableWithBillingUnitReason()
    {
        var withSlices = CostAccrualCalculator.Calculate([Slice(0, 15, 0.0250m)], Costing(), null, T);
        var withoutSlices = CostAccrualCalculator.Calculate([], Costing(), null, T);
        var withoutRate = new GameServerDeployment { PossibleCost = true };
        var withoutSlicesAndRate = CostAccrualCalculator.Calculate(
            [], withoutRate, RuntimeBillingUnit.StartedHour, T);

        Assert.Null(withSlices.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.BillingUnit, withSlices.UnavailableReason);
        Assert.Equal(AccruedCostUnavailableReasons.NotYetCharged, withoutSlices.UnavailableReason);
        Assert.Equal(AccruedCostUnavailableReasons.Rate, withoutSlicesAndRate.UnavailableReason);
    }

    /// <summary>
    /// Creates a charged slice spanning minute offsets from <see cref="T" />.
    /// </summary>
    /// <param name="startMinutes">The start offset in minutes.</param>
    /// <param name="endMinutes">The end offset in minutes.</param>
    /// <param name="amount">The positive amount charged for the whole slice.</param>
    /// <returns>The slice.</returns>
    private static AccruedCostSlice Slice(int startMinutes, int endMinutes, decimal amount)
        => new(T + Duration.FromMinutes(startMinutes), T + Duration.FromMinutes(endMinutes), amount);

    /// <summary>
    /// Creates a charged slice with a recorded hourly rate spanning minute offsets from <see cref="T" />.
    /// </summary>
    /// <param name="startMinutes">The start offset in minutes.</param>
    /// <param name="endMinutes">The end offset in minutes.</param>
    /// <param name="amount">The positive amount charged for the whole slice.</param>
    /// <param name="hourlyRate">The hourly rate the slice was charged at.</param>
    /// <returns>The slice.</returns>
    private static AccruedCostSlice Slice(int startMinutes, int endMinutes, decimal amount, decimal hourlyRate)
        => new(T + Duration.FromMinutes(startMinutes), T + Duration.FromMinutes(endMinutes), amount, hourlyRate);

    /// <summary>
    /// Creates a deployment that may still cost and has an hourly rate.
    /// </summary>
    /// <returns>The deployment.</returns>
    private static GameServerDeployment Costing()
        => new() { PossibleCost = true, EstimatedHourlyCostEur = 0.10m };

    /// <summary>
    /// Creates a deployment whose cost has stopped at the given instant.
    /// </summary>
    /// <param name="resolvedAt">The resolution instant.</param>
    /// <returns>The deployment.</returns>
    private static GameServerDeployment Resolved(Instant resolvedAt)
        => new() { PossibleCost = false, TimeStampResolved = resolvedAt, EstimatedHourlyCostEur = 0.10m };
}

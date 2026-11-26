using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Pins the rounding rule of <see cref="RuntimeBillingUnit" />: every started unit is billed in full, at least one
/// unit once billing has started, and nothing before the anchor. The tests are pure and need no database.
/// </summary>
public sealed class RuntimeBillingUnitTests
{
    /// <summary>
    /// The instant billing starts at in these tests.
    /// </summary>
    private static readonly Instant Anchor = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A zero or negative unit length is rejected, so the rounding can never divide by zero.
    /// </summary>
    /// <param name="seconds">The invalid length in seconds.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3600)]
    public void PerStarted_RejectsZeroAndNegativeLength(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeBillingUnit.PerStarted(Duration.FromSeconds(seconds)));
    }

    /// <summary>
    /// The started-hour unit is one hour long.
    /// </summary>
    [Fact]
    public void StartedHour_IsOneHourLong()
    {
        Assert.Equal(Duration.FromHours(1), RuntimeBillingUnit.StartedHour.Length);
    }

    /// <summary>
    /// With the started-hour unit the billed time is the end of the last started hour: the first hour is billed from
    /// the very first instant, exactly 60 minutes is still one hour, and one second more starts the second hour.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed seconds since the anchor.</param>
    /// <param name="expectedHours">The billed hours after the anchor.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(300, 1)]
    [InlineData(3_600, 1)]
    [InlineData(3_601, 2)]
    [InlineData(7_200, 2)]
    [InlineData(7_201, 3)]
    public void StartedHour_BillsEveryStartedHourInFull(int elapsedSeconds, int expectedHours)
    {
        var billedThrough = RuntimeBillingUnit.StartedHour.BilledThrough(
            Anchor,
            Anchor + Duration.FromSeconds(elapsedSeconds));

        Assert.Equal(Anchor + Duration.FromHours(expectedHours), billedThrough);
    }

    /// <summary>
    /// An as-of instant before the anchor bills nothing and returns the anchor itself.
    /// </summary>
    [Fact]
    public void BilledThrough_BeforeTheAnchor_ReturnsTheAnchor()
    {
        var billedThrough = RuntimeBillingUnit.StartedHour.BilledThrough(Anchor, Anchor - Duration.FromSeconds(1));

        Assert.Equal(Anchor, billedThrough);
    }

    /// <summary>
    /// A per-started-minute unit rounds 90 seconds up to two minutes and keeps exact minutes unchanged.
    /// </summary>
    [Fact]
    public void PerStartedMinute_RoundsNinetySecondsUpToTwoMinutes()
    {
        var unit = RuntimeBillingUnit.PerStarted(Duration.FromMinutes(1));

        Assert.Equal(
            Anchor + Duration.FromMinutes(2),
            unit.BilledThrough(Anchor, Anchor + Duration.FromSeconds(90)));
        Assert.Equal(
            Anchor + Duration.FromMinutes(1),
            unit.BilledThrough(Anchor, Anchor + Duration.FromSeconds(60)));
    }

    /// <summary>
    /// A very long session does not overflow the nanosecond arithmetic.
    /// </summary>
    [Fact]
    public void StartedHour_HandlesYearsOfElapsedTime()
    {
        var asOf = Anchor + Duration.FromDays(3_650) + Duration.FromSeconds(1);

        var billedThrough = RuntimeBillingUnit.StartedHour.BilledThrough(Anchor, asOf);

        Assert.Equal(Anchor + Duration.FromDays(3_650) + Duration.FromHours(1), billedThrough);
    }
}

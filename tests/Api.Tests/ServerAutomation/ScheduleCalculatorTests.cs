using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers weekly schedule occurrences, including time zone transitions and half-open windows. Transition instants
/// are derived from the zone data, never hard-coded.
/// </summary>
public sealed class ScheduleCalculatorTests
{
    private const int EveryDay = 0b1111111;
    private static readonly DateTimeZone Berlin = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    /// <summary>Weekday lists convert to masks and back.</summary>
    [Fact]
    public void DaysMask_RoundTripsIsoDays()
    {
        Assert.Equal(21, ScheduleCalculator.ToDaysMask([1, 3, 5]));
        Assert.Equal([1, 3, 5], ScheduleCalculator.FromDaysMask(21));
        Assert.Equal(EveryDay, ScheduleCalculator.ToDaysMask([1, 2, 3, 4, 5, 6, 7]));
        Assert.Empty(ScheduleCalculator.FromDaysMask(0));
    }

    /// <summary>Days outside the ISO range are rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void ToDaysMask_OutOfRangeDay_Throws(int day)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScheduleCalculator.ToDaysMask([day]));
    }

    /// <summary>Only IANA ids resolve to a zone.</summary>
    [Fact]
    public void FindZone_ResolvesOnlyKnownIanaIds()
    {
        Assert.NotNull(ScheduleCalculator.FindZone("Europe/Berlin"));
        Assert.Null(ScheduleCalculator.FindZone("Mars/Base"));
        Assert.Null(ScheduleCalculator.FindZone(""));
        Assert.Null(ScheduleCalculator.FindZone(null));
    }

    /// <summary>A Monday-only rule yields exactly the Monday occurrence inside a one week window.</summary>
    [Fact]
    public void OccurrencesBetween_MondayOnlyRule_YieldsOneMondayInstant()
    {
        var from = Instant.FromUtc(2026, 11, 22, 12, 0); // Sunday
        var to = Instant.FromUtc(2026, 11, 29, 12, 0); // next Sunday

        var occurrences = ScheduleCalculator.OccurrencesBetween(1, 18 * 60, DateTimeZone.Utc, from, to).ToList();

        Assert.Equal([Instant.FromUtc(2026, 11, 23, 18, 0)], occurrences);
    }

    /// <summary>A daily rule occurs at the same local wall time in winter and in summer.</summary>
    [Theory]
    [InlineData(2026, 1, 5)]
    [InlineData(2026, 7, 6)]
    public void OccurrencesBetween_DailyRuleInBerlin_KeepsLocalWallTime(int year, int month, int day)
    {
        var from = Instant.FromUtc(year, month, day, 0, 0);
        var to = from + Duration.FromDays(7);

        var occurrences = ScheduleCalculator.OccurrencesBetween(EveryDay, 18 * 60, Berlin, from, to).ToList();

        Assert.Equal(7, occurrences.Count);
        Assert.All(occurrences, q => Assert.Equal(new LocalTime(18, 0), q.InZone(Berlin).TimeOfDay));
        Assert.Equal(occurrences.OrderBy(q => q).ToList(), occurrences);
    }

    /// <summary>The window excludes its start and includes its end.</summary>
    [Fact]
    public void OccurrencesBetween_WindowIsHalfOpen()
    {
        var first = Instant.FromUtc(2026, 11, 23, 18, 0);
        var second = Instant.FromUtc(2026, 11, 24, 18, 0);

        var occurrences = ScheduleCalculator.OccurrencesBetween(EveryDay, 18 * 60, DateTimeZone.Utc, first, second)
            .ToList();

        Assert.Equal([second], occurrences);
    }

    /// <summary>A degenerate or reversed window yields nothing.</summary>
    [Fact]
    public void OccurrencesBetween_EmptyWindowOrMask_YieldsNothing()
    {
        var at = Instant.FromUtc(2026, 11, 23, 18, 0);

        Assert.Empty(ScheduleCalculator.OccurrencesBetween(EveryDay, 18 * 60, DateTimeZone.Utc, at, at));
        Assert.Empty(ScheduleCalculator.OccurrencesBetween(0, 18 * 60, DateTimeZone.Utc, at, at + Duration.FromDays(3)));
    }

    /// <summary>A minute of day outside 0..1439 is rejected when the call is made.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1440)]
    public void OccurrencesBetween_InvalidMinuteOfDay_Throws(int minuteOfDay)
    {
        var at = Instant.FromUtc(2026, 11, 23, 18, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScheduleCalculator.OccurrencesBetween(EveryDay, minuteOfDay, DateTimeZone.Utc, at, at));
    }

    /// <summary>A wall time inside the spring-forward gap runs forward by the gap length, once.</summary>
    [Fact]
    public void OccurrencesBetween_SpringForwardGap_MovesForwardByGapOnce()
    {
        var (gapStart, gapDate) = FindTransition(2026, true);
        var before = Berlin.GetZoneInterval(gapStart - Duration.Epsilon);
        var after = Berlin.GetZoneInterval(gapStart);
        var gap = after.WallOffset - before.WallOffset;
        Assert.True(gap > Offset.Zero);

        var occurrences = OccurrencesOnDate(gapDate, 2 * 60 + 30);

        var occurrence = Assert.Single(occurrences);
        var expected = Berlin.AtStrictly(gapDate.At(new LocalTime(2, 30)).PlusSeconds(gap.Seconds)).ToInstant();
        Assert.Equal(expected, occurrence);
        Assert.Equal(new LocalTime(3, 30), occurrence.InZone(Berlin).TimeOfDay);
    }

    /// <summary>An ambiguous wall time in the fall-back overlap runs once, at the earlier instant.</summary>
    [Fact]
    public void OccurrencesBetween_FallBackOverlap_RunsOnceAtEarlierInstant()
    {
        var (_, overlapDate) = FindTransition(2026, false);
        var mapping = Berlin.MapLocal(overlapDate.At(new LocalTime(2, 30)));
        Assert.Equal(2, mapping.Count);

        var occurrences = OccurrencesOnDate(overlapDate, 2 * 60 + 30);

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(mapping.First().ToInstant(), occurrence);
        Assert.True(occurrence < mapping.Last().ToInstant());
    }

    /// <summary>The next occurrence is the first one strictly after the given instant.</summary>
    [Fact]
    public void NextAfter_ReturnsFirstOccurrenceStrictlyAfter()
    {
        var mondayAndWednesday = ScheduleCalculator.ToDaysMask([1, 3]);
        var monday = Instant.FromUtc(2026, 11, 23, 18, 0);

        Assert.Equal(
            Instant.FromUtc(2026, 11, 25, 18, 0),
            ScheduleCalculator.NextAfter(mondayAndWednesday, 18 * 60, DateTimeZone.Utc, monday));
        Assert.Equal(
            monday,
            ScheduleCalculator.NextAfter(
                mondayAndWednesday,
                18 * 60,
                DateTimeZone.Utc,
                monday - Duration.FromSeconds(1)));
    }

    /// <summary>The next occurrence crosses the week boundary.</summary>
    [Fact]
    public void NextAfter_CrossesWeekBoundary()
    {
        var wednesday = Instant.FromUtc(2026, 11, 25, 18, 0);

        var next = ScheduleCalculator.NextAfter(ScheduleCalculator.ToDaysMask([1, 3]), 18 * 60, DateTimeZone.Utc, wednesday);

        Assert.Equal(Instant.FromUtc(2026, 11, 30, 18, 0), next);
    }

    /// <summary>A rule that fires only once a week is found from any starting instant.</summary>
    [Fact]
    public void NextAfter_SingleDayRule_FoundWithinEightDays()
    {
        var monday = Instant.FromUtc(2026, 11, 23, 18, 0);

        var next = ScheduleCalculator.NextAfter(1, 18 * 60, DateTimeZone.Utc, monday);

        Assert.Equal(monday + Duration.FromDays(7), next);
    }

    /// <summary>An empty mask has no next occurrence.</summary>
    [Fact]
    public void NextAfter_EmptyMask_ReturnsNull()
    {
        Assert.Null(ScheduleCalculator.NextAfter(0, 18 * 60, DateTimeZone.Utc, Instant.FromUtc(2026, 11, 23, 18, 0)));
    }

    private static List<Instant> OccurrencesOnDate(LocalDate date, int minuteOfDay)
    {
        var from = Berlin.AtStartOfDay(date.PlusDays(-1)).ToInstant();
        var to = Berlin.AtStartOfDay(date.PlusDays(1)).ToInstant();
        return ScheduleCalculator.OccurrencesBetween(EveryDay, minuteOfDay, Berlin, from, to)
            .Where(q => q.InZone(Berlin).Date == date)
            .ToList();
    }

    private static (Instant Start, LocalDate Date) FindTransition(int year, bool springForward)
    {
        var window = new Interval(Instant.FromUtc(year, 1, 1, 0, 0), Instant.FromUtc(year + 1, 1, 1, 0, 0));
        var interval = Berlin.GetZoneIntervals(window)
            .Single(q => q.HasStart
                         && q.Start > window.Start
                         && (q.Savings != Offset.Zero) == springForward);
        return (interval.Start, interval.Start.InZone(Berlin).Date);
    }
}

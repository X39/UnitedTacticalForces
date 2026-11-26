using NodaTime.TimeZones;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Pure, zone-aware arithmetic for weekly schedule rules. It reads no clock; every function takes explicit instants.
/// </summary>
/// <remarks>
/// <para>
/// A rule is a set of ISO weekdays (stored as a bit mask, Monday = bit 0) and a minute of the day. Wall times are
/// resolved in the given IANA zone with NodaTime's lenient resolver: an ambiguous wall time (clocks go back) occurs
/// once, at the earlier of the two instants; a skipped wall time (clocks go forward) is moved forward by the length
/// of the gap, so 02:30 in a one hour gap occurs at 03:30.
/// </para>
/// <para>
/// Occurrences are produced for a half-open window <c>(from, to]</c> in chronological order, so a persisted
/// watermark scan that continues at the previous <c>to</c> never repeats or misses an occurrence. Tests derive
/// transition instants from the zone data instead of hard-coding dates.
/// </para>
/// </remarks>
public static class ScheduleCalculator
{
    private const int MinutesPerDay = 24 * 60;
    private const int AllDaysMask = 0b1111111;

    /// <summary>
    /// Converts ISO weekdays into a bit mask.
    /// </summary>
    /// <param name="isoDays">The ISO weekdays, Monday = 1 through Sunday = 7. Duplicates are ignored.</param>
    /// <returns>The mask with bit <c>1 &lt;&lt; (day - 1)</c> set for each day.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="isoDays" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A day is outside 1..7.</exception>
    public static int ToDaysMask(IEnumerable<int> isoDays)
    {
        ArgumentNullException.ThrowIfNull(isoDays);
        var mask = 0;
        foreach (var day in isoDays)
        {
            if (day is < 1 or > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(isoDays),
                    day,
                    "ISO weekdays are 1 (Monday) to 7 (Sunday).");
            }

            mask |= 1 << (day - 1);
        }

        return mask;
    }

    /// <summary>
    /// Converts a bit mask into ISO weekdays.
    /// </summary>
    /// <param name="daysMask">The weekday mask; bits above Sunday are ignored.</param>
    /// <returns>The ISO weekdays, Monday = 1 through Sunday = 7, in ascending order.</returns>
    public static IReadOnlyList<int> FromDaysMask(int daysMask)
    {
        var days = new List<int>(7);
        for (var day = 1; day <= 7; day++)
        {
            if ((daysMask & (1 << (day - 1))) != 0)
            {
                days.Add(day);
            }
        }

        return days;
    }

    /// <summary>
    /// Resolves an IANA time zone id.
    /// </summary>
    /// <param name="timeZoneId">The IANA zone id, for example <c>Europe/Berlin</c>.</param>
    /// <returns>The zone, or <see langword="null" /> for an unknown id or <see langword="null" /> input. Only the
    /// IANA database is consulted, never an operating system zone.</returns>
    public static DateTimeZone? FindZone(string? timeZoneId)
    {
        return string.IsNullOrWhiteSpace(timeZoneId)
            ? null
            : DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId);
    }

    /// <summary>
    /// Enumerates the occurrences of a weekly rule inside a half-open window.
    /// </summary>
    /// <param name="daysMask">The weekday mask; an empty mask yields nothing.</param>
    /// <param name="minuteOfDay">The wall time as minutes since local midnight, 0..1439.</param>
    /// <param name="zone">The zone the wall time is interpreted in.</param>
    /// <param name="fromExclusive">The window start; an occurrence at exactly this instant is not returned.</param>
    /// <param name="toInclusive">The window end; an occurrence at exactly this instant is returned.</param>
    /// <returns>The occurrence instants in ascending order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zone" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minuteOfDay" /> is outside 0..1439.</exception>
    public static IEnumerable<Instant> OccurrencesBetween(
        int daysMask,
        int minuteOfDay,
        DateTimeZone zone,
        Instant fromExclusive,
        Instant toInclusive)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (minuteOfDay is < 0 or >= MinutesPerDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minuteOfDay),
                minuteOfDay,
                "The minute of the day must be 0..1439.");
        }

        return OccurrencesBetweenCore(daysMask & AllDaysMask, minuteOfDay, zone, fromExclusive, toInclusive);
    }

    /// <summary>
    /// Finds the first occurrence of a weekly rule strictly after an instant.
    /// </summary>
    /// <param name="daysMask">The weekday mask.</param>
    /// <param name="minuteOfDay">The wall time as minutes since local midnight, 0..1439.</param>
    /// <param name="zone">The zone the wall time is interpreted in.</param>
    /// <param name="afterExclusive">The instant after which the occurrence must lie.</param>
    /// <returns>The next occurrence, or <see langword="null" /> only for an empty mask. At most eight days are
    /// scanned, which always contains an occurrence of a non-empty weekly rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zone" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minuteOfDay" /> is outside 0..1439.</exception>
    public static Instant? NextAfter(int daysMask, int minuteOfDay, DateTimeZone zone, Instant afterExclusive)
    {
        if ((daysMask & AllDaysMask) == 0)
        {
            return null;
        }

        foreach (var occurrence in OccurrencesBetween(
                     daysMask,
                     minuteOfDay,
                     zone,
                     afterExclusive,
                     afterExclusive + Duration.FromDays(8)))
        {
            return occurrence;
        }

        return null;
    }

    private static IEnumerable<Instant> OccurrencesBetweenCore(
        int daysMask,
        int minuteOfDay,
        DateTimeZone zone,
        Instant fromExclusive,
        Instant toInclusive)
    {
        if (daysMask == 0 || toInclusive <= fromExclusive)
        {
            yield break;
        }

        var time = LocalTime.FromMinutesSinceMidnight(minuteOfDay);
        // One day of slack on each side covers wall times that the resolver moves across a date boundary.
        var firstDate = fromExclusive.InZone(zone).Date.PlusDays(-1);
        var lastDate = toInclusive.InZone(zone).Date.PlusDays(1);
        for (var date = firstDate; date <= lastDate; date = date.PlusDays(1))
        {
            if ((daysMask & (1 << ((int)date.DayOfWeek - 1))) == 0)
            {
                continue;
            }

            var instant = zone.ResolveLocal(date.At(time), Resolvers.LenientResolver).ToInstant();
            if (instant > fromExclusive && instant <= toInclusive)
            {
                yield return instant;
            }
        }
    }
}

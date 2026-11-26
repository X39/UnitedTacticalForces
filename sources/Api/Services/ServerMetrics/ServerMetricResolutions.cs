namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// The rollup resolutions of stored server metrics and the window alignment they share.
/// </summary>
public static class ServerMetricResolutions
{
    /// <summary>
    /// The finest resolution in seconds, equal to the worker report interval.
    /// </summary>
    public const int Raw = 15;

    /// <summary>
    /// The one-minute resolution in seconds.
    /// </summary>
    public const int Minute = 60;

    /// <summary>
    /// The five-minute resolution in seconds.
    /// </summary>
    public const int FiveMinutes = 300;

    /// <summary>
    /// Gets every stored resolution in seconds, finest first.
    /// </summary>
    /// <value>The raw, minute and five-minute resolutions.</value>
    public static IReadOnlyList<int> All { get; } = [Raw, Minute, FiveMinutes];

    /// <summary>
    /// Aligns an instant to the start of its window.
    /// </summary>
    /// <param name="instant">The instant to align.</param>
    /// <param name="resolutionSeconds">The window length in seconds; must be positive.</param>
    /// <returns>The start of the window, counted in whole seconds since the Unix epoch.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resolutionSeconds"/> is not positive.</exception>
    public static Instant Floor(Instant instant, int resolutionSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(resolutionSeconds, 0);
        var seconds   = instant.ToUnixTimeSeconds();
        var remainder = ((seconds % resolutionSeconds) + resolutionSeconds) % resolutionSeconds;
        return Instant.FromUnixTimeSeconds(seconds - remainder);
    }

    /// <summary>
    /// Gets the resolution of the tier that serves a history range.
    /// </summary>
    /// <param name="range">The requested range.</param>
    /// <returns>15 for an hour, 60 for a day and 300 for a week.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is not a defined range.</exception>
    public static int For(EServerMetricsRange range)
        => range switch
        {
            EServerMetricsRange.Hour => Raw,
            EServerMetricsRange.Day  => Minute,
            EServerMetricsRange.Week => FiveMinutes,
            _                        => throw new ArgumentOutOfRangeException(nameof(range), range, null),
        };

    /// <summary>
    /// Gets how far back a history range reaches.
    /// </summary>
    /// <param name="range">The requested range.</param>
    /// <returns>One hour, 24 hours or seven days.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is not a defined range.</exception>
    public static Duration Span(EServerMetricsRange range)
        => range switch
        {
            EServerMetricsRange.Hour => Duration.FromHours(1),
            EServerMetricsRange.Day  => Duration.FromHours(24),
            EServerMetricsRange.Week => Duration.FromDays(7),
            _                        => throw new ArgumentOutOfRangeException(nameof(range), range, null),
        };

    /// <summary>
    /// Parses a range code; only <c>1h</c>, <c>24h</c> and <c>7d</c> are accepted, case-sensitively.
    /// </summary>
    /// <param name="value">The code to parse, or <see langword="null"/>.</param>
    /// <param name="range">The parsed range, or <see cref="EServerMetricsRange.Hour"/> when parsing fails.</param>
    /// <returns><see langword="true"/> when the code is one of the three accepted codes.</returns>
    public static bool TryParse(string? value, out EServerMetricsRange range)
    {
        switch (value)
        {
            case "1h":
                range = EServerMetricsRange.Hour;
                return true;
            case "24h":
                range = EServerMetricsRange.Day;
                return true;
            case "7d":
                range = EServerMetricsRange.Week;
                return true;
            default:
                range = EServerMetricsRange.Hour;
                return false;
        }
    }

    /// <summary>
    /// Formats a range as its code.
    /// </summary>
    /// <param name="range">The range to format.</param>
    /// <returns><c>1h</c>, <c>24h</c> or <c>7d</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is not a defined range.</exception>
    public static string Format(EServerMetricsRange range)
        => range switch
        {
            EServerMetricsRange.Hour => "1h",
            EServerMetricsRange.Day  => "24h",
            EServerMetricsRange.Week => "7d",
            _                        => throw new ArgumentOutOfRangeException(nameof(range), range, null),
        };
}

namespace Api.Tests;

internal static class NodaTimeTestExtensions
{
    public static Instant Add(this Instant instant, TimeSpan duration)
        => instant.Plus(NodaTime.Duration.FromTimeSpan(duration));

    public static TimeSpan Duration(this NodaTime.Duration duration)
        => (duration < NodaTime.Duration.Zero ? -duration : duration).ToTimeSpan();

    public static Instant TruncateToMillisecond(this Instant instant)
        => Instant.FromUnixTimeMilliseconds(instant.ToUnixTimeMilliseconds());
}

namespace X39.UnitedTacticalForces.Api.ExtensionMethods;

internal static class NodaTimeCompatibilityExtensions
{
    public static Instant Add(this Instant instant, TimeSpan duration)
        => instant.Plus(NodaTime.Duration.FromTimeSpan(duration));

    public static Instant Subtract(this Instant instant, TimeSpan duration)
        => instant.Minus(NodaTime.Duration.FromTimeSpan(duration));

    public static NodaTime.Duration Duration(this NodaTime.Duration duration)
        => duration < NodaTime.Duration.Zero ? -duration : duration;

    public static System.Text.Json.JsonSerializerOptions ConfigureApiNodaTime(
        this System.Text.Json.JsonSerializerOptions options)
    {
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return options;
    }
}

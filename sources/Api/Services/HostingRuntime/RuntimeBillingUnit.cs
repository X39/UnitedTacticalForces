using System.Numerics;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// The granularity a runtime provider bills at: every started unit of <see cref="Length"/> is billed in full.
/// </summary>
/// <remarks>
/// A provider declares its unit through <see cref="RuntimeProviderCapabilities.BillingUnit"/>. The unit is
/// provider-neutral: Hetzner Cloud bills every started hour (<see cref="StartedHour"/>), while another provider may
/// bill every started minute or second (<see cref="PerStarted"/>). The unit only rounds the billed time; it never
/// changes the charged ledger amounts.
/// </remarks>
public sealed record RuntimeBillingUnit
{
    /// <summary>
    /// The number of nanoseconds in one day.
    /// </summary>
    private const long NanosecondsPerDay = 86_400_000_000_000L;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeBillingUnit"/> record.
    /// </summary>
    /// <param name="length">The positive length of one billed unit.</param>
    private RuntimeBillingUnit(Duration length)
    {
        Length = length;
    }

    /// <summary>
    /// Gets the length of one billed unit; every started unit is billed in full.
    /// </summary>
    /// <value>A positive duration.</value>
    public Duration Length { get; }

    /// <summary>
    /// Gets the unit of a provider that bills every started hour.
    /// </summary>
    /// <value>A unit whose <see cref="Length"/> is one hour.</value>
    public static RuntimeBillingUnit StartedHour { get; } = new(Duration.FromHours(1));

    /// <summary>
    /// Creates the unit of a provider that bills every started <paramref name="length"/>.
    /// </summary>
    /// <param name="length">The positive length of one billed unit.</param>
    /// <returns>The unit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is zero or negative.
    /// </exception>
    public static RuntimeBillingUnit PerStarted(Duration length)
    {
        if (length <= Duration.Zero)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The billing unit must be positive.");
        return new RuntimeBillingUnit(length);
    }

    /// <summary>
    /// Returns the end of the last started unit at <paramref name="asOf"/>, counted from <paramref name="anchor"/>.
    /// </summary>
    /// <param name="anchor">The instant billing starts at.</param>
    /// <param name="asOf">The instant the billed time is measured at.</param>
    /// <returns>
    /// <paramref name="anchor"/> when <paramref name="asOf"/> is before it; otherwise the anchor plus the number of
    /// started units, at least one, times <see cref="Length"/>.
    /// </returns>
    public Instant BilledThrough(Instant anchor, Instant asOf)
    {
        if (asOf < anchor)
            return anchor;

        var elapsed = ToNanoseconds(asOf - anchor);
        var unit = ToNanoseconds(Length);
        var started = BigInteger.DivRem(elapsed, unit, out var remainder);
        if (remainder > BigInteger.Zero)
            started += BigInteger.One;
        if (started < BigInteger.One)
            started = BigInteger.One;
        return anchor + Length * (long) started;
    }

    /// <summary>
    /// Converts a duration to whole nanoseconds without overflow.
    /// </summary>
    /// <param name="duration">The duration to convert.</param>
    /// <returns>The duration in nanoseconds.</returns>
    private static BigInteger ToNanoseconds(Duration duration)
        => new BigInteger(duration.Days) * NanosecondsPerDay + duration.NanosecondOfDay;
}

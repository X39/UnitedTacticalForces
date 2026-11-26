namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Limits and timings of automatic server stops and starts, bound from the <c>Hosting:Automation</c> section.
/// </summary>
public sealed class ServerAutomationOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Hosting:Automation";

    /// <summary>
    /// Gets or sets how often the control plane evaluates idle, runtime, and schedule rules.
    /// </summary>
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the warning lead. An idle or maximum-runtime stop happens one lead after the limit is reached,
    /// and a scheduled stop is warned one lead before its time (D-AUTO-1).
    /// </summary>
    public TimeSpan WarningLead { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets how long after its instant a scheduled start is still executed instead of being recorded as
    /// missed. The window applies to a scheduled start that is first seen after its instant, for example when the
    /// platform was not running; a start that came due on time while a stop of the server was in flight waits for that
    /// stop instead and is not limited by this window.
    /// </summary>
    public TimeSpan StartGraceWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the smallest idle time, in minutes, an owner may configure.
    /// </summary>
    public int IdleMinutesMin { get; set; } = 5;

    /// <summary>
    /// Gets or sets the largest idle time, in minutes, an owner may configure.
    /// </summary>
    public int IdleMinutesMax { get; set; } = 1440;

    /// <summary>
    /// Gets or sets the smallest maximum runtime, in hours, an owner may configure.
    /// </summary>
    public int MaxRuntimeHoursMin { get; set; } = 1;

    /// <summary>
    /// Gets or sets the largest maximum runtime, in hours, an owner may configure.
    /// </summary>
    public int MaxRuntimeHoursMax { get; set; } = 168;

    /// <summary>
    /// Gets or sets the maximum number of schedule rules one server may hold.
    /// </summary>
    public int MaxSchedules { get; set; } = 8;

    /// <summary>
    /// Gets or sets the idle time, in minutes, proposed when idle shutdown is first switched on.
    /// </summary>
    public int DefaultIdleMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum runtime, in hours, proposed when the runtime limit is first switched on.
    /// </summary>
    public int DefaultMaxRuntimeHours { get; set; } = 6;

    /// <summary>
    /// Checks the options for unusable values.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The evaluation interval is not positive, the warning lead is outside 30 seconds to 60 minutes, the start grace
    /// window is negative, a minimum is below one or above its maximum, the schedule limit is outside 1..32, or a
    /// default lies outside its range.
    /// </exception>
    public void Validate()
    {
        if (EvaluationInterval <= TimeSpan.Zero
            || WarningLead < TimeSpan.FromSeconds(30)
            || WarningLead > TimeSpan.FromMinutes(60)
            || StartGraceWindow < TimeSpan.Zero
            || IdleMinutesMin < 1
            || IdleMinutesMin > IdleMinutesMax
            || MaxRuntimeHoursMin < 1
            || MaxRuntimeHoursMin > MaxRuntimeHoursMax
            || MaxSchedules is < 1 or > 32
            || DefaultIdleMinutes < IdleMinutesMin
            || DefaultIdleMinutes > IdleMinutesMax
            || DefaultMaxRuntimeHours < MaxRuntimeHoursMin
            || DefaultMaxRuntimeHours > MaxRuntimeHoursMax)
        {
            throw new InvalidOperationException("Hosting:Automation contains invalid settings.");
        }
    }
}

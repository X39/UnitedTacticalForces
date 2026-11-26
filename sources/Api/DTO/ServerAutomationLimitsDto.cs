namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The limits the control plane enforces on server automation settings.
/// </summary>
public sealed record ServerAutomationLimitsDto
{
    /// <summary>
    /// Gets the smallest idle limit.
    /// </summary>
    /// <value>The minimum number of idle minutes.</value>
    public required int IdleMinutesMin { get; init; }

    /// <summary>
    /// Gets the largest idle limit.
    /// </summary>
    /// <value>The maximum number of idle minutes.</value>
    public required int IdleMinutesMax { get; init; }

    /// <summary>
    /// Gets the smallest maximum runtime.
    /// </summary>
    /// <value>The minimum number of hours.</value>
    public required int MaxRuntimeHoursMin { get; init; }

    /// <summary>
    /// Gets the largest maximum runtime.
    /// </summary>
    /// <value>The maximum number of hours.</value>
    public required int MaxRuntimeHoursMax { get; init; }

    /// <summary>
    /// Gets the largest number of schedule rules one server may hold.
    /// </summary>
    /// <value>The rule limit.</value>
    public required int MaxSchedules { get; init; }

    /// <summary>
    /// Gets the warning lead: an automatic stop is warned this long before it happens.
    /// </summary>
    /// <value>The lead in whole minutes, rounded up.</value>
    public required int WarningLeadMinutes { get; init; }
}

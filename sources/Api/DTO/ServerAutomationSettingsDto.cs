namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The saved automation settings of one game server.
/// </summary>
public sealed record ServerAutomationSettingsDto
{
    /// <summary>
    /// Gets whether a running server is stopped after it stayed without players.
    /// </summary>
    /// <value><see langword="true"/> when idle shutdown is switched on.</value>
    public required bool IdleShutdownEnabled { get; init; }

    /// <summary>
    /// Gets the minutes without connected players after which idle shutdown applies.
    /// </summary>
    /// <value>The idle limit in minutes; the proposed default while nothing was saved.</value>
    public required int IdleShutdownMinutes { get; init; }

    /// <summary>
    /// Gets whether a session is stopped after it ran for a maximum duration.
    /// </summary>
    /// <value><see langword="true"/> when the maximum runtime limit is switched on.</value>
    public required bool MaxRuntimeEnabled { get; init; }

    /// <summary>
    /// Gets the hours after which the maximum runtime limit applies.
    /// </summary>
    /// <value>The runtime limit in hours; the proposed default while nothing was saved.</value>
    public required int MaxRuntimeHours { get; init; }

    /// <summary>
    /// Gets the IANA time zone in which the schedule rules are interpreted.
    /// </summary>
    /// <value>The zone identifier such as <c>Europe/Berlin</c>, or <see langword="null"/> when none was chosen.</value>
    public string? TimeZone { get; init; }

    /// <summary>
    /// Gets the weekly schedule rules in display order.
    /// </summary>
    /// <value>The rules, or an empty list when the server has none.</value>
    public required IReadOnlyList<ServerScheduleDto> Schedules { get; init; }

    /// <summary>
    /// Gets the revision of the saved settings.
    /// </summary>
    /// <value><c>0</c> while nothing was saved, otherwise the counter to send back unchanged on the next save.</value>
    public required int Version { get; init; }
}

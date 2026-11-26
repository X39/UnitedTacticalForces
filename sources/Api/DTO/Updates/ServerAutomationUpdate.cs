using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// A full replacement of the automation settings and weekly schedule rules of one game server.
/// </summary>
/// <remarks>
/// One save replaces everything: a rule that is missing from <see cref="Schedules" /> is deleted, a rule without an
/// identifier is created.
/// </remarks>
public sealed record ServerAutomationUpdate
{
    /// <summary>
    /// Gets the version the editor loaded.
    /// </summary>
    /// <value>
    /// The version of the read this save is based on, zero for a server that never saved automation settings. A save
    /// with a different version is rejected as a conflict.
    /// </value>
    public int Version { get; init; }

    /// <summary>
    /// Gets whether a running server is stopped after it stayed without connected players.
    /// </summary>
    /// <value><see langword="true" /> to enable idle shutdown; only games that report players support it.</value>
    public bool IdleShutdownEnabled { get; init; }

    /// <summary>
    /// Gets the number of minutes without connected players after which the idle shutdown applies.
    /// </summary>
    /// <value>
    /// A value within the configured idle limits when idle shutdown is enabled. While it is disabled, an out-of-range
    /// value is ignored and the stored value is kept.
    /// </value>
    public int IdleShutdownMinutes { get; init; }

    /// <summary>
    /// Gets whether a session is stopped after it ran for the maximum runtime.
    /// </summary>
    /// <value><see langword="true" /> to enable the maximum runtime limit.</value>
    public bool MaxRuntimeEnabled { get; init; }

    /// <summary>
    /// Gets the number of hours after which the maximum runtime limit applies.
    /// </summary>
    /// <value>
    /// A value within the configured runtime limits when the limit is enabled. While it is disabled, an out-of-range
    /// value is ignored and the stored value is kept.
    /// </value>
    public int MaxRuntimeHours { get; init; }

    /// <summary>
    /// Gets the IANA time zone in which the weekly rules are interpreted.
    /// </summary>
    /// <value>An IANA zone identifier such as <c>Europe/Berlin</c>; required when any rule exists.</value>
    [MaxLength(64)]
    public string? TimeZone { get; init; }

    /// <summary>
    /// Gets the complete list of weekly schedule rules.
    /// </summary>
    /// <value>The rules in display order; at most the configured number of rules.</value>
    public IReadOnlyList<ServerScheduleRuleUpdate> Schedules { get; init; } = [];

    /// <summary>
    /// Gets whether the saving user confirms that scheduled starts may incur cost for the current cost owner.
    /// </summary>
    /// <value>
    /// <see langword="true" /> to record the consent; <see langword="false" /> records nothing and never revokes an
    /// existing consent.
    /// </value>
    public bool StartConsent { get; init; }
}

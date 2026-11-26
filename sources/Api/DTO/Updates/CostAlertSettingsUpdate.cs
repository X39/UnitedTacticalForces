namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// A full replacement of the cost alert configuration of one cost owner.
/// </summary>
public sealed record CostAlertSettingsUpdate
{
    /// <summary>
    /// Gets the revision the editor loaded.
    /// </summary>
    /// <value>
    /// The revision of the read this save is based on; a save with a different revision is rejected as a conflict.
    /// </value>
    public int Revision { get; init; }

    /// <summary>
    /// Gets the complete list of alert amounts.
    /// </summary>
    /// <value>
    /// The absolute amounts to alert at. Each must be above zero, at most the configured maximum, carry at most two
    /// decimals and be unique; an empty list switches alerts off.
    /// </value>
    public IReadOnlyList<decimal> Thresholds { get; init; } = [];

    /// <summary>
    /// Gets whether alerts are delivered as Discord direct messages.
    /// </summary>
    /// <value><see langword="true"/> to enable the Discord channel.</value>
    public bool DiscordEnabled { get; init; }

    /// <summary>
    /// Gets whether alerts are delivered by email.
    /// </summary>
    /// <value><see langword="true"/> to enable the email channel.</value>
    public bool EmailEnabled { get; init; }

    /// <summary>
    /// Gets the interface language of the saving user.
    /// </summary>
    /// <value>
    /// A locale such as <c>de-DE</c> that is recorded on the saving user so messages are written in it, or
    /// <see langword="null"/> to leave the recorded language unchanged.
    /// </value>
    public string? Language { get; init; }
}

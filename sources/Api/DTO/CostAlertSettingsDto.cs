namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The cost alert configuration of one cost owner together with what the viewer may do with it and which channels
/// can currently deliver.
/// </summary>
/// <remarks>
/// The in-app channel is always on and therefore is not a field. An empty <see cref="Thresholds"/> list means no
/// alert fires for the cost owner; that is the state of an owner without a saved setting.
/// </remarks>
public sealed record CostAlertSettingsDto
{
    /// <summary>
    /// Gets the cost owner the settings belong to.
    /// </summary>
    /// <value>The primary key of the cost owner.</value>
    public required Guid CostOwnerId { get; init; }

    /// <summary>
    /// Gets the display name of the cost owner.
    /// </summary>
    /// <value>The name shown in the editor heading.</value>
    public required string CostOwnerName { get; init; }

    /// <summary>
    /// Gets the revision of the stored setting.
    /// </summary>
    /// <value>
    /// <c>0</c> while nothing was saved; otherwise the counter that is incremented on every save and must be sent
    /// back unchanged to save again.
    /// </value>
    public required int Revision { get; init; }

    /// <summary>
    /// Gets the alert amounts.
    /// </summary>
    /// <value>The absolute accrued amounts, in ascending order, at which an alert is raised; empty means none.</value>
    public required IReadOnlyList<decimal> Thresholds { get; init; }

    /// <summary>
    /// Gets whether alerts are delivered as Discord direct messages.
    /// </summary>
    /// <value><see langword="true"/> when the Discord channel is enabled for this owner.</value>
    public required bool DiscordEnabled { get; init; }

    /// <summary>
    /// Gets whether alerts are delivered by email.
    /// </summary>
    /// <value><see langword="true"/> when the email channel is enabled for this owner.</value>
    public required bool EmailEnabled { get; init; }

    /// <summary>
    /// Gets the currency of the thresholds.
    /// </summary>
    /// <value>The ISO currency code of the cost owner.</value>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets the largest number of thresholds a save may carry.
    /// </summary>
    /// <value>The configured limit on the number of alert amounts.</value>
    public required int MaxThresholds { get; init; }

    /// <summary>
    /// Gets the largest alert amount a save may carry.
    /// </summary>
    /// <value>The configured upper bound of one alert amount, in <see cref="Currency"/>.</value>
    public required decimal MaxThresholdAmount { get; init; }

    /// <summary>
    /// Gets whether the viewer may save changes.
    /// </summary>
    /// <value><see langword="true"/> when the viewer may use the cost owner.</value>
    public required bool CanManage { get; init; }

    /// <summary>
    /// Gets who the alerts of this owner are addressed to.
    /// </summary>
    /// <value><see cref="ECostAlertAudience.Self"/> for a personal owner, otherwise <see cref="ECostAlertAudience.Group"/>.</value>
    public required ECostAlertAudience Audience { get; init; }

    /// <summary>
    /// Gets whether the Discord channel can reach the recipients.
    /// </summary>
    /// <value>The availability of Discord direct messages for the current recipients.</value>
    public required ECostAlertChannelAvailability Discord { get; init; }

    /// <summary>
    /// Gets whether the email channel can reach the recipients.
    /// </summary>
    /// <value>The availability of email for the current recipients.</value>
    public required ECostAlertChannelAvailability Email { get; init; }

    /// <summary>
    /// Gets the language alert messages are written in.
    /// </summary>
    /// <value>
    /// <c>en</c> or <c>de</c> for a personal owner, from the language recorded on the user; <see langword="null"/>
    /// for a group owner, whose recipients each read their own language.
    /// </value>
    public string? MessageLanguage { get; init; }
}

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// The resource keys of the automatic stop warning texts in <c>Language.resx</c> and <c>Language.de.resx</c>.
/// </summary>
internal static class AutoStopMessageKeys
{
    /// <summary>
    /// The subject template. Placeholders: 0 server title, 1 stop time.
    /// </summary>
    public const string Subject = "AutoStop_Subject";

    /// <summary>
    /// The body sentence of an idle stop. Placeholders: 0 server title, 1 idle minutes, 2 stop time.
    /// </summary>
    public const string BodyIdle = "AutoStop_Body_Idle";

    /// <summary>
    /// The body sentence of a maximum runtime stop. Placeholders: 0 server title, 1 maximum hours, 2 stop time.
    /// </summary>
    public const string BodyMaxRuntime = "AutoStop_Body_MaxRuntime";

    /// <summary>
    /// The body sentence of a schedule stop. Placeholders: 0 server title, 2 stop time.
    /// </summary>
    public const string BodySchedule = "AutoStop_Body_Schedule";

    /// <summary>
    /// The sentence that explains what the stop does and when billing ends.
    /// </summary>
    public const string Consequence = "AutoStop_Consequence";

    /// <summary>
    /// The hint to change the automatic shutdown, used when a server link is known. Placeholder: 0 server link.
    /// </summary>
    public const string WithLink = "AutoStop_WithLink";

    /// <summary>
    /// The footer explaining why the recipient receives the message.
    /// </summary>
    public const string Footer = "AutoStop_Footer";
}

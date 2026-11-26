namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// The resource keys of the cost alert message texts in <c>Language.resx</c> and <c>Language.de.resx</c>.
/// </summary>
internal static class CostAlertMessageKeys
{
    /// <summary>
    /// The subject template. Placeholders: 0 server title, 1 threshold amount.
    /// </summary>
    public const string Subject = "CostAlert_Subject";

    /// <summary>
    /// The body sentence template. Placeholders: 0 server title, 1 threshold amount, 2 accrued amount, 3 time.
    /// </summary>
    public const string Body = "CostAlert_Body";

    /// <summary>
    /// The shutdown hint used when a server link is known. Placeholder: 0 server link.
    /// </summary>
    public const string StopWithLink = "CostAlert_StopWithLink";

    /// <summary>
    /// The shutdown hint used when no server link is known.
    /// </summary>
    public const string StopWithoutLink = "CostAlert_StopWithoutLink";

    /// <summary>
    /// The footer explaining why the recipient receives the message.
    /// </summary>
    public const string Footer = "CostAlert_Footer";
}

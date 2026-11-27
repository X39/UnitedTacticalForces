using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.Api.Properties;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Renders one cost alert to a localized subject and plain-text body from the API resource files.
/// </summary>
internal static partial class CostAlertMessageRenderer
{
    /// <summary>
    /// The maximum length of a Discord message in characters.
    /// </summary>
    public const int DiscordMessageLimit = 2000;

    /// <summary>
    /// Renders the cost alert in the language of the recipient.
    /// </summary>
    /// <param name="model">The facts of the alert.</param>
    /// <param name="locale">
    /// The recipient's recorded locale. <see langword="null" /> or an unsupported value renders English.
    /// </param>
    /// <returns>The localized subject and body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">A required resource key is missing from the resource files.</exception>
    public static CostAlertMessage Render(CostAlertMessageModel model, string? locale)
    {
        ArgumentNullException.ThrowIfNull(model);
        var culture = MessageLocales.ToCulture(locale);
        var title = SanitizeTitle(model.ServerTitle);
        var threshold = FormatAmount(model.ThresholdAmount, model.Currency, culture);
        var accrued = FormatAmount(model.AccruedAmount, model.Currency, culture);
        var time = FormatTime(model.CreatedAt, culture);

        var subject = string.Format(culture, GetResource(CostAlertMessageKeys.Subject, culture), title, threshold);
        var sentence = string.Format(
            culture,
            GetResource(CostAlertMessageKeys.Body, culture),
            title,
            threshold,
            accrued,
            time);
        var stop = model.ServerLink is null
            ? GetResource(CostAlertMessageKeys.StopWithoutLink, culture)
            : string.Format(culture, GetResource(CostAlertMessageKeys.StopWithLink, culture), model.ServerLink);
        var footer = GetResource(CostAlertMessageKeys.Footer, culture);

        var body = new StringBuilder()
            .Append(sentence)
            .Append("\n\n")
            .Append(stop)
            .Append("\n\n")
            .Append(footer)
            .ToString();
        return new CostAlertMessage(subject, body);
    }

    /// <summary>
    /// The path and fragment appended to the server link of an automatic stop warning, which opens the automation
    /// panel of the server settings.
    /// </summary>
    private const string AutomationLinkSuffix = "?tab=settings#server-automation";

    /// <summary>
    /// Renders the automatic stop warning in the language of the recipient. The text names the reason, the limit and
    /// the stop time and stays neutral about whether the stop will still happen, because it may be delivered after
    /// the stop was cancelled.
    /// </summary>
    /// <param name="model">The facts of the warning.</param>
    /// <param name="locale">
    /// The recipient's recorded locale. <see langword="null" /> or an unsupported value renders English.
    /// </param>
    /// <returns>The localized subject and body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The reason is not a known stop reason.</exception>
    /// <exception cref="InvalidOperationException">A required resource key is missing from the resource files.</exception>
    public static CostAlertMessage RenderAutoStop(AutoStopMessageModel model, string? locale)
    {
        ArgumentNullException.ThrowIfNull(model);
        var culture = MessageLocales.ToCulture(locale);
        var title = SanitizeTitle(model.ServerTitle);
        var time = FormatTime(model.StopsAt, culture);
        var bodyKey = model.Reason switch
        {
            EAutoStopReason.Idle => AutoStopMessageKeys.BodyIdle,
            EAutoStopReason.MaxRuntime => AutoStopMessageKeys.BodyMaxRuntime,
            EAutoStopReason.Schedule => AutoStopMessageKeys.BodySchedule,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.Reason, "Unknown stop reason."),
        };

        var subject = string.Format(culture, GetResource(AutoStopMessageKeys.Subject, culture), title, time);
        var sentence = string.Format(
            culture,
            GetResource(bodyKey, culture),
            title,
            model.Limit ?? 0,
            time);
        var builder = new StringBuilder()
            .Append(sentence)
            .Append("\n\n")
            .Append(GetResource(AutoStopMessageKeys.Consequence, culture))
            .Append("\n\n");
        if (model.ServerLink is not null)
        {
            builder
                .Append(string.Format(
                    culture,
                    GetResource(AutoStopMessageKeys.WithLink, culture),
                    model.ServerLink + AutomationLinkSuffix))
                .Append("\n\n");
        }

        builder.Append(GetResource(AutoStopMessageKeys.Footer, culture));
        return new CostAlertMessage(subject, builder.ToString());
    }

    /// <summary>
    /// Joins subject and body into the text of a Discord direct message.
    /// </summary>
    /// <param name="message">The rendered alert.</param>
    /// <returns>Subject, a blank line and the body, truncated to <see cref="DiscordMessageLimit" /> characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
    public static string ToDiscordText(CostAlertMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var text = message.Subject + "\n\n" + message.Body;
        return text.Length <= DiscordMessageLimit
            ? text
            : text[..DiscordMessageLimit];
    }

    /// <summary>
    /// Makes a user-supplied server title safe for a subject line or direct message by replacing every control
    /// character with a space, collapsing runs of whitespace and trimming.
    /// </summary>
    /// <param name="title">The raw server title.</param>
    /// <returns>A single-line title without control characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title" /> is <see langword="null" />.</exception>
    public static string SanitizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var builder = new StringBuilder(title.Length);
        foreach (var character in title)
            builder.Append(char.IsControl(character) ? ' ' : character);
        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static string FormatAmount(decimal amount, string currency, CultureInfo culture)
        => amount.ToString("N2", culture) + " " + currency;

    /// <summary>
    /// Formats a UTC instant as the culture's short date and time followed by "UTC". ICU (Linux) puts a narrow
    /// no-break space before the day period ("6:30 PM") while Windows NLS uses a plain space; both are replaced with a
    /// plain space so the plain-text message is identical on every host.
    /// </summary>
    /// <param name="instant">The instant to format.</param>
    /// <param name="culture">The culture of the message.</param>
    /// <returns>The short date and time in UTC with plain spaces, suffixed with " UTC".</returns>
    private static string FormatTime(Instant instant, CultureInfo culture)
        => instant.ToDateTimeUtc().ToString("g", culture).Replace(' ', ' ').Replace(' ', ' ') + " UTC";

    private static string GetResource(string key, CultureInfo culture)
        => Language.ResourceManager.GetString(key, culture)
           ?? throw new InvalidOperationException($"The resource key '{key}' is missing.");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

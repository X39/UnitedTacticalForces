using System.Globalization;

namespace X39.UnitedTacticalForces.Api.Services.Notifications;

/// <summary>
/// Maps a recipient's recorded locale to one of the languages the API has message resources for.
/// </summary>
internal static class MessageLocales
{
    /// <summary>
    /// The language code used when the recorded locale is missing or unsupported.
    /// </summary>
    public const string English = "en";

    /// <summary>
    /// The German language code.
    /// </summary>
    public const string German = "de";

    /// <summary>
    /// Reduces a locale value such as <c>de-DE</c> or <c>en_GB</c> to a supported language code.
    /// </summary>
    /// <param name="value">The recorded locale, possibly <see langword="null" /> or blank.</param>
    /// <returns><c>en</c> or <c>de</c>; <see langword="null" /> when the value is empty or not supported.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        var separator = trimmed.IndexOfAny(['-', '_']);
        var language = (separator < 0 ? trimmed : trimmed[..separator]).ToLowerInvariant();
        return language switch
        {
            English => English,
            German  => German,
            _       => null,
        };
    }

    /// <summary>
    /// Resolves the culture used to look up resources and format numbers and dates for a recipient.
    /// </summary>
    /// <param name="locale">The recorded locale, possibly <see langword="null" /> or unsupported.</param>
    /// <returns>The German culture for <c>de</c> locales and the English culture for everything else.</returns>
    public static CultureInfo ToCulture(string? locale)
        => Normalize(locale) is German
            ? CultureInfo.GetCultureInfo(German)
            : CultureInfo.GetCultureInfo(English);
}

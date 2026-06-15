using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Singleton<CommunityConnectionConfiguration>]
public sealed class CommunityConnectionConfiguration
{
    public CommunityConnectionConfiguration(IConfiguration configuration)
    {
        DiscordUrl = GetValidUrl(
            configuration[Constants.Configuration.Urls.Discord],
            uri => uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            "inviteurl"
        );
        TeamSpeak3Url = GetValidUrl(
            configuration[Constants.Configuration.Urls.TeamSpeak3],
            uri => uri.Scheme.Equals("ts3server", StringComparison.OrdinalIgnoreCase),
            "host-url-or-ip"
        );
    }

    public string? DiscordUrl { get; }

    public string? TeamSpeak3Url { get; }

    public bool HasAny => DiscordUrl is not null || TeamSpeak3Url is not null;

    private static string? GetValidUrl(
        string? configuredUrl,
        Func<Uri, bool> isAllowedScheme,
        string placeholderMarker)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl)
            || configuredUrl.Contains(placeholderMarker, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !isAllowedScheme(uri))
            return null;

        return uri.ToString();
    }
}

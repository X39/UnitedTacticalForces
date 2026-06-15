namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Installation-owned settings for the Hetzner Cloud runtime provider. The token belongs in a secret
/// configuration source and is never included in provider labels, logs, or result messages.
/// </summary>
public sealed class HetznerRuntimeOptions
{
    public const string SectionName = "Hosting:Providers:Hetzner";
    internal const string OfficialApiHost = "api.hetzner.cloud";

    public string Token { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.hetzner.cloud/v1/";

    /// <summary>
    /// Allows a non-Hetzner HTTPS endpoint for isolated tests. Normal installation configuration must leave this
    /// disabled so the provider token cannot be sent outside the official Hetzner API host.
    /// </summary>
    internal bool AllowCustomEndpointForTesting { get; set; }

    public string ServerType { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Conservative combined hourly estimate for the dedicated server and explicit Primary IPv4.
    /// </summary>
    public decimal EstimatedHourlyCostEur { get; set; }

    internal string? ValidateConnection()
    {
        if (string.IsNullOrWhiteSpace(Token))
            return $"{SectionName}:Token must be configured through a secret configuration source.";
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return $"{SectionName}:BaseUrl must be an absolute HTTPS URL.";
        }
        if (!string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            return $"{SectionName}:BaseUrl must not contain user information, a query, or a fragment.";
        }
        if (!AllowCustomEndpointForTesting
            && (!string.Equals(baseUri.IdnHost, OfficialApiHost, StringComparison.OrdinalIgnoreCase)
                || !baseUri.IsDefaultPort))
        {
            return $"{SectionName}:BaseUrl must use the official Hetzner API endpoint.";
        }
        return null;
    }

    internal string? ValidateProvisioning()
    {
        var connectionFailure = ValidateConnection();
        if (connectionFailure is not null)
            return connectionFailure;
        if (string.IsNullOrWhiteSpace(ServerType))
            return $"{SectionName}:ServerType must be configured.";
        if (string.IsNullOrWhiteSpace(Image))
            return $"{SectionName}:Image must be configured.";
        if (string.IsNullOrWhiteSpace(Location))
            return $"{SectionName}:Location must be configured.";
        if (EstimatedHourlyCostEur <= 0)
            return $"{SectionName}:EstimatedHourlyCostEur must be greater than zero.";
        return null;
    }

    internal Uri GetBaseUri()
    {
        var validationFailure = ValidateConnection();
        if (validationFailure is not null)
            throw new InvalidOperationException(validationFailure);
        var value = BaseUrl.Trim();
        if (!value.EndsWith("/", StringComparison.Ordinal))
            value += '/';
        return new Uri(value, UriKind.Absolute);
    }
}
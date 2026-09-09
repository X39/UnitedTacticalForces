using System.Globalization;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Resolves Steam Workshop detail URLs and verifies their metadata.</summary>
[Scoped<SteamWorkshopModSourceProvider, IModSourceProvider>]
public sealed class SteamWorkshopModSourceProvider(ISteamWorkshopMetadataClient metadataClient) : IModSourceProvider
{
    /// <inheritdoc />
    public string Identifier => ModSourceProviderIdentifiers.SteamWorkshop;

    /// <inheritdoc />
    public bool CanResolve(string input) => TryGetIdentifier(input, out _);

    /// <inheritdoc />
    public async Task<ModSourceResolution> ResolveAsync(
        string input,
        string? versionSelector,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentifier(input, out var identifier))
            throw new ArgumentException("A valid Steam Workshop detail URL or positive item ID is required.", nameof(input));
        try
        {
            var metadata = (await metadataClient.GetDetailsAsync([identifier], cancellationToken).ConfigureAwait(false)).Single();
            if (!string.Equals(metadata.Status, "available", StringComparison.Ordinal))
                throw new ArgumentException("The Steam Workshop item does not exist or is inaccessible.", nameof(input));
            return new(Identifier, identifier, metadata.Title?.Trim() ?? $"Steam Workshop {identifier}",
                versionSelector?.Trim() ?? string.Empty);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            throw new ModSourceUnavailableException(Identifier, exception);
        }
    }

    private static bool TryGetIdentifier(string input, out string identifier)
    {
        identifier = string.Empty;
        if (ulong.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var raw) && raw > 0)
        {
            identifier = raw.ToString(CultureInfo.InvariantCulture);
            return true;
        }
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.Equals("www.steamcommunity.com", StringComparison.OrdinalIgnoreCase))
            || !uri.AbsolutePath.TrimEnd('/').Equals("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase))
            return false;
        var value = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Split('=', 2)).FirstOrDefault(q => q.Length == 2 && q[0].Equals("id", StringComparison.OrdinalIgnoreCase));
        if (value is null || !ulong.TryParse(Uri.UnescapeDataString(value[1]), out raw) || raw == 0)
            return false;
        identifier = raw.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}

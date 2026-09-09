using System.Globalization;
using System.Text.Json;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

[Scoped<SteamWorkshopMetadataClient, ISteamWorkshopMetadataClient>]
internal sealed class SteamWorkshopMetadataClient(IHttpClientFactory httpClientFactory)
    : ISteamWorkshopMetadataClient
{
    private static readonly Uri Endpoint = new(
        "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/");

    public async Task<IReadOnlyCollection<SteamWorkshopMetadata>> GetDetailsAsync(
        IReadOnlyCollection<string> itemIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (itemIdentifiers.Count == 0)
            return [];
        var identifiers = itemIdentifiers
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToArray();
        var fields = new List<KeyValuePair<string, string>>(identifiers.Length + 1)
        {
            new("itemcount", identifiers.Length.ToString(CultureInfo.InvariantCulture)),
        };
        fields.AddRange(identifiers.Select((identifier, index) =>
            new KeyValuePair<string, string>($"publishedfileids[{index}]", identifier)));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        using var response = await httpClientFactory.CreateClient()
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("response", out var responseElement)
            || !responseElement.TryGetProperty("publishedfiledetails", out var details)
            || details.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidDataException("Steam Workshop metadata returned an invalid response.");
        }

        var result = new List<SteamWorkshopMetadata>();
        foreach (var detail in details.EnumerateArray())
        {
            var identifier = ReadString(detail, "publishedfileid");
            if (string.IsNullOrWhiteSpace(identifier) || !identifiers.Contains(identifier, StringComparer.Ordinal))
                continue;
            var resultCode = ReadInt64(detail, "result");
            var size = ReadInt64(detail, "file_size");
            var updated = ReadInt64(detail, "time_updated");
            result.Add(new SteamWorkshopMetadata(
                identifier,
                resultCode == 1 && size is >= 0 ? size : null,
                resultCode == 1 ? "available" : resultCode == 9 ? "deleted-or-inaccessible" : "unavailable",
                updated is > 0 ? updated.Value.ToString(CultureInfo.InvariantCulture) : null,
                updated is > 0 ? Instant.FromUnixTimeSeconds(updated.Value) : null,
                ReadString(detail, "title")));
        }
        foreach (var missing in identifiers.Except(result.Select(q => q.ItemIdentifier), StringComparer.Ordinal))
            result.Add(new SteamWorkshopMetadata(missing, null, "unavailable", null, null));
        return result;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind is JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static long? ReadInt64(JsonElement element, string name)
        => long.TryParse(ReadString(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

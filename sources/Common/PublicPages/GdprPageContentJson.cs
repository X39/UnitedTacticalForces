using System.Text.Json;

namespace X39.UnitedTacticalForces.Common.PublicPages;

public static class GdprPageContentJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize(GdprPageContent content)
        => JsonSerializer.Serialize(GdprPageContentNormalizer.Normalize(content), SerializerOptions);

    public static bool TryDeserialize(string? contentJson, out GdprPageContent content)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            content = new GdprPageContent();
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<GdprPageContent>(contentJson, SerializerOptions);
            if (parsed is null)
            {
                content = new GdprPageContent();
                return false;
            }

            content = GdprPageContentNormalizer.Normalize(parsed);
            return true;
        }
        catch (JsonException)
        {
            content = new GdprPageContent();
            return false;
        }
    }

    public static bool TryCanonicalize(string? contentJson, out string canonicalContentJson)
    {
        if (!TryDeserialize(contentJson, out var content))
        {
            canonicalContentJson = string.Empty;
            return false;
        }

        canonicalContentJson = Serialize(content);
        return true;
    }
}
namespace X39.UnitedTacticalForces.Api.Helpers;

public static class ImageMimeType
{
    private const string DefaultMimeType = "image/png";

    private static readonly string[] AllowedMimeTypes =
    [
        "image/avif",
        "image/bmp",
        "image/gif",
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    public static string? Normalize(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;

        var normalized = mimeType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return AllowedMimeTypes.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : null;
    }

    public static string NormalizeOrDefault(string? mimeType)
        => Normalize(mimeType) ?? DefaultMimeType;

    public static string? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";
        if (content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8))
            return "image/gif";
        if (content.StartsWith("BM"u8))
            return "image/bmp";
        if (content.Length >= 12
            && content[..4].SequenceEqual("RIFF"u8)
            && content.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        if (content.Length >= 12
            && content.Slice(4, 4).SequenceEqual("ftyp"u8)
            && (content.Slice(8, 4).SequenceEqual("avif"u8)
                || content.Slice(8, 4).SequenceEqual("avis"u8)))
            return "image/avif";

        return null;
    }
}

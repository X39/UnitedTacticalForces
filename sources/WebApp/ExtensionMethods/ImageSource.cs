using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class ImageSource
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

    public static string? NormalizeMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;

        var normalized = mimeType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return AllowedMimeTypes.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : null;
    }

    public static string NormalizeMimeTypeOrDefault(string? mimeType)
        => NormalizeMimeType(mimeType) ?? DefaultMimeType;

    public static string? Create(string? mimeType, byte[]? image)
    {
        if (image is null || image.Length is 0)
            return null;

        return $"data:{NormalizeMimeTypeOrDefault(mimeType)};base64,{Convert.ToBase64String(image)}";
    }

    public static string? ToImageSource(this FullEventDto? self) => self?.ImagePath;

    public static string? ToTerrainImageSource(this FullEventDto? self) => self?.TerrainImagePath;

    public static string? ToImageSource(this UpcomingEventDto? self) => self?.ImagePath;
    public static string? ToImageSource(this UserDto? self) => Create(self?.AvatarMimeType, self?.Avatar);

    public static string? ToImageSource(this PlainUserDto? self) => Create(self?.AvatarMimeType, self?.Avatar);

    public static string? ToImageSource(this FullUserDto? self) => Create(self?.AvatarMimeType, self?.Avatar);

    public static string? ToImageSource(this CommandCenterCommunityMemberInfo? self) => Create(self?.AvatarMimeType, self?.Avatar);
}

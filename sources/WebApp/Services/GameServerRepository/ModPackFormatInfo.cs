namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>Describes a game-owned mod-pack import and export format.</summary>
public sealed record ModPackFormatInfo
{
    /// <summary>Gets the stable format identifier.</summary>
    public string Identifier { get; init; } = string.Empty;
    /// <summary>Gets the format display name.</summary>
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Gets the conventional file extension.</summary>
    public string FileExtension { get; init; } = string.Empty;
    /// <summary>Gets the media type.</summary>
    public string MediaType { get; init; } = string.Empty;
    /// <summary>Gets the maximum accepted import size.</summary>
    public long MaximumSizeBytes { get; init; }
}

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Public description of a game-owned mod-pack format.</summary>
public sealed record ModPackFormatDto
{
    /// <summary>Gets the format identifier.</summary>
    public string Identifier { get; init; } = string.Empty;
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Gets the file extension.</summary>
    public string FileExtension { get; init; } = string.Empty;
    /// <summary>Gets the media type.</summary>
    public string MediaType { get; init; } = string.Empty;
    /// <summary>Gets the import size limit.</summary>
    public long MaximumSizeBytes { get; init; }
}

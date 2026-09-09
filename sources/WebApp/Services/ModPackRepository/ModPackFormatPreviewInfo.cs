namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Contains the reviewed result of importing a game-owned mod-pack format.</summary>
public sealed record ModPackFormatPreviewInfo
{
    /// <summary>Gets the imported title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets resolved entries.</summary>
    public IReadOnlyList<ModPackFormatPreviewEntryInfo> Entries { get; init; } = [];
    /// <summary>Gets non-fatal import warnings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

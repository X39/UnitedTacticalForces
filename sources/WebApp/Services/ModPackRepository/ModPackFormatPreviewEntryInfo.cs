namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents one source resolved from an imported mod-pack file.</summary>
public sealed record ModPackFormatPreviewEntryInfo
{
    /// <summary>Gets the validated provider input that can be submitted for mutation.</summary>
    public string SourceInput { get; init; } = string.Empty;
    /// <summary>Gets the imported title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets resolved source metadata.</summary>
    public ModSourceResolutionInfo Source { get; init; } = new();
    /// <summary>Gets an existing catalog match.</summary>
    public Guid? ExistingModId { get; init; }
}

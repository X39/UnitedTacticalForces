namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents a resolved mod source awaiting explicit catalog creation.</summary>
public sealed record ModSourcePreviewInfo
{
    /// <summary>Gets the validated provider input that can be submitted for mutation.</summary>
    public string SourceInput { get; init; } = string.Empty;
    /// <summary>Gets resolved source metadata.</summary>
    public ModSourceResolutionInfo Source { get; init; } = new();
    /// <summary>Gets an existing matching catalog mod, when present.</summary>
    public Guid? ExistingModId { get; init; }
    /// <summary>Gets installers compatible with the selected game.</summary>
    public IReadOnlyList<string> InstallerIdentifiers { get; init; } = [];
}

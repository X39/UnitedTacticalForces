namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Describes a normalized mod-pack definition and its immutable history.</summary>
public sealed record NormalizedModPackWorkspaceInfo
{
    /// <summary>Gets the definition identifier.</summary>
    public long DefinitionId { get; init; }
    /// <summary>Gets the owning game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the display title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets whether the definition is active.</summary>
    public bool IsActive { get; init; }
    /// <summary>Gets immutable revisions ordered newest first.</summary>
    public IReadOnlyList<NormalizedModPackRevisionInfo> Revisions { get; init; } = [];
}

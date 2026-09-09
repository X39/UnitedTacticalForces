namespace X39.UnitedTacticalForces.WebApp.Services;

/// <summary>Represents a selectable active normalized mod-pack revision.</summary>
public sealed record NormalizedModPackRevisionOption
{
    /// <summary>Gets the definition identifier.</summary>
    public long DefinitionId { get; init; }
    /// <summary>Gets the active revision identifier.</summary>
    public long RevisionId { get; init; }
    /// <summary>Gets the owning game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the mod-pack title.</summary>
    public string Title { get; init; } = string.Empty;
}

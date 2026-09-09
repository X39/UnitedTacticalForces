namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Describes a normalized mod-pack definition in a game-scoped list.</summary>
public sealed record NormalizedModPackListItemInfo
{
    /// <summary>Gets the definition identifier.</summary>
    public long DefinitionId { get; init; }
    /// <summary>Gets the owning game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the display title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the latest active revision identifier.</summary>
    public long? LatestRevisionId { get; init; }
}
